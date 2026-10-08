using Godot;

namespace Lanternwake.Presentation;

/// <summary>Bindings and optional variations for one editor-authored location.</summary>
[GlobalClass]
[Tool]
public partial class StageScene : Node3D
{
    [ExportGroup("Scene bindings")]
    [Export] public Camera3D StoryCamera { get; set; } = null!;
    [Export] public WorldEnvironment Atmosphere { get; set; } = null!;
    [Export] public DirectionalLight3D KeyLight { get; set; } = null!;
    [Export] public Node3D CastOrigin { get; set; } = null!;
    [Export] public Node3D? BellBody { get; set; }
    [Export] public Vector3 BellLoweredOffset { get; set; } = new(0, -2.1f, 0);

    [ExportGroup("Optional time-of-day looks")]
    [Export] public Godot.Environment? DawnEnvironment { get; set; }
    [Export] public Godot.Environment? DuskEnvironment { get; set; }
    [Export] public Color DawnKeyLightColor { get; set; } = new("f3cfb2");
    [Export] public Color DuskKeyLightColor { get; set; } = new("deb5a1");
    [Export] public float DawnKeyLightEnergyMultiplier { get; set; } = 1;
    [Export] public float DuskKeyLightEnergyMultiplier { get; set; } = 1;

    [ExportGroup("Cast placement")]
    [Export] public float PairSpacing { get; set; } = 3.1f;
    [Export] public float TrioSpacing { get; set; } = 2.65f;
    [Export] public float EnsembleSpacing { get; set; } = 2.1f;
    [Export] public float FacingDegrees { get; set; } = 24;
    [Export] public float FacingStepDegrees { get; set; } = -12;

    [ExportGroup("Fallback characters")]
    [Export] public StageCharacter? AdaPlacement { get; set; }
    [Export] public StageCharacter? NessaPlacement { get; set; }
    [Export] public StageCharacter? TomasPlacement { get; set; }
    [Export] public StageCharacter? SeraPlacement { get; set; }

    [ExportGroup("Cast preview")]
    [Export]
    public string PreviewLayout
    {
        get => _previewStoryScene;
        set
        {
            _previewStoryScene = value;
            if (Engine.IsEditorHint() && IsNodeReady()) ApplyEditorCastPreview();
        }
    }

    private string _previewStoryScene = "Default";
    private StageCastLayout? _activeCastLayout;

