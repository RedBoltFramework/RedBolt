using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Scene;

internal static class DynamicGeometryProgram
{
    private static int Main()
    {
        try
        {
            using var application = new DynamicGeometry();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt DynamicGeometry sample finished.");
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
/// Dynamic geometry example.
/// This sample demonstrates:
///     - Cloning a Model resource
///     - Modifying the vertex buffer data of the cloned models at runtime to efficiently animate them
///     - Creating a Model resource and its buffer data from scratch
/// </summary>
public class DynamicGeometry : Application
{
    /// <summary>
    /// Construct.
    /// </summary>
    public DynamicGeometry()
    {
        _animate = true;
        _time = 0.0f;
    }

    /// <summary>
    /// Setup after engine initialization and before running the main loop.
    /// </summary>
    protected override void Start()
    {
        // Execute base class startup
        base.Start();
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));

        // Create the scene content
        CreateScene();

        // Create the UI content
        CreateInstructions();

        // Setup the viewport for displaying the scene
        SetupViewport();

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    /// <summary>
    /// Construct the scene content.
    /// </summary>
    private void CreateScene()
    {
        var cache = Resources;

        _scene = new Scene();

        // Create the Octree component to the scene so that drawable objects can be rendered. Use default volume
        // (-1000, -1000, -1000) to (1000, 1000, 1000)
        _scene.CreateComponent<Octree>();

        // Create a Zone for ambient light & fog control
        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(new Color(0.2f, 0.2f, 0.2f));
        zone.SetFogStart(200.0f);
        zone.SetFogEnd(300.0f);

        // Create a directional light
        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(-0.6f, -1.0f, -0.8f)); // The direction vector does not need to be normalized
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetColor(new Color(0.4f, 1.0f, 0.4f));
        light.SetSpecularIntensity(1.5f);

        // Get the original model and its unmodified vertices, which are used as source data for the animation
        var originalModel = cache.GetResourceOrNull<Model>("Models/Box.mdl");
        if (originalModel is null)
        {
            Log.GetLogger().LogError("Model not found, cannot initialize example scene");
            return;
        }
        // Get the vertex buffer from the first geometry's first LOD level
        var buffer = originalModel.GetGeometry(0, 0)!.GetVertexBuffer(0)!;
        var vertexData = buffer.GetShadowData();
        if (vertexData.Length != 0)
        {
            var numVertices = checked((int)buffer.GetVertexCount());
            var vertexSize = checked((int)(buffer.GetVertexSize() / sizeof(float)));
            var vertexFloats = MemoryMarshal.Cast<byte, float>(vertexData.AsSpan());
            // Copy the original vertex positions
            for (var i = 0; i < numVertices; ++i)
            {
                var offset = i * vertexSize;
                var src = new Vector3f(vertexFloats[offset], vertexFloats[offset + 1], vertexFloats[offset + 2]);
                _originalVertices.Add(src);
            }

            // Detect duplicate vertices to allow seamless animation
            for (var i = 0; i < _originalVertices.Count; ++i)
            {
                _vertexDuplicates.Add(i); // Assume not a duplicate
                for (var j = 0; j < i; ++j)
                {
                    if (_originalVertices[i].Equals(_originalVertices[j]))
                    {
                        _vertexDuplicates[i] = j;
                        break;
                    }
                }
            }
        }
        else
        {
            Log.GetLogger().LogError("Failed to lock the model vertex buffer to get original vertices");
            return;
        }

        // Create StaticModels in the scene. Clone the model for each so that we can modify the vertex data individually
        for (var y = -1; y <= 1; ++y)
        {
            for (var x = -1; x <= 1; ++x)
            {
                var node = _scene.CreateChild("Object");
                node.SetPosition(new Vector3f(x * 2.0f, 0.0f, y * 2.0f));
                var staticModel = node.CreateComponent<StaticModel>();
                var cloneModel = originalModel.Clone();
                staticModel.SetModel(cloneModel);
                // Store the cloned vertex buffer that we will modify when animating
                _animatingBuffers.Add(cloneModel.GetGeometry(0, 0)!.GetVertexBuffer(0)!);
            }
        }

        // Finally create one model (pyramid shape) and a StaticModel to display it from scratch
        // Note: there are duplicated vertices to enable face normals. We will calculate normals programmatically
        {
            const int NumVertices = 18;

            float[] vertexDataFromScratch =
            [
                // Position             Normal
                0.0f, 0.5f, 0.0f,       0.0f, 0.0f, 0.0f,
                0.5f, -0.5f, 0.5f,      0.0f, 0.0f, 0.0f,
                0.5f, -0.5f, -0.5f,     0.0f, 0.0f, 0.0f,

                0.0f, 0.5f, 0.0f,       0.0f, 0.0f, 0.0f,
                -0.5f, -0.5f, 0.5f,     0.0f, 0.0f, 0.0f,
                0.5f, -0.5f, 0.5f,      0.0f, 0.0f, 0.0f,

                0.0f, 0.5f, 0.0f,       0.0f, 0.0f, 0.0f,
                -0.5f, -0.5f, -0.5f,    0.0f, 0.0f, 0.0f,
                -0.5f, -0.5f, 0.5f,     0.0f, 0.0f, 0.0f,

                0.0f, 0.5f, 0.0f,       0.0f, 0.0f, 0.0f,
                0.5f, -0.5f, -0.5f,     0.0f, 0.0f, 0.0f,
                -0.5f, -0.5f, -0.5f,    0.0f, 0.0f, 0.0f,

                0.5f, -0.5f, -0.5f,     0.0f, 0.0f, 0.0f,
                0.5f, -0.5f, 0.5f,      0.0f, 0.0f, 0.0f,
                -0.5f, -0.5f, 0.5f,     0.0f, 0.0f, 0.0f,

                0.5f, -0.5f, -0.5f,     0.0f, 0.0f, 0.0f,
                -0.5f, -0.5f, 0.5f,     0.0f, 0.0f, 0.0f,
                -0.5f, -0.5f, -0.5f,    0.0f, 0.0f, 0.0f
            ];

            ushort[] indexData =
            [
                0, 1, 2,
                3, 4, 5,
                6, 7, 8,
                9, 10, 11,
                12, 13, 14,
                15, 16, 17
            ];

            // Calculate face normals now
            for (var i = 0; i < NumVertices; i += 3)
            {
                var v1Offset = 6 * i;
                var v2Offset = 6 * (i + 1);
                var v3Offset = 6 * (i + 2);
                var v1 = new Vector3f(vertexDataFromScratch[v1Offset], vertexDataFromScratch[v1Offset + 1], vertexDataFromScratch[v1Offset + 2]);
                var v2 = new Vector3f(vertexDataFromScratch[v2Offset], vertexDataFromScratch[v2Offset + 1], vertexDataFromScratch[v2Offset + 2]);
                var v3 = new Vector3f(vertexDataFromScratch[v3Offset], vertexDataFromScratch[v3Offset + 1], vertexDataFromScratch[v3Offset + 2]);

                var edge1 = v1 - v2;
                var edge2 = v1 - v3;
                var normal = edge1.CrossProduct(edge2).Normalized;
                for (var j = 0; j < 3; ++j)
                {
                    var normalOffset = 6 * (i + j) + 3;
                    vertexDataFromScratch[normalOffset] = normal.X;
                    vertexDataFromScratch[normalOffset + 1] = normal.Y;
                    vertexDataFromScratch[normalOffset + 2] = normal.Z;
                }
            }

            var fromScratchModel = new Model();
            var vb = new VertexBuffer();
            var ib = new IndexBuffer();
            var geom = new Geometry();

            // Always name your GPU objects, its usefully when things goes wrong
            vb.SetDebugName("DynamicGeometry");
            ib.SetDebugName("DynamicGeometry");

            // Shadowed buffer needed for raycasts to work, and so that data can be automatically restored on device loss
            vb.SetShadowed(true);
            // We could use the "legacy" element bitmask to define elements for more compact code, but let's demonstrate
            // defining the vertex elements explicitly to allow any element types and order
            List<VertexElement> elements = [];
            elements.Add(new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position));
            elements.Add(new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Normal));
            vb.SetSize(NumVertices, elements);
            vb.Update(MemoryMarshal.AsBytes(vertexDataFromScratch.AsSpan()));

            ib.SetShadowed(true);
            ib.SetSize(NumVertices, false);
            ib.Update(MemoryMarshal.AsBytes(indexData.AsSpan()));

            geom.SetVertexBuffer(0, vb);
            geom.SetIndexBuffer(ib);
            geom.SetDrawRange(PrimitiveType.TriangleList, 0, NumVertices);

            fromScratchModel.SetNumGeometries(1);
            fromScratchModel.SetGeometry(0, 0, geom);
            fromScratchModel.SetBoundingBox(new BoundingBox(new Vector3f(-0.5f, -0.5f, -0.5f), new Vector3f(0.5f, 0.5f, 0.5f)));

            // Though not necessary to render, the vertex & index buffers must be listed in the model so that it can be saved properly
            List<VertexBuffer?> vertexBuffers = [];
            List<IndexBuffer> indexBuffers = [];
            vertexBuffers.Add(vb);
            indexBuffers.Add(ib);
            // Morph ranges could also be not defined. Here we simply define a zero range (no morphing) for the vertex buffer
            List<uint> morphRangeStarts = [];
            List<uint> morphRangeCounts = [];
            morphRangeStarts.Add(0);
            morphRangeCounts.Add(0);
            fromScratchModel.SetVertexBuffers(vertexBuffers, morphRangeStarts, morphRangeCounts);
            fromScratchModel.SetIndexBuffers(indexBuffers);

            var node = _scene.CreateChild("FromScratchObject");
            node.SetPosition(new Vector3f(0.0f, 3.0f, 0.0f));
            var staticModel = node.CreateComponent<StaticModel>();
            staticModel.SetModel(fromScratchModel);
        }

