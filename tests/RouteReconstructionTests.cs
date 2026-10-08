using System.Text.Json;
using Lanternwake.Core;

internal static class RouteReconstructionTests
{
    public static int Run(Story story)
    {
        int checks = 0;
        void Check(bool value, string claim) { if (!value) throw new Exception(claim); checks++; }
        var session = new StorySession(story);
        Check(!RouteReconstruction.AvailableAt(session), "Model unavailable before route sources are encountered");
        SaveData prior = session.Snapshot();
        while (session.Beat.Id != RouteReconstruction.ActivityBeatId)
        {
            prior = session.Snapshot();
            if (session.Beat.Activity is { } activity) session.AnswerActivity(activity.CorrectIndex);
            if (!session.Advance()) throw new Exception("Route checkpoint missing");
        }
        var earlier = new StorySession(story); earlier.Restore(prior);
        Check(!RouteReconstruction.AvailableAt(earlier), "Model remains unavailable immediately before its canonical question");
        Check(RouteReconstruction.AvailableAt(session), "Route model available at its unanswered canonical question");
        var before = JsonSerializer.Serialize(session.Snapshot(), Story.Json);
        if (Environment.GetEnvironmentVariable("LANTERNWAKE_ROUTE_COMPAT_OUTPUT") is { } output)
            SaveStore.Write(Path.Combine(output, "pending-route-v2.json"), session.Snapshot());
        var model = new RouteReconstruction();
        Check(model.Current.Area == "unplotted" && !model.CanMoveBack, "No invented rescue departure position");
        Check(model.MoveForward() && model.Current.Title == "Eastern approach", "Issued route follows the named eastern approach");
        Check(model.MoveForward() && model.Current.Title == "Exposed turn", "Issued route stops at the documented exposed turn");
        Check(!model.MoveForward() && !model.CanMoveForward, "No unsupported issued-route outcome");
        Check(model.MoveBack() && model.Current.Title == "Eastern approach", "Comparison can be retraced without changing story time");
        model.SelectRoute(RouteReconstruction.RouteKind.CorrectedWestern);
        Check(model.PositionIndex == 0 && model.MoveForward() && model.Current.Area == "west", "Corrected route starts separately and reaches the sheltered decision point");
        Check(!model.MoveForward() && model.Current.Note.Contains("only if") && model.Current.Note.Contains("no safe arrival"), "Corrected route retains its conditions and refuses an invented safe ending");
        model.Reset(); Check(model.PositionIndex == 0 && !model.MoveBack(), "Reset stops at unplotted, not an invented previous location");
        try { model.SelectRoute((RouteReconstruction.RouteKind)99); throw new Exception("Invalid route accepted"); }
        catch (ArgumentOutOfRangeException) { checks++; }
        Check(model.Route == RouteReconstruction.RouteKind.CorrectedWestern && model.PositionIndex == 0, "Invalid selection preserves model state");
        Check(JsonSerializer.Serialize(session.Snapshot(), Story.Json) == before && !session.CanAdvance, "Exploration grants no gates, history, facts, items or save fields");
        var beats = session.Scene.Beats;
        Check(RouteReconstruction.SourceBeatIds.All(id => Array.FindIndex(beats, beat => beat.Id == id) < Array.FindIndex(beats, beat => beat.Id == session.Beat.Id) && beats.Any(beat => beat.Id == id)), "Every displayed source precedes the question in this scene");
        session.AnswerActivity(session.Beat.Activity!.CorrectIndex);
        Check(!RouteReconstruction.AvailableAt(session), "Solved gate does not reopen a pending-question model");
        var restored = new StorySession(story); restored.Restore(JsonSerializer.Deserialize<SaveData>(before, Story.Json)!);
        Check(RouteReconstruction.AvailableAt(restored) && new RouteReconstruction().PositionIndex == 0, "Existing v2 save restores the question with a fresh non-persisting model");
        Console.WriteLine($"Route reconstruction core passed: {checks} assertions.");
        return checks;
    }
}
