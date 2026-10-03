namespace Lanternwake.Core;

public enum SessionMode { Normal, AuthorPreview, AutomatedTest }

/// <summary>One authority for runtime save/load paths. Preview never resolves a player slot.</summary>
public sealed class SessionStorage : IDisposable
{
    public SessionMode Mode { get; }
    public bool CanUseSaves => !_disposed && Mode != SessionMode.AuthorPreview;
    private readonly string? _directory;
    private readonly bool _ownsDirectory;
    private bool _disposed;

    public SessionStorage(SessionMode mode, string playerDirectory)
    {
        Mode = mode;
        switch (mode)
        {
            case SessionMode.Normal:
                _directory = Path.GetFullPath(playerDirectory);
                break;
            case SessionMode.AuthorPreview:
                break;
            case SessionMode.AutomatedTest:
                _directory = Path.Combine(Path.GetTempPath(), "lanternwake-test-" + Guid.NewGuid().ToString("N"));
                _ownsDirectory = true;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    public void Write(bool automatic, SaveData save) => SaveStore.Write(Slot(automatic), save);
    public SaveData Read(bool automatic) => SaveStore.Read(Slot(automatic));

    private string Slot(bool automatic)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Mode == SessionMode.AuthorPreview)
            throw new InvalidOperationException("Author preview cannot read or write player saves. Restart normally to resume your watch.");
        return Path.Combine(_directory!, automatic ? "autosave.json" : "save.json");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsDirectory && Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
