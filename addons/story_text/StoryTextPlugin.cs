#if TOOLS
using Godot;
using Lanternwake.Core;
using File = System.IO.File;

[Tool]
public partial class StoryTextPlugin : EditorPlugin
{
    private string _storyPath = "res://Content/story.json";
    private Control _dock = null!;
    private EditorDock _editorDock = null!;
    private OptionButton _scenes = null!, _speakers = null!;
    private ItemList _beats = null!;
    private TextEdit _text = null!, _context = null!;
    private LineEdit _search = null!;
    private ItemList _matches = null!;
    private StoryAuthoringIndex? _index;
    private StoryAuthoringEntry[] _results = [];
    private Label _status = null!, _searchStatus = null!;
    private Story _story = null!;
    private Lanternwake.Core.Scene[] _sceneData = [];
    private string _loaded = "";
    private int _sceneIndex, _beatIndex;
    private bool _loading, _dirty;

    public override void _EnterTree()
    {
        var scroll = GD.Load<PackedScene>("res://addons/story_text/StoryTextDock.tscn").Instantiate<ScrollContainer>();
        _dock = scroll.GetNode<Control>("Content");
        _scenes = _dock.GetNode<OptionButton>("Scenes");
        _speakers = _dock.GetNode<OptionButton>("Speakers");
        _beats = _dock.GetNode<ItemList>("Beats");
        _text = _dock.GetNode<TextEdit>("BeatText");
        _context = _dock.GetNode<TextEdit>("Context");
        _search = _dock.GetNode<LineEdit>("Search");
        _matches = _dock.GetNode<ItemList>("Matches");
        _search.TextChanged += _ => Search();
        _matches.ItemSelected += SelectMatch;
        _status = _dock.GetNode<Label>("Status");
        _searchStatus = _dock.GetNode<Label>("SearchStatus");
        _scenes.ItemSelected += SelectScene;
        _beats.ItemSelected += SelectBeat;
        _text.TextChanged += () => { if (!_loading) { _dirty = true; UpdateEditLocks(); } };
        _speakers.ItemSelected += _ => { if (!_loading) { _dirty = true; UpdateEditLocks(); } };
        _dock.GetNode<Button>("Actions/Save").Pressed += Save;
        _dock.GetNode<Button>("Actions/Reload").Pressed += Reload;
        InitializeProfiles();
        _dock.GetNode<Button>("Playtest").Pressed += () => PlaySavedBeat();
        _editorDock = new EditorDock { Title = "Story Text", DefaultSlot = EditorDock.DockSlot.RightUl };
        _editorDock.AddChild(scroll);
        AddDock(_editorDock);
        Reload();
        if (OS.GetCmdlineUserArgs().Contains("--editor-roundtrip"))
            Callable.From((Action)(async () =>
            {
                try
                {
                    await RunDockSmoke();
                    await RunPlaytestSmoke();
                    AddChild(GD.Load<GDScript>("res://integration/editor/roundtrip.gd").New().As<Node>());
                }
                catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            })).CallDeferred();
    }

    public override void _ExitTree()
    {
        _playtestProcess?.Dispose(); _playtestProcess = null;
        RemoveDock(_editorDock);
        // Plugin shutdown may end the editor before another deferred-delete frame.
        _editorDock.Free();
    }

