#if DEBUG
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Lanternwake.Core;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

/// <summary>Owned fault fixtures; production buttons select recovery and publish subsequent saves.</summary>
public partial class LateRecoveryQualification : Node
{
    private GameView _game = null!;
    private Story _story = null!;
    private Beat[] _ordered = [];
    private SaveData _beforeCue = null!, _afterCue = null!, _cue = null!, _late = null!;
    private string _root = "", _case = "", _damage = "";
    private bool _automatic;
    private int _checks, _loads, _cancels, _saveErrors, _capturedSelections;
    private StorySession Session => Observe<StorySession>(_game, "_session");
    private SessionStorage Storage => Observe<SessionStorage>(_game, "_storage");
    private Window Modal => Observe<Window>(_game, "_modal");
    private string UserDirectory => ProjectSettings.GlobalizePath("user://");
    private bool ValidBackup => _damage is not ("both-invalid" or "all-invalid");

    private static T Observe<T>(object owner, string name) =>
        (T)(owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner)
            ?? throw new InvalidOperationException("Recovery observation unavailable: " + name));
    private void Check(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException("Late recovery: " + claim);
        _checks++;
    }
    private static string Snapshot(SaveData save) => JsonSerializer.Serialize(save, Story.Json);
    private string Slot(bool automatic, bool previous = false) => Path.Combine(UserDirectory,
        (automatic ? "autosave" : "save") + (previous ? ".previous" : "") + ".json");
    private string Label(bool automatic, bool previous, SaveData save) =>
        (previous ? "Recover previous " : "Load current ") + (automatic ? "autosave" : "manual save") + " · " + save.BeatId;
    private void Press(string name)
    {
        var button = _game.InterfaceRoot.GetNode<Button>("%" + name);
        Check(!button.Disabled, name + " is enabled");
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }
    private Button[] Actions() => Modal.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().ToArray();
    private void Action(string text)
    {
        var button = Actions().Single(b => b.Text == text);
        Check(!button.Disabled, "selected action is enabled");
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private Dictionary<string, string> Slots() => new[] { Slot(false), Slot(false, true), Slot(true), Slot(true, true) }
        .ToDictionary(p => Path.GetFileName(p), p => Directory.Exists(p) ? "directory" : File.Exists(p) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) : "missing");
    private void Unchanged(Dictionary<string, string> before) => Check(before.OrderBy(p => p.Key).SequenceEqual(Slots().OrderBy(p => p.Key)), "inspection/load/cancel preserves all slot bytes and absence");

    private void Match(SaveData expected, bool rendered = true)
    {
        Check(Snapshot(Session.Snapshot()) == Snapshot(expected), "exact inspected checkpoint restored without advance or duplicate transcript");
        var prefix = _ordered.TakeWhile(b => b.Id != expected.BeatId).Append(_ordered.Single(b => b.Id == expected.BeatId)).ToArray();
        Check(Session.History.Select(h => h.BeatId).SequenceEqual(prefix.Select(b => b.Id)) && Session.History.Select(h => h.Text).SequenceEqual(prefix.Select(b => b.Text)), "full late transcript preserves canonical provenance");
        Check(Session.KnownFacts.Select(f => f.Id).ToHashSet().SetEquals(prefix.SelectMany(b => b.UnlockFacts ?? [])), "later facts removed on earlier recovery");
        Check(Session.Inventory.Select(i => i.Id).ToHashSet().SetEquals(prefix.SelectMany(b => b.UnlockItems ?? [])), "inventory reconstructed from exact prefix");
        Check(Session.ActiveStageCues.ToHashSet().SetEquals(prefix.Select(b => b.StageCue).OfType<string>()), "cue history reconstructed without invented events");
        Check(Session.CanAdvance == (Session.Beat.Activity is null || expected.SolvedActivities.Contains(expected.BeatId)), "current activity gate restored exactly");
        if (!rendered) return;
        Check(_game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText").Text == Session.Beat.Text, "actual dialogue displays recovered beat");
        Check(Observe<string>(_game.Stage, "_location") == Session.Scene.Location, "actual stage matches recovered scene");
        var stage = Observe<StageScene>(_game.Stage, "_stage");
        if (stage.BellBody is { } bell)
        {
            var origin = Observe<Vector3>(stage, "_bellOrigin");
            Check(bell.Position.IsEqualApprox(origin + (Session.ActiveStageCues.Contains("bell_lowered") ? stage.BellLoweredOffset : Vector3.Zero)), "native bell cue remains idempotent on repeated recovery");
        }
    }

    private async Task Select(bool automatic, bool previous, SaveData expected)
    {
        var before = Slots();
        Press("LoadButton");
        Action(Label(automatic, previous, expected));
        await Frame();
        Match(expected);
        Unchanged(before);
        _loads++;
    }

    private void CancelDamagedMenu()
    {
        var progress = Session.Snapshot();
        var before = Slots();
        Press("LoadButton");
        Check(Snapshot(Session.Snapshot()) == Snapshot(progress), "opening damaged chooser does not silently fall back");
        var text = Modal.GetNode<RichTextLabel>("%ModalText").Text;
        var prefix = _automatic ? "Autosave" : "Manual save";
        var reason = _damage == "missing" ? "no snapshot" : _damage == "read-error" ? "could not read:" : "unreadable save JSON";
        Check(text.Contains(prefix + " · current: " + reason), "actual chooser describes primary failure");
        Check(!Actions().Any(b => b.Text.StartsWith("Load current " + (_automatic ? "autosave" : "manual save"))), "bad primary has no actionable load control");
        Check(Actions().Any(b => b.Text == Label(_automatic, true, _beforeCue)) == ValidBackup, "only compatible previous snapshot is offered");
        if (_damage == "all-invalid") Check(Actions().Length == 0, "all-invalid chooser has no recovery actions");
        else Check(Actions().Any(b => b.Text == Label(!_automatic, false, _late)), "intact independent slot remains explicitly selectable");
        Modal.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed);
        Check(Snapshot(Session.Snapshot()) == Snapshot(progress), "cancel leaves existing progress unchanged");
        Check(_game.InterfaceRoot.GetNode<Button>("%AdvanceButton").HasFocus(), "cancel returns in-engine focus to advance");
        Unchanged(before);
        _cancels++;
    }

    private void Checkpoints()
    {
        var session = new StorySession(_story);
        var cueIndex = Array.FindIndex(_ordered, b => b.StageCue == "bell_lowered");
        Check(cueIndex > 1000 && cueIndex + 1 < _ordered.Length, "real late cue boundary available");
        for (var i = 0; i < _ordered.Length; i++)
        {
            if (i == cueIndex - 1) _beforeCue = session.Snapshot();
            if (i == cueIndex) _cue = session.Snapshot();
            if (i == cueIndex + 1) _afterCue = session.Snapshot();
            if (session.IsEnding) { _late = session.Snapshot(); break; }
            if (session.Beat.Activity is { } activity) Check(session.AnswerActivity(activity.CorrectIndex), "fixture setup solves only actual prior gates");
            Check(session.Advance(), "fixture setup follows production domain timeline");
        }
        Check(_late.History.Count == _ordered.Length && !_late.SolvedActivities.Contains(_late.BeatId), "late checkpoint retains final unsolved gate");
    }

    private async Task Prepare()
    {
        Storage.Write(_automatic, _beforeCue);
        var old = JsonNode.Parse(File.ReadAllText(Slot(_automatic)))!;
        old["qualificationMetadata"] = "owned unknown field retained byte-for-byte";
        File.WriteAllText(Slot(_automatic), old.ToJsonString(), new System.Text.UTF8Encoding(true));
        var exact = File.ReadAllBytes(Slot(_automatic));
        Storage.Write(_automatic, _afterCue);
        Check(File.ReadAllBytes(Slot(_automatic, true)).SequenceEqual(exact), "real save rotation preserves BOM and unknown metadata");
        Storage.Write(!_automatic, _late);
        Directory.CreateDirectory(Path.Combine(_root, "archive"));
        foreach (var path in Directory.GetFiles(UserDirectory, "*.json"))
            File.Copy(path, Path.Combine(_root, "archive", Path.GetFileName(path)));
        await Select(!_automatic, false, _late);
        var primary = Slot(_automatic);
        if (_damage == "missing") File.Delete(primary);
        else if (_damage == "read-error") { File.Delete(primary); Directory.CreateDirectory(primary); }
        else if (_damage == "truncated") File.WriteAllBytes(primary, File.ReadAllBytes(primary)[..(int)(new FileInfo(primary).Length / 2)]);
        else File.WriteAllText(primary, "{ owned malformed primary");
        if (!ValidBackup) File.WriteAllText(Slot(_automatic, true), "{ owned malformed previous");
        if (_damage == "all-invalid")
            foreach (var automatic in new[] { false, true })
                foreach (var previous in new[] { false, true }) File.WriteAllText(Slot(automatic, previous), "{ owned unusable snapshot");
        CancelDamagedMenu(); CancelDamagedMenu();
        Match(_late);
    }

    private async Task Recover()
    {
        CancelDamagedMenu(); CancelDamagedMenu();
        SaveData repaired;
        if (ValidBackup)
        {
            if (_damage == "captured")
            {
                Press("LoadButton");
                File.WriteAllText(Slot(_automatic, true), "{ replaced after actual chooser inspection");
                var replaced = Slots();
                Action(Label(_automatic, true, _beforeCue));
                await Frame(); Match(_beforeCue); Unchanged(replaced);
                Check(Storage.Inspect(_automatic, true).Availability == SaveAvailability.InvalidFormat, "disk replacement stays invalid while captured UI candidate loads");
                _loads++; _capturedSelections++;
                File.WriteAllBytes(Slot(_automatic, true), File.ReadAllBytes(Path.Combine(_root, "archive", Path.GetFileName(Slot(_automatic, true)))));
            }
            for (var i = 0; i < 3; i++)
            {
                await Select(_automatic, true, _beforeCue);
                await Select(!_automatic, false, _late);
            }
            await Select(_automatic, true, _beforeCue);
            var backup = File.ReadAllBytes(Slot(_automatic, true));
            Press("SettingsButton"); Action("Toggle instant text");
            var before = Slots();
            if (_automatic) { Press("AdvanceButton"); repaired = _cue; }
            else { Press("SaveButton"); repaired = _beforeCue; }
            if (_damage == "read-error")
            {
                Check(_game.InterfaceRoot.GetNode<Label>("%StatusLabel").Text.StartsWith("Could not save:"), "actual save surfaces read error");
                Unchanged(before); _saveErrors++;
                Directory.Delete(Slot(_automatic));
                if (_automatic) { await Select(true, true, _beforeCue); Press("AdvanceButton"); }
                else Press("SaveButton");
            }
            Check(File.ReadAllBytes(Slot(_automatic, true)).SequenceEqual(backup), "publishing after unusable primary preserves exact good previous bytes");
            Match(repaired);
        }
        else if (_damage == "all-invalid")
        {
            Press("AdvanceButton");
            repaired = Session.Snapshot();
            Check(repaired.BeatId == _ordered[0].Id && repaired.History.Count == 1 && repaired.SolvedActivities.Count == 0, "unrecoverable session starts a new watch without invented late progress");
            var before = Slots(); Press("SaveButton");
            Check(Slots().Where(p => p.Key != "save.json").All(p => before[p.Key] == p.Value), "explicit new-watch save preserves all other damaged slots");
        }
        else
        {
            for (var i = 0; i < 3; i++) await Select(!_automatic, false, _late);
            var backup = File.ReadAllBytes(Slot(_automatic, true));
            if (_automatic)
            {
                Press("SettingsButton"); Action("Toggle instant text");
                Press("AdvanceButton");
                Action(Session.Beat.Activity!.Options[Session.Beat.Activity.CorrectIndex]);
                repaired = _late with { SolvedActivities = new(_late.SolvedActivities) { _late.BeatId } };
            }
            else { Press("SaveButton"); repaired = _late; }
            Check(File.ReadAllBytes(Slot(_automatic, true)).SequenceEqual(backup), "no corrupt primary rotates over existing previous bytes");
            Match(repaired);
        }
        Check(Snapshot(Storage.Read(_automatic)) == Snapshot(repaired), "subsequent real save publishes compatible primary");
        File.WriteAllText(Path.Combine(_root, "repaired.json"), Snapshot(repaired));
        for (var i = 0; i < 3; i++) await Select(_automatic, false, repaired);
        if (ValidBackup) { await Select(_automatic, true, _beforeCue); await Select(_automatic, false, repaired); }
    }

    private async Task Verify()
    {
        var repaired = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(Path.Combine(_root, "repaired.json")), Story.Json)!;
        for (var i = 0; i < 3; i++) await Select(_automatic, false, repaired);
        if (ValidBackup) { await Select(_automatic, true, _beforeCue); await Select(_automatic, false, repaired); }
        if (ValidBackup)
            Check(File.ReadAllBytes(Slot(_automatic, true)).SequenceEqual(File.ReadAllBytes(Path.Combine(_root, "archive", Path.GetFileName(Slot(_automatic, true))))), "fresh-process recovery retains original byte-exact good backup");
    }

    public override async void _Ready()
    {
        try
        {
            _root = OS.GetEnvironment("LANTERNWAKE_QUALIFICATION_ROOT");
            Check(OS.IsDebugBuild() && Path.IsPathFullyQualified(_root) && File.Exists(Path.Combine(_root, "owned-fixture")), "explicit runner-owned debug fixture required");
            Check(UserDirectory.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "normal user slots are within owned temporary root");
            _case = OS.GetEnvironment("LANTERNWAKE_RECOVERY_CASE");
            _automatic = _case.StartsWith("auto-", StringComparison.Ordinal);
            _damage = _case == "all-invalid" ? _case : _case[(_automatic ? 5 : 7)..];
            Check(_case == "all-invalid" || (_case.StartsWith("manual-", StringComparison.Ordinal) || _automatic)
                && new[] { "missing", "corrupt", "truncated", "both-invalid", "read-error", "captured" }.Contains(_damage), "bounded fault case");
            var phase = OS.GetEnvironment("LANTERNWAKE_RECOVERY_PHASE");
            Check(new[] { "prepare", "recover", "verify" }.Contains(phase), "bounded fixture phase");
            _story = Story.Parse(Godot.FileAccess.GetFileAsString("res://Content/story.json"));
            _ordered = _story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).ToArray();
            Checkpoints();
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game);
            await Frame();
            Check(Storage.Mode == SessionMode.Normal && Session.Beat.Id == _ordered[0].Id, "fresh startup never automatically recovers or advances");
            if (phase == "prepare") await Prepare();
            else if (phase == "recover") await Recover();
            else await Verify();
            Check(!Directory.GetFiles(UserDirectory, "*.pending-*").Any(), "actual recovery/save flows leave no owned staging files");
            GD.Print("LANTERNWAKE_LATE_RECOVERY_OK " + JsonSerializer.Serialize(new
            {
                scenario = _case, phase, checks = _checks, loads = _loads, cancels = _cancels,
                saveErrors = _saveErrors, capturedSelections = _capturedSelections,
                finalBeat = Session.Beat.Id, history = Session.History.Count, beforeCue = _beforeCue.BeatId,
                cue = _cue.BeatId, afterCue = _afterCue.BeatId, late = _late.BeatId
            }));
            Press("SettingsButton"); Action("Quit game");
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
#endif
