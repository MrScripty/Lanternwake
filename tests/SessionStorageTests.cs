using Lanternwake.Core;

internal static class SessionStorageTests
{
    public static int Run(Story story)
    {
        var count = 0;
        void Check(bool condition, string claim) { if (!condition) throw new Exception(claim); count++; }
        var root = Path.Combine(Path.GetTempPath(), "lanternwake-player-sentinel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var manual = Path.Combine(root, "save.json"); var auto = Path.Combine(root, "autosave.json");
        var data = new StorySession(story).Snapshot();
        try
        {
            File.WriteAllText(manual, "real-manual-sentinel"); File.WriteAllText(auto, "real-auto-sentinel");
            using (var preview = new SessionStorage(SessionMode.AuthorPreview, root))
            {
                Check(!preview.CanUseSaves, "Preview exposes save/load disabled");
                foreach (var automatic in new[] { false, true })
                {
                    try { preview.Write(automatic, data); throw new Exception("Preview wrote a save"); } catch (InvalidOperationException) { count++; }
                    try { preview.Read(automatic); throw new Exception("Preview read a player slot"); } catch (InvalidOperationException) { count++; }
                }
            }
            var owned = new List<string>();
            using (var first = new SessionStorage(SessionMode.AutomatedTest, root))
            using (var second = new SessionStorage(SessionMode.AutomatedTest, root))
            {
                owned.Add(first.OwnedTestDirectory!); owned.Add(second.OwnedTestDirectory!);
                first.Write(false, data); first.Write(true, data);
                Check(first.Read(false).BeatId == data.BeatId && first.Read(true).BeatId == data.BeatId, "Test mode save/load works in disposable slots");
                try { second.Read(false); throw new Exception("Independent tests shared a slot"); } catch (FileNotFoundException) { count++; } catch (DirectoryNotFoundException) { count++; }
                second.Write(false, data with { StoryTitle = "independent-test" });
                Check(first.Read(false).StoryTitle == story.Title, "Repeated/concurrent tests have separate custody");
            }
            Check(owned.All(path => !Directory.Exists(path)), "Test storage is removed on dispose");
            Check(File.ReadAllText(manual) == "real-manual-sentinel" && File.ReadAllText(auto) == "real-auto-sentinel", "Preview/test flows preserve real slots byte-for-byte");
            using (var normal = new SessionStorage(SessionMode.Normal, root))
            {
                normal.Write(false, data); normal.Write(true, data);
                Check(SaveStore.Read(manual).BeatId == data.BeatId && SaveStore.Read(auto).BeatId == data.BeatId, "Normal mode uses unchanged real slot filenames");
                Check(normal.Read(false).BeatId == data.BeatId && normal.Read(true).BeatId == data.BeatId, "Normal manual/autosave load still works");
            }
            Check(File.Exists(manual) && File.Exists(auto), "Disposing normal mode never deletes player saves");
            var disposed = new SessionStorage(SessionMode.AutomatedTest, root); disposed.Dispose(); disposed.Dispose();
            try { disposed.Write(false, data); throw new Exception("Disposed session wrote a save"); } catch (ObjectDisposedException) { count++; }
        }
        finally { Directory.Delete(root, true); }
        return count;
    }
}
