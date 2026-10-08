using System.Text.Json;

namespace Lanternwake.Core;

public enum DialogueProvider { Pumas, OpenRouter }

/// <summary>Independent speech connection preferences; availability is owned by the speech adapter.</summary>
public sealed record SpeechServiceSettings(bool Enabled = false, string Endpoint = "http://127.0.0.1:8080/", string Model = "")
{
    public DialogueProvider Provider { get; init; } = DialogueProvider.Pumas;

    public SpeechServiceSettings Validate()
    {
        var connection = new AiSettings(Provider, false, Endpoint, Model).ValidateConnection();
        return this with { Endpoint = connection.Endpoint, Model = connection.Model };
    }
}

/// <summary>Local connection preferences, separate from story progress and save slots.</summary>
public sealed record AiSettings(DialogueProvider Provider, bool DialogueEnabled, string Endpoint, string Model)
{
    public int Version { get; init; } = 1;
    public SpeechServiceSettings Transcription { get; init; } = new();
    public SpeechServiceSettings CharacterSpeech { get; init; } = new();
    public Dictionary<string, string> CharacterVoices { get; init; } = new();

    public static AiSettings FromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable("LANTERNWAKE_PUMAS_URL");
        var model = Environment.GetEnvironmentVariable("LANTERNWAKE_PUMAS_MODEL") ?? "";
        var endpoint = string.IsNullOrWhiteSpace(url) ? DefaultEndpoint(DialogueProvider.Pumas) : url;
        return new(DialogueProvider.Pumas, !string.IsNullOrWhiteSpace(model), endpoint, model)
        { Transcription = new(Endpoint: endpoint), CharacterSpeech = new(Endpoint: endpoint) };
    }

    public static string DefaultEndpoint(DialogueProvider provider) => provider == DialogueProvider.Pumas
        ? "http://127.0.0.1:8080/" : "https://openrouter.ai/api/v1/";

    public AiSettings Validate()
    {
        var connection = ValidateConnection();
        if (Transcription is null || CharacterSpeech is null || CharacterVoices is null || CharacterVoices.Count > 64)
            throw new InvalidDataException("Invalid speech settings.");
        foreach (var (character, voice) in CharacterVoices)
            if (string.IsNullOrWhiteSpace(character) || character.Length > 160 || character.StartsWith("sk-or-", StringComparison.OrdinalIgnoreCase) ||
                character.Any(c => !char.IsLetterOrDigit(c) && c is not '_' and not '-') ||
                voice is null || voice.Length > 512 || voice.Any(char.IsControl) || voice.Trim().StartsWith("sk-or-", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid character voice selection.");
        return connection with { Transcription = Transcription.Validate(), CharacterSpeech = CharacterSpeech.Validate(),
            CharacterVoices = new(CharacterVoices, StringComparer.Ordinal) };
    }

    internal AiSettings ValidateConnection()
    {
        if (Version != 1) throw new InvalidDataException("Unsupported AI settings version.");
        if (!Enum.IsDefined(Provider)) throw new InvalidDataException("Choose Pumas or OpenRouter.");
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidDataException("Enter a valid provider address without credentials, query or fragment.");
        if (Provider == DialogueProvider.Pumas &&
            (uri.Scheme != "http" || uri.Host != "127.0.0.1" || uri.AbsolutePath != "/"))
            throw new InvalidDataException("Use a local Pumas address such as http://127.0.0.1:8080/.");
        if (Provider == DialogueProvider.OpenRouter &&
            (uri.Scheme != "https" || uri.Host != "openrouter.ai" || uri.Port != 443 || uri.AbsolutePath.TrimEnd('/') != "/api/v1"))
            throw new InvalidDataException("Use the OpenRouter address https://openrouter.ai/api/v1/.");
        if (Model is null || Model.Length > 512 || Model.Any(char.IsControl) || Model.Trim().StartsWith("sk-or-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The model name must be at most 512 characters, with no control characters.");
        if (DialogueEnabled && string.IsNullOrWhiteSpace(Model))
            throw new InvalidDataException("Choose a loaded model or turn off AI conversations.");
        return this with { Endpoint = uri.AbsoluteUri.TrimEnd('/') + "/", Model = Model.Trim() };
    }

    public static AiSettings Load(string path, AiSettings defaults)
    {
        if (!File.Exists(path)) return defaults.Validate();
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("AI settings file is too large.");
        try
        {
            return (JsonSerializer.Deserialize<AiSettings>(File.ReadAllText(path)) ??
                throw new InvalidDataException("AI settings file is empty.")).Validate();
        }
        catch (JsonException error) { throw new InvalidDataException("AI settings file could not be read.", error); }
    }

    public void Save(string path)
    {
        var settings = Validate();
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 65536) throw new InvalidDataException("AI settings file is too large.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
