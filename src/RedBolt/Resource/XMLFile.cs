using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>XML document resource.</summary>
public class XMLFile : Resource
{
    /// <summary>Construct.</summary>
    public XMLFile()
    {
        Document = new XDocument();
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        Document = null!;
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AddFactoryReflection<XMLFile>();
    }

    public XDocument Document { get; private set; }

    public XMLElement Root => new(this, Document.Root);

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var dataSize = source.CanSeek ? checked((int)(source.Length - source.Position)) : -1;
        if (dataSize == 0 && !string.IsNullOrEmpty(Name))
        {
            Log.GetLogger().LogError("Zero sized XML data in {Name}", Name);
            return false;
        }

        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = reader.ReadToEnd();
        try
        {
            Document = XDocument.Parse(text);
            Document.Declaration = null;
            foreach (var node in Document.DescendantNodes()
                         .Where(static node => node is XComment or XProcessingInstruction or XDocumentType)
                         .ToArray())
                node.Remove();
        }
        catch (System.Xml.XmlException)
        {
            Log.GetLogger().LogError("Could not parse XML data from {Name}", Name);
            Document = new XDocument();
            return false;
        }

        var rootElement = GetRoot();
        var inherit = rootElement.GetAttribute("inherit");
        if (!string.IsNullOrEmpty(inherit))
        {
            // The existence of this attribute indicates this is an RFC 5261 patch file
            var cache = GetSubsystem<ResourceCache>()!;
            // If being async loaded, GetResource() is not safe, so use GetTempResource() instead
            var inheritedXmlFile = GetAsyncLoadState() == AsyncLoadState.Done
                ? cache.GetResourceOrNull<XMLFile>(inherit)
                : cache.GetTempResource<XMLFile>(inherit);
            if (inheritedXmlFile is null)
            {
                Log.GetLogger().LogError("Could not find inherited XML file: {Inherit}", inherit);
                return false;
            }

            // Patch this XMLFile and leave the original inherited XMLFile as it is
            var patchDocument = Document;
            Document = new XDocument(inheritedXmlFile.Document);
            Patch(rootElement);
            GC.KeepAlive(patchDocument);

            // Store resource dependencies so we know when to reload/repatch when the inherited resource changes
            cache.StoreResourceDependency(this, inherit);

            // Approximate patched data size
            dataSize = (dataSize >= 0 ? dataSize : Encoding.UTF8.GetByteCount(text)) + inheritedXmlFile.GetMemoryUse();
        }

