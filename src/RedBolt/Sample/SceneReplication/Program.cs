// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.SceneReplication;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Network;
using RedBolt.PhysicsJolt;
using RedBolt.Replica;
using RedBolt.Resource;
using RedBolt.Scene;

internal static class SceneReplicationProgram
{
    private static int Main()
    {
        try
        {
            using var application = new SceneReplication();
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

public struct PlayerControls
{
    public float Yaw;
    public uint Buttons;
}

/// <summary>Replicates the ball light color and returns owner controls to the authoritative server.</summary>
[Reflected]
public partial class SceneReplicationPlayer : NetworkBehavior
{
    public SceneReplicationPlayer()
        : base(NetworkCallbackFlags.UnreliableFeedback)
    {
    }

    public PlayerControls Controls { get; set; }

    public override void WriteSnapshot(NetworkFrame frame, Serializer destination) =>
        destination.WriteColor(GetComponent<Light>()!.Color);

    public override void InitializeFromSnapshot(NetworkFrame frame, Deserializer source, bool isOwned) =>
        GetComponent<Light>()!.SetColor(source.ReadColor());

    public override bool PrepareUnreliableFeedback(NetworkFrame frame) => true;

    public override void WriteUnreliableFeedback(NetworkFrame frame, Serializer destination)
    {
        destination.WriteFloat(Controls.Yaw);
        destination.WriteVLE(Controls.Buttons);
    }

    public override void ReadUnreliableFeedback(NetworkFrame feedbackFrame, Deserializer source)
    {
        if (_lastFeedbackFrame is { } lastFeedbackFrame && lastFeedbackFrame >= feedbackFrame)
            return;
        Controls = new PlayerControls { Yaw = source.ReadFloat(), Buttons = source.ReadVLE() };
        _lastFeedbackFrame = feedbackFrame;
    }

    private NetworkFrame? _lastFeedbackFrame;
}

internal sealed class ReplicatedDataChannelConnection : DataChannelConnection
{
    public ReplicatedDataChannelConnection()
    {
        ReplicationPeer = new ReplicatedPeer(this);
        Statistics = new NetworkStatisticsCounter(this);
    }

    public ReplicatedPeer ReplicationPeer { get; }
    public NetworkStatisticsCounter Statistics { get; }
    public Node? ControlledObject { get; set; }

    public override void Dispose()
    {
        ReplicationPeer.Dispose();
        base.Dispose();
    }
}

/// <summary>Authoritative scene-replication sample using Jolt rigid-body physics.</summary>
public sealed class SceneReplication : Application
{
    private const ushort ServerPort = 2345;
    private const uint ControlForward = 1;
    private const uint ControlBack = 2;
    private const uint ControlLeft = 4;
    private const uint ControlRight = 8;
    private const int AddressBufferSize = 256;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.SceneReplication";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
#endif
        parameters.WindowTitle = "RedBolt Scene Replication (Jolt)";
    }

    protected override void SetupCommandLine(CommandLineParser parser, EngineParameters parameters)
    {
        parser.AddFlag(["--start-server"], "Start the replication server on launch.",
            () => _startServerOnLaunch = true);
        parser.AddOption(["--connect"], "address", "Connect to a replication server on launch.",
            address => _connectOnLaunch = address);
    }

    protected override void Start()
    {
        base.Start();
        Context.RegisterSubsystem(new RedBolt.Network.Network());
        RedBolt.PhysicsJolt.Physics.RegisterPhysicsLibrary(Context);
        Context.RegisterAssembly(typeof(SceneReplicationPlayer).Assembly);
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _addressBuffer = Marshal.AllocHGlobal(AddressBufferSize);
        Marshal.WriteByte(_addressBuffer, 0, 0);
        if (!string.IsNullOrWhiteSpace(_connectOnLaunch))
        {
            var addressBytes = Encoding.UTF8.GetBytes(_connectOnLaunch);
            var length = System.Math.Min(addressBytes.Length, AddressBufferSize - 1);
            Marshal.Copy(addressBytes, 0, _addressBuffer, length);
            Marshal.WriteByte(_addressBuffer, length, 0);
        }

        CreateScene();
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
        SubscribeToEvent<PhysicsEvents.PhysicsPreStepEvent>(
            _scene!.GetComponent<PhysicsWorld>()!, HandlePhysicsPreStep);
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInterface());
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);

        if (_startServerOnLaunch)
            StartServer();
        else if (!string.IsNullOrWhiteSpace(_connectOnLaunch))
            Connect();
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }
        MoveCamera();
        _statisticsTime += timeStep;
        if (_statisticsTime >= 1.0f)
        {
            _statisticsTime %= 1.0f;
            UpdateStatistics();
        }
    }

    protected override void Stop()
    {
        Disconnect();
        _server?.Dispose();
        _server = null;
        Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        if (_addressBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_addressBuffer);
            _addressBuffer = IntPtr.Zero;
        }
        base.Stop();
    }

    private void CreateScene()
    {
        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<PhysicsWorld>();
        _scene.CreateComponent<ReplicationManager>();

        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.1f, 0.1f, 0.1f));
        zone.SetFogStart(100.0f);
        zone.SetFogEnd(300.0f);

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.5f, -1.0f, 0.5f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetColor(new Color(0.2f, 0.2f, 0.2f));
        light.SetSpecularIntensity(1.0f);

        var boxModel = Resources.Get<Model>("Models/Box.mdl");
        var stoneMaterial = Resources.Get<Material>("Materials/Stone.xml");
        for (var y = -20; y <= 20; ++y)
        {
            for (var x = -20; x <= 20; ++x)
            {
                var floorNode = _scene.CreateChild("FloorTile");
                floorNode.SetPosition(new Vector3f(x * 20.2f, -0.5f, y * 20.2f));
                floorNode.SetScale(new Vector3f(20.0f, 1.0f, 20.0f));
                var floorObject = floorNode.CreateComponent<StaticModel>();
                floorObject.SetModel(boxModel);
                floorObject.SetMaterial(stoneMaterial);
                floorNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
                floorNode.CreateComponent<RigidBody>().Friction = 1.0f;
            }
        }

        _cameraNode = _scene.CreateChild("Camera");
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(300.0f);
        _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, 0.0f));
    }

    private void DrawInterface()
    {
        ImGui.SetNextWindowSize(new Vector2f(430.0f, 270.0f), ImGuiCond.ImGuiCondFirstUseEver);
        var open = true;
        if (ImGui.Begin("Scene Replication (Jolt)"u8, ref open,
            ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            var clientConnected = _clientConnection?.IsConnected == true;
            var serverRunning = _server?.IsListening == true;
            if (!clientConnected && !serverRunning)
            {
                ImGui.InputText("Server address"u8, _addressBuffer, AddressBufferSize);
                if (ImGui.Button("Connect"u8))
                    Connect();
#if !REDBOLT_PLATFORM_BROWSER
                    ImGui.SameLine();
                    if (ImGui.Button("Start server"u8))
                        StartServer();
#endif
            }
            else if (ImGui.Button("Disconnect"u8))
                Disconnect();

            var mode = clientConnected ? "Client connected" : serverRunning ? "Server running" : "Offline";
            ImGui.TextUnformatted(Encoding.UTF8.GetBytes(mode));
            ImGui.Separator();
            ImGui.TextUnformatted(Encoding.UTF8.GetBytes($"Packets in/out: {_statistics.PacketsInPerSec} / {_statistics.PacketsOutPerSec}"));
            ImGui.TextUnformatted(Encoding.UTF8.GetBytes($"Bytes in/out: {_statistics.BytesInPerSec} / {_statistics.BytesOutPerSec}"));
            ImGui.TextUnformatted(Encoding.UTF8.GetBytes($"Connections: {_connectionCount}"));
            if (GetPlayerObject() is not null)
                ImGui.TextUnformatted("WASD: roll ball   RMB + mouse: orbit camera"u8);
        }
        ImGui.End();
    }

    private void MoveCamera()
    {
        if (_cameraNode is null)
            return;
        var rotating = Input.GetMouseButtonDown(MouseButton.Right) && !ImGui.GetIO().WantCaptureMouse;
        Input.SetMouseMode(rotating ? MouseMode.Relative : MouseMode.Free);
        Input.SetMouseVisible(!rotating);
        if (rotating)
        {
            var mouseMove = Input.GetMouseMove();
            _yaw += 0.1f * mouseMove.X;
            _pitch = System.Math.Clamp(_pitch + 0.1f * mouseMove.Y, 1.0f, 90.0f);
        }
        _cameraNode.SetRotation(new Quaternion(_pitch, _yaw, 0.0f));
        if (GetPlayerObject() is { } playerNode)
            _cameraNode.SetPosition(playerNode.GetPosition() + _cameraNode.GetRotation() * Vector3f.Back * 5.0f);
    }

    private void HandlePhysicsPreStep(ref PhysicsEvents.PhysicsPreStepEvent eventData)
    {
        if (_clientConnection?.IsConnected == true)
        {
            if (GetPlayerObject()?.GetComponent<SceneReplicationPlayer>() is { } player)
            {
                var controls = new PlayerControls { Yaw = _yaw };
                if (!ImGui.GetIO().WantCaptureKeyboard)
                {
                    if (Input.GetKeyDown(Key.W)) controls.Buttons |= ControlForward;
                    if (Input.GetKeyDown(Key.S)) controls.Buttons |= ControlBack;
                    if (Input.GetKeyDown(Key.A)) controls.Buttons |= ControlLeft;
                    if (Input.GetKeyDown(Key.D)) controls.Buttons |= ControlRight;
                }
                player.Controls = controls;
            }
            return;
        }

        if (_server?.IsListening != true)
            return;
        foreach (var connection in _serverConnections)
        {
            if (connection.ControlledObject is not { } playerNode)
                continue;
            var body = playerNode.GetComponent<RigidBody>()!;
            var controls = playerNode.GetComponent<SceneReplicationPlayer>()!.Controls;
            var rotation = new Quaternion(0.0f, controls.Yaw, 0.0f);
            const float moveTorque = 3.0f;
            if ((controls.Buttons & ControlForward) != 0) body.ApplyTorque(rotation * Vector3f.Right * moveTorque);
            if ((controls.Buttons & ControlBack) != 0) body.ApplyTorque(rotation * Vector3f.Left * moveTorque);
            if ((controls.Buttons & ControlLeft) != 0) body.ApplyTorque(rotation * Vector3f.Forward * moveTorque);
            if ((controls.Buttons & ControlRight) != 0) body.ApplyTorque(rotation * Vector3f.Back * moveTorque);
        }
    }

    private Node CreateControllableObject(ReplicatedPeer owner)
    {
        var prefab = Resources.Get<PrefabResource>("Prefabs/SceneReplicationPlayer.prefab")!;
        var position = new Vector3f(
            (float)Random.Shared.NextDouble() * 40.0f - 20.0f,
            5.0f,
            (float)Random.Shared.NextDouble() * 40.0f - 20.0f);
        var playerNode = _scene!.InstantiatePrefab(prefab.GetNodePrefab(), position, Quaternion.IDENTITY)!;
        playerNode.SetName("Ball");
        var networkObject = playerNode.CreateComponent<BehaviorNetworkObject>();
        networkObject.SetClientPrefab(prefab);
        networkObject.SetOwner(owner);
        playerNode.CreateComponent<CollisionShape>().SetSphere(1.0f);
        var body = playerNode.CreateComponent<RigidBody>();
        body.Mass = 1.0f;
        body.Friction = 1.0f;
        body.LinearDamping = 0.5f;
        body.AngularDamping = 0.5f;
        playerNode.GetComponent<Light>()!.SetColor(new Color(
            Random.Shared.Next(2) == 0 ? 0.5f : 1.0f,
            Random.Shared.Next(2) == 0 ? 0.5f : 1.0f,
            Random.Shared.Next(2) == 0 ? 0.5f : 1.0f));
        return playerNode;
    }

    private Node? GetPlayerObject() =>
        _scene?.GetComponent<ReplicationManager>()?.ClientReplica?.OwnedNetworkObject?.Node;

    private void Connect()
    {
        Disconnect();
        var address = (Marshal.PtrToStringUTF8(_addressBuffer) ?? string.Empty).Trim();
        if (address.Length == 0)
            address = "localhost";
        _clientConnection = new ReplicatedDataChannelConnection();
        _clientConnection.ReplicationPeer.SetReplicationManager(_scene!.GetComponent<ReplicationManager>());
        _clientConnection.Connect(new URL(address.Contains("://", StringComparison.Ordinal)
            ? address
            : $"ws://{address}:{ServerPort}"));
    }

    private void StartServer()
    {
        if (_server is null)
        {
            _server = new DataChannelServer();
            _server.SetConnectionFactory(static () => new ReplicatedDataChannelConnection());
            _server.Connected += HandleServerConnected;
            _server.Disconnected += HandleServerDisconnected;
        }
        _scene!.GetComponent<ReplicationManager>()!.StartServer();
        _server.Listen(new URL($"ws://0.0.0.0:{ServerPort}"));
    }

    private void Disconnect()
    {
        if (_clientConnection is not null)
        {
            _clientConnection.Disconnect();
            _clientConnection.Dispose();
            _clientConnection = null;
        }
        if (_server?.IsListening == true)
        {
            foreach (var connection in _serverConnections.ToArray())
                connection.Disconnect();
            _serverConnections.Clear();
            _server.Stop();
            _scene?.GetComponent<ReplicationManager>()?.StartStandalone();
        }
    }

    private void HandleServerConnected(NetworkServer server, NetworkConnection connection)
    {
        var replicatedConnection = (ReplicatedDataChannelConnection)connection;
        replicatedConnection.ReplicationPeer.SetReplicationManager(_scene!.GetComponent<ReplicationManager>());
        replicatedConnection.ControlledObject = CreateControllableObject(replicatedConnection.ReplicationPeer);
        _serverConnections.Add(replicatedConnection);
    }

    private void HandleServerDisconnected(NetworkServer server, NetworkConnection connection)
    {
        var replicatedConnection = (ReplicatedDataChannelConnection)connection;
        replicatedConnection.ControlledObject?.Remove();
        replicatedConnection.ControlledObject = null;
        _serverConnections.Remove(replicatedConnection);
        replicatedConnection.Dispose();
    }

    private void UpdateStatistics()
    {
        _statistics = default;
        if (_clientConnection?.IsConnected == true)
        {
            _statistics = _clientConnection.Statistics.Snapshot;
            _connectionCount = 1;
            return;
        }
        _connectionCount = _serverConnections.Count;
        foreach (var connection in _serverConnections)
            _statistics += connection.Statistics.Snapshot;
    }

    private Scene? _scene;
    private Node? _cameraNode;
    private DataChannelServer? _server;
    private ReplicatedDataChannelConnection? _clientConnection;
    private readonly List<ReplicatedDataChannelConnection> _serverConnections = [];
    private IntPtr _addressBuffer;
    private float _yaw;
    private float _pitch = 30.0f;
    private float _statisticsTime;
    private NetworkStatisticsSnapshot _statistics;
    private int _connectionCount;
    private bool _startServerOnLaunch;
    private string _connectOnLaunch = string.Empty;
}
