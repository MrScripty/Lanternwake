using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lanternwake.Core;

namespace Lanternwake.Conversation;

public enum AudioOperationOutcome { NotAdmitted, Completed, Unknown }
public sealed record AudioTextReply(bool Success, string Text, string Code, AudioOperationOutcome Outcome, string FinishReason = "");
public sealed record AudioTextCapability(bool Advertised, string Code, string Model = "", string Profile = "", int MaxRequestBytes = 0);

/// <summary>
/// Pumas's generic selected-model audio-input/text-output consumer.
/// Advertised capability is not qualification of installed audio. HTTP cancellation
/// settles only this transport: there is no public producer status/cancel receipt.
/// </summary>
public sealed class PumasAudioTextClient : IDisposable
{
    private const int MaxResponseBytes = 131072, MaxRequestBytes = 32 * 1024 * 1024;
    private readonly SpeechServiceSettings _settings;
    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _sync = new();
    private bool _active, _uncertain;
    private volatile bool _disposed;
    // Retained only until this request's transport settles; never written to disk.
    private byte[]? _audioBody;

    public PumasAudioTextClient(SpeechServiceSettings settings) : this(settings, new SocketsHttpHandler
    {
        AllowAutoRedirect = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(5),
        MaxConnectionsPerServer = 1
    }, TimeSpan.FromSeconds(90)) { }

    internal PumasAudioTextClient(SpeechServiceSettings settings, HttpMessageHandler handler, TimeSpan timeout)
    {
        _settings = settings.Validate();
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        _timeout = timeout;
        _http = new HttpClient(handler) { BaseAddress = new Uri(_settings.Endpoint), Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<AudioTextCapability> DiscoverAsync(CancellationToken cancellation = default)
    {
        var refused = Enter();
        if (refused is not null) return new(false, refused);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token);
        deadline.CancelAfter(_timeout);
        try { return await DiscoverCoreAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { return new(false, cancellation.IsCancellationRequested || _disposed ? "cancelled" : "timeout"); }
        catch (ProtocolException error) { return new(false, error.Code); }
        catch (Exception error) when (error is HttpRequestException or IOException) { return new(false, "pumas_unavailable"); }
        catch (JsonException) { return new(false, "invalid_response"); }
        finally { Exit(); }
    }

    public async Task<AudioTextReply> GenerateAsync(IReadOnlyList<StereoSample> samples, int sampleRate,
        CancellationToken cancellation = default)
    {
        var refused = Enter();
        if (refused is not null) return Failure(refused, AudioOperationOutcome.NotAdmitted);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token);
        deadline.CancelAfter(_timeout);
        var submitted = false;
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            // Pumas owns PCM conversion/normalization. Preserve actual mix rate and
            // both channels rather than labelling unconverted samples mono/16kHz.
            var frameCount = samples.Count;
            if (sampleRate is < 8000 or > 192000 || frameCount == 0 || frameCount > sampleRate * 30 ||
                frameCount > (24 * 1024 * 1024) / 8)
                return Failure("invalid_audio", AudioOperationOutcome.NotAdmitted);
            var pcm = new byte[checked(frameCount * 8)];
            try
            {
                for (var i = 0; i < frameCount; i++)
                {
                    if ((i & 4095) == 0) deadline.Token.ThrowIfCancellationRequested();
                    var frame = samples[i];
                    if (!float.IsFinite(frame.Left) || !float.IsFinite(frame.Right) ||
                        Math.Abs(frame.Left) > 1 || Math.Abs(frame.Right) > 1)
                        return Failure("invalid_audio", AudioOperationOutcome.NotAdmitted);
                    BinaryPrimitives.WriteSingleLittleEndian(pcm.AsSpan(i * 8, 4), frame.Left);
                    BinaryPrimitives.WriteSingleLittleEndian(pcm.AsSpan(i * 8 + 4, 4), frame.Right);
                }
                // Snapshot encoded audio before awaiting: subsequent caller mutation
                // cannot change admitted bytes. Discovery never submits audio.
                var capability = await DiscoverCoreAsync(deadline.Token).ConfigureAwait(false);
                if (!capability.Advertised) return Failure(capability.Code, AudioOperationOutcome.NotAdmitted);
                var requestId = "lanternwake-audio-" + Guid.NewGuid().ToString("N");
                _audioBody = Encode(pcm, sampleRate, frameCount, requestId, capability.Profile);
                if (_audioBody.Length > Math.Min(MaxRequestBytes, capability.MaxRequestBytes))
                    return Failure("request_limit", AudioOperationOutcome.NotAdmitted);
                using var request = new HttpRequestMessage(HttpMethod.Post, "v1/model-operations")
                    { Content = new ByteArrayContent(_audioBody) };
                request.Content.Headers.ContentType = new("application/json");
                deadline.Token.ThrowIfCancellationRequested();
                submitted = true; // Conservative: after this point a lost response cannot prove non-admission.
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
                using var json = await ReadAsync(response, deadline.Token).ConfigureAwait(false);
                var root = json.RootElement;
                RequireVersion(root);
                if (Field(root, "request_id") != requestId) throw new ProtocolException("invalid_response");
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var (code, outcome) = Error(root);
                    return Failure(code, outcome);
                }
                if (root.TryGetProperty("error", out _) || !root.TryGetProperty("result", out var result) ||
                    Field(result, "kind") != "text" || !result.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String ||
                    Field(result, "finish_reason") is not ("stop" or "length"))
                    throw new ProtocolException("invalid_response");
                var transcript = text.GetString()!;
                if (transcript.Length > 16000 || transcript.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t'))
                    throw new ProtocolException("invalid_response");
                return new(true, transcript, "", AudioOperationOutcome.Completed, Field(result, "finish_reason"));
            }
            finally { CryptographicOperations.ZeroMemory(pcm); }
        }
        catch (OperationCanceledException) { return Failure(cancellation.IsCancellationRequested || _disposed ? "cancelled" : "timeout", submitted ? AudioOperationOutcome.Unknown : AudioOperationOutcome.NotAdmitted); }
        catch (ProtocolException error) { return Failure(error.Code, submitted ? AudioOperationOutcome.Unknown : AudioOperationOutcome.NotAdmitted); }
        catch (Exception error) when (error is HttpRequestException or IOException) { return Failure("pumas_unavailable", submitted ? AudioOperationOutcome.Unknown : AudioOperationOutcome.NotAdmitted); }
        catch (JsonException) { return Failure("invalid_response", submitted ? AudioOperationOutcome.Unknown : AudioOperationOutcome.NotAdmitted); }
        finally
        {
            if (_audioBody is not null) CryptographicOperations.ZeroMemory(_audioBody);
            _audioBody = null;
            Exit();
        }
    }

