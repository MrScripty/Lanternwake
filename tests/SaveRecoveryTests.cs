using Lanternwake.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class SaveRecoveryTests
{
    public static int Run(Story story)
    {
        var count = 0;
        void Check(bool condition, string claim) { if (!condition) throw new Exception(claim); count++; }
        var folder = Path.Combine(Path.GetTempPath(), "lanternwake-recovery-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var current = Path.Combine(folder, "save.json"); var previous = Path.Combine(folder, "save.previous.json");
        var session = new StorySession(story); var first = session.Snapshot(); session.Advance(); var second = session.Snapshot(); session.Advance(); var third = session.Snapshot();
        byte[] Bytes(SaveData data) => JsonSerializer.SerializeToUtf8Bytes(data, Story.Json);
        void Reset() { File.WriteAllBytes(current, Bytes(second)); File.WriteAllBytes(previous, Bytes(first)); }
        try
        {
            Check(SaveRecovery.Inspect(current, story, false, false).Availability == SaveAvailability.Missing, "Missing slot is identified without creation");
            SaveRecovery.Write(current, previous, story, first);
            Check(!File.Exists(previous), "First save invents no previous snapshot");
            var unknown = JsonNode.Parse(File.ReadAllText(current))!; unknown["futureMetadata"] = "preserve-exact-old-bytes";
            File.WriteAllText(current, unknown.ToJsonString(), new System.Text.UTF8Encoding(true)); var exactOld = File.ReadAllBytes(current);
            SaveRecovery.Write(current, previous, story, second);
            Check(File.ReadAllBytes(previous).SequenceEqual(exactOld), "Backup preserves exact compatible previous bytes and unknown fields");
            Check(SaveRecovery.Inspect(previous, story, false, true).Snapshot!.BeatId == first.BeatId, "Previous candidate describes the prior compatible snapshot");
            File.WriteAllText(current, JsonSerializer.Serialize(second, Story.Json), System.Text.Encoding.Unicode);
            var utf16 = File.ReadAllBytes(current);
            Check(SaveRecovery.Inspect(current, story, false, false).Availability == SaveAvailability.Available, "Recovery accepts BOM-aware encodings already supported by the normal reader");
            SaveRecovery.Write(current, previous, story, third);
            Check(File.ReadAllBytes(previous).SequenceEqual(utf16), "Compatible BOM-encoded prior bytes are preserved exactly");
            foreach (var bad in new[] { "{ malformed", JsonSerializer.Serialize(second with { Version = 99 }, Story.Json), JsonSerializer.Serialize(second with { StoryTitle = "Other story" }, Story.Json) })
            {
                File.WriteAllText(current, bad); File.WriteAllBytes(previous, Bytes(first)); var backup = File.ReadAllBytes(previous);
                Check(SaveRecovery.Inspect(current, story, false, false).Availability != SaveAvailability.Available, "Unusable primary is not offered for load");
                SaveRecovery.Write(current, previous, story, third);
                Check(File.ReadAllBytes(previous).SequenceEqual(backup), "Corrupt/unsupported/incompatible primary cannot replace a usable backup");
                Check(SaveStore.Read(current).BeatId == third.BeatId, "A valid new save still publishes with unusable old primary");
            }
            File.WriteAllText(current, "{ malformed");
            Check(SaveRecovery.Inspect(current, story, false, false).Availability == SaveAvailability.InvalidFormat, "Malformed JSON is distinct from story compatibility");
            File.WriteAllBytes(current, Bytes(second with { Version = 99 }));
            Check(SaveRecovery.Inspect(current, story, false, false).Availability == SaveAvailability.UnsupportedVersion, "Unsupported schema is explicit");
            File.WriteAllBytes(current, Bytes(second with { StoryTitle = "Other story" }));
            Check(SaveRecovery.Inspect(current, story, false, false).Availability == SaveAvailability.Incompatible, "Format-valid but different-story save is explicitly incompatible");
            using (var large = new FileStream(current, FileMode.Create, FileAccess.Write)) large.SetLength(16 * 1024 * 1024 + 1);
            Check(SaveRecovery.Inspect(current, story, false, false).Availability == SaveAvailability.InvalidFormat, "Oversize slot is rejected before deserialization");
            Reset();
            foreach (var stage in Enum.GetValues<SavePublicationStage>())
            {
                Reset();
                try { SaveRecovery.Write(current, previous, story, third, observed => { if (observed == stage) throw new IOException("Injected boundary interruption"); }); throw new Exception("Boundary fault did not happen"); }
                catch (IOException) { count++; }
                Check(SaveStore.Read(current).BeatId == (stage == SavePublicationStage.CurrentPublished ? third : second).BeatId, "Current preservation at " + stage);
                Check(SaveStore.Read(previous).BeatId == (stage is SavePublicationStage.PreviousPublished or SavePublicationStage.CurrentPublished ? second : first).BeatId, "Backup-before-primary ordering at " + stage);
                Check(!Directory.EnumerateFiles(folder, "*.pending-*").Any(), "Owned interrupted staging cleaned at " + stage);
            }
            Reset();
            using (var locked = new FileStream(current, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Check(SaveRecovery.Inspect(current, story, false, false).Availability == SaveAvailability.ReadError, "Read I/O failure is visible in the chooser");
                try { SaveRecovery.Write(current, previous, story, third); throw new Exception("Read I/O failure was ignored"); } catch (IOException) { count++; }
                Check(SaveStore.Read(previous).BeatId == first.BeatId, "Unexpected current-read failure preserves backup");
            }
            Check(SaveStore.Read(current).BeatId == second.BeatId, "Unexpected read failure preserves current");
            Reset();
            File.Delete(previous); Directory.CreateDirectory(previous);
            try { SaveRecovery.Write(current, previous, story, third); throw new Exception("Backup publication failure was ignored"); } catch (IOException) { count++; }
            Check(SaveStore.Read(current).BeatId == second.BeatId, "Actual backup rename failure cannot publish new primary");
            Check(!Directory.EnumerateFiles(folder, "*.pending-*").Any(), "Actual backup publication failure cleans owned staging");
            Directory.Delete(previous); Reset();
            File.WriteAllText(previous, "broken backup");
            Check(SaveRecovery.Inspect(previous, story, false, true).Availability == SaveAvailability.InvalidFormat, "Corrupt backup is never offered as recoverable");
            SaveRecovery.Write(current, previous, story, third);
            Check(SaveStore.Read(previous).BeatId == second.BeatId, "Compatible primary can replace an unusable backup");
            Reset();
            var beforeCurrent = File.ReadAllBytes(current); var beforePrevious = File.ReadAllBytes(previous);
            try { SaveRecovery.Write(current, previous, story, third with { Version = 99 }); throw new Exception("Invalid new snapshot accepted"); } catch (InvalidDataException) { count++; }
            Check(File.ReadAllBytes(current).SequenceEqual(beforeCurrent) && File.ReadAllBytes(previous).SequenceEqual(beforePrevious), "New compatibility validation precedes any writes");
            var huge = third with { History = Enumerable.Repeat(new TranscriptLine("narrator", new string('x', 20000), BeatId: first.BeatId), 1000).ToList() };
            try { SaveRecovery.Write(current, previous, story, huge); throw new Exception("Oversize new snapshot accepted"); } catch (InvalidDataException) { count++; }
            Check(File.ReadAllBytes(current).SequenceEqual(beforeCurrent) && File.ReadAllBytes(previous).SequenceEqual(beforePrevious), "New size validation precedes any writes");
            File.WriteAllText(current + ".pending-abandoned", "interrupted untrusted data");
            Check(SaveRecovery.Inspect(current, story, false, false).Snapshot!.BeatId == second.BeatId, "Abandoned staging is never silently loaded");
            SaveRecovery.Write(current, previous, story, third);
            Check(File.Exists(current + ".pending-abandoned"), "Only this operation's staging is cleaned, never another operation's file");
            var proposal = SaveRecovery.Inspect(previous, story, false, true);
            File.WriteAllBytes(previous, Bytes(third));
            var recovered = new StorySession(story); recovered.Restore(proposal.Snapshot!);
            Check(recovered.Beat.Id == second.BeatId && SaveStore.Read(previous).BeatId == third.BeatId, "Explicit candidate is a stable inspected snapshot; reading/recovery never rewrites disk");
            Check(proposal.Description.Contains(second.BeatId) && proposal.Description.Contains("UTC"), "Candidate identifies beat and file timestamp");
        }
        finally { Directory.Delete(folder, true); }
        return count;
    }
}
