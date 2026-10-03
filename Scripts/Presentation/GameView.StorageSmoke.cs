using Godot;
using Lanternwake.Core;

namespace Lanternwake.Presentation;

public partial class GameView
{
    private void RunSaveIsolationSmoke()
    {
        var root = Path.Combine(Path.GetTempPath(), "lanternwake-save-isolation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var manual = Path.Combine(root, "save.json"); var automatic = Path.Combine(root, "autosave.json");
        void Check(bool condition, string claim) { if (!condition) throw new InvalidOperationException("Save isolation smoke: " + claim); }
        try
        {
            Check(_storage!.Mode == SessionMode.AuthorPreview && _previewMode, "debug preview launch chooses non-persisting policy");
            Check(InterfaceRoot.GetNode<Button>("%SaveButton").Disabled && InterfaceRoot.GetNode<Button>("%LoadButton").Disabled, "preview buttons disabled");
            File.WriteAllText(manual, "REAL_MANUAL_SENTINEL"); File.WriteAllText(automatic, "REAL_AUTO_SENTINEL");
            _storage.Dispose(); _storage = new SessionStorage(SessionMode.AuthorPreview, root);
            _dialogue.VisibleCharacters = -1; Advance();
            Save(false); Save(true); var before = _session.Beat.Id;
            Load(false); Load(true); ShowLoad();
            Check(_session.Beat.Id == before && _modal is null, "preview load cannot replace position or open save slots");
            while (_session.Beat.Activity is null && !_session.IsEnding) _session.Advance();
            RenderBeat(); ShowActivity();
            var correct = _session.Beat.Activity!.CorrectIndex;
            _modal!.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().ElementAt(correct).EmitSignal(Button.SignalName.Pressed);
            Check(_session.CanAdvance, "preview evidence can be solved locally");
            Check(File.ReadAllText(manual) == "REAL_MANUAL_SENTINEL" && File.ReadAllText(automatic) == "REAL_AUTO_SENTINEL", "advance, activity, manual save and load preserve player sentinels");
            _storage.Dispose(); _storage = new SessionStorage(SessionMode.AutomatedTest, root);
            Save(false); Save(true); before = _session.Beat.Id;
            _session.Advance(); Load(false);
            Check(_session.Beat.Id == before, "test runtime save/load uses disposable slots");
            Check(File.ReadAllText(manual) == "REAL_MANUAL_SENTINEL" && File.ReadAllText(automatic) == "REAL_AUTO_SENTINEL", "test runtime preserves player sentinels");
            GD.Print("LANTERNWAKE_SAVE_ISOLATION_OK preview advance activity manual/autosave load and test slots preserve real-slot sentinels");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { _storage?.Dispose(); _storage = null; Directory.Delete(root, true); }
    }
}
