#if TOOLS
using Godot;

public partial class StoryTextPlugin
{
    private async Task RunColdSelectionSmoke()
    {
        var scroll = (ScrollContainer)_dock.GetParent();
        var parent = scroll.GetParent();
        var size = scroll.Size;
        var theme = scroll.Theme;
        var minimum = _beats.CustomMinimumSize;
        try
        {
            // Use the real bound controls at the default narrow width. Model the
            // viewport's final cold-layout reduction after the first list draw.
            parent.RemoveChild(scroll); GetTree().Root.AddChild(scroll);
            scroll.Theme = EditorInterface.Singleton.GetEditorTheme();
            scroll.Size = new(290, 600);
            _beats.CustomMinimumSize = new(minimum.X, minimum.Y + 16);
            _search.Text = "ch5_s5_evidence";
            _search.EmitSignal(LineEdit.SignalName.TextChanged, _search.Text);
            if (_results.Length != 1) throw new InvalidOperationException("Cold selection requires the exact late evidence result.");
            _matches.EmitSignal(ItemList.SignalName.ItemSelected, 0L);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _beats.CustomMinimumSize = minimum;
            await CheckSelectedBeatVisibility();
            if (_sceneData[_sceneIndex].Beats[_beatIndex].Id != "ch5_s5_evidence")
                throw new InvalidOperationException("Cold selection changed the stable beat ID.");
            var first = _index!.Entries[0].Beat.Id;
            var focus = GetViewport().GuiGetFocusOwner();
            for (var attempt = 0; attempt < 6; attempt++)
            {
                foreach (var id in new[] { first, "ch5_s5_evidence", first })
                {
                    _search.Text = id; _search.EmitSignal(LineEdit.SignalName.TextChanged, id);
                    _matches.EmitSignal(ItemList.SignalName.ItemSelected, 0L);
                }
            }
            await CheckSelectedBeatVisibility();
            if (_sceneData[_sceneIndex].Beats[_beatIndex].Id != first || GetViewport().GuiGetFocusOwner() != focus)
                throw new InvalidOperationException("Rapid selection must keep the latest row and existing keyboard focus.");
            _search.Text = "ch5_s5_evidence"; _search.EmitSignal(LineEdit.SignalName.TextChanged, _search.Text);
            _matches.EmitSignal(ItemList.SignalName.ItemSelected, 0L);
            await CheckSelectedBeatVisibility();
            await CheckVisibilityTeardown();
            GD.Print("LANTERNWAKE_STORY_DOCK_COLD_SELECTION_OK 290px real controls; late viewport settlement, latest rapid selection and cancelled teardown");
        }
        finally
        {
            _beats.CustomMinimumSize = minimum;
            GetTree().Root.RemoveChild(scroll); parent.AddChild(scroll);
            scroll.Theme = theme; scroll.Size = size;
            _search.Text = ""; _search.EmitSignal(LineEdit.SignalName.TextChanged, _search.Text);
            Reload();
        }
    }

    private async Task CheckVisibilityTeardown()
    {
        var live = _beats;
        var index = _beatIndex;
        var fixture = new ItemList();
        GetTree().Root.AddChild(fixture);
        fixture.AddItem("Owned pending selection"); fixture.Select(0);
        try
        {
            _beats = fixture; _beatIndex = 0;
            QueueBeatVisibility();
            if (_visibilityTree is null) throw new InvalidOperationException("Teardown fixture must own a pending request.");
            StopBeatVisibility(); fixture.Free();
            // The same stop path runs before the actual plugin frees its dock.
            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_visibilityTree is not null || _visibilityFrames != 0)
                throw new InvalidOperationException("Teardown retained a visibility callback.");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(fixture)) fixture.Free();
            _beats = live; _beatIndex = index; _visibilityStopped = false;
            QueueBeatVisibility();
        }
    }
}
#endif
