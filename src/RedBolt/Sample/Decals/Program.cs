namespace RedBolt.Sample.Decals;

using System;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Platform;
using RedBolt.Scene;

internal static class DecalsProgram
{
    private static int Main()
    {
        try { return new DecalsApplication().Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

/// <summary>Decals example.</summary>
sealed class DecalsApplication : Application
{
    private Scene? _scene; private Node? _cameraNode; private Octree? _octree; private bool _drawDebug;
    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Decals"; parameters.Borderless = false; parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data"; parameters.ResourcePrefixPaths = ".;.."; parameters.WindowMaximize = false;
        if (ProcessUtils.GetPlatform() != PlatformId.Web)
        {
            parameters.WindowWidth = 1280; parameters.WindowHeight = 720;
        }
        parameters.WindowTitle = "RedBolt Decals";
    }
    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        CreateScene(); Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!)); SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>((ref CoreEvents.PostRenderUpdateEvent _) => HandlePostRenderUpdate()); Input.SetMouseMode(MouseMode.Free); Input.SetMouseVisible(true);
    }
    private void CreateScene()
    {
        var cache = Resources; _scene = new Scene(); _octree = _scene.CreateComponent<Octree>(); _scene.CreateComponent<DebugRenderer>();
        var planeNode = _scene.CreateChild("Plane"); planeNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f)); var plane = planeNode.CreateComponent<StaticModel>(); plane.SetModel(cache.Get<Model>("Models/Plane.mdl")); plane.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));
        var zoneNode = _scene.CreateChild("Zone"); var zone = zoneNode.CreateComponent<Zone>(); zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f)); zone.SetAmbientColor(new Color(0.15f, 0.15f, 0.15f)); zone.SetFogColor(new Color(0.5f, 0.5f, 0.7f)); zone.SetFogStart(100.0f); zone.SetFogEnd(300.0f);
        var lightNode = _scene.CreateChild("DirectionalLight"); lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f)); var light = lightNode.CreateComponent<Light>(); light.SetLightType(LightType.Directional); light.SetCastShadows(true); light.SetShadowBias(new BiasParameters(0.00025f, 0.5f)); light.SetShadowCascade(new CascadeParameters(10.0f, 50.0f, 200.0f, 0.0f, 0.8f));
        for (var i = 0; i < 240; ++i) { var node = _scene.CreateChild("Mushroom"); node.SetPosition(new Vector3f(MathDefs.Random(90.0f) - 45.0f, 0.0f, MathDefs.Random(90.0f) - 45.0f)); node.SetRotation(new Quaternion(0.0f, MathDefs.Random(360.0f), 0.0f)); node.SetScale(0.5f + MathDefs.Random(2.0f)); var model = node.CreateComponent<StaticModel>(); model.SetModel(cache.Get<Model>("Models/Mushroom.mdl")); model.SetMaterial(cache.Get<Material>("Materials/Mushroom.xml")); model.SetCastShadows(true); }
        for (var i = 0; i < 20; ++i) { var size = 1.0f + MathDefs.Random(10.0f); var node = _scene.CreateChild("Box"); node.SetPosition(new Vector3f(MathDefs.Random(80.0f) - 40.0f, size * 0.5f, MathDefs.Random(80.0f) - 40.0f)); node.SetScale(size); var model = node.CreateComponent<StaticModel>(); model.SetModel(cache.Get<Model>("Models/Box.mdl")); model.SetMaterial(cache.Get<Material>("Materials/Stone.xml")); model.SetCastShadows(true); if (size >= 3.0f) model.SetOccluder(true); }
        _cameraNode = _scene.CreateChild("Camera"); _cameraNode.CreateComponent<FreeFlyController>(); _cameraNode.CreateComponent<Camera>().SetFarClip(300.0f); _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, 0.0f));
    }
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) Exit();
        if (Input.GetKeyPress(Key.Space)) _drawDebug = !_drawDebug;
        if (!Input.GetMouseButtonDown(MouseButton.Right) && Input.GetMouseButtonPress(MouseButton.Left)) PaintDecal();
    }
    private void HandlePostRenderUpdate()
    {
        // If draw debug mode is enabled, draw viewport debug geometry. Disable depth test so that we can see the effect of occlusion
        if (_drawDebug)
            Renderer.DrawDebugGeometry(false);
    }
    private void PaintDecal()
    {
        var ray = _cameraNode!.GetComponent<Camera>()!.GetScreenRayFromMouse();
        using var query = new RayOctreeQuery(ray, RayQueryLevel.RayTriangle, 250.0f, DrawableFlags.Geometry);
        _octree!.RaycastSingle(query);
        if (query.Result.Count == 0) return;
        var result = query.Result[0];
        var hitDrawable = result.Drawable!;
        var targetNode = hitDrawable.Node!;
        var decal = targetNode.GetComponent<DecalSet>();
        if (decal is null) { decal = targetNode.CreateComponent<DecalSet>(); decal.SetMaterial(Resources.Get<Material>("Materials/UrhoDecal.xml")); }
        decal.AddDecal(hitDrawable, result.Position, _cameraNode.Rotation, 0.5f, 1.0f, 1.0f, Vector2f.Zero, Vector2f.One);
    }
    private void DrawInstructions() { var windowOpen = true; if (ImGui.Begin("Decals"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings)) { ImGui.TextUnformatted("Use WASD keys to move"u8); ImGui.TextUnformatted("LMB to paint decals, RMB to rotate view"u8); ImGui.TextUnformatted(_drawDebug ? "Debug geometry requested"u8 : "Space to toggle debug geometry"u8); } ImGui.End(); }
}
