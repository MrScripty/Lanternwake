using System.Text.Json;
using Lanternwake.Core;
namespace Lanternwake.Conversation;

/// <summary>One explicit acquisition after local-only observation; uncertain submissions are never replayed in this session.</summary>
public sealed class PumasAcquisitionGate
{
    private readonly Dictionary<string, HfDownloadStarted> _submitted = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unknown = new(StringComparer.Ordinal);
    private int _pending;
    public async Task<PumasResult<HfDownloadStarted>> RequestAsync(PumasClient client, PumasOwnerClient owner,
        PumasLibrarySelection selection, PumasOwnerObservation observed, HfDownloadRequest request, CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0) return PumasResult<HfDownloadStarted>.Unavailable("busy");
        try
        {
            var identity = JsonSerializer.Serialize(new { observed.LibraryRoot, request.RepoId, request.Quant,
                Files = request.Filenames?.OrderBy(value => value, StringComparer.Ordinal).ToArray() });
            if (_submitted.TryGetValue(identity, out var accepted)) return PumasResult<HfDownloadStarted>.Unavailable("acquisition_already_requested",
                "This game session already received a submission receipt. Inspect it in Pumas; no duplicate was requested. Download ID: " + accepted.DownloadId);
            if (_unknown.Contains(identity)) return PumasResult<HfDownloadStarted>.Unavailable("acquisition_unknown",
                "A previous submission has an unknown outcome. Inspect it in Pumas; this game session will not replay it.");
            var local = await client.QueryExistingHfModelsAsync(request, token);
            if (!local.Success) return PumasResult<HfDownloadStarted>.Unavailable("local_query_blocked",
                "The selected library could not establish a local-only result (" + local.ErrorCode + "). No acquisition was requested.");
            if (local.Value!.Count != 0) return PumasResult<HfDownloadStarted>.Unavailable("local_model_exists",
                "Local matches already exist. Reuse or repair them in Pumas; no duplicate was requested.\n" +
                string.Join("\n", local.Value.Take(20).Select(value => $"{value.ModelId} · {value.State}")));
            await owner.AuthenticateAsync(selection, observed.Endpoint, token, observed);
            token.ThrowIfCancellationRequested();
            var result = await client.StartModelDownloadFromHfAsync(request, token);
            if (result.Success) _submitted[identity] = result.Value!;
            if (!result.Success && result.ErrorCode is not ("invalid_request" or "busy"))
            {
                // The legacy success:false envelope can follow durable producer admission.
                // It supplies no typed not-admitted receipt, so even a backend refusal is uncertain.
                _unknown.Add(identity);
                return PumasResult<HfDownloadStarted>.Unavailable("acquisition_unknown",
                    "Pumas returned " + result.ErrorCode + ". Admission is uncertain; inspect Pumas before requesting again. " + result.Message);
            }
            return result;
        }
        finally { Volatile.Write(ref _pending, 0); }
    }
}
