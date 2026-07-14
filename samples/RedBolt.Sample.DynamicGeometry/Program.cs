using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
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
    private readonly List<Vector3f> _originalVertices = [];
    private readonly List<uint> _vertexDuplicates = [];
    private readonly List<VertexBuffer> _animatingBuffers = [];
    private ContextEventSubscription? _dearImGuiSubscription;
    private bool _animate = true;
    private float _time;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.DynamicGeometry";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
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

        var originalModel = Resources.Get<Model>("Models/Box.mdl");
        var originalBuffer = originalModel.GetGeometry(0, 0)!.GetVertexBuffer(0)!;
        var originalData = originalBuffer.GetRawBuffer()!.GetShadowData();
        var originalFloats = MemoryMarshal.Cast<byte, float>(originalData.AsSpan());
        var originalVertexSize = (int)(originalBuffer.GetVertexSize() / sizeof(float));
        for (var i = 0; i < originalBuffer.GetVertexCount(); ++i)
        {
            var offset = (int)i * originalVertexSize;
            _originalVertices.Add(new Vector3f(originalFloats[offset], originalFloats[offset + 1], originalFloats[offset + 2]));
        }

        for (var i = 0; i < _originalVertices.Count; ++i)
        {
            var duplicate = (uint)i;
            for (var j = 0; j < i; ++j)
            {
                if (_originalVertices[i].Equals(_originalVertices[j]))
                {
                    duplicate = (uint)j;
                    break;
                }
            }
            _vertexDuplicates.Add(duplicate);
        }

        for (var y = -1; y <= 1; ++y)
        {
            for (var x = -1; x <= 1; ++x)
            {
                var node = scene.CreateChild("Object");
                node.SetPosition(new Vector3f(x * 2.0f, 0.0f, y * 2.0f));
                var model = originalModel.Clone();
                var staticModel = node.CreateComponent<StaticModel>();
                staticModel.SetModel(model);
                _animatingBuffers.Add(model.GetGeometry(0, 0)!.GetVertexBuffer(0)!);
            }
        }

        CreatePyramid(scene);

        var cameraNode = scene.CreateChild("Camera");
        cameraNode.CreateComponent<FreeFlyController>().SetInput(Input);
        cameraNode.SetPosition(new Vector3f(0.0f, 2.0f, -20.0f));
        var camera = cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(300.0f);

        Renderer.SetViewport(0, new Viewport(scene, camera));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
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
        for (var i = 0; i < _animatingBuffers.Count; ++i)
        {
            var startPhase = _time + i * 30.0f;
            var buffer = _animatingBuffers[i];
            var vertexData = buffer.Map();
            var vertexFloats = MemoryMarshal.Cast<byte, float>(vertexData);
            var vertexSize = (int)(buffer.GetVertexSize() / sizeof(float));
            for (var j = 0; j < buffer.GetVertexCount(); ++j)
            {
                var phase = startPhase + _vertexDuplicates[(int)j] * 10.0f;
                var source = _originalVertices[(int)j];
                var offset = (int)j * vertexSize;
                vertexFloats[offset] = source.X * (1.0f + 0.1f * MathDefs.Sin(phase));
                vertexFloats[offset + 1] = source.Y * (1.0f + 0.1f * MathDefs.Sin(phase + 60.0f));
                vertexFloats[offset + 2] = source.Z * (1.0f + 0.1f * MathDefs.Sin(phase + 120.0f));
            }
            buffer.Unmap();
        }
    }

    private static void CreatePyramid(Scene scene)
    {
        const uint numVertices = 18;
        var vertexData = new float[]
        {
             0.0f,  0.5f,  0.0f, 0.0f, 0.0f, 0.0f,  0.5f, -0.5f,  0.5f, 0.0f, 0.0f, 0.0f,
             0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 0.0f,  0.0f,  0.5f,  0.0f, 0.0f, 0.0f, 0.0f,
            -0.5f, -0.5f,  0.5f, 0.0f, 0.0f, 0.0f,  0.5f, -0.5f,  0.5f, 0.0f, 0.0f, 0.0f,
             0.0f,  0.5f,  0.0f, 0.0f, 0.0f, 0.0f, -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 0.0f,
            -0.5f, -0.5f,  0.5f, 0.0f, 0.0f, 0.0f,  0.0f,  0.5f,  0.0f, 0.0f, 0.0f, 0.0f,
             0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 0.0f, -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 0.0f,
             0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 0.0f,  0.5f, -0.5f,  0.5f, 0.0f, 0.0f, 0.0f,
            -0.5f, -0.5f,  0.5f, 0.0f, 0.0f, 0.0f,  0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 0.0f,
            -0.5f, -0.5f,  0.5f, 0.0f, 0.0f, 0.0f, -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 0.0f
        };

        for (var i = 0u; i < numVertices; i += 3)
        {
            var v1 = ReadPosition(vertexData, i);
            var v2 = ReadPosition(vertexData, i + 1);
            var v3 = ReadPosition(vertexData, i + 2);
            var normal = (v1 - v2).CrossProduct(v1 - v3).Normalized;
            for (var j = 0u; j < 3; ++j)
            {
                var offset = checked((int)(6 * (i + j) + 3));
                vertexData[offset] = normal.X;
                vertexData[offset + 1] = normal.Y;
                vertexData[offset + 2] = normal.Z;
            }
        }

        var vertexBuffer = new VertexBuffer();
        var indexBuffer = new IndexBuffer();
        var geometry = new Geometry();
        vertexBuffer.SetDebugName("DynamicGeometry");
        indexBuffer.SetDebugName("DynamicGeometry");
        vertexBuffer.SetShadowed(true);
        vertexBuffer.SetSize(numVertices,
        [
            new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position),
            new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Normal)
        ]);
        vertexBuffer.SetData(MemoryMarshal.AsBytes(vertexData.AsSpan()));
        indexBuffer.SetShadowed(true);
        indexBuffer.SetSize(numVertices, false);
        indexBuffer.SetUnpackedData(new uint[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 });
        geometry.SetVertexBuffer(0, vertexBuffer);
        geometry.SetIndexBuffer(indexBuffer);
        geometry.SetDrawRange(PrimitiveType.TriangleList, 0, numVertices);

        var model = new Model(string.Empty);
        model.SetNumGeometries(1);
        model.SetGeometry(0, 0, geometry);
        model.SetBoundingBox(new BoundingBox(new Vector3f(-0.5f, -0.5f, -0.5f), new Vector3f(0.5f, 0.5f, 0.5f)));
        model.SetVertexBuffers([vertexBuffer], [0], [0]);
        model.SetIndexBuffers([indexBuffer]);
        var node = scene.CreateChild("FromScratchObject");
        node.SetPosition(new Vector3f(0.0f, 3.0f, 0.0f));
        var staticModel = node.CreateComponent<StaticModel>();
        staticModel.SetModel(model);
    }

    private static Vector3f ReadPosition(float[] vertexData, uint vertexIndex)
    {
        var offset = checked((int)(6 * vertexIndex));
        return new Vector3f(vertexData[offset], vertexData[offset + 1], vertexData[offset + 2]);
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
