#if TOOLS
using Godot;
using Lanternwake.Core;
using File = System.IO.File;

public partial class StoryTextPlugin
{
    // Exercises real controls/signals and persistence, but only against a temporary story copy.
    private async Task RunDockSmoke()
    {
        var originalPath = _storyPath;
        var temporary = "user://story-dock-" + Guid.NewGuid().ToString("N") + ".json";
        var path = ProjectSettings.GlobalizePath(temporary);
        void Check(bool condition, string claim)
        {
            if (!condition) throw new InvalidOperationException("Story dock smoke: " + claim);
        }
        try
        {
            _editorDock.MakeVisible();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            File.WriteAllText(path, _loaded); _storyPath = temporary; Reload();
            var target = _index!.Entries.Last(e => e.Beat.Activity is not null);
            _search.Text = target.Beat.Id; _search.EmitSignal(LineEdit.SignalName.TextChanged, _search.Text);
            Check(_results.Length == 1 && _matches.ItemCount == 1, "stable-ID search locates one beat");
            _matches.EmitSignal(ItemList.SignalName.ItemSelected, 0L);
            Check(_text.Text == target.Beat.Text && _context.Text.Contains(target.Beat.Activity!.Prompt), "search navigates to beat and gate context");
            await CheckSelectedBeatVisibility();
            _text.Text = "Dock smoke edited line. 語";
            _text.EmitSignal(TextEdit.SignalName.TextChanged);
            _search.Text = _index.Entries[0].Beat.Id; _search.EmitSignal(LineEdit.SignalName.TextChanged, _search.Text);
            _matches.EmitSignal(ItemList.SignalName.ItemSelected, 0L);
            _scenes.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
            _beats.Select(0); _beats.EnsureCurrentIsVisible();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _beats.EmitSignal(ItemList.SignalName.ItemSelected, 0L);
            Check(_sceneIndex == target.SceneIndex && _beatIndex == target.BeatIndex && _dirty, "dirty navigation stays on current beat");
            await CheckSelectedBeatVisibility();
            _dock.GetNode<Button>("Actions/Save").EmitSignal(Button.SignalName.Pressed);
            Check(!_dirty && _text.Text == "Dock smoke edited line. 語", "save reload retains selected stable ID");
            await CheckSelectedBeatVisibility();
            var saved = File.ReadAllText(path);
            Check(Story.Parse(saved).Chapters.SelectMany(c => c.Scenes).SelectMany(s => s.Beats).Single(b => b.Id == target.Beat.Id).Text == _text.Text, "saved edit reaches runtime parser");
            _text.Text = "unsaved local line"; _text.EmitSignal(TextEdit.SignalName.TextChanged);
            File.WriteAllText(path, "{ malformed");
            _dock.GetNode<Button>("Actions/Reload").EmitSignal(Button.SignalName.Pressed);
            Check(_dirty && _loaded == saved && _text.Text == "unsaved local line", "failed reload preserves last good snapshot and draft");
            _dock.GetNode<Button>("Actions/Save").EmitSignal(Button.SignalName.Pressed);
            Check(File.ReadAllText(path) == "{ malformed" && _dirty, "stale save never overwrites externally changed content");
            File.WriteAllText(path, saved);
            _dock.GetNode<Button>("Actions/Reload").EmitSignal(Button.SignalName.Pressed);
            Check(!_dirty && _text.Text == "Dock smoke edited line. 語", "reload discards draft and recovers");
            _search.Text = "no-match-unique-573269"; _search.EmitSignal(LineEdit.SignalName.TextChanged, _search.Text);
            Check(_matches.ItemCount == 0, "empty result clears prior matches");
            _search.Text = ""; _search.EmitSignal(LineEdit.SignalName.TextChanged, _search.Text);
            Check(!_matches.Visible, "clearing search restores normal navigation");
            _scenes.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
            Check(_beatIndex == 0, "scene navigation selects its first beat");
            await CheckSelectedBeatVisibility();
            RunProfileSmoke(path);
            await CheckNarrowDockLayout();
            GD.Print("LANTERNWAKE_STORY_DOCK_OK search context visible selection dirty-navigation save reload conflict recovery; canonical story untouched");
        }
        finally
        {
            _storyPath = originalPath;
            File.Delete(path);
            _search.Text = ""; _search.EmitSignal(LineEdit.SignalName.TextChanged, _search.Text); Reload();
        }
    }

