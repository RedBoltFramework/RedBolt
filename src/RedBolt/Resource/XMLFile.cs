using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;

namespace RedBolt;

public sealed class XMLFile : Resource
{
    public XMLFile(string name)
        : base(name)
    {
        Document = new XDocument();
    }

    public XDocument Document { get; private set; }

    public XMLElement Root => new(this, Document.Root);

    public override bool BeginLoad(Stream source)
    {
        // C++: checks for "inherit" attribute in root, loads inherited XML, patches, stores dependencies
        // C++: error logging via URHO3D_LOGERROR
        ArgumentNullException.ThrowIfNull(source);
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return FromString(reader.ReadToEnd());
    }

    public XMLElement CreateRoot(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var root = new XElement(name);
        Document = new XDocument(root);
        RefreshMemoryUse();
        return new XMLElement(this, root);
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

    public XMLElement GetRoot(string name = "")
    {
        return string.IsNullOrEmpty(name) || string.Equals(Document.Root?.Name.LocalName, name, StringComparison.Ordinal)
            ? new XMLElement(this, Document.Root)
            : new XMLElement(this, null);
    }

    public XMLElement GetOrCreateRoot(string name)
    {
        var root = GetRoot(name);
        return root.NotNull ? root : CreateRoot(name);
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

    public bool SaveObjectCallback(Action<Archive> serializeValue)
    {
        ArgumentNullException.ThrowIfNull(serializeValue);
        try
        {
            var archive = new XMLOutputArchive(Context.Instance);
            serializeValue(archive);
            archive.Flush();
            return FromString(archive.ToXmlString());
        }
        catch
        {
            Document = new XDocument();
            return false;
        }
    }

    public bool LoadObjectCallback(Action<Archive> serializeValue)
    {
        ArgumentNullException.ThrowIfNull(serializeValue);
        try
        {
            var archive = new XMLInputArchive(Context.Instance, Document.Root ?? throw new InvalidOperationException("XML root is missing."));
            serializeValue(archive);
            archive.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool SaveObject(Object value) => SaveObjectCallback(value.SerializeInBlock);

    public bool LoadObject(Object value) => LoadObjectCallback(value.SerializeInBlock);

    public void Patch(XMLFile patchFile)
    {
        ArgumentNullException.ThrowIfNull(patchFile);
        Patch(patchFile.Root);
    }

    public void Patch(XMLElement patchElement)
    {
        ArgumentNullException.ThrowIfNull(patchElement);
        if (Document.Root is null || patchElement.Element is null) return;
        foreach (var patch in patchElement.Element.Elements().ToArray())
        {
            var selection = patch.Attribute("sel")?.Value;
            if (string.IsNullOrWhiteSpace(selection)) continue;
            var selected = Document.XPathEvaluate(selection);
            var original = selected is IEnumerable<object> objects ? objects.FirstOrDefault() : selected;
            switch (patch.Name.LocalName)
            {
                case "add": PatchAdd(patch, original); break;
                case "replace": PatchReplace(patch, original); break;
                case "remove": PatchRemove(original); break;
            }
        }
        RefreshMemoryUse();
    }

    // C++: PatchAdd delegates to AddNode/AddAttribute which include CombineText logic for
    // C++: merging consecutive text nodes. C# simplified to direct element copy.
    private static void PatchAdd(XElement patch, object? original)
    {
        if (original is not XElement element) return;
        var type = patch.Attribute("type")?.Value;
        if (type?.StartsWith('@') == true)
        {
            // C++: AddAttribute validates child is text, strips '@' prefix, uses SetAttributeValue
            element.SetAttributeValue(type[1..], patch.Value);
            return;
        }
        // C++: AddNode handles append/prepend/before/after with CombineText merging
        var copies = patch.Nodes().Select(CloneNode).ToArray();
        switch (patch.Attribute("pos")?.Value)
        {
            case "prepend": element.AddFirst(copies); break;
            case "before": element.AddBeforeSelf(copies); break;
            case "after": element.AddAfterSelf(copies); break;
            default: element.Add(copies); break;
        }
    }

    private static void PatchReplace(XElement patch, object? original)
    {
        if (original is XElement element)
            element.ReplaceWith(patch.Nodes().Select(CloneNode));
        else if (original is XAttribute attribute)
            attribute.Value = patch.Value;
    }

    private static void PatchRemove(object? original)
    {
        if (original is XNode node) node.Remove();
        else if (original is XAttribute attribute) attribute.Remove();
    }

    // TODO: port - C++ code:
    // bool CombineText(const pugi::xml_node& patch, const pugi::xml_node& original, bool prepend) const;
    // AddNode(const pugi::xml_node& patch, const pugi::xpath_node& original) const;
    // AddAttribute(const pugi::xml_node& patch, const pugi::xpath_node& original) const;
    private static XNode CloneNode(XNode node) => node switch
    {
        XElement element => new XElement(element),
        XCData cdata => new XCData(cdata.Value),
        XText text => new XText(text.Value),
        XComment comment => new XComment(comment.Value),
        _ => new XText(node.ToString())
    };

    public override string ToString() => ToString("\t");

    public string ToString(string indentation)
    {
        if (Document.Root is null)
            return string.Empty;

        return string.IsNullOrEmpty(indentation)
            ? Document.ToString(SaveOptions.DisableFormatting)
            : Document.ToString();
    }

    internal void RefreshMemoryUse()
    {
        MemoryUse = (uint)Encoding.UTF8.GetByteCount(ToString());
    }
}
