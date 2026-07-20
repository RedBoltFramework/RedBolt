using System.Diagnostics;
using System.Xml.Linq;

namespace RedBolt;

/// <summary>Base archive for XML serialization.</summary>
public abstract class XMLArchiveBase<TBlock> : ArchiveBase
    where TBlock : ArchiveBlockBase
{
    protected XMLArchiveBase(Context context, XMLElement element, XMLFile? xmlFile, bool serializeRootName)
        : base(context)
    {
        Debug.Assert(element.NotNull);
        _rootElement = element;
        _serializeRootName = serializeRootName;
        _xmlFile = xmlFile;
    }

    /// <summary>Archive implementation.</summary>
    public override string Name => _xmlFile?.Name ?? string.Empty;

    public override void Dispose()
    {
        _rootElement = null;
        _xmlFile = null;
        Stack.Clear();
        base.Dispose();
    }

    protected XMLElement RootElement => _rootElement!;
    protected bool SerializeRootName => _serializeRootName;
    protected List<TBlock> Stack { get; } = [];

    private XMLElement? _rootElement;
    private readonly bool _serializeRootName;

    /// <summary>XML file.</summary>
    private XMLFile? _xmlFile;
}

/// <summary>XML attribute reference for input and output archives.</summary>
public readonly struct XMLAttributeReference
{
    public XMLAttributeReference(XMLElement element, string attribute)
    {
        _element = element;
        _attribute = attribute;
    }

    public XMLElement GetElement() => _element;
    public string GetAttributeName() => _attribute;

    private readonly XMLElement _element;
    private readonly string _attribute;
}

/// <summary>XML output archive block.</summary>
public class XMLOutputArchiveBlock : ArchiveBlockBase
{
    public XMLOutputArchiveBlock(string name, ArchiveBlockType type, XMLElement blockElement, int sizeHint)
        : base(name, type)
    {
        _blockElement = blockElement;
        if (_type == ArchiveBlockType.Array)
            _expectedElementCount = sizeHint;
    }

    public XMLElement CreateElement(ArchiveBase archive, string elementName)
    {
        Debug.Assert(_expectedElementCount < 0 || _numElements < _expectedElementCount);

        if (_type == ArchiveBlockType.Unordered && _usedNames.Contains(elementName))
            throw archive.DuplicateElementException(elementName);

        var element = _blockElement.CreateChild(elementName);
        ++_numElements;

        if (_type == ArchiveBlockType.Unordered)
            _usedNames.Add(elementName);

        Debug.Assert(element.NotNull);
        return element;
    }

    public XMLAttributeReference CreateElementOrAttribute(ArchiveBase archive, string elementName)
    {
        if (_type != ArchiveBlockType.Unordered)
        {
            var child = CreateElement(archive, elementName);
            return new XMLAttributeReference(child, "value");
        }

        // Special case for Unordered
        if (_usedNames.Contains(elementName))
            throw archive.DuplicateElementException(elementName);

        ++_numElements;
        return new XMLAttributeReference(_blockElement, elementName);
    }

    public bool IsUnorderedAccessSupported() => _type == ArchiveBlockType.Unordered;
    public bool HasElementOrBlock(string name) => false;

    public void Close(ArchiveBase archive)
    {
        Debug.Assert(_expectedElementCount < 0 || _numElements == _expectedElementCount);
    }

    private readonly XMLElement _blockElement;

    /// <summary>Expected block size (for arrays).</summary>
    private int _expectedElementCount = -1;
    /// <summary>Number of elements in block.</summary>
    private int _numElements;

    /// <summary>Set of used names for checking.</summary>
    private readonly HashSet<string> _usedNames = [];
}

/// <summary>XML output archive.</summary>
public class XMLOutputArchive : XMLArchiveBase<XMLOutputArchiveBlock>
{
    /// <summary>Construct from element.</summary>
    public XMLOutputArchive(Context context, XMLElement element, XMLFile? xmlFile = null, bool serializeRootName = false)
        : base(context, element, xmlFile, serializeRootName)
    {
    }

