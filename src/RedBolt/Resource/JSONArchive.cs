using System.Diagnostics;
using System.Globalization;

namespace RedBolt;

/// <summary>Base archive for JSON serialization.</summary>
public abstract class JSONArchiveBase<TBlock> : ArchiveBase
    where TBlock : ArchiveBlockBase
{
    protected JSONArchiveBase(Context context, JSONFile? jsonFile)
        : base(context)
    {
        _jsonFile = jsonFile;
    }

    /// <summary>Archive implementation.</summary>
    public override string Name => _jsonFile?.Name ?? string.Empty;

    public override void Dispose()
    {
        Stack.Clear();
        _jsonFile = null;
        base.Dispose();
    }

    protected List<TBlock> Stack { get; } = [];

    private JSONFile? _jsonFile;
}

/// <summary>JSON output archive block.</summary>
public class JSONOutputArchiveBlock : ArchiveBlockBase, ICloneable
{
    public JSONOutputArchiveBlock(string name, ArchiveBlockType type, JSONValue blockValue, int sizeHint)
        : base(name, type)
    {
        _blockValue = blockValue;
        if (_type == ArchiveBlockType.Array)
            _expectedElementCount = sizeHint;

        if (IsArchiveBlockJsonArray(_type))
            _blockValue.SetType(JSONValueType.Array);
        else if (IsArchiveBlockJsonObject(_type))
            _blockValue.SetType(JSONValueType.Object);
        else
            Debug.Assert(false);
    }

    public JSONOutputArchiveBlock(JSONOutputArchiveBlock other)
        : base(other.Name, other.Type)
    {
        _blockValue = other._blockValue;
        _expectedElementCount = other._expectedElementCount;
        _numElements = other._numElements;
        _inlineBlockDepth = other._inlineBlockDepth;
    }

    public object Clone() => new JSONOutputArchiveBlock(this);

    public JSONValue CreateElement(ArchiveBase archive, string elementName)
    {
        Debug.Assert(_numElements < _expectedElementCount);

        switch (_type)
        {
            case ArchiveBlockType.Sequential:
            case ArchiveBlockType.Array:
                ++_numElements;
                using (var value = new JSONValue())
                    _blockValue.Push(value);
                return _blockValue[_blockValue.Size() - 1];

            case ArchiveBlockType.Unordered:
                if (_blockValue.GetObject().ContainsKey(elementName))
                    throw archive.DuplicateElementException(elementName);

                ++_numElements;
                using (var value = new JSONValue())
                    _blockValue.Set(elementName, value);
                return _blockValue[elementName];

            default:
                Debug.Assert(false);
                return _blockValue;
        }
    }

    public bool IsUnorderedAccessSupported() => _type == ArchiveBlockType.Unordered;
    public bool HasElementOrBlock(string name) => false;
    public void Close(ArchiveBase archive)
    {
    }

    private static bool IsArchiveBlockJsonArray(ArchiveBlockType type) =>
        type is ArchiveBlockType.Array or ArchiveBlockType.Sequential;

    private static bool IsArchiveBlockJsonObject(ArchiveBlockType type) =>
        type == ArchiveBlockType.Unordered;

    private readonly JSONValue _blockValue;
    private int _expectedElementCount = int.MaxValue;
    private int _numElements;
}

/// <summary>JSON output archive.</summary>
public class JSONOutputArchive : JSONArchiveBase<JSONOutputArchiveBlock>
{
    /// <summary>Construct from element.</summary>
    public JSONOutputArchive(Context context, JSONValue value, JSONFile? jsonFile = null)
        : base(context, jsonFile)
    {
        _rootValue = value;
    }

    /// <summary>Construct from file.</summary>
    public JSONOutputArchive(JSONFile jsonFile)
        : this(Context.Instance, jsonFile.Root, jsonFile)
    {
    }

    public override void Dispose()
    {
        _tempString = string.Empty;
        _rootValue = null;
        base.Dispose();
    }

    /// <summary>Archive implementation.</summary>
    public override bool IsInput => false;
    public override bool IsHumanReadable => true;
    public override bool IsUnorderedAccessSupportedInCurrentBlock =>
        Stack.Count != 0 && GetCurrentBlock().IsUnorderedAccessSupported();

