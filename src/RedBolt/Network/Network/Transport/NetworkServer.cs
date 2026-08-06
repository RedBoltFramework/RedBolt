//
// Copyright (c) 2017-2022 the rbfx project.
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

public abstract class NetworkServer : RedObject
{
    protected NetworkServer()
    {
        _workQueue = Context.GetSubsystem<WorkQueue>()!;
    }

    public abstract bool Listen(URL url);
    public abstract void Stop();
    public abstract bool IsListening { get; }

    public IReadOnlyList<NetworkConnection> Connections => _connections;

    public void SetConnectionFactory(Func<NetworkConnection> factory)
    {
        _connectionFactory = factory;
    }

    public NetworkConnection CreateConnection()
    {
        System.Diagnostics.Debug.Assert(_connectionFactory is not null);
        var connection = _connectionFactory!();
        AddConnection(connection);
        return connection;
    }

    public event Action<NetworkServer, NetworkConnection>? Connected;
    public event Action<NetworkServer, NetworkConnection>? Disconnected;
    public event Action<NetworkServer>? ListenStarted;
    public event Action<NetworkServer>? ListenStopped;

    protected void NotifyStopping()
    {
        Context.GetSubsystem<Network>()?.OnServerStopping(this);
    }

    protected virtual void OnConnected(NetworkConnection connection)
    {
        Connected?.Invoke(this, connection);
        var eventData = new NetworkEvents.ServerClientConnectedEvent
        {
            Server = this,
            Connection = connection,
            Address = connection.Address,
            Port = connection.Port
        };
        SendEvent(ref eventData);
    }

    protected virtual void OnDisconnected(NetworkConnection connection)
    {
        Disconnected?.Invoke(this, connection);
        var eventData = new NetworkEvents.ServerClientDisconnectedEvent
        {
            Server = this,
            Connection = connection,
            Address = connection.Address,
            Port = connection.Port
        };
        SendEvent(ref eventData);
    }

    protected virtual void OnListenStart()
    {
        ListenStarted?.Invoke(this);
        var eventData = new NetworkEvents.ServerListenStartEvent { Server = this };
        SendEvent(ref eventData);
    }

    protected virtual void OnListenStop()
    {
        ListenStopped?.Invoke(this);
        var eventData = new NetworkEvents.ServerListenStopEvent { Server = this };
        SendEvent(ref eventData);
    }

    protected internal void DoOnConnected(NetworkConnection connection)
    {
        _workQueue.PostTaskForMainThread(() => OnConnected(connection));
    }

    protected internal void DoOnDisconnected(NetworkConnection connection)
    {
        _workQueue.PostTaskForMainThread(() =>
        {
            OnDisconnected(connection);
            RemoveConnection(connection);
        });
    }

    protected void DoOnListenStart()
    {
        _workQueue.PostTaskForMainThread(OnListenStart);
    }

    protected void DoOnListenStop()
    {
        _workQueue.PostTaskForMainThread(OnListenStop);
    }

    protected void AddConnection(NetworkConnection connection)
    {
        _workQueue.PostTaskForMainThread(() => _connections.Add(connection));
    }

    protected void RemoveConnection(NetworkConnection connection)
    {
        _workQueue.PostTaskForMainThread(() => _connections.RemoveAll(item => ReferenceEquals(item, connection)));
    }

    private readonly WorkQueue _workQueue;
    private readonly List<NetworkConnection> _connections = [];
    private Func<NetworkConnection>? _connectionFactory;
}
