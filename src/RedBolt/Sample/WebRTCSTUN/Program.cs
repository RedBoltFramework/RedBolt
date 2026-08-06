// Copyright (c) 2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.WebRTCSTUN;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using RedBolt.Bindings.DearImGui;
using RedBolt.Bindings.LibDataChannel;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.IO;
using RedBolt.Input;
using RedBolt.Network;
using RedBolt.Platform;

using static RedBolt.Bindings.LibDataChannel.LibDataChannelNativeBindings;

internal static class WebRTCSTUNProgram
{
    private static int Main()
    {
        try
        {
            using var application = new WebRTCSTUN();
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

/// <summary>WebRTC STUN/TURN connectivity test with chat and relay signaling.</summary>
public sealed class WebRTCSTUN : Application
{
    private const ushort DefaultPort = 2345;
    private const ushort DataPort = 2346;
    private const string DefaultRelayUrl = "ws://localhost:9876";
    private const int MaxRetries = 5;
    private const float RetryDelay = 2.0f;
    private const int InputBufferSize = 1024;
    private static readonly NetworkMessageId ChatMessage =
        (NetworkMessageId)((uint)NetworkMessageId.User + 0);
    private static readonly NetworkMessageId SystemMessage =
        (NetworkMessageId)((uint)NetworkMessageId.User + 1);
    private static readonly string[] IceServers =
    [
        "stun:stun.l.google.com:19302",
        "stun:stun1.l.google.com:19302",
        "stun:stun2.l.google.com:19302",
        "stun:stun3.l.google.com:19302",
        "stun:stun4.l.google.com:19302"
    ];

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.WebRTCSTUN";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        if (ProcessUtils.GetPlatform() != PlatformId.Web)
        {
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
        }
        parameters.WindowTitle = "RedBolt WebRTC STUN/TURN Test";
    }

    protected override void Start()
    {
        base.Start();
        Context.RegisterSubsystem(new RedBolt.Network.Network());
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _textBuffer = Marshal.AllocHGlobal(InputBufferSize);
        Marshal.WriteByte(_textBuffer, 0, 0);
        for (var i = 0; i < 38; ++i)
            _chatHistory.Add(string.Empty);

        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => Draw());
        SubscribeToEvent<IOEvents.LogMessage>(HandleSampleLogMessage);
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);

        Log("=== WebRTC STUN/TURN Test ===");
        Log($"Signaling port: {DefaultPort} (TCP) | Data port: {DataPort} (UDP, muxed)");
        Log("Direct: type IP[:port] -> Connect | Start Server [port]");
        Log($"Relay: room[@host:port] -> Start/Connect Relay (default: {DefaultRelayUrl})");
    }

    protected override void Update(float timeStep)
    {
        if (!_retrying || _retryTimer.GetMSec(false) < RetryDelay * 1000.0f)
            return;

        _retrying = false;
        if (_retryCount < MaxRetries && !_wasEverConnected)
        {
            if (_isRelayMode)
                TryConnectRelay(_retryAddress, _retryRelayUrl);
            else
                TryConnect(_retryAddress);
        }
    }