    public override bool HasElementOrBlock(string name)
    {
        Debug.Assert(false, "Archive::HasElementOrBlock is not supported for Output Archive");
        return false;
    }

    public override string CurrentBlockPath
    {
        get
        {
            var result = string.Empty;
            foreach (var block in Stack)
            {
                if (result.Length != 0)
                    result += "/";
                result += block.Name;
                if (block.HasOpenInlineBlock)
                    result += "/?";
            }
            return result;
        }
    }

    public override void EndBlock()
    {
        Debug.Assert(Stack.Count != 0);

        var currentBlock = GetCurrentBlock();
        if (currentBlock.HasOpenInlineBlock)
        {
            currentBlock.CloseInlineBlock();
            return;
        }

        try
        {
            currentBlock.Close(this);
        }
        catch (ArchiveException exception)
        {
            SetDelayedException(exception);
        }

        Stack.RemoveAt(Stack.Count - 1);
        if (Stack.Count == 0)
            CloseArchive();
    }

    public override void BeginBlock(string name, ref uint sizeHint, bool safe, ArchiveBlockType type)
    {
        FlushDelayedException();
        CheckIfNotEof(name);
        CheckBlockOrElementName(name);

        if (Stack.Count == 0)
        {
            Stack.Add(new JSONOutputArchiveBlock(name, type, RootValue, checked((int)sizeHint)));
            return;
        }

        var blockValue = GetCurrentBlock().CreateElement(this, name);
        Stack.Add(new JSONOutputArchiveBlock(name, type, blockValue, checked((int)sizeHint)));
    }

    public override void Serialize(string name, ref bool value)
    {
        using var jsonValue = new JSONValue(value);
        CreateElement(name, jsonValue);
    }

    public override void Serialize(string name, ref sbyte value)
    {
        using var jsonValue = new JSONValue((int)value);
        CreateElement(name, jsonValue);
    }

    public override void Serialize(string name, ref byte value)
    {
        using var jsonValue = new JSONValue((uint)value);
        CreateElement(name, jsonValue);
    }

    public override void Serialize(string name, ref short value)
    {
        using var jsonValue = new JSONValue((int)value);
        CreateElement(name, jsonValue);
    }

    public override void Serialize(string name, ref ushort value)
    {
        using var jsonValue = new JSONValue((uint)value);
        CreateElement(name, jsonValue);
    }

    public override void Serialize(string name, ref int value)
    {
        using var jsonValue = new JSONValue(value);
        CreateElement(name, jsonValue);
    }

    public override void Serialize(string name, ref uint value)
    {
        using var jsonValue = new JSONValue(value);
        CreateElement(name, jsonValue);
    }

    public override void Serialize(string name, ref long value)
    {
        using var jsonValue = new JSONValue(value.ToString(CultureInfo.InvariantCulture));
        CreateElement(name, jsonValue);
    }

    public override void Serialize(string name, ref ulong value)
    {
        using var jsonValue = new JSONValue(value.ToString(CultureInfo.InvariantCulture));
        CreateElement(name, jsonValue);
    }

    public override void Serialize(string name, ref float value)
    {
        using var jsonValue = new JSONValue(value);
        CreateElement(name, jsonValue);
    }

    public override void Serialize(string name, ref double value)
    {
        using var jsonValue = new JSONValue(value);
        CreateElement(name, jsonValue);
    }

    public override void Serialize(string name, ref string value)
    {
        using var jsonValue = new JSONValue(value);
        CreateElement(name, jsonValue);
    }

    public override void SerializeBytes(string name, Span<byte> bytes)
    {
        _tempString = Convert.ToHexString(bytes);
        using var jsonValue = new JSONValue(_tempString);
        CreateElement(name, jsonValue);
    }

    public override void SerializeVle(string name, ref uint value)
    {
        using var jsonValue = new JSONValue(value);
        CreateElement(name, jsonValue);
    }

    private JSONValue RootValue => _rootValue!;
    private JSONOutputArchiveBlock GetCurrentBlock() => Stack[^1];

