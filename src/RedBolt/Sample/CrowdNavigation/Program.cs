//
// Copyright (c) 2008-2022 the Urho3D project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Sample.CrowdNavigation;

using System;
using System.Collections.Generic;
using System.Linq;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Navigation;
using RedBolt.Platform;
using RedBolt.Resource;
using RedBolt.Scene;

internal static class CrowdNavigationProgram
{
    private static int Main()
    {
        try
        {
            using var application = new CrowdNavigationSample();
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

/// <summary>Crowd navigation example.</summary>
public sealed class CrowdNavigationSample : Application
{
    private const int StreamingDistance = 2;
    private const string WalkingAnimation = "Models/Jack_Walk.ani";
    private Scene? _scene;
    private Node? _cameraNode;
    private Octree? _octree;
    private Viewport? _viewport;
    private readonly Dictionary<IntVector2, byte[]> _tileData = [];
    private readonly HashSet<IntVector2> _addedTiles = [];
    private bool _drawDebug;
    private bool _useStreaming;
    private bool _showInstructions = true;

    protected override void Start()
    {
        base.Start();
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        CreateScene();
        _viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!);
        Renderer.SetViewport(0, _viewport);
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>(
            (ref CoreEvents.PostRenderUpdateEvent _) => HandlePostRenderUpdate());
        SubscribeToEvent<NavigationEvents.CrowdAgentFailureEvent>(HandleCrowdAgentFailure);
        SubscribeToEvent<NavigationEvents.CrowdAgentRepositionEvent>(HandleCrowdAgentReposition);
        SubscribeToEvent<NavigationEvents.CrowdAgentFormationEvent>(HandleCrowdAgentFormation);
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    private void CreateScene()
    {
        var cache = Resources;
        _scene = new Scene();
        _octree = _scene.CreateComponent<Octree>();
        _scene.CreateComponent<DebugRenderer>();

        var planeNode = _scene.CreateChild("Plane");
        planeNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f));
        var planeObject = planeNode.CreateComponent<StaticModel>();
        planeObject.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        planeObject.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));

        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.15f, 0.15f, 0.15f));
        zone.SetFogColor(new Color(0.5f, 0.5f, 0.7f));
        zone.SetFogStart(100.0f);
        zone.SetFogEnd(300.0f);

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetCastShadows(true);
        light.SetShadowBias(new BiasParameters(0.00025f, 0.5f));
        light.SetShadowCascade(new CascadeParameters(10.0f, 50.0f, 200.0f, 0.0f, 0.8f));

        var boxGroup = _scene.CreateChild("Boxes");
        for (var i = 0; i < 20; ++i)
        {
            var boxNode = boxGroup.CreateChild("Box");
            var size = 1.0f + MathDefs.Random(10.0f);
            boxNode.SetPosition(new Vector3f(MathDefs.Random(80.0f) - 40.0f, size * 0.5f, MathDefs.Random(80.0f) - 40.0f));
            boxNode.SetScale(size);
            var boxObject = boxNode.CreateComponent<StaticModel>();
            boxObject.SetModel(cache.Get<Model>("Models/Box.mdl"));
            boxObject.SetMaterial(cache.Get<Material>("Materials/Stone.xml"));
            boxObject.SetCastShadows(true);
            if (size >= 3.0f)
                boxObject.SetOccluder(true);
        }

        var navMesh = _scene.CreateComponent<DynamicNavigationMesh>();
        navMesh.TileSize = 30;
        navMesh.DrawObstacles = true;
        navMesh.DrawOffMeshConnections = true;
        navMesh.AgentHeight = 10.0f;
        navMesh.CellHeight = 0.05f;
        _scene.CreateComponent<Navigable>();
        navMesh.Padding = new Vector3f(0.0f, 10.0f, 0.0f);
        navMesh.Rebuild();
        CreateBoxOffMeshConnections(navMesh, boxGroup);

        for (var i = 0; i < 100; ++i)
            CreateMushroom(new Vector3f(MathDefs.Random(90.0f) - 45.0f, 0.0f, MathDefs.Random(90.0f) - 45.0f));

        var crowdManager = _scene.CreateComponent<CrowdManager>();
        var parameters = crowdManager.GetObstacleAvoidanceParams(0);
        parameters.VelBias = 0.5f;
        parameters.AdaptiveDivs = 7;
        parameters.AdaptiveRings = 3;
        parameters.AdaptiveDepth = 3;
        crowdManager.SetObstacleAvoidanceParams(0, parameters);

        CreateMovingBarrels(navMesh);
        SpawnJack(new Vector3f(-5.0f, 0.0f, 20.0f), _scene.CreateChild("Jacks"));

        _cameraNode = _scene.CreateChild("Camera");
        var controller = _cameraNode.CreateComponent<FreeFlyController>();
        controller.SetSpeed(20.0f);
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(300.0f);
        _cameraNode.SetPosition(new Vector3f(0.0f, 50.0f, 0.0f));
        _cameraNode.SetRotation(new Quaternion(80.0f, 0.0f, 0.0f));
    }

    private void SpawnJack(Vector3f position, Node jackGroup)
    {
        var jackNode = jackGroup.CreateChild("Jack");
        jackNode.SetPosition(position);
        var modelObject = jackNode.CreateComponent<AnimatedModel>();
        modelObject.SetModel(Resources.Get<Model>("Models/Jack.mdl"));
        modelObject.SetCastShadows(true);
        jackNode.CreateComponent<AnimationController>();
        var agent = jackNode.CreateComponent<CrowdAgent>();
        agent.Height = 2.0f;
        agent.MaxSpeed = 3.0f;
        agent.MaxAccel = 5.0f;
    }

    private void CreateMushroom(Vector3f position)
    {
        var mushroomNode = _scene!.CreateChild("Mushroom");
        mushroomNode.SetPosition(position);
        mushroomNode.SetRotation(new Quaternion(0.0f, MathDefs.Random(360.0f), 0.0f));
        mushroomNode.SetScale(2.0f + MathDefs.Random(0.5f));
        var model = mushroomNode.CreateComponent<StaticModel>();
        model.SetModel(Resources.Get<Model>("Models/Mushroom.mdl"));
        model.SetMaterial(Resources.Get<Material>("Materials/Mushroom.xml"));
        model.SetCastShadows(true);
        var obstacle = mushroomNode.CreateComponent<Obstacle>();
        obstacle.Radius = mushroomNode.Scale.X;
        obstacle.Height = mushroomNode.Scale.Y;
    }

    private static void CreateBoxOffMeshConnections(DynamicNavigationMesh navMesh, Node boxGroup)
    {
        foreach (var box in boxGroup.GetChildren())
        {
            var boxPosition = box.Position;
            var boxHalfSize = box.Scale.X / 2.0f;
            var connectionStart = box.CreateChild("ConnectionStart");
            connectionStart.SetWorldPosition(navMesh.FindNearestPoint(
                boxPosition + new Vector3f(boxHalfSize, -boxHalfSize, 0.0f)));
            var connectionEnd = connectionStart.CreateChild("ConnectionEnd");
            connectionEnd.SetWorldPosition(navMesh.FindNearestPoint(
                boxPosition + new Vector3f(boxHalfSize, boxHalfSize, 0.0f)));
            connectionStart.CreateComponent<OffMeshConnection>().EndPoint = connectionEnd;
        }
    }

    private void CreateMovingBarrels(DynamicNavigationMesh navMesh)
    {
        var barrel = _scene!.CreateChild("Barrel");
        var model = barrel.CreateComponent<StaticModel>();
        model.SetModel(Resources.Get<Model>("Models/Cylinder.mdl"));
        model.SetMaterial(Resources.Get<Material>("Materials/StoneTiled.xml"));
        model.SetCastShadows(true);
        for (var i = 0; i < 20; ++i)
        {
            var clone = barrel.Clone()!;
            var size = 0.5f + MathDefs.Random(1.0f);
            clone.SetScale(new Vector3f(size / 1.5f, size * 2.0f, size / 1.5f));
            clone.SetPosition(navMesh.FindNearestPoint(new Vector3f(
                MathDefs.Random(80.0f) - 40.0f, size * 0.5f, MathDefs.Random(80.0f) - 40.0f)));
            var agent = clone.CreateComponent<CrowdAgent>();
            agent.Radius = clone.Scale.X * 0.5f;
            agent.Height = size;
            agent.NavigationQuality = NavigationQuality.Low;
        }
        barrel.Remove();
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }
        if (!Input.GetMouseButtonDown(MouseButton.Right) && Input.GetMouseButtonPress(MouseButton.Left))
            SetPathPoint(Input.GetKeyDown(Key.Shift));
        else if (!Input.GetMouseButtonDown(MouseButton.Right)
            && (Input.GetMouseButtonPress(MouseButton.Middle) || Input.GetKeyPress(Key.O)))
            AddOrRemoveObject();
        else if (Input.GetKeyPress(Key.Space))
            _drawDebug = !_drawDebug;
        else if (Input.GetKeyPress(Key.F12))
            _showInstructions = !_showInstructions;

        if (Input.GetKeyPress(Key.Tab))
        {
            _useStreaming = !_useStreaming;
            ToggleStreaming(_useStreaming);
        }
        if (_useStreaming)
            UpdateStreaming();
    }

    private void SetPathPoint(bool spawning)
    {
        if (!Raycast(250.0f, out var hitPosition, out _))
            return;
        var navMesh = _scene!.GetComponent<DynamicNavigationMesh>()!;
        var pathPosition = navMesh.FindNearestPoint(hitPosition, Vector3f.One);
        var jackGroup = _scene.GetChild("Jacks")!;
        if (spawning)
            SpawnJack(pathPosition, jackGroup);
        else
            _scene.GetComponent<CrowdManager>()!.SetCrowdTarget(pathPosition, jackGroup);
    }

    private void AddOrRemoveObject()
    {
        if (!Raycast(250.0f, out var hitPosition, out var hitDrawable))
            return;
        var hitNode = hitDrawable!.Node!;
        if (hitNode.Name is "Mushroom" or "Jack")
            hitNode.Remove();
        else
            CreateMushroom(hitPosition);
    }

    private bool Raycast(float maxDistance, out Vector3f hitPosition, out Drawable? hitDrawable)
    {
        hitPosition = Vector3f.Zero;
        hitDrawable = null;
        var ray = _cameraNode!.GetComponent<Camera>()!.GetScreenRayFromMouse();
        using var query = new RayOctreeQuery(ray, RayQueryLevel.RayTriangle, maxDistance, DrawableFlags.Geometry);
        _octree!.RaycastSingle(query);
        if (query.Result.Count == 0)
            return false;
        hitPosition = query.Result[0].Position;
        hitDrawable = query.Result[0].Drawable;
        return true;
    }

    private void ToggleStreaming(bool enabled)
    {
        var navMesh = _scene!.GetComponent<DynamicNavigationMesh>()!;
        if (enabled)
        {
            SaveNavigationData();
            navMesh.Allocate();
        }
        else
            navMesh.Rebuild();
    }

    private void UpdateStreaming()
    {
        var averagePosition = Vector3f.Zero;
        var jackGroup = _scene!.GetChild("Jacks");
        if (jackGroup is not null)
        {
            var jacks = jackGroup.GetChildren();
            for (var i = 0; i < jacks.Count; ++i)
                averagePosition += jacks[i].WorldPosition;
            if (jacks.Count != 0)
                averagePosition /= jacks.Count;
        }
        var navMesh = _scene.GetComponent<DynamicNavigationMesh>()!;
        var jackTile = navMesh.GetTileIndex(averagePosition);
        var beginTile = jackTile - IntVector2.One * StreamingDistance;
        var endTile = jackTile + IntVector2.One * StreamingDistance;
        foreach (var tileIndex in _addedTiles.ToArray())
        {
            if (beginTile.X <= tileIndex.X && tileIndex.X <= endTile.X
                && beginTile.Y <= tileIndex.Y && tileIndex.Y <= endTile.Y)
                continue;
            navMesh.RemoveTile(tileIndex);
            _addedTiles.Remove(tileIndex);
        }
        for (var z = beginTile.Y; z <= endTile.Y; ++z)
        {
            for (var x = beginTile.X; x <= endTile.X; ++x)
            {
                var tileIndex = new IntVector2(x, z);
                if (!navMesh.HasTile(tileIndex) && _tileData.TryGetValue(tileIndex, out var data))
                {
                    _addedTiles.Add(tileIndex);
                    navMesh.AddTile(data);
                }
            }
        }
    }

    private void SaveNavigationData()
    {
        var navMesh = _scene!.GetComponent<DynamicNavigationMesh>()!;
        _tileData.Clear();
        _addedTiles.Clear();
        foreach (var tileIndex in navMesh.GetAllTileIndices())
            _tileData[tileIndex] = navMesh.GetTileData(tileIndex);
    }

    private void HandlePostRenderUpdate()
    {
        if (!_drawDebug)
            return;
        _scene!.GetComponent<DynamicNavigationMesh>()!.DrawDebugGeometry(true);
        _scene.GetComponent<CrowdManager>()!.DrawDebugGeometry(true);
    }

    private void HandleCrowdAgentFailure(ref NavigationEvents.CrowdAgentFailureEvent eventData)
    {
        if (eventData.CrowdAgentState == CrowdAgentState.Invalid)
        {
            var newPosition = _scene!.GetComponent<DynamicNavigationMesh>()!.FindNearestPoint(
                eventData.Node.Position, new Vector3f(5.0f, 5.0f, 5.0f));
            eventData.Node.SetPosition(newPosition);
        }
    }

    private static void HandleCrowdAgentReposition(ref NavigationEvents.CrowdAgentRepositionEvent eventData)
    {
        var animationController = eventData.Node.GetComponent<AnimationController>();
        if (animationController is null)
            return;
        var speed = eventData.Velocity.Length;
        if (animationController.IsPlaying(WalkingAnimation))
        {
            var speedRatio = speed / eventData.CrowdAgent.MaxSpeed;
            eventData.Node.SetRotation(eventData.Node.Rotation.Slerp(
                new Quaternion(Vector3f.Forward, eventData.Velocity), 10.0f * eventData.TimeStep * speedRatio));
            animationController.SetSpeed(WalkingAnimation, speedRatio * 1.5f);
        }
        else
            animationController.Play(WalkingAnimation, 0, true, 0.1f);
        if (speed < eventData.CrowdAgent.Radius)
            animationController.Stop(WalkingAnimation, 0.5f);
    }

    private void HandleCrowdAgentFormation(ref NavigationEvents.CrowdAgentFormationEvent eventData)
    {
        if (eventData.Index != 0)
        {
            eventData.Position = _scene!.GetComponent<CrowdManager>()!.GetRandomPointInCircle(
                eventData.Position, eventData.CrowdAgent.Radius, checked((int)eventData.CrowdAgent.QueryFilterType));
        }
    }

    private void DrawInstructions()
    {
        if (!_showInstructions)
            return;
        var open = true;
        if (ImGui.Begin("Crowd Navigation"u8, ref open, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Use WASD keys to move, RMB to rotate view"u8);
            ImGui.TextUnformatted("LMB sets destination, SHIFT+LMB spawns a Jack"u8);
            ImGui.TextUnformatted("MMB or O adds/removes obstacles or agents"u8);
            ImGui.TextUnformatted("Tab toggles streaming; Space toggles debug; F12 toggles help"u8);
        }
        ImGui.End();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.CrowdNavigation";
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
        parameters.WindowTitle = "RedBolt Crowd Navigation";
    }

    protected override void Stop()
    {
        Renderer.SetNumViewports(0);
        _viewport?.Dispose();
        _viewport = null;
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Stop();
    }
}
