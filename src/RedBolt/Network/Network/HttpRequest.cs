// Copyright (c) 2008-2022 the Urho3D project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Network;

using System.Net.Http;
using System.Text;

using RedBolt.IO;

public enum HttpRequestState
{
    Initializing,
    Error,
    Open,
    Closed
}

/// <summary>An HTTP connection with a non-blocking response data stream.</summary>
public sealed class HttpRequest : AbstractFile
{
    public HttpRequest(string url, string verb = "",
        IEnumerable<string>? headers = null, string postData = "")
    {
        _url = new URL(url.Trim());
        _verb = string.IsNullOrEmpty(verb) ? "GET" : verb;
        _headers = headers?.ToArray() ?? [];
        _postData = postData;
        Size = uint.MaxValue;
        _task = Task.Run(ThreadFunction);
    }

    public string URL => _url.ToString();
    public string Verb => _verb;

    public string Error
    {
        get
        {
            lock (_mutex)
                return _error;
        }
    }

    public HttpRequestState State
    {
        get
        {
            lock (_mutex)
                return _state;
        }
    }

    public uint AvailableSize
    {
        get
        {
            lock (_mutex)
                return _readBuffer.Size - _readPosition;
        }
    }

    public bool IsRequestOpen => State == HttpRequestState.Open;

    public int StatusCode
    {
        get
        {
            lock (_mutex)
                return _statusCode;
        }
    }

    public override uint Read(Span<byte> destination)
    {
        lock (_mutex)
        {
            var readSize = System.Math.Min(
                checked((int)(_readBuffer.Size - _readPosition)), destination.Length);
            _readBuffer.Buffer.Slice(checked((int)_readPosition), readSize)
                .CopyTo(destination);
            _readPosition += unchecked((uint)readSize);
            return unchecked((uint)readSize);
        }
    }

    public override uint Seek(uint position) => 0;

    public override uint Write(ReadOnlySpan<byte> data) => 0;

    public override bool IsEof()
    {
        lock (_mutex)
            return _readBuffer.Size == _readPosition;
    }

    public override void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _task?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        _cancellation.Dispose();
        _readBuffer.Dispose();
        base.Dispose();
    }

    private async Task ThreadFunction()
    {
        try
        {
            using var request = new HttpRequestMessage(
                new HttpMethod(_verb), _url.ToString());
            if (!string.IsNullOrEmpty(_postData))
                request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(_postData));

            foreach (var rawHeader in _headers)
            {
                var header = rawHeader.Trim();
                if (header.Length == 0)
                    continue;

                var separator = header.IndexOf(':');
                if (separator <= 0)
                    continue;

                var name = header[..separator].Trim();
                var value = header[(separator + 1)..].Trim();
                if (name.Length == 0 || value.Length == 0)
                    continue;

                if (!request.Headers.TryAddWithoutValidation(name, value))
                {
                    request.Content ??= new ByteArrayContent([]);
                    request.Content.Headers.TryAddWithoutValidation(name, value);
                }
            }

            using var response = await SharedClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, _cancellation.Token).ConfigureAwait(false);
            lock (_mutex)
            {
                _statusCode = (int)response.StatusCode;
                _state = HttpRequestState.Open;
            }

            await using var responseStream = await response.Content
                .ReadAsStreamAsync(_cancellation.Token).ConfigureAwait(false);
            var chunk = new byte[ReadBufferSize];
            while (true)
            {
                var bytesRead = await responseStream.ReadAsync(chunk, _cancellation.Token)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                    break;

                lock (_mutex)
                {
                    _readBuffer.Seek(_readBuffer.Size);
                    _readBuffer.Write(chunk.AsSpan(0, bytesRead));
                }
            }

            lock (_mutex)
                _state = HttpRequestState.Closed;
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            lock (_mutex)
                _state = HttpRequestState.Closed;
        }
        catch (Exception exception)
        {
            lock (_mutex)
            {
                _error = exception.Message;
                _state = HttpRequestState.Error;
            }
        }
    }

    private const int ReadBufferSize = 1024;
    private static readonly HttpClient SharedClient = new();

    private readonly URL _url;
    private readonly string _verb;
    private readonly string[] _headers;
    private readonly string _postData;
    private readonly object _mutex = new();
    private readonly VectorBuffer _readBuffer = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _task;
    private uint _readPosition;
    private string _error = string.Empty;
    private HttpRequestState _state = HttpRequestState.Initializing;
    private int _statusCode;
}
