using System.Text.Json;

namespace Lanternwake.Core;

public enum SaveAvailability { Available, Missing, InvalidFormat, UnsupportedVersion, Incompatible, ReadError }
public sealed record SaveCandidate(bool Automatic, bool Previous, SaveAvailability Availability, string Description, SaveData? Snapshot);
internal enum SavePublicationStage { CurrentStaged, PreviousStaged, PreviousPublished, CurrentPublished }

/// <summary>Compatibility-aware recovery. Per-file replacement, not a multi-file durability transaction.</summary>
public static class SaveRecovery
{
    internal const int MaximumBytes = 16 * 1024 * 1024;

    internal static void Validate(Story story, SaveData save)
    {
        var probe = new StorySession(story);
        probe.Restore(save);
    }

    internal static (byte[] Bytes, DateTime FileTimeUtc) ReadSnapshot(string path, Action? afterOpen = null)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        afterOpen?.Invoke(); // deterministic owned-fixture growth/replacement boundary
        var bytes = ReadBounded(stream);
        return (bytes, File.GetLastWriteTimeUtc(stream.SafeFileHandle));
    }

    internal static byte[] ReadBounded(Stream stream)
    {
        using var result = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var remaining = MaximumBytes + 1 - (int)result.Length;
            var read = stream.Read(buffer, 0, Math.Min(buffer.Length, remaining));
            if (read == 0) return result.ToArray();
            // Never append the sentinel byte or trust a prior path/handle length for allocation.
            if (result.Length + read > MaximumBytes) throw new InvalidDataException("Save is too large.");
            result.Write(buffer, 0, read);
        }
    }

    internal static SaveData ReadCompatible(string path, Story story)
    {
        var save = Decode(ReadSnapshot(path).Bytes); Validate(story, save); return save;
    }

    private static SaveData Decode(byte[] bytes)
    {
        // Match SaveStore.Read's BOM-aware text decoding while retaining original bytes for backup.
        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return JsonSerializer.Deserialize<SaveData>(reader.ReadToEnd(), Story.Json)
            ?? throw new InvalidDataException("Save is empty.");
    }

    public static SaveCandidate Inspect(string path, Story story, bool automatic, bool previous)
    {
        var label = (automatic ? "Autosave" : "Manual save") + (previous ? " · previous good" : " · current");
        SaveCandidate Unavailable(SaveAvailability status, string reason) => new(automatic, previous, status, label + ": " + reason, null);
        try
        {
            var file = ReadSnapshot(path);
            var save = Decode(file.Bytes);
            if (save.Version != 1) return Unavailable(SaveAvailability.UnsupportedVersion, "unsupported save version " + save.Version);
            try { Validate(story, save); }
            catch (InvalidDataException error) { return Unavailable(SaveAvailability.Incompatible, "not valid for the current story: " + error.Message); }
            var position = story.Chapters.SelectMany(c => c.Scenes.SelectMany(s => s.Beats.Select(b => (Chapter: c, Scene: s, Beat: b))))
                .Single(p => p.Beat.Id == save.BeatId);
            return new(automatic, previous, SaveAvailability.Available,
                $"{label}: {position.Chapter.Title} / {position.Scene.Title}\nBeat {save.BeatId} · file time {file.FileTimeUtc:yyyy-MM-dd HH:mm:ss} UTC", save);
        }
        catch (FileNotFoundException) { return Unavailable(SaveAvailability.Missing, "no snapshot"); }
        catch (DirectoryNotFoundException) { return Unavailable(SaveAvailability.Missing, "no snapshot"); }
        catch (JsonException) { return Unavailable(SaveAvailability.InvalidFormat, "unreadable save JSON"); }
        catch (InvalidDataException error) { return Unavailable(SaveAvailability.InvalidFormat, error.Message); }
        catch (IOException error) { return Unavailable(SaveAvailability.ReadError, "could not read: " + error.Message); }
        catch (UnauthorizedAccessException error) { return Unavailable(SaveAvailability.ReadError, "could not read: " + error.Message); }
    }

    internal static void Write(string currentPath, string previousPath, Story story, SaveData save,
        Action<SavePublicationStage>? checkpoint = null)
    {
        Validate(story, save);
        var currentBytes = JsonSerializer.SerializeToUtf8Bytes(save, Story.Json);
        if (currentBytes.Length > MaximumBytes) throw new InvalidDataException("Save exceeds the supported 16 MiB limit; existing slots were preserved.");
        byte[]? previousBytes = null;
        // Read first: File.Exists can hide access/I/O errors as false. Only actual absence is absent.
        try
        {
            var candidate = ReadSnapshot(currentPath).Bytes;
            Validate(story, Decode(candidate));
            previousBytes = candidate; // preserve exact old bytes, including unknown fields
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (JsonException) { }
        catch (InvalidDataException) { }
        Directory.CreateDirectory(Path.GetDirectoryName(currentPath)!);
        var currentPending = currentPath + ".pending-" + Guid.NewGuid().ToString("N");
        var previousPending = previousPath + ".pending-" + Guid.NewGuid().ToString("N");
        try
        {
            Stage(currentPending, currentBytes);
            checkpoint?.Invoke(SavePublicationStage.CurrentStaged);
            if (previousBytes is not null)
            {
                Stage(previousPending, previousBytes);
                checkpoint?.Invoke(SavePublicationStage.PreviousStaged);
                File.Move(previousPending, previousPath, true);
                checkpoint?.Invoke(SavePublicationStage.PreviousPublished);
            }
            File.Move(currentPending, currentPath, true);
            checkpoint?.Invoke(SavePublicationStage.CurrentPublished);
        }
        finally
        {
            foreach (var path in new[] { currentPending, previousPending }) File.Delete(path);
        }
    }

    private static void Stage(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes); stream.Flush(true);
    }
}
