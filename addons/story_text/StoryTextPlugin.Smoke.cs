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
            File.WriteAllText(path, _loaded); _storyPath = temporary; Reload();
            var target = _index!.Entries.Last(e => e.Beat.Activity is not null);
            _search.Text = target.Beat.Id; _search.EmitSignal(LineEdit.SignalName.TextChanged, _search.Text);
            Check(_results.Length == 1 && _matches.ItemCount == 1, "stable-ID search locates one beat");
            _matches.EmitSignal(ItemList.SignalName.ItemSelected, 0L);
            Check(_text.Text == target.Beat.Text && _context.Text.Contains(target.Beat.Activity!.Prompt), "search navigates to beat and gate context");
            _text.Text = "Dock smoke edited line. 語";
            _text.EmitSignal(TextEdit.SignalName.TextChanged);
            _search.Text = _index.Entries[0].Beat.Id; _search.EmitSignal(LineEdit.SignalName.TextChanged, _search.Text);
            _matches.EmitSignal(ItemList.SignalName.ItemSelected, 0L);
            _scenes.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
            _beats.EmitSignal(ItemList.SignalName.ItemSelected, 0L);
            Check(_sceneIndex == target.SceneIndex && _beatIndex == target.BeatIndex && _dirty, "dirty navigation stays on current beat");
            _dock.GetNode<Button>("Actions/Save").EmitSignal(Button.SignalName.Pressed);
            Check(!_dirty && _text.Text == "Dock smoke edited line. 語", "save reload retains selected stable ID");
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
            await CheckNarrowDockLayout();
            GD.Print("LANTERNWAKE_STORY_DOCK_OK search context dirty-navigation save reload conflict recovery; canonical story untouched");
        }
        finally
        {
            _storyPath = originalPath;
            File.Delete(path);
            _search.Text = ""; _search.EmitSignal(LineEdit.SignalName.TextChanged, _search.Text); Reload();
        }
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
            scroll.GetNode<Label>("Content/Status").Text = "Saved and validated. Run the game to see your edit. Stable story/save IDs are unchanged.";
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var content = scroll.GetNode<Control>("Content");
            if (content.Size.X > scroll.Size.X || scroll.GetVScrollBar().MaxValue <= scroll.GetVScrollBar().Page)
                throw new InvalidOperationException("Narrow dock must fit horizontally and allow vertical overflow to scroll.");
            scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var actions = scroll.GetNode<Control>("Content/Actions").GetGlobalRect();
            var viewport = scroll.GetGlobalRect();
            if (actions.Position.Y < viewport.Position.Y || actions.End.Y > viewport.End.Y)
                throw new InvalidOperationException("Narrow dock save/reload actions must be reachable by scrolling.");
            GD.Print("LANTERNWAKE_STORY_DOCK_LAYOUT_OK 320x600; horizontal fit and scroll-to-actions");
        }
        finally { scroll.Free(); }
    }

}
#endif
