using System;
using System.IO;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try { return new DecalsApplication().Run(); }
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }

/// <summary>Decals example.</summary>
sealed class DecalsApplication : Application
{
    private Scene? _scene; private Node? _cameraNode; private Octree? _octree; private bool _drawDebug; private ContextEventSubscription? _dearImGuiSubscription;
    protected override void Setup(EngineParameters parameters) { parameters.ApplicationName = "RedBolt.Sample.Decals"; parameters.Borderless = false; parameters.FullScreen = false; parameters.ResourcePaths = "CoreData;Data"; parameters.ResourcePrefixPaths = FindResourcePrefixPath(); parameters.WindowMaximize = false; parameters.WindowWidth = 1280; parameters.WindowHeight = 720; parameters.WindowTitle = "RedBolt Decals"; }
    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        CreateScene(); Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!)); Input.SetMouseMode(MouseMode.Free); Input.SetMouseVisible(true);
    }
    private void CreateScene()
    {
        var cache = Resources; _scene = new Scene(); _octree = _scene.CreateComponent<Octree>(); _scene.CreateComponent<DebugRenderer>();
        var planeNode = _scene.CreateChild("Plane"); planeNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f)); var plane = planeNode.CreateComponent<StaticModel>(); plane.SetModel(cache.Get<Model>("Models/Plane.mdl")); plane.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));
        var zoneNode = _scene.CreateChild("Zone"); var zone = zoneNode.CreateComponent<Zone>(); zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f)); zone.SetAmbientColor(new Color(0.15f, 0.15f, 0.15f)); zone.SetFogColor(new Color(0.5f, 0.5f, 0.7f)); zone.SetFogStart(100.0f); zone.SetFogEnd(300.0f);
        var lightNode = _scene.CreateChild("DirectionalLight"); lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f)); var light = lightNode.CreateComponent<Light>(); light.SetLightType(LightType.Directional); light.SetCastShadows(true); light.SetShadowBias(new BiasParameters(0.00025f, 0.5f)); light.SetShadowCascade(new CascadeParameters(10.0f, 50.0f, 200.0f, 0.0f, 0.8f));
        for (var i = 0; i < 240; ++i) { var node = _scene.CreateChild("Mushroom"); node.SetPosition(new Vector3f(MathDefs.Random(90.0f) - 45.0f, 0.0f, MathDefs.Random(90.0f) - 45.0f)); node.SetRotation(Quaternion.FromEulerAngles(0.0f, MathDefs.Random(360.0f), 0.0f)); node.SetScale(0.5f + MathDefs.Random(2.0f)); var model = node.CreateComponent<StaticModel>(); model.SetModel(cache.Get<Model>("Models/Mushroom.mdl")); model.SetMaterial(cache.Get<Material>("Materials/Mushroom.xml")); model.SetCastShadows(true); }
        for (var i = 0; i < 20; ++i) { var size = 1.0f + MathDefs.Random(10.0f); var node = _scene.CreateChild("Box"); node.SetPosition(new Vector3f(MathDefs.Random(80.0f) - 40.0f, size * 0.5f, MathDefs.Random(80.0f) - 40.0f)); node.SetScale(size); var model = node.CreateComponent<StaticModel>(); model.SetModel(cache.Get<Model>("Models/Box.mdl")); model.SetMaterial(cache.Get<Material>("Materials/Stone.xml")); model.SetCastShadows(true); if (size >= 3.0f) model.SetOccluder(true); }
        _cameraNode = _scene.CreateChild("Camera"); _cameraNode.CreateComponent<FreeFlyController>(); _cameraNode.CreateComponent<Camera>().SetFarClip(300.0f); _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, 0.0f));
    }
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) Exit();
        if (Input.GetKeyPress(Key.Space)) _drawDebug = !_drawDebug;
        if (!Input.GetMouseButtonDown(MouseButton.Right) && Input.GetMouseButtonPress(MouseButton.Left)) PaintDecal();
        // TODO: port Renderer::DrawDebugGeometry(false) when the managed renderer exposes it.
    }
    private void PaintDecal()
    {
        var ray = _cameraNode!.GetComponent<Camera>()!.GetScreenRayFromMouse();
        var result = _octree!.RaycastSingle(ray, RayQueryLevel.RayTriangle, 250.0f, DrawableFlags.Geometry);
        if (result?.Drawable is null) return;
        var targetNode = result.Drawable.Node;
        var decal = targetNode.GetComponent<DecalSet>();
        if (decal is null) { decal = targetNode.CreateComponent<DecalSet>(); decal.SetMaterial(Resources.Get<Material>("Materials/UrhoDecal.xml")); }
        decal.AddDecal(result.Drawable, result.Position, _cameraNode.Rotation, 0.5f, 1.0f, 1.0f, Vector2f.Zero, Vector2f.One);
    }
    protected override void Stop() { _dearImGuiSubscription?.Dispose(); }
    private void DrawInstructions() { if (ImGui.Begin("Decals\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings)) { ImGui.TextUnformatted("Use WASD keys to move\0"u8); ImGui.TextUnformatted("LMB to paint decals, RMB to rotate view\0"u8); ImGui.TextUnformatted(_drawDebug ? "Debug geometry requested\0"u8 : "Space to toggle debug geometry\0"u8); } ImGui.End(); }
    private static string FindResourcePrefixPath() { var root = Environment.GetEnvironmentVariable("REDBOLT_RBFX_BIN_DIR"); if (Valid(root, out var result)) return result; for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent) { foreach (var path in new[] { Path.Combine(current.FullName, "bin"), Path.Combine(current.FullName, "tests", "RedBolt.Tests", "TestData", "rbfx"), Path.Combine(current.FullName, "rbfx", "bin"), Path.Combine(current.FullName, "..", "rbfx", "bin") }) if (Valid(path, out result)) return result; if (Directory.Exists(Path.Combine(current.FullName, ".git"))) break; } throw new InvalidOperationException("Decals sample could not locate rbfx CoreData/Data resource directories."); }
    private static bool Valid(string? root, out string result) { result = string.Empty; if (string.IsNullOrWhiteSpace(root)) return false; var path = Path.GetFullPath(root); if (!Directory.Exists(Path.Combine(path, "CoreData")) || !Directory.Exists(Path.Combine(path, "Data"))) return false; result = path; return true; }
}
