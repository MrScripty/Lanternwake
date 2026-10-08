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
    [Export] public Node3D? PerformanceFocus { get; set; }
    [Export] public Node3D? BellBody { get; set; }
    [Export] public Vector3 BellLoweredOffset { get; set; } = new(0, -2.1f, 0);

    [ExportGroup("Optional guided bell descent")]
    [Export] public string BellDescentStartBeatId { get; set; } = "";
    [Export] public string BellDescentFlowBeatId { get; set; } = "";
    [Export] public Camera3D? BellReleaseCamera { get; set; }
    [Export] public MeshInstance3D? BellSuspension { get; set; }
    [Export(PropertyHint.Range, "0.1,6,0.1")] public double BellTravelSeconds { get; set; } = 1.8;

    [ExportGroup("Optional cup states")]
    [Export] public Node3D? CupIntact { get; set; }
    [Export] public Node3D? CupFragments { get; set; }
    [Export] public Node3D? CupFloorHandle { get; set; }
    [Export] public Node3D? CupBoxed { get; set; }
    [Export] public Node3D? SteelMug { get; set; }
    [Export] public Camera3D? CupFloorCamera { get; set; }
    [Export] public Camera3D? CupInventoryCamera { get; set; }
    [Export] public string CupInventoryBeatId { get; set; } = "";

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
    private Vector3 _bellTarget;
    private string? _bellBeatId;
    private Tween? _bellTravel;
    private bool _motionEnabled = true;
    private Vector3 _suspensionOrigin;
    private Vector3 _suspensionScale;
    private float _suspensionHeight;

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
        if (BellBody != null) _bellOrigin = _bellTarget = BellBody.Position;
        if (BellSuspension != null)
        {
            if (BellSuspension.Mesh == null) throw new InvalidOperationException($"Stage '{Name}' has no mesh on its bell suspension binding.");
            _suspensionOrigin = BellSuspension.Position; _suspensionScale = BellSuspension.Scale;
            _suspensionHeight = BellSuspension.Mesh.GetAabb().Size.Y * _suspensionScale.Y;
        }
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

    public void ApplyAuthoredCues(string[] cues, string? currentCue = null, string? currentBeatId = null, bool playTransition = true)
    {
        bool bellStart = BellDescentStartBeatId.Length > 0 && currentBeatId == BellDescentStartBeatId;
        bool bellFlow = BellDescentFlowBeatId.Length > 0 && currentBeatId == BellDescentFlowBeatId;
        ApplyBellState(Array.IndexOf(cues, "bell_lowered") >= 0 ? 1 : bellFlow ? .9f : bellStart ? .5f : 0,
            currentBeatId, playTransition && (bellStart || bellFlow));
        bool mug = Array.IndexOf(cues, "steel_mug") >= 0;
        bool boxed = mug || Array.IndexOf(cues, "cup_boxed") >= 0;
        bool broken = boxed || Array.IndexOf(cues, "cup_broken") >= 0;
        if (CupIntact != null) CupIntact.Visible = !broken;
        if (CupFragments != null) CupFragments.Visible = broken && !boxed;
        if (CupFloorHandle != null) CupFloorHandle.Visible = broken && !mug;
        if (CupBoxed != null) CupBoxed.Visible = boxed;
        if (SteelMug != null) SteelMug.Visible = mug;
        // Persistent object state survives recovery; close views belong to the
        // current inventory/break beat, never to later cumulative history.
        if (!Engine.IsEditorHint())
        {
            var camera = BellReleaseCamera != null && (bellStart || bellFlow || currentCue == "bell_lowered") ? BellReleaseCamera
                : currentCue == "cup_broken" && broken && !boxed ? CupFloorCamera ?? StoryCamera
                : !broken && currentBeatId == CupInventoryBeatId ? CupInventoryCamera ?? StoryCamera : StoryCamera;
            if (!camera.Current) camera.MakeCurrent();
        }
    }

    private void ApplyBellState(float fraction, string? beatId, bool animate)
    {
        if (BellBody == null || Engine.IsEditorHint()) return;
        var target = _bellOrigin + BellLoweredOffset * fraction;
        // Same-beat refreshes retain their current travel; Load always settles directly.
        if (animate && _motionEnabled && beatId == _bellBeatId && target == _bellTarget) return;
        StopBellTravel(); _bellBeatId = beatId; _bellTarget = target;
        if (!animate || !_motionEnabled || BellBody.Position.IsEqualApprox(target)) { SetBellPosition(target); return; }
        _bellTravel = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _bellTravel.TweenMethod(Callable.From<Vector3>(SetBellPosition), BellBody.Position, target,
            double.IsFinite(BellTravelSeconds) ? Math.Clamp(BellTravelSeconds, .1, 6) : 1.8);
    }

    private void SetBellPosition(Vector3 position)
    {
        BellBody!.Position = position;
        if (BellSuspension == null || _suspensionHeight <= 0) return;
        // Pay out beneath the fixed top anchor without changing the authored mesh resource.
        var descent = position.Y - _bellOrigin.Y;
        BellSuspension.Position = _suspensionOrigin + Vector3.Up * (descent * .5f);
        BellSuspension.Scale = _suspensionScale * new Vector3(1, (_suspensionHeight - descent) / _suspensionHeight, 1);
    }

    private void StopBellTravel() { _bellTravel?.Kill(); _bellTravel?.Dispose(); _bellTravel = null; }
    public override void _ExitTree() => StopBellTravel();

    public void SetMotionEnabled(bool enabled)
    {
        _motionEnabled = enabled;
        if (!enabled && BellBody != null && !Engine.IsEditorHint()) { StopBellTravel(); SetBellPosition(_bellTarget); }
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
