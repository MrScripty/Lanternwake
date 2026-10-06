using Godot;

namespace Lanternwake.Presentation;

/// <summary>Editable character prefab and three authored static performance families.</summary>
[Tool, GlobalClass]
public partial class StageCharacter : Node3D
{
    [Export] public StageMotion? Breathing { get; set; }
    [Export] public string CharacterId { get; set; } = "";
    [ExportGroup("Performance joints")]
    [Export] public Node3D? Torso { get; set; }
    [Export] public Node3D? Head { get; set; }
    [Export] public Node3D? LeftShoulder { get; set; }
    [Export] public Node3D? LeftElbow { get; set; }
    [Export] public Node3D? RightShoulder { get; set; }
    [Export] public Node3D? RightElbow { get; set; }
    [ExportGroup("Authored poses")]
    [Export] public CharacterPose? Listening { get; set; }
    [Export] public CharacterPose? Working { get; set; }
    [Export] public CharacterPose? Resting { get; set; }
    public enum PoseFamily { Listening, Working, Resting }
    public PoseFamily? ActivePose { get; private set; }
    private Node3D[] _joints = [];
    private Vector3[] _baseRotations = [];
    private bool _breathingEnabled;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        _joints = new[] { Torso, Head, LeftShoulder, LeftElbow, RightShoulder, RightElbow }.OfType<Node3D>().ToArray();
        if (_joints.Length != 6 || Listening is null || Working is null || Resting is null)
            throw new InvalidOperationException($"Character '{Name}' requires six performance joints and three authored poses.");
        _baseRotations = _joints.Select(joint => joint.RotationDegrees).ToArray();
        _breathingEnabled = Breathing?.Enabled ?? false;
    }

    public void ApplyPose(PoseFamily? family, float facingDegrees = 0, bool still = false)
    {
        if (Engine.IsEditorHint()) return;
        ActivePose = family;
        var pose = family switch { PoseFamily.Listening => Listening, PoseFamily.Working => Working, PoseFamily.Resting => Resting, _ => null };
        Vector3[] angles = pose is null ? new Vector3[6] : [pose.TorsoDegrees, pose.HeadDegrees, pose.LeftShoulderDegrees,
            pose.LeftElbowDegrees, pose.RightShoulderDegrees, pose.RightElbowDegrees];
        var turn = Mathf.Clamp(facingDegrees, -35, 35);
        angles[0].Y += turn * .35f; angles[1].Y += turn * .65f;
        for (var i = 0; i < _joints.Length; i++) _joints[i].RotationDegrees = _baseRotations[i] + angles[i];
        if (Breathing is not null) Breathing.Enabled = _breathingEnabled && !still && family != PoseFamily.Resting;
    }
}
