using System;
using System.Collections.Generic;
using System.IO;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try
{
    var exitCode = new DynamicGeometryApplication().Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt DynamicGeometry sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class DynamicGeometryApplication : Application
{
    private readonly CString _instructionLine = new();
    private readonly List<(CustomGeometry Geometry, List<Vector3f> OriginalVertices, List<uint> Duplicates)> _objects = [];
    private ContextEventSubscription? _dearImGuiSubscription;
    private bool _animate = true;
    private float _time;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.DynamicGeometry";
        parameters.GraphicsBackend = GraphicsBackend.OpenGL;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Dynamic Geometry";
        parameters.WindowWidth = 1280;
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        var scene = new Scene();
        scene.CreateComponent<RenderPipeline>();
        scene.CreateComponent<Octree>();

        var zoneNode = scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(new Color(0.2f, 0.2f, 0.2f));
        zone.SetFogStart(200.0f);
        zone.SetFogEnd(300.0f);

        var lightNode = scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(-0.6f, -1.0f, -0.8f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetColor(new Color(0.4f, 1.0f, 0.4f));
        light.SetSpecularIntensity(1.5f);

        for (var y = -1; y <= 1; ++y)
        {
            for (var x = -1; x <= 1; ++x)
            {
                var node = scene.CreateChild("Object");
                node.SetPosition(new Vector3f(x * 2.0f, 0.0f, y * 2.0f));
                var geometry = node.CreateComponent<CustomGeometry>();
                CreateCube(geometry);
                geometry.SetMaterial(Resources.Get<Material>("Materials/Stone.xml"));
                _objects.Add((geometry, geometry.GetVertices()[0].ConvertAll(static vertex => vertex.Position), CreateDuplicates(geometry.GetVertices()[0])));
            }
        }

        var cameraNode = scene.CreateChild("Camera");
        cameraNode.CreateComponent<FreeFlyController>().SetInput(Input);
        cameraNode.SetPosition(new Vector3f(0.0f, 2.0f, -20.0f));
        var camera = cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(300.0f);

        Renderer.SetViewport(0, new Viewport(scene, camera));
        Input.SetMouseMode(MouseMode.Relative);
        Input.SetMouseVisible(false);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
        if (Input.GetKeyPress(Key.Space))
            _animate = !_animate;
        if (_animate)
            AnimateObjects(timeStep);
    }

    protected override void Stop()
    {
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
        _instructionLine.Dispose();
    }

    private void AnimateObjects(float timeStep)
    {
        _time += timeStep * 100.0f;
        foreach (var item in _objects)
        {
            var vertices = item.Geometry.GetVertices()[0];
            for (var i = 0; i < vertices.Count; ++i)
            {
                var phase = _time + item.Duplicates[i] * 10.0f;
                var source = item.OriginalVertices[i];
                vertices[i].Position = new Vector3f(
                    source.X * (1.0f + 0.1f * MathF.Sin(phase)),
                    source.Y * (1.0f + 0.1f * MathF.Sin(phase + 60.0f)),
                    source.Z * (1.0f + 0.1f * MathF.Sin(phase + 120.0f)));
            }
            item.Geometry.Commit();
        }
    }

    private static void CreateCube(CustomGeometry geometry)
    {
        geometry.SetDynamic(true);
        geometry.BeginGeometry(0, PrimitiveType.TriangleList);
        var positions = new[]
        {
            new Vector3f(-0.5f, -0.5f, 0.5f), new Vector3f(0.5f, -0.5f, 0.5f), new Vector3f(0.5f, 0.5f, 0.5f),
            new Vector3f(-0.5f, -0.5f, 0.5f), new Vector3f(0.5f, 0.5f, 0.5f), new Vector3f(-0.5f, 0.5f, 0.5f),
            new Vector3f(0.5f, -0.5f, -0.5f), new Vector3f(-0.5f, -0.5f, -0.5f), new Vector3f(-0.5f, 0.5f, -0.5f),
            new Vector3f(0.5f, -0.5f, -0.5f), new Vector3f(-0.5f, 0.5f, -0.5f), new Vector3f(0.5f, 0.5f, -0.5f),
            new Vector3f(-0.5f, 0.5f, 0.5f), new Vector3f(0.5f, 0.5f, 0.5f), new Vector3f(0.5f, 0.5f, -0.5f),
            new Vector3f(-0.5f, 0.5f, 0.5f), new Vector3f(0.5f, 0.5f, -0.5f), new Vector3f(-0.5f, 0.5f, -0.5f),
            new Vector3f(-0.5f, -0.5f, -0.5f), new Vector3f(0.5f, -0.5f, -0.5f), new Vector3f(0.5f, -0.5f, 0.5f),
            new Vector3f(-0.5f, -0.5f, -0.5f), new Vector3f(0.5f, -0.5f, 0.5f), new Vector3f(-0.5f, -0.5f, 0.5f)
        };
        foreach (var position in positions)
        {
            geometry.DefineVertex(position);
            geometry.DefineNormal(position.Normalized);
        }
        geometry.Commit();
    }

    private static List<uint> CreateDuplicates(IReadOnlyList<CustomGeometryVertex> vertices)
    {
        var duplicates = new List<uint>(vertices.Count);
        for (var i = 0; i < vertices.Count; ++i)
        {
            var duplicate = (uint)i;
            for (var j = 0; j < i; ++j)
            {
                if (vertices[i].Position.Equals(vertices[j].Position))
                {
                    duplicate = (uint)j;
                    break;
                }
            }
            duplicates.Add(duplicate);
        }
        return duplicates;
    }

    private void DrawInstructions()
    {
        _instructionLine.FormatInPlace("Use WASD keys and mouse/touch to move");
        if (ImGui.Begin("Dynamic Geometry\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted(_instructionLine.Bytes);
            ImGui.TextUnformatted("Space to toggle animation\0"u8);
        }
        ImGui.End();
    }

    private static string FindResourcePrefixPath()
    {
        var envRoot = Environment.GetEnvironmentVariable("REDBOLT_RBFX_BIN_DIR");
        if (TryNormalizeResourcePrefixPath(envRoot, out var normalizedEnvRoot))
            return normalizedEnvRoot;
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "bin"), out var localBinRoot))
                return localBinRoot;
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "tests", "RedBolt.Tests", "TestData", "rbfx"), out var testDataRoot))
                return testDataRoot;
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                break;
        }
        throw new InvalidOperationException("DynamicGeometry sample could not locate rbfx CoreData/Data resource directories.");
    }

    private static bool TryNormalizeResourcePrefixPath(string? root, out string normalizedRoot)
    {
        normalizedRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(root))
            return false;
        var candidateRoot = Path.GetFullPath(root);
        if (!Directory.Exists(Path.Combine(candidateRoot, "CoreData")) || !Directory.Exists(Path.Combine(candidateRoot, "Data")))
            return false;
        normalizedRoot = candidateRoot;
        return true;
    }
}
