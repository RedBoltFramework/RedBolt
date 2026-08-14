// Copyright (c) 2017-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Network;

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

using RedBolt.Bindings.LibDataChannel;
using RedBolt.Core;
using RedBolt.IO;

using static RedBolt.Bindings.LibDataChannel.LibDataChannel;

[Reflected(ObjectCategory.Category_Network, Factory = false, InheritAttributes = false)]
public partial class DataChannelConnection : NetworkConnection
{
    public DataChannelConnection()
    {
        Array.Fill(_dataChannels, InvalidId);
    }

    public override bool Connect(URL url)
    {
        if (!IsDisconnected)
        {
            RedBolt.Logging.Log.GetLogger().LogError(
                "DataChannelConnection::Connect called while not in Disconnected state.");
            return false;
        }

        var finalUrl = url;
        if (string.IsNullOrEmpty(finalUrl.Scheme))
            finalUrl.Scheme = "ws";

        using var config = new RtcWsConfiguration
        {
            DisableTlsVerification = finalUrl.Scheme == "ws"
        };
        var webSocket = RtcCreateWebSocketEx(DataChannelInterop.Utf8(finalUrl.ToString()), config);
        if (webSocket < 0)
            return false;

        InitializeFromSocket(null, webSocket);
        return true;
    }

    public override void Disconnect()
    {
        if (_state is NetworkConnectionState.Disconnected or NetworkConnectionState.Disconnecting)
            return;

        base.Disconnect();
        _state = NetworkConnectionState.Disconnecting;

        if (_peer >= 0)
        {
            ResetPeerCallbacks(_peer);
            RtcClosePeerConnection(_peer);
        }
        else
            OnDataChannelDisconnected(0, false);
    }

    public override bool SendData(MemoryBuffer data,
        PacketType type = PacketType.ReliableOrdered)
    {
        if (_state != NetworkConnectionState.Connected)
        {
            RedBolt.Logging.Log.GetLogger().LogDebug(
                "Network message was not sent: connection is not connected.");
            return false;
        }

        var dataSize = data.Size - data.Position;
        if (dataSize > MaxMessageSize + NetworkProtocol.NetworkMessageHeaderSize)
        {
            RedBolt.Logging.Log.GetLogger().LogError(
                "DataChannel tried to send {DataSize} bytes, which is more than max allowed {MaxSize} bytes of data per message.",
                dataSize, MaxMessageSize + NetworkProtocol.NetworkMessageHeaderSize);
            return false;
        }

        if (!base.SendData(data, type))
            return false;

        var index = (int)type;
        var dataChannel = index < _dataChannels.Length ? _dataChannels[index] : InvalidId;
        if (dataChannel >= 0)
        {
            if (RtcIsOpen(dataChannel))
            {
                var payload = ReadRemaining(data);
                return RtcSendMessage(dataChannel, payload, payload.Length) >= 0;
            }
            RedBolt.Logging.Log.GetLogger().LogDebug(
                "DataChannel {PacketType} is not open yet, message dropped.", index);
        }
        else
        {
            RedBolt.Logging.Log.GetLogger().LogError(
                "DataChannel {PacketType} is not connected!", index);
            Disconnect();
        }

        return false;
    }

    public override uint MaxMessageSize =>
        _state == NetworkConnectionState.Connected && _dataChannels[0] >= 0
            ? unchecked(MaxPacketSize - (uint)NetworkProtocol.NetworkMessageHeaderSize)
            : 0;

    public void SetIceServers(IEnumerable<string> servers)
    {
        _iceServers.Clear();
        _iceServers.AddRange(servers);
    }

    public void SetPortRange(ushort begin, ushort end)
    {
        _portRangeBegin = begin;
        _portRangeEnd = end;
    }

    public void SetIceUdpMux(bool enable) => _enableIceUdpMux = enable;

    public void SetIceTransportPolicy(RtcTransportPolicy policy) =>
        _iceTransportPolicy = policy;