    protected override void Stop()
    {
        Disconnect();
        foreach (var socket in _relaySockets.ToArray())
            socket.Dispose();
        _relaySockets.Clear();
        _clientConnection?.Dispose();
        _clientConnection = null;
        _server?.Dispose();
        _server = null;
        if (_textBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_textBuffer);
            _textBuffer = IntPtr.Zero;
        }
        base.Stop();
    }

    private void Draw()
    {
        ImGui.SetNextWindowSize(new Vector2f(1100.0f, 650.0f),
            ImGuiCond.ImGuiCondFirstUseEver);
        var open = true;
        if (ImGui.Begin("WebRTC STUN/TURN Test"u8, ref open,
            ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            if (ImGui.BeginChild("Chat history"u8, new Vector2f(0.0f, 520.0f)))
            {
                foreach (var row in _chatHistory)
                    ImGui.TextUnformatted(Encoding.UTF8.GetBytes(row));
            }
            ImGui.EndChild();

            ImGui.InputText("Address, port, or relay room"u8, _textBuffer, InputBufferSize);
            var clientConnected = _clientConnection?.IsConnected == true;
            var serverRunning = _server is not null &&
                (_server.IsListening || _relayConnections.Count != 0);
            var canChat = clientConnected || serverRunning && _serverConnections.Count != 0;
            var idle = !clientConnected && !serverRunning;

            if (idle && ImGui.Button("Start Server"u8))
                StartServer();
            if (idle)
            {
                ImGui.SameLine();
                if (ImGui.Button("Start Relay"u8))
                    StartRelay();
                ImGui.SameLine();
                if (ImGui.Button("Connect"u8))
                    Connect();
                ImGui.SameLine();
                if (ImGui.Button("Connect Relay"u8))
                    ConnectRelay();
            }
            if (!idle && ImGui.Button("Disconnect"u8))
                Disconnect();
            if (canChat)
            {
                ImGui.SameLine();
                if (ImGui.Button("Send"u8))
                    Send();
            }
        }
        ImGui.End();
    }

    private void ShowChatText(string row)
    {
        if (_chatHistory.Count != 0)
            _chatHistory.RemoveAt(0);
        _chatHistory.Add(row);
    }

    private void HandleSampleLogMessage(ref IOEvents.LogMessage eventData) =>
        ShowChatText(eventData.Message);

    private void Send()
    {
        var text = ReadText();
        if (text.Length == 0)
            return;

        using var data = new VectorBuffer();
        data.WriteString(text);
        using var message = new MemoryBuffer(data);
        if (_clientConnection?.IsConnected == true)
        {
            _clientConnection.SendMessage(ChatMessage, message);
            ShowChatText("[You -> Server] " + text);
        }
        else if (_server is not null &&
            (_server.IsListening || _relayConnections.Count != 0))
        {
            foreach (var weakConnection in _serverConnections)
            {
                if (weakConnection.TryGetTarget(out var connection))
                    connection.SendMessage(ChatMessage, message);
            }
            ShowChatText("[Server -> All] " + text);
        }
        ClearText();
    }

    private void Connect()
    {
        var address = ReadText().Trim();
        if (address.Length == 0)
            address = "localhost";
        ClearText();

        if (_clientConnection is not null && !_clientConnection.IsDisconnected)
            _clientConnection.Disconnect();
        _retryAddress = address;
        _retryCount = 0;
        _wasEverConnected = false;
        _isRelayMode = false;
        TryConnect(address);
    }

    private void TryConnect(string address)
    {
        EnsureClientConnection();
        var host = address;
        var port = DefaultPort;
        var colonPosition = address.IndexOf(':');
        if (colonPosition >= 0)
        {
            host = address[..colonPosition];
            if (ushort.TryParse(address[(colonPosition + 1)..], out var parsedPort))
                port = parsedPort;
        }

        var url = $"ws://{host}:{port}";
        Log($"Connecting to {url} ... (attempt {_retryCount + 1})");
        _clientConnection!.Connect(new URL(url));
        HookIceLogging(_clientConnection);
    }

    private void StartServer()
    {
        EnsureServer();
        var signalingPort = DefaultPort;
        var dataPort = DataPort;
        var input = ReadText().Trim();
        if (ushort.TryParse(input, out var parsedPort) && parsedPort != 0)
        {
            signalingPort = parsedPort;
            dataPort = unchecked((ushort)(signalingPort + 1));
        }
        ClearText();

        _server!.SetIceUdpMux(true);
        _server.SetPortRange(dataPort, dataPort);
        var listenUrl = new URL($"ws://0.0.0.0:{signalingPort}");
        Log($"Signaling: {listenUrl} | WebRTC data: UDP :{dataPort} (single port, muxed)");
        Log($"Port-forward TCP {signalingPort} and UDP {dataPort} for direct connections.");
        _server.Listen(listenUrl);
    }

    private void StartRelay()
    {
        var (roomId, relayUrl) = ParseRelay(ReadText().Trim());
        ClearText();
        EnsureServer();
        Log($"Joining relay room '{roomId}' as host via {relayUrl}...");

        RelayWebSocket? socket = null;
        socket = new RelayWebSocket(roomId, relayUrl, webSocket =>
        {
            Log("Relay: Paired!");
            var connection = new DataChannelConnection();
            connection.SetIceServers(IceServers);
            connection.InitializeWithWebSocket(webSocket, _server);
            HookIceLogging(connection);
            _relayConnections.Add(connection);
            if (socket is not null)
                _relaySockets.Remove(socket);
        });
        _relaySockets.Add(socket);
    }

    private void ConnectRelay()
    {
        var (roomId, relayUrl) = ParseRelay(ReadText().Trim());
        ClearText();
        if (_clientConnection is not null && !_clientConnection.IsDisconnected)
            _clientConnection.Disconnect();
        _retryAddress = roomId;
        _retryRelayUrl = relayUrl;
        _retryCount = 0;
        _wasEverConnected = false;
        _isRelayMode = true;
        TryConnectRelay(roomId, relayUrl);
    }

    private void TryConnectRelay(string roomId, string relayUrl)
    {
        EnsureClientConnection();
        Log($"Joining relay room '{roomId}' as client via {relayUrl}... (attempt {_retryCount + 1})");

        RelayWebSocket? socket = null;
        socket = new RelayWebSocket(roomId, relayUrl, webSocket =>
        {
            Log("Relay: Paired!");
            _clientConnection!.InitializeWithWebSocket(webSocket);
            HookIceLogging(_clientConnection);
            if (socket is not null)
                _relaySockets.Remove(socket);
        });
        _relaySockets.Add(socket);
    }

    private void ScheduleRetry()
    {
        if (_retryCount >= MaxRetries || _wasEverConnected)
            return;
        ++_retryCount;
        Log($"Connection failed. Retrying in {RetryDelay:0}s... ({_retryCount}/{MaxRetries})");
        _retrying = true;
        _retryTimer.Reset();
    }

    private void Disconnect()
    {
        _retryCount = MaxRetries;
        _retrying = false;
        if (_clientConnection is not null && !_clientConnection.IsDisconnected)
        {
            Log("Disconnecting client...");
            _clientConnection.Disconnect();
        }

        if (_server?.IsListening == true)
        {
            Log("Stopping server...");
            foreach (var weakConnection in _serverConnections)
            {
                if (weakConnection.TryGetTarget(out var connection))
                    connection.Disconnect();
            }
            _serverConnections.Clear();
            _serverClientIds.Clear();
            _server.Stop();
        }

        foreach (var connection in _relayConnections)
        {
            if (!connection.IsDisconnected)
                connection.Disconnect();
            connection.Dispose();
        }
        _relayConnections.Clear();
        foreach (var socket in _relaySockets.ToArray())
            socket.Dispose();
        _relaySockets.Clear();
    }

    private void EnsureServer()
    {
        if (_server is not null)
            return;
        _server = new DataChannelServer();
        _server.SetIceServers(IceServers);
        _server.ListenStarted += _ => HandleServerListenStart();
        _server.ListenStopped += _ => HandleServerListenStop();
        _server.Connected += HandleServerConnected;
        _server.Disconnected += HandleServerDisconnected;
    }

    private void EnsureClientConnection()
    {
        if (_clientConnection is not null)
            return;
        _clientConnection = new DataChannelConnection();
        _clientConnection.SetIceServers(IceServers);
        _clientConnection.Connected += HandleClientConnected;
        _clientConnection.Disconnected += HandleClientDisconnected;
        _clientConnection.MessageReceived += HandleNetworkMessage;
    }

    private void HandleServerListenStart()
    {
        Log("Server is listening. Give this machine's IP to the client.");
        Log("For direct connections, forward TCP signaling port and UDP data port.");
    }

    private void HandleServerListenStop() => Log("Server stopped listening.");

    private void HandleServerConnected(NetworkServer server, NetworkConnection connection)
    {
        var clientId = _nextClientId++;
        _serverConnections.Add(new WeakReference<NetworkConnection>(connection));
        _serverClientIds.Add(clientId);
        connection.MessageReceived += HandleNetworkMessage;

        if (connection is DataChannelConnection dataChannelConnection)
        {
            HookIceLogging(dataChannelConnection);
            LogSelectedCandidatePair(dataChannelConnection.Peer);
        }

        var type = ClassifyAddress(connection.Address);
        Log(connection.Address.Length != 0
            ? $"Server: Client {clientId} connected! {connection.Address} ({type}) ({_serverConnections.Count} total)"
            : $"Server: Client {clientId} connected! ({_serverConnections.Count} total)");

        using var data = new VectorBuffer();
        data.WriteString($"Client {clientId} joined from {type} ({_serverConnections.Count} online)");
        using var message = new MemoryBuffer(data);
        foreach (var weakConnection in _serverConnections)
        {
            if (weakConnection.TryGetTarget(out var other) && !ReferenceEquals(other, connection))
                other.SendMessage(SystemMessage, message);
        }
    }

    private void HandleServerDisconnected(NetworkServer server, NetworkConnection connection)
    {
        uint disconnectedId = 0;
        for (var i = 0; i < _serverConnections.Count;)
        {
            if (_serverConnections[i].TryGetTarget(out var item) && !ReferenceEquals(item, connection))
            {
                ++i;
                continue;
            }
            disconnectedId = _serverClientIds[i];
            _serverConnections.RemoveAt(i);
            _serverClientIds.RemoveAt(i);
        }

        Log($"Server: Client {disconnectedId} disconnected. ({_serverConnections.Count} remaining)");
        if (_serverConnections.Count == 0)
            return;

        using var data = new VectorBuffer();
        data.WriteString($"Client {disconnectedId} left ({_serverConnections.Count} online)");
        using var message = new MemoryBuffer(data);
        foreach (var weakConnection in _serverConnections)
        {
            if (weakConnection.TryGetTarget(out var other))
                other.SendMessage(SystemMessage, message);
        }
    }

    private void HandleClientConnected(NetworkConnection connection)
    {
        _wasEverConnected = true;
        _retryCount = MaxRetries;
        Log(connection.Address.Length != 0
            ? $"Connected! Remote peer: {connection.Address} ({ClassifyAddress(connection.Address)})"
            : "Connected! (Web platform - address unavailable)");
        if (connection is DataChannelConnection dataChannelConnection)
            LogSelectedCandidatePair(dataChannelConnection.Peer);
    }

    private void HandleClientDisconnected(NetworkConnection connection)
    {
        if (!_wasEverConnected)
            ScheduleRetry();
        else
            Log("Disconnected from server.");
    }

    private void HandleNetworkMessage(NetworkConnection connection,
        NetworkMessageId messageId, MemoryBuffer message, ref bool handled)
    {
        if (messageId == SystemMessage)
        {
            ShowChatText(message.ReadString());
            handled = true;
            return;
        }
        if (messageId != ChatMessage)
            return;

        var text = message.ReadString();
        var isServer = _server is not null &&
            (_server.IsListening || _relayConnections.Count != 0);
        if (isServer)
        {
            uint senderId = 0;
            for (var i = 0; i < _serverConnections.Count; ++i)
            {
                if (_serverConnections[i].TryGetTarget(out var item) && ReferenceEquals(item, connection))
                {
                    senderId = _serverClientIds[i];
                    break;
                }
            }

            ShowChatText($"Client {senderId}: {text}");
            using var data = new VectorBuffer();
            data.WriteString($"Client {senderId}: {text}");
            using var outgoing = new MemoryBuffer(data);
            foreach (var weakConnection in _serverConnections)
            {
                if (weakConnection.TryGetTarget(out var other) && !ReferenceEquals(other, connection))
                    other.SendMessage(ChatMessage, outgoing);
            }
        }
        else
            ShowChatText("[Server] " + text);
        handled = true;
    }

    private static (string RoomId, string RelayUrl) ParseRelay(string input)
    {
        if (input.Length == 0)
            input = "default";
        var roomId = input;
        var relayUrl = DefaultRelayUrl;
        var atPosition = input.IndexOf('@');
        if (atPosition >= 0)
        {
            roomId = input[..atPosition];
            var hostPort = input[(atPosition + 1)..];
            if (!hostPort.Contains(':'))
                hostPort += ":9876";
            relayUrl = "ws://" + hostPort;
        }
        return (roomId, relayUrl);
    }

    private static string ClassifyAddress(string address)
    {
        if (address.Length == 0)
            return "?";
        if (address == "::1" || address.StartsWith("::1%") || address.StartsWith("127."))
            return "localhost";
        if (address.StartsWith("10.") || address.StartsWith("192.168."))
            return "LAN";
        if (address.StartsWith("172."))
        {
            var parts = address[4..].Split('.');
            if (parts.Length != 0 && int.TryParse(parts[0], out var value) && value is >= 16 and <= 31)
                return "LAN";
        }
        if (address.StartsWith("fc") || address.StartsWith("fd") || address.StartsWith("fe80"))
            return "LAN";
        return "WAN";
    }

    private static void HookIceLogging(DataChannelConnection connection)
    {
        if (connection.Peer < 0)
            return;
        RtcSetStateChangeCallback(connection.Peer, IceStateCallbackPointer);
        RtcSetGatheringStateChangeCallback(connection.Peer, IceGatheringCallbackPointer);
    }

    private static void LogSelectedCandidatePair(int peer)
    {
        if (peer < 0)
            return;
        var local = ReadNativeString((buffer, size) =>
            RtcGetSelectedCandidatePair(peer, buffer, size, [], 0));
        var remote = ReadNativeString((buffer, size) =>
            RtcGetSelectedCandidatePair(peer, [], 0, buffer, size));
        if (local.Length != 0 && remote.Length != 0)
            Log($"ICE selected pair: local={CandidateType(local)}, remote={CandidateType(remote)}");
    }

    private static string CandidateType(string candidate)
    {
        if (candidate.Contains(" typ host")) return "Host";
        if (candidate.Contains(" typ srflx")) return "ServerReflexive";
        if (candidate.Contains(" typ prflx")) return "PeerReflexive";
        if (candidate.Contains(" typ relay")) return "Relayed";
        return "?";
    }

    private delegate int NativeStringGetter(Span<sbyte> buffer, int size);

    private static string ReadNativeString(NativeStringGetter getter)
    {
        var required = getter([], 0);
        if (required <= 0)
            return string.Empty;
        var buffer = new sbyte[required];
        if (getter(buffer, buffer.Length) < 0)
            return string.Empty;
        var bytes = MemoryMarshal.AsBytes(buffer.AsSpan());
        var length = bytes.IndexOf((byte)0);
        return Encoding.UTF8.GetString(length >= 0 ? bytes[..length] : bytes);
    }

    private string ReadText() => Marshal.PtrToStringUTF8(_textBuffer) ?? string.Empty;
    private void ClearText() => Marshal.WriteByte(_textBuffer, 0, 0);

    private static void Log(string message) =>
        RedBolt.Logging.Log.GetLogger().Info("{}", message);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void IceStateCallback(int peer, RtcState state, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void IceGatheringCallback(int peer, RtcGatheringState state, nint userData);

    private static void HandleIceState(int peer, RtcState state, nint userData) =>
        Log($"ICE state: {state.ToString().Replace("Rtc", string.Empty)} ({(int)state})");

    private static void HandleIceGathering(int peer, RtcGatheringState state, nint userData) =>
        Log($"ICE gathering: {state.ToString().Replace("RtcGathering", string.Empty)} ({(int)state})");

    private static readonly IceStateCallback IceStateChanged = HandleIceState;
    private static readonly nint IceStateCallbackPointer = Marshal.GetFunctionPointerForDelegate(IceStateChanged);
    private static readonly IceGatheringCallback IceGatheringChanged = HandleIceGathering;
    private static readonly nint IceGatheringCallbackPointer = Marshal.GetFunctionPointerForDelegate(IceGatheringChanged);

    private readonly List<string> _chatHistory = [];
    private readonly List<WeakReference<NetworkConnection>> _serverConnections = [];
    private readonly List<uint> _serverClientIds = [];
    private readonly List<DataChannelConnection> _relayConnections = [];
    private readonly List<RelayWebSocket> _relaySockets = [];
    private readonly Timer _retryTimer = new();
    private IntPtr _textBuffer;
    private DataChannelServer? _server;
    private DataChannelConnection? _clientConnection;
    private uint _nextClientId = 1;
    private string _retryAddress = string.Empty;
    private string _retryRelayUrl = string.Empty;
    private int _retryCount;
    private bool _wasEverConnected;
    private bool _retrying;
    private bool _isRelayMode;

    private sealed class RelayWebSocket : IDisposable
    {
        public RelayWebSocket(string roomId, string url, Action<int> paired)
        {
            _roomId = roomId;
            _paired = paired;
            _webSocket = RtcCreateWebSocket(Encoding.UTF8.GetBytes(url + '\0'));
            if (_webSocket < 0)
                return;
            _selfHandle = GCHandle.Alloc(this);
            RtcSetUserPointer(_webSocket, GCHandle.ToIntPtr(_selfHandle));
            RtcSetOpenCallback(_webSocket, OpenCallbackPointer);
            RtcSetMessageCallback(_webSocket, MessageCallbackPointer);
            RtcSetClosedCallback(_webSocket, ClosedCallbackPointer);
        }

        public void Dispose()
        {
            var webSocket = Detach();
            if (webSocket >= 0)
            {
                RtcClose(webSocket);
                RtcDeleteWebSocket(webSocket);
            }
        }

        private int Detach()
        {
            var webSocket = _webSocket;
            _webSocket = -1;
            if (webSocket >= 0)
            {
                RtcSetUserPointer(webSocket, 0);
                RtcSetOpenCallback(webSocket, 0);
                RtcSetMessageCallback(webSocket, 0);
                RtcSetClosedCallback(webSocket, 0);
            }
            if (_selfHandle.IsAllocated)
                _selfHandle.Free();
            return webSocket;
        }

        private void OnOpen()
        {
            var json = JsonSerializer.Serialize(new { type = "join", room = _roomId });
            var message = Encoding.UTF8.GetBytes(json);
            RtcSendMessage(_webSocket, message, message.Length);
        }

        private void OnMessage(nint message, int size)
        {
            var json = size < 0
                ? Marshal.PtrToStringUTF8(message) ?? string.Empty
                : Encoding.UTF8.GetString(CopyBytes(message, size));
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("type", out var type) &&
                type.GetString() == "paired")
            {
                var webSocket = Detach();
                if (webSocket >= 0)
                    _paired(webSocket);
            }
        }

        private static byte[] CopyBytes(nint source, int size)
        {
            var result = new byte[size];
            if (size != 0)
                Marshal.Copy(source, result, 0, size);
            return result;
        }

        private static RelayWebSocket? GetTarget(nint userData)
        {
            if (userData == 0)
                return null;
            try
            {
                return GCHandle.FromIntPtr(userData).Target as RelayWebSocket;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static void HandleOpen(int webSocket, nint userData)
        {
            try { GetTarget(userData)?.OnOpen(); }
            catch (Exception exception) { Console.Error.WriteLine(exception); }
        }

        private static void HandleMessage(int webSocket, nint message, int size, nint userData)
        {
            try { GetTarget(userData)?.OnMessage(message, size); }
            catch (Exception exception) { Console.Error.WriteLine(exception); }
        }

        private static void HandleClosed(int webSocket, nint userData)
        {
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void OpenCallback(int webSocket, nint userData);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MessageCallback(int webSocket, nint message, int size, nint userData);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ClosedCallback(int webSocket, nint userData);

        private static readonly OpenCallback Opened = HandleOpen;
        private static readonly nint OpenCallbackPointer = Marshal.GetFunctionPointerForDelegate(Opened);
        private static readonly MessageCallback MessageReceived = HandleMessage;
        private static readonly nint MessageCallbackPointer = Marshal.GetFunctionPointerForDelegate(MessageReceived);
        private static readonly ClosedCallback Closed = HandleClosed;
        private static readonly nint ClosedCallbackPointer = Marshal.GetFunctionPointerForDelegate(Closed);

        private readonly string _roomId;
        private readonly Action<int> _paired;
        private int _webSocket = -1;
        private GCHandle _selfHandle;
    }
}
