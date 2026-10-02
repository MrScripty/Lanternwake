using System;
using System.Collections.Generic;
using Godot;

namespace Lanternwake.Presentation;

/// <summary>
/// Owns the visual-novel's disposable, procedural sets. Invoke on Godot's scene
/// thread; it does not own narrative state, input, or the dialogue overlay.
/// </summary>
public partial class StageDirector : Node3D
{
    private readonly Dictionary<string, StandardMaterial3D> _materials = new();
    private readonly List<(Node3D Node, Vector3 Origin, float Phase, float Amount)> _floating = new();
    private readonly List<(OmniLight3D Light, float Energy, float Phase)> _lamps = new();
    private readonly List<(Node3D Node, float Speed)> _rain = new();
    private readonly List<ShaderMaterial> _waterMaterials = new();
    private Node3D _set = null!;
    private Node3D _cast = null!;
    private Camera3D _camera = null!;
    private DirectionalLight3D _keyLight = null!;
    private Godot.Environment _environment = null!;
    private Node3D? _beacon, _bellBody;
    private string _location = "";
    private string _castKey = "";
    private float _elapsed;
    private bool _initialized;

    // This also freezes water, rain, the beacon and breathing for accessibility.
    public bool MotionEnabled { get; set; } = true;

    private const string Ink = "111c28";
    private const string Midnight = "192d39";
    private const string Teal = "315b60";
    private const string SeaGlass = "8faea4";
    private const string Stone = "546c70";
    private const string PaleStone = "a5b0a1";
    private const string Wood = "5b4944";
    private const string WarmWood = "82604a";
    private const string Brass = "b49359";
    private const string Amber = "ffd08a";
    private const string Coral = "aa6f61";
    private const string Paper = "d2c9aa";

    public override void _Ready()
    {
        EnsureRig();
    }

    /// <summary>Replace the set only when necessary; repeated dialogue beats keep it alive.</summary>
    public void ShowLocation(string location, string timeOfDay, string[] characterIds)
    {
        ArgumentNullException.ThrowIfNull(characterIds);
        if (location is not ("harbor" or "keeper_house" or "archive" or "lantern_room" or "tide_cave"))
        {
            throw new ArgumentException($"Unknown Lanternwake location '{location}'.", nameof(location));
        }

        EnsureRig();
        if (_location != location)
        {
            ClearChildren(_set);
            ClearChildren(_cast);
            _floating.Clear();
            _lamps.Clear();
            _rain.Clear();
            _waterMaterials.Clear();
            _beacon = null; _bellBody = null;
            _castKey = "";
            _location = location;
            switch (location)
            {
                case "harbor": BuildHarbor(); break;
                case "keeper_house": BuildKeeperHouse(); break;
                case "archive": BuildArchive(); break;
                case "lantern_room": BuildLanternRoom(); break;
                case "tide_cave": BuildTideCave(); break;
            }
        }

        ApplyTimeOfDay(timeOfDay);
        string castKey = string.Join('|', characterIds);
        if (_castKey != castKey || _cast.GetChildCount() == 0)
        {
            // Actors are owned separately from ambient motion to avoid retaining a
            // freed actor after a conversation changes its visible participants.
            ClearChildren(_cast);
            BuildCast(characterIds);
            _castKey = castKey;
        }
    }

    public void ApplyAuthoredCues(string[] cues)
    {
        if (_bellBody is not null) _bellBody.Position = new Vector3(0, Array.IndexOf(cues, "bell_lowered") >= 0 ? -2.1f : 0, 0);
    }

    public override void _Process(double delta)
    {
        if (!_initialized || !MotionEnabled)
        {
            return;
        }

        _elapsed += (float)Math.Min(delta, 0.1);
        foreach (var item in _floating)
        {
            item.Node.Position = item.Origin + Vector3.Up * (Mathf.Sin(_elapsed * 0.8f + item.Phase) * item.Amount);
            item.Node.Rotation = new Vector3(0, item.Node.Rotation.Y, Mathf.Sin(_elapsed * 0.65f + item.Phase) * item.Amount * 0.4f);
        }
        foreach (var lamp in _lamps)
        {
            lamp.Light.LightEnergy = lamp.Energy * (0.96f + 0.025f * Mathf.Sin(_elapsed * 3.2f + lamp.Phase) + 0.015f * Mathf.Sin(_elapsed * 7.1f));
        }
        foreach (var drop in _rain)
        {
            Vector3 position = drop.Node.Position;
            position.Y -= (float)delta * drop.Speed;
            position.X -= (float)delta * drop.Speed * 0.16f;
            if (position.Y < -1)
            {
                position.Y += 13;
                position.X += 2.08f;
            }
            drop.Node.Position = position;
        }
        foreach (var water in _waterMaterials)
        {
            water.SetShaderParameter("elapsed", _elapsed);
        }
        if (_beacon != null)
        {
            _beacon.Rotation = new Vector3(0, _elapsed * 0.13f, 0);
        }
        foreach (Node child in _cast.GetChildren())
        {
            if (child is Node3D actor)
            {
                actor.Scale = new Vector3(1, 1 + Mathf.Sin(_elapsed * 1.6f + actor.GetIndex() * 2) * 0.004f, 1);
            }
        }
    }