        // Create the camera
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>();
        _cameraNode.SetPosition(new Vector3f(0.0f, 2.0f, -20.0f));
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(300.0f);
    }

    /// <summary>
    /// Construct an instruction text to the UI.
    /// </summary>
    private void CreateInstructions()
    {
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
    }

    /// <summary>
    /// Set up a viewport for displaying the scene.
    /// </summary>
    private void SetupViewport()
    {
        var renderer = Renderer;

        // Set up a viewport to the Renderer subsystem so that the 3D scene can be seen
        _viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>());
        renderer.SetViewport(0, _viewport);
    }

    /// <summary>
    /// Animate the vertex data of the objects.
    /// </summary>
    private void AnimateObjects(float timeStep)
    {
        // TODO: port URHO3D_PROFILE("AnimateObjects") when managed profiling zones are available.

        _time += timeStep * 100.0f;

        // Repeat for each of the cloned vertex buffers
        for (var i = 0; i < _animatingBuffers.Count; ++i)
        {
            var startPhase = _time + i * 30.0f;
            var buffer = _animatingBuffers[i];

            // Lock the vertex buffer for update and rewrite positions with sine wave modulated ones
            // Cannot use discard lock as there is other data (normals, UVs) that we are not overwriting
            var vertexData = buffer.Map();
            if (!vertexData.IsEmpty)
            {
                var vertexSize = checked((int)(buffer.GetVertexSize() / sizeof(float)));
                var numVertices = checked((int)buffer.GetVertexCount());
                var vertexFloats = MemoryMarshal.Cast<byte, float>(vertexData);
                for (var j = 0; j < numVertices; ++j)
                {
                    // If there are duplicate vertices, animate them in phase of the original
                    var phase = startPhase + _vertexDuplicates[j] * 10.0f;
                    var src = _originalVertices[j];
                    var offset = j * vertexSize;
                    vertexFloats[offset] = src.X * (1.0f + 0.1f * MathDefs.Sin(phase));
                    vertexFloats[offset + 1] = src.Y * (1.0f + 0.1f * MathDefs.Sin(phase + 60.0f));
                    vertexFloats[offset + 2] = src.Z * (1.0f + 0.1f * MathDefs.Sin(phase + 120.0f));
                }

                buffer.Unmap();
            }
        }
    }

    /// <summary>
    /// Handle the logic update event.
    /// </summary>
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();

        // Toggle animation with space
        var input = Input;

        if (input.GetKeyPress(Key.Space))
            _animate = !_animate;

        // Animate objects' vertex data if enabled
        if (_animate)
            AnimateObjects(timeStep);
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.DynamicGeometry";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        if (ProcessUtils.GetPlatform() != PlatformId.Web)
        {
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
        }
        parameters.WindowTitle = "RedBolt Dynamic Geometry";
    }

    protected override void Stop()
    {
        _instructionLine.Dispose();
        Renderer.SetNumViewports(0);
        base.Stop();
    }

    public override void Dispose()
    {
        _instructionLine.Dispose();
        if (_viewport is not null)
            Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        _viewport?.Dispose();
        _viewport = null;
        base.Dispose();
    }

    private void DrawInstructions()
    {
        _instructionLine.FormatInPlace("Use WASD keys and mouse/touch to move");
        var windowOpen = true;
        if (ImGui.Begin("Dynamic Geometry\0"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted(_instructionLine.Bytes);
            ImGui.TextUnformatted("Space to toggle animation\0"u8);
        }
        ImGui.End();
    }

    private Viewport? _viewport;
    private Scene? _scene;
    private Node? _cameraNode;
    /// <summary>
    /// Cloned models' vertex buffers that we will animate.
    /// </summary>
    private readonly List<VertexBuffer> _animatingBuffers = [];
    /// <summary>
    /// Original vertex positions for the sphere model.
    /// </summary>
    private readonly List<Vector3f> _originalVertices = [];
    /// <summary>
    /// If the vertices are duplicates, indices to the original vertices (to allow seamless animation.)
    /// </summary>
    private readonly List<int> _vertexDuplicates = [];
    /// <summary>
    /// Animation flag.
    /// </summary>
    private bool _animate;
    /// <summary>
    /// Animation's elapsed time.
    /// </summary>
    private float _time;
    private readonly CString _instructionLine = new();
}