    public void SetBindAddress(string address) => _bindAddress = address;

    public void SetMtu(nuint mtu) => _mtu = checked((int)mtu);

    public int Peer => _peer;

    public void InitializeWithWebSocket(int webSocket, DataChannelServer? server = null)
    {
        InitializeFromSocket(server, webSocket);
        _webSocketWasOpened = true;
    }

    public override void Dispose()
    {
        if (_state is NetworkConnectionState.Connected or NetworkConnectionState.Connecting)
        {
            RedBolt.Logging.Log.GetLogger().LogWarning(
                "DataChannelConnection was not properly disconnected before destruction. Forcing disconnect.");
        }

        ReleaseNativeHandles();
        base.Dispose();
    }

    internal void InitializeFromSocket(DataChannelServer? server, int webSocket)
    {
        SetServer(server);
        _state = NetworkConnectionState.Connecting;
        _webSocket = webSocket;
        _webSocketWasOpened = server is not null;
        EnsureSelfHandle();

        using var iceServers = new NativeUtf8StringArray(_iceServers);
        using var bindAddress = new NativeUtf8String(_bindAddress);
        using var config = new RtcConfiguration
        {
            PortRangeBegin = _portRangeBegin,
            PortRangeEnd = _portRangeEnd,
            EnableIceUdpMux = _enableIceUdpMux,
            IceTransportPolicy = _iceTransportPolicy,
            BindAddress = bindAddress.Pointer,
            Mtu = _mtu
        };
        iceServers.Apply(config);
        _peer = RtcCreatePeerConnection(config);
        if (_peer < 0)
        {
            OnDataChannelDisconnected(0, false);
            return;
        }

        var userData = GCHandle.ToIntPtr(_selfHandle);
        RtcSetUserPointer(_peer, userData);
        RtcSetLocalDescriptionCallback(_peer, LocalDescriptionCallbackPointer);
        RtcSetLocalCandidateCallback(_peer, LocalCandidateCallbackPointer);

        if (server is not null)
            CreateServerDataChannels(userData);

        RtcSetDataChannelCallback(_peer, DataChannelCallbackPointer);
        RtcSetUserPointer(_webSocket, userData);
        RtcSetOpenCallback(_webSocket, WebSocketOpenCallbackPointer);
        RtcSetMessageCallback(_webSocket, MessageCallbackPointer);
        RtcSetClosedCallback(_webSocket, WebSocketClosedCallbackPointer);
    }

    private void CreateServerDataChannels(nint userData)
    {
        _dataChannels[(int)PacketType.UnreliableUnordered] = CreateDataChannel(
            "uu", unordered: true, unreliable: true);
        _dataChannels[(int)PacketType.ReliableUnordered] = CreateDataChannel(
            "ru", unordered: true, unreliable: false);
        _dataChannels[(int)PacketType.UnreliableOrdered] = CreateDataChannel(
            "uo", unordered: false, unreliable: true);
        _dataChannels[(int)PacketType.ReliableOrdered] = CreateDataChannel(
            "ro", unordered: false, unreliable: false);

        for (var i = 0; i < _dataChannels.Length; ++i)
            InitializeDataChannel(i, _dataChannels[i], userData);
    }

    private int CreateDataChannel(string label, bool unordered, bool unreliable)
    {
        using var init = new RtcDataChannelInit
        {
            ReliabilityUnordered = unordered,
            ReliabilityUnreliable = unreliable
        };
        return RtcCreateDataChannelEx(_peer, DataChannelInterop.Utf8(label), init);
    }

    private void InitializeDataChannel(int index, int dataChannel, nint userData)
    {
        if (dataChannel < 0)
            return;

        RtcSetUserPointer(dataChannel, userData);
        RtcSetOpenCallback(dataChannel, DataChannelOpenCallbackPointer);
        RtcSetClosedCallback(dataChannel, DataChannelClosedCallbackPointer);
        RtcSetMessageCallback(dataChannel, MessageCallbackPointer);
        _dataChannelIndices[dataChannel] = index;
    }