    private void CreateElement(string name, JSONValue value)
    {
        FlushDelayedException();
        CheckIfNotEof(name);
        Debug.Assert(Stack.Count != 0, "Root block must be opened before serialization");
        CheckBlockOrElementName(name);

        var jsonValue = GetCurrentBlock().CreateElement(this, name);
        jsonValue.Set(value);
    }

    private JSONValue? _rootValue;
    private string _tempString = string.Empty;
}

/// <summary>JSON input archive block.</summary>
public class JSONInputArchiveBlock : ArchiveBlockBase, ICloneable
{
    public JSONInputArchiveBlock(string name, ArchiveBlockType type, JSONValue value)
        : base(name, type)
    {
        _value = value;
    }

    public JSONInputArchiveBlock(JSONInputArchiveBlock other)
        : base(other.Name, other.Type)
    {
        _value = other._value;
        _nextElementIndex = other._nextElementIndex;
        _inlineBlockDepth = other._inlineBlockDepth;
    }

    public object Clone() => new JSONInputArchiveBlock(this);

    public int GetSizeHint() => _value.Size();

    public JSONValue ReadElement(ArchiveBase archive, string elementName, ArchiveBlockType? elementBlockType)
    {
        JSONValue elementValue;
        if (IsArchiveBlockJsonArray(_type))
        {
            if (_nextElementIndex >= _value.Size())
                throw archive.ElementNotFoundException(elementName, checked((uint)_nextElementIndex));

            elementValue = _value.Get(_nextElementIndex);
            ++_nextElementIndex;
        }
        else if (IsArchiveBlockJsonObject(_type))
        {
            if (!_value.Contains(elementName))
                throw archive.ElementNotFoundException(elementName);

            elementValue = _value.Get(elementName);
        }
        else
        {
            Debug.Assert(false);
            return _value;
        }

        if (elementBlockType.HasValue && !IsArchiveBlockTypeMatching(elementValue, elementBlockType.Value))
            throw archive.UnexpectedElementValueException(_name);

        return elementValue;
    }

    public bool IsUnorderedAccessSupported() => _type == ArchiveBlockType.Unordered;
    public bool HasElementOrBlock(string name) => _value.GetObject().ContainsKey(name);
    public void Close(ArchiveBase archive)
    {
    }

    internal static bool IsArchiveBlockTypeMatching(JSONValue value, ArchiveBlockType type) =>
        IsArchiveBlockJsonArray(type) && IsJsonValueCompatibleWithArray(value)
        || IsArchiveBlockJsonObject(type) && IsJsonValueCompatibleWithObject(value);

    private static bool IsArchiveBlockJsonArray(ArchiveBlockType type) =>
        type is ArchiveBlockType.Array or ArchiveBlockType.Sequential;

    private static bool IsArchiveBlockJsonObject(ArchiveBlockType type) =>
        type == ArchiveBlockType.Unordered;

    private static bool IsJsonValueCompatibleWithArray(JSONValue value) =>
        value.IsArray || value.IsNull || value.IsObject && value.GetObject().Count == 0;

    private static bool IsJsonValueCompatibleWithObject(JSONValue value) =>
        value.IsObject || value.IsNull || value.IsArray && value.GetArray().Count == 0;

    private readonly JSONValue _value;
    private int _nextElementIndex;
}

/// <summary>JSON input archive.</summary>
public class JSONInputArchive : JSONArchiveBase<JSONInputArchiveBlock>
{
    /// <summary>Construct from element.</summary>
    public JSONInputArchive(Context context, JSONValue value, JSONFile? jsonFile = null)
        : base(context, jsonFile)
    {
        _rootValue = value;
    }

    /// <summary>Construct from file.</summary>
    public JSONInputArchive(JSONFile jsonFile)
        : this(Context.Instance, jsonFile.Root, jsonFile)
    {
    }

    public override void Dispose()
    {
        _rootValue = null;
        base.Dispose();
    }

    /// <summary>Archive implementation.</summary>
    public override bool IsInput => true;
    public override bool IsHumanReadable => true;
    public override bool IsUnorderedAccessSupportedInCurrentBlock =>
        Stack.Count != 0 && GetCurrentBlock().IsUnorderedAccessSupported();

