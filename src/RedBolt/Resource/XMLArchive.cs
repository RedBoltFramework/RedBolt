using System.Globalization;
using System.Xml.Linq;
using System.Xml;

namespace RedBolt;

public sealed class XMLOutputArchive : ArchiveBase
{
    private sealed record Frame(string Name, ArchiveBlockType Type, XElement Element)
    {
        public HashSet<string> UsedNames { get; } = [];
    }

    private readonly List<Frame> _stack = [];
    private XElement? _root;

    public XMLOutputArchive() : base(Context.Instance) { }

    public XElement? Root => _root;
    public string ToXmlString()
    {
        Flush();
        return _root?.ToString(SaveOptions.DisableFormatting) ?? string.Empty;
    }

    public override bool IsInput => false;
    public override bool IsHumanReadable => true;
    public override bool IsUnorderedAccessSupportedInCurrentBlock => _stack.Count != 0 && _stack[^1].Type == ArchiveBlockType.Unordered;
    public override string CurrentBlockPath => string.Join('/', _stack.Select(static frame => frame.Name));
    public override bool HasElementOrBlock(string name) => false;

    public override void BeginBlock(string name, ref uint sizeHint, bool safe, ArchiveBlockType type)
    {
        CheckBlockOrElementName(name);

        var element = new XElement(name);
        if (_stack.Count == 0)
        {
            if (_root is not null)
                throw DuplicateElementException(name);
            _root = element;
        }
        else
            CreateChild(name).Add(element);

        _stack.Add(new Frame(name, type, element));
    }

    public override void EndBlock()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("No archive block is open.");
        _stack.RemoveAt(_stack.Count - 1);
    }

    public override void Serialize(string name, ref bool value) => Write(name, XmlConvert.ToString(value));
    public override void Serialize(string name, ref sbyte value) => Write(name, value.ToString(CultureInfo.InvariantCulture));
    public override void Serialize(string name, ref byte value) => Write(name, value.ToString(CultureInfo.InvariantCulture));
    public override void Serialize(string name, ref short value) => Write(name, value.ToString(CultureInfo.InvariantCulture));
    public override void Serialize(string name, ref ushort value) => Write(name, value.ToString(CultureInfo.InvariantCulture));
    public override void Serialize(string name, ref int value) => Write(name, value.ToString(CultureInfo.InvariantCulture));
    public override void Serialize(string name, ref uint value) => Write(name, value.ToString(CultureInfo.InvariantCulture));
    public override void Serialize(string name, ref long value) => Write(name, value.ToString(CultureInfo.InvariantCulture));
    public override void Serialize(string name, ref ulong value) => Write(name, value.ToString(CultureInfo.InvariantCulture));
    public override void Serialize(string name, ref float value) => Write(name, XmlConvert.ToString(value));
    public override void Serialize(string name, ref double value) => Write(name, XmlConvert.ToString(value));
    public override void Serialize(string name, ref string value) => Write(name, value);
    public override void SerializeBytes(string name, Span<byte> bytes) => Write(name, Convert.ToHexString(bytes));
    public override void SerializeVle(string name, ref uint value) => Write(name, value.ToString(CultureInfo.InvariantCulture));

    private XElement CreateChild(string name)
    {
        var frame = _stack[^1];
        if (frame.Type == ArchiveBlockType.Unordered && !frame.UsedNames.Add(name))
            throw DuplicateElementException(name);
        return frame.Element;
    }

    private void Write(string name, string value)
    {
        CheckBlockOrElementName(name);
        if (_stack.Count == 0)
            throw new InvalidOperationException("Root archive block is not open.");

        var frame = _stack[^1];
        if (frame.Type == ArchiveBlockType.Unordered)
        {
            if (!frame.UsedNames.Add(name))
                throw DuplicateElementException(name);
            frame.Element.SetAttributeValue(name, value);
        }
        else
            frame.Element.Add(new XElement(name, new XAttribute("value", value)));
    }
}

public sealed class XMLInputArchive : ArchiveBase
{
    private sealed class Frame(string name, ArchiveBlockType type, XElement element) : ArchiveBlockBase(name, type)
    {
        public XElement Element { get; } = element;
        public int NextChildIndex { get; set; }
        public IReadOnlyList<XElement> Children { get; } = element.Elements().ToList();
    }

