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
            var actors = activeStage.CastOrigin.GetChildren().OfType<StageCharacter>().ToArray();
            Check(actors.Length == 2 && !actors[0].Position.IsEqualApprox(actors[1].Position), "two visible characters remain separated");
            var authored = director.Harbor.Instantiate<StageScene>();
            var cameraSize = authored.StoryCamera.Size + 2;
            var energy = authored.KeyLight.LightEnergy + .3f;
            var castPosition = authored.CastOrigin.Position + Vector3.Right;
            authored.StoryCamera.Size = cameraSize;
            authored.KeyLight.LightEnergy = energy;
            authored.CastOrigin.Position = castPosition;
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

    private static void Check(bool condition, string claim)
    {
        if (!condition) throw new InvalidOperationException("Authoring resource smoke: " + claim);
    }
}
