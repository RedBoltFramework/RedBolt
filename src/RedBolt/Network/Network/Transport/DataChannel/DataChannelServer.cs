// Copyright (c) 2017-2022 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Network;

using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

using RedBolt.Bindings.LibDataChannel;
using RedBolt.Core;

using static RedBolt.Bindings.LibDataChannel.LibDataChannelNativeBindings;

[Reflected(ObjectCategory.Category_Network, Factory = false, InheritAttributes = false)]
public partial class DataChannelServer : NetworkServer
{
    public DataChannelServer()
    {
        SetConnectionFactory(static () => new DataChannelConnection());
    }

    public override bool Listen(URL url)
    {
        if (IsListening)
        {
            RedBolt.Logging.Log.GetLogger().LogError(
                "DataChannelServer::Listen called while already listening.");
            return false;
        }

        var enableTls = url.Scheme == "wss";
        if (enableTls && (string.IsNullOrEmpty(_certificatePemFile) || string.IsNullOrEmpty(_keyPemFile)))
        {
            RedBolt.Logging.Log.GetLogger().LogError(
                "Server requires TLS support, but certificate and/or key were not provided.");
            return false;
        }

        using var certificate = new NativeUtf8String(_certificatePemFile);
        using var key = new NativeUtf8String(_keyPemFile);
        using var password = new NativeUtf8String(_keyPassword);
        using var config = new RtcWsServerConfiguration
        {
            EnableTls = enableTls,
            CertificatePemFile = certificate.Pointer,
            KeyPemFile = key.Pointer,
            KeyPemPass = password.Pointer,
            Port = url.Port
        };

        EnsureSelfHandle();
        _webSocketServer = RtcCreateWebSocketServer(config, WebSocketClientCallbackPointer);
        if (_webSocketServer < 0)
        {
            FreeSelfHandle();
            return false;
        }

        RtcSetUserPointer(_webSocketServer, GCHandle.ToIntPtr(_selfHandle));
        DoOnListenStart();
        return true;
    }

    public override void Stop()
    {
        if (!IsListening)
            return;

        NotifyStopping();

        foreach (var connection in Connections.ToArray())
            connection.Disconnect();

        var webSocketServer = _webSocketServer;
        _webSocketServer = InvalidId;
        RtcSetUserPointer(webSocketServer, 0);
        RtcDeleteWebSocketServer(webSocketServer);
        FreeSelfHandle();
        DoOnListenStop();
    }

    public override bool IsListening => _webSocketServer >= 0;

    public void SetTLSCertificate(
        string certificatePemFile, string keyPemFile, string keyPassword)
    {
        _certificatePemFile = certificatePemFile;
        _keyPemFile = keyPemFile;
        _keyPassword = keyPassword;
    }

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

    public override void Dispose()
    {
        Stop();
        base.Dispose();
    }

    private void OnWebSocketClient(int webSocket)
    {
        var connection = CreateConnection() as DataChannelConnection;
        System.Diagnostics.Debug.Assert(connection is not null);
        if (connection is null)
        {
            RtcDeleteWebSocket(webSocket);
            return;
        }

        connection.SetIceServers(_iceServers);
        connection.SetPortRange(_portRangeBegin, _portRangeEnd);
        connection.SetIceUdpMux(_enableIceUdpMux);
        connection.SetIceTransportPolicy(_iceTransportPolicy);
        if (!string.IsNullOrEmpty(_bindAddress))
            connection.SetBindAddress(_bindAddress);
        if (_mtu > 0)
            connection.SetMtu(unchecked((nuint)_mtu));
        connection.InitializeFromSocket(this, webSocket);
    }

    private void EnsureSelfHandle()
    {
        if (!_selfHandle.IsAllocated)
            _selfHandle = GCHandle.Alloc(this);
    }

    private void FreeSelfHandle()
    {
        if (_selfHandle.IsAllocated)
            _selfHandle.Free();
    }

    private static void HandleWebSocketClient(int server, int webSocket, nint userData) =>
        DataChannelInterop.Guard(() => DataChannelInterop.GetTarget<DataChannelServer>(userData)?
            .OnWebSocketClient(webSocket));

    private const int InvalidId = -1;

    private static readonly DataChannelInterop.WebSocketClientCallback WebSocketClientCallback =
        HandleWebSocketClient;
    private static readonly nint WebSocketClientCallbackPointer =
        Marshal.GetFunctionPointerForDelegate(WebSocketClientCallback);

    private int _webSocketServer = InvalidId;
    private string _certificatePemFile = string.Empty;
    private string _keyPemFile = string.Empty;
    private string _keyPassword = string.Empty;
    private readonly List<string> _iceServers = [];
    private ushort _portRangeBegin = 1024;
    private ushort _portRangeEnd = 65535;
    private bool _enableIceUdpMux;
    private RtcTransportPolicy _iceTransportPolicy = RtcTransportPolicy.RtcTransportPolicyAll;
    private string _bindAddress = string.Empty;
    private int _mtu;
    private GCHandle _selfHandle;
}
