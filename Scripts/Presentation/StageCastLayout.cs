using Godot;

namespace Lanternwake.Presentation;

/// <summary>Reusable placement slots by cast size, with optional scene and beat overrides.</summary>
[GlobalClass, Tool]
public partial class StageCastLayout : Node3D
{
    [Export(PropertyHint.Range, "0,4,1")]
    public int CharacterCount
    {
        get => _characterCount;
        set { _characterCount = value; NotifyPropertyListChanged(); RefreshPreview(); }
    }
    private int _characterCount;
    [Export] public Node3D? Slot1 { get; set; }
    [Export] public Node3D? Slot2 { get; set; }
    [Export] public Node3D? Slot3 { get; set; }
    [Export] public Node3D? Slot4 { get; set; }

    [ExportGroup("Optional scene or beat override")]
    [Export]
    public string StorySceneId
    {
        get => _storySceneId;
        set { _storySceneId = value; RefreshPreview(); }
    }
    [Export]
    public string StartAtBeatId
    {
        get => _startAtBeatId;
        set { _startAtBeatId = value; RefreshPreview(); }
    }
    private string _storySceneId = "";
    private string _startAtBeatId = "";

    public bool IsShared => CharacterCount > 0 && StorySceneId.Length == 0 && StartAtBeatId.Length == 0;
    public bool UsesSlots => CharacterCount > 0;
    public string PreviewKey => IsShared ? CharacterCount + (CharacterCount == 1 ? " character" : " characters")
        : StartAtBeatId.Length == 0 ? StorySceneId : StorySceneId + " / " + StartAtBeatId;

    [ExportGroup("Individual character placements")]
    [Export] public StageCharacter? AdaPlacement { get; set; }
    [Export] public StageCharacter? NessaPlacement { get; set; }
    [Export] public StageCharacter? TomasPlacement { get; set; }
    [Export] public StageCharacter? SeraPlacement { get; set; }

    public Node3D? GetSlot(int index) => index switch
    {
        0 => Slot1,
        1 => Slot2,
        2 => Slot3,
        3 => Slot4,
        _ => null,
    };

    public StageCharacter? GetCharacterPlacement(string characterId) => characterId switch
    {
        "ada" => AdaPlacement,
        "nessa" => NessaPlacement,
        "tomas" => TomasPlacement,
        "sera" => SeraPlacement,
        _ => null,
    };

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        string name = property["name"].AsString();
        bool hidden = UsesSlots ? name is nameof(AdaPlacement) or nameof(NessaPlacement) or nameof(TomasPlacement) or nameof(SeraPlacement)
            : name is nameof(Slot1) or nameof(Slot2) or nameof(Slot3) or nameof(Slot4);
        if (hidden) property["usage"] = (int)(property["usage"].As<PropertyUsageFlags>() & ~PropertyUsageFlags.Editor);
    }

    private void RefreshPreview()
    {
        if (!Engine.IsEditorHint() || !IsInsideTree()) return;
        for (Node? ancestor = GetParent(); ancestor != null; ancestor = ancestor.GetParent())
            if (ancestor is StageScene stage) { stage.RefreshEditorCastPreview(); break; }
    }
}
