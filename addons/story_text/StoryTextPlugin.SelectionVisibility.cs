#if TOOLS
using Godot;

public partial class StoryTextPlugin
{
    private SceneTree? _visibilityTree;
    private int _visibilityFrames;
    private bool _visibilityStopped;

    private void QueueBeatVisibility()
    {
        if (_visibilityStopped || !IsInsideTree() || !GodotObject.IsInstanceValid(_beats) || !_beats.IsInsideTree()) return;
        // A selection and container/scrollbar changes can share a frame. Coalesce
        // them, then allow a completed layout/draw before ensuring the latest row.
        _visibilityFrames = 2;
        if (_visibilityTree is not null) return;
        _visibilityTree = GetTree();
        _visibilityTree.ProcessFrame += CompleteBeatVisibility;
    }

    private void CompleteBeatVisibility()
    {
        if (--_visibilityFrames > 0) return;
        CancelBeatVisibility();
        if (_visibilityStopped || !IsInsideTree() || !GodotObject.IsInstanceValid(_beats)
            || !_beats.IsInsideTree() || !_beats.IsVisibleInTree()) return;
        if (_beatIndex >= 0 && _beatIndex < _beats.ItemCount && _beats.GetSelectedItems().Contains(_beatIndex))
            _beats.EnsureCurrentIsVisible();
    }

    private void CancelBeatVisibility()
    {
        if (_visibilityTree is not null && GodotObject.IsInstanceValid(_visibilityTree))
            _visibilityTree.ProcessFrame -= CompleteBeatVisibility;
        _visibilityTree = null; _visibilityFrames = 0;
    }

    private void StopBeatVisibility()
    {
        _visibilityStopped = true;
        CancelBeatVisibility();
    }
}
#endif
