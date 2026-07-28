namespace RedBolt.Sample.TextureFormats;

using System;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;

internal static class TextureFormatsProgram
{
    private static int Main()
    {
        try
        {
            return new TextureFormatsSample().Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>
/// Sample that demonstrates different texture formats.
/// </summary>
public class TextureFormatsSample : Application
{
    /// <summary>
    /// Construct.
    /// </summary>
    public TextureFormatsSample()
    {
    }

    /// <summary>
    /// Setup after engine initialization and before running the main loop.
    /// </summary>
    protected override void Start()
    {
        base.Start();

        CreateScene();
        CreateInstructions();
        SetupViewport();

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
        _scene.CreateComponent<Octree>();

        // Create ground plane
        var planeNode = _scene.CreateChild("Plane");
        planeNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f));
        var planeObject = planeNode.CreateComponent<StaticModel>();
        planeObject.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        planeObject.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));

        // Create directional light
        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetCastShadows(true);

        // Create camera.
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<Camera>();
        _cameraNode.CreateComponent<FreeFlyController>();
        _cameraNode.SetPosition(new Vector3f(0.0f, 3.0f, -6.0f));

        // Create labels for textured objects.
        var softwareLabel = CreateLabel("Supported\non Software");
        softwareLabel.SetPosition(new Vector3f(0.0f, 0.5f, 0.0f));
        softwareLabel.SetRotation(new Quaternion(45.0f, Vector3f.Right));

        var hardwareLabel = CreateLabel("Supported\non Hardware");
        hardwareLabel.SetPosition(new Vector3f(2.0f, 0.5f, 0.0f));
        hardwareLabel.SetRotation(new Quaternion(45.0f, Vector3f.Right));

        // Create textured objects.
        var index = 0;
        var invalidImage = cache.GetResourceOrNull<Image>("Textures/Formats/Invalid.png");
        foreach (var textureName in TextureNames)
        {
            var image = cache.GetResourceOrNull<Image>(textureName);
            var texture = cache.GetResourceOrNull<Texture2D>(textureName);
            var isHardwareSupported = image is not null && texture is not null
                && image.GetGPUFormat() == texture.Format;

            var decompressedImage = image is not null ? image.GetDecompressedImage() : invalidImage;

            if (decompressedImage is not null)
            {
                var decompressedTexture = new Texture2D();
                decompressedTexture.SetData(decompressedImage);

                var softwareBox = CreateTexturedBox(decompressedTexture);
                softwareBox.SetPosition(new Vector3f(0.0f, 0.5f, 2.0f + 2.0f * index));
            }

            if (isHardwareSupported)
            {
                var hardwareBox = CreateTexturedBox(texture!);
                hardwareBox.SetPosition(new Vector3f(2.0f, 0.5f, 2.0f + 2.0f * index));
            }

            ++index;
        }
    }

    /// <summary>
    /// Create box with texture.
    /// </summary>
    private Node CreateTexturedBox(Texture2D texture)
    {
        var cache = Resources;

        var material = new Material();
        material.SetTechnique(0, cache.Get<Technique>("Techniques/LitOpaque.xml"));
        material.SetTexture(ShaderResources.Albedo, texture);

        var node = _scene!.CreateChild("Box");
        var staticModel = node.CreateComponent<StaticModel>();
        staticModel.SetModel(cache.Get<Model>("Models/Box.mdl"));
        staticModel.SetMaterial(material);
        staticModel.SetCastShadows(true);

        return node;
    }

    /// <summary>
    /// Create text label in 3D.
    /// </summary>
    private Node CreateLabel(string text)
    {
        // auto* cache = GetSubsystem<ResourceCache>();
        var node = _scene!.CreateChild("Text3D");

        // TODO: port Text3D when the excluded built-in rbfx UI surface is available.
        // auto* text3D = node->CreateComponent<Text3D>();
        // text3D->SetText(text);
        // text3D->SetFont(cache->GetResource<Font>("Fonts/Anonymous Pro.sdf"), 24);
        // text3D->SetColor(Color::WHITE);
        // text3D->SetAlignment(HA_CENTER, VA_CENTER);
        // text3D->SetFaceCameraMode(FC_NONE);

        return node;
    }

    /// <summary>
    /// Construct an instruction text to the UI.
    /// </summary>
    private void CreateInstructions()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
    }

    /// <summary>
    /// Set up a viewport for displaying the scene.
    /// </summary>
    private void SetupViewport()
    {
        var viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!);
        Renderer.SetViewport(0, viewport);
    }

    private static void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Texture Formats\0"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Supported on Software: left\0"u8);
            ImGui.TextUnformatted("Supported on Hardware: right\0"u8);
            ImGui.TextUnformatted("Use WASD keys and right mouse button to move\0"u8);
        }
        ImGui.End();
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.TextureFormats";
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
        parameters.WindowTitle = "RedBolt Texture Formats";
    }

    protected override void Stop()
    {
        Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Stop();
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

    private static readonly string[] TextureNames =
    [
        "Textures/Formats/DXT1.dds",
        "Textures/Formats/DXT3.dds",
        "Textures/Formats/DXT5.dds",
        "Textures/Formats/ETC1.dds",
        "Textures/Formats/ETC2.dds",
        "Textures/Formats/PTC2.dds",
        "Textures/Formats/PTC4.dds",

        "Textures/Formats/RGBA.dds",
        "Textures/Formats/RGBE.dds",
        "Textures/Formats/RGBA16.dds",
        "Textures/Formats/RGBA_F16.dds",
        "Textures/Formats/RGBA_F32.dds",
        "Textures/Formats/RGB10A2.dds",
        "Textures/Formats/BGR5A1.dds",
        "Textures/Formats/BGR565.dds"
    ];

    private Scene? _scene;
    private Node? _cameraNode;
}