    private readonly XElement _root;
    private readonly List<Frame> _stack = [];

    public XMLInputArchive(string xml) : base(Context.Instance)
    {
        ArgumentNullException.ThrowIfNull(xml);
        _root = XDocument.Parse(xml).Root ?? throw new InvalidOperationException("XML archive root is missing.");
    }

    public XMLInputArchive(XElement root) : base(Context.Instance)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    public override bool IsInput => true;
    public override bool IsHumanReadable => true;
    public override bool IsUnorderedAccessSupportedInCurrentBlock => _stack.Count != 0 && _stack[^1].Type == ArchiveBlockType.Unordered;
    public override string CurrentBlockPath => string.Join('/', _stack.Select(static frame => frame.Name));
    public override bool HasElementOrBlock(string name)
    {
        if (_stack.Count == 0)
            return false;
        var frame = _stack[^1];
        return frame.Element.Attribute(name) is not null || frame.Element.Element(name) is not null;
    }

    public override void BeginBlock(string name, ref uint sizeHint, bool safe, ArchiveBlockType type)
    {
        CheckBlockOrElementName(name);

        XElement element;
        if (_stack.Count == 0)
        {
            if (_root.Name.LocalName != name)
                throw ElementNotFoundException(name);
            element = _root;
        }
        else
            element = ReadElement(name);

        var frame = new Frame(name, type, element);
        sizeHint = checked((uint)frame.Children.Count);
        _stack.Add(frame);
    }

    public override void EndBlock()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("No archive block is open.");
        _stack.RemoveAt(_stack.Count - 1);
    }

    public override void Serialize(string name, ref bool value) => value = XmlConvert.ToBoolean(Read(name));
    public override void Serialize(string name, ref sbyte value) => value = sbyte.Parse(Read(name), CultureInfo.InvariantCulture);
    public override void Serialize(string name, ref byte value) => value = byte.Parse(Read(name), CultureInfo.InvariantCulture);
    public override void Serialize(string name, ref short value) => value = short.Parse(Read(name), CultureInfo.InvariantCulture);
    public override void Serialize(string name, ref ushort value) => value = ushort.Parse(Read(name), CultureInfo.InvariantCulture);
    public override void Serialize(string name, ref int value) => value = int.Parse(Read(name), CultureInfo.InvariantCulture);
    public override void Serialize(string name, ref uint value) => value = uint.Parse(Read(name), CultureInfo.InvariantCulture);
    public override void Serialize(string name, ref long value) => value = long.Parse(Read(name), CultureInfo.InvariantCulture);
    public override void Serialize(string name, ref ulong value) => value = ulong.Parse(Read(name), CultureInfo.InvariantCulture);
    public override void Serialize(string name, ref float value) => value = XmlConvert.ToSingle(Read(name));
    public override void Serialize(string name, ref double value) => value = XmlConvert.ToDouble(Read(name));
    public override void Serialize(string name, ref string value) => value = Read(name);
    public override void SerializeBytes(string name, Span<byte> bytes)
    {
        var decoded = Convert.FromHexString(Read(name));
        if (decoded.Length != bytes.Length)
            throw UnexpectedElementValueException(name);
        decoded.CopyTo(bytes);
    }
    public override void SerializeVle(string name, ref uint value) => value = uint.Parse(Read(name), CultureInfo.InvariantCulture);

    private string Read(string name)
    {
        CheckBlockOrElementName(name);
        if (_stack.Count == 0)
            throw new InvalidOperationException("Root archive block is not open.");

        var frame = _stack[^1];
        if (frame.Type == ArchiveBlockType.Unordered)
            return frame.Element.Attribute(name)?.Value ?? throw ElementNotFoundException(name);

        var child = ReadElement(name);
        return child.Attribute("value")?.Value ?? throw ElementNotFoundException(name);
    }

    private XElement ReadElement(string name)
    {
        var frame = _stack[^1];
        if (frame.Type == ArchiveBlockType.Unordered)
            return frame.Element.Element(name) ?? throw ElementNotFoundException(name);

        if (frame.NextChildIndex >= frame.Children.Count)
            throw ElementNotFoundException(name);

        return frame.Children[frame.NextChildIndex++];
    }
}
