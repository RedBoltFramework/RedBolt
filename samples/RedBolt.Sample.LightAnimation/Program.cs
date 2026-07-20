using System;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try { return new LightAnimationApplication().Run(); }
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }

/// <summary>Light animation example.</summary>
sealed class LightAnimationApplication : Application
{
    private Scene? _scene; private Node? _cameraNode; private ContextEventSubscription? _dearImGuiSubscription; private float _uiTime;
    protected override void Setup(EngineParameters parameters) { parameters.ApplicationName = "RedBolt.Sample.LightAnimation"; parameters.Borderless = false; parameters.FullScreen = false; parameters.ResourcePaths = "CoreData;Data"; parameters.ResourcePrefixPaths = ".;.."; parameters.WindowMaximize = false; parameters.WindowWidth = 1280; parameters.WindowHeight = 720; parameters.WindowTitle = "RedBolt Light Animation"; }
    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        CreateScene(); Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!)); Input.SetMouseMode(MouseMode.Free); Input.SetMouseVisible(true);
    }
    private void CreateScene()
    {
        var cache = Resources; _scene = new Scene(); _scene.CreateComponent<Octree>();
        var planeNode = _scene.CreateChild("Plane"); planeNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f)); var plane = planeNode.CreateComponent<StaticModel>(); plane.SetModel(cache.Get<Model>("Models/Plane.mdl")); plane.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));
        var lightContainer = _scene.CreateChild("PointLightContainer"); var lightNode = lightContainer.CreateChild("PointLight"); var light = lightNode.CreateComponent<Light>(); light.SetLightType(LightType.Point); light.SetRange(10.0f);
        lightNode.CreateComponent<AnimationController>().PlayNew(new AnimationParameters("Animations/LightAnimation.xml").Looped());
        // Built-in Text and Sprite are intentionally replaced by Dear ImGui in managed samples.
        // TODO: port the animated sprite frames to Dear ImGui texture rendering.
        const uint numObjects = 200;
        for (var i = 0u; i < numObjects; ++i) { var node = _scene.CreateChild("Mushroom"); node.SetPosition(new Vector3f(MathDefs.Random(90.0f) - 45.0f, 0.0f, MathDefs.Random(90.0f) - 45.0f)); node.SetRotation(new Quaternion(0.0f, MathDefs.Random(360.0f), 0.0f)); node.SetScale(0.5f + MathDefs.Random(2.0f)); var model = node.CreateComponent<StaticModel>(); model.SetModel(cache.Get<Model>("Models/Mushroom.mdl")); model.SetMaterial(cache.Get<Material>("Materials/Mushroom.xml")); }
        _cameraNode = _scene.CreateChild("Camera"); _cameraNode.CreateComponent<FreeFlyController>(); _cameraNode.CreateComponent<Camera>(); _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, 0.0f));
    }
    protected override void Update(float timeStep) { if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) Exit(); _uiTime = (_uiTime + timeStep) % 4.0f; }
    protected override void Stop() { _dearImGuiSubscription?.Dispose(); }
    private void DrawInstructions()
    {
        if (ImGui.Begin("Light Animation\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Use WASD keys and right mouse button to move\0"u8);
            if (_uiTime < 1.0f) ImGui.TextUnformatted("WHITE\0"u8); else if (_uiTime < 2.0f) ImGui.TextUnformatted("RED\0"u8); else if (_uiTime < 3.0f) ImGui.TextUnformatted("YELLOW\0"u8); else ImGui.TextUnformatted("GREEN\0"u8);
        }
        ImGui.End();
    }
}
