namespace RedBolt;

public sealed class BinaryOutputArchive : ArchiveBase
{
    private sealed class Frame : ArchiveBlockBase
    {
        public Frame(string name, ArchiveBlockType type, Serializer parentSerializer, bool safe)
            : base(name, type)
        {
            ParentSerializer = parentSerializer;
            if (safe)
                OutputBuffer = new VectorBuffer();
        }

        public Serializer ParentSerializer { get; }
        public VectorBuffer OutputBuffer { get; } = null!;
        public Serializer Serializer => OutputBuffer is null ? ParentSerializer : OutputBuffer;
    }

    private readonly Serializer _serializer;
    private readonly List<Frame> _stack = [];

    public BinaryOutputArchive(Context context, Serializer serializer)
        : base(context)
    {
        _serializer = serializer;
    }

    public override string Name => _serializer.Name;
    public override uint Checksum => _serializer is Deserializer deserializer ? deserializer.GetChecksum() : 0;
    public override bool IsInput => false;
    public override bool IsHumanReadable => false;
    public override bool IsUnorderedAccessSupportedInCurrentBlock => false;
    public override string CurrentBlockPath => string.Join('/', _stack.Select(static frame => frame.Name));
    public override bool HasElementOrBlock(string name) => false;

    public override void BeginBlock(string name, ref uint sizeHint, bool safe, ArchiveBlockType type)
    {
        CheckBlockOrElementName(name);
        CheckIfNotEof(name);

        if (_stack.Count == 0 || safe)
            _stack.Add(new Frame(name, type, _stack.Count == 0 ? _serializer : _stack[^1].Serializer, safe));
        else
            _stack[^1].OpenInlineBlock();

        if (type == ArchiveBlockType.Array)
            WriteResult(CurrentSerializer.WriteVLE(sizeHint), "<block guard>");
    }

    public override void EndBlock()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("No archive block is open.");

        var frame = _stack[^1];
        if (frame.HasOpenInlineBlock)
        {
            frame.CloseInlineBlock();
            return;
        }

        _stack.RemoveAt(_stack.Count - 1);
        if (frame.OutputBuffer is not null)
        {
            try
            {
                var size = frame.OutputBuffer.Size;
                if (!frame.ParentSerializer.WriteVLE(size)
                    || frame.ParentSerializer.Write(frame.OutputBuffer.GetBuffer().AsSpan(0, checked((int)size))) != size)
                {
                    throw IoFailureException("<block guard>");
                }
            }
            catch (Exception exception)
            {
                SetDelayedException(exception);
            }
        }

        if (_stack.Count == 0)
            CloseArchive();
    }

    public override void Serialize(string name, ref bool value) => Write(name, CurrentSerializer.WriteBool(value));
    public override void Serialize(string name, ref sbyte value) => Write(name, CurrentSerializer.WriteByte(value));
    public override void Serialize(string name, ref byte value) => Write(name, CurrentSerializer.WriteUByte(value));
    public override void Serialize(string name, ref short value) => Write(name, CurrentSerializer.WriteShort(value));
    public override void Serialize(string name, ref ushort value) => Write(name, CurrentSerializer.WriteUShort(value));
    public override void Serialize(string name, ref int value) => Write(name, CurrentSerializer.WriteInt(value));
    public override void Serialize(string name, ref uint value) => Write(name, CurrentSerializer.WriteUInt(value));
    public override void Serialize(string name, ref long value) => Write(name, CurrentSerializer.WriteInt64(value));
    public override void Serialize(string name, ref ulong value) => Write(name, CurrentSerializer.WriteUInt64(value));
    public override void Serialize(string name, ref float value) => Write(name, CurrentSerializer.WriteFloat(value));
    public override void Serialize(string name, ref double value) => Write(name, CurrentSerializer.WriteDouble(value));
    public override void Serialize(string name, ref string value) => Write(name, CurrentSerializer.WriteString(value));

    public override void SerializeBytes(string name, Span<byte> bytes)
    {
        CheckElement(name);
        WriteResult(CurrentSerializer.Write(bytes) == bytes.Length, name);
    }

    public override void SerializeVle(string name, ref uint value)
    {
        CheckElement(name);
        WriteResult(CurrentSerializer.WriteVLE(value), name);
    }

    private Serializer CurrentSerializer => _stack.Count == 0 ? throw new InvalidOperationException("Root archive block is not open.") : _stack[^1].Serializer;

    private void CheckElement(string name)
    {
        CheckBlockOrElementName(name);
        CheckIfNotEof(name);
        if (_stack.Count == 0)
            throw new InvalidOperationException("Root archive block must be open before serialization.");
    }

    private void Write(string name, bool result)
    {
        CheckElement(name);
        WriteResult(result, name);
    }

    private void WriteResult(bool result, string name)
    {
        if (!result)
            throw IoFailureException(name);
    }
}

