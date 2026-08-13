namespace RedBolt.Sample.CSG;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.CSG;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

internal static class CsgProgram
{
    private static int Main()
    {
        try
        {
            using var application = new CsgShowcase();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt CSG sample finished.");
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
/// Constructive solid geometry example.
/// This sample demonstrates:
///     - Combining model operands with union, difference, and intersection
///     - Rebuilding component-based CSG on the work queue
///     - Preserving source materials on generated geometry
/// </summary>
public class CsgShowcase : Application
{
    protected override void Start()
    {
        base.Start();
        Csg.RegisterCsgLibrary(Context);
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));

        CreateScene();
        SetupViewport();
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    private void CreateScene()
    {
        _scene = new Scene();
        _scene.CreateComponent<Octree>();

        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.25f, 0.25f, 0.3f));

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetCastShadows(true);

        var floorNode = _scene.CreateChild("Floor");
        floorNode.SetPosition(new Vector3f(0.0f, -1.15f, 0.0f));
        floorNode.SetScale(new Vector3f(14.0f, 1.0f, 7.0f));
        var floor = floorNode.CreateComponent<StaticModel>();
        floor.SetModel(Resources.Get<Model>("Models/Plane.mdl"));
        floor.SetMaterial(Resources.Get<Material>("Materials/StoneTiled.xml"));

        CreateCsgResult("Union", new Vector3f(-4.0f, 0.0f, 0.0f), CsgOperation.Union,
            new Vector3f(0.65f, 0.45f, 0.45f));
        CreateCsgResult("Difference", Vector3f.Zero, CsgOperation.DifferenceAB,
            new Vector3f(0.55f, 0.55f, 0.55f));
        CreateCsgResult("Intersection", new Vector3f(4.0f, 0.0f, 0.0f), CsgOperation.Intersection,
            new Vector3f(0.65f, 0.45f, 0.45f));

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.SetPosition(new Vector3f(0.0f, 4.5f, -14.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 0.3f, 0.0f));
        _cameraNode.CreateComponent<Camera>();
        _cameraNode.CreateComponent<FreeFlyController>();
    }

    private void CreateCsgResult(string name, Vector3f position, CsgOperation operation, Vector3f brushPosition)
    {
        var rootNode = _scene!.CreateChild(name);
        rootNode.SetPosition(position);
        var root = rootNode.CreateComponent<CsgRoot>();
        root.SetModel(new ResourceRef(nameof(Model), "Models/Box.mdl"));
        root.SetMaterials(new ResourceRefList(nameof(Material), ["Materials/Stone.xml"]));

        var brushNode = rootNode.CreateChild("Brush");
        brushNode.SetPosition(brushPosition);
        brushNode.SetRotation(new Quaternion(20.0f, 35.0f, 10.0f));
        var brush = brushNode.CreateComponent<CsgBrush>();
        brush.Operation = operation;
        brush.SetModel(new ResourceRef(nameof(Model), "Models/Box.mdl"));
        brush.SetMaterials(new ResourceRefList(nameof(Material), ["Materials/CheckboardProperties.xml"]));

        root.Rebuild();
    }

    private void SetupViewport()
    {
        _viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!);
        Renderer.SetViewport(0, _viewport);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.CSG";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt CSG";
    }

    protected override void Stop()
    {
        Renderer.SetNumViewports(0);
        base.Stop();
    }

    private static void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Constructive Solid Geometry"u8, ref windowOpen,
                ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Union        Difference        Intersection"u8);
            ImGui.TextUnformatted("Use WASD and the right mouse button to move"u8);
        }
        ImGui.End();
    }

    public override void Dispose()
    {
        if (_viewport is not null)
            Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        _viewport?.Dispose();
        _viewport = null;
        base.Dispose();
    }

    private Viewport? _viewport;
    private Scene? _scene;
    private Node? _cameraNode;
}
