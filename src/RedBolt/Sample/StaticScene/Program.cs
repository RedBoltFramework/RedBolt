// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.StaticScene;

using System;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;

internal static class StaticSceneProgram
{
    private static int Main()
    {
        try
        {
            using var application = new StaticScene();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt StaticScene sample finished.");
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
/// Static 3D scene example.
/// This sample demonstrates:
///     - Creating a static 3D scene with components and resources
///     - Displaying the scene using the Renderer subsystem
///     - Handling keyboard and mouse input to move a freelook camera
/// </summary>
public class StaticScene : Application
{
    /// <summary>
    /// Construct.
    /// </summary>
    public StaticScene()
    {
    }

    /// <summary>
    /// Setup after engine initialization and before running the main loop.
    /// </summary>
    protected override void Start()
    {
        // Execute base class startup
        base.Start();

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

        // Create the Octree component to the scene. This is required before adding any drawable components, or else nothing will
        // show up. The default octree volume will be from (-1000, -1000, -1000) to (1000, 1000, 1000) in world coordinates; it
        // is also legal to place objects outside the volume but their visibility can then not be checked in a hierarchically
        // optimizing manner
        _scene.CreateComponent<Octree>();

        // Create a child scene node (at world origin) and a StaticModel component into it. Scale the plane to 100 x 100 world units.
        var planeNode = _scene.CreateChild("Plane");
        planeNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f));
        var planeObject = planeNode.CreateComponent<StaticModel>();
        planeObject.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        planeObject.SetMaterial(cache.Get<Material>("ThirdParty/KenneyNature/grass.glb.d/Materials/grass_Lit.xml"));

        var zone = _scene.CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.28f, 0.30f, 0.26f));
        zone.SetFogColor(new Color(0.62f, 0.72f, 0.82f));
        zone.SetFogStart(75.0f);
        zone.SetFogEnd(180.0f);

        // Create a warm directional light with cascaded shadows.
        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetColor(new Color(1.0f, 0.84f, 0.68f));
        light.SetCastShadows(true);
        light.SetShadowBias(new BiasParameters(0.00025f, 0.5f));
        light.SetShadowCascade(new CascadeParameters(10.0f, 50.0f, 150.0f, 0.0f, 0.8f));

        // Repeated trees use instancing when the GPU supports it.
        const int NumTrees = 160;
        var treeModel = cache.Get<Model>("ThirdParty/KenneyNature/tree_default.glb.d/Models/Mesh tree_default.mdl");
        var barkMaterial = cache.Get<Material>("ThirdParty/KenneyNature/tree_default.glb.d/Materials/woodBark_Lit.xml");
        var leavesMaterial = cache.Get<Material>("ThirdParty/KenneyNature/tree_default.glb.d/Materials/leafsGreen_Lit.xml");
        for (var i = 0; i < NumTrees; ++i)
        {
            var treeNode = _scene.CreateChild("Tree");
            treeNode.SetPosition(new Vector3f(MathDefs.Random(90.0f) - 45.0f, 0.0f, MathDefs.Random(90.0f) - 45.0f));
            treeNode.SetRotation(new Quaternion(0.0f, MathDefs.Random(360.0f), 0.0f));
            treeNode.SetScale(1.5f + MathDefs.Random(2.5f));
            var treeObject = treeNode.CreateComponent<StaticModel>();
            treeObject.SetModel(treeModel);
            treeObject.SetMaterial(0, barkMaterial);
            treeObject.SetMaterial(1, leavesMaterial);
            treeObject.SetCastShadows(true);
        }

        // Add low-poly flowers from the same nature kit as the trees.
        const int NumFlowers = 80;
        var flowerModel = cache.Get<Model>("ThirdParty/KenneyNature/flower_redA.glb.d/Models/Mesh flower_redA.mdl");
        var grassMaterial = cache.Get<Material>("ThirdParty/KenneyNature/flower_redA.glb.d/Materials/grass_Lit.xml");
        var flowerMaterial = cache.Get<Material>("ThirdParty/KenneyNature/flower_redA.glb.d/Materials/colorRed_Lit.xml");
        for (var i = 0; i < NumFlowers; ++i)
        {
            var flowerNode = _scene.CreateChild("Flower");
            flowerNode.SetPosition(new Vector3f(MathDefs.Random(90.0f) - 45.0f, 0.0f, MathDefs.Random(90.0f) - 45.0f));
            flowerNode.SetRotation(new Quaternion(0.0f, MathDefs.Random(360.0f), 0.0f));
            flowerNode.SetScale(0.8f + MathDefs.Random(0.8f));
            var flowerObject = flowerNode.CreateComponent<StaticModel>();
            flowerObject.SetModel(flowerModel);
            flowerObject.SetMaterial(0, grassMaterial);
            flowerObject.SetMaterial(1, flowerMaterial);
        }
        // Create a scene node for the camera, which we will move around.
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<Camera>().SetFarClip(180.0f);
        _cameraNode.CreateComponent<FreeFlyController>();
        _cameraNode.SetPosition(new Vector3f(0.0f, 7.0f, -18.0f));
    }

    /// <summary>
    /// Construct an instruction text to the UI.
    /// </summary>
    private void CreateInstructions()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        // Construct new Text object, set string to display and font to use
        // Position the text relative to the screen center
        // The rbfx Text control is replaced by the allowed Dear ImGui sample UI surface.
    }

    /// <summary>
    /// Set up a viewport for displaying the scene.
    /// </summary>
    private void SetupViewport()
    {
        // Set up a viewport to the Renderer subsystem so that the 3D scene can be seen. We need to define the scene and the camera
        // at minimum. Additionally we could configure the viewport screen size and the rendering path (eg. forward / deferred) to
        // use, but now we just use full screen and default render path configured in the engine command line options
        var viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!);
        Renderer.SetViewport(0, viewport);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

    }

    protected override void Stop()
    {
        Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Stop();
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Static Scene"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted("Use WASD keys and mouse/touch to move"u8);
        ImGui.End();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.StaticScene";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Static Scene";
    }

    public override void Dispose()
    {
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Dispose();
    }

    private Scene? _scene;
    private Node? _cameraNode;
}