    private async Task<AudioTextCapability> DiscoverCoreAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!_settings.Enabled) return new(false, "speech_disabled");
        if (_settings.Provider != DialogueProvider.Pumas) return new(false, "unsupported_provider");
        if (_settings.Model.Length == 0) return new(false, "model_not_selected");
        if (System.Text.Encoding.UTF8.GetByteCount(_settings.Model) > 256) return new(false, "invalid_model");
        var path = "v1/capabilities?model=" + Uri.EscapeDataString(_settings.Model);
        if (_settings.Profile.Length > 0) path += "&profile=" + Uri.EscapeDataString(_settings.Profile);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        using var json = await ReadAsync(response, token).ConfigureAwait(false);
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new ProtocolException("invalid_response");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            RequireVersion(root);
            var (code, outcome) = Error(root);
            if (outcome != AudioOperationOutcome.NotAdmitted || (!root.TryGetProperty("request_id", out var errorId) || errorId.ValueKind != JsonValueKind.Null))
                throw new ProtocolException("invalid_response");
            return new(false, code);
        }
        if (!root.TryGetProperty("supported_contract_versions", out var versions) || versions.ValueKind != JsonValueKind.Array ||
            !versions.EnumerateArray().Any(v => v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var version) && version == 1))
            return new(false, "unsupported_contract");
        var profile = Field(root, "profile");
        if (Field(root, "model") != _settings.Model || !SpeechServiceSettings.ValidProfile(profile) || profile.Length == 0 ||
            (_settings.Profile.Length > 0 && profile != _settings.Profile) ||
            !root.TryGetProperty("max_request_bytes", out var limit) || limit.ValueKind != JsonValueKind.Number || !limit.TryGetInt32(out var maxBytes) || maxBytes <= 0 ||
            !root.TryGetProperty("capabilities", out var descriptors) || descriptors.ValueKind != JsonValueKind.Array || descriptors.GetArrayLength() > 32)
            throw new ProtocolException("invalid_response");
        var matches = descriptors.EnumerateArray().Where(d => Field(d, "capability") == "audio_transcription" && Field(d, "semantic_task") == "speech_to_text" &&
            Contains(d, "input_formats", "pcm_f32le") && Contains(d, "output_formats", "text")).ToArray();
        if (matches.Length == 0) return new(false, "unsupported_modality");
        if (matches.Length > 1) return new(false, "ambiguous_operation");
        var descriptor = matches[0];
        if (!descriptor.TryGetProperty("availability", out var available)) throw new ProtocolException("invalid_response");
        if (Field(available, "state") == "unavailable")
            return new(false, Field(available, "reason") == "unqualified_audio_runtime" ? "unqualified_audio_runtime" : "capability_unavailable");
        if (Field(available, "state") != "available" || !descriptor.TryGetProperty("streaming", out var streaming) || streaming.ValueKind != JsonValueKind.False ||
            !descriptor.TryGetProperty("option_bounds", out var bounds) || bounds.ValueKind != JsonValueKind.Array ||
            !bounds.EnumerateArray().Any(b => Field(b, "option") == "max_output_tokens" &&
                b.TryGetProperty("minimum", out var minimum) && minimum.ValueKind == JsonValueKind.Number && minimum.TryGetDouble(out var min) && min <= 512 &&
                b.TryGetProperty("maximum", out var maximum) && maximum.ValueKind == JsonValueKind.Number && maximum.TryGetDouble(out var max) && max >= 512))
            throw new ProtocolException("invalid_response");
        return new(true, "", _settings.Model, profile, maxBytes);
    }

    private byte[] Encode(byte[] pcm, int rate, int count, string id, string profile)
    {
        // Base64 plus worst-case JSON escaping and writer slack. One fixed owned
        // array avoids abandoned growth arrays and Utf8JsonWriter's stream staging.
        var capacity = checked(((pcm.Length + 2) / 3) * 4 + 4096 + 6 *
            (Encoding.UTF8.GetByteCount(_settings.Model) + Encoding.UTF8.GetByteCount(profile) +
             Encoding.UTF8.GetByteCount(_settings.Language) + Encoding.UTF8.GetByteCount(id)));
        using var buffer = new AudioEncodingBuffer(capacity);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteNumber("contract_version", 1); writer.WriteString("request_id", id);
            writer.WriteString("model", _settings.Model); writer.WriteString("profile", profile);
            writer.WriteStartObject("input"); writer.WriteString("kind", "audio"); writer.WriteString("encoding", "pcm_f32le");
            writer.WriteNumber("sample_rate_hz", rate); writer.WriteNumber("channels", 2); writer.WriteNumber("sample_count", count);
            WriteBase64Audio(writer, pcm); writer.WriteEndObject(); writer.WriteString("output", "text");
            writer.WriteString("capability", "audio_transcription");
            writer.WriteStartObject("options"); writer.WriteString("kind", "audio"); writer.WriteString("language", _settings.Language);
            writer.WriteNumber("max_output_tokens", 512); writer.WriteEndObject(); writer.WriteBoolean("stream", false); writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteBase64Audio(Utf8JsonWriter writer, ReadOnlySpan<byte> pcm)
    {
        // .NET 8 WriteBase64String rents scratch without clearing it on return.
        // Only our internally generated quoted base64 bypasses input validation;
        // model/profile and every other string keep the writer's normal escaping.
        var length = Base64.GetMaxEncodedToUtf8Length(pcm.Length);
        using var encoded = new AudioEncodingBuffer(checked(length + 2));
        var quoted = encoded.GetSpan(length + 2)[..(length + 2)];
        quoted[0] = quoted[^1] = (byte)'"';
        if (Base64.EncodeToUtf8(pcm, quoted.Slice(1, length), out var consumed, out var written) != OperationStatus.Done ||
            consumed != pcm.Length || written != length) throw new ProtocolException("invalid_audio");
        encoded.Advance(length + 2);
        writer.WritePropertyName("data_base64"); writer.WriteRawValue(encoded.WrittenSpan, skipInputValidation: true);
    }

    internal sealed class AudioEncodingBuffer(int capacity) : IBufferWriter<byte>, IDisposable
    {
        private readonly byte[] _bytes = new byte[capacity];
        private int _written;
        private bool _disposed;
        public ReadOnlySpan<byte> WrittenSpan => _bytes.AsSpan(0, _written);
        public void Advance(int count)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (count < 0 || count > _bytes.Length - _written) throw new ArgumentOutOfRangeException(nameof(count));
            _written += count;
        }
        private void RequireCapacity(int sizeHint)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));
            if (Math.Max(1, sizeHint) > _bytes.Length - _written) throw new ProtocolException("request_limit");
        }
        public Memory<byte> GetMemory(int sizeHint = 0) { RequireCapacity(sizeHint); return _bytes.AsMemory(_written); }
        public Span<byte> GetSpan(int sizeHint = 0) { RequireCapacity(sizeHint); return _bytes.AsSpan(_written); }
        public void Dispose() { CryptographicOperations.ZeroMemory(_bytes); _disposed = true; }
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.Content.Headers.ContentType?.MediaType != "application/json" || response.Content.Headers.ContentLength > MaxResponseBytes)
            throw new ProtocolException("invalid_response");
        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var block = new byte[4096];
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(block, token).ConfigureAwait(false);
                if (read == 0) break;
                if (buffer.Length + read > MaxResponseBytes) throw new ProtocolException("response_limit");
                buffer.Write(block, 0, read);
            }
            buffer.Position = 0;
            var document = JsonDocument.Parse(buffer, new JsonDocumentOptions { MaxDepth = 16 });
            try { Unique(document.RootElement); return document; }
            catch { document.Dispose(); throw; }
        }
        finally { CryptographicOperations.ZeroMemory(block); CryptographicOperations.ZeroMemory(buffer.GetBuffer()); }
    }

    private static void Unique(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            { if (!names.Add(property.Name)) throw new ProtocolException("invalid_response"); Unique(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Unique(item);
    }
    private static string Field(JsonElement value, string field) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(field, out var text) && text.ValueKind == JsonValueKind.String ? text.GetString()! : "";
    private static bool Contains(JsonElement value, string field, string expected) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(field, out var items) && items.ValueKind == JsonValueKind.Array && items.EnumerateArray().Any(v => v.ValueKind == JsonValueKind.String && v.GetString() == expected);
    private static void RequireVersion(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("contract_version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1)
            throw new ProtocolException("unsupported_contract");
    }
    private static (string Code, AudioOperationOutcome Outcome) Error(JsonElement root)
    {
        if (root.TryGetProperty("result", out _) || !root.TryGetProperty("error", out var error)) throw new ProtocolException("invalid_response");
        var code = Field(error, "code");
        if (code is not ("invalid_request" or "unsupported_contract" or "model_not_found" or "ambiguous_model" or "ambiguous_operation" or
            "unsupported_modality" or "capability_unavailable" or "provider_failure" or "invalid_provider_result" or "request_limit" or "response_limit" or "transport_lost"))
            throw new ProtocolException("invalid_response");
        var outcome = Field(error, "outcome") switch
        {
            "not_admitted" => AudioOperationOutcome.NotAdmitted, "unknown" => AudioOperationOutcome.Unknown,
            _ => throw new ProtocolException("invalid_response")
        };
        return (code, outcome);
    }
    private string? Enter()
    {
        lock (_sync)
        {
            if (_disposed) return "disposed";
            if (_uncertain) return "producer_outcome_unknown";
            if (_active) return "busy";
            _active = true; return null;
        }
    }
    private AudioTextReply Failure(string code, AudioOperationOutcome outcome)
    {
        if (outcome == AudioOperationOutcome.Unknown) lock (_sync) _uncertain = true;
        return new(false, "", code, outcome);
    }
    private void Exit()
    {
        lock (_sync) { _active = false; if (_disposed) { _http.Dispose(); _lifetime.Dispose(); } }
    }
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true; _lifetime.Cancel();
            if (!_active) { _http.Dispose(); _lifetime.Dispose(); }
        }
    }
    private sealed class ProtocolException(string code) : Exception { public string Code { get; } = code; }
}
