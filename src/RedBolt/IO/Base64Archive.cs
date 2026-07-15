namespace RedBolt;

// C++: class URHO3D_API Base64OutputArchive : private VectorBuffer, public BinaryOutputArchive
// C++ inherits VectorBuffer directly (multiple inheritance); C# uses composition.
/// Binary archive backed by a managed buffer and exposed as Base64 text.
public sealed class Base64OutputArchive : Archive
{
    private readonly VectorBuffer _buffer = new();
    private readonly BinaryOutputArchive _archive;

    public Base64OutputArchive() => _archive = new BinaryOutputArchive(Context.Instance, _buffer);

    // C++: n/a — C# convenience to access raw buffer bytes.
    public byte[] GetBuffer() => _buffer.GetBuffer();
    // C++: ea::string GetBase64() const; — const in C++, C# flushes first.
    public string GetBase64() { Flush(); return Convert.ToBase64String(_buffer.GetBuffer()); }

    public override Context Context => _archive.Context;
    public override string Name => _archive.Name;
    public override uint Checksum => _archive.Checksum;
    public override bool IsInput => false;
    public override bool IsHumanReadable => false;
    public override bool IsUnorderedAccessSupportedInCurrentBlock => _archive.IsUnorderedAccessSupportedInCurrentBlock;
    public override bool IsEof => _archive.IsEof;
    public override string CurrentBlockPath => _archive.CurrentBlockPath;
    public override bool HasElementOrBlock(string name) => _archive.HasElementOrBlock(name);
    public override void BeginBlock(string name, ref uint sizeHint, bool safe, ArchiveBlockType type) => _archive.BeginBlock(name, ref sizeHint, safe, type);
    public override void EndBlock() => _archive.EndBlock();
    public override void Flush() => _archive.Flush();
    public override void Serialize(string name, ref bool value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref sbyte value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref byte value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref short value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref ushort value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref int value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref uint value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref long value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref ulong value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref float value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref double value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref string value) => _archive.Serialize(name, ref value);
    public override void SerializeBytes(string name, Span<byte> bytes) => _archive.SerializeBytes(name, bytes);
    public override void SerializeVle(string name, ref uint value) => _archive.SerializeVle(name, ref value);
}

// C++: class URHO3D_API Base64InputArchive : private VectorBuffer, public BinaryInputArchive
// C++ inherits VectorBuffer directly; C# uses composition with MemoryBuffer.
/// Binary input archive initialized from Base64 text.
public sealed class Base64InputArchive : Archive
{
    private readonly MemoryBuffer _buffer;
    private readonly BinaryInputArchive _archive;

    public Base64InputArchive(string base64)
    {
        ArgumentNullException.ThrowIfNull(base64);
        _buffer = new MemoryBuffer(Convert.FromBase64String(base64));
        _archive = new BinaryInputArchive(Context.Instance, _buffer);
    }

    public override Context Context => _archive.Context;
    public override string Name => _archive.Name;
    public override uint Checksum => _archive.Checksum;
    public override bool IsInput => true;
    public override bool IsHumanReadable => false;
    public override bool IsUnorderedAccessSupportedInCurrentBlock => _archive.IsUnorderedAccessSupportedInCurrentBlock;
    public override bool IsEof => _archive.IsEof;
    public override string CurrentBlockPath => _archive.CurrentBlockPath;
    public override bool HasElementOrBlock(string name) => _archive.HasElementOrBlock(name);
    public override void BeginBlock(string name, ref uint sizeHint, bool safe, ArchiveBlockType type) => _archive.BeginBlock(name, ref sizeHint, safe, type);
    public override void EndBlock() => _archive.EndBlock();
    public override void Flush() => _archive.Flush();
    public override void Serialize(string name, ref bool value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref sbyte value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref byte value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref short value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref ushort value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref int value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref uint value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref long value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref ulong value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref float value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref double value) => _archive.Serialize(name, ref value);
    public override void Serialize(string name, ref string value) => _archive.Serialize(name, ref value);
    public override void SerializeBytes(string name, Span<byte> bytes) => _archive.SerializeBytes(name, bytes);
    public override void SerializeVle(string name, ref uint value) => _archive.SerializeVle(name, ref value);
}