    private void EnsureRig()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;
        _environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("192b3b"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("91b4bd"),
            AmbientLightEnergy = 0.65f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
            FogEnabled = true,
            FogLightColor = new Color("38575f"),
            FogLightEnergy = 0.35f,
            FogDensity = 0.008f,
        };
        AddChild(new WorldEnvironment { Name = "IslandAtmosphere", Environment = _environment });
        _keyLight = new DirectionalLight3D
        {
            Name = "Moonlight",
            LightColor = new Color("b7d4d9"),
            LightEnergy = 1.15f,
            RotationDegrees = new Vector3(-42, -34, 0),
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 65,
        };
        AddChild(_keyLight);
        AddChild(new DirectionalLight3D
        {
            Name = "SeaFill",
            LightColor = new Color("759aaf"),
            LightEnergy = 0.48f,
            RotationDegrees = new Vector3(-15, 140, 0),
        });
        _camera = new Camera3D
        {
            Name = "StoryCamera",
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 15.2f,
            Near = 0.1f,
            Far = 180,
            Current = true,
        };
        AddChild(_camera);
        _set = new Node3D { Name = "LocationSet" };
        _cast = new Node3D { Name = "VisibleCharacters" };
        AddChild(_set);
        AddChild(_cast);
        SetCamera(new Vector3(13, 11, 23), new Vector3(0, 1.7f, 0), 15.2f);
    }

    private void ApplyTimeOfDay(string timeOfDay)
    {
        string time = (timeOfDay ?? "").ToLowerInvariant();
        bool dawn = time.Contains("dawn") || time.Contains("morning") || time.Contains("sunrise");
        bool evening = time.Contains("dusk") || time.Contains("sunset") || time.Contains("evening");
        bool interior = _location is "keeper_house" or "archive" or "tide_cave";
        _environment.BackgroundColor = new Color(dawn ? "566f79" : evening ? "49475b" : "192b3b");
        _environment.FogLightColor = new Color(dawn ? "7e9695" : "38575f");
        _environment.FogDensity = interior ? 0.003f : 0.008f;
        _environment.AmbientLightEnergy = interior ? 0.5f : dawn ? 0.83f : 0.63f;
        _keyLight.LightColor = new Color(dawn ? "f3cfb2" : evening ? "deb5a1" : "b7d4d9");
        _keyLight.LightEnergy = interior ? 0.72f : dawn ? 1.35f : 1.1f;
    }

    private void SetCamera(Vector3 position, Vector3 target, float size)
    {
        _camera.Position = position;
        _camera.LookAt(target);
        _camera.Size = size;
    }

    private void BuildHarbor()
    {
        SetCamera(new Vector3(14, 12, 24), new Vector3(0, 3.3f, -1), 19.5f);
        BuildSea(new Vector3(0, -0.68f, -4), new Vector2(100, 95));
        BuildDistantIslands();
        // The lighthouse, quay and fishing boat form three readable silhouettes.
        Rock(_set, new Vector3(-5.8f, -0.35f, -6), new Vector3(4.8f, 2.1f, 4), Stone, 11);
        Rock(_set, new Vector3(-8, -0.55f, -4), new Vector3(2.8f, 1.4f, 3.5f), Midnight, 19);
        BuildLighthouse(_set, new Vector3(-5.8f, 0.8f, -6.5f), 0.88f);
        Box(_set, new Vector3(1, -0.35f, 2.6f), new Vector3(15, 0.9f, 6.4f), Stone);
        Box(_set, new Vector3(1, 0.12f, 2.6f), new Vector3(15.3f, 0.18f, 6.6f), PaleStone);
        for (int row = 0; row < 5; row++)
        {
            for (int column = 0; column < 13; column++)
            {
                Box(_set, new Vector3(-5.7f + column * 1.08f + (row % 2) * 0.45f, 0.225f, 0.1f + row * 1.04f),
                    new Vector3(0.99f, 0.055f, 0.94f), ((column + row) % 4 == 0) ? "71827e" : "657b7c");
            }
        }
        // Rough masonry courses are visible across the seawall rather than a blank block.
        for (int x = 0; x < 16; x++)
        {
            Box(_set, new Vector3(-6 + x * 0.9f, -0.25f, -0.67f), new Vector3(0.82f, 0.5f, 0.18f), x % 3 == 0 ? "7a8982" : "4a6369");
        }
        var pier = new Node3D { Position = new Vector3(5, -0.05f, -3.8f), RotationDegrees = new Vector3(0, -7, 0) };
        _set.AddChild(pier);
        for (int plank = 0; plank < 13; plank++)
        {
            Box(pier, new Vector3(0, 0, -plank * 0.55f), new Vector3(2.5f, 0.16f, 0.48f), plank % 3 == 0 ? WarmWood : Wood);
        }
        for (int i = 0; i < 4; i++)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Cylinder(pier, new Vector3(side * 1.05f, -0.35f, -i * 2), 0.13f, 0.19f, 2, Wood, 7);
                Cylinder(pier, new Vector3(side * 1.05f, 0.69f, -i * 2), 0.18f, 0.18f, 0.12f, Brass, 10);
            }
        }
        BuildBoat(new Vector3(8, -0.14f, -5.2f));
        BuildShed(new Vector3(-4, 0.24f, 1.3f));
        BuildCrates(_set, new Vector3(6.2f, 0.3f, 3.8f));
        for (int i = 0; i < 4; i++)
        {
            Cylinder(_set, new Vector3(-3.6f + i * 3.2f, 0.54f, -0.22f), 0.15f, 0.22f, 0.65f, Ink, 8);
            Cylinder(_set, new Vector3(-3.6f + i * 3.2f, 0.84f, -0.22f), 0.31f, 0.31f, 0.11f, Ink, 8);
        }
        BuildLampPost(_set, new Vector3(-0.7f, 0.25f, -0.1f), 3.4f);
        BuildLampPost(_set, new Vector3(7, 0.25f, 4.9f), 3.2f);
        Rope(_set, new Vector3(-3.6f, 0.62f, -0.22f), new Vector3(-0.4f, 0.62f, -0.22f), 0.035f, 0.35f);
        BuildChartBoard(_set, new Vector3(3, 0.26f, 0));
        BuildRain();
    }

    private void BuildKeeperHouse()
    {
        SetCamera(new Vector3(13.5f, 10, 21), new Vector3(0, 1.8f, 0), 11.9f);
        RoomFloor(12, 8.5f);
        Box(_set, new Vector3(0, 2.75f, -4.3f), new Vector3(12.4f, 5.5f, 0.3f), "536666");
        Box(_set, new Vector3(-6.1f, 2.75f, 0), new Vector3(0.3f, 5.5f, 8.5f), "405457");
        Box(_set, new Vector3(0, 0.34f, -4.06f), new Vector3(12, 0.54f, 0.14f), Wood);
        for (int i = 0; i < 4; i++)
        {
            Box(_set, new Vector3(-5.8f + i * 3.8f, 2.75f, -4.07f), new Vector3(0.2f, 5.5f, 0.25f), Wood);
        }
        Box(_set, new Vector3(0, 5.4f, -4), new Vector3(12.2f, 0.3f, 0.35f), Wood);
        BuildWindow(_set, new Vector3(2.6f, 3.05f, -4.02f), new Vector2(2.7f, 2.8f));
        // Gently glowing hearth, stone chimney, hanging copper kettle.
        Box(_set, new Vector3(-3.3f, 1.45f, -3.25f), new Vector3(3.3f, 2.9f, 1.5f), Stone);
        Box(_set, new Vector3(-3.3f, 4, -3.7f), new Vector3(2.1f, 2.5f, 0.8f), Stone);
        Box(_set, new Vector3(-3.3f, 0.95f, -2.46f), new Vector3(2.3f, 1.85f, 0.1f), Ink);
        Box(_set, new Vector3(-3.3f, 2.9f, -3.13f), new Vector3(3.65f, 0.3f, 1.9f), WarmWood);
        Box(_set, new Vector3(-3.3f, 0.16f, -2.7f), new Vector3(3.8f, 0.18f, 2.2f), Stone);
        for (int i = 0; i < 5; i++)
        {
            Cylinder(_set, new Vector3(-3.9f + i * 0.28f, 0.4f, -2.45f), 0.13f, 0.18f, 0.9f, Wood, 7,
                new Vector3(75, 0, 25 + i * 8));
            Sphere(_set, new Vector3(-3.8f + i * 0.27f, 0.72f + (i % 2) * 0.12f, -2.5f),
                new Vector3(0.22f, 0.48f, 0.18f), i % 2 == 0 ? Amber : Coral, true);
        }
        AddWarmLight(_set, new Vector3(-3.3f, 1.15f, -1.8f), 3.1f, 7.5f);
        BuildTable(_set, new Vector3(1.2f, 0, -0.55f), new Vector2(4.2f, 2.3f));
        Box(_set, new Vector3(1.1f, 1.7f, -0.55f), new Vector3(2.6f, 0.025f, 1.6f), Paper);
        for (int i = 0; i < 4; i++)
        {
            Box(_set, new Vector3(0.2f + i * 0.5f, 1.72f, -0.6f), new Vector3(0.022f, 0.009f, 1.15f), "958870", rotation: new Vector3(0, i * 11, 0));
        }
        Lantern(_set, new Vector3(2.4f, 1.73f, -0.85f), 0.85f);
        Cylinder(_set, new Vector3(-0.1f, 1.79f, 0), 0.17f, 0.13f, 0.23f, Paper, 12);
        BuildChair(_set, new Vector3(0, 0, 1.6f), 0);
        BuildChair(_set, new Vector3(3.8f, 0, -0.6f), -90);
        // The bookshelf and wet raincoat suggest an inhabited keeper's home.
        BuildBookshelf(_set, new Vector3(-5.62f, 0, 1.15f), true, 2.6f, 3.5f);
        Box(_set, new Vector3(5.1f, 1.55f, -3.8f), new Vector3(1.35f, 3.1f, 0.21f), Wood);
        Box(_set, new Vector3(5.1f, 1.55f, -3.66f), new Vector3(1.06f, 2.83f, 0.08f), Midnight);
        Sphere(_set, new Vector3(5.49f, 1.6f, -3.51f), new Vector3(0.09f, 0.09f, 0.09f), Brass);
        Box(_set, new Vector3(3.85f, 2.7f, -3.9f), new Vector3(1.25f, 0.12f, 0.14f), WarmWood);
        Cylinder(_set, new Vector3(3.95f, 1.85f, -3.5f), 0.29f, 0.47f, 1.55f, Coral, 6);
        Cylinder(_set, new Vector3(4.15f, 2.65f, -3.5f), 0.12f, 0.27f, 0.25f, Coral, 8);
        Box(_set, new Vector3(0.65f, 0.13f, 2.05f), new Vector3(5.3f, 0.06f, 2.7f), "526f70");
        for (int i = 0; i < 6; i++)
        {
            Box(_set, new Vector3(-1.4f + i * 0.82f, 0.169f, 2.05f), new Vector3(0.045f, 0.009f, 2.4f), "b19b76");
        }
        WallPicture(_set, new Vector3(-0.25f, 3.55f, -4), new Vector2(1.35f, 1.75f));
        Cylinder(_set, new Vector3(0.15f, 1.86f, -1.3f), 0.15f, 0.13f, 0.33f, "527998", 12);
        for (int pencil = 0; pencil < 4; pencil++)
            Cylinder(_set, new Vector3(0.07f + pencil * 0.05f, 2.12f, -1.3f), 0.014f, 0.014f, 0.45f, Brass, 5, new Vector3(0, 0, -8 + pencil * 5));
        Box(_set, new Vector3(1.2f, 1.36f, 0.62f), new Vector3(1.1f, 0.15f, 0.06f), Brass);
    }

    private void BuildArchive()
    {
        SetCamera(new Vector3(13.8f, 11.6f, 23), new Vector3(0, 2.1f, -0.5f), 13.8f);
        RoomFloor(14, 10);
        Box(_set, new Vector3(0, 3.3f, -5), new Vector3(14.3f, 6.6f, 0.3f), Midnight);
        Box(_set, new Vector3(-7, 3.3f, -0.5f), new Vector3(0.28f, 6.6f, 9.3f), "34454d");
        for (int i = 0; i < 3; i++)
        {
            BuildBookshelf(_set, new Vector3(-4.6f + i * 4.3f, 0.08f, -4.55f), false, 3.75f, 5.6f);
        }
        BuildBookshelf(_set, new Vector3(-6.55f, 0.08f, -0.2f), true, 3.9f, 5.6f);
        // Brass rail, sliding ladder and stacks break the rhythm of the shelves.
        Segment(_set, new Vector3(-6, 5.4f, -3.62f), new Vector3(6.3f, 5.4f, -3.62f), 0.045f, Brass);
        var ladder = new Node3D { Position = new Vector3(-2.2f, 0, -2.85f), RotationDegrees = new Vector3(-8, 0, 0) };
        _set.AddChild(ladder);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(ladder, new Vector3(side * 0.48f, 2.6f, 0), new Vector3(0.1f, 5.2f, 0.14f), WarmWood);
        }
        for (int step = 0; step < 10; step++)
        {
            Box(ladder, new Vector3(0, 0.3f + step * 0.49f, 0), new Vector3(1.05f, 0.11f, 0.15f), WarmWood);
        }
        BuildTable(_set, new Vector3(1, 0, -0.4f), new Vector2(5.3f, 2.8f));
        Box(_set, new Vector3(0.3f, 1.72f, -0.1f), new Vector3(2.1f, 0.08f, 1.2f), Paper, rotation: new Vector3(0, 12, 0));
        Box(_set, new Vector3(0.3f, 1.77f, -0.1f), new Vector3(0.035f, 0.02f, 1.12f), Wood, rotation: new Vector3(0, 12, 0));
        for (int page = 0; page < 5; page++)
        {
            Box(_set, new Vector3(0.2f, 1.78f, -0.47f + page * 0.16f), new Vector3(1.6f, 0.008f, 0.013f), "8d8f80", rotation: new Vector3(0, 12, 0));
        }
        BuildBookStack(_set, new Vector3(2.5f, 1.7f, -1.05f), 5);
        Lantern(_set, new Vector3(-0.95f, 1.7f, -1.02f), 1.05f);
        BuildChair(_set, new Vector3(0.6f, 0, 1.6f), 0);
        BuildChair(_set, new Vector3(4.1f, 0, -0.65f), -90);
        BuildBookStack(_set, new Vector3(5.6f, 0.12f, 2.5f), 7);
        BuildBookStack(_set, new Vector3(4.7f, 0.12f, 2.6f), 4);
        Box(_set, new Vector3(-3.85f, 0.28f, 2.2f), new Vector3(2.2f, 0.38f, 1.8f), Wood);
        for (int roll = 0; roll < 7; roll++)
        {
            Cylinder(_set, new Vector3(-4.5f + roll * 0.24f, 0.75f, 2.2f), 0.13f, 0.13f, 1, Paper, 9, new Vector3(0, 0, -7 + roll * 3));
        }
        AddWarmLight(_set, new Vector3(2, 4.4f, 1), 2.6f, 11);
        BuildCompassRose(_set, new Vector3(1.4f, 0.132f, 2.45f), 1.45f);
        BuildTapeRecorder(_set, new Vector3(2.5f, 1.73f, -0.35f));
    }

    private void BuildLanternRoom()
    {
        SetCamera(new Vector3(12, 9.4f, 22), new Vector3(0, 2.05f, 0), 12.9f);
        BuildSea(new Vector3(0, -5, -4), new Vector2(140, 130));
        BuildDistantIslands();
        Cylinder(_set, new Vector3(0, -0.18f, 0), 6.6f, 6.6f, 0.45f, Ink, 12);
        Cylinder(_set, new Vector3(0, 0.08f, 0), 6.35f, 6.35f, 0.09f, "536c6d", 12);
        for (int spoke = 0; spoke < 12; spoke++)
        {
            float angle = spoke * Mathf.Tau / 12;
            Segment(_set, new Vector3(0, 0.16f, 0), new Vector3(Mathf.Cos(angle) * 6.2f, 0.16f, Mathf.Sin(angle) * 6.2f), 0.023f, Brass);
        }
        // Rear-only glazing leaves a deliberate theatrical cutaway toward the player.
        for (int i = 0; i < 9; i++)
        {
            float angle = Mathf.DegToRad(145 + i * 27);
            Vector3 edge = new(Mathf.Cos(angle) * 5.9f, 0, Mathf.Sin(angle) * 5.9f);
            if (edge.Z > 1.8f) continue;
            Cylinder(_set, edge + Vector3.Up * 2.6f, 0.085f, 0.1f, 5.2f, Ink, 8);
            Vector3 next = new(Mathf.Cos(angle + Mathf.DegToRad(27)) * 5.9f, 0, Mathf.Sin(angle + Mathf.DegToRad(27)) * 5.9f);
            Segment(_set, edge + Vector3.Up * 1.05f, next + Vector3.Up * 1.05f, 0.065f, Ink);
            Segment(_set, edge + Vector3.Up * 5.1f, next + Vector3.Up * 5.1f, 0.085f, Ink);
        }
        // Fresnel rings remain opaque luminous glass so they work in compatibility rendering.
        Cylinder(_set, new Vector3(-0.9f, 0.75f, -0.65f), 1.5f, 1.65f, 1.2f, Ink, 12);
        Cylinder(_set, new Vector3(-0.9f, 1.42f, -0.65f), 1.8f, 1.8f, 0.2f, Brass, 16);
        Sphere(_set, new Vector3(-0.9f, 3.25f, -0.65f), new Vector3(1.13f, 1.45f, 1.13f), "8bada0");
        for (int ring = 0; ring < 9; ring++)
        {
            float y = 2.1f + ring * 0.285f;
            float radius = 0.64f + Mathf.Sin((ring + 0.8f) / 9.6f * Mathf.Pi) * 0.64f;
            Torus(_set, new Vector3(-0.9f, y, -0.65f), radius, 0.07f, ring % 2 == 0 ? Amber : "bfbc89", true);
        }
        for (int i = 0; i < 4; i++)
        {
            float angle = Mathf.Pi * 0.25f + i * Mathf.Pi * 0.5f;
            Cylinder(_set, new Vector3(-0.9f + Mathf.Cos(angle) * 1.48f, 3.08f, -0.65f + Mathf.Sin(angle) * 1.48f),
                0.065f, 0.065f, 3.1f, Brass, 8);
        }
        Cylinder(_set, new Vector3(-0.9f, 4.76f, -0.65f), 1.72f, 1.72f, 0.18f, Brass, 16);
        Cylinder(_set, new Vector3(-0.9f, 5.01f, -0.65f), 0.5f, 1.72f, 0.42f, Ink, 12);
        AddWarmLight(_set, new Vector3(-0.9f, 3.3f, -0.65f), 3.4f, 11.5f);
        _beacon = new Node3D { Position = new Vector3(-0.9f, 3.3f, -0.65f) };
        _set.AddChild(_beacon);
        Beam(_beacon, 28, 4.5f);
        BuildCrates(_set, new Vector3(3.7f, 0.15f, -2.5f));
        Torus(_set, new Vector3(3.45f, 1.48f, -2.6f), 0.5f, 0.09f, Brass, false, new Vector3(90, 0, 0));
        Segment(_set, new Vector3(3.45f, 0.3f, -2.6f), new Vector3(3.45f, 1.48f, -2.6f), 0.08f, Ink);
        for (int i = 0; i < 4; i++)
        {
            Box(_set, new Vector3(4.3f, -0.12f - i * 0.17f, 2.8f + i * 0.24f), new Vector3(1.8f, 0.12f, 0.3f), Ink);
        }
        BuildCompassRose(_set, new Vector3(-2.65f, 0.145f, 3.1f), 1.25f);
        BuildCounterweight(_set, new Vector3(2.7f, 0.15f, -0.7f));
    }

    private void BuildTideCave()
    {
        SetCamera(new Vector3(12, 8, 21), new Vector3(0, 1.65f, -0.5f), 12.5f);
        BuildSea(new Vector3(0, -0.38f, -7), new Vector2(60, 60));
        Box(_set, new Vector3(0, -0.31f, 1), new Vector3(15, 0.55f, 9.5f), "344e55");
        Rock(_set, new Vector3(-6.8f, 2.3f, 0), new Vector3(3.1f, 4.2f, 6), Midnight, 21);
        Rock(_set, new Vector3(6.8f, 2.6f, -1), new Vector3(3.7f, 4.6f, 5.1f), Midnight, 13);
        for (int i = 0; i < 8; i++)
        {
            float x = -6.5f + i * 1.9f;
            Rock(_set, new Vector3(x, 5.8f - Mathf.Abs(x) * 0.12f, -4.2f), new Vector3(2.7f, 2.1f, 3), "233d47", 10 + i);
        }
        for (int i = 0; i < 10; i++)
        {
            float x = -6.6f + (i % 2) * 12.5f + (i % 3) * 0.25f;
            Rock(_set, new Vector3(x, 1.1f + (i % 4) * 0.7f, -4 + (i / 2) * 1.55f),
                new Vector3(1.4f, 2.2f, 1.7f), i % 3 == 0 ? Teal : "2e4852", i + 2);
        }
        for (int i = 0; i < 11; i++)
        {
            Rock(_set, new Vector3(-4.8f + i * 0.93f, 0.12f, 1.7f + Mathf.Sin(i * 2.3f) * 1.5f),
                new Vector3(0.6f + i % 3 * 0.22f, 0.28f, 0.6f), Stone, i + 35);
        }
        // The sea-pressure installation remains visibly separate from the dry walking shelf.
        BuildPressureWorks(_set, new Vector3(-2.65f, 0.12f, -1.8f));
        for (int i = 0; i < 8; i++)
        {
            Sphere(_set, new Vector3(-5.6f + (i % 3) * 0.34f, 0.9f + i * 0.3f, -1.8f),
                new Vector3(0.12f, 0.3f, 0.16f), "7db5a6", true);
        }
        Lantern(_set, new Vector3(2.5f, 0.1f, 1.2f), 1.15f);
        AddWarmLight(_set, new Vector3(2.5f, 1.5f, 1.3f), 2.1f, 8);
        AddCoolLight(_set, new Vector3(-3.7f, 2.4f, -1), 2.2f, 7);
        AddCoolLight(_set, new Vector3(0, 3.1f, -7), 2.6f, 13);
        // Wet pools are small insets in the exposed cave shelf.
        BuildSea(new Vector3(2.8f, -0.012f, -1.8f), new Vector2(5.3f, 3.2f));
        BuildSea(new Vector3(-4, -0.01f, 3.6f), new Vector2(2.6f, 1.8f));
        for (int i = 0; i < 9; i++)
        {
            Cylinder(_set, new Vector3(-5.1f + i * 1.18f, 5.2f - (i % 2) * 0.2f, -3.6f),
                0.28f, 0.02f, 1 + (i % 3) * 0.38f, "35505a", 5);
        }
    }

    private void BuildLighthouse(Node3D parent, Vector3 origin, float scale)
    {
        var tower = new Node3D { Position = origin, Scale = Vector3.One * scale };
        parent.AddChild(tower);
        Cylinder(tower, new Vector3(0, 0.2f, 0), 1.95f, 2.25f, 0.45f, Stone, 12);
        Cylinder(tower, new Vector3(0, 4.15f, 0), 1.18f, 1.7f, 7.9f, PaleStone, 12);
        Cylinder(tower, new Vector3(0, 4.35f, 0), 1.44f, 1.46f, 0.82f, Coral, 12);
        Box(tower, new Vector3(0, 1.05f, 1.64f), new Vector3(0.72f, 1.62f, 0.14f), Ink);
        Box(tower, new Vector3(0, 1.05f, 1.73f), new Vector3(0.51f, 1.38f, 0.05f), Wood);
        for (int i = 0; i < 3; i++)
        {
            float y = 3.1f + i * 1.75f;
            Box(tower, new Vector3(0.48f, y, 1.4f - i * 0.12f), new Vector3(0.33f, 0.68f, 0.12f), Ink);
            Box(tower, new Vector3(0.48f, y, 1.48f - i * 0.12f), new Vector3(0.21f, 0.54f, 0.05f), Amber, true);
        }
        Cylinder(tower, new Vector3(0, 8.17f, 0), 1.68f, 1.48f, 0.38f, Ink, 12);
        Cylinder(tower, new Vector3(0, 8.48f, 0), 1.83f, 1.83f, 0.13f, Brass, 16);
        Cylinder(tower, new Vector3(0, 9.17f, 0), 1.03f, 1.03f, 1.4f, "9cab92", 12, emissive: true);
        Sphere(tower, new Vector3(0, 9.17f, 0), new Vector3(0.67f, 0.64f, 0.67f), Amber, true);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.Tau / 8;
            Cylinder(tower, new Vector3(Mathf.Cos(a) * 1.11f, 9.18f, Mathf.Sin(a) * 1.11f), 0.065f, 0.065f, 1.5f, Ink, 6);
            Cylinder(tower, new Vector3(Mathf.Cos(a) * 1.78f, 8.85f, Mathf.Sin(a) * 1.78f), 0.035f, 0.035f, 0.7f, Ink, 6);
        }
        Torus(tower, new Vector3(0, 9.2f, 0), 1.78f, 0.04f, Ink);
        Cylinder(tower, new Vector3(0, 10.05f, 0), 0.05f, 1.42f, 0.95f, Ink, 8);
        Cylinder(tower, new Vector3(0, 10.75f, 0), 0.015f, 0.06f, 0.65f, Brass, 6);
        AddWarmLight(tower, new Vector3(0, 9.15f, 0), 2.4f, 9);
        _beacon = new Node3D { Position = new Vector3(0, 9.15f, 0) };
        tower.AddChild(_beacon);
        Beam(_beacon, 26, 3.9f);
    }

    private void BuildSea(Vector3 origin, Vector2 size)
    {
        var shader = new Shader
        {
            Code = """
                shader_type spatial;
                render_mode unshaded, cull_disabled;
                uniform float elapsed = 0.0;
                varying vec3 point;
                void vertex() {
                    VERTEX.y += sin(VERTEX.x * 0.72 + elapsed * 0.65) * 0.035
                        + sin(VERTEX.z * 1.1 + elapsed * 0.48) * 0.035;
                    point = VERTEX;
                }
                void fragment() {
                    float broad = sin(point.x * 0.24 + point.z * 0.3 + elapsed * 0.17) * 0.5 + 0.5;
                    float ripple = sin(point.z * 4.6 + sin(point.x * 0.8 + elapsed * 0.31) * 1.5 + elapsed * 0.55);
                    float split = sin(point.x * 2.7 - point.z * 0.37 + elapsed * 0.25);
                    float foam = smoothstep(0.965, 1.0, ripple) * smoothstep(0.2, 0.65, split);
                    vec3 deep = mix(vec3(0.075, 0.17, 0.215), vec3(0.15, 0.30, 0.32), broad);
                    ALBEDO = mix(deep, vec3(0.38, 0.54, 0.53), foam * 0.55);
                }
                """,
        };
        var material = new ShaderMaterial { Shader = shader };
        _waterMaterials.Add(material);
        var mesh = new PlaneMesh { Size = size, SubdivideWidth = 45, SubdivideDepth = 45 };
        _set.AddChild(new MeshInstance3D { Name = "TidalWater", Mesh = mesh, MaterialOverride = material, Position = origin,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private void BuildDistantIslands()
    {
        for (int i = 0; i < 8; i++)
        {
            Rock(_set, new Vector3(-34 + i * 10, -1.6f, -28 - (i % 3) * 6),
                new Vector3(6.5f + i % 3, 2.5f + i % 2, 3.8f), "283f4a", 42 + i);
        }
        Sphere(_set, new Vector3(-15, 14, -38), new Vector3(1.2f, 1.2f, 0.4f), "9baa9d", true);
        for (int i = 0; i < 8; i++)
        {
            Sphere(_set, new Vector3(-35 + i * 10, 14 + (i % 3) * 1.8f, -42), new Vector3(9.5f, 1.7f, 2.2f), "263b4b");
        }
    }

    private void BuildBoat(Vector3 origin)
    {
        var boat = new Node3D { Name = "WeatheredFishingBoat", Position = origin, RotationDegrees = new Vector3(0, -8, 0) };
        _set.AddChild(boat);
        // An eight-sided hull with a pointed bow, rather than a scaled primitive.
        Vector3[] bottom =
        {
            new(-0.55f, -0.25f, -1.7f), new(0.55f, -0.25f, -1.7f), new(0.75f, -0.25f, 0.9f),
            new(0, -0.25f, 2.2f), new(-0.75f, -0.25f, 0.9f),
        };
        Vector3[] top =
        {
            new(-0.92f, 0.45f, -1.95f), new(0.92f, 0.45f, -1.95f), new(1.05f, 0.45f, 0.85f),
            new(0, 0.45f, 2.65f), new(-1.05f, 0.45f, 0.85f),
        };
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < top.Length; i++)
        {
            int next = (i + 1) % top.Length;
            AddTriangle(surface, bottom[i], top[i], top[next]);
            AddTriangle(surface, bottom[i], top[next], bottom[next]);
            AddTriangle(surface, top[i], new Vector3(0, 0.44f, 0), top[next]);
            Segment(boat, top[i] + Vector3.Up * 0.035f, top[next] + Vector3.Up * 0.035f, 0.065f, Paper);
        }
        boat.AddChild(new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = Material(Coral) });
        Box(boat, new Vector3(0, 0.51f, -0.25f), new Vector3(1.65f, 0.08f, 2.6f), Wood);
        Box(boat, new Vector3(0, 1.23f, -0.88f), new Vector3(1.32f, 1.4f, 1.2f), Paper);
        Box(boat, new Vector3(0, 1.42f, -0.25f), new Vector3(1.09f, 0.7f, 0.055f), Midnight);
        Box(boat, new Vector3(0.69f, 1.42f, -0.87f), new Vector3(0.035f, 0.7f, 0.85f), Midnight);
        Box(boat, new Vector3(0, 1.95f, -0.89f), new Vector3(1.65f, 0.12f, 1.6f), Teal);
        Cylinder(boat, new Vector3(0, 2.4f, -0.8f), 0.04f, 0.065f, 0.9f, Ink, 6);
        Segment(boat, new Vector3(-0.6f, 2.75f, -0.8f), new Vector3(0.6f, 2.75f, -0.8f), 0.035f, Ink);
        Torus(boat, new Vector3(0.71f, 1.08f, -0.82f), 0.33f, 0.09f, Coral, false, new Vector3(0, 0, 90));
        _floating.Add((boat, origin, 0.8f, 0.09f));
        Lantern(boat, new Vector3(0, 2.05f, -0.9f), 0.45f);
    }

    private void BuildShed(Vector3 origin)
    {
        var shed = new Node3D { Position = origin };
        _set.AddChild(shed);
        Box(shed, new Vector3(0, 1.3f, 0), new Vector3(3.3f, 2.6f, 2.7f), "53605c");
        for (int i = 0; i < 13; i++)
        {
            Box(shed, new Vector3(-1.5f + i * 0.25f, 1.3f, 1.39f), new Vector3(0.03f, 2.5f, 0.035f), "35494c");
        }
        Box(shed, new Vector3(0.68f, 1.15f, 1.45f), new Vector3(0.93f, 2.2f, 0.1f), Wood);
        Box(shed, new Vector3(-0.83f, 1.68f, 1.47f), new Vector3(0.98f, 0.9f, 0.09f), Ink);
        Box(shed, new Vector3(-0.83f, 1.68f, 1.53f), new Vector3(0.78f, 0.7f, 0.06f), Amber, true);
        Box(shed, new Vector3(-0.83f, 1.68f, 1.58f), new Vector3(0.06f, 0.78f, 0.06f), Wood);
        Box(shed, new Vector3(-0.83f, 1.68f, 1.58f), new Vector3(0.85f, 0.06f, 0.06f), Wood);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(shed, new Vector3(side * 0.95f, 2.9f, 0), new Vector3(2.12f, 0.16f, 3.15f), Midnight,
                rotation: new Vector3(0, 0, side * -23));
        }
        Cylinder(shed, new Vector3(0.85f, 3.35f, -0.65f), 0.18f, 0.18f, 1.05f, Ink, 8);
        Torus(shed, new Vector3(-1.1f, 0.93f, 1.68f), 0.47f, 0.11f, Coral, false, new Vector3(90, 0, 0));
        AddWarmLight(shed, new Vector3(-0.8f, 1.6f, 2), 1.2f, 4);
    }

    private void BuildCast(string[] characterIds)
    {
        characterIds = Array.FindAll(characterIds, id => id != "ivo");
        int count = characterIds.Length;
        for (int i = 0; i < count; i++)
        {
            float spacing = count <= 2 ? 3.1f : count <= 3 ? 2.65f : 2.1f;
            float x = 1.3f + (i - (count - 1) * 0.5f) * spacing;
            float z = _location switch { "harbor" => 3.45f, "tide_cave" => 2.5f, "lantern_room" => 3.05f, _ => 2.6f };
            var actor = CreateActor(characterIds[i]);
            actor.Position = new Vector3(x, _location == "harbor" ? 0.28f : 0.14f, z);
            actor.RotationDegrees = new Vector3(0, 24 - i * 12, 0);
            _cast.AddChild(actor);
        }
    }

    private Node3D CreateActor(string characterId)
    {
        string id = characterId.ToLowerInvariant();
        bool keeper = id == "nessa";
        bool scholar = id == "sera";
        bool sailor = id == "tomas";
        string coat = keeper ? "b77b55" : scholar ? "b49855" : sailor ? "756150" : "39434b";
        string sleeves = sailor ? "3e6264" : coat;
        string skin = keeper ? "d3b39c" : scholar ? "795643" : sailor ? "a79673" : "a67b5f";
        string hair = keeper ? "92958d" : "283039";
        var actor = new Node3D { Name = $"Actor_{characterId}" };
        // Adult proportions: roughly seven heads tall, tailored shoulders and a long coat.
        Cylinder(actor, new Vector3(-0.19f, 0.58f, 0), 0.145f, 0.13f, 0.91f, Ink, 8, new Vector3(-3, 0, -2));
        Cylinder(actor, new Vector3(0.19f, 0.59f, -0.045f), 0.145f, 0.13f, 0.93f, Ink, 8, new Vector3(3, 0, 2));
        Sphere(actor, new Vector3(-0.19f, 0.13f, 0.12f), new Vector3(0.19f, 0.13f, 0.32f), Ink);
        Sphere(actor, new Vector3(0.19f, 0.13f, 0.075f), new Vector3(0.19f, 0.13f, 0.32f), Ink);
        Cylinder(actor, new Vector3(0, sailor ? 1.4f : 1.2f, 0), 0.35f, sailor ? 0.36f : 0.52f, sailor ? 0.52f : 0.93f, coat, 7);
        var torso = Cylinder(actor, new Vector3(0, 1.87f, 0), 0.43f, 0.34f, 0.73f, coat, 8);
        torso.Scale = new Vector3(1, 1, 0.7f);
        Sphere(actor, new Vector3(-0.38f, 2.11f, 0), new Vector3(0.21f, 0.21f, 0.22f), sleeves);
        Sphere(actor, new Vector3(0.38f, 2.11f, 0), new Vector3(0.21f, 0.21f, 0.22f), sleeves);
        // Arms have an elbow and wrist, with visible hands rather than box limbs.
        Segment(actor, new Vector3(-0.43f, 2.09f, 0), new Vector3(-0.56f, 1.64f, 0.075f), 0.16f, sleeves);
        Segment(actor, new Vector3(-0.56f, 1.64f, 0.075f), new Vector3(-0.42f, 1.36f, 0.34f), 0.135f, sleeves);
        Sphere(actor, new Vector3(-0.4f, 1.3f, 0.36f), new Vector3(0.105f, 0.15f, 0.085f), skin);
        Segment(actor, new Vector3(0.43f, 2.09f, 0), new Vector3(0.55f, 1.65f, 0), 0.16f, sleeves);
        Segment(actor, new Vector3(0.55f, 1.65f, 0), new Vector3(0.55f, 1.26f, 0.11f), 0.135f, sleeves);
        Sphere(actor, new Vector3(0.55f, 1.18f, 0.11f), new Vector3(0.105f, 0.15f, 0.085f), skin);
        Cylinder(actor, new Vector3(0, 2.31f, 0), 0.14f, 0.14f, 0.23f, skin, 9);
        Sphere(actor, new Vector3(0, 2.62f, 0.02f), new Vector3(0.245f, 0.32f, 0.23f), skin);
        Sphere(actor, new Vector3(-0.234f, 2.6f, 0), new Vector3(0.052f, 0.078f, 0.048f), skin);
        Sphere(actor, new Vector3(0.234f, 2.6f, 0), new Vector3(0.052f, 0.078f, 0.048f), skin);
        Sphere(actor, new Vector3(0, 2.81f, -0.04f), new Vector3(0.255f, 0.17f, 0.235f), hair);
        Sphere(actor, new Vector3(-0.15f, 2.7f, 0.145f), new Vector3(0.13f, 0.16f, 0.1f), hair);
        Sphere(actor, new Vector3(0, 2.6f, 0.236f), new Vector3(0.05f, 0.07f, 0.075f), skin);
        Box(actor, new Vector3(-0.095f, 2.66f, 0.232f), new Vector3(0.068f, 0.021f, 0.012f), Ink);
        Box(actor, new Vector3(0.095f, 2.66f, 0.232f), new Vector3(0.068f, 0.021f, 0.012f), Ink);
        Box(actor, new Vector3(0, 2.48f, 0.219f), new Vector3(0.077f, 0.015f, 0.012f), "806455");
        // Lapels, shirt and fastenings give each silhouette an authored costume.
        Box(actor, new Vector3(0, 2.03f, 0.282f), new Vector3(0.22f, 0.43f, 0.04f), keeper ? "233b50" : scholar ? "345b50" : sailor ? "3e6264" : "735b70");
        Box(actor, new Vector3(-0.16f, 2.06f, 0.29f), new Vector3(0.15f, 0.46f, 0.06f), coat, rotation: new Vector3(0, 0, 18));
        Box(actor, new Vector3(0.16f, 2.06f, 0.29f), new Vector3(0.15f, 0.46f, 0.06f), coat, rotation: new Vector3(0, 0, -18));
        for (int i = 0; i < 4; i++)
        {
            Sphere(actor, new Vector3(0.09f, 1.84f - i * 0.22f, 0.345f), new Vector3(0.032f, 0.032f, 0.022f), Brass);
        }
        if (keeper)
        {
            // Nessa's repaired navy cuffs and iron-gray cropped hair.
            Cylinder(actor, new Vector3(-0.44f, 1.4f, 0.3f), 0.14f, 0.14f, 0.16f, "263c50", 8, new Vector3(35, 0, 0));
            Cylinder(actor, new Vector3(0.55f, 1.31f, 0.095f), 0.14f, 0.14f, 0.16f, "263c50", 8);
        }
        else if (scholar)
        {
            Box(actor, new Vector3(-0.43f, 1.51f, 0.48f), new Vector3(0.41f, 0.59f, 0.11f), "36554b", rotation: new Vector3(0, -12, 9));
        }
        else if (sailor)
        {
            // Tomas wears a repaired headset with one mismatched brown ear pad.
            Vector3 previous = new(-0.3f, 2.62f, 0);
            for (int segment = 1; segment <= 8; segment++)
            {
                float angle = Mathf.Pi - segment * Mathf.Pi / 8;
                Vector3 next = new(Mathf.Cos(angle) * 0.31f, 2.62f + Mathf.Sin(angle) * 0.35f, 0);
                Segment(actor, previous, next, 0.037f, Ink);
                previous = next;
            }
            Sphere(actor, new Vector3(-0.29f, 2.62f, 0), new Vector3(0.09f, 0.13f, 0.12f), Ink);
            Sphere(actor, new Vector3(0.29f, 2.62f, 0), new Vector3(0.09f, 0.13f, 0.12f), WarmWood);
            Sphere(actor, new Vector3(-0.19f, 2.76f, 0), new Vector3(0.07f, 0.06f, 0.17f), "979b8a");
        }
        else
        {
            Segment(actor, new Vector3(-0.29f, 2.12f, 0.31f), new Vector3(0.32f, 1.33f, 0.34f), 0.047f, "6d785a");
            Box(actor, new Vector3(0.36f, 1.2f, 0.12f), new Vector3(0.45f, 0.46f, 0.24f), "6d785a");
            for (int wave = 0; wave < 3; wave++)
                Sphere(actor, new Vector3(-0.16f + wave * 0.12f, 2.85f, 0.09f), new Vector3(0.09f, 0.08f, 0.12f), hair);
        }
        return actor;
    }

    private void BuildChartBoard(Node3D parent, Vector3 position)
    {
        var board = new Node3D { Position = position, RotationDegrees = new Vector3(0, -12, 0) };
        parent.AddChild(board);
        Box(board, new Vector3(0, 1.78f, 0), new Vector3(2.3f, 1.25f, 0.18f), Wood);
        Box(board, new Vector3(0, 1.78f, 0.105f), new Vector3(2.08f, 1.05f, 0.03f), Paper);
        for (int i = -1; i <= 1; i += 2)
            Box(board, new Vector3(i * 0.87f, 0.9f, 0), new Vector3(0.12f, 1.8f, 0.14f), Wood);
        for (int i = 0; i < 4; i++)
            Box(board, new Vector3(-0.7f + i * 0.44f, 1.8f, 0.13f), new Vector3(0.024f, 0.81f, 0.01f), Teal, rotation: new Vector3(0, 0, -18 + i * 14));
    }

    private void BuildTapeRecorder(Node3D parent, Vector3 position)
    {
        var recorder = new Node3D { Position = position, RotationDegrees = new Vector3(-9, -13, 0) };
        parent.AddChild(recorder);
        Box(recorder, new Vector3(0, 0.37f, 0), new Vector3(1.43f, 0.74f, 0.52f), "4a585c");
        Box(recorder, new Vector3(0, 0.36f, 0.28f), new Vector3(1.31f, 0.6f, 0.08f), "aaab94");
        for (int side = -1; side <= 1; side += 2)
        {
            Cylinder(recorder, new Vector3(side * 0.39f, 0.63f, 0.36f), 0.27f, 0.27f, 0.09f, Ink, 16, new Vector3(90, 0, 0));
            Cylinder(recorder, new Vector3(side * 0.39f, 0.63f, 0.42f), 0.095f, 0.095f, 0.05f, Brass, 10, new Vector3(90, 0, 0));
        }
        Segment(recorder, new Vector3(-0.39f, 0.47f, 0.42f), new Vector3(0.39f, 0.47f, 0.42f), 0.022f, Wood);
        for (int i = 0; i < 4; i++)
            Box(recorder, new Vector3(-0.27f + i * 0.18f, 0.2f, 0.34f), new Vector3(0.12f, 0.09f, 0.05f), i == 0 ? Coral : Ink);
    }

    private void BuildPressureWorks(Node3D parent, Vector3 position)
    {
        var works = new Node3D { Position = position };
        parent.AddChild(works);
        Box(works, new Vector3(0, 0.9f, 0), new Vector3(2.25f, 1.8f, 1.2f), "456465");
        for (int side = -1; side <= 1; side += 2)
        {
            Segment(works, new Vector3(side * 0.82f, 0.85f, 0), new Vector3(side * 2.1f, 0.85f, 0), 0.16f, "64877e");
            Segment(works, new Vector3(side * 2.1f, 0.85f, 0), new Vector3(side * 2.1f, -0.25f, -1.7f), 0.16f, "64877e");
            Cylinder(works, new Vector3(side * 0.56f, 1.92f, 0.25f), 0.26f, 0.26f, 0.13f, Brass, 12, new Vector3(90, 0, 0));
            Cylinder(works, new Vector3(side * 0.56f, 1.92f, 0.33f), 0.21f, 0.21f, 0.03f, Paper, 12, new Vector3(90, 0, 0));
            Segment(works, new Vector3(side * 0.56f, 1.92f, 0.36f), new Vector3(side * 0.56f + 0.12f, 2.04f, 0.36f), 0.014f, Ink);
        }
        Cylinder(works, new Vector3(0, 1.17f, 0.64f), 0.44f, 0.44f, 0.1f, Brass, 12, new Vector3(90, 0, 0));
        Cylinder(works, new Vector3(0, 1.17f, 0.71f), 0.34f, 0.34f, 0.06f, "79ad99", 12, new Vector3(90, 0, 0), true);
        Torus(works, new Vector3(1.49f, 1.35f, 0.6f), 0.46f, 0.065f, Coral, false, new Vector3(90, 0, 0));
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Pi / 2;
            Segment(works, new Vector3(1.49f, 1.35f, 0.6f), new Vector3(1.49f + Mathf.Cos(a) * 0.44f, 1.35f + Mathf.Sin(a) * 0.44f, 0.6f), 0.035f, Coral);
        }
    }

    private void BuildCounterweight(Node3D parent, Vector3 position)
    {
        var counterweight = new Node3D { Position = position };
        parent.AddChild(counterweight);
        Cylinder(counterweight, new Vector3(0, 0.04f, 0), 1.34f, 1.34f, 0.13f, Ink, 12);
        Torus(counterweight, new Vector3(0, 0.13f, 0), 1.34f, 0.065f, Coral);
        Segment(counterweight, new Vector3(0, 3.7f, 0), new Vector3(0, 6.6f, 0), 0.065f, Ink);
        _bellBody = new Node3D { Name = "CounterweightBell" }; counterweight.AddChild(_bellBody);
        Cylinder(_bellBody, new Vector3(0, 3.04f, 0), 0.52f, 0.94f, 1.4f, "aa8b55", 16);
        Cylinder(_bellBody, new Vector3(0, 2.31f, 0), 1.04f, 1.04f, 0.18f, Brass, 16);
        for (int side = -1; side <= 1; side += 2)
            Cylinder(counterweight, new Vector3(side * 1.34f, 0.66f, 0.9f), 0.045f, 0.065f, 1.15f, Ink, 7);
        Segment(counterweight, new Vector3(-1.34f, 1.14f, 0.9f), new Vector3(1.34f, 1.14f, 0.9f), 0.05f, Coral);
        // The chronograph's visible paper bridges two brass rollers.
        Box(counterweight, new Vector3(1.64f, 0.8f, -0.8f), new Vector3(1.25f, 1.4f, 0.8f), Wood);
        Box(counterweight, new Vector3(1.64f, 1.53f, -0.8f), new Vector3(1.15f, 0.04f, 0.7f), Paper);
        for (int side = -1; side <= 1; side += 2)
            Cylinder(counterweight, new Vector3(1.64f + side * 0.51f, 1.63f, -0.8f), 0.13f, 0.13f, 0.87f, Brass, 10, new Vector3(90, 0, 0));
        for (int i = 0; i < 10; i++)
        {
            float angle = -Mathf.Pi + i * 0.34f;
            Box(counterweight, new Vector3(1.65f + Mathf.Cos(angle) * 1.1f, 0.18f + i * 0.23f, -2.1f + Mathf.Sin(angle) * 1.1f),
                new Vector3(1.15f, 0.11f, 0.33f), Ink, rotation: new Vector3(0, -Mathf.RadToDeg(angle), 0));
        }
    }

    private void RoomFloor(float width, float depth)
    {
        Box(_set, new Vector3(0, -0.18f, 0), new Vector3(width + 0.3f, 0.35f, depth + 0.3f), Ink);
        int planks = (int)(width / 0.44f);
        for (int i = 0; i < planks; i++)
        {
            Box(_set, new Vector3(-width * 0.5f + 0.22f + i * 0.44f, 0.035f, 0),
                new Vector3(0.415f, 0.12f, depth), i % 5 == 0 ? "746454" : i % 3 == 0 ? "625d51" : "696151");
            for (int seam = 0; seam < 3; seam++)
            {
                Box(_set, new Vector3(-width * 0.5f + 0.22f + i * 0.44f, 0.097f, -depth * 0.45f + seam * 2.8f + (i % 3) * 0.7f),
                    new Vector3(0.415f, 0.003f, 0.024f), "393d37");
            }
        }
    }

    private void BuildWindow(Node3D parent, Vector3 center, Vector2 size)
    {
        Box(parent, center, new Vector3(size.X + 0.25f, size.Y + 0.25f, 0.16f), Wood);
        Box(parent, center + Vector3.Back * 0.105f, new Vector3(size.X, size.Y, 0.07f), "48666e", true);
        Box(parent, center + Vector3.Back * 0.18f, new Vector3(0.11f, size.Y, 0.08f), Wood);
        Box(parent, center + Vector3.Back * 0.18f, new Vector3(size.X, 0.1f, 0.08f), Wood);
        Box(parent, center + new Vector3(0, -size.Y * 0.5f, 0.22f), new Vector3(size.X + 0.5f, 0.15f, 0.45f), WarmWood);
        for (int i = 0; i < 11; i++)
        {
            Box(parent, center + new Vector3(-size.X * 0.43f + i * size.X * 0.079f, ((i * 7) % 5 - 2) * 0.32f, 0.2f),
                new Vector3(0.012f, 0.27f + i % 3 * 0.17f, 0.014f), "9cb6b5", true, new Vector3(0, 0, -12));
        }
        AddCoolLight(parent, center + Vector3.Back * 0.8f, 1.9f, 7);
    }

    private void BuildTable(Node3D parent, Vector3 origin, Vector2 size)
    {
        Box(parent, origin + Vector3.Up * 1.58f, new Vector3(size.X, 0.22f, size.Y), WarmWood);
        Box(parent, origin + Vector3.Up * 1.38f, new Vector3(size.X - 0.3f, 0.24f, size.Y - 0.3f), Wood);
        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                Box(parent, origin + new Vector3(x * (size.X * 0.5f - 0.28f), 0.72f, z * (size.Y * 0.5f - 0.25f)),
                    new Vector3(0.18f, 1.45f, 0.18f), Wood);
            }
        }
    }

    private void BuildChair(Node3D parent, Vector3 origin, float turn)
    {
        var chair = new Node3D { Position = origin, RotationDegrees = new Vector3(0, turn, 0) };
        parent.AddChild(chair);
        Box(chair, new Vector3(0, 0.77f, 0), new Vector3(0.97f, 0.14f, 0.91f), WarmWood);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(chair, new Vector3(side * 0.37f, 0.88f, 0.34f), new Vector3(0.12f, 1.76f, 0.12f), Wood);
            Box(chair, new Vector3(side * 0.37f, 0.38f, -0.34f), new Vector3(0.12f, 0.76f, 0.12f), Wood);
        }
        Box(chair, new Vector3(0, 1.54f, 0.34f), new Vector3(0.89f, 0.32f, 0.12f), WarmWood);
        Box(chair, new Vector3(0, 1.15f, 0.34f), new Vector3(0.89f, 0.13f, 0.12f), Wood);
    }

    private void BuildBookshelf(Node3D parent, Vector3 origin, bool sideways, float width, float height)
    {
        var shelf = new Node3D { Position = origin, RotationDegrees = new Vector3(0, sideways ? 90 : 0, 0) };
        parent.AddChild(shelf);
        Box(shelf, new Vector3(0, height * 0.5f, 0), new Vector3(width, height, 0.18f), Ink);
        Box(shelf, new Vector3(-width * 0.5f, height * 0.5f, 0.28f), new Vector3(0.17f, height, 0.72f), Wood);
        Box(shelf, new Vector3(width * 0.5f, height * 0.5f, 0.28f), new Vector3(0.17f, height, 0.72f), Wood);
        string[] colors = { "926657", "5c7678", "ac9870", "797b66", "485b67", "836c63" };
        int rows = height > 4 ? 6 : 4;
        for (int row = 0; row <= rows; row++)
        {
            float y = row * height / rows;
            Box(shelf, new Vector3(0, y, 0.3f), new Vector3(width + 0.22f, 0.13f, 0.8f), WarmWood);
            if (row == rows) continue;
            int books = (int)(width / 0.23f) - 2;
            for (int i = 0; i < books; i++)
            {
                float bookHeight = height / rows * (0.58f + ((i * 5 + row * 7) % 8) * 0.038f);
                float x = -width * 0.5f + 0.3f + i * 0.23f;
                string color = colors[(i + row * 3) % colors.Length];
                Box(shelf, new Vector3(x, y + 0.09f + bookHeight * 0.5f, 0.37f), new Vector3(0.17f, bookHeight, 0.47f), color);
                Box(shelf, new Vector3(x, y + bookHeight * 0.76f, 0.615f), new Vector3(0.14f, 0.025f, 0.01f), Brass);
                Box(shelf, new Vector3(x, y + 0.19f, 0.615f), new Vector3(0.14f, 0.025f, 0.01f), Brass);
            }
        }
    }

    private void BuildBookStack(Node3D parent, Vector3 origin, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 position = origin + Vector3.Up * (0.1f + i * 0.19f);
            float rotation = -8 + i * 7 % 19;
            string color = i % 3 == 0 ? Coral : i % 3 == 1 ? Teal : WarmWood;
            Box(parent, position, new Vector3(0.89f, 0.16f, 0.68f), Paper, rotation: new Vector3(0, rotation, 0));
            Box(parent, position + Vector3.Up * 0.087f, new Vector3(0.93f, 0.028f, 0.73f), color, rotation: new Vector3(0, rotation, 0));
            Box(parent, position - Vector3.Up * 0.087f, new Vector3(0.93f, 0.028f, 0.73f), color, rotation: new Vector3(0, rotation, 0));
        }
    }

    private void WallPicture(Node3D parent, Vector3 position, Vector2 size)
    {
        Box(parent, position, new Vector3(size.X + 0.13f, size.Y + 0.13f, 0.1f), Brass);
        Box(parent, position + Vector3.Back * 0.08f, new Vector3(size.X, size.Y, 0.07f), "304a56");
        // Ivo is present only as this small painted portrait, never a living actor.
        Sphere(parent, position + new Vector3(0, -size.Y * 0.23f, 0.14f), new Vector3(size.X * 0.33f, size.Y * 0.27f, 0.022f), "a26c55");
        Sphere(parent, position + new Vector3(0, size.Y * 0.11f, 0.17f), new Vector3(size.X * 0.17f, size.Y * 0.2f, 0.027f), "b99b7f");
        Sphere(parent, position + new Vector3(0, size.Y * 0.26f, 0.19f), new Vector3(size.X * 0.18f, size.Y * 0.09f, 0.025f), "b2b6a2");
        Box(parent, position + new Vector3(-size.X * 0.063f, size.Y * 0.13f, 0.205f), new Vector3(0.065f, 0.02f, 0.013f), Ink);
        Box(parent, position + new Vector3(size.X * 0.063f, size.Y * 0.13f, 0.205f), new Vector3(0.065f, 0.02f, 0.013f), Ink);
    }

    private void BuildCrates(Node3D parent, Vector3 origin)
    {
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = origin + new Vector3((i % 2) * 1.02f, 0.46f + (i / 2) * 0.95f, 0);
            Box(parent, p, new Vector3(0.91f, 0.86f, 0.86f), i == 1 ? Wood : WarmWood);
            Box(parent, p + new Vector3(0, 0.32f, 0.45f), new Vector3(0.96f, 0.1f, 0.06f), "a28b66");
            Box(parent, p + new Vector3(0, -0.32f, 0.45f), new Vector3(0.96f, 0.1f, 0.06f), "a28b66");
            Box(parent, p + new Vector3(0, 0, 0.45f), new Vector3(0.1f, 1.1f, 0.07f), "a28b66", rotation: new Vector3(0, 0, -43));
        }
    }

    private void BuildCompassRose(Node3D parent, Vector3 center, float radius)
    {
        Torus(parent, center, radius, 0.019f, Brass);
        Torus(parent, center, radius * 0.83f, 0.011f, Brass);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Pi / 4;
            Vector3 tip = center + new Vector3(Mathf.Cos(angle) * radius * 0.77f, 0, Mathf.Sin(angle) * radius * 0.77f);
            Segment(parent, center, tip, i % 2 == 0 ? 0.025f : 0.014f, Brass);
        }
    }

    private void BuildLampPost(Node3D parent, Vector3 origin, float height)
    {
        Cylinder(parent, origin + Vector3.Up * height * 0.5f, 0.07f, 0.12f, height, Ink, 8);
        Cylinder(parent, origin + Vector3.Up * 0.2f, 0.22f, 0.3f, 0.4f, Ink, 8);
        Segment(parent, origin + Vector3.Up * height, origin + new Vector3(0.55f, height, 0), 0.065f, Ink);
        Lantern(parent, origin + new Vector3(0.55f, height - 0.65f, 0), 0.9f);
    }

    private void Lantern(Node3D parent, Vector3 origin, float scale)
    {
        var lamp = new Node3D { Position = origin, Scale = Vector3.One * scale };
        parent.AddChild(lamp);
        Cylinder(lamp, new Vector3(0, 0.1f, 0), 0.23f, 0.25f, 0.2f, Ink, 8);
        Cylinder(lamp, new Vector3(0, 0.42f, 0), 0.17f, 0.19f, 0.47f, Amber, 8, emissive: true);
        Cylinder(lamp, new Vector3(0, 0.69f, 0), 0.1f, 0.27f, 0.18f, Ink, 8);
        for (int i = 0; i < 4; i++)
        {
            float angle = Mathf.Pi * 0.25f + i * Mathf.Pi * 0.5f;
            Cylinder(lamp, new Vector3(Mathf.Cos(angle) * 0.21f, 0.43f, Mathf.Sin(angle) * 0.21f), 0.025f, 0.025f, 0.53f, Brass, 6);
        }
        Torus(lamp, new Vector3(0, 0.86f, 0), 0.13f, 0.025f, Brass, false, new Vector3(90, 0, 0));
        AddWarmLight(parent, origin + Vector3.Up * scale * 0.44f, 1.6f, 4.5f * scale);
    }

    private void Beam(Node3D parent, float length, float radius)
    {
        var material = Material("ffcf83", true, 0.047f);
        var beam = new MeshInstance3D
        {
            Name = "RotatingLanternBeam",
            Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = radius, Height = length, RadialSegments = 20 },
            MaterialOverride = material,
            Position = new Vector3(0, 0, -length * 0.5f),
            RotationDegrees = new Vector3(90, 0, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(beam);
    }

    private void BuildRain()
    {
        var random = new Random(6031);
        var mesh = new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.008f, Height = 0.48f, RadialSegments = 3 };
        var material = Material("a3bbc4", true, 0.28f);
        for (int i = 0; i < 76; i++)
        {
            var drop = new MeshInstance3D
            {
                Mesh = mesh,
                MaterialOverride = material,
                Position = new Vector3((float)random.NextDouble() * 28 - 13, (float)random.NextDouble() * 13, (float)random.NextDouble() * 20 - 10),
                RotationDegrees = new Vector3(0, 0, -9),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            _set.AddChild(drop);
            _rain.Add((drop, 7 + (float)random.NextDouble() * 3));
        }
    }

    private void AddWarmLight(Node3D parent, Vector3 position, float energy, float range)
    {
        var light = new OmniLight3D { Position = position, LightColor = new Color(Amber), LightEnergy = energy, OmniRange = range, OmniAttenuation = 1.35f };
        parent.AddChild(light);
        _lamps.Add((light, energy, _lamps.Count * 1.7f));
    }

    private static void AddCoolLight(Node3D parent, Vector3 position, float energy, float range)
    {
        parent.AddChild(new OmniLight3D { Position = position, LightColor = new Color("88b9b6"), LightEnergy = energy, OmniRange = range, OmniAttenuation = 1.2f });
    }

    private MeshInstance3D Box(Node3D parent, Vector3 position, Vector3 size, string color, bool emissive = false, Vector3 rotation = default)
    {
        var instance = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = position, RotationDegrees = rotation, MaterialOverride = Material(color, emissive) };
        parent.AddChild(instance);
        return instance;
    }

    private MeshInstance3D Cylinder(Node3D parent, Vector3 position, float topRadius, float bottomRadius, float height,
        string color, int sides = 12, Vector3 rotation = default, bool emissive = false)
    {
        var instance = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = topRadius, BottomRadius = bottomRadius, Height = height, RadialSegments = sides, Rings = 1 },
            Position = position,
            RotationDegrees = rotation,
            MaterialOverride = Material(color, emissive),
        };
        parent.AddChild(instance);
        return instance;
    }

    private MeshInstance3D Sphere(Node3D parent, Vector3 position, Vector3 radii, string color, bool emissive = false)
    {
        var instance = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 12, Rings = 5 },
            Position = position,
            Scale = radii,
            MaterialOverride = Material(color, emissive),
        };
        parent.AddChild(instance);
        return instance;
    }

    private MeshInstance3D Torus(Node3D parent, Vector3 position, float radius, float thickness, string color,
        bool emissive = false, Vector3 rotation = default)
    {
        var instance = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = Math.Max(0.005f, radius - thickness), OuterRadius = radius + thickness, Rings = 20, RingSegments = 6 },
            Position = position,
            RotationDegrees = rotation,
            MaterialOverride = Material(color, emissive),
        };
        parent.AddChild(instance);
        return instance;
    }

    private void Segment(Node3D parent, Vector3 start, Vector3 end, float radius, string color)
    {
        Vector3 direction = end - start;
        if (direction.LengthSquared() < 0.000001f) return;
        var segment = Cylinder(parent, (start + end) * 0.5f, radius, radius, direction.Length(), color, 7);
        Vector3 axis = direction.Normalized();
        segment.Quaternion = new Quaternion(Vector3.Up, axis);
    }

    private void Rope(Node3D parent, Vector3 start, Vector3 end, float radius, float sag)
    {
        Vector3 previous = start;
        for (int i = 1; i <= 10; i++)
        {
            float t = i / 10f;
            Vector3 point = start.Lerp(end, t) - Vector3.Up * (Mathf.Sin(t * Mathf.Pi) * sag);
            Segment(parent, previous, point, radius, "ab9e7d");
            previous = point;
        }
    }

    private void Rock(Node3D parent, Vector3 position, Vector3 radii, string color, int seed)
    {
        var random = new Random(seed);
        const int sides = 7;
        var lower = new Vector3[sides];
        var upper = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.Tau / sides;
            float radius = 0.85f + (float)random.NextDouble() * 0.24f;
            lower[i] = new Vector3(Mathf.Cos(a) * radius, -0.52f, Mathf.Sin(a) * radius);
            upper[i] = new Vector3(Mathf.Cos(a + 0.15f) * radius * 0.75f, 0.45f + (float)random.NextDouble() * 0.22f, Mathf.Sin(a + 0.15f) * radius * 0.72f);
        }
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 peak = new(0.05f, 0.7f, -0.12f);
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            AddTriangle(surface, lower[i], upper[i], upper[next]);
            AddTriangle(surface, lower[i], upper[next], lower[next]);
            AddTriangle(surface, upper[i], peak, upper[next]);
        }
        parent.AddChild(new MeshInstance3D { Mesh = surface.Commit(), Position = position, Scale = radii, MaterialOverride = Material(color) });
    }

    private static void AddTriangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = (b - a).Cross(c - a).Normalized();
        surface.SetNormal(normal);
        surface.AddVertex(a);
        surface.SetNormal(normal);
        surface.AddVertex(b);
        surface.SetNormal(normal);
        surface.AddVertex(c);
    }

    private StandardMaterial3D Material(string hex, bool emissive = false, float opacity = 1)
    {
        string key = $"{hex}:{emissive}:{opacity}";
        if (_materials.TryGetValue(key, out var material)) return material;
        var color = new Color(hex) { A = opacity };
        material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.9f,
            Metallic = 0,
            DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Toon,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        if (emissive)
        {
            material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            material.EmissionEnabled = true;
            material.Emission = new Color(hex);
            material.EmissionEnergyMultiplier = 0.6f;
        }
        if (opacity < 1)
        {
            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            material.NoDepthTest = false;
        }
        _materials[key] = material;
        return material;
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
