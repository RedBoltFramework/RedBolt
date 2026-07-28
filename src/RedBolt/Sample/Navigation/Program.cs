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

using System;
using System.Collections.Generic;
using System.Linq;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Navigation;
using RedBolt.Resource;
using RedBolt.Scene;

internal static class NavigationProgram
{
    private static int Main()
    {
        try
        {
            using var application = new NavigationSample();
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

/// <summary>Navigation example.</summary>
public sealed class NavigationSample : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private Node? _jackNode;
    private Octree? _octree;
    private Viewport? _viewport;
    private readonly List<Vector3f> _currentPath = [];
    private readonly Dictionary<IntVector2, byte[]> _tileData = [];
    private readonly HashSet<IntVector2> _addedTiles = [];
    private Vector3f _endPosition;
    private bool _drawDebug;
    private bool _useStreaming;
    private const int StreamingDistance = 2;

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

        for (var i = 0; i < 100; ++i)
            CreateMushroom(new Vector3f(MathDefs.Random(90.0f) - 45.0f, 0.0f, MathDefs.Random(90.0f) - 45.0f));

        for (var i = 0; i < 20; ++i)
        {
            var boxNode = _scene.CreateChild("Box");
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

        _jackNode = _scene.CreateChild("Jack");
        _jackNode.SetPosition(new Vector3f(-5.0f, 0.0f, 20.0f));
        var modelObject = _jackNode.CreateComponent<AnimatedModel>();
        modelObject.SetModel(cache.Get<Model>("Models/Jack.mdl"));
        modelObject.SetCastShadows(true);

        var navMesh = _scene.CreateComponent<NavigationMesh>();
        navMesh.TileSize = 30;
        _scene.CreateComponent<Navigable>();
        navMesh.Padding = new Vector3f(0.0f, 10.0f, 0.0f);
        navMesh.Rebuild();

        _cameraNode = _scene.CreateChild("Camera");
        var controller = _cameraNode.CreateComponent<FreeFlyController>();
        controller.SetSpeed(20.0f);
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(300.0f);
        _cameraNode.SetPosition(new Vector3f(0.0f, 50.0f, 0.0f));
        _cameraNode.SetRotation(new Quaternion(80.0f, 0.0f, 0.0f));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        if (!Input.GetMouseButtonDown(MouseButton.Right) && Input.GetMouseButtonPress(MouseButton.Left))
            SetPathPoint();
        if (!Input.GetMouseButtonDown(MouseButton.Right)
            && (Input.GetMouseButtonPress(MouseButton.Middle) || Input.GetKeyPress(Key.O)))
            AddOrRemoveObject();
        if (Input.GetKeyPress(Key.Space))
            _drawDebug = !_drawDebug;

        FollowPath(timeStep);

        if (Input.GetKeyPress(Key.Tab))
        {
            _useStreaming = !_useStreaming;
            ToggleStreaming(_useStreaming);
        }
        if (_useStreaming)
            UpdateStreaming();
    }

    private void SetPathPoint()
    {
        if (!Raycast(250.0f, out var hitPosition, out _))
            return;
        var navMesh = _scene!.GetComponent<NavigationMesh>()!;
        var pathPosition = navMesh.FindNearestPoint(hitPosition, Vector3f.One);
        if (Input.GetKeyDown(Key.Shift))
        {
            _currentPath.Clear();
            _jackNode!.LookAt(new Vector3f(pathPosition.X, _jackNode.Position.Y, pathPosition.Z), Vector3f.Up);
            _jackNode.SetPosition(pathPosition);
        }
        else
        {
            _endPosition = pathPosition;
            navMesh.FindPath(_currentPath, _jackNode!.Position, _endPosition);
        }
    }

    private void AddOrRemoveObject()
    {
        if (_useStreaming || !Raycast(250.0f, out var hitPosition, out var hitDrawable))
            return;
        BoundingBox updateBox;
        var hitNode = hitDrawable!.Node!;
        if (hitNode.Name == "Mushroom")
        {
            updateBox = hitDrawable.WorldBoundingBox;
            hitNode.Remove();
        }
        else
        {
            var newNode = CreateMushroom(hitPosition);
            updateBox = newNode.GetComponent<StaticModel>()!.WorldBoundingBox;
        }
        var navMesh = _scene!.GetComponent<NavigationMesh>()!;
        navMesh.BuildTilesInRegion(updateBox);
        if (_currentPath.Count != 0)
            navMesh.FindPath(_currentPath, _jackNode!.Position, _endPosition);
    }

    private Node CreateMushroom(Vector3f position)
    {
        var node = _scene!.CreateChild("Mushroom");
        node.SetPosition(position);
        node.SetRotation(new Quaternion(0.0f, MathDefs.Random(360.0f), 0.0f));
        node.SetScale(2.0f + MathDefs.Random(0.5f));
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(Resources.Get<Model>("Models/Mushroom.mdl"));
        model.SetMaterial(Resources.Get<Material>("Materials/Mushroom.xml"));
        model.SetCastShadows(true);
        return node;
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

    private void FollowPath(float timeStep)
    {
        if (_currentPath.Count == 0)
            return;
        var nextWaypoint = _currentPath[0];
        var move = 5.0f * timeStep;
        var distance = (_jackNode!.Position - nextWaypoint).Length;
        if (move > distance)
            move = distance;
        _jackNode.LookAt(nextWaypoint, Vector3f.Up);
        _jackNode.Translate(Vector3f.Forward * move);
        if (distance < 0.1f)
            _currentPath.RemoveAt(0);
    }

    private void ToggleStreaming(bool enabled)
    {
        var navMesh = _scene!.GetComponent<NavigationMesh>()!;
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
        var navMesh = _scene!.GetComponent<NavigationMesh>()!;
        var jackTile = navMesh.GetTileIndex(_jackNode!.WorldPosition);
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
        var navMesh = _scene!.GetComponent<NavigationMesh>()!;
        _tileData.Clear();
        _addedTiles.Clear();
        foreach (var tileIndex in navMesh.GetAllTileIndices())
            _tileData[tileIndex] = navMesh.GetTileData(tileIndex);
    }

    private void HandlePostRenderUpdate()
    {
        if (_drawDebug)
            _scene!.GetComponent<NavigationMesh>()!.DrawDebugGeometry(true);
        if (_currentPath.Count == 0)
            return;
        var debug = _scene!.GetComponent<DebugRenderer>()!;
        var markerExtent = new Vector3f(0.1f, 0.1f, 0.1f);
        debug.AddBoundingBox(new BoundingBox(_endPosition - markerExtent, _endPosition + markerExtent), Color.White);
        var bias = new Vector3f(0.0f, 0.05f, 0.0f);
        debug.AddLine(_jackNode!.Position + bias, _currentPath[0] + bias, Color.White);
        for (var i = 0; i < _currentPath.Count - 1; ++i)
            debug.AddLine(_currentPath[i] + bias, _currentPath[i + 1] + bias, Color.White);
    }

    private void DrawInstructions()
    {
        var open = true;
        if (ImGui.Begin("Navigation\0"u8, ref open, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Use WASD keys to move, RMB to rotate view\0"u8);
            ImGui.TextUnformatted("LMB to set destination, SHIFT+LMB to teleport\0"u8);
            ImGui.TextUnformatted("MMB or O to add or remove obstacles\0"u8);
            ImGui.TextUnformatted("Tab toggles streaming; Space toggles debug geometry\0"u8);
        }
        ImGui.End();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Navigation";
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
        parameters.WindowTitle = "RedBolt Navigation";
    }

    protected override void Stop()
    {
        Renderer.SetNumViewports(0);
        _viewport?.Dispose();
        _viewport = null;
        _jackNode = null;
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Stop();
    }
}
