using Godot;

namespace Lanternwake.Presentation;

/// <summary>Additive motion for an authored parent node. Base transforms are captured on entry.</summary>
[GlobalClass]
[Tool]
public partial class StageMotion : Node
{
    public enum MotionKind { BoatFloat, LampFlicker, RainFall, BeaconSpin, Breathing, Water }

    [Export] public Node3D Target { get; set; } = null!;
    [Export] public MotionKind Kind { get; set; }
    [Export] public bool Enabled { get; set; } = true;
    [Export] public float Phase { get; set; }
    [Export] public float Amount { get; set; } = 0.09f;
    [Export] public float Speed { get; set; } = 0.8f;
    [Export] public float RainFirstFallDistance { get; set; } = 1;
    [Export] public float RainHeight { get; set; } = 13;
    [Export] public float RainSlant { get; set; } = 0.16f;

    public bool MotionAllowed { get; set; } = true;

    private Node3D _target = null!;
    private Vector3 _position, _rotation, _scale;
    private float _energy, _elapsed, _waterOrigin;
    private ShaderMaterial? _water;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        _target = Target ?? throw new InvalidOperationException($"Motion '{Name}' has no Inspector target.");
        _position = _target.Position;
        _rotation = _target.Rotation;
        _scale = _target.Scale;
        if (_target is OmniLight3D light) _energy = light.LightEnergy;
        if (Kind == MotionKind.Water && _target is MeshInstance3D mesh && mesh.MaterialOverride is ShaderMaterial material)
        {
            // Each instance owns its animation clock; resource edits remain the authored source.
            _water = (ShaderMaterial)material.Duplicate();
            mesh.MaterialOverride = _water;
            _waterOrigin = _water.GetShaderParameter("elapsed").AsSingle();
        }
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() || !Enabled || !MotionAllowed) return;
        _elapsed += (float)Math.Min(delta, 0.1);
        switch (Kind)
        {
            case MotionKind.BoatFloat:
                _target.Position = _position + Vector3.Up * (Mathf.Sin(_elapsed * Speed + Phase) * Amount);
                _target.Rotation = _rotation + new Vector3(0, 0, Mathf.Sin(_elapsed * 0.65f + Phase) * Amount * 0.4f);
                break;
            case MotionKind.LampFlicker when _target is OmniLight3D light:
                light.LightEnergy = _energy * (0.96f + 0.025f * Mathf.Sin(_elapsed * 3.2f + Phase) + 0.015f * Mathf.Sin(_elapsed * 7.1f));
                break;
            case MotionKind.RainFall:
                if (RainHeight <= 0) return;
                float travel = _elapsed * Speed;
                float relativeY = travel <= RainFirstFallDistance ? -travel
                    : -RainFirstFallDistance + RainHeight - Mathf.PosMod(travel - RainFirstFallDistance, RainHeight);
                _target.Position = _position + new Vector3(relativeY * RainSlant, relativeY, 0);
                break;
            case MotionKind.BeaconSpin:
                _target.Rotation = _rotation + Vector3.Up * (_elapsed * Speed);
                break;
            case MotionKind.Breathing:
                _target.Scale = _scale * new Vector3(1, 1 + Mathf.Sin(_elapsed * Speed + Phase) * Amount, 1);
                break;
            case MotionKind.Water:
                _water?.SetShaderParameter("elapsed", _waterOrigin + _elapsed);
                break;
        }
    }
}