    private void OnDataChannelConnected(int index)
    {
        if (_state == NetworkConnectionState.Connected)
            return;

        foreach (var dataChannel in _dataChannels)
        {
            if (dataChannel < 0 || !RtcIsOpen(dataChannel))
                return;
        }

        _address = ReadNativeString((buffer, size) => RtcGetRemoteAddress(_peer, buffer, size));
        DoOnConnected();

        if (Server is DataChannelServer server)
            server.DoOnConnected(this);

        CloseWebSocket();
    }

    private void OnDataChannelDisconnected(int index, bool notifyCallbacks = true)
    {
        if (_state == NetworkConnectionState.Disconnected)
            return;

        _state = NetworkConnectionState.Disconnecting;
        if ((uint)index < (uint)_dataChannels.Length)
            DeleteDataChannel(index);

        foreach (var dataChannel in _dataChannels)
        {
            if (dataChannel >= 0 && _state != NetworkConnectionState.Connecting)
                return;
        }

        if (notifyCallbacks)
            DoOnDisconnected();

        if (Server is DataChannelServer server)
            server.DoOnDisconnected(this);

        CloseWebSocket();
        ClosePeer();
        SetServer(null);
    }

    private void OnDataChannelReceived(nint message, int size)
    {
        if (size < 0)
            return;

        using var data = new MemoryBuffer(DataChannelInterop.CopyBytes(message, size));
        DoOnData(data);
    }

    private void OnWebSocketMessage(nint message, int size)
    {
        if (size < 0)
            return;

        using var data = new MemoryBuffer(DataChannelInterop.CopyBytes(message, size));
        var type = data.ReadString();
        if (type is "offer" or "answer")
        {
            var sdp = data.ReadString();
            RtcSetRemoteDescription(_peer, DataChannelInterop.Utf8(sdp), DataChannelInterop.Utf8(type));
        }
        else if (type == "candidate")
        {
            var candidate = data.ReadString();
            var mid = data.ReadString();
            RtcAddRemoteCandidate(_peer,
                DataChannelInterop.Utf8(candidate), DataChannelInterop.Utf8(mid));
        }
    }

    private void SendDescription(string type, string sdp)
    {
        if (_webSocket < 0)
            return;

        using var message = new VectorBuffer();
        message.WriteString(type);
        message.WriteString(sdp);
        RtcSendMessage(_webSocket, message.Buffer, checked((int)message.Size));
    }

    private void SendCandidate(string candidate, string mid)
    {
        if (_webSocket < 0)
            return;

        using var message = new VectorBuffer();
        message.WriteString("candidate");
        message.WriteString(candidate);
        message.WriteString(mid);
        RtcSendMessage(_webSocket, message.Buffer, checked((int)message.Size));
    }

    private void EnsureSelfHandle()
    {
        if (!_selfHandle.IsAllocated)
            _selfHandle = GCHandle.Alloc(this);
    }

    private void DeleteDataChannel(int index)
    {
        var dataChannel = _dataChannels[index];
        _dataChannels[index] = InvalidId;
        if (dataChannel < 0)
            return;

        _dataChannelIndices.Remove(dataChannel);
        RtcSetUserPointer(dataChannel, 0);
        RtcSetOpenCallback(dataChannel, 0);
        RtcSetClosedCallback(dataChannel, 0);
        RtcSetMessageCallback(dataChannel, 0);
        RtcDeleteDataChannel(dataChannel);
    }

    private void CloseWebSocket()
    {
        var webSocket = _webSocket;
        _webSocket = InvalidId;
        if (webSocket < 0)
            return;

        RtcSetUserPointer(webSocket, 0);
        RtcSetOpenCallback(webSocket, 0);
        RtcSetClosedCallback(webSocket, 0);
        RtcSetMessageCallback(webSocket, 0);
        RtcClose(webSocket);
        RtcDeleteWebSocket(webSocket);
    }