        // Note: this probably does not reflect internal data structure size accurately
        SetMemoryUse(dataSize >= 0 ? dataSize : Encoding.UTF8.GetByteCount(text));
        return true;
    }

    public XMLElement CreateRoot(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var root = new XElement(name);
        Document = new XDocument(root);
        return new XMLElement(this, root);
    }

    public bool FromString(string source)
    {
        if (source.Length == 0)
            return false;

        using var buffer = new MemoryStream(Encoding.UTF8.GetBytes(source));
        return Load(buffer);
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
        if (root.NotNull)
            return root;
        root = GetRoot();
        if (root.NotNull)
            Log.GetLogger().LogWarning("XMLFile already has root {Root}, deleting it and creating root {Name}", root.Name, name);
        return CreateRoot(name);
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
            Document.RemoveNodes();
            using var archive = new XMLOutputArchive(this);
            serializeValue(archive);
            return true;
        }
        catch (ArchiveException exception)
        {
            Document.RemoveNodes();
            Log.GetLogger().LogError(exception, "Failed to save object to XML");
            return false;
        }
    }

    public bool LoadObjectCallback(Action<Archive> serializeValue)
    {
        ArgumentNullException.ThrowIfNull(serializeValue);
        try
        {
            using var archive = new XMLInputArchive(this);
            serializeValue(archive);
            return true;
        }
        catch (ArchiveException exception)
        {
            Log.GetLogger().LogError(exception, "Failed to load object from XML");
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
        foreach (var patch in patchElement.Element.Elements())
        {
            var selection = patch.Attribute("sel")?.Value;
            if (string.IsNullOrEmpty(selection))
            {
                Log.GetLogger().LogError("XML Patch failed due to node not having a sel attribute.");
                continue;
            }
            var selected = Document.XPathEvaluate(selection);
            var original = selected is IEnumerable<object> objects ? objects.FirstOrDefault() : null;
            if (original is null)
            {
                Log.GetLogger().LogError("XML Patch failed with bad select: {Selection}.", selection);
                continue;
            }
            switch (patch.Name.LocalName)
            {
                case "add": PatchAdd(patch, original); break;
                case "replace": PatchReplace(patch, original); break;
                case "remove": PatchRemove(original); break;
                default:
                    Log.GetLogger().LogError("XMLFiles used for patching should only use 'add', 'replace' or 'remove' elements.");
                    break;
            }
        }
    }

    private void PatchAdd(XElement patch, object? original)
    {
        if (original is XAttribute attribute)
        {
            Log.GetLogger().LogError("XML Patch failed calling Add due to not selecting a node, {Attribute} attribute was selected.", attribute.Name.LocalName);
            return;
        }
        if (original is not XElement element)
            return;
        var type = patch.Attribute("type")?.Value;
        if (string.IsNullOrEmpty(type))
        {
            AddNode(patch, element);
            return;
        }

        if (type.StartsWith('@'))
            AddAttribute(patch, element);
    }

    private void AddNode(XElement patch, XElement original)
    {
        var nodes = patch.Nodes().ToArray();
        switch (patch.Attribute("pos")?.Value)
        {
            case "prepend":
                if (nodes.Length > 0 && CombineText(nodes[^1], original.FirstNode, true))
                    nodes = nodes[..^1];
                original.AddFirst(nodes.Select(CloneNode));
                break;
            case "before":
                if (nodes.Length > 0 && CombineText(nodes[0], original.PreviousNode, false))
                    nodes = nodes[1..];
                if (nodes.Length > 0 && CombineText(nodes[^1], original, true))
                    nodes = nodes[..^1];
                original.AddBeforeSelf(nodes.Select(CloneNode));
                break;
            case "after":
                if (nodes.Length > 0 && CombineText(nodes[0], original, false))
                    nodes = nodes[1..];
                if (nodes.Length > 0 && CombineText(nodes[^1], original.NextNode, true))
                    nodes = nodes[..^1];
                original.AddAfterSelf(nodes.Select(CloneNode));
                break;
            default:
                if (nodes.Length > 0 && CombineText(nodes[0], original.LastNode, false))
                    nodes = nodes[1..];
                original.Add(nodes.Select(CloneNode));
                break;
        }
    }

    private void AddAttribute(XElement patch, XElement original)
    {
        var attribute = patch.Attribute("type");
        if (patch.FirstNode is null)
        {
            Log.GetLogger().LogError("XML Patch failed calling Add due to attempting to add non text to an attribute for {Attribute}.", attribute?.Value);
            return;
        }

        var name = attribute!.Value[1..];
        original.Add(new XAttribute(name, patch.Value));
    }

    private bool CombineText(XNode? patch, XNode? original, bool prepend)
    {
        if (patch is XCData patchCdata && original is XCData originalCdata)
        {
            originalCdata.Value = prepend ? patchCdata.Value + originalCdata.Value : originalCdata.Value + patchCdata.Value;
            return true;
        }

        if (patch is XText patchText && patch is not XCData && original is XText originalText && original is not XCData)
        {
            originalText.Value = prepend ? patchText.Value + originalText.Value : originalText.Value + patchText.Value;
            return true;
        }

        return false;
    }

    private void PatchReplace(XElement patch, object? original)
    {
        if (original is XNode node)
        {
            var replacement = patch.FirstNode;
            if (replacement is not null)
                node.ReplaceWith(CloneNode(replacement));
            else
                node.Remove();
        }
        else if (original is XAttribute attribute)
            attribute.Value = patch.Value;
    }

    private void PatchRemove(object? original)
    {
        if (original is XNode node) node.Remove();
        else if (original is XAttribute attribute) attribute.Remove();
    }

    private static XNode CloneNode(XNode node) => node switch
    {
        XElement element => new XElement(element),
        XCData cdata => new XCData(cdata.Value),
        XText text => new XText(text.Value),
        XComment comment => new XComment(comment.Value),
        XProcessingInstruction instruction => new XProcessingInstruction(instruction.Target, instruction.Data),
        _ => new XText(node.ToString())
    };

    public override string ToString() => ToString("\t");

    public string ToString(string indentation)
    {
        var result = new StringBuilder("<?xml version=\"1.0\"?>\n");
        if (Document.Root is null)
            return result.ToString();

        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            IndentChars = indentation,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.None
        };
        using (var writer = XmlWriter.Create(result, settings))
            Document.Save(writer);
        if (result[^1] != '\n')
            result.Append('\n');
        return result.ToString();
    }

}
