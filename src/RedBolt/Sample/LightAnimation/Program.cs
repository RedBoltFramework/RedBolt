namespace RedBolt.Sample.LightAnimation;

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

internal static class LightAnimationProgram
{
    private static int Main()
    {
        try { return new LightAnimationApplication().Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

/// <summary>
/// Light animation example.
/// This sample is base on StaticScene, and it demonstrates:
/// - Usage of attribute animation for light color &amp; UI animation
/// </summary>
sealed class LightAnimationApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private DearImGui? _dearImGui;
    private readonly Texture2D?[] _spriteTextures = new Texture2D[5];
    private readonly ImTextureRef?[] _spriteTextureReferences = new ImTextureRef[5];
    private float _uiTime;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.LightAnimation";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
        if (ProcessUtils.GetPlatform() != PlatformId.Web)
        {
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
        }
        parameters.WindowTitle = "RedBolt Light Animation";
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        _dearImGui = new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags);
        Context.RegisterSubsystem(_dearImGui);

        // Create the UI content
        CreateInstructions();

        // Create the scene content
        CreateScene();

        // Setup the viewport for displaying the scene
        SetupViewport();

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    /// <summary>Construct the scene content.</summary>
    private void CreateScene()
    {
        var cache = Resources;

        _scene = new Scene();

        // Create the Octree component to the scene. This is required before adding any drawable components, or else nothing will
        // show up. The default octree volume will be from (-1000, -1000, -1000) to (1000, 1000, 1000) in world coordinates; it
        // is also legal to place objects outside the volume but their visibility can then not be checked in a hierarchically
        // optimizing manner
        _scene.CreateComponent<Octree>();

        // Create a child scene node (at world origin) and a StaticModel component into it. Set the StaticModel to show a simple
        // plane mesh with a "stone" material. Note that naming the scene nodes is optional. Scale the scene node larger
        // (100 x 100 world units)
        var planeNode = _scene.CreateChild("Plane");
        planeNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f));
        var plane = planeNode.CreateComponent<StaticModel>();
        plane.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        plane.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));

        // Create a point light to the world so that we can see something.
        var lightContainer = _scene.CreateChild("PointLightContainer");
        var lightNode = lightContainer.CreateChild("PointLight");
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Point);
        light.SetRange(10.0f);

        // Apply light animation to light node
        lightNode.CreateComponent<AnimationController>().PlayNew(new AnimationParameters("Animations/LightAnimation.xml").Looped());

        // Create UI element animation
        // (note: a spritesheet and "Image Rect" attribute should be used in real use cases for better performance)
        for (var i = 0; i < _spriteTextures.Length; ++i)
        {
            var texture = cache.Get<Texture2D>($"Urho2D/GoldIcon/{i + 1}.png");
            _spriteTextures[i] = texture;
            var textureView = texture.GetRawTexture()!.GetHandles().ShaderResourceView!;
            _spriteTextureReferences[i] = new ImTextureRef(textureView.Handle);
        }

        // Create more StaticModel objects to the scene, randomly positioned, rotated and scaled. For rotation, we construct a
        // quaternion from Euler angles where the Y angle (rotation about the Y axis) is randomized. The mushroom model contains
        // LOD levels, so the StaticModel component will automatically select the LOD level according to the view distance (you'll
        // see the model get simpler as it moves further away). Finally, rendering a large number of the same object with the
        // same material allows instancing to be used, if the GPU supports it. This reduces the amount of CPU work in rendering the
        // scene.
        const int numObjects = 200;
        for (var i = 0; i < numObjects; ++i)
        {
            var node = _scene.CreateChild("Mushroom");
            node.SetPosition(new Vector3f(MathDefs.Random(90.0f) - 45.0f, 0.0f, MathDefs.Random(90.0f) - 45.0f));
            node.SetRotation(new Quaternion(0.0f, MathDefs.Random(360.0f), 0.0f));
            node.SetScale(0.5f + MathDefs.Random(2.0f));
            var model = node.CreateComponent<StaticModel>();
            model.SetModel(cache.Get<Model>("Models/Mushroom.mdl"));
            model.SetMaterial(cache.Get<Material>("Materials/Mushroom.xml"));
        }

        // Create a scene node for the camera, which we will move around
        // The camera will use default settings (1000 far clip distance, 45 degrees FOV, set aspect ratio automatically)
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>();
        _cameraNode.CreateComponent<Camera>();

        // Set an initial position for the camera scene node above the plane
        _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, 0.0f));
    }

    /// <summary>Construct an instruction text to the UI.</summary>
    private void CreateInstructions()
    {
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
    }

    /// <summary>Set up a viewport for displaying the scene.</summary>
    private void SetupViewport()
    {
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
        _uiTime = (_uiTime + timeStep) % 4.0f;
    }

    protected override void Stop()
    {
        for (var i = _spriteTextureReferences.Length - 1; i >= 0; --i)
            _spriteTextureReferences[i]?.Dispose();
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Light Animation\0"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Use WASD keys and right mouse button to move\0"u8);
            if (_uiTime < 1.0f)
                ImGui.TextUnformatted("WHITE\0"u8);
            else if (_uiTime < 2.0f)
                ImGui.TextUnformatted("RED\0"u8);
            else if (_uiTime < 3.0f)
                ImGui.TextUnformatted("YELLOW\0"u8);
            else
                ImGui.TextUnformatted("GREEN\0"u8);

            var spriteFrame = (int)(_uiTime * 10.0f) % _spriteTextures.Length;
            _dearImGui!.ReferenceTexture(_spriteTextures[spriteFrame]!);
            var spriteSize = new Vector2f(64.0f, 64.0f);
            ImGui.Image(_spriteTextureReferences[spriteFrame]!, spriteSize);
        }
        ImGui.End();
    }
}
