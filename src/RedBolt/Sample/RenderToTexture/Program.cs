namespace RedBolt.Sample.RenderToTexture;

using System;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Scene;

internal static class RenderToTextureProgram
{
    private static int Main()
    {
        try
        {
            using var application = new RenderToTexture();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt RenderToTexture sample finished.");
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
/// Render to texture example.
/// This sample demonstrates:
///     - Creating two 3D scenes and rendering the other into a texture
///     - Creating rendertarget texture and material programmatically
/// </summary>
public class RenderToTexture : Application
{
    /// <summary>Construct.</summary>
    public RenderToTexture()
    {
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
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

    /// <summary>Construct the scene content.</summary>
    private void CreateScene()
    {
        var cache = Resources;

        {
            // Create the scene which will be rendered to a texture
            _rttScene = new Scene();

            // Create octree, use default volume (-1000, -1000, -1000) to (1000, 1000, 1000)
            _rttScene.CreateComponent<Octree>();

            // Create a Zone for ambient light & fog control
            var renderZone = _rttScene.CreateChild("Zone").CreateComponent<Zone>();
            // Set same volume as the Octree, set a close bluish fog and some ambient light
            renderZone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
            renderZone.SetAmbientColor(new Color(0.05f, 0.1f, 0.15f));
            renderZone.SetFogColor(new Color(0.1f, 0.2f, 0.3f));
            renderZone.SetFogStart(10.0f);
            renderZone.SetFogEnd(100.0f);

            // Create randomly positioned and oriented box StaticModels in the scene
            const int NumObjects = 2000;
            for (var i = 0; i < NumObjects; ++i)
            {
                var boxNode = _rttScene.CreateChild("Box");
                boxNode.SetPosition(new Vector3f(MathDefs.Random(200.0f) - 100.0f, MathDefs.Random(100.0f), MathDefs.Random(200.0f) - 100.0f));
                // Orient using random pitch, yaw and roll Euler angles
                boxNode.SetRotation(new Quaternion(MathDefs.Random(360.0f), MathDefs.Random(360.0f), MathDefs.Random(360.0f)));
                var box = boxNode.CreateComponent<StaticModel>();
                box.SetModel(cache.Get<Model>("Models/Box.mdl"));
                box.SetMaterial(cache.Get<Material>("Materials/Stone.xml"));

                // Add our custom Rotator component which will rotate the scene node each frame, when the scene sends its update event.
                // Simply set same rotation speed for all objects
                boxNode.CreateComponent<Rotator>().SetRotationSpeed(new Vector3f(10.0f, 20.0f, 30.0f));
            }

            // Create a camera for the render-to-texture scene. Simply leave it at the world origin and let it observe the scene
            _rttCameraNode = _rttScene.CreateChild("Camera");
            var renderCamera = _rttCameraNode.CreateComponent<Camera>();
            renderCamera.SetFarClip(100.0f);

            // Create a point light to the camera scene node
            var renderLight = _rttCameraNode.CreateComponent<Light>();
            renderLight.SetLightType(LightType.Point);
            renderLight.SetRange(30.0f);
        }

        {
            // Create the scene in which we move around
            _scene = new Scene();

            // Create octree, use also default volume (-1000, -1000, -1000) to (1000, 1000, 1000)
            _scene.CreateComponent<Octree>();

            // Create a Zone component for ambient lighting & fog control
            var zone = _scene.CreateChild("Zone").CreateComponent<Zone>();
            zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
            zone.SetAmbientColor(new Color(0.1f, 0.1f, 0.1f));
            zone.SetFogStart(100.0f);
            zone.SetFogEnd(300.0f);

            // Create a directional light without shadows
            var lightNode = _scene.CreateChild("DirectionalLight");
            lightNode.SetDirection(new Vector3f(0.5f, -1.0f, 0.5f));
            var light = lightNode.CreateComponent<Light>();
            light.SetLightType(LightType.Directional);
            light.SetColor(new Color(0.2f, 0.2f, 0.2f));
            light.SetSpecularIntensity(1.0f);

            // Create a "floor" consisting of several tiles
            for (var y = -5; y <= 5; ++y)
            {
                for (var x = -5; x <= 5; ++x)
                {
                    var floorNode = _scene.CreateChild("FloorTile");
                    floorNode.SetPosition(new Vector3f(x * 20.5f, -0.5f, y * 20.5f));
                    floorNode.SetScale(new Vector3f(20.0f, 1.0f, 20.0f));
                    var floor = floorNode.CreateComponent<StaticModel>();
                    floor.SetModel(cache.Get<Model>("Models/Box.mdl"));
                    floor.SetMaterial(cache.Get<Material>("Materials/Stone.xml"));
                }
            }

            // Create a "screen" like object for viewing the second scene. Construct it from two StaticModels, a box for the frame
            // and a plane for the actual view
            {
                var screenFrameNode = _scene.CreateChild("ScreenBox");
                screenFrameNode.SetPosition(new Vector3f(0.0f, 10.0f, 0.0f));
                screenFrameNode.SetScale(new Vector3f(21.0f, 16.0f, 0.5f));
                var screenFrame = screenFrameNode.CreateComponent<StaticModel>();
                screenFrame.SetModel(cache.Get<Model>("Models/Box.mdl"));
                screenFrame.SetMaterial(cache.Get<Material>("Materials/Stone.xml"));

                var screenNode = _scene.CreateChild("Screen");
                screenNode.SetPosition(new Vector3f(0.0f, 10.0f, -0.27f));
                screenNode.SetRotation(new Quaternion(-90.0f, 0.0f, 0.0f));
                screenNode.SetScale(new Vector3f(20.0f, 0.0f, 15.0f));
                var screen = screenNode.CreateComponent<StaticModel>();
                screen.SetModel(cache.Get<Model>("Models/Plane.mdl"));

                // Create a renderable texture (1024x768, RGB format), enable bilinear filtering on it
                _renderTexture = new Texture2D();
                _renderTexture.SetSize(1024, 768, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget, 4);
                _renderTexture.SetFilterMode(TextureFilterMode.Bilinear);

                // Create a new material from scratch, use the diffuse unlit technique, assign the render texture
                // as its diffuse texture, then assign the material to the screen plane object
                _renderMaterial = new Material();
                _renderMaterial.SetTechnique(0, cache.Get<Technique>("Techniques/DiffUnlit.xml"));
                _renderMaterial.SetTexture("Albedo", _renderTexture);
                // Since the screen material is on top of the box model and may Z-fight, use negative depth bias
                // to push it forward (particularly necessary on mobiles with possibly less Z resolution)
                _renderMaterial.SetDepthBias(new BiasParameters(-0.001f, 0.0f));
                screen.SetMaterial(_renderMaterial);

                // Get the texture's RenderSurface object (exists when the texture has been created in rendertarget mode)
                // and define the viewport for rendering the second scene, similarly as how backbuffer viewports are defined
                // to the Renderer subsystem. By default the texture viewport will be updated when the texture is visible
                // in the main view
                var renderSurface = _renderTexture.GetRenderSurface();
                var renderToTextureViewport = new Viewport(_rttScene, _rttCameraNode.GetComponent<Camera>()!);
                renderSurface!.SetViewport(0, renderToTextureViewport);
            }

            // Create the camera which we will move around. Limit far clip distance to match the fog
            _cameraNode = _scene.CreateChild("Camera");
            _cameraNode.CreateComponent<FreeFlyController>();
            var camera = _cameraNode.CreateComponent<Camera>();
            camera.SetFarClip(300.0f);
            // Set an initial position for the camera scene node above the plane
            _cameraNode.SetPosition(new Vector3f(0.0f, 7.0f, -30.0f));
        }
    }

    /// <summary>Construct an instruction text to the UI.</summary>
    private void CreateInstructions()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        // Construct new Text object, set string to display and font to use
        // Position the text relative to the screen center
        // The rbfx Text control is replaced by the allowed Dear ImGui sample UI surface.
    }

    /// <summary>Set up a viewport for displaying the scene.</summary>
    private void SetupViewport()
    {
        // Set up a viewport to the Renderer subsystem so that the 3D scene can be seen
        var viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!);
        Renderer.SetViewport(0, viewport);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
    }

    protected override void Stop()
    {
        _rttCameraNode = null;
        _rttScene?.Dispose();
        _rttScene = null;
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        _renderMaterial?.Dispose();
        _renderMaterial = null;
        _renderTexture?.Dispose();
        _renderTexture = null;
        base.Stop();
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Render To Texture"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted("Use WASD keys and mouse/touch to move"u8);
        ImGui.End();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.RenderToTexture";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Render To Texture";
    }

    public override void Dispose()
    {
        _rttCameraNode = null;
        _rttScene?.Dispose();
        _rttScene = null;
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        _renderMaterial?.Dispose();
        _renderMaterial = null;
        _renderTexture?.Dispose();
        _renderTexture = null;
        base.Dispose();
    }

    /// <summary>Scene that is rendered to a texture.</summary>
    private Scene? _rttScene;
    /// <summary>Camera scene node in the render-to-texture scene.</summary>
    private Node? _rttCameraNode;
    private Scene? _scene;
    private Node? _cameraNode;
    private Texture2D? _renderTexture;
    private Material? _renderMaterial;
}
