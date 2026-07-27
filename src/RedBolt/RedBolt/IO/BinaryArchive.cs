using SystemMath = global::System.Math;

using RedBolt.Core;

namespace RedBolt.IO;

/// <summary>Binary output archive block.</summary>
public class BinaryOutputArchiveBlock : ArchiveBlockBase, IDisposable
{
    public BinaryOutputArchiveBlock(string name, ArchiveBlockType type, Serializer parentSerializer, bool safe)
        : base(name, type)
    {
        _parentSerializer = parentSerializer;
        if (safe)
            _outputBuffer = new VectorBuffer();
    }

    public Serializer GetSerializer()
    {
        return _outputBuffer ?? _parentSerializer;
    }

    public bool IsUnorderedAccessSupported() => false;

    public bool HasElementOrBlock(string name) => false;

    public void Close(ArchiveBase archive)
    {
        if (_outputBuffer is null)
        {
            System.Diagnostics.Debug.Assert(!HasOpenInlineBlock);
            return;
        }

        var size = _outputBuffer.Size;
        if (_parentSerializer.WriteVLE(size)
            && _parentSerializer.Write(_outputBuffer.GetBuffer().AsSpan(0, checked((int)size))) == size)
        {
            return;
        }

        throw archive.IoFailureException("<block guard>");
    }

    public void Dispose()
    {
        _outputBuffer?.Dispose();
    }

    /// <summary>For safe blocks only.</summary>
    private readonly VectorBuffer? _outputBuffer;
    private readonly Serializer _parentSerializer;
}

/// <summary>Binary output archive.</summary>
public class BinaryOutputArchive : ArchiveBase
{
    private readonly List<BinaryOutputArchiveBlock> _stack = [];
    private readonly Serializer _serializer;
    /// <summary>Serializer used within currently open block.</summary>
    private Serializer? _currentBlockSerializer;

    public BinaryOutputArchive(Context context, Serializer serializer)
        : base(context)
    {
        _serializer = serializer;
    }

    public override void Dispose()
    {
        for (var i = _stack.Count - 1; i >= 0; --i)
            _stack[i].Dispose();
        _stack.Clear();
        _currentBlockSerializer = null;
        base.Dispose();
    }

    public sealed override string Name => _serializer is Deserializer deserializer ? deserializer.Name : string.Empty;
    public sealed override uint Checksum => _serializer is Deserializer deserializer ? deserializer.GetChecksum() : 0;
    public sealed override bool IsInput => false;
    public sealed override bool IsHumanReadable => false;
    public sealed override bool IsUnorderedAccessSupportedInCurrentBlock =>
        _stack.Count != 0 && _stack[^1].IsUnorderedAccessSupported();
    public sealed override string CurrentBlockPath => GetCurrentBlockPath();
    public sealed override bool HasElementOrBlock(string name)
    {
        System.Diagnostics.Debug.Assert(false, "Archive::HasElementOrBlock is not supported for Output Archive");
        return false;
    }

    public sealed override void BeginBlock(string name, ref int sizeHint, bool safe, ArchiveBlockType type)
    {
        FlushDelayedException();
        CheckIfNotEof(name);

        if (_stack.Count == 0)
        {
            _stack.Add(new BinaryOutputArchiveBlock(name, type, _serializer, safe));
            _currentBlockSerializer = _stack[^1].GetSerializer();
        }
        else if (safe)
        {
            _stack.Add(new BinaryOutputArchiveBlock(name, type, _stack[^1].GetSerializer(), true));
            _currentBlockSerializer = _stack[^1].GetSerializer();
        }
        else
            _stack[^1].OpenInlineBlock();

        if (type == ArchiveBlockType.Array)
        {
            if (!_currentBlockSerializer!.WriteVLE(checked((uint)sizeHint)))
            {
                EndBlock();
                throw IoFailureException("<block guard>");
            }
        }
    }

    public sealed override void EndBlock()
    {
        System.Diagnostics.Debug.Assert(_stack.Count != 0);

        var frame = _stack[^1];
        if (frame.HasOpenInlineBlock)
        {
            frame.CloseInlineBlock();
            return;
        }

        try
        {
            frame.Close(this);
        }
        catch (InvalidOperationException exception)
        {
            SetDelayedException(exception);
        }

        _stack.RemoveAt(_stack.Count - 1);
        frame.Dispose();
        if (_stack.Count == 0)
            CloseArchive();
        _currentBlockSerializer = _stack.Count == 0 ? null : _stack[^1].GetSerializer();
    }