    /// <summary>Construct from file.</summary>
    public XMLOutputArchive(XMLFile xmlFile)
        : this(Context.Instance, xmlFile.GetOrCreateRoot(RootBlockName), xmlFile, true)
    {
    }

    public XMLOutputArchive(bool serializeRootName = true)
        : this(Context.Instance, serializeRootName)
    {
    }

    public XMLOutputArchive(Context context, bool serializeRootName = true)
        : this(context, new XMLElement(null, new XElement(RootBlockName)), null, serializeRootName)
    {
    }

    public XElement? Root => RootElement.Element;

    public string ToXmlString()
    {
        Flush();
        return Root?.ToString(SaveOptions.DisableFormatting) ?? string.Empty;
    }

    public override void Dispose()
    {
        _tempString = string.Empty;
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

        // Close inline block if possible
        var currentBlock = GetCurrentBlock();
        if (currentBlock.HasOpenInlineBlock)
        {
            currentBlock.CloseInlineBlock();
            return;
        }

        // Close block normally
        try
        {
            currentBlock.Close(this);
        }
        catch (InvalidOperationException exception)
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
            if (SerializeRootName)
                RootElement.SetName(name);
            Stack.Add(new XMLOutputArchiveBlock(name, type, RootElement, checked((int)sizeHint)));
            return;
        }

        var blockElement = GetCurrentBlock().CreateElement(this, name);
        var block = new XMLOutputArchiveBlock(name, type, blockElement, checked((int)sizeHint));
        Stack.Add(block);
    }

    public override void Serialize(string name, ref bool value)
    {
        var reference = CreateElementOrAttribute(name);
        reference.GetElement().SetBool(reference.GetAttributeName(), value);
    }

    public override void Serialize(string name, ref sbyte value)
    {
        var reference = CreateElementOrAttribute(name);
        reference.GetElement().SetInt(reference.GetAttributeName(), value);
    }

    public override void Serialize(string name, ref byte value)
    {
        var reference = CreateElementOrAttribute(name);
        reference.GetElement().SetUInt(reference.GetAttributeName(), value);
    }

    public override void Serialize(string name, ref short value)
    {
        var reference = CreateElementOrAttribute(name);
        reference.GetElement().SetInt(reference.GetAttributeName(), value);
    }

    public override void Serialize(string name, ref ushort value)
    {
        var reference = CreateElementOrAttribute(name);
        reference.GetElement().SetUInt(reference.GetAttributeName(), value);
    }

    public override void Serialize(string name, ref int value)
    {
        var reference = CreateElementOrAttribute(name);
        reference.GetElement().SetInt(reference.GetAttributeName(), value);
    }

    public override void Serialize(string name, ref uint value)
    {
        var reference = CreateElementOrAttribute(name);
        reference.GetElement().SetUInt(reference.GetAttributeName(), value);
    }

    public override void Serialize(string name, ref long value)
    {
        var reference = CreateElementOrAttribute(name);
        reference.GetElement().SetInt64(reference.GetAttributeName(), value);
    }

    public override void Serialize(string name, ref ulong value)
    {
        var reference = CreateElementOrAttribute(name);
        reference.GetElement().SetUInt64(reference.GetAttributeName(), value);
    }

    public override void Serialize(string name, ref float value)
    {
        var reference = CreateElementOrAttribute(name);
        reference.GetElement().SetFloat(reference.GetAttributeName(), value);
    }

    public override void Serialize(string name, ref double value)
    {
        var reference = CreateElementOrAttribute(name);
        reference.GetElement().SetDouble(reference.GetAttributeName(), value);
    }

    public override void Serialize(string name, ref string value)
    {
        var reference = CreateElementOrAttribute(name);
        reference.GetElement().SetString(reference.GetAttributeName(), value);
    }

    public override void SerializeBytes(string name, Span<byte> bytes)
    {
        var reference = CreateElementOrAttribute(name);
        _tempString = Convert.ToHexString(bytes);
        reference.GetElement().SetString(reference.GetAttributeName(), _tempString);
    }

    public override void SerializeVle(string name, ref uint value)
    {
        var reference = CreateElementOrAttribute(name);
        reference.GetElement().SetUInt(reference.GetAttributeName(), value);
    }

    private XMLAttributeReference CreateElementOrAttribute(string name)
    {
        FlushDelayedException();
        CheckIfNotEof(name);
        Debug.Assert(Stack.Count != 0, "Root block must be opened before serialization");
        CheckBlockOrElementName(name);

        var block = GetCurrentBlock();
        return GetCurrentBlock().CreateElementOrAttribute(this, name);
    }

    private XMLOutputArchiveBlock GetCurrentBlock() => Stack[^1];

    private string _tempString = string.Empty;
}

