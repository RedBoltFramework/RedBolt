using System.Text.Json;
using System.Text.Json.Nodes;

namespace RedBolt;

public sealed class JSONOutputArchive : ArchiveBase
{
    private sealed record Frame(string Name, ArchiveBlockType Type, JsonNode Node);
    private readonly List<Frame> _stack = [];
    private JsonNode? _root;

    public JSONOutputArchive()
        : this(Context.Instance)
    {
    }

    public JSONOutputArchive(Context context)
        : base(context)
    {
    }

    public JsonNode? Root => _root;
    public string ToJsonString(bool indented = true)
    {
        Flush();
        return _root?.ToJsonString(new JsonSerializerOptions { WriteIndented = indented }) ?? "null";
    }

    public override bool IsInput => false;
    public override bool IsHumanReadable => true;
    public override bool IsUnorderedAccessSupportedInCurrentBlock => _stack.Count != 0 && _stack[^1].Type != ArchiveBlockType.Array;
    public override string CurrentBlockPath => string.Join('/', _stack.Select(static frame => frame.Name));
    public override bool HasElementOrBlock(string name) => false;

    public override void BeginBlock(string name, ref uint sizeHint, bool safe, ArchiveBlockType type)
    {
        CheckBlockOrElementName(name);
        JsonNode node = type == ArchiveBlockType.Array ? new JsonArray() : new JsonObject();
        Attach(name, node);
        _stack.Add(new Frame(name, type, node));
    }

    public override void EndBlock()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("No archive block is open.");
        _stack.RemoveAt(_stack.Count - 1);
    }

    public override void Serialize(string name, ref bool value) => Write(name, JsonValue.Create(value));
    public override void Serialize(string name, ref sbyte value) => Write(name, JsonValue.Create(value));
    public override void Serialize(string name, ref byte value) => Write(name, JsonValue.Create(value));
    public override void Serialize(string name, ref short value) => Write(name, JsonValue.Create(value));
    public override void Serialize(string name, ref ushort value) => Write(name, JsonValue.Create(value));
    public override void Serialize(string name, ref int value) => Write(name, JsonValue.Create(value));
    public override void Serialize(string name, ref uint value) => Write(name, JsonValue.Create(value));
    // C++: serializes 64-bit ints as JSON strings (eastl::to_string) to avoid precision loss
    public override void Serialize(string name, ref long value) => Write(name, JsonValue.Create(value));
    public override void Serialize(string name, ref ulong value) => Write(name, JsonValue.Create(value));
    public override void Serialize(string name, ref float value) => Write(name, JsonValue.Create(value));
    public override void Serialize(string name, ref double value) => Write(name, JsonValue.Create(value));
    public override void Serialize(string name, ref string value) => Write(name, JsonValue.Create(value));
    public override void SerializeBytes(string name, Span<byte> bytes) => Write(name, JsonValue.Create(Convert.ToBase64String(bytes)));
    public override void SerializeVle(string name, ref uint value) => Write(name, JsonValue.Create(value));

    private void Attach(string name, JsonNode node)
    {
        if (_stack.Count == 0)
        {
            if (_root is not null)
                throw DuplicateElementException(name);
            _root = node;
            return;
        }
        var parent = _stack[^1].Node;
        if (parent is JsonArray array)
            array.Add(node);
        else
            ((JsonObject)parent).Add(name, node);
    }

    private void Write(string name, JsonNode? value)
    {
        CheckBlockOrElementName(name);
        if (_stack.Count == 0)
            throw new InvalidOperationException("Root archive block is not open.");
        var parent = _stack[^1].Node;
        if (parent is JsonArray array)
            array.Add(value);
        else
            ((JsonObject)parent).Add(name, value);
    }
}

// TODO: port - C++ code:
// ea::optional<ea::string> ToJSONString(T& object) { ... }
// ea::optional<T> FromJSONString(const ea::string& jsonString) { ... }
public sealed class JSONInputArchive : ArchiveBase
{
    private sealed class Frame(string name, ArchiveBlockType type, JsonNode node) : ArchiveBlockBase(name, type)
    {
        public JsonNode Node { get; } = node;
        public int ArrayIndex { get; set; }
    }

    private readonly JsonNode _root;
    private readonly List<Frame> _stack = [];

    public JSONInputArchive(string json)
        : this(Context.Instance, json)
    {
    }

    public JSONInputArchive(Context context, string json)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(json);
        _root = JsonNode.Parse(json) ?? throw new JsonException("JSON archive root is null.");
    }

    public override bool IsInput => true;
    public override bool IsHumanReadable => true;
    public override bool IsUnorderedAccessSupportedInCurrentBlock => _stack.Count != 0 && _stack[^1].Type != ArchiveBlockType.Array;
    public override string CurrentBlockPath => string.Join('/', _stack.Select(static frame => frame.Name));
    public override bool HasElementOrBlock(string name) => _stack.Count != 0 && _stack[^1].Node is JsonObject obj && obj.ContainsKey(name);

    public override void BeginBlock(string name, ref uint sizeHint, bool safe, ArchiveBlockType type)
    {
        CheckBlockOrElementName(name);
        JsonNode node;
        if (_stack.Count == 0)
            node = _root;
        else
            node = Take(name) ?? throw ElementNotFoundException(name);
        if (type == ArchiveBlockType.Array)
        {
            if (node is not JsonArray array)
                throw UnexpectedElementValueException(name);
            sizeHint = (uint)array.Count;
        }
        else if (node is not JsonObject)
            throw UnexpectedElementValueException(name);
        _stack.Add(new Frame(name, type, node));
    }

    public override void EndBlock()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("No archive block is open.");
        _stack.RemoveAt(_stack.Count - 1);
    }

    public override void Serialize(string name, ref bool value) => value = Read<bool>(name);
    public override void Serialize(string name, ref sbyte value) => value = Read<sbyte>(name);
    public override void Serialize(string name, ref byte value) => value = Read<byte>(name);
    public override void Serialize(string name, ref short value) => value = Read<short>(name);
    public override void Serialize(string name, ref ushort value) => value = Read<ushort>(name);
    public override void Serialize(string name, ref int value) => value = Read<int>(name);
    public override void Serialize(string name, ref uint value) => value = Read<uint>(name);
    // C++: reads 64-bit ints from JSON strings and converts via ToInt64/ToUInt64; also CheckType validation
    public override void Serialize(string name, ref long value) => value = Read<long>(name);
    public override void Serialize(string name, ref ulong value) => value = Read<ulong>(name);
    public override void Serialize(string name, ref float value) => value = Read<float>(name);
    public override void Serialize(string name, ref double value) => value = Read<double>(name);
    public override void Serialize(string name, ref string value) => value = Read<string>(name);
    public override void SerializeBytes(string name, Span<byte> bytes)
    {
        var decoded = Convert.FromBase64String(Read<string>(name));
        if (decoded.Length != bytes.Length)
            throw UnexpectedElementValueException(name);
        decoded.CopyTo(bytes);
    }
    public override void SerializeVle(string name, ref uint value) => value = Read<uint>(name);

    private T Read<T>(string name)
    {
        CheckBlockOrElementName(name);
        var node = Take(name) ?? throw ElementNotFoundException(name);
        return node.GetValue<T>();
    }

    private JsonNode? Take(string name)
    {
        if (_stack.Count == 0)
            return _root;
        var frame = _stack[^1];
        if (frame.Node is JsonArray array)
            return frame.ArrayIndex < array.Count ? array[frame.ArrayIndex++] : null;
        return ((JsonObject)frame.Node)[name];
    }
}
