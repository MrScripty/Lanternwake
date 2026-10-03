#if TOOLS
using Godot;
using Lanternwake.Core;
using File = System.IO.File;

[Tool]
public partial class StoryTextPlugin : EditorPlugin
{
    private const string StoryPath = "res://Content/story.json";
    private Control _dock = null!;
    private EditorDock _editorDock = null!;
    private OptionButton _scenes = null!, _speakers = null!;
    private ItemList _beats = null!;
    private TextEdit _text = null!;
    private Label _status = null!;
    private Story _story = null!;
    private Lanternwake.Core.Scene[] _sceneData = [];
    private string _loaded = "";
    private int _sceneIndex, _beatIndex;
    private bool _loading, _dirty;

    public override void _EnterTree()
    {
        _dock = GD.Load<PackedScene>("res://addons/story_text/StoryTextDock.tscn").Instantiate<Control>();
        _scenes = _dock.GetNode<OptionButton>("Scenes");
        _speakers = _dock.GetNode<OptionButton>("Speakers");
        _beats = _dock.GetNode<ItemList>("Beats");
        _text = _dock.GetNode<TextEdit>("BeatText");
        _status = _dock.GetNode<Label>("Status");
        _scenes.ItemSelected += SelectScene;
        _beats.ItemSelected += SelectBeat;
        _text.TextChanged += () => { if (!_loading) _dirty = true; };
        _speakers.ItemSelected += _ => { if (!_loading) _dirty = true; };
        _dock.GetNode<Button>("Actions/Save").Pressed += Save;
        _dock.GetNode<Button>("Actions/Reload").Pressed += Reload;
        _editorDock = new EditorDock { Title = "Story Text", DefaultSlot = EditorDock.DockSlot.RightUl };
        _editorDock.AddChild(_dock);
        AddDock(_editorDock);
        Reload();
        if (OS.GetCmdlineUserArgs().Contains("--editor-roundtrip"))
            AddChild(GD.Load<GDScript>("res://integration/editor/roundtrip.gd").New().As<Node>());
    }

    public override void _ExitTree()
    {
        RemoveDock(_editorDock);
        // Plugin shutdown may end the editor before another deferred-delete frame.
        _editorDock.Free();
    }

    private void Reload()
    {
        try
        {
            _loading = true;
            _loaded = File.ReadAllText(ProjectSettings.GlobalizePath(StoryPath));
            _story = Story.Parse(_loaded);
            _sceneData = _story.Chapters.SelectMany(c => c.Scenes).ToArray();
            _scenes.Clear();
            foreach (var scene in _sceneData) _scenes.AddItem(scene.Id + " · " + scene.Title);
            _speakers.Clear(); _speakers.AddItem("narrator");
            foreach (var character in _story.Characters) _speakers.AddItem(character.Id);
            _sceneIndex = Math.Min(_sceneIndex, _sceneData.Length - 1);
            _scenes.Select(_sceneIndex);
            _dirty = false; PopulateBeats();
            _status.Text = "Loaded canonical story.json. Save writes only the selected beat's text/speaker. Reload discards unsaved edits.";
        }
        catch (Exception error) { _status.Text = error.Message; }
        finally { _loading = false; }
    }

    private void SelectScene(long index)
    {
        if (_dirty) { _scenes.Select(_sceneIndex); _status.Text = "Save or Reload your current edit before changing scene."; return; }
        _sceneIndex = (int)index; _beatIndex = 0; PopulateBeats();
    }

    private void PopulateBeats()
    {
        _beats.Clear();
        foreach (var beat in _sceneData[_sceneIndex].Beats) _beats.AddItem(beat.Id + " · " + beat.Text[..Math.Min(48, beat.Text.Length)]);
        _beatIndex = Math.Min(_beatIndex, _beats.ItemCount - 1);
        _beats.Select(_beatIndex); DisplayBeat();
    }

    private void SelectBeat(long index)
    {
        if (_dirty) { _beats.Select(_beatIndex); _status.Text = "Save or Reload your current edit before selecting another beat."; return; }
        _beatIndex = (int)index; DisplayBeat();
    }

    private void DisplayBeat()
    {
        _loading = true;
        var beat = _sceneData[_sceneIndex].Beats[_beatIndex];
        _text.Text = beat.Text;
        for (var i = 0; i < _speakers.ItemCount; i++) if (_speakers.GetItemText(i) == beat.Speaker) _speakers.Select(i);
        _loading = false;
    }

    private void Save()
    {
        string? pending = null;
        try
        {
            var path = ProjectSettings.GlobalizePath(StoryPath);
            var result = StoryTextEdit.Apply(_loaded, File.ReadAllText(path), _sceneData[_sceneIndex].Beats[_beatIndex].Id,
                _speakers.GetItemText(_speakers.Selected), _text.Text);
            pending = path + "." + Guid.NewGuid().ToString("N") + ".pending";
            using (var stream = new FileStream(pending, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(result);
                stream.Write(bytes); stream.Flush(true);
            }
            if (File.ReadAllText(path) != _loaded) throw new InvalidOperationException("The story changed on disk. Reload before saving.");
            File.Move(pending, path, true);
            Reload();
            _status.Text = "Saved and validated. Run the game to see your edit. Stable story/save IDs are unchanged.";
        }
        catch (Exception error) { _status.Text = error.Message; }
        finally { if (pending is not null && File.Exists(pending)) File.Delete(pending); }
    }
}
#endif
