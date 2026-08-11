namespace RedBolt.Sample.Particles;

using System;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;

internal static class ParticlesProgram
{
    private static int Main()
    {
        try
        {
            return new ParticlesApplication().Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

sealed class ParticlesApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private Node? _burstRing;
    private Node? _discoRing;
    private Viewport? _viewport;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Particles";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Particles";
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        CreateScene();
        SetupViewport();

        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    private void CreateScene()
    {
        _scene = new Scene();
        _scene.CreateComponent<Octree>();

        var zone = _scene.CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-100.0f, 100.0f));
        zone.SetAmbientColor(new Color(0.015f, 0.02f, 0.06f));
        zone.SetFogColor(new Color(0.005f, 0.01f, 0.03f));
        zone.SetFogStart(35.0f);
        zone.SetFogEnd(70.0f);

        var floorNode = _scene.CreateChild("Floor");
        floorNode.SetScale(new Vector3f(60.0f, 1.0f, 60.0f));
        var floor = floorNode.CreateComponent<StaticModel>();
        floor.SetModel(Resources.Get<Model>("Models/Plane.mdl"));
        floor.SetMaterial(Resources.Get<Material>("Materials/StoneTiled.xml"));

        var pedestalNode = _scene.CreateChild("Pedestal");
        pedestalNode.SetPosition(new Vector3f(0.0f, 0.5f, 0.0f));
        pedestalNode.SetScale(new Vector3f(4.0f, 1.0f, 4.0f));
        var pedestal = pedestalNode.CreateComponent<StaticModel>();
        pedestal.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        pedestal.SetMaterial(Resources.Get<Material>("Materials/Stone.xml"));

        var fire = Resources.Get<ParticleEffect>("Particle/Fire.xml");
        var smoke = Resources.Get<ParticleEffect>("Particle/SmokeStack.xml");
        var burst = Resources.Get<ParticleEffect>("Particle/Burst.xml");
        var disco = Resources.Get<ParticleEffect>("Particle/Disco.xml");

        CreateEmitter(_scene, "Fire", new Vector3f(0.0f, 1.0f, 0.0f), 5.0f, fire);
        CreateEmitter(_scene, "Smoke", new Vector3f(0.0f, 1.0f, 0.0f), 2.5f, smoke);

        _burstRing = _scene.CreateChild("BurstRing");
        for (var i = 0; i < 8; ++i)
        {
            var angle = i * 45.0f;
            var position = new Vector3f(MathDefs.Sin(angle) * 6.0f, 4.0f, MathDefs.Cos(angle) * 6.0f);
            CreateEmitter(_burstRing, "Burst", position, 1.5f, burst);
        }

        _discoRing = _scene.CreateChild("DiscoRing");
        for (var i = 0; i < 6; ++i)
        {
            var angle = i * 60.0f + 30.0f;
            var position = new Vector3f(MathDefs.Sin(angle) * 4.0f, 8.0f, MathDefs.Cos(angle) * 4.0f);
            CreateEmitter(_discoRing, "Disco", position, 2.5f, disco);
        }

        for (var i = 0; i < 4; ++i)
        {
            var angle = i * 90.0f + 45.0f;
            var lightNode = _scene.CreateChild("PointLight");
            lightNode.SetPosition(new Vector3f(MathDefs.Sin(angle) * 5.0f, 3.0f, MathDefs.Cos(angle) * 5.0f));
            var light = lightNode.CreateComponent<Light>();
            light.SetLightType(LightType.Point);
            light.SetRange(14.0f);
            light.SetBrightness(2.5f);
            light.SetColor(i % 2 == 0
                ? new Color(1.0f, 0.12f, 0.04f)
                : new Color(0.05f, 0.3f, 1.0f));
        }

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>();
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(100.0f);
        _cameraNode.SetPosition(new Vector3f(0.0f, 8.0f, -23.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 4.0f, 0.0f));
    }

    private static void CreateEmitter(
        Node parent, string name, in Vector3f position, float scale, ParticleEffect effect)
    {
        var node = parent.CreateChild(name);
        node.SetPosition(position);
        node.SetScale(scale);
        node.CreateComponent<ParticleEmitter>().SetEffect(effect);
    }

    private void SetupViewport()
    {
        _viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!);
        Renderer.SetViewport(0, _viewport);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        _burstRing!.Rotate(new Quaternion(0.0f, 22.0f * timeStep, 0.0f), TransformSpace.World);
        _discoRing!.Rotate(new Quaternion(0.0f, -38.0f * timeStep, 0.0f), TransformSpace.World);
    }

    private static void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Particle Vortex"u8, ref windowOpen,
            ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Fire, smoke, sparks, and color in one scene"u8);
            ImGui.TextUnformatted("Use WASD keys and the right mouse button to move"u8);
        }
        ImGui.End();
    }

    protected override void Stop()
    {
        DisposeScene();
        base.Stop();
    }

    public override void Dispose()
    {
        DisposeScene();
        base.Dispose();
    }

    private void DisposeScene()
    {
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _viewport?.Dispose();
        _viewport = null;
        _discoRing = null;
        _burstRing = null;
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
    }
}