    private Godot.Environment _nightEnvironment = null!;
    private Color _nightLightColor;
    private float _nightEnergy;
    private Vector3 _bellOrigin;

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            ApplyEditorCastPreview();
            NotifyPropertyListChanged();
            return;
        }
        if (Atmosphere == null || KeyLight == null || StoryCamera == null || CastOrigin == null)
            throw new InvalidOperationException($"Stage '{Name}' has an unassigned scene binding in the Inspector.");
        _nightEnvironment = Atmosphere.Environment;
        _nightLightColor = KeyLight.LightColor;
        _nightEnergy = KeyLight.LightEnergy;
        if (BellBody != null) _bellOrigin = BellBody.Position;
        StoryCamera.MakeCurrent();
    }

    public void ApplyTimeOfDay(string timeOfDay)
    {
        string time = (timeOfDay ?? "").ToLowerInvariant();
        bool dawn = time.Contains("dawn") || time.Contains("morning") || time.Contains("sunrise");
        bool dusk = time.Contains("dusk") || time.Contains("sunset") || time.Contains("evening");
        Atmosphere.Environment = dawn ? DawnEnvironment ?? _nightEnvironment : dusk ? DuskEnvironment ?? _nightEnvironment : _nightEnvironment;
        KeyLight.LightColor = dawn ? DawnKeyLightColor : dusk ? DuskKeyLightColor : _nightLightColor;
        KeyLight.LightEnergy = _nightEnergy * (dawn ? DawnKeyLightEnergyMultiplier : dusk ? DuskKeyLightEnergyMultiplier : 1);
    }

    public void ApplyAuthoredCues(string[] cues)
    {
        if (BellBody != null) BellBody.Position = _bellOrigin + (Array.IndexOf(cues, "bell_lowered") >= 0 ? BellLoweredOffset : Vector3.Zero);
    }

    public void SetMotionEnabled(bool enabled)
    {
        SetMotionEnabledBelow(this, enabled);
        // Slot meshes are static authoring references, even while the real cast animates.
        foreach (var layout in CastLayouts().Where(layout => layout.UsesSlots))
            for (int i = 0; i < layout.CharacterCount; i++)
                if (layout.GetSlot(i) is { } slot) SetMotionEnabledBelow(slot, false);
    }

    /// <summary>Native character instances provide visible, editable placement and lighting targets.</summary>
    public StageCharacter? GetCharacterPlacement(string characterId) =>
        _activeCastLayout?.GetCharacterPlacement(characterId) ?? GetDefaultCharacterPlacement(characterId);

    private StageCharacter? GetDefaultCharacterPlacement(string characterId) => characterId switch
    {
        "ada" => AdaPlacement,
        "nessa" => NessaPlacement,
        "tomas" => TomasPlacement,
        "sera" => SeraPlacement,
        _ => null,
    };

    public void HidePlacedCharacters()
    {
        // Unassigned preview instances must also stay out of the story's active cast.
        foreach (var character in CastOrigin.FindChildren("*", "", true, false).OfType<StageCharacter>())
            character.Visible = false;
        foreach (var layout in CastLayouts()) layout.Visible = false;
        _activeCastLayout = null;
    }

    /// <summary>Beat layouts take effect from their trigger through the rest of this scene, in story order.</summary>
    public StageCastLayout? FindCastLayout(string storySceneId, string[]? reachedBeatIds = null, int characterCount = 0)
    {
        var layouts = CastLayouts().Where(layout => !layout.IsShared && layout.StorySceneId == storySceneId).ToArray();
        foreach (var beatId in (reachedBeatIds ?? Array.Empty<string>()).Reverse())
        {
            var beatLayout = layouts.SingleOrDefault(layout => layout.StartAtBeatId == beatId);
            if (beatLayout != null) return beatLayout;
        }
        return layouts.SingleOrDefault(layout => layout.StartAtBeatId.Length == 0)
            ?? CastLayouts().SingleOrDefault(layout => layout.IsShared && layout.CharacterCount == characterCount);
    }

    public void SelectCastLayout(string storySceneId, string[]? reachedBeatIds = null, int characterCount = 0)
    {
        HidePlacedCharacters();
        _activeCastLayout = FindCastLayout(storySceneId, reachedBeatIds, characterCount);
        if (_activeCastLayout != null) _activeCastLayout.Visible = true;
    }

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (property["name"].AsString() != nameof(PreviewLayout)) return;
        property["hint"] = (int)PropertyHint.Enum;
        property["hint_string"] = string.Join(',', new[] { "Default" }.Concat(CastLayouts().Select(layout => layout.PreviewKey)));
    }

    public void RefreshEditorCastPreview()
    {
        NotifyPropertyListChanged();
        if (Engine.IsEditorHint() && IsNodeReady()) ApplyEditorCastPreview();
    }

    private IEnumerable<StageCastLayout> CastLayouts() => CastOrigin == null
        ? Enumerable.Empty<StageCastLayout>()
        : CastOrigin.GetChildren().OfType<StageCastLayout>();

    private void ApplyEditorCastPreview()
    {
        if (CastOrigin == null) return;
        var selected = CastLayouts().FirstOrDefault(layout => layout.PreviewKey == _previewStoryScene);
        foreach (var layout in CastLayouts()) layout.Visible = layout == selected;
        foreach (var character in CastOrigin.GetChildren().OfType<StageCharacter>())
            character.Visible = selected == null;
        // Preview visibility belongs to layout parents; individual authored poses stay untouched.
    }

    public static void SetMotionEnabledBelow(Node parent, bool enabled)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is StageMotion motion) motion.MotionAllowed = enabled;
            SetMotionEnabledBelow(child, enabled);
        }
    }
}
