namespace Lanternwake.Core;

/// <summary>Optional, non-persisting comparison of the chapter-two documented routes.</summary>
public sealed class RouteReconstruction
{
    public const string ActivityBeatId = "ch2_s3a_evidence";
    public enum RouteKind { IssuedEastern, CorrectedWestern }
    public sealed record Position(string Title, string Area, string Note);
    private static readonly Position Unplotted = new("Not plotted", "unplotted",
        "The comparison does not establish the rescue craft's exact departure position.");
    private static readonly Position[] Issued = [Unplotted,
        new("Eastern approach", "east", "The issued plan sends the rescue craft around the eastern wall."),
        new("Exposed turn", "east", "High water and onshore wind make this approach longer and more difficult. The record supports lost time, not a guaranteed alternative outcome.")];
    private static readonly Position[] Corrected = [Unplotted,
        new("Sheltered approach decision point", "west", "The corrected west approach avoids the exposed turn only if crews have the corrected depth information and the passage remains open. Stop at this decision point; no safe arrival or survival outcome is established.")];
    public static IReadOnlyList<string> SourceBeatIds { get; } = Array.AsReadOnly(new[]
        { "ch2_s3a_b006", "ch2_s3a_b007", "ch2_s3a_b012", "ch2_s3a_b015", "ch2_s3a_b018", "ch2_s3a_b020" });
    public RouteKind Route { get; private set; }
    public int PositionIndex { get; private set; }
    private Position[] Path => Route == RouteKind.IssuedEastern ? Issued : Corrected;
    public Position Current => Path[PositionIndex];
    public bool CanMoveForward => PositionIndex + 1 < Path.Length;
    public bool CanMoveBack => PositionIndex > 0;
    public string RouteTitle => Route == RouteKind.IssuedEastern ? "Issued eastern route" : "Corrected western approach";
    public static bool AvailableAt(StorySession session) => session.Beat.Id == ActivityBeatId && !session.CanAdvance;
    public bool MoveForward()
    {
        if (!CanMoveForward) return false;
        PositionIndex++; return true;
    }
    public bool MoveBack()
    {
        if (!CanMoveBack) return false;
        PositionIndex--; return true;
    }
    public void Reset() => PositionIndex = 0;
    public void SelectRoute(RouteKind route)
    {
        if (!Enum.IsDefined(route)) throw new ArgumentOutOfRangeException(nameof(route));
        Route = route; Reset();
    }
}