/// <summary>XML input archive block.</summary>
public class XMLInputArchiveBlock : ArchiveBlockBase
{
    public XMLInputArchiveBlock(string name, ArchiveBlockType type, XMLElement blockElement)
        : base(name, type)
    {
        _blockElement = blockElement;
        if (_blockElement.NotNull)
            _nextChild = _blockElement.GetChild();
    }

    /// <summary>Return size hint.</summary>
    public int CalculateSizeHint()
    {
        var child = _blockElement.GetChild();
        var count = 0;
        while (child.NotNull)
        {
            ++count;
            child = child.GetNext();
        }
        return count;
    }

    /// <summary>Read current child and move to the next one.</summary>
    public XMLElement ReadElement(ArchiveBase archive, string elementName)
    {
        if (_type != ArchiveBlockType.Unordered && (_nextChild is null || _nextChild.IsNull))
            throw archive.ElementNotFoundException(elementName);

        XMLElement element;
        if (_type == ArchiveBlockType.Unordered)
            element = _blockElement.GetChild(elementName);
        else
        {
            element = _nextChild!;
            _nextChild = _nextChild!.GetNext();
        }

        if (element.IsNull)
            throw archive.ElementNotFoundException(elementName);

        return element;
    }

    /// <summary>Read attribute (for Unordered blocks only) or the element and move to the next one.</summary>
    public XMLAttributeReference ReadElementOrAttribute(ArchiveBase archive, string elementName)
    {
        if (_type != ArchiveBlockType.Unordered)
        {
            var child = ReadElement(archive, elementName);
            return new XMLAttributeReference(child, "value");
        }

        // Special case for Unordered
        if (!_blockElement.HasAttribute(elementName))
            throw archive.ElementNotFoundException(elementName);

        return new XMLAttributeReference(_blockElement, elementName);
    }

    public bool IsUnorderedAccessSupported() => _type == ArchiveBlockType.Unordered;
    public bool HasElementOrBlock(string name) => _blockElement.HasChild(name) || _blockElement.HasAttribute(name);
    public void Close(ArchiveBase archive) { }

    /// <summary>Block element.</summary>
    private readonly XMLElement _blockElement;
    /// <summary>Next child to read.</summary>
    private XMLElement? _nextChild;
}

/// <summary>XML input archive.</summary>
public class XMLInputArchive : XMLArchiveBase<XMLInputArchiveBlock>
{
    /// <summary>Construct from element.</summary>
    public XMLInputArchive(Context context, XMLElement element, XMLFile? xmlFile = null, bool serializeRootName = false)
        : base(context, element, xmlFile, serializeRootName)
    {
    }

    /// <summary>Construct from file.</summary>
    public XMLInputArchive(XMLFile xmlFile)
        : this(Context.Instance, xmlFile.GetRoot(), xmlFile, false)
    {
    }

    public XMLInputArchive(string xml, bool serializeRootName = false)
        : this(Context.Instance, xml, serializeRootName)
    {
    }

    public XMLInputArchive(Context context, string xml, bool serializeRootName = false)
        : this(context, new XMLElement(null,
            XDocument.Parse(xml).Root ?? throw new InvalidOperationException("XML archive root is missing.")), null,
            serializeRootName)
    {
    }

    public XMLInputArchive(XElement root, bool serializeRootName = false)
        : this(Context.Instance, root, serializeRootName)
    {
    }

