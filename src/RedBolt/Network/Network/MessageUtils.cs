// Copyright (c) 2025-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Network;

using System.Diagnostics;
using System.Text;

using RedBolt.IO;

public interface INetworkSerializableMessage
{
    void Load(MemoryBuffer source);
    void Save(VectorBuffer destination);
}

/// <summary>Helper to send a large message as multiple small messages.</summary>
public sealed class LargeMessageWriter : IDisposable
{
    public LargeMessageWriter(NetworkConnection connection, VectorBuffer buffer,
        NetworkMessageId incompleteMessageId, NetworkMessageId lastMessageId)
    {
        _connection = connection;
        _buffer = buffer;
        _debugInfo = connection.DebugInfoBuffer;
        _incompleteMessageId = incompleteMessageId;
        _lastMessageId = lastMessageId;

        _buffer.Clear();
        _debugInfo.Clear();
    }

    public void Discard()
    {
        _discarded = true;
    }

    public VectorBuffer Buffer => _buffer;
    public StringBuilder DebugInfo => _debugInfo;

    public void Dispose()
    {
        if (!_discarded)
        {
            var payload = _buffer.Buffer;
            var maxMessageSize = _connection.MaxMessageSize;
            var numChunks = (_buffer.Size + maxMessageSize - 1) / maxMessageSize;
            for (var i = 0u; i < numChunks; ++i)
            {
                var isLastChunk = i + 1 == numChunks;
                var messageId = isLastChunk ? _lastMessageId : _incompleteMessageId;
                var chunkOffset = i * maxMessageSize;
                var chunkSize = isLastChunk ? _buffer.Size % maxMessageSize : maxMessageSize;
                var chunkData = payload.Slice(checked((int)chunkOffset), checked((int)chunkSize));
                var chunkDebugInfo = isLastChunk ? _debugInfo.ToString() : string.Empty;
                using var memory = new MemoryBuffer(chunkData.ToArray());
                _connection.SendMessage(messageId, memory, PacketType.ReliableOrdered, chunkDebugInfo);
            }
        }
        _buffer.Clear();
        GC.SuppressFinalize(this);
    }

    private readonly NetworkConnection _connection;
    private readonly VectorBuffer _buffer;
    private readonly StringBuilder _debugInfo;
    private readonly NetworkMessageId _incompleteMessageId;
    private readonly NetworkMessageId _lastMessageId;
    private bool _discarded;
}

/// <summary>Helper to reassemble large messages.</summary>
public class LargeMessageReader
{
    public LargeMessageReader(VectorBuffer buffer,
        NetworkMessageId incompleteMessageId, NetworkMessageId lastMessageId)
    {
        _buffer = buffer;
        _incompleteMessageId = incompleteMessageId;
        _lastMessageId = lastMessageId;
    }

    public void OnMessage(NetworkMessageId messageId, MemoryBuffer messageData,
        Action<MemoryBuffer> onMessageReceived)
    {
        Debug.Assert(messageId == _incompleteMessageId || messageId == _lastMessageId);

        if (messageId == _lastMessageId && _buffer.Size == 0)
        {
            onMessageReceived(messageData);
        }
        else
        {
            _buffer.Seek(_buffer.Size);
            CopyRemaining(messageData, _buffer);
            if (messageId == _lastMessageId)
            {
                using var memoryBuffer = new MemoryBuffer(_buffer);
                onMessageReceived(memoryBuffer);
                _buffer.Clear();
            }
        }
    }

    private static void CopyRemaining(MemoryBuffer source, VectorBuffer destination)
    {
        var oldPosition = source.Position;
        var remaining = source.Size - oldPosition;
        var data = new byte[checked((int)remaining)];
        source.Read(data);
        source.Seek(oldPosition);
        destination.Write(data);
    }

    private readonly VectorBuffer _buffer;
    private readonly NetworkMessageId _incompleteMessageId;
    private readonly NetworkMessageId _lastMessageId;
}

/// <summary>Helper to send multiple messages of one type with a shared header.</summary>
public sealed class MultiMessageWriter : IDisposable
{
    public MultiMessageWriter(NetworkConnection connection, VectorBuffer buffer,
        NetworkMessageId messageId, PacketType packetType)
    {
        _connection = connection;
        _buffer = buffer;
        _debugInfo = connection.DebugInfoBuffer;
        _messageId = messageId;
        _packetType = packetType;

        _buffer.Clear();
        _debugInfo.Clear();
    }

    public void CompleteHeader()
    {
        if (_headerSize.HasValue)
        {
            Debug.Fail($"Common message header ({_headerSize.Value} bytes) is already completed");
            return;
        }

        _headerSize = _buffer.Size;
        _nextPayloadOffset = _headerSize.Value;
        Debug.Assert(_headerSize.Value <= _connection.MaxMessageSize);
    }

    public void CompletePayload()
    {
        _headerSize ??= 0;

        if (_buffer.Size > _connection.MaxMessageSize && _nextPayloadOffset != _headerSize)
        {
            SendPreviousPayloads();

            var data = _buffer.GetBuffer();
            var nextPayloadSize = (uint)data.Length - _nextPayloadOffset;
            data.AsSpan(checked((int)_nextPayloadOffset), checked((int)nextPayloadSize))
                .CopyTo(data.AsSpan(checked((int)_headerSize.Value)));
            _buffer.Resize(_headerSize.Value + nextPayloadSize);
            _debugInfo.Remove(0, checked((int)_nextDebugInfoOffset));
        }

        _nextPayloadOffset = _buffer.Size;
        _nextDebugInfoOffset = (uint)_debugInfo.Length;
    }

    public VectorBuffer Buffer => _buffer;
    public StringBuilder DebugInfo => _debugInfo;

    public void Dispose()
    {
        if (_nextPayloadOffset != _headerSize)
            SendPreviousPayloads();
        _buffer.Clear();
        _debugInfo.Clear();
        GC.SuppressFinalize(this);
    }

    private void SendPreviousPayloads()
    {
        var payload = _buffer.Buffer[..checked((int)_nextPayloadOffset)].ToArray();
        using var memory = new MemoryBuffer(payload);
        var debugInfo = _debugInfo.ToString(0, checked((int)_nextDebugInfoOffset));
        _connection.SendMessage(_messageId, memory, _packetType, debugInfo);
    }

    private readonly NetworkConnection _connection;
    private readonly VectorBuffer _buffer;
    private readonly StringBuilder _debugInfo;
    private readonly NetworkMessageId _messageId;
    private readonly PacketType _packetType;

    private uint? _headerSize;
    private uint _nextPayloadOffset;
    private uint _nextDebugInfoOffset;
}

public static class NetworkMessageUtils
{
    public static T ReadSerializedMessage<T>(MemoryBuffer source)
        where T : struct, INetworkSerializableMessage
    {
        var message = new T();
        message.Load(source);
        return message;
    }

    public static void WriteSerializedMessage<T>(NetworkConnection connection,
        NetworkMessageId messageId, T message,
        PacketType messageType = PacketType.ReliableOrdered)
        where T : struct, INetworkSerializableMessage
    {
        using var builder = connection.BeginMessage(messageId, messageType, message.ToString() ?? string.Empty);
        message.Save(builder.Buffer);
        builder.EndMessage();
    }
}
