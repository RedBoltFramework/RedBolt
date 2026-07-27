using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using SystemMath = global::System.Math;
using System.Threading;
using System.Threading.Tasks;

using RedBolt.Logging;

namespace RedBolt.IO;

/// <summary>Named pipe for interprocess communication.</summary>
public class NamedPipe : AbstractFile
{
    /// <summary>Construct.</summary>
    public NamedPipe()
    {
        _isServer = false;
    }

    /// <summary>Construct and open in either server or client mode.</summary>
    public NamedPipe(string name, bool isServer)
        : this()
    {
        Open(name, isServer);
    }

    /// <summary>Read bytes from the pipe without blocking if there is less data available.</summary>
    public override uint Read(Span<byte> destination)
    {
        lock (_readMutex)
        {
            var count = SystemMath.Min(destination.Length, _readBuffer.Count);
            for (var i = 0; i < count; ++i)
                destination[i] = _readBuffer.Dequeue();
            return (uint)count;
        }
    }

    /// <summary>Set position.</summary>
    public override uint Seek(uint position)
    {
        return 0;
    }

    /// <summary>Write bytes to the pipe.</summary>
    public override uint Write(ReadOnlySpan<byte> data)
    {
        if (_pipe is null || !_connected)
            return 0;

        try
        {
            _pipe.Write(data);
            _pipe.Flush();
            return (uint)data.Length;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (ObjectDisposedException)
        {
            return 0;
        }
    }

    /// <summary>Return whether pipe has no data available.</summary>
    public override bool IsEof()
    {
        lock (_readMutex)
        {
            return _readBuffer.Count == 0;
        }
    }

    /// <summary>Not supported.</summary>
    public override void SetName(string name)
    {
        Log.GetLogger().LogError("Cannot change name of the NamedPipe!");
        Debug.Assert(false);
    }

    /// <summary>Open the pipe in either server or client mode.</summary>
    public bool Open(string name, bool isServer)
    {
        Close();

        _isServer = false;
        _cancellation = new CancellationTokenSource();

        try
        {
            if (isServer)
            {
                var server = new NamedPipeServerStream(
                    name,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                _pipe = server;
                _name = name;
                _isServer = true;
                _readTask = ReadLoopAsync(server, true, _cancellation.Token);
                return true;
            }
            else
            {
                var client = new NamedPipeClientStream(
                    ".",
                    name,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                client.Connect(0);
                _pipe = client;
                _name = name;
                _readTask = ReadLoopAsync(client, false, _cancellation.Token);
                return true;
            }
        }
        catch (TimeoutException)
        {
            Close();
            return false;
        }
        catch (IOException)
        {
            Close();
            return false;
        }
        catch (ArgumentException)
        {
            Close();
            return false;
        }
    }

    /// <summary>Close the pipe. Note that once a client has disconnected, the server needs to close and reopen the pipe so that another client can connect.</summary>
    public override void Close()
    {
        _cancellation?.Cancel();
        _connected = false;

        if (_pipe is not null)
        {
            _pipe.Dispose();
            _pipe = null;
        }

        if (_readTask is not null)
        {
            _readTask.GetAwaiter().GetResult();
            _readTask = null;
        }

        lock (_readMutex)
            _readBuffer.Clear();

        _cancellation?.Dispose();
        _cancellation = null;
        _isServer = false;
        _name = string.Empty;
    }

    /// <summary>Return whether is open.</summary>
    public override bool IsOpen => _pipe is not null;

    /// <summary>Return whether is in server mode.</summary>
    public bool IsServer()
    {
        return _isServer;
    }

    public override void Dispose()
    {
        Close();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task ReadLoopAsync(PipeStream pipe, bool isServer, CancellationToken cancellationToken)
    {
        try
        {
            if (isServer)
                await ((NamedPipeServerStream)pipe).WaitForConnectionAsync(cancellationToken);

            _connected = true;
            var buffer = new byte[4096];
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await pipe.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                    break;

                lock (_readMutex)
                {
                    for (var i = 0; i < read; ++i)
                        _readBuffer.Enqueue(buffer[i]);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _connected = false;
        }
    }

    private bool _isServer;
    private PipeStream? _pipe;
    private bool _connected;
    private readonly Queue<byte> _readBuffer = new();
    private readonly object _readMutex = new();
    private CancellationTokenSource? _cancellation;
    private Task? _readTask;
}