    public override bool HasElementOrBlock(string name)
    {
        Debug.Assert(Stack.Count != 0, "Root block must be opened before serialization");
        return GetCurrentBlock().HasElementOrBlock(name);
    }

    public override string CurrentBlockPath
    {
        get
        {
            var result = string.Empty;
            foreach (var block in Stack)
            {
                if (result.Length != 0)
                    result += "/";
                result += block.Name;
                if (block.HasOpenInlineBlock)
                    result += "/?";
            }
            return result;
        }
    }

    public override void EndBlock()
    {
        Debug.Assert(Stack.Count != 0);

        var currentBlock = GetCurrentBlock();
        if (currentBlock.HasOpenInlineBlock)
        {
            currentBlock.CloseInlineBlock();
            return;
        }

        try
        {
            currentBlock.Close(this);
        }
        catch (ArchiveException exception)
        {
            SetDelayedException(exception);
        }

        Stack.RemoveAt(Stack.Count - 1);
        if (Stack.Count == 0)
            CloseArchive();
    }

    public override void BeginBlock(string name, ref uint sizeHint, bool safe, ArchiveBlockType type)
    {
        FlushDelayedException();
        CheckIfNotEof(name);
        CheckBlockOrElementName(name);

        if (Stack.Count == 0)
        {
            if (!JSONInputArchiveBlock.IsArchiveBlockTypeMatching(RootValue, type))
                throw UnexpectedElementValueException(name);

            var frame = new JSONInputArchiveBlock(name, type, RootValue);
            sizeHint = checked((uint)frame.GetSizeHint());
            Stack.Add(frame);
            return;
        }

        var blockValue = GetCurrentBlock().ReadElement(this, name, type);
        var blockFrame = new JSONInputArchiveBlock(name, type, blockValue);
        sizeHint = checked((uint)blockFrame.GetSizeHint());
        Stack.Add(blockFrame);
    }

    public override void Serialize(string name, ref bool value)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.Bool);
        value = jsonValue.GetBool();
    }

    public override void Serialize(string name, ref sbyte value)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.Number);
        value = unchecked((sbyte)jsonValue.GetInt());
    }

    public override void Serialize(string name, ref byte value)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.Number);
        value = unchecked((byte)jsonValue.GetUInt());
    }

    public override void Serialize(string name, ref short value)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.Number);
        value = unchecked((short)jsonValue.GetInt());
    }

    public override void Serialize(string name, ref ushort value)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.Number);
        value = unchecked((ushort)jsonValue.GetUInt());
    }

    public override void Serialize(string name, ref int value)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.Number);
        value = jsonValue.GetInt();
    }

    public override void Serialize(string name, ref uint value)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.Number);
        value = jsonValue.GetUInt();
    }

    public override void Serialize(string name, ref long value)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.String);
        value = long.Parse(jsonValue.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    public override void Serialize(string name, ref ulong value)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.String);
        value = ulong.Parse(jsonValue.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    public override void Serialize(string name, ref float value)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.Number);
        value = jsonValue.GetFloat();
    }

    public override void Serialize(string name, ref double value)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.Number);
        value = jsonValue.GetDouble();
    }

    public override void Serialize(string name, ref string value)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.String);
        value = jsonValue.GetString();
    }

    public override void SerializeBytes(string name, Span<byte> bytes)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.String);
        ReadBytesFromHexString(name, jsonValue.GetString(), bytes);
    }

    public override void SerializeVle(string name, ref uint value)
    {
        var jsonValue = ReadElement(name);
        CheckType(name, jsonValue, JSONValueType.Number);
        value = jsonValue.GetUInt();
    }

    private JSONValue RootValue => _rootValue!;
    private JSONInputArchiveBlock GetCurrentBlock() => Stack[^1];

    private JSONValue ReadElement(string name)
    {
        FlushDelayedException();
        CheckIfNotEof(name);
        Debug.Assert(Stack.Count != 0, "Root block must be opened before serialization");
        CheckBlockOrElementName(name);
        return GetCurrentBlock().ReadElement(this, name, null);
    }

    private void CheckType(string name, JSONValue value, JSONValueType type)
    {
        if (value.GetValueType() != type)
            throw UnexpectedElementValueException(name);
    }

    private JSONValue? _rootValue;
}
