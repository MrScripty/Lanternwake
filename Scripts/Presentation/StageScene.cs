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

    private Godot.Environment _nightEnvironment = null!;
    private Color _nightLightColor;
    private float _nightEnergy;
    private Vector3 _bellOrigin;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
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

    public void ApplyAuthoredCues(string[] cues, string? currentCue = null, string? currentBeatId = null)
    {
        if (BellBody != null) BellBody.Position = _bellOrigin + (Array.IndexOf(cues, "bell_lowered") >= 0 ? BellLoweredOffset : Vector3.Zero);
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
            var camera = currentCue == "cup_broken" && broken && !boxed ? CupFloorCamera ?? StoryCamera
                : !broken && currentBeatId == CupInventoryBeatId ? CupInventoryCamera ?? StoryCamera : StoryCamera;
            if (!camera.Current) camera.MakeCurrent();
        }
    }

    public void SetMotionEnabled(bool enabled) => SetMotionEnabledBelow(this, enabled);

    public static void SetMotionEnabledBelow(Node parent, bool enabled)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is StageMotion motion) motion.MotionAllowed = enabled;
            SetMotionEnabledBelow(child, enabled);
        }
    }
}
