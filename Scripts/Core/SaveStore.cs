using System.Text.Json;
namespace Lanternwake.Core;
public static class SaveStore
{
    public static void Write(string path, SaveData save)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(save, Story.Json);
        if (bytes.Length > 16 * 1024 * 1024) throw new InvalidDataException("Save exceeds the supported 16 MiB limit; the previous save was preserved.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".pending";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }
        // Same-directory replacement: interrupted staging never truncates the prior save.
        File.Move(temporary, path, true);
    }
    public static SaveData Read(string path)
    {
        if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("Save is too large.");
        return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path), Story.Json) ?? throw new InvalidDataException("Save is empty.");
    }
}