    public XMLInputArchive(Context context, XElement root, bool serializeRootName = false)
        : this(context, new XMLElement(null, root), null, serializeRootName)
    {
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

        // Close inline block if possible
        var currentBlock = GetCurrentBlock();
        if (currentBlock.HasOpenInlineBlock)
        {
            currentBlock.CloseInlineBlock();
            return;
        }

        // Close block normally
        try
        {
            currentBlock.Close(this);
        }
        catch (InvalidOperationException exception)
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

        // Open root block
        if (Stack.Count == 0)
        {
            if (SerializeRootName && RootElement.Name != name)
                throw ElementNotFoundException(name);

            var block = new XMLInputArchiveBlock(name, type, RootElement);
            sizeHint = checked((uint)block.CalculateSizeHint());
            Stack.Add(block);
            return;
        }

        var blockElement = GetCurrentBlock().ReadElement(this, name);
        var childBlock = new XMLInputArchiveBlock(name, type, blockElement);
        sizeHint = checked((uint)childBlock.CalculateSizeHint());
        Stack.Add(childBlock);
    }

    public override void Serialize(string name, ref bool value)
    {
        var reference = ReadElementOrAttribute(name);
        value = reference.GetElement().GetBool(reference.GetAttributeName());
    }

    public override void Serialize(string name, ref sbyte value)
    {
        var reference = ReadElementOrAttribute(name);
        value = checked((sbyte)reference.GetElement().GetInt(reference.GetAttributeName()));
    }

    public override void Serialize(string name, ref byte value)
    {
        var reference = ReadElementOrAttribute(name);
        value = checked((byte)reference.GetElement().GetUInt(reference.GetAttributeName()));
    }

    public override void Serialize(string name, ref short value)
    {
        var reference = ReadElementOrAttribute(name);
        value = checked((short)reference.GetElement().GetInt(reference.GetAttributeName()));
    }

    public override void Serialize(string name, ref ushort value)
    {
        var reference = ReadElementOrAttribute(name);
        value = checked((ushort)reference.GetElement().GetUInt(reference.GetAttributeName()));
    }

    public override void Serialize(string name, ref int value)
    {
        var reference = ReadElementOrAttribute(name);
        value = reference.GetElement().GetInt(reference.GetAttributeName());
    }

    public override void Serialize(string name, ref uint value)
    {
        var reference = ReadElementOrAttribute(name);
        value = reference.GetElement().GetUInt(reference.GetAttributeName());
    }

    public override void Serialize(string name, ref long value)
    {
        var reference = ReadElementOrAttribute(name);
        value = reference.GetElement().GetInt64(reference.GetAttributeName());
    }

    public override void Serialize(string name, ref ulong value)
    {
        var reference = ReadElementOrAttribute(name);
        value = reference.GetElement().GetUInt64(reference.GetAttributeName());
    }

    public override void Serialize(string name, ref float value)
    {
        var reference = ReadElementOrAttribute(name);
        value = reference.GetElement().GetFloat(reference.GetAttributeName());
    }

    public override void Serialize(string name, ref double value)
    {
        var reference = ReadElementOrAttribute(name);
        value = reference.GetElement().GetDouble(reference.GetAttributeName());
    }

    public override void Serialize(string name, ref string value)
    {
        var reference = ReadElementOrAttribute(name);
        value = reference.GetElement().GetAttribute(reference.GetAttributeName());
    }

    public override void SerializeBytes(string name, Span<byte> bytes)
    {
        var reference = ReadElementOrAttribute(name);
        var value = reference.GetElement().GetAttribute(reference.GetAttributeName());
        ReadBytesFromHexString(name, value, bytes);
    }

    public override void SerializeVle(string name, ref uint value)
    {
        var reference = ReadElementOrAttribute(name);
        value = reference.GetElement().GetUInt(reference.GetAttributeName());
    }

    private XMLAttributeReference ReadElementOrAttribute(string name)
    {
        FlushDelayedException();
        CheckIfNotEof(name);
        Debug.Assert(Stack.Count != 0, "Root block must be opened before serialization");
        CheckBlockOrElementName(name);

        return GetCurrentBlock().ReadElementOrAttribute(this, name);
    }

    private XMLInputArchiveBlock GetCurrentBlock() => Stack[^1];
}
