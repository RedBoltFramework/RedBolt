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
        "Textures/Texture2D_DXT1.dds", "Textures/Texture2D_DXT3.dds", "Textures/Texture2D_DXT5.dds",
        "Textures/Texture2D_BC4.dds", "Textures/Texture2D_BC5.dds", "Textures/Texture2D_BC6H.dds",
        "Textures/Texture2D_BC7.dds", "Textures/Texture2D_ETC1.dds", "Textures/Texture2D_ETC2_RGB.dds",
        "Textures/Texture2D_ETC2_RGBA.dds", "Textures/Texture2D_PVRTC_RGB_2BPP.dds",
        "Textures/Texture2D_PVRTC_RGBA_2BPP.dds", "Textures/Texture2D_PVRTC_RGB_4BPP.dds",
        "Textures/Texture2D_PVRTC_RGBA_4BPP.dds", "Textures/Texture2D_ASTC_4x4.dds"
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
        var lightNode = _scene.CreateChild("DirectionalLight"); lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f)); lightNode.CreateComponent<Light>().SetLightType(LightType.Directional);

        for (var i = 0; i < TextureNames.Length; ++i)
        {
            var image = cache.Get<Image>(TextureNames[i]);
            var hardwareTexture = cache.Get<Texture2D>(TextureNames[i]);
            var hardwareSupported = image.GetGPUFormat() == hardwareTexture.Format;
            var decompressedImage = image.GetDecompressedImage() ?? image;
            var softwareTexture = new Texture2D(TextureNames[i] + "#decompressed");
            softwareTexture.SetData(decompressedImage);
            CreateBox(0.0f, 2.0f + 2.0f * i, softwareTexture);
            CreateBox(2.0f, 2.0f + 2.0f * i, hardwareTexture);
            _ = hardwareSupported;
        }
        _cameraNode = _scene.CreateChild("Camera"); _cameraNode.CreateComponent<FreeFlyController>(); _cameraNode.CreateComponent<Camera>();
        _cameraNode.SetPosition(new Vector3f(8.0f, 12.0f, -10.0f)); _cameraNode.LookAt(new Vector3f(1.0f, 5.0f, 12.0f));

        void CreateBox(float x, float z, Texture2D texture)
        {
            var material = new Material(string.Empty);
            material.SetTechnique(0, cache.Get<Technique>("Techniques/LitOpaque.xml"));
            material.SetTexture(ShaderResources.Albedo, texture);
            var node = _scene.CreateChild("Box"); node.SetPosition(new Vector3f(x, 1.0f, z));
            var model = node.CreateComponent<StaticModel>(); model.SetModel(cache.Get<Model>("Models/Box.mdl")); model.SetMaterial(material);
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
