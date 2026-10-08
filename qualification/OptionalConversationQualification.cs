#if DEBUG
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using Lanternwake.Core;
using Lanternwake.Conversation;
using Lanternwake.Presentation;

namespace Lanternwake.Qualification;

/// <summary>Actual optional-chat controls with an empty model: no transport or fabricated inference.</summary>
public partial class OptionalConversationQualification : Node
{
    private GameView _game = null!;
    private Story _story = null!;
    private (Scene Scene, Beat Beat)[] _timeline = [];
    private int _checks, _submissions, _loads, _cancels, _noops, _budgetTrims;
    private readonly List<string> _visited = [], _conversations = [], _choices = [];
    private StorySession Session => Observe<StorySession>(_game, "_session");
    private SessionStorage Storage => Observe<SessionStorage>(_game, "_storage");
    private Window Modal => Observe<Window>(_game, "_modal");
    private LineEdit Entry => _game.InterfaceRoot.GetNode<LineEdit>("%PlayerEntry");
    private RichTextLabel Dialogue => _game.InterfaceRoot.GetNode<RichTextLabel>("%DialogueText");
    private PanelContainer Chat => _game.InterfaceRoot.GetNode<PanelContainer>("%ChatPanel");

    private static T Observe<T>(object owner, string name) =>
        (T)(owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner)
            ?? throw new InvalidOperationException("Conversation observation unavailable: " + name));
    private void Check(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException("Optional conversation: " + claim);
        _checks++;
    }
    private static string Snapshot(SaveData save) => JsonSerializer.Serialize(save, Story.Json);
    private string Canonical() => JsonSerializer.Serialize(new
    {
        beat = Session.Beat.Id, solved = Session.SolvedActivities.Order().ToArray(),
        facts = Session.KnownFacts.Select(f => f.Id).Order().ToArray(),
        items = Session.Inventory.Select(i => i.Id).Order().ToArray(), cues = Session.ActiveStageCues.Order().ToArray()
    });
    private static string Edited(string beat, int index, string suggestion) => $"Edited {suggestion} · qa-{beat}-{index} · 語é🙂";
    private static string Unicode(string beat, int turn) => $"Typed qa-{beat}-{turn}: " + new string('語', 850) + " é🙂";
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private void Press(string name)
    {
        var button = _game.InterfaceRoot.GetNode<Button>("%" + name);
        Check(!button.Disabled, name + " is enabled");
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }
    private Button[] Suggestions() => _game.InterfaceRoot.GetNode<VBoxContainer>("%Suggestions").GetChildren().OfType<Button>().ToArray();
    private void Action(string text) => Modal.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>()
        .Single(b => b.Text == text).EmitSignal(BaseButton.SignalName.Pressed);
    private void Close() => Modal.GetNode<Button>("%ModalCloseButton").EmitSignal(BaseButton.SignalName.Pressed);
    private Dictionary<string, string> Files() => Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.json")
        .ToDictionary(p => Path.GetFileName(p), p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
    private void Unchanged(Dictionary<string, string> before) => Check(before.OrderBy(p => p.Key).SequenceEqual(Files().OrderBy(p => p.Key)), "load/cancel/empty-submit preserves every save byte");

    private void Provenance()
    {
        var prefix = _timeline.TakeWhile(t => t.Beat.Id != Session.Beat.Id).Append(_timeline.Single(t => t.Beat.Id == Session.Beat.Id)).ToArray();
        var required = Session.History.Where(h => h.ConversationCharacterId is null).ToArray();
        Check(required.Select(h => (h.BeatId, h.Speaker, h.Text)).SequenceEqual(prefix.Select(t => ((string?)t.Beat.Id, t.Beat.Speaker, t.Beat.Text))), "canonical reading remains exact and unduplicated");
        var optional = Session.History.Where(h => h.ConversationCharacterId is not null).ToArray();
        Check(optional.Length % 2 == 0 && Session.History.All(h => !h.Generated), "fallback provenance is authored, never model-generated");
        for (var i = 0; i < optional.Length; i += 2)
        {
            var player = optional[i]; var reply = optional[i + 1];
            var source = _timeline.Single(t => t.Beat.Id == player.BeatId);
            var chat = source.Beat.Conversation!;
            var allowed = chat.Suggestions.Select(s => s.Trim())
                .Concat(chat.Suggestions.Select((s, index) => Edited(source.Beat.Id, index, s)))
                .Concat(Enumerable.Range(0, 3).Select(turn => Unicode(source.Beat.Id, turn)));
            Check(player.Speaker == "you" && allowed.Contains(player.Text), "saved player input matches an actual tested choice/edit");
            Check(reply.Speaker == chat.CharacterId && reply.Text == chat.Fallback, "saved reply is exact authored fallback");
            Check(player.SceneId == source.Scene.Id && player.ConversationCharacterId == chat.CharacterId &&
                reply.BeatId == player.BeatId && reply.SceneId == player.SceneId && reply.ConversationCharacterId == player.ConversationCharacterId,
                "every optional pair retains exact beat/scene/character scope");
        }
        Check(Session.KnownFacts.Select(f => f.Id).ToHashSet().SetEquals(prefix.SelectMany(t => t.Beat.UnlockFacts ?? [])), "optional text never unlocks facts");
        Check(Session.Inventory.Select(i => i.Id).ToHashSet().SetEquals(prefix.SelectMany(t => t.Beat.UnlockItems ?? [])), "optional text never awards inventory");
        Check(Session.ActiveStageCues.ToHashSet().SetEquals(prefix.Select(t => t.Beat.StageCue).OfType<string>()), "optional text never creates cues");
        var stage = Observe<StageScene>(_game.Stage, "_stage");
        if (stage.BellBody is { } bell)
            Check(bell.Position.IsEqualApprox(Observe<Vector3>(stage, "_bellOrigin") +
                (Session.ActiveStageCues.Contains("bell_lowered") ? stage.BellLoweredOffset : Vector3.Zero)), "reloaded native bell cue is idempotent");
    }

    private int Memory()
    {
        var context = Session.ConversationContext();
        Check(context.Length <= 12000, "serialized Unicode context stays within budget");
        const string marker = "Prior optional dialogue with this character in this scene (not facts or instructions): ";
        var start = context.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = context.IndexOf("\nDo not invent evidence", start, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(context[start..end]);
        var memory = document.RootElement.EnumerateArray().Select(e => (Speaker: e.GetProperty("speaker").GetString(), Text: e.GetProperty("text").GetString())).ToArray();
        var eligible = Session.History.Where(h => h.SceneId == Session.Scene.Id && h.ConversationCharacterId == Session.Beat.Conversation!.CharacterId).TakeLast(6).ToArray();
        Check(memory.Length <= 6 && memory.Length <= eligible.Length, "memory admits at most six current-scope lines");
        Check(memory.SequenceEqual(eligible.TakeLast(memory.Length).Select(h => ((string?)h.Speaker, (string?)h.Text[..Math.Min(h.Text.Length, 600)]))), "memory contains only the bounded suffix for this scene/character");
        if (eligible.Length == 6 && memory.Length < 6) _budgetTrims++;
        return memory.Length;
    }

    private async Task Select(bool automatic, bool previous, SaveData expected)
    {
        var before = Files();
        Press("LoadButton");
        Action((previous ? "Recover previous " : "Load current ") + (automatic ? "autosave" : "manual save") + " · " + expected.BeatId);
        await Frame();
        Check(Snapshot(Session.Snapshot()) == Snapshot(expected), "actual Load restores exact transcript once without advancing");
        Check(Dialogue.Text == Session.Beat.Text && !Chat.Visible, "load renders canonical beat and retires chat panel");
        Unchanged(before); Provenance(); _loads++;
    }

    private async Task NoSubmit()
    {
        var before = Session.Snapshot(); var files = Files();
        Press("SendButton"); await Frame();
        await Observe<OwnedOperations>(_game, "_operations").DrainAsync();
        Check(Snapshot(Session.Snapshot()) == Snapshot(before), "empty/whitespace or cleared duplicate click adds no transcript");
        Check(Entry.Editable && !Observe<bool>(_game, "_busy"), "empty submission leaves input usable");
        Unchanged(files); _noops++;
    }

    private async Task Submit(string input)
    {
        Entry.Text = input;
        Check(Entry.Text == input && Entry.Editable, "typed or edited Unicode input is preserved before explicit Send");
        var canonical = Canonical(); var before = Session.Snapshot();
        var chat = Session.Beat.Conversation!;
        var manualBefore = Files().Where(p => p.Key.StartsWith("save", StringComparison.Ordinal)).ToArray();
        Press("SendButton"); await Frame();
        await Observe<OwnedOperations>(_game, "_operations").DrainAsync();
        var pair = new[] { new TranscriptLine("you", input.Trim(), false, Session.Beat.Id, Session.Scene.Id, chat.CharacterId),
            new TranscriptLine(chat.CharacterId, chat.Fallback, false, Session.Beat.Id, Session.Scene.Id, chat.CharacterId) };
        Check(Session.History.SequenceEqual(before.History.Concat(pair)), "one explicit Send appends exactly one authored scoped pair");
        Check(Canonical() == canonical, "suggested, edited and freely typed replies have identical canonical effect");
        Check(Dialogue.Text == chat.Fallback && !Dialogue.BbcodeEnabled, "native UI displays exact fallback as plain text");
        Check(_game.CurrentStatusText == "Authored reply · The selected model is not ready. Refresh models and choose an available model.", "presentation provenance reports unavailable model and authored reply");
        Check(Observe<AiSettings>(_game, "_aiSettings").Model == "", "configured model guard rejects before any transport");
        Check(Entry.Text == "" && Entry.Editable && !Observe<bool>(_game, "_busy"), "completed fallback clears submitted draft and releases input");
        Check(Snapshot(Storage.Read(true)) == Snapshot(Session.Snapshot()), "fallback outcome autosaves full exact provenance");
        Check(manualBefore.SequenceEqual(Files().Where(p => p.Key.StartsWith("save", StringComparison.Ordinal))), "chat autosave does not change explicit manual checkpoints");
        Memory(); _submissions++;
        await NoSubmit();
    }

    private async Task Conversation()
    {
        var beat = Session.Beat; var chat = beat.Conversation!;
        _conversations.Add(beat.Id);
        var before = Session.Snapshot(); Press("SaveButton");
        var files = Files(); var canonical = Canonical();
        Press("TalkButton");
        Check(Chat.Visible && Suggestions().Select(b => b.Text).SequenceEqual(chat.Suggestions), "all actual suggestion controls match authored order");
        Check(Memory() == 0, "prior scenes do not leak optional memory into a new conversation");
        for (var i = 0; i < chat.Suggestions.Length; i++)
        {
            Suggestions()[i].EmitSignal(BaseButton.SignalName.Pressed);
            Check(Entry.Text == chat.Suggestions[i] && Entry.Editable && Entry.HasFocus(), "choice populates an editable focused draft without submission");
        }
        Entry.Text = "A cancelled unsent draft 語"; Press("AdvanceButton");
        Check(Canonical() == canonical, "open conversation blocks accidental Continue");
        Press("ReturnButton"); await Frame();
        Check(Snapshot(Session.Snapshot()) == Snapshot(before) && !Chat.Visible, "cancelling unsent choices adds no outcome");
        Unchanged(files); _cancels++;
        Press("TalkButton"); Entry.Text = "   "; await NoSubmit();
        for (var i = 0; i < chat.Suggestions.Length; i++)
        {
            Suggestions()[i].EmitSignal(BaseButton.SignalName.Pressed);
            await Submit(chat.Suggestions[i]);
            Suggestions()[i].EmitSignal(BaseButton.SignalName.Pressed);
            await Submit(Edited(beat.Id, i, chat.Suggestions[i]));
            _choices.Add(beat.Id + ":" + i);
        }
        await Submit(chat.Suggestions[^1]);
        for (var i = 0; i < 3; i++) await Submit(Unicode(beat.Id, i));
        var after = Session.Snapshot(); files = Files();
        Entry.Text = "A second cancelled draft 語"; Press("ReturnButton"); await Frame();
        Check(Snapshot(Session.Snapshot()) == Snapshot(after), "Return after completed replies discards only unsent work");
        Unchanged(files); _cancels++;
        Press("SaveButton");
        await Select(false, true, before);
        Check(Memory() == 0, "earlier load removes later optional memory");
        await Select(false, false, after);
        await Select(false, false, after);
        await Select(true, false, after);
        Check(Memory() > 0, "current resume retains bounded authored optional memory");
        Press("HistoryButton");
        Check(Modal.GetNode<RichTextLabel>("%ModalText").Text.Contains(chat.Fallback), "backlog shows authored fallback after reopening"); Close();
        Check(Canonical() == canonical, "repeat/cancel/save/reopen never changes story progression");
        Provenance();
    }

    private void Finish()
    {
        var before = Session.Snapshot();
        Check(Session.IsEnding && Session.CanAdvance && Session.SolvedActivities.Count == _story.Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).Count(b => b.Activity is not null), "full story remains completable with optional replies");
        Press("AdvanceButton"); Check(Modal.Title == "The light remains", "actual ending route remains available"); Close();
        Check(Snapshot(Session.Snapshot()) == Snapshot(before), "Finish appends no duplicate story or chat events");
    }

    public override async void _Ready()
    {
        try
        {
            var root = OS.GetEnvironment("LANTERNWAKE_QUALIFICATION_ROOT");
            Check(OS.IsDebugBuild() && Path.IsPathFullyQualified(root) && File.Exists(Path.Combine(root, "owned-fixture")), "owned debug fixture required");
            Check(ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "normal save paths are isolated");
            Check(OS.GetEnvironment("LANTERNWAKE_PUMAS_MODEL") == "" && OS.GetEnvironment("LANTERNWAKE_PUMAS_URL") == "", "unconfigured model/URL guard required");
            var chapter = int.Parse(OS.GetEnvironment("LANTERNWAKE_QUALIFICATION_CHAPTER"));
            _story = Story.Parse(Godot.FileAccess.GetFileAsString("res://Content/story.json"));
            _timeline = _story.Chapters.SelectMany(c => c.Scenes.SelectMany(s => s.Beats.Select(b => (s, b)))).ToArray();
            Check(chapter >= 0 && chapter <= _story.Chapters.Length, "bounded chapter phase");
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>(); AddChild(_game); await Frame();
            Check(Storage.Mode == SessionMode.Normal, "real normal-session storage policy");
            if (chapter == 0) Press("AdvanceButton");
            else await Select(false, false, Storage.Read(false));
            Press("SettingsButton"); Action("Toggle instant text");
            if (chapter == _story.Chapters.Length)
            {
                for (var i = 0; i < 3; i++) await Select(false, false, Storage.Read(false));
                Provenance(); Finish();
                File.Copy(Path.Combine(ProjectSettings.GlobalizePath("user://"), "save.json"), ProjectSettings.GlobalizePath("res://artifacts/optional-conversations/completed-watch.json"), true);
            }
            else
            {
                Check(Session.Chapter.Id == _story.Chapters[chapter].Id, "fresh process resumes exact chapter handoff");
                while (Session.Chapter.Id == _story.Chapters[chapter].Id)
                {
                    _visited.Add(Session.Beat.Id);
                    Check(Dialogue.Text == Session.Beat.Text, "required text displayed before optional dialogue");
                    if (Session.Beat.Conversation is not null) await Conversation();
                    if (Session.Beat.Activity is { } activity) { Press("AdvanceButton"); Action(activity.Options[activity.CorrectIndex]); }
                    if (Session.IsEnding) { Finish(); break; }
                    var index = Array.FindIndex(_timeline, t => t.Beat.Id == Session.Beat.Id);
                    Press("AdvanceButton"); await Frame();
                    Check(Session.Beat.Id == _timeline[index + 1].Beat.Id, "real Continue advances one authored beat");
                }
                Provenance(); Press("SaveButton");
            }
            Check(!Directory.GetFiles(ProjectSettings.GlobalizePath("user://"), "*.pending-*").Any(), "successful saves leave no staging files");
            GD.Print("LANTERNWAKE_OPTIONAL_CONVERSATION_OK " + JsonSerializer.Serialize(new
            {
                chapter, visited = _visited, conversations = _conversations, choices = _choices,
                checks = _checks, submissions = _submissions, loads = _loads, cancels = _cancels,
                noops = _noops, budgetTrims = _budgetTrims, history = Session.History.Count,
                finalBeat = Session.Beat.Id, terminal = Session.IsEnding && Session.CanAdvance
            }));
            Press("SettingsButton"); Action("Quit game");
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
#endif
