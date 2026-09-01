// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.AnimatingScene;

using System;

using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;

internal static class AnimatingSceneProgram
{
    private static int Main()
    {
        try
        {
            using var application = new AnimatingScene();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt AnimatingScene sample finished.");
            return exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>
/// Animating 3D scene example.
/// This sample demonstrates:
///     - Creating a 3D scene and using a custom component to animate spacecraft
///     - Controlling scene ambience with the Zone component
///     - Lighting a large focal object with directional and point lights
/// </summary>
public class AnimatingScene : Application
{
    private const string SpaceshipPath = "ThirdParty/Quaternius/UltimateSpaceshipsShowcase.glb.d/Models/";

    /// <summary>
    /// Construct.
    /// </summary>
    public AnimatingScene()
    {
    }

    /// <summary>
    /// Setup after engine initialization and before running the main loop.
    /// </summary>
    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        base.Start();

        CreateScene();
        SetupViewport();
    }

    /// <summary>
    /// Construct the scene content.
    /// </summary>
    private void CreateScene()
    {
        var cache = Resources;

        _scene = new Scene();
        _scene.CreateComponent<Octree>();

        var zoneNode = _scene.CreateChild("Space");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.015f, 0.025f, 0.055f));
        zone.SetFogColor(new Color(0.003f, 0.006f, 0.018f));
        zone.SetFogStart(110.0f);
        zone.SetFogEnd(240.0f);

        var starModel = cache.Get<Model>("Models/Box.mdl");
        var starMaterial = cache.Get<Material>("Materials/Constant/UnlitWhite.xml");
        const int numStars = 240;
        for (var i = 0; i < numStars; ++i)
        {
            var starNode = _scene.CreateChild("Star");
            starNode.SetPosition(new Vector3f(
                MathDefs.Random(150.0f) - 75.0f,
                MathDefs.Random(75.0f) - 35.0f,
                MathDefs.Random(95.0f) + 48.0f));
            starNode.SetScale(0.025f + MathDefs.Random(0.08f));
            var star = starNode.CreateComponent<StaticModel>();
            star.SetModel(starModel);
            star.SetMaterial(starMaterial);
        }

        var moonNode = _scene.CreateChild("Moon");
        moonNode.SetPosition(new Vector3f(-34.0f, 10.0f, 43.0f));
        moonNode.SetScale(38.0f);
        var moon = moonNode.CreateComponent<StaticModel>();
        moon.SetModel(cache.Get<Model>("Models/Sphere.mdl"));
        moon.SetMaterial(cache.Get<Material>("Materials/AnimatingScene/Moon.xml"));
        moon.SetCastShadows(true);
        var moonRotator = moonNode.CreateComponent<Rotator>();
        moonRotator.SetRotationSpeed(new Vector3f(0.0f, 1.5f, 0.15f));

        var keyLightNode = _scene.CreateChild("Sunlight");
        keyLightNode.SetRotation(new Quaternion(28.0f, -42.0f, 0.0f));
        var keyLight = keyLightNode.CreateComponent<Light>();
        keyLight.SetLightType(LightType.Directional);
        keyLight.SetBrightness(1.15f);
        keyLight.SetColor(new Color(0.72f, 0.82f, 1.0f));
        keyLight.SetCastShadows(true);

        var rimLightNode = _scene.CreateChild("Moonlight");
        rimLightNode.SetPosition(new Vector3f(-15.0f, 6.0f, 25.0f));
        var rimLight = rimLightNode.CreateComponent<Light>();
        rimLight.SetLightType(LightType.Point);
        rimLight.SetRange(70.0f);
        rimLight.SetBrightness(0.65f);
        rimLight.SetColor(new Color(0.22f, 0.36f, 0.7f));

        var bob = cache.Get<Model>(SpaceshipPath + "Bob.mdl");
        var challenger = cache.Get<Model>(SpaceshipPath + "Challenger.mdl");
        var omen = cache.Get<Model>(SpaceshipPath + "Omen.mdl");

        CreateSpacecraft("Courier", bob, new Vector3f(-80.0f, -40.0f, 20.0f), new Vector3f(80.0f, 20.0f, 30.0f), 18.0f, 0.18f, 1.45f);
        CreateSpacecraft("Patrol", challenger, new Vector3f(80.0f, 25.0f, 28.0f), new Vector3f(-80.0f, -35.0f, 22.0f), 21.0f, 0.3f, 1.3f);
        CreateSpacecraft("Shuttle", omen, new Vector3f(-80.0f, -45.0f, 26.0f), new Vector3f(80.0f, 15.0f, 30.0f), 24.0f, 0.45f, 1.05f);
        CreateSpacecraft("Freighter", challenger, new Vector3f(80.0f, 20.0f, 22.0f), new Vector3f(-80.0f, -40.0f, 28.0f), 26.0f, 0.58f, 1.15f);
        CreateSpacecraft("Escort A", bob, new Vector3f(-80.0f, -38.0f, 24.0f), new Vector3f(80.0f, 22.0f, 29.0f), 20.0f, 0.7f, 0.8f);
        CreateSpacecraft("Escort B", omen, new Vector3f(80.0f, 17.0f, 30.0f), new Vector3f(-80.0f, -43.0f, 24.0f), 28.0f, 0.82f, 0.75f);

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.SetPosition(new Vector3f(0.0f, 3.5f, -24.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 0.0f, 36.0f));
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(250.0f);
    }

    private void CreateSpacecraft(string name, Model? model, in Vector3f start, in Vector3f end,
        float duration, float phase, float scale)
    {
        var node = _scene!.CreateChild(name);
        node.SetScale(scale);
        var spacecraft = node.CreateComponent<StaticModel>();
        spacecraft.SetModel(model);
        spacecraft.SetCastShadows(false);
        node.CreateComponent<SpacecraftFlight>().Configure(start, end, duration, phase);
    }

    /// <summary>
    /// Set up a viewport for displaying the scene.
    /// </summary>
    private void SetupViewport()
    {
        _viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>());
        Renderer.SetViewport(0, _viewport);
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.AnimatingScene";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Animating Scene";
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit"))
            Exit();
    }

    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        Renderer.SetNumViewports(0);
        base.Stop();
    }

    public override void Dispose()
    {
        if (_viewport is not null)
            Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        _viewport?.Dispose();
        _viewport = null;
        base.Dispose();
    }

    private Viewport? _viewport;
    private Scene? _scene;
    private Node? _cameraNode;
    private SampleInputMap? _sampleInput;
}
