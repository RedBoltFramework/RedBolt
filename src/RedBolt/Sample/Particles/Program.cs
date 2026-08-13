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
using RedBolt.Particles;
using RedBolt.Particles.ParticleGraphNodes;
using RedBolt.Resource;
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
    private ParticleGraphEffect? _graphEffect;
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
        if (Context.GetSubsystem<ParticleGraphSystem>() is null)
            Context.RegisterSubsystem(new ParticleGraphSystem());
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

        _graphEffect = CreateGraphEffect();
        var graphEmitterNode = _scene.CreateChild("ParticleGraphRing");
        graphEmitterNode.SetPosition(new Vector3f(0.0f, 5.0f, 0.0f));
        graphEmitterNode.CreateComponent<ParticleGraphEmitter>().SetEffect(_graphEffect);

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

    private static ParticleGraphEffect CreateGraphEffect()
    {
        const int numParticles = 12;
        var effect = new ParticleGraphEffect();
        effect.SetNumLayers(numParticles);
        for (var i = 0; i < numParticles; ++i)
        {
            var layer = effect.GetLayer(i);
            layer.Capacity = 1;

            var emitCount = AddConstant(layer.EmitGraph, 1.0f);
            var emit = new Emit();
            emit.SetPinSource(0, emitCount);
            layer.EmitGraph.Add(emit);

            var angle = 360.0f * i / numParticles;
            var position = new Vector3f(MathDefs.Sin(angle) * 3.4f, MathDefs.Cos(angle) * 3.4f, 0.0f);
            AddSetAttribute(layer.InitGraph, "position", VariantType.Vector3,
                AddConstant(layer.InitGraph, position));

            var getPosition = new GetAttribute();
            getPosition.SetAttributeName("position");
            getPosition.SetAttributeType(VariantType.Vector3);
            var positionIndex = layer.UpdateGraph.Add(getPosition);
            var sizeIndex = AddConstant(layer.UpdateGraph, new Vector2f(0.8f, 0.8f));
            var frameIndex = AddConstant(layer.UpdateGraph, 0.0f);
            var colorIndex = AddConstant(layer.UpdateGraph, (i % 3) switch
            {
                0 => new Color(0.05f, 0.8f, 1.0f),
                1 => new Color(1.0f, 0.1f, 0.65f),
                _ => new Color(1.0f, 0.75f, 0.05f)
            });
            var rotationIndex = AddConstant(layer.UpdateGraph, 0.0f);
            var directionIndex = AddConstant(layer.UpdateGraph, Vector3f.Up);
            var render = new RenderBillboard
            {
                Material = new ResourceRef(typeof(Material).GetTypeId(), "Materials/Particle.xml"),
                Rows = 1,
                Columns = 1
            };
            render.SetPinSource(0, positionIndex);
            render.SetPinSource(1, sizeIndex);
            render.SetPinSource(2, frameIndex);
            render.SetPinSource(3, colorIndex);
            render.SetPinSource(4, rotationIndex);
            render.SetPinSource(5, directionIndex);
            layer.UpdateGraph.Add(render);
        }
        return effect;
    }

    private static int AddConstant(ParticleGraph graph, float value)
    {
        using var variant = new Variant(value);
        return AddConstant(graph, variant);
    }

    private static int AddConstant(ParticleGraph graph, Vector2f value)
    {
        using var variant = new Variant(value);
        return AddConstant(graph, variant);
    }

    private static int AddConstant(ParticleGraph graph, Vector3f value)
    {
        using var variant = new Variant(value);
        return AddConstant(graph, variant);
    }

    private static int AddConstant(ParticleGraph graph, Color value)
    {
        using var variant = new Variant(value);
        return AddConstant(graph, variant);
    }

    private static int AddConstant(ParticleGraph graph, in Variant value)
    {
        var constant = new Constant();
        constant.SetValue(value);
        return graph.Add(constant);
    }

    private static void AddSetAttribute(ParticleGraph graph, string name, VariantType type, int sourceNode)
    {
        var setAttribute = new SetAttribute();
        setAttribute.SetAttributeName(name);
        setAttribute.SetAttributeType(type);
        setAttribute.SetPinSource(1, sourceNode);
        graph.Add(setAttribute);
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
        _graphEffect?.Dispose();
        _graphEffect = null;
    }
}
