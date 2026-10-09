namespace Lanternwake.Core;

/// <summary>Explicit existing root and installed producer observer; never a registry credential.</summary>
public sealed record PumasLibrarySelection(string Root = "", string ObserverExecutable = "")
{
    public bool Selected => Root.Length > 0 && ObserverExecutable.Length > 0;
    public PumasLibrarySelection Validate()
    {
        if (Root is null || ObserverExecutable is null || new[] { Root, ObserverExecutable }.Any(v => v.Length > 4096 || v.Any(char.IsControl)))
            throw new InvalidDataException("Enter valid local Pumas paths.");
        if (Root.Length == 0 && ObserverExecutable.Length == 0) return this;
        if (!Selected || !Path.IsPathFullyQualified(Root) || !Path.IsPathFullyQualified(ObserverExecutable))
            throw new InvalidDataException("Select an existing Pumas library and an absolute installed pumas-rpc executable path.");
        return this;
    }
    public static PumasLibrarySelection FromEnvironment() => new(
        Environment.GetEnvironmentVariable("LANTERNWAKE_PUMAS_LIBRARY_ROOT") ?? "",
        Environment.GetEnvironmentVariable("LANTERNWAKE_PUMAS_OBSERVER") ?? "");
}
