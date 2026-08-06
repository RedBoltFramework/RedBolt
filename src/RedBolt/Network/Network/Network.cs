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

namespace RedBolt.Network;

using RedBolt.Core;

/// <summary>Network subsystem that provides update scheduling and replication events.</summary>
public class Network : RedObject
{
    public Network()
    {
        RegisterNetworkLibrary(Context);
        SubscribeToEvent<CoreEvents.BeginFrameEvent>(HandleBeginFrame);
        SubscribeToEvent<CoreEvents.RenderUpdateEvent>(HandleRenderUpdate);
        SubscribeToEvent<NetworkEvents.ClientDisconnectedEvent>(HandleClientDisconnected);
        SubscribeToEvent<NetworkEvents.ServerClientDisconnectedEvent>(HandleServerClientDisconnected);
    }

    /// <summary>Register Network library objects.</summary>
    public static void RegisterNetworkLibrary(Context context)
    {
        context.RegisterAssembly(typeof(Network).Assembly);
    }

    public void SetUpdateFps(uint fps)
    {
        _updateFps = System.Math.Max(fps, 1);
        _updateInterval = 1.0f / _updateFps;
        _updateAccumulator = 0.0f;
    }

    public uint UpdateFps => _updateFps;
    public float UpdateOvertime => _updateAccumulator;
    public bool IsUpdateNow => _updateNow;
    public bool HasActiveResources => _stoppingServers.Count != 0 || _disconnectingConnections.Count != 0;

    public void Update(float timeStep)
    {
        _updateAccumulator += timeStep;
        _updateNow = _updateAccumulator >= _updateInterval;
        if (_updateNow)
            _updateAccumulator %= _updateInterval;

        var eventData = new NetworkEvents.NetworkInputProcessedEvent { TimeStep = timeStep };
        SendEvent(ref eventData);
    }

    public void PostUpdate(float timeStep)
    {
        if (_updateNow)
        {
            SendNetworkUpdateEvent(true);
            SendNetworkUpdateSentEvent(true);
        }

        SendNetworkUpdateEvent(false);
        SendNetworkUpdateSentEvent(false);
    }

    internal void OnConnectionDisconnecting(NetworkConnection connection)
    {
        _disconnectingConnections.Add(connection);
    }

    internal void OnServerStopping(NetworkServer server)
    {
        _stoppingServers.Add(server);
    }

    private void HandleBeginFrame(ref CoreEvents.BeginFrameEvent eventData)
    {
        Update(eventData.TimeStep);
    }

    private void HandleRenderUpdate(ref CoreEvents.RenderUpdateEvent eventData)
    {
        PostUpdate(eventData.TimeStep);
    }

    private void HandleClientDisconnected(ref NetworkEvents.ClientDisconnectedEvent eventData)
    {
        _disconnectingConnections.Remove(eventData.Connection);
    }

    private void HandleServerClientDisconnected(ref NetworkEvents.ServerClientDisconnectedEvent eventData)
    {
        _disconnectingConnections.Remove(eventData.Connection);
        if (eventData.Server.Connections.Count <= 1)
            _stoppingServers.Remove(eventData.Server);
    }

    private void SendNetworkUpdateEvent(bool isServer)
    {
        var eventData = new NetworkEvents.NetworkUpdateEvent { IsServer = isServer };
        SendEvent(ref eventData);
    }

    private void SendNetworkUpdateSentEvent(bool isServer)
    {
        var eventData = new NetworkEvents.NetworkUpdateSentEvent { IsServer = isServer };
        SendEvent(ref eventData);
    }

    private uint _updateFps = 30;
    private float _updateInterval = 1.0f / 30;
    private float _updateAccumulator;
    private bool _updateNow;

    private readonly HashSet<NetworkServer> _stoppingServers = [];
    private readonly HashSet<NetworkConnection> _disconnectingConnections = [];
}