    private void ClosePeer()
    {
        var peer = _peer;
        _peer = InvalidId;
        if (peer < 0)
            return;

        RtcSetUserPointer(peer, 0);
        ResetPeerCallbacks(peer);
        RtcClosePeerConnection(peer);
        RtcDeletePeerConnection(peer);
    }

    private static void ResetPeerCallbacks(int peer)
    {
        RtcSetLocalDescriptionCallback(peer, 0);
        RtcSetLocalCandidateCallback(peer, 0);
        RtcSetDataChannelCallback(peer, 0);
    }

    private void ReleaseNativeHandles()
    {
        for (var i = 0; i < _dataChannels.Length; ++i)
            DeleteDataChannel(i);
        CloseWebSocket();
        ClosePeer();
        if (_selfHandle.IsAllocated)
            _selfHandle.Free();
        _state = NetworkConnectionState.Disconnected;
    }

    private static byte[] ReadRemaining(MemoryBuffer data)
    {
        var oldPosition = data.Position;
        var result = new byte[checked((int)(data.Size - oldPosition))];
        data.Read(result);
        data.Seek(oldPosition);
        return result;
    }

    private delegate int StringGetter(Span<sbyte> buffer, int size);

    private static string ReadNativeString(StringGetter getter)
    {
        var required = getter([], 0);
        if (required <= 0)
            return string.Empty;

        var buffer = new sbyte[required];
        if (getter(buffer, buffer.Length) < 0)
            return string.Empty;
        var bytes = MemoryMarshal.AsBytes(buffer.AsSpan());
        var length = bytes.IndexOf((byte)0);
        return System.Text.Encoding.UTF8.GetString(length >= 0 ? bytes[..length] : bytes);
    }

    private static void HandleLocalDescription(int peer, nint sdp, nint type, nint userData) =>
        DataChannelInterop.Guard(() => DataChannelInterop.GetTarget<DataChannelConnection>(userData)?
            .SendDescription(DataChannelInterop.StringFromUtf8(type), DataChannelInterop.StringFromUtf8(sdp)));

    private static void HandleLocalCandidate(int peer, nint candidate, nint mid, nint userData) =>
        DataChannelInterop.Guard(() => DataChannelInterop.GetTarget<DataChannelConnection>(userData)?
            .SendCandidate(DataChannelInterop.StringFromUtf8(candidate), DataChannelInterop.StringFromUtf8(mid)));

    private static void HandleDataChannel(int peer, int dataChannel, nint userData) =>
        DataChannelInterop.Guard(() =>
        {
            var self = DataChannelInterop.GetTarget<DataChannelConnection>(userData);
            if (self is null)
                return;

            using var reliability = new RtcReliability();
            if (RtcGetDataChannelReliability(dataChannel, reliability) < 0)
                return;

            var packetType = PacketType.UnreliableUnordered;
            if (!reliability.Unreliable)
                packetType |= PacketType.Reliable;
            if (!reliability.Unordered)
                packetType |= PacketType.Ordered;
            var index = (int)packetType;
            Debug.Assert(self._dataChannels[index] < 0);
            self._dataChannels[index] = dataChannel;
            self.InitializeDataChannel(index, dataChannel, userData);
        });

    private static void HandleDataChannelOpen(int dataChannel, nint userData) =>
        DataChannelInterop.Guard(() =>
        {
            var self = DataChannelInterop.GetTarget<DataChannelConnection>(userData);
            if (self is not null && self._dataChannelIndices.TryGetValue(dataChannel, out var index))
                self.OnDataChannelConnected(index);
        });

    private static void HandleDataChannelClosed(int dataChannel, nint userData) =>
        DataChannelInterop.Guard(() =>
        {
            var self = DataChannelInterop.GetTarget<DataChannelConnection>(userData);
            if (self is not null && self._dataChannelIndices.TryGetValue(dataChannel, out var index))
                self.OnDataChannelDisconnected(index);
        });

