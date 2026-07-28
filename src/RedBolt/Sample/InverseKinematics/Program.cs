namespace RedBolt.Sample.InverseKinematics;

using System;
using System.Collections.Generic;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.IK;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Platform;
using RedBolt.Scene;

internal static class InverseKinematicsProgram
{
    private static int Main()
    {
        try { return new InverseKinematicsApplication().Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

sealed class InverseKinematicsApplication : Application
{
    private readonly List<Node> _handles = [];
    private Scene? _scene;
    private Node? _cameraNode;
    private Octree? _octree;
    private OutlineGroup? _outlineGroup;
    private Node? _draggedHandle;
    private Plane _dragPlane;

    public InverseKinematicsApplication()
    {
        RedBolt.IK.IK.RegisterIKLibrary(Context.Instance);
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.InverseKinematics";
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
        parameters.WindowTitle = "RedBolt Inverse Kinematics";
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>(
            (ref CoreEvents.PostRenderUpdateEvent _) => Renderer.DrawDebugGeometry(false));

        CreateScene();
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));

        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    private void CreateScene()
    {
        var cache = Resources;
        _scene = new Scene();
        _octree = _scene.CreateComponent<Octree>();
        _scene.CreateComponent<DebugRenderer>();
        _outlineGroup = _scene.CreateComponent<OutlineGroup>();
        _outlineGroup.SetColor(Color.Yellow);

        var planeNode = _scene.CreateChild("Plane");
        planeNode.SetScale(new Vector3f(20.0f, 1.0f, 20.0f));
        var plane = planeNode.CreateComponent<StaticModel>();
        plane.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        plane.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));

        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.35f, 0.35f, 0.35f));

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetCastShadows(true);

        var characterNode = _scene.CreateChild("Character");
        characterNode.SetRotation(new Quaternion(180.0f, Vector3f.Up));
        var character = characterNode.CreateComponent<AnimatedModel>();
        character.SetModel(cache.Get<Model>("Models/Mutant/Mutant.mdl"));
        character.SetMaterial(cache.Get<Material>("Models/Mutant/Materials/mutant_M.xml"));
        character.SetCastShadows(true);
        characterNode.CreateComponent<IKSolver>();

        CreateLimb(characterNode, "Mutant:LeftShoulder", "Mutant:LeftArm", "Mutant:LeftForeArm",
            "LeftHandTarget", "LeftElbowTarget");
        CreateLimb(characterNode, "Mutant:RightArm", "Mutant:RightForeArm", "Mutant:RightHand",
            "RightHandTarget", "RightElbowTarget");
        CreateLimb(characterNode, "Mutant:LeftUpLeg", "Mutant:LeftLeg", "Mutant:LeftFoot",
            "LeftFootTarget", "LeftKneeTarget");
        CreateLimb(characterNode, "Mutant:RightUpLeg", "Mutant:RightLeg", "Mutant:RightFoot",
            "RightFootTarget", "RightKneeTarget");

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.SetPosition(new Vector3f(0.0f, 1.8f, -5.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 1.1f, 0.0f));
        _cameraNode.CreateComponent<FreeFlyController>();
        _cameraNode.CreateComponent<Camera>().SetFarClip(100.0f);
    }

    private void CreateLimb(
        Node characterNode,
        string firstBoneName,
        string secondBoneName,
        string thirdBoneName,
        string targetName,
        string bendTargetName)
    {
        var secondBone = characterNode.GetChild(secondBoneName, true)!;
        var thirdBone = characterNode.GetChild(thirdBoneName, true)!;
        CreateHandle(characterNode, targetName, thirdBone.WorldPosition);
        CreateHandle(characterNode, bendTargetName, secondBone.WorldPosition);

        var solver = characterNode.CreateComponent<IKLimbSolver>();
        solver.FirstBoneName = firstBoneName;
        solver.SecondBoneName = secondBoneName;
        solver.ThirdBoneName = thirdBoneName;
        solver.TargetName = targetName;
        solver.BendTargetName = bendTargetName;
    }

    private void CreateHandle(Node characterNode, string name, Vector3f worldPosition)
    {
        var handleNode = characterNode.CreateChild(name);
        handleNode.SetWorldPosition(worldPosition);
        handleNode.SetScale(0.16f);
        var handle = handleNode.CreateComponent<StaticModel>();
        handle.SetModel(Resources.Get<Model>("Models/Sphere.mdl"));
        handle.SetMaterial(Resources.Get<Material>("Materials/DefaultWhite.xml"));
        _handles.Add(handleNode);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();

        if (_draggedHandle is null && Input.GetMouseButtonPress(MouseButton.Left)
            && !ImGui.GetIO().WantCaptureMouse)
            BeginDrag();

        if (_draggedHandle is not null)
        {
            if (!Input.GetMouseButtonDown(MouseButton.Left))
                EndDrag();
            else
                UpdateDrag();
        }
    }

    private void BeginDrag()
    {
        var camera = _cameraNode!.GetComponent<Camera>()!;
        var ray = camera.GetScreenRayFromMouse();
        using var query = new RayOctreeQuery(ray, RayQueryLevel.RayTriangle, 100.0f, DrawableFlags.Geometry);
        _octree!.Raycast(query);
        Node? node = null;
        for (var i = 0; i < query.Result.Count; ++i)
        {
            var candidate = query.Result[i].Drawable?.Node;
            if (candidate is not null && _handles.Contains(candidate))
            {
                node = candidate;
                break;
            }
        }
        if (node is null)
            return;

        _draggedHandle = node;
        _dragPlane = new Plane(_cameraNode.WorldDirection, node.WorldPosition);
        _outlineGroup!.ClearDrawables();
        _outlineGroup.AddDrawable(node.GetComponent<StaticModel>()!);
    }

    private void UpdateDrag()
    {
        var ray = _cameraNode!.GetComponent<Camera>()!.GetScreenRayFromMouse();
        var distance = ray.HitDistance(_dragPlane);
        if (!float.IsPositiveInfinity(distance))
            _draggedHandle!.SetWorldPosition(ray.Origin + ray.Direction * distance);
    }

    private void EndDrag()
    {
        _draggedHandle = null;
        _outlineGroup!.ClearDrawables();
    }

    protected override void Stop()
    {
        _outlineGroup?.ClearDrawables();
        _handles.Clear();
        _scene?.Dispose();
        _scene = null;
        _cameraNode = null;
        _octree = null;
        _outlineGroup = null;
        _draggedHandle = null;
    }

    private static void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Inverse Kinematics\0"u8, ref windowOpen,
            ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Drag the joint spheres with the left mouse button\0"u8);
            ImGui.TextUnformatted("Use WASD and the right mouse button to move the camera\0"u8);
        }
        ImGui.End();
    }
}
