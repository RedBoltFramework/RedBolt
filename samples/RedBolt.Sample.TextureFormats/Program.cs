using System;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try { return new TextureFormatsApplication().Run(); }
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }

/// <summary>Texture formats example.</summary>
sealed class TextureFormatsApplication : Application
{
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
    private ContextEventSubscription? _dearImGuiSubscription;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.TextureFormats"; parameters.Borderless = false; parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data"; parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false; parameters.WindowWidth = 1280; parameters.WindowHeight = 720; parameters.WindowTitle = "RedBolt Texture Formats";
    }
    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        CreateScene(); Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
        Input.SetMouseMode(MouseMode.Free); Input.SetMouseVisible(true);
    }
    private void CreateScene()
    {
        var cache = Resources; _scene = new Scene(); _scene.CreateComponent<Octree>();
        var planeNode = _scene.CreateChild("Plane"); planeNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f));
        var plane = planeNode.CreateComponent<StaticModel>(); plane.SetModel(cache.Get<Model>("Models/Plane.mdl")); plane.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));
        var lightNode = _scene.CreateChild("DirectionalLight"); lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f));
        var light = lightNode.CreateComponent<Light>(); light.SetLightType(LightType.Directional); light.SetCastShadows(true);

        _cameraNode = _scene.CreateChild("Camera"); _cameraNode.CreateComponent<Camera>(); _cameraNode.CreateComponent<FreeFlyController>();
        _cameraNode.SetPosition(new Vector3f(0.0f, 3.0f, -6.0f));

        var invalidImage = cache.GetResourceOrNull<Image>("Textures/Formats/Invalid.png");
        for (var i = 0; i < TextureNames.Length; ++i)
        {
            var image = cache.GetResourceOrNull<Image>(TextureNames[i]);
            var hardwareTexture = cache.GetResourceOrNull<Texture2D>(TextureNames[i]);
            var hardwareSupported = image is not null && hardwareTexture is not null
                && image.GetGPUFormat() == hardwareTexture.Format;
            var decompressedImage = image is not null ? image.GetDecompressedImage() : invalidImage;
            if (decompressedImage is not null)
            {
                var softwareTexture = new Texture2D();
                softwareTexture.SetData(decompressedImage);
                CreateBox(0.0f, 2.0f + 2.0f * i, softwareTexture);
            }
            if (hardwareSupported && hardwareTexture is not null)
                CreateBox(2.0f, 2.0f + 2.0f * i, hardwareTexture);
        }

        void CreateBox(float x, float z, Texture2D texture)
        {
            var material = new Material();
            material.SetTechnique(0, cache.Get<Technique>("Techniques/LitOpaque.xml"));
            material.SetTexture(ShaderResources.Albedo, texture);
            var node = _scene.CreateChild("Box"); node.SetPosition(new Vector3f(x, 0.5f, z));
            var model = node.CreateComponent<StaticModel>(); model.SetModel(cache.Get<Model>("Models/Box.mdl")); model.SetMaterial(material); model.SetCastShadows(true);
        }
    }
    protected override void Update(float timeStep) { if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) Exit(); }
    protected override void Stop() { _dearImGuiSubscription?.Dispose(); }
    private static void DrawInstructions()
    {
        if (ImGui.Begin("Texture Formats\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Left: software decompression; right: hardware format\0"u8);
            ImGui.TextUnformatted("Use WASD keys and right mouse button to move\0"u8);
        }
        ImGui.End();
    }
}