    private static void HandleMessage(int id, nint message, int size, nint userData) =>
        DataChannelInterop.Guard(() =>
        {
            var self = DataChannelInterop.GetTarget<DataChannelConnection>(userData);
            if (self is null)
                return;
            if (id == self._webSocket)
                self.OnWebSocketMessage(message, size);
            else
                self.OnDataChannelReceived(message, size);
        });

    private static void HandleWebSocketOpen(int webSocket, nint userData) =>
        DataChannelInterop.Guard(() =>
        {
            var self = DataChannelInterop.GetTarget<DataChannelConnection>(userData);
            if (self is not null)
                self._webSocketWasOpened = true;
        });

    private static void HandleWebSocketClosed(int webSocket, nint userData) =>
        DataChannelInterop.Guard(() =>
        {
            var self = DataChannelInterop.GetTarget<DataChannelConnection>(userData);
            if (self is not null && !self._webSocketWasOpened)
            {
                self.OnDataChannelDisconnected(0, false);
                RedBolt.Logging.Log.GetLogger().LogDebug(
                    "Websocket failed to connect. Signaling server may be offline.");
            }
        });

    private const int InvalidId = -1;

    private static readonly DataChannelInterop.DescriptionCallback LocalDescriptionCallback = HandleLocalDescription;
    private static readonly nint LocalDescriptionCallbackPointer = Marshal.GetFunctionPointerForDelegate(LocalDescriptionCallback);
    private static readonly DataChannelInterop.CandidateCallback LocalCandidateCallback = HandleLocalCandidate;
    private static readonly nint LocalCandidateCallbackPointer = Marshal.GetFunctionPointerForDelegate(LocalCandidateCallback);
    private static readonly DataChannelInterop.DataChannelCallback DataChannelCallback = HandleDataChannel;
    private static readonly nint DataChannelCallbackPointer = Marshal.GetFunctionPointerForDelegate(DataChannelCallback);
    private static readonly DataChannelInterop.OpenCallback DataChannelOpenCallback = HandleDataChannelOpen;
    private static readonly nint DataChannelOpenCallbackPointer = Marshal.GetFunctionPointerForDelegate(DataChannelOpenCallback);
    private static readonly DataChannelInterop.ClosedCallback DataChannelClosedCallback = HandleDataChannelClosed;
    private static readonly nint DataChannelClosedCallbackPointer = Marshal.GetFunctionPointerForDelegate(DataChannelClosedCallback);
    private static readonly DataChannelInterop.MessageCallback MessageCallback = HandleMessage;
    private static readonly nint MessageCallbackPointer = Marshal.GetFunctionPointerForDelegate(MessageCallback);
    private static readonly DataChannelInterop.OpenCallback WebSocketOpenCallback = HandleWebSocketOpen;
    private static readonly nint WebSocketOpenCallbackPointer = Marshal.GetFunctionPointerForDelegate(WebSocketOpenCallback);
    private static readonly DataChannelInterop.ClosedCallback WebSocketClosedCallback = HandleWebSocketClosed;
    private static readonly nint WebSocketClosedCallbackPointer = Marshal.GetFunctionPointerForDelegate(WebSocketClosedCallback);

    private int _webSocket = InvalidId;
    private int _peer = InvalidId;
    private readonly int[] _dataChannels = new int[4];
    private readonly Dictionary<int, int> _dataChannelIndices = [];
    private bool _webSocketWasOpened;
    private readonly List<string> _iceServers = [];
    private ushort _portRangeBegin = 1024;
    private ushort _portRangeEnd = 65535;
    private bool _enableIceUdpMux;
    private RtcTransportPolicy _iceTransportPolicy = RtcTransportPolicy.RtcTransportPolicyAll;
    private string _bindAddress = string.Empty;
    private int _mtu;
    private GCHandle _selfHandle;
}
