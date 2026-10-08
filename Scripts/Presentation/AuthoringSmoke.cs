using Godot;

namespace Lanternwake.Presentation;

/// <summary>Exercises scene/resource persistence through the same native formats used by the editor.</summary>
internal static class AuthoringSmoke
{
    public static void Run(Node host, StageDirector director, Control ui)
    {
        var scenePath = "user://authoring-scene-" + Guid.NewGuid().ToString("N") + ".tscn";
        var themePath = "user://authoring-theme-" + Guid.NewGuid().ToString("N") + ".tres";
        StageScene? reopened = null;
        try
        {
            var activeStage = director.GetChildren().OfType<StageScene>().Single();
            var actors = VisibleCast(activeStage);
            Check(actors.Length == 2 && !actors[0].GlobalPosition.IsEqualApprox(actors[1].GlobalPosition), "two visible characters remain separated");
            var authored = director.Harbor.Instantiate<StageScene>();
            var cameraSize = authored.StoryCamera.Size + 2;
            var energy = authored.KeyLight.LightEnergy + .3f;
            var castPosition = authored.CastOrigin.Position + Vector3.Right;
            authored.StoryCamera.Size = cameraSize;
            authored.KeyLight.LightEnergy = energy;
            authored.CastOrigin.Position = castPosition;
            var shared = authored.FindCastLayout("", characterCount: 2)!;
            shared.Slot1!.Position += new Vector3(.6f, .1f, -.4f);
            shared.Slot1.Rotation += new Vector3(.1f, .37f, .05f);
            shared.Slot1.Scale = new Vector3(1.1f, .95f, 1.07f);
            var sharedTransform = shared.Slot1.Transform;
            AddOverride(authored, shared, "ch1_s1a", "", new Vector3(-.7f, 0, .9f));
            AddOverride(authored, shared, "ch1_s1", "fixture_z_movement", new Vector3(1.4f, 0, -.7f));
            AddOverride(authored, shared, "ch1_s1", "fixture_a_movement", new Vector3(-1.2f, .1f, .8f));
            var packed = new PackedScene();
            Check(packed.Pack(authored) == Error.Ok, "pack editable stage");
            Check(ResourceSaver.Save(packed, scenePath) == Error.Ok, "save editable stage");
            authored.Free();
            reopened = ResourceLoader.Load<PackedScene>(scenePath, cacheMode: ResourceLoader.CacheMode.Ignore).Instantiate<StageScene>();
            host.AddChild(reopened);
            reopened.ApplyTimeOfDay("night");
            Check(Mathf.IsEqualApprox(reopened.StoryCamera.Size, cameraSize), "camera edit survives reopening and runtime initialization");
            Check(Mathf.IsEqualApprox(reopened.KeyLight.LightEnergy, energy), "lighting edit survives runtime look selection");
            Check(reopened.CastOrigin.Position.IsEqualApprox(castPosition), "cast placement survives reopening");
            Check(reopened.FindCastLayout("", characterCount: 2)!.Slot1!.Transform.IsEqualApprox(sharedTransform),
                "shared slot position, rotation, scale and binding survive reopening");
            CheckSharedCast(host, scenePath, sharedTransform);
            var rain = reopened.FindChildren("*", "", true, false).OfType<StageMotion>().First(m => m.Kind == StageMotion.MotionKind.RainFall);
            GD.Print($"LANTERNWAKE_AUTHORING_DEFAULTS pair_spacing={reopened.PairSpacing} dusk_multiplier={reopened.DuskKeyLightEnergyMultiplier} rain_enabled={rain.Enabled} rain_height={rain.RainHeight}");
            var rainPosition = rain.Target.Position;
            rain._Process(0.1);
            Check(rain.Enabled && !rain.Target.Position.IsEqualApprox(rainPosition), "authored rain remains enabled and moves at runtime");
            rain.MotionAllowed = false;
            rainPosition = rain.Target.Position;
            rain._Process(0.1);
            Check(rain.Target.Position.IsEqualApprox(rainPosition), "reduced motion freezes authored animation");

            var theme = (Theme)ui.Theme.Duplicate(true);
            theme.DefaultFontSize += 4;
            Check(ResourceSaver.Save(theme, themePath) == Error.Ok, "save interface theme");
            var loadedTheme = ResourceLoader.Load<Theme>(themePath, cacheMode: ResourceLoader.CacheMode.Ignore);
            var label = new Label { Theme = loadedTheme };
            host.AddChild(label);
            try { Check(label.GetThemeFontSize("font_size") == theme.DefaultFontSize, "saved UI style affects a runtime control"); }
            finally { host.RemoveChild(label); label.Free(); }
            GD.Print("LANTERNWAKE_AUTHORING_RESOURCE_OK camera lighting cast theme saved reopened applied");
        }
        finally
        {
            if (reopened is not null) { host.RemoveChild(reopened); reopened.Free(); }
            foreach (var path in new[] { scenePath, themePath })
                if (Godot.FileAccess.FileExists(path)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        }
    }

    private static void AddOverride(StageScene stage, StageCastLayout source, string sceneId, string beatId, Vector3 offset)
    {
        var copy = (StageCastLayout)source.Duplicate();
        copy.Name = beatId.Length > 0 ? beatId : sceneId;
        copy.StorySceneId = sceneId;
        copy.StartAtBeatId = beatId;
        stage.CastOrigin.AddChild(copy);
        copy.Owner = stage;
        foreach (var child in copy.GetChildren()) child.Owner = stage;
        Check(copy.Slot1 == copy.GetNode<Node3D>("Slot1") && copy.Slot1 != source.Slot1,
            "duplicating a layout remaps slots to independent instances");
        copy.Slot1!.Position += offset;
    }

    private static StageCharacter[] VisibleCast(StageScene stage) => stage.CastOrigin.FindChildren("*", "", true, false)
        .OfType<StageCharacter>().Where(actor => actor.IsVisibleInTree()).ToArray();

    private static StageCharacter Actor(StageScene stage, string name) => VisibleCast(stage).Single(actor => actor.Name == name);

    private static void CheckSharedCast(Node host, string harborPath, Transform3D sharedTransform)
    {
        var director = ResourceLoader.Load<PackedScene>("res://Scenes/Stages/StageDirector.tscn").Instantiate<StageDirector>();
        director.Harbor = ResourceLoader.Load<PackedScene>(harborPath, cacheMode: ResourceLoader.CacheMode.Ignore);
        director.MotionEnabled = false;
        host.AddChild(director);
        try
        {
            var pair = new[] { "ada", "nessa" };
            director.ShowLocation("harbor", "night", Array.Empty<string>());
            var stage = director.GetChildren().OfType<StageScene>().Single();
            Check(VisibleCast(stage).Length == 0, "editor samples are hidden before story cast selection");
            director.ShowLocation("harbor", "night", pair, "ch1_s1", new[] { "fixture_start" });
            var ada = Actor(stage, "Ada");
            var shared = stage.FindCastLayout("ch1_s1", characterCount: 2)!;
            Check(ada.Transform.IsEqualApprox(sharedTransform) && ada.GlobalTransform.IsEqualApprox(shared.GlobalTransform * sharedTransform),
                "game uses the edited shared slot and its parent transform");
            var secondPose = Actor(stage, "Nessa").GlobalTransform;
            director.ShowLocation("harbor", "night", pair, "ch2_s3");
            Check(Actor(stage, "Ada") == ada && ada.Transform.IsEqualApprox(sharedTransform),
                "repeated casts in different story scenes reuse the same layout and actor instances");
            director.ShowLocation("harbor", "night", new[] { "tomas", "ada" }, "future_scene");
            Check(Actor(stage, "Tomas").GlobalTransform.IsEqualApprox(secondPose)
                && Actor(stage, "Ada").Transform.IsEqualApprox(sharedTransform),
                "a different two-character cast automatically occupies the same shared slots");
            var tomas = Actor(stage, "Tomas");
            director.ShowLocation("harbor", "night", new[] { "ada", "tomas" }, "another_scene");
            Check(Actor(stage, "Tomas") == tomas, "story cast order cannot shuffle repeated characters between slots");
            foreach (var ids in new[] { new[] { "nessa" }, new[] { "tomas", "ada", "nessa" }, new[] { "sera", "tomas", "nessa", "ada" } })
            {
                director.ShowLocation("harbor", "night", ids, "unconfigured_scene");
                Check(VisibleCast(stage).Length == ids.Length && VisibleCast(stage).All(actor => actor.GetParent().Name == "Characters" + ids.Length),
                    "cast size automatically selects one, three or four character layouts without a scene entry");
                Check(VisibleCast(stage).All(actor => actor.Breathing?.MotionAllowed == false), "reduced motion applies to slotted actors");
            }
            director.ShowLocation("harbor", "night", pair, "ch1_s1a");
            var sceneOverride = stage.FindCastLayout("ch1_s1a", characterCount: 2)!;
            Check(Actor(stage, "Ada").Transform.IsEqualApprox(sceneOverride.Slot1!.Transform)
                && !sceneOverride.Slot1.Transform.IsEqualApprox(sharedTransform), "an explicit scene override takes priority over the shared count layout");
            director.ShowLocation("harbor", "night", pair, "ch1_s1", new[] { "fixture_start", "fixture_z_movement" });
            var moved = Actor(stage, "Ada");
            var beatTransform = stage.FindCastLayout("ch1_s1", new[] { "fixture_z_movement" }, 2)!.Slot1!.Transform;
            Check(moved.Transform.IsEqualApprox(beatTransform), "beat override applies at its trigger");
            director.ShowLocation("harbor", "night", pair, "ch1_s1", new[] { "fixture_start", "fixture_z_movement", "fixture_following" });
            Check(Actor(stage, "Ada") == moved, "following beats retain the active override and actor instances");
            director.ShowLocation("harbor", "night", pair, "ch1_s1", new[] { "fixture_z_movement", "fixture_a_movement", "fixture_following" });
            var latest = stage.FindCastLayout("ch1_s1", new[] { "fixture_z_movement", "fixture_a_movement" }, 2)!;
            Check(Actor(stage, "Ada").Transform.IsEqualApprox(latest.Slot1!.Transform), "latest trigger wins in story order, not ID order");
            Check(VisibleCast(stage).Length == 2, "inactive layouts and editor samples create no ghost characters");
            director.ShowLocation("keeper_house", "night", pair, "ch1_s2");
            director.ShowLocation("harbor", "night", pair, "ch1_s1", new[] { "fixture_z_movement", "fixture_following" });
            stage = director.GetChildren().OfType<StageScene>().Single();
            Check(Actor(stage, "Ada").Transform.IsEqualApprox(beatTransform), "restored or directly previewed later beats reconstruct the active override");
            director.ShowLocation("harbor", "night", pair, "ch1_s1", new[] { "fixture_start" });
            Check(Actor(stage, "Ada").Transform.IsEqualApprox(sharedTransform), "restarting a scene returns to its shared layout");
            director.MotionEnabled = true;
            var reference = stage.FindCastLayout("", characterCount: 2)!.Slot1!;
            var referenceTransform = reference.Transform;
            foreach (var motion in reference.FindChildren("*", "", true, false).OfType<StageMotion>()) motion._Process(.1);
            Check(reference.Transform.IsEqualApprox(referenceTransform) && Actor(stage, "Ada").Breathing?.MotionAllowed == true,
                "slot references remain static while real characters animate");
            director.MotionEnabled = false;
            // Retain compatibility when an author clears a shared layout.
            director.ShowLocation("harbor", "night", new[] { "ada" });
            var removed = stage.FindCastLayout("", characterCount: 2)!;
            stage.CastOrigin.RemoveChild(removed);
            removed.Free();
            director.ShowLocation("harbor", "night", pair, "unconfigured_scene");
            Check(Actor(stage, "Ada") == stage.AdaPlacement, "a missing shared layout retains legacy character placement fallback");
            GD.Print("LANTERNWAKE_SHARED_CAST_OK saved slot poses; repeat/different/reordered casts; counts 1-4; override priority; replay; no ghost samples");
            GD.Print("LANTERNWAKE_STORY_LAYOUTS_OK optional scene overrides and persistent beat triggers; story-order selection; direct/restarted playback");
        }
        finally { host.RemoveChild(director); director.Free(); }
    }

    private static void Check(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException("Authoring resource smoke: " + claim);
    }
}