    public sealed override void Serialize(string name, ref bool value) { CheckElement(name); WriteResult(_currentBlockSerializer!.WriteBool(value), name); }
    public sealed override void Serialize(string name, ref sbyte value) { CheckElement(name); WriteResult(_currentBlockSerializer!.WriteByte(value), name); }
    public sealed override void Serialize(string name, ref byte value) { CheckElement(name); WriteResult(_currentBlockSerializer!.WriteUByte(value), name); }
    public sealed override void Serialize(string name, ref short value) { CheckElement(name); WriteResult(_currentBlockSerializer!.WriteShort(value), name); }
    public sealed override void Serialize(string name, ref ushort value) { CheckElement(name); WriteResult(_currentBlockSerializer!.WriteUShort(value), name); }
    public sealed override void Serialize(string name, ref int value) { CheckElement(name); WriteResult(_currentBlockSerializer!.WriteInt(value), name); }
    public sealed override void Serialize(string name, ref uint value) { CheckElement(name); WriteResult(_currentBlockSerializer!.WriteUInt(value), name); }
    public sealed override void Serialize(string name, ref long value) { CheckElement(name); WriteResult(_currentBlockSerializer!.WriteInt64(value), name); }
    public sealed override void Serialize(string name, ref ulong value) { CheckElement(name); WriteResult(_currentBlockSerializer!.WriteUInt64(value), name); }
    public sealed override void Serialize(string name, ref float value) { CheckElement(name); WriteResult(_currentBlockSerializer!.WriteFloat(value), name); }
    public sealed override void Serialize(string name, ref double value) { CheckElement(name); WriteResult(_currentBlockSerializer!.WriteDouble(value), name); }
    public sealed override void Serialize(string name, ref string value) { CheckElement(name); WriteResult(_currentBlockSerializer!.WriteString(value), name); }

    public sealed override void SerializeBytes(string name, Span<byte> bytes)
    {
        CheckElement(name);
        WriteResult(_currentBlockSerializer!.Write(bytes) == bytes.Length, name);
    }

    public sealed override void SerializeVle(string name, ref uint value)
    {
        CheckElement(name);
        WriteResult(_currentBlockSerializer!.WriteVLE(value), name);
    }

    private string GetCurrentBlockPath()
    {
        var result = new System.Text.StringBuilder();
        foreach (var frame in _stack)
        {
            if (result.Length != 0)
                result.Append('/');
            result.Append(frame.Name);
            if (frame.HasOpenInlineBlock)
                result.Append("/?");
        }
        return result.ToString();
    }

    private void CheckElement(string name)
    {
        FlushDelayedException();
        CheckIfNotEof(name);
        System.Diagnostics.Debug.Assert(_stack.Count != 0, "Root block must be opened before serialization");
    }

    private void WriteResult(bool result, string name)
    {
        if (!result)
            throw IoFailureException(name);
    }
}

/// <summary>Binary input archive block.</summary>
public class BinaryInputArchiveBlock : ArchiveBlockBase, ICloneable
{
    public BinaryInputArchiveBlock(string name, ArchiveBlockType type, Deserializer deserializer, bool safe,
        uint nextElementPosition)
        : base(name, type)
    {
        _deserializer = deserializer;
        _safe = safe;
        _nextElementPosition = nextElementPosition;
        if (_safe)
        {
            _blockSize = _deserializer.ReadVLE();
            _blockOffset = _deserializer.Position;
            _nextElementPosition = SystemMath.Min(_blockOffset + _blockSize, _deserializer.Size);
        }
    }

    private BinaryInputArchiveBlock(BinaryInputArchiveBlock other)
        : base(other.Name, other.Type)
    {
        _inlineBlockDepth = other._inlineBlockDepth;
        _deserializer = other._deserializer;
        _safe = other._safe;
        _blockOffset = other._blockOffset;
        _blockSize = other._blockSize;
        _nextElementPosition = other._nextElementPosition;
    }

    public object Clone() => new BinaryInputArchiveBlock(this);

    public uint GetNextElementPosition() => _nextElementPosition;

    public bool IsUnorderedAccessSupported() => false;

    public bool HasElementOrBlock(string name) => false;

    public void Close(ArchiveBase archive)
    {
        if (_safe)
        {
            System.Diagnostics.Debug.Assert(_nextElementPosition != uint.MaxValue);
            if (_nextElementPosition != _deserializer.Position)
                _deserializer.Seek(_nextElementPosition);
        }
    }

    private readonly Deserializer _deserializer;
    /// <summary>Whether the block is safe.</summary>
    private readonly bool _safe;

    /// <summary>For safe blocks only.</summary>
    private readonly uint _blockOffset;
    private readonly uint _blockSize;
    private readonly uint _nextElementPosition;
}

/// <summary>Binary input archive.</summary>
public class BinaryInputArchive : ArchiveBase
{
    private readonly List<BinaryInputArchiveBlock> _stack = [];
    /// <summary>Deserializer.</summary>
    private readonly Deserializer _deserializer;