public sealed class BinaryInputArchive : ArchiveBase
{
    private sealed class Frame : ArchiveBlockBase
    {
        public Frame(string name, ArchiveBlockType type, bool safe, uint nextElementPosition)
            : base(name, type)
        {
            Safe = safe;
            NextElementPosition = nextElementPosition;
        }

        public bool Safe { get; }
        public uint NextElementPosition { get; set; }
    }

    private readonly Deserializer _deserializer;
    private readonly List<Frame> _stack = [];

    public BinaryInputArchive(Context context, Deserializer deserializer)
        : base(context)
    {
        _deserializer = deserializer;
    }

    public override string Name => _deserializer.Name;
    public override uint Checksum => _deserializer.GetChecksum();
    public override bool IsInput => true;
    public override bool IsHumanReadable => false;
    public override bool IsUnorderedAccessSupportedInCurrentBlock => false;
    public override string CurrentBlockPath => string.Join('/', _stack.Select(static frame => frame.Name));
    public override bool HasElementOrBlock(string name) => false;

    public override void BeginBlock(string name, ref uint sizeHint, bool safe, ArchiveBlockType type)
    {
        CheckBlockOrElementName(name);
        CheckIfNotEof(name);

        if (_stack.Count == 0)
        {
            var nextElementPosition = uint.MaxValue;
            if (safe)
            {
                var size = _deserializer.ReadVLE();
                nextElementPosition = Math.Min(_deserializer.Position + size, _deserializer.Size);
            }
            _stack.Add(new Frame(name, type, safe, nextElementPosition));
        }
        else if (safe)
        {
            var size = _deserializer.ReadVLE();
            var nextElementPosition = Math.Min(_deserializer.Position + size, _deserializer.Size);
            _stack.Add(new Frame(name, type, true, nextElementPosition));
        }
        else
            _stack[^1].OpenInlineBlock();

        if (type == ArchiveBlockType.Array)
        {
            sizeHint = _deserializer.ReadVLE();
            if (_deserializer.IsEof() && sizeHint != 0)
            {
                EndBlock();
                throw IoFailureException(name);
            }
        }
    }

    public override void EndBlock()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("No archive block is open.");

        var frame = _stack[^1];
        if (frame.HasOpenInlineBlock)
        {
            frame.CloseInlineBlock();
            return;
        }

        _stack.RemoveAt(_stack.Count - 1);
        if (frame.Safe && frame.NextElementPosition != uint.MaxValue && _deserializer.Position != frame.NextElementPosition)
            _deserializer.Seek(frame.NextElementPosition);

        if (_stack.Count == 0)
            CloseArchive();
    }

    public override void Serialize(string name, ref bool value) => Read(name, () => _deserializer.ReadBool(), out value);
    public override void Serialize(string name, ref sbyte value) => Read(name, () => _deserializer.ReadByte(), out value);
    public override void Serialize(string name, ref byte value) => Read(name, () => _deserializer.ReadUByte(), out value);
    public override void Serialize(string name, ref short value) => Read(name, () => _deserializer.ReadShort(), out value);
    public override void Serialize(string name, ref ushort value) => Read(name, () => _deserializer.ReadUShort(), out value);
    public override void Serialize(string name, ref int value) => Read(name, () => _deserializer.ReadInt(), out value);
    public override void Serialize(string name, ref uint value) => Read(name, () => _deserializer.ReadUInt(), out value);
    public override void Serialize(string name, ref long value) => Read(name, () => _deserializer.ReadInt64(), out value);
    public override void Serialize(string name, ref ulong value) => Read(name, () => _deserializer.ReadUInt64(), out value);
    public override void Serialize(string name, ref float value) => Read(name, () => _deserializer.ReadFloat(), out value);
    public override void Serialize(string name, ref double value) => Read(name, () => _deserializer.ReadDouble(), out value);
    public override void Serialize(string name, ref string value) => Read(name, () => _deserializer.ReadString(), out value);

    public override void SerializeBytes(string name, Span<byte> bytes)
    {
        CheckElement(name);
        if (_deserializer.Read(bytes) != bytes.Length)
            throw IoFailureException(name);
    }

    public override void SerializeVle(string name, ref uint value)
    {
        CheckElement(name);
        value = _deserializer.ReadVLE();
    }

    private void CheckElement(string name)
    {
        CheckBlockOrElementName(name);
        CheckIfNotEof(name);
        if (_stack.Count == 0)
            throw new InvalidOperationException("Root archive block must be open before serialization.");
    }

    private void Read<T>(string name, Func<T> reader, out T value)
    {
        CheckElement(name);
        value = reader();
    }
}
