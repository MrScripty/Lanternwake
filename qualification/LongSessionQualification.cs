#if DEBUG
using System.Reflection;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

/// <summary>Explicit test scene only. Drives production buttons; reflection observes private state.</summary>
public partial class LongSessionQualification : Node
{
    private GameView _game = null!;
    private Story _story = null!;
    private Beat[] _ordered = [];
    private int _checks, _resumes, _wrongAnswers, _recoveries, _rollbackRecoveries, _sceneChecks;
    private readonly List<string> _visited = [];
    private readonly HashSet<string> _scenes = [];
    private StorySession Session => Observe<StorySession>(_game, "_session");
    private SessionStorage Storage => Observe<SessionStorage>(_game, "_storage");
    private Window Modal => Observe<Window>(_game, "_modal");

    private static T Observe<T>(object owner, string name) =>
        (T)(owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner)
            ?? throw new InvalidOperationException("Qualification observation unavailable: " + name));
    private void Check(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException("Long-session qualification: " + claim);
        _checks++;
    }
    private Button Button(string name) => _game.InterfaceRoot.GetNode<Button>("%" + name);
    private void Press(string name)
    {
        var button = Button(name);
        Check(!button.Disabled, name + " is enabled");
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }
    private void Action(string text)
    {
        var button = Modal.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == text);
        Check(!button.Disabled, "modal action enabled: " + text);
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }
    private void Close() => Modal.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed);
    private static string Snapshot(SaveData save) => JsonSerializer.Serialize(save, Story.Json);
    private Dictionary<string, byte[]> Files() => Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.json")
        .ToDictionary(path => Path.GetFileName(path), File.ReadAllBytes);
    private void FilesUnchanged(Dictionary<string, byte[]> before) =>
        Check(before.Count == Files().Count && before.All(p => File.ReadAllBytes(Path.Combine(ProjectSettings.GlobalizePath("user://"), p.Key)).SequenceEqual(p.Value)),
            "load and inspection preserve every save byte");

    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private async Task Resume(bool automatic, bool previous, SaveData expected)
    {
        var before = Files();
        var fresh = new StorySession(_story);
        var candidate = Storage.Inspect(automatic, previous);
        Check(candidate.Availability == SaveAvailability.Available, "selected slot is compatible");
        fresh.Restore(candidate.Snapshot!);
        Check(Snapshot(fresh.Snapshot()) == Snapshot(expected), "fresh domain instance restores exact snapshot");
        Press("LoadButton");
        var label = (previous ? "Recover previous " : "Load current ") + (automatic ? "autosave" : "manual save") + " · " + expected.BeatId;
        Action(label);
        await Frame();
        Check(Snapshot(Session.Snapshot()) == Snapshot(expected), "actual UI restores selected snapshot");
        FilesUnchanged(before);
        CheckBeat();
        _resumes++;
    }

    private void CheckBeat()
    {
        var index = Array.FindIndex(_ordered, b => b.Id == Session.Beat.Id);
        var prefix = _ordered.Take(index + 1).ToArray();
        Check(index >= 0, "current authored beat exists");
        Check(Session.History.Count == prefix.Length && Session.History.Select(h => h.BeatId).SequenceEqual(prefix.Select(b => b.Id)),
            "all required text appears once in canonical history order");
        Check(Session.History.Select(h => h.Text).SequenceEqual(prefix.Select(b => b.Text)) && Session.History.All(h => !h.Generated),
            "offline main path contains exact authored text without generated additions");
        Check(Session.KnownFacts.Select(f => f.Id).ToHashSet().SetEquals(prefix.SelectMany(b => b.UnlockFacts ?? [])), "derived known facts match authored prefix");
        Check(Session.Inventory.Select(i => i.Id).ToHashSet().SetEquals(prefix.SelectMany(b => b.UnlockItems ?? [])), "derived inventory matches authored prefix");
        Check(Session.ActiveStageCues.ToHashSet().SetEquals(prefix.Select(b => b.StageCue).OfType<string>()), "stage cues match authored prefix");
        Check(_game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText").Text == Session.Beat.Text, "game renders current required text");
        Check(_game.InterfaceRoot.GetNode<Label>("%PlaceLabel").Text == Session.Scene.Title + "  ·  " + Session.Scene.TimeOfDay.Replace('_', ' '), "scene label matches source");
        Check(Observe<string>(_game.Stage, "_location") == Session.Scene.Location, "actual stage location matches source");
        var visible = Session.Scene.CharacterIds.Where(id => id is not ("ivo" or "operator" or "clerk")).ToArray();
        var actors = Observe<List<Node3D>>(_game.Stage, "_actors");
        Check(actors.Select(a => Path.GetFileNameWithoutExtension(a.SceneFilePath).ToLowerInvariant()).SequenceEqual(visible), "actual cast is living authored participants in order");
        var stage = Observe<StageScene>(_game.Stage, "_stage");
        if (stage.BellBody is { } bell)
        {
            var origin = Observe<Vector3>(stage, "_bellOrigin");
            var expected = origin + (Session.ActiveStageCues.Contains("bell_lowered") ? stage.BellLoweredOffset : Vector3.Zero);
            Check(bell.Position.IsEqualApprox(expected), "actual bell position follows restored cue");
        }
        _scenes.Add(Session.Scene.Id);
        _sceneChecks++;
    }

    private async Task SaveAndResume()
    {
        var expected = Session.Snapshot();
        Press("SaveButton");
        Check(Snapshot(Storage.Read(false)) == Snapshot(expected), "manual save contains current exact state");
        var previous = Storage.Inspect(false, true);
        if (previous.Availability == SaveAvailability.Available && previous.Snapshot!.BeatId != expected.BeatId)
        {
            await Resume(false, true, previous.Snapshot);
            Check(Session.SolvedActivities.SetEquals(previous.Snapshot.SolvedActivities), "earlier checkpoint removes later solved gates");
            await Resume(false, false, expected);
            _rollbackRecoveries++;
        }
        await Resume(false, false, expected);
    }

    private async Task EvidenceGate()
    {
        var activity = Session.Beat.Activity!;
        var unsolved = Session.Snapshot();
        await SaveAndResume();
        var before = Files();
        Press("AdvanceButton");
        Check(Session.Beat.Id == unsolved.BeatId && !Session.CanAdvance, "evidence blocks progression");
        Action(activity.Options[(activity.CorrectIndex + 1) % activity.Options.Length]);
        await Frame();
        Check(Snapshot(Session.Snapshot()) == Snapshot(unsolved), "wrong answer has no progress, transcript or evidence penalty");
        Check(_game.InterfaceRoot.GetNode<Label>("%StatusLabel").Text.Contains("try again"), "wrong answer gives retry feedback");
        FilesUnchanged(before);
        _wrongAnswers++;
        Press("AdvanceButton");
        Action(activity.Options[activity.CorrectIndex]);
        await Frame();
        Check(Session.CanAdvance && Session.SolvedActivities.Contains(unsolved.BeatId), "correct answer unlocks current activity");
        var solved = Session.Snapshot();
        Check(Snapshot(Storage.Read(true)) == Snapshot(solved), "activity completion autosaves exact solved state");
        Press("SaveButton");
        await Resume(false, true, unsolved);
        _recoveries++;
        Check(!Session.CanAdvance, "explicit earlier recovery restores the unsolved gate");
        await Resume(true, false, solved);
        Check(Session.CanAdvance, "autosave resume restores solved gate without re-answering");
    }

    private void ReadingSurfaces()
    {
        var before = Session.Snapshot();
        Press("HistoryButton");
        var history = Modal.GetNode<RichTextLabel>("%ModalText").Text;
        Check(history.Contains(_ordered[0].Text) && history.Contains(Session.Beat.Text), "long backlog includes beginning and current beat");
        Close();
        Press("EvidenceButton");
        var catalogue = Modal.GetNode<RichTextLabel>("%ModalText").Text;
        Check(Session.KnownFacts.All(f => catalogue.Contains(f.Text)) && Session.Inventory.All(i => catalogue.Contains(i.Description)), "catalogue contains all current evidence");
        Close();
        Check(Snapshot(Session.Snapshot()) == Snapshot(before), "history/catalogue inspection leaves progress intact");
    }

    private void Finish()
    {
        Check(Session.IsEnding && Session.CanAdvance, "terminal activity solved before ending");
        Check(Session.SolvedActivities.Count == _ordered.Count(b => b.Activity is not null), "all authored activities remain solved");
        var before = Session.Snapshot();
        Press("AdvanceButton");
        Check(Modal.Title == "The light remains", "actual Finish route opens ending");
        Close();
        Check(Snapshot(Session.Snapshot()) == Snapshot(before) && !Session.Advance(), "terminal completion is stable");
        ReadingSurfaces();
    }

    public override async void _Ready()
    {
        try
        {
            var root = OS.GetEnvironment("LANTERNWAKE_QUALIFICATION_ROOT");
            Check(OS.IsDebugBuild() && Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")), "explicit owned debug fixture required");
            Check(ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "normal slot directory lies within owned fixture");
            var chapter = int.Parse(OS.GetEnvironment("LANTERNWAKE_QUALIFICATION_CHAPTER"));
            _story = Story.Parse(Godot.FileAccess.GetFileAsString("res://Content/story.json"));
            _ordered = _story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).ToArray();
            Check(chapter >= 0 && chapter <= _story.Chapters.Length, "bounded chapter selection");
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>();
            AddChild(_game);
            await Frame();
            Check(Storage.Mode == SessionMode.Normal, "actual normal-session persistence policy");
            Check(Observe<ReadingTextStyle>(_game, "_readingText").Percent == 100, "fresh native process begins at default text size");
            if (chapter == 0) Press("AdvanceButton");
            else
            {
                var saved = Storage.Read(false);
                await Resume(false, false, saved);
                Check(chapter == _story.Chapters.Length ? Session.IsEnding : Session.Chapter.Id == _story.Chapters[chapter].Id,
                    "fresh process resumes exact chapter handoff");
            }
            var initialBeat = Session.Beat.Id;
            Press("AdvanceButton");
            Check(Session.Beat.Id == initialBeat, "fresh resume reveals text without advancing");
            Press("SettingsButton");
            Action("Larger reading text");
            Check(Observe<ReadingTextStyle>(_game, "_readingText").Percent == 125, "session preference is exercised without save migration");
            Action("Toggle instant text");
            if (chapter == _story.Chapters.Length) { CheckBeat(); Finish(); }
            else
            {
                string? scene = null;
                while (Session.Chapter.Id == _story.Chapters[chapter].Id)
                {
                    CheckBeat();
                    _visited.Add(Session.Beat.Id);
                    var index = Array.FindIndex(_ordered, b => b.Id == Session.Beat.Id);
                    if (Session.Scene.Id != scene || index % 50 == 0 || Session.Beat.StageCue is not null) { await SaveAndResume(); ReadingSurfaces(); }
                    scene = Session.Scene.Id;
                    if (Session.Beat.Activity is not null) await EvidenceGate();
                    if (Session.IsEnding) { Finish(); break; }
                    var expected = _ordered[index + 1].Id;
                    Press("AdvanceButton");
                    await Frame();
                    Check(Session.Beat.Id == expected, "actual Continue advances exactly one canonical beat");
                    Check(Snapshot(Storage.Read(true)) == Snapshot(Session.Snapshot()), "each Continue autosaves exact progress");
                }
                await SaveAndResume();
            }
            Check(!Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.pending-*").Any(), "successful save flows leave no staging files");
            GD.Print("LANTERNWAKE_LONG_SESSION_CHAPTER_OK " + JsonSerializer.Serialize(new
            {
                chapter, visited = _visited, checks = _checks, resumes = _resumes, wrongAnswers = _wrongAnswers,
                recoveries = _recoveries, rollbackRecoveries = _rollbackRecoveries,
                sceneChecks = _sceneChecks, scenes = _scenes.Order().ToArray(),
                finalBeat = Session.Beat.Id, terminal = Session.IsEnding && Session.CanAdvance
            }));
            Press("SettingsButton");
            Action("Quit game");
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
#endif