    public BinaryInputArchive(Context context, Deserializer deserializer)
        : base(context)
    {
        _deserializer = deserializer;
    }

    public override void Dispose()
    {
        _stack.Clear();
        base.Dispose();
    }

    public sealed override string Name => _deserializer.Name;
    public sealed override uint Checksum => _deserializer.GetChecksum();
    public sealed override bool IsInput => true;
    public sealed override bool IsHumanReadable => false;
    public sealed override bool IsUnorderedAccessSupportedInCurrentBlock =>
        _stack.Count != 0 && _stack[^1].IsUnorderedAccessSupported();
    public sealed override string CurrentBlockPath => GetCurrentBlockPath();
    public sealed override bool HasElementOrBlock(string name)
    {
        System.Diagnostics.Debug.Assert(_stack.Count != 0, "Root block must be opened before serialization");
        return _stack[^1].HasElementOrBlock(name);
    }

    public sealed override void BeginBlock(string name, ref int sizeHint, bool safe, ArchiveBlockType type)
    {
        FlushDelayedException();
        CheckIfNotEof(name);

        if (_stack.Count == 0)
        {
            var frame = new BinaryInputArchiveBlock(name, type, _deserializer, safe, uint.MaxValue);
            _stack.Add((BinaryInputArchiveBlock)frame.Clone());
        }
        else if (safe)
        {
            var blockFrame = new BinaryInputArchiveBlock(name, type, _deserializer, true,
                _stack[^1].GetNextElementPosition());
            _stack.Add((BinaryInputArchiveBlock)blockFrame.Clone());
        }
        else
            _stack[^1].OpenInlineBlock();

        if (type == ArchiveBlockType.Array)
        {
            sizeHint = checked((int)_deserializer.ReadVLE());
            if (_deserializer.IsEof() && sizeHint != 0)
            {
                EndBlock();
                throw IoFailureException("<block guard>");
            }
        }
    }

    public sealed override void EndBlock()
    {
        System.Diagnostics.Debug.Assert(_stack.Count != 0);

        var frame = _stack[^1];
        if (frame.HasOpenInlineBlock)
        {
            frame.CloseInlineBlock();
            return;
        }

        try
        {
            frame.Close(this);
        }
        catch (InvalidOperationException exception)
        {
            SetDelayedException(exception);
        }
        _stack.RemoveAt(_stack.Count - 1);

        if (_stack.Count == 0)
            CloseArchive();
    }

    public sealed override void Serialize(string name, ref bool value) { CheckElement(name); value = _deserializer.ReadBool(); }
    public sealed override void Serialize(string name, ref sbyte value) { CheckElement(name); value = _deserializer.ReadByte(); }
    public sealed override void Serialize(string name, ref byte value) { CheckElement(name); value = _deserializer.ReadUByte(); }
    public sealed override void Serialize(string name, ref short value) { CheckElement(name); value = _deserializer.ReadShort(); }
    public sealed override void Serialize(string name, ref ushort value) { CheckElement(name); value = _deserializer.ReadUShort(); }
    public sealed override void Serialize(string name, ref int value) { CheckElement(name); value = _deserializer.ReadInt(); }
    public sealed override void Serialize(string name, ref uint value) { CheckElement(name); value = _deserializer.ReadUInt(); }
    public sealed override void Serialize(string name, ref long value) { CheckElement(name); value = _deserializer.ReadInt64(); }
    public sealed override void Serialize(string name, ref ulong value) { CheckElement(name); value = _deserializer.ReadUInt64(); }
    public sealed override void Serialize(string name, ref float value) { CheckElement(name); value = _deserializer.ReadFloat(); }
    public sealed override void Serialize(string name, ref double value) { CheckElement(name); value = _deserializer.ReadDouble(); }
    public sealed override void Serialize(string name, ref string value) { CheckElement(name); value = _deserializer.ReadString(); }

    public sealed override void SerializeBytes(string name, Span<byte> bytes)
    {
        CheckElement(name);
        if (_deserializer.Read(bytes) != bytes.Length)
            throw IoFailureException(name);
    }

    public sealed override void SerializeVle(string name, ref uint value)
    {
        CheckElement(name);
        value = _deserializer.ReadVLE();
    }

    private string GetCurrentBlockPath()
    {
        var result = new System.Text.StringBuilder();
        foreach (var frame in _stack)
        {
            if (result.Length != 0)
                result.Append('/');
            result.Append(frame.Name);
            if (frame.HasOpenInlineBlock)
                result.Append("/?");
        }
        return result.ToString();
    }

    private void CheckElement(string name)
    {
        FlushDelayedException();
        CheckIfNotEof(name);
        System.Diagnostics.Debug.Assert(_stack.Count != 0, "Root block must be opened before serialization");
    }
}
