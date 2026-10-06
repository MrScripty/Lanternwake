using Godot;

namespace Lanternwake.Presentation;

/// <summary>Instantiates authored scenes; never constructs or rewrites their artwork.</summary>
[GlobalClass]
[Tool]
public partial class StageDirector : Node3D
{
    [ExportGroup("Locations")]
    [Export] public PackedScene Harbor { get; set; } = null!;
    [Export] public PackedScene KeeperHouse { get; set; } = null!;
    [Export] public PackedScene Archive { get; set; } = null!;
    [Export] public PackedScene LanternRoom { get; set; } = null!;
    [Export] public PackedScene TideCave { get; set; } = null!;

    [ExportGroup("Characters")]
    [Export] public PackedScene Ada { get; set; } = null!;
    [Export] public PackedScene Nessa { get; set; } = null!;
    [Export] public PackedScene Tomas { get; set; } = null!;
    [Export] public PackedScene Sera { get; set; } = null!;
    [ExportGroup("Authored performance")]
    [Export] public PerformanceDirection? Performance { get; set; }

    private readonly List<Node3D> _actors = new();
    private StageScene? _stage;
    private string _location = "";
    private string? _castKey;
    private bool _motionEnabled = true;

    [ExportGroup("Playback")]
    [Export]
    public bool MotionEnabled
    {
        get => _motionEnabled;
        set
        {
            _motionEnabled = value;
            _stage?.SetMotionEnabled(value);
        }
    }

    /// <summary>Repeated beats retain the authored stage and its current ambient motion.</summary>
    public void ShowLocation(string location, string timeOfDay, string[] characterIds)
    {
        ArgumentNullException.ThrowIfNull(characterIds);
        PackedScene scene = location switch
        {
            "harbor" => Harbor,
            "keeper_house" => KeeperHouse,
            "archive" => Archive,
            "lantern_room" => LanternRoom,
            "tide_cave" => TideCave,
            _ => throw new ArgumentException($"Unknown Lanternwake location '{location}'.", nameof(location)),
        };
        if (scene == null)
            throw new InvalidOperationException($"The {location} scene is not assigned in StageDirector's Inspector.");

        if (_location != location)
        {
            ClearActors();
            if (_stage != null)
            {
                RemoveChild(_stage);
                _stage.QueueFree();
            }
            _stage = scene.Instantiate<StageScene>();
            AddChild(_stage);
            _location = location;
            _castKey = null;
            _stage.SetMotionEnabled(_motionEnabled);
        }
        _stage!.ApplyTimeOfDay(timeOfDay);
        string[] visibleIds = Array.FindAll(characterIds, id => id is not ("ivo" or "operator" or "clerk"));
        string castKey = string.Join('|', visibleIds);
        if (_castKey == castKey) return;
        ClearActors();
        float spacing = visibleIds.Length <= 2 ? _stage.PairSpacing : visibleIds.Length <= 3 ? _stage.TrioSpacing : _stage.EnsembleSpacing;
        for (int i = 0; i < visibleIds.Length; i++)
        {
            PackedScene character = visibleIds[i] switch
            {
                "ada" => Ada,
                "nessa" => Nessa,
                "tomas" => Tomas,
                "sera" => Sera,
                _ => throw new ArgumentException($"Unknown visible character '{visibleIds[i]}'.", nameof(characterIds)),
            };
            if (character == null)
                throw new InvalidOperationException($"The {visibleIds[i]} character scene is not assigned in StageDirector's Inspector.");
            var actor = character.Instantiate<StageCharacter>();
            actor.Position += Vector3.Right * ((i - (visibleIds.Length - 1) * 0.5f) * spacing);
            actor.RotationDegrees += Vector3.Up * (_stage.FacingDegrees + i * _stage.FacingStepDegrees);
            if (actor.Breathing is { } breathing) breathing.Phase += i * 2;
            _stage.CastOrigin.AddChild(actor);
            StageScene.SetMotionEnabledBelow(actor, _motionEnabled);
            _actors.Add(actor);
        }
        _castKey = castKey;
    }

    public void ApplyAuthoredCues(string[] cues, string? currentCue = null, string? currentBeatId = null, bool playTransition = true)
    {
        ArgumentNullException.ThrowIfNull(cues);
        _stage?.ApplyAuthoredCues(cues, currentCue, currentBeatId, playTransition);
    }

    public void ApplyPerformance(string sceneId, string beatId, string speakerId)
    {
        var actors = _actors.OfType<StageCharacter>().ToArray();
        var directed = Performance?.SceneIds.Contains(sceneId) == true;
        var resting = Performance?.RestingSceneIds.Contains(sceneId) == true;
        var still = Performance?.StillSceneIds.Contains(sceneId) == true;
        string? workerId = null;
        if (directed) Performance!.WorkingBeatActors.TryGetValue(beatId, out workerId);
        var speaker = actors.FirstOrDefault(actor => actor.CharacterId == speakerId);
        var worker = actors.FirstOrDefault(actor => actor.CharacterId == workerId);
        var focus = worker ?? speaker;
        foreach (var actor in actors)
        {
            StageCharacter.PoseFamily? family = !directed ? null : actor == worker || (!resting && actor == speaker)
                ? StageCharacter.PoseFamily.Working : resting ? StageCharacter.PoseFamily.Resting : StageCharacter.PoseFamily.Listening;
            float turn = 0;
            if (directed)
            {
                // Listener attention belongs to a living speaker/worker; recorded
                // voices and object work use the authored fixed focus instead.
                var target = focus != null && focus != actor ? focus.GlobalPosition : _stage?.PerformanceFocus?.GlobalPosition;
                if (target is { } position)
                {
                    var direction = actor.GlobalBasis.Inverse() * (position - actor.GlobalPosition);
                    if (new Vector2(direction.X, direction.Z).LengthSquared() > .001f)
                        turn = Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z));
                }
            }
            actor.ApplyPose(family, turn, still);
        }
    }

    private void ClearActors()
    {
        foreach (var actor in _actors)
        {
            actor.GetParent().RemoveChild(actor);
            actor.QueueFree();
        }
        _actors.Clear();
    }
}
