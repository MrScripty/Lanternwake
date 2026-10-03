namespace Lanternwake.Core;

/// <summary>Fail closed on contradictory preview/test flags; never silently fall back to player saves.</summary>
public sealed record SessionLaunch(SessionMode Mode, string? BeatId, bool StagePreview, string? Check)
{
    public static SessionLaunch Parse(IReadOnlyList<string> arguments, bool debugBuild)
    {
        string[] known = ["--stage-preview", "--author-preview-beat", "--smoke", "--ui-smoke", "--live-ui-preview", "--save-isolation-smoke", "--author-preview-smoke"];
        string[] namespaces = ["--stage-preview", "--author-preview", "--smoke", "--ui-smoke", "--live-ui-preview", "--save-isolation"];
        foreach (var argument in arguments)
            if (namespaces.Any(prefix => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) && !known.Contains(argument))
                throw new InvalidDataException("Unsupported preview/test option. Use exact flags and --author-preview-beat followed by a separate stable beat ID.");
        var stages = arguments.Count(a => a == "--stage-preview");
        var selected = arguments.Select((value, index) => (value, index)).Where(a => a.value == "--author-preview-beat").ToArray();
        var checks = arguments.Where(a => a is "--smoke" or "--ui-smoke" or "--live-ui-preview" or "--save-isolation-smoke" or "--author-preview-smoke").ToArray();
        if (stages > 1 || selected.Length > 1 || checks.Length > 1) throw new InvalidDataException("Choose one preview/test entry per launch.");
        string? beatId = null;
        if (selected.Length == 1)
        {
            var position = selected[0].index + 1;
            if (position >= arguments.Count || string.IsNullOrWhiteSpace(arguments[position]) || arguments[position].StartsWith("--", StringComparison.Ordinal))
                throw new InvalidDataException("Author preview requires a stable beat ID.");
            beatId = arguments[position];
        }
        var check = checks.SingleOrDefault();
        if (stages > 0 && beatId is not null) throw new InvalidDataException("Choose stage preview or a selected beat, not both.");
        if (check == "--author-preview-smoke" && beatId is null) throw new InvalidDataException("Author preview smoke requires a beat ID.");
        if (check == "--save-isolation-smoke" && beatId is not null) throw new InvalidDataException("Save isolation smoke cannot select a beat.");
        var author = stages > 0 || beatId is not null || check == "--save-isolation-smoke";
        if (author && check is "--smoke" or "--ui-smoke" or "--live-ui-preview")
            throw new InvalidDataException("Author preview cannot be combined with another test mode.");
        if (author && !debugBuild) throw new InvalidDataException("Author preview is only available in debug builds.");
        return new(author ? SessionMode.AuthorPreview : check is null ? SessionMode.Normal : SessionMode.AutomatedTest,
            beatId, stages > 0 || check == "--save-isolation-smoke", check);
    }
}