    private async Task CheckSelectedBeatVisibility()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var item = _beats.GetItemRect(_beatIndex);
        var scroll = _beats.GetVScrollBar();
        if (!_beats.IsVisibleInTree() || scroll.Page <= 0 || item.Size.Y <= 0)
            throw new InvalidOperationException("Selected-beat visibility requires a laid-out visible list.");
        // Item rectangles include the panel's content margin; scrollbar values do not.
        var top = scroll.Value + _beats.GetThemeStylebox("panel").GetContentMargin(Side.Top);
        if (item.Position.Y < top || item.End.Y > top + scroll.Page)
            throw new InvalidOperationException($"Selected beat {_beatIndex} must be visible in the list: item={item}, scroll={scroll.Value}, page={scroll.Page}");
    }

    private async Task CheckNarrowDockLayout()
    {
        var scroll = GD.Load<PackedScene>("res://addons/story_text/StoryTextDock.tscn").Instantiate<ScrollContainer>();
        GetTree().Root.AddChild(scroll);
        try
        {
            scroll.Size = new Vector2(320, 600);
            scroll.GetNode<ItemList>("Content/Matches").Show();
            scroll.GetNode<Label>("Content/SearchStatus").Show();
            scroll.GetNode<Control>("Content/Profiles").Show();
            scroll.GetNode<Control>("Content/Preview").Show();
            scroll.GetNode<Label>("Content/Profiles/Notice").Text = "AUTHOR ONLY · never sent to model. Long writer guidance wraps inside the narrow dock without hiding the save controls.";
            scroll.GetNode<Label>("Content/Status").Text = "Saved and validated. Run the game to see your edit. Stable story/save IDs are unchanged.";
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var content = scroll.GetNode<Control>("Content");
            if (content.Size.X > scroll.Size.X || scroll.GetVScrollBar().MaxValue <= scroll.GetVScrollBar().Page)
                throw new InvalidOperationException("Narrow dock must fit horizontally and allow vertical overflow to scroll.");
            foreach (var target in new[] { "Content/Actions", "Content/Profiles/Save", "Content/Profiles/Reload" })
            {
                var control = scroll.GetNode<Control>(target);
                scroll.EnsureControlVisible(control);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var actions = control.GetGlobalRect();
                var viewport = scroll.GetGlobalRect();
                if (actions.Position.Y < viewport.Position.Y || actions.End.Y > viewport.End.Y)
                    throw new InvalidOperationException("Narrow dock actions must be reachable by scrolling: " + target);
            }
            scroll.ScrollVertical = 0;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var save = scroll.GetNode<Button>("Content/Profiles/Save");
            save.GrabFocus();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var focused = save.GetGlobalRect();
            var visible = scroll.GetGlobalRect();
            if (!save.HasFocus() || focused.Position.Y < visible.Position.Y || focused.End.Y > visible.End.Y)
                throw new InvalidOperationException($"Keyboard focus must scroll the narrow dock to its dossier save action: focus={save.HasFocus()}, follow={scroll.FollowFocus}, control={focused}, viewport={visible}, scroll={scroll.ScrollVertical}");
            GD.Print("LANTERNWAKE_STORY_DOCK_LAYOUT_OK 320x600; expanded dossier/preview horizontal fit, scroll-to-actions and visible keyboard focus");
        }
        finally { scroll.Free(); }
    }

}
#endif
