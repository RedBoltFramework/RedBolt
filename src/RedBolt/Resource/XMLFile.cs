using System.Text;
using System.Xml.Linq;

namespace RedBolt;

public sealed class XMLFile : Resource
{
    public XMLFile(string name)
        : base(name)
    {
        Document = new XDocument();
    }

    public XDocument Document { get; private set; }

    public XElement? Root => Document.Root;

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return FromString(reader.ReadToEnd());
    }

    public XElement CreateRoot(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var root = new XElement(name);
        Document = new XDocument(root);
        RefreshMemoryUse();
        return root;
    }

    public bool FromString(string source)
    {
        try
        {
            Document = XDocument.Parse(source, LoadOptions.PreserveWhitespace);
            RefreshMemoryUse();
            return true;
        }
        catch (System.Xml.XmlException)
        {
            Document = new XDocument();
            MemoryUse = 0;
            return false;
        }
    }

    public XDocument GetDocument() => Document;

    public XElement? GetRoot(string name = "")
    {
        return string.IsNullOrEmpty(name) || string.Equals(Document.Root?.Name.LocalName, name, StringComparison.Ordinal)
            ? Document.Root
            : null;
    }

    public XElement GetOrCreateRoot(string name)
    {
        return GetRoot(name) ?? CreateRoot(name);
    }

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var bytes = Encoding.UTF8.GetBytes(ToString());
        destination.Write(bytes);
        return true;
    }

    public bool Save(Stream destination, string indentation)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var bytes = Encoding.UTF8.GetBytes(ToString(indentation));
        destination.Write(bytes);
        return true;
    }

    public override string ToString() => ToString("\t");

    public string ToString(string indentation)
    {
        if (Document.Root is null)
            return string.Empty;

        return string.IsNullOrEmpty(indentation)
            ? Document.ToString(SaveOptions.DisableFormatting)
            : Document.ToString();
    }

    private void RefreshMemoryUse()
    {
        MemoryUse = (uint)Encoding.UTF8.GetByteCount(ToString());
    }
}