    private void Reload()
    {
        try
        {
            _loading = true;
            // Parse before replacing the last good snapshot; a failed reload must not
            // make an old selection eligible to overwrite a different on-disk story.
            var loaded = File.ReadAllText(ProjectSettings.GlobalizePath(_storyPath));
            var story = Story.Parse(loaded);
            var selectedId = _index?.Entries.FirstOrDefault(e => e.SceneIndex == _sceneIndex && e.BeatIndex == _beatIndex)?.Beat.Id;
            _loaded = loaded;
            _story = story;
            _index = new StoryAuthoringIndex(story);
            var selected = _index.Entries.FirstOrDefault(e => e.Beat.Id == selectedId);
            _sceneIndex = selected?.SceneIndex ?? 0;
            _beatIndex = selected?.BeatIndex ?? 0;
            _sceneData = _story.Chapters.SelectMany(c => c.Scenes).ToArray();
            _scenes.Clear();
            foreach (var scene in _sceneData) _scenes.AddItem(scene.Id + " · " + scene.Title);
            _speakers.Clear(); _speakers.AddItem("narrator");
            foreach (var character in _story.Characters) _speakers.AddItem(character.Id);
            _sceneIndex = Math.Min(_sceneIndex, _sceneData.Length - 1);
            _scenes.Select(_sceneIndex);
            _dirty = false; _profileDirty = false; ReloadProfiles(); PopulateBeats(); Search(); UpdateEditLocks();
            _status.Text = "Loaded canonical story.json. Save writes only the selected beat's text/speaker. Reload discards unsaved edits.";
        }
        catch (Exception error) { _status.Text = error.Message; }
        finally { _loading = false; }
    }

    private void Search()
    {
        _results = _index?.Search(_search.Text) ?? [];
        _matches.Clear();
        foreach (var result in _results)
        {
            var label = result.Label.Replace("\n", " ");
            _matches.AddItem(label[..Math.Min(160, label.Length)]);
        }
        _matches.Visible = !string.IsNullOrWhiteSpace(_search.Text);
        _searchStatus.Visible = _matches.Visible;
        _searchStatus.Text = $"{_results.Length} matching beats";
        _matches.TooltipText = $"{_results.Length} matching beats. Select a result to navigate; story order stays unchanged.";
    }

    private void SelectMatch(long index)
    {
        if (HasDraft)
        {
            _matches.DeselectAll();
            _status.Text = "Save or Reload your current edit before opening a search result.";
            return;
        }
        var selected = _results[(int)index];
        _sceneIndex = selected.SceneIndex; _beatIndex = selected.BeatIndex;
        _scenes.Select(_sceneIndex); PopulateBeats();
        _status.Text = $"Opened {selected.Beat.Id}. Context below is read-only author information, including spoilers.";
    }

    private void SelectScene(long index)
    {
        if (HasDraft) { _scenes.Select(_sceneIndex); _status.Text = "Save or Reload your current edit before changing scene."; return; }
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
        if (HasDraft) { _beats.Select(_beatIndex); _status.Text = "Save or Reload your current edit before selecting another beat."; return; }
        _beatIndex = (int)index; DisplayBeat();
    }

    private void DisplayBeat()
    {
        _loading = true;
        var beat = _sceneData[_sceneIndex].Beats[_beatIndex];
        _text.Text = beat.Text;
        _context.Text = _index!.Entries.Single(e => e.Beat.Id == beat.Id).Context;
        DisplayPreview(beat);
        for (var i = 0; i < _speakers.ItemCount; i++) if (_speakers.GetItemText(i) == beat.Speaker) _speakers.Select(i);
        _loading = false;
    }

    private void Save()
    {
        if (_profileDirty) { _status.Text = "Save or Reload your dossier draft before saving a beat."; return; }
        try
        {
            var path = ProjectSettings.GlobalizePath(_storyPath);
            var result = StoryTextEdit.Apply(_loaded, File.ReadAllText(path), _sceneData[_sceneIndex].Beats[_beatIndex].Id,
                _speakers.GetItemText(_speakers.Selected), _text.Text);
            PublishEdit(result);
        }
        catch (Exception error) { _status.Text = error.Message; }
    }

    private void PublishEdit(string result)
    {
        var path = ProjectSettings.GlobalizePath(_storyPath);
        var pending = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        try
        {
            using (var stream = new FileStream(pending, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(result);
                stream.Write(bytes); stream.Flush(true);
            }
            if (File.ReadAllText(path) != _loaded) throw new InvalidOperationException("The story changed on disk. Reload before saving.");
            File.Move(pending, path, true);
            Reload();
            _status.Text = "Saved and validated. Stable story/save IDs are unchanged. Preview reflects saved content only.";
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
    }
}
#endif
