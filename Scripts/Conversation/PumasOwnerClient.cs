using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Lanternwake.Core;

namespace Lanternwake.Conversation;

/// <summary>Point-in-time borrowed observation, with no shutdown or library ownership authority.</summary>
public sealed record PumasOwnerObservation(string Endpoint, string LibraryRoot, string RegistryLibraryId,
    string CoreGeneration, string HttpGeneration, string Descriptor)
{
    public string Lifetime => "Borrowed";
}

/// <summary>Uses Pumas's authenticated CLI projection; never reads its private registry or starts a service.</summary>
public sealed class PumasOwnerClient : IDisposable
{
    private const int Limit = 65536;
    private readonly HttpClient _http;
    private readonly Func<PumasLibrarySelection, CancellationToken, Task<byte[]>> _observe;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    private readonly object _sync = new();
    private int _active;
    public PumasOwnerClient() : this(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(2) }, ObserveAsync) { }
    internal PumasOwnerClient(HttpMessageHandler handler, Func<PumasLibrarySelection, CancellationToken, Task<byte[]>> observe)
    { _http = new(handler) { Timeout = Timeout.InfiniteTimeSpan }; _observe = observe; }

    public async Task<PumasOwnerObservation> AuthenticateAsync(PumasLibrarySelection selection, string? expectedEndpoint,
        CancellationToken cancellation, PumasOwnerObservation? previous = null)
    {
        lock (_sync) { ObjectDisposedException.ThrowIf(_disposed, this); _active++; }
        try
        {
        selection.Validate();
        if (!selection.Selected || !Directory.Exists(selection.Root) || !File.Exists(selection.ObserverExecutable))
            throw new InvalidDataException("Select an existing library and installed Pumas observer in AI setup. No service was started.");
        var endpoint = expectedEndpoint is null ? null : new AiSettings(DialogueProvider.Pumas, false, expectedEndpoint, "").Validate().Endpoint;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var before = Parse(await ObserveCheckedAsync(selection, deadline.Token).ConfigureAwait(false), selection, endpoint);
        if (previous is not null && before.Descriptor != previous.Descriptor)
            throw new InvalidDataException("The selected Pumas owner changed. Review and save your library selection again.");
        endpoint = before.Endpoint;
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint + ".well-known/pumas");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        if ((int)response.StatusCode != 200 || response.Content.Headers.ContentType?.MediaType != "application/json" ||
            response.Headers.CacheControl?.NoStore != true || response.Content.Headers.ContentLength > Limit)
            throw new InvalidDataException("The selected Pumas HTTP advertisement is unavailable or incompatible.");
        var live = Parse(await ReadBoundedAsync(await response.Content.ReadAsStreamAsync(deadline.Token), Limit, deadline.Token), selection, endpoint);
        var after = Parse(await ObserveCheckedAsync(selection, deadline.Token).ConfigureAwait(false), selection, endpoint);
        if (before.Descriptor != live.Descriptor || before.Descriptor != after.Descriptor)
            throw new InvalidDataException("Pumas owner identity changed during authentication. No request was submitted.");
        return before;
        }
        finally { lock (_sync) { if (--_active == 0 && _disposed) _lifetime.Dispose(); } }
    }
    private async Task<byte[]> ObserveCheckedAsync(PumasLibrarySelection selection, CancellationToken token)
    {
        var bytes = await _observe(selection, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested(); // A late read cannot proceed through a disposed borrower.
        return bytes;
    }
    internal static async Task<byte[]> ObserveAsync(PumasLibrarySelection selection, CancellationToken token)
    {
        var info = new ProcessStartInfo(selection.ObserverExecutable) { UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        info.ArgumentList.Add("--describe-local-http"); info.ArgumentList.Add("--launcher-root"); info.ArgumentList.Add(selection.Root);
        using var child = Process.Start(info) ?? throw new IOException("The Pumas observer could not start.");
        var output = ReadBoundedAsync(child.StandardOutput.BaseStream, Limit, token);
        // Diagnostics do not authorize discovery; discard them with bounded storage while keeping the pipe drained.
        var errors = child.StandardError.BaseStream.CopyToAsync(Stream.Null, 4096, token);
        try
        {
            await child.WaitForExitAsync(token).ConfigureAwait(false);
            var data = await output.ConfigureAwait(false); await errors.ConfigureAwait(false);
            if (child.ExitCode != 0) throw new InvalidDataException("Pumas refused authenticated owner discovery. No startup or download fallback is permitted.");
            return data;
        }
        finally
        {
            // This is our transient read-only observer, never the borrowed daemon or a PID found in an advertisement.
            if (!child.HasExited)
            {
                try { child.Kill(); } catch (InvalidOperationException) when (child.HasExited) { }
                await child.WaitForExitAsync().ConfigureAwait(false);
            }
            try { await Task.WhenAll(output, errors).ConfigureAwait(false); } catch { }
        }
    }
    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit, CancellationToken token)
    {
        using var body = new MemoryStream(); var block = new byte[4096]; var overflow = false;
        for (int count; (count = await stream.ReadAsync(block, token).ConfigureAwait(false)) != 0;)
        { if (body.Length + count > limit) overflow = true; if (!overflow) body.Write(block, 0, count); }
        if (overflow) throw new InvalidDataException("Pumas observation exceeded its bounded response limit.");
        return body.ToArray();
    }
    internal static PumasOwnerObservation Parse(byte[] bytes, PumasLibrarySelection selection, string? expectedEndpoint)
    {
        try { return ParseDescription(bytes, selection, expectedEndpoint); }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        { throw new InvalidDataException("Malformed Pumas owner advertisement.", error); }
    }
    private static PumasOwnerObservation ParseDescription(byte[] bytes, PumasLibrarySelection selection, string? expectedEndpoint)
    {
        if (bytes.Length > Limit) throw new InvalidDataException("Pumas description is too large.");
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        var root = document.RootElement; ValidateUnique(root);
        Fields(root, "advertisement_schema_version", "service_generation", "instance", "endpoint", "build_info");
        Version(root, "advertisement_schema_version");
        var endpoint = new AiSettings(DialogueProvider.Pumas, false, Text(root, "endpoint", 128), "").Validate().Endpoint;
        if (new Uri(endpoint).Port == 0) throw new InvalidDataException("Pumas HTTP listener port is invalid.");
        if (expectedEndpoint is not null && endpoint != expectedEndpoint) throw new InvalidDataException("Pumas advertised a different endpoint than your saved selection.");
        var instance = root.GetProperty("instance");
        Fields(instance, "discovery_schema_version", "build_info", "registry_library_id", "library_root", "generation", "pumas_version", "protocols", "capabilities", "model_ref_schema_version", "selector_schema_version");
        foreach (var name in new[] { "discovery_schema_version", "model_ref_schema_version", "selector_schema_version" }) Version(instance, name);
        var observedRoot = Text(instance, "library_root", 4096);
        // Conservative canonical-path subset. Producer CLI owns native selected-root authentication;
        // aliases which this managed consumer cannot compare are refused, never guessed by case folding.
        if (!Path.IsPathFullyQualified(observedRoot) || !Directory.Exists(observedRoot) ||
            Path.GetFullPath(observedRoot) != Path.GetFullPath(selection.Root)) throw new InvalidDataException("Pumas described a different library root. Use its canonical absolute folder path.");
        Protocol(instance.GetProperty("protocols"), "pumas.local-ipc");
        var capabilities = Labels(instance.GetProperty("capabilities"));
        if (!new[] { "model.query@1", "model.get.local@1", "model.selector@1", "artifact.resolve@1" }.All(capabilities.Contains))
            throw new InvalidDataException("Pumas lacks the required local-first owner capabilities.");
        Build(instance.GetProperty("build_info"), "pumas-library"); Build(root.GetProperty("build_info"), "pumas-rpc");
        if (Text(instance, "pumas_version", 128) != Text(instance.GetProperty("build_info"), "package_version", 128))
            throw new InvalidDataException("Pumas core build context differs.");
        return new(endpoint, observedRoot, Text(instance, "registry_library_id", 128), Text(instance, "generation", 128),
            Text(root, "service_generation", 128), Canonical(root));
    }
    private static void Build(JsonElement value, string component)
    {
        var mandatory = new[] { "build_info_schema_version", "component", "package_version", "compiled_features", "protocols", "schemas" };
        var allowed = mandatory.Concat(new[] { "build_id", "source_revision", "target" }).ToHashSet();
        if (value.ValueKind != JsonValueKind.Object || mandatory.Any(name => !value.TryGetProperty(name, out _)) || value.EnumerateObject().Any(p => !allowed.Contains(p.Name)))
            throw new InvalidDataException("Unsupported Pumas build fields.");
        Version(value, "build_info_schema_version");
        if (Text(value, "component", 128) != component) throw new InvalidDataException("Wrong Pumas build component.");
        Text(value, "package_version", 128); Labels(value.GetProperty("compiled_features"));
        foreach (var field in new[] { "build_id", "source_revision", "target" })
            if (value.TryGetProperty(field, out var provenance) && provenance.ValueKind != JsonValueKind.Null) Text(value, field, 128);
        Protocol(value.GetProperty("protocols"), "pumas.local-ipc");
        var required = new Dictionary<string, int> { ["pumas.build-info"] = 1, ["pumas.discovery"] = 1,
            ["pumas.model-ref"] = 1, ["pumas.model-selector"] = 1 };
        if (component == "pumas-rpc") { Protocol(value.GetProperty("protocols"), "pumas.local-http"); required["pumas.http-advertisement"] = 1; }
        var schemas = new Dictionary<string, int>();
        foreach (var item in Array(value.GetProperty("schemas")))
        { Fields(item, "name", "version"); if (!item.GetProperty("version").TryGetInt32(out var version) || version < 1 || !schemas.TryAdd(Text(item, "name", 128), version)) throw new InvalidDataException("Invalid Pumas schema advertisement."); }
        if (required.Any(item => !schemas.TryGetValue(item.Key, out var version) || version != item.Value)) throw new InvalidDataException("Unsupported Pumas schemas.");
    }
    private static void Protocol(JsonElement value, string required)
    {
        var found = false; var names = new HashSet<string>();
        foreach (var item in Array(value))
        {
            Fields(item, "name", "versions"); var name = Text(item, "name", 128);
            var versions = Array(item.GetProperty("versions")).Select(v => v.TryGetInt32(out var n) && n > 0 ? n : throw new InvalidDataException("Invalid protocol version.")).ToArray();
            if (!names.Add(name) || versions.Length == 0 || versions.Distinct().Count() != versions.Length) throw new InvalidDataException("Ambiguous protocol advertisement.");
            if (name == required && versions.Contains(1)) found = true;
        }
        if (!found) throw new InvalidDataException("Unsupported Pumas discovery protocol.");
    }
    private static JsonElement.ArrayEnumerator Array(JsonElement value) => value.ValueKind == JsonValueKind.Array && value.GetArrayLength() <= 64
        ? value.EnumerateArray() : throw new InvalidDataException("Invalid bounded advertisement list.");
    private static HashSet<string> Labels(JsonElement value)
    {
        var labels = new HashSet<string>();
        foreach (var item in Array(value)) if (item.ValueKind != JsonValueKind.String || !Valid(item.GetString(), 128) || !labels.Add(item.GetString()!)) throw new InvalidDataException("Ambiguous Pumas labels.");
        return labels;
    }
    private static bool Valid(string? value, int limit) => !string.IsNullOrWhiteSpace(value) && value.Length <= limit && !value.Any(char.IsControl);
    private static string Text(JsonElement value, string field, int limit) => value.GetProperty(field) is var item && item.ValueKind == JsonValueKind.String && Valid(item.GetString(), limit)
        ? item.GetString()! : throw new InvalidDataException("Invalid Pumas identity field.");
    private static void Version(JsonElement value, string field)
    { if (!value.GetProperty(field).TryGetInt32(out var version) || version != 1) throw new InvalidDataException("Unsupported Pumas description version."); }
    private static void Fields(JsonElement value, params string[] names)
    { if (value.ValueKind != JsonValueKind.Object || !value.EnumerateObject().Select(p => p.Name).ToHashSet().SetEquals(names)) throw new InvalidDataException("Unsupported Pumas description fields."); }
    private static void ValidateUnique(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        { var names = new HashSet<string>(); foreach (var item in value.EnumerateObject()) { if (!names.Add(item.Name)) throw new InvalidDataException("Duplicate Pumas description field."); ValidateUnique(item.Value); } }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) ValidateUnique(item);
    }
    private static string Canonical(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", value.EnumerateArray().Select(Canonical)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(value.GetString()),
        _ => value.GetRawText()
    };
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return; _disposed = true; _lifetime.Cancel(); _http.Dispose();
            if (_active == 0) _lifetime.Dispose(); // Finite local reads retain cancellation state until settled.
        }
        // Never stop a borrowed owner.
    }
}
