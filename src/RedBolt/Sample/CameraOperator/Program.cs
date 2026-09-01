// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.CameraOperator;

using System;

using RedBolt;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;

internal static class CameraOperatorProgram
{
    private static int Main()
    {
        try { return new CameraOperatorSampleApplication().Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

/// <summary>
/// Camera operator sample.
/// This sample demonstrates:
///     - How camera operator tracks objects in space to keep them in camera view
///     - How camera operator parameters work
/// </summary>
sealed class CameraOperatorSampleApplication : Application
{
    private static readonly Vector3f[] _trackPoints =
    [
        new(-8.0f, 0.08f, -1.0f),
        new(-4.5f, 0.08f, -5.0f),
        new(4.0f, 0.08f, -5.0f),
        new(8.0f, 0.08f, -1.5f),
        new(6.5f, 0.08f, 5.0f),
        new(1.5f, 0.08f, 7.0f),
        new(-5.5f, 0.08f, 5.5f),
        new(-8.5f, 0.08f, 2.5f)
    ];

    private Scene? _scene;
    private Node? _cameraNode;
    private CameraOperator? _cameraOperator;
    private Node? _carA;
    private Node? _incomingTarget;
    private Node? _focusedNode;
    private Vector3f _observerPosition;
    private Vector3f _lookTarget;
    private float _raceTime;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.CameraOperator";
        parameters.Borderless = false; parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData"; parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720; parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Camera Operator";
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        // Execute base class startup
        // Create the scene content
        CreateScene();
        // Setup the viewport for displaying the scene
        SetupViewport();
    }

    /// <summary>Construct the scene content.</summary>
    private void CreateScene()
    {
        _scene = new Scene(); _scene.LoadFile("Scenes/CameraOperatorScene.scene");
        _cameraNode = _scene.FindChild("MainCamera");
        _cameraOperator = _cameraNode!.FindComponent<CameraOperator>();
        _cameraOperator!.SetEnabled(false);
        _carA = _scene.FindChild("CarA", true);
        _incomingTarget = _scene.FindChild("IncomingTarget", true);
        var incomingPosition = GetTrackPosition(0.08f);
        incomingPosition.Y = 0.8f;
        _incomingTarget!.SetPosition(incomingPosition);
        _observerPosition = _cameraNode.WorldPosition;
        _lookTarget = _incomingTarget.WorldPosition;
        _cameraNode.LookAt(_lookTarget, Vector3f.Up);
        CreateTechnicalTrack();
        SetFocusedNode(_incomingTarget);
    }

    private void CreateTechnicalTrack()
    {
        var scene = _scene!;
        var roadMaterial = Resources.Get<Material>("Materials/TechnicalDemo/Grid.xml");
        var roadNode = scene.CreateChild("Track Surface");
        var road = roadNode.CreateComponent<CustomGeometry>();
        road.BeginGeometry(0, PrimitiveType.TriangleList);
        const int numRoadSegments = 64;
        for (var i = 0; i < numRoadSegments; ++i)
        {
            var progress = (float)i / numRoadSegments;
            var nextProgress = (float)(i + 1) / numRoadSegments;
            GetRoadEdges(progress, out var left, out var right);
            GetRoadEdges(nextProgress, out var nextLeft, out var nextRight);
            DefineRoadVertex(road, left, new Vector2f(0.0f, progress * 12.0f));
            DefineRoadVertex(road, nextLeft, new Vector2f(0.0f, nextProgress * 12.0f));
            DefineRoadVertex(road, right, new Vector2f(1.0f, progress * 12.0f));
            DefineRoadVertex(road, right, new Vector2f(1.0f, progress * 12.0f));
            DefineRoadVertex(road, nextLeft, new Vector2f(0.0f, nextProgress * 12.0f));
            DefineRoadVertex(road, nextRight, new Vector2f(1.0f, nextProgress * 12.0f));
        }
        road.Commit();
        road.SetMaterial(roadMaterial);
        road.SetCastShadows(true);

        for (var i = 0; i < 24; ++i)
        {
            var progress = (i + 0.5f) / 24.0f;
            GetRoadEdges(progress, out var left, out var right);
            var material = i % 3 == 0
                ? "Materials/TechnicalDemo/Accent.xml"
                : "Materials/TechnicalDemo/Dark.xml";
            CreateTrackMarker(left, progress, material);
            CreateTrackMarker(right, progress, material);
        }

        float[] gateProgress = [0.02f, 0.27f, 0.52f, 0.77f];
        foreach (var progress in gateProgress)
        {
            GetRoadEdges(progress, out var left, out var right);
            CreateTrackBox("Gate Post", left + Vector3f.Up * 1.5f, new Vector3f(0.35f, 3.0f, 0.35f),
                "Materials/TechnicalDemo/Accent.xml");
            CreateTrackBox("Gate Post", right + Vector3f.Up * 1.5f, new Vector3f(0.35f, 3.0f, 0.35f),
                "Materials/TechnicalDemo/Accent.xml");
            var beamPosition = (left + right) * 0.5f + Vector3f.Up * 3.0f;
            var beam = CreateTrackBox("Gate Beam", beamPosition,
                new Vector3f(0.35f, 0.35f, (right - left).Length + 0.35f),
                "Materials/TechnicalDemo/Dark.xml");
            beam.LookAt(right + Vector3f.Up * 3.0f, Vector3f.Up);
        }
    }

    private void CreateTrackMarker(Vector3f position, float progress, string material)
    {
        var marker = CreateTrackBox("Track Marker", position + Vector3f.Up * 0.22f,
            new Vector3f(0.28f, 0.44f, 0.8f), material);
        marker.LookAt(GetTrackPosition(progress + 0.01f), Vector3f.Up);
    }

    private Node CreateTrackBox(string name, Vector3f position, Vector3f scale, string material)
    {
        var node = _scene!.CreateChild(name);
        node.SetPosition(position);
        node.SetScale(scale);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        model.SetMaterial(Resources.Get<Material>(material));
        model.SetCastShadows(true);
        return node;
    }

    private static Vector3f GetTrackPosition(float progress) => new(
        CatmullRom(progress, static point => point.X),
        0.08f,
        CatmullRom(progress, static point => point.Z));

    private static float CatmullRom(float progress, Func<Vector3f, float> getCoordinate)
    {
        var scaledProgress = WrapProgress(progress) * _trackPoints.Length;
        var index = (int)MathF.Floor(scaledProgress);
        var factor = scaledProgress - index;
        var p0 = getCoordinate(_trackPoints[(index - 1 + _trackPoints.Length) % _trackPoints.Length]);
        var p1 = getCoordinate(_trackPoints[index]);
        var p2 = getCoordinate(_trackPoints[(index + 1) % _trackPoints.Length]);
        var p3 = getCoordinate(_trackPoints[(index + 2) % _trackPoints.Length]);
        var factor2 = factor * factor;
        var factor3 = factor2 * factor;
        return 0.5f * (2.0f * p1 + (-p0 + p2) * factor
            + (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * factor2
            + (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * factor3);
    }

    private static void GetRoadEdges(float progress, out Vector3f left, out Vector3f right)
    {
        var position = GetTrackPosition(progress);
        position.Y = 0.04f;
        var previous = GetTrackPosition(WrapProgress(progress - 0.01f));
        var next = GetTrackPosition(WrapProgress(progress + 0.01f));
        var direction = (next - previous).Normalized;
        var side = new Vector3f(-direction.Z, 0.0f, direction.X) * 3.2f;
        left = position + side;
        right = position - side;
    }

    private static float WrapProgress(float progress) => progress - MathF.Floor(progress);

    private static void DefineRoadVertex(CustomGeometry road, Vector3f position, Vector2f texCoord)
    {
        road.DefineVertex(position);
        road.DefineNormal(Vector3f.Up);
        road.DefineTexCoord(texCoord);
    }

    private static float UpdateCar(Node car, float raceTime)
    {
        const float lapTime = 12.0f;
        var progress = raceTime / lapTime;
        car.SetPosition(GetTrackPosition(progress));
        car.LookAt(GetTrackPosition(WrapProgress(progress + 0.01f)), Vector3f.Up);
        return progress;
    }

    private void SetFocusedNode(Node node)
    {
        if (ReferenceEquals(_focusedNode, node))
            return;
        _cameraOperator!.RemoveAllTrackedNodes();
        _cameraOperator.TrackNode(node);
        _focusedNode = node;
    }

    /// <summary>Set up a viewport for displaying the scene.</summary>
    private void SetupViewport()
    {
        // Set up a viewport to the Renderer subsystem so that the 3D scene can be seen. We need to define the scene and the
        // camera at minimum. Additionally we could configure the viewport screen size and the rendering path (eg. forward /
        // deferred) to use, but now we just use full screen and default render path configured in the engine command line
        // options
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.FindComponent<Camera>()!));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit")) { Exit(); return; }
        _raceTime = (_raceTime + timeStep) % 12.0f;
        var carAProgress = UpdateCar(_carA!, _raceTime);
        var observerDistance = MathF.Abs(carAProgress - 0.22f);
        if (observerDistance > 0.17f)
            SetFocusedNode(_incomingTarget!);
        else
            SetFocusedNode(_carA!);
        _lookTarget = _lookTarget.Lerp(_focusedNode!.WorldPosition, Math.Min(1.0f, timeStep * 2.5f));
        _cameraNode!.SetWorldPosition(_observerPosition);
        _cameraNode.LookAt(_lookTarget, Vector3f.Up);
    }

    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        base.Stop();
    }

    private SampleInputMap? _sampleInput;
}
