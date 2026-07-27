using Microsoft.Extensions.Logging;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Scene;

namespace RedBolt.Resource;

/// <summary>XML document resource.</summary>
[Reflected]
public partial class XMLFile : Resource
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
        context.RegisterAttributedType<XMLFile>();
    }

    /// <summary>Load resource from stream. May be called from a worker thread. Return true if successful.</summary>
    public override bool BeginLoad(Deserializer source)
    {
        var dataSize = checked((int)source.Size);
        if (dataSize == 0 && !string.IsNullOrEmpty(source.GetName()))
        {
            Log.GetLogger().LogError("Zero sized XML data in {Name}", source.GetName());
            return false;
        }

        var data = new byte[dataSize];
        if (source.Read(data) != dataSize)
            return false;
        var text = Encoding.UTF8.GetString(data);
        try
        {
            Document = XDocument.Parse(text);
            Document.Declaration = null;
            var nodesToRemove = new List<XNode>();
            foreach (var node in Document.DescendantNodes())
            {
                if (node is XComment or XProcessingInstruction or XDocumentType)
                    nodesToRemove.Add(node);
            }
            foreach (var node in nodesToRemove)
                node.Remove();
        }
        catch (System.Xml.XmlException)
        {
            Log.GetLogger().LogError("Could not parse XML data from {Name}", source.GetName());
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
            dataSize += inheritedXmlFile.GetMemoryUse();
        }

        // Note: this probably does not reflect internal data structure size accurately
        SetMemoryUse(dataSize);
        return true;
    }

    /// <summary>Save resource with default indentation (one tab). Return true if successful.</summary>
    public override bool Save(Serializer destination)
    {
        return Save(destination, "\t");
    }

    /// <summary>Save resource with user-defined indentation. Return true if successful.</summary>
    public bool Save(Serializer destination, string indentation)
    {
        var bytes = Encoding.UTF8.GetBytes(ToString(indentation));
        return destination.Write(bytes) == bytes.Length;
    }

    /// <summary>Save objects using Archive serialization.</summary>
    public bool SaveObjectCallback(Action<Archive> serializeValue)
    {
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
            Log.GetLogger().LogError("Failed to save object to XML: {Error}", exception.Message);
            return false;
        }
    }

    /// <summary>Load objects using Archive serialization.</summary>
    public bool LoadObjectCallback(Action<Archive> serializeValue)
    {
        try
        {
            using var archive = new XMLInputArchive(this);
            serializeValue(archive);
            return true;
        }
        catch (ArchiveException exception)
        {
            Log.GetLogger().LogError("Failed to load object from XML: {Error}", exception.Message);
            return false;
        }
    }

    public bool SaveObject(string name, IArchiveSerializable value)
    {
        return SaveObjectCallback(archive =>
        {
            using var block = archive.OpenUnorderedBlock(name);
            value.SerializeInBlock(archive);
        });
    }

    public bool LoadObject(string name, IArchiveSerializable value)
    {
        return LoadObjectCallback(archive =>
        {
            using var block = archive.OpenUnorderedBlock(name);
            value.SerializeInBlock(archive);
        });
    }

    public bool SaveObject(RedObject value) => SaveObject(value.GetTypeName(), value);

    public bool LoadObject(RedObject value) => LoadObject(value.GetTypeName(), value);

    /// <summary>Deserialize from a string. Return true if successful.</summary>
    public bool FromString(string source)
    {
        if (source.Length == 0)
            return false;

        using var buffer = new MemoryBuffer(Encoding.UTF8.GetBytes(source));
        return Load(buffer);
    }

    /// <summary>Clear the document and create a root element.</summary>
    public XMLElement CreateRoot(string name)
    {
        var root = new XElement(name);
        Document = new XDocument(root);
        return new XMLElement(this, root);
    }

    /// <summary>Get the root element if it has matching name, otherwise create it and clear the document.</summary>
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

    /// <summary>Return the root element, with optionally specified name. Return null element if not found.</summary>
    public XMLElement GetRoot(string name = "")
    {
        return string.IsNullOrEmpty(name) || string.Equals(Document.Root?.Name.LocalName, name, StringComparison.Ordinal)
            ? new XMLElement(this, Document.Root)
            : new XMLElement(this, (XNode?)null);
    }

    /// <summary>Return the XML document.</summary>
    public XDocument Document { get; private set; }

    /// <summary>Serialize the XML content to a string.</summary>
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

    /// <summary>Patch the XMLFile with another XMLFile. Based on RFC 5261.</summary>
    public void Patch(XMLFile patchFile)
    {
        Patch(patchFile.GetRoot());
    }

    /// <summary>Patch the XMLFile with another XMLElement. Based on RFC 5261.</summary>
    public void Patch(XMLElement patchElement)
    {
        if (Document.Root is null || patchElement.Node is not XElement patchRoot) return;
        foreach (var patch in patchRoot.Elements())
        {
            var selection = patch.Attribute("sel")?.Value;
            if (string.IsNullOrEmpty(selection))
            {
                Log.GetLogger().LogError("XML Patch failed due to node not having a sel attribute.");
                continue;
            }
            var selected = Document.XPathEvaluate(selection);
            object? original = null;
            if (selected is IEnumerable<object> objects)
            {
                foreach (var selectedObject in objects)
                {
                    original = selectedObject;
                    break;
                }
            }
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

    /// <summary>Add an node in the Patch.</summary>
    private void PatchAdd(XElement patch, object? original)
    {
        if (original is XAttribute attribute)
        {
            Log.GetLogger().LogError("XML Patch failed calling Add due to not selecting a node, {Attribute} attribute was selected.", attribute.Name.LocalName);
            return;
        }
        if (original is not XNode node)
            return;
        var type = patch.Attribute("type")?.Value;
        if (string.IsNullOrEmpty(type))
        {
            AddNode(patch, node);
            return;
        }

        if (type[0] == '@' && node is XElement element)
            AddAttribute(patch, element);
    }

    /// <summary>Replace a node or attribute in the Patch.</summary>
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

    /// <summary>Remove a node or attribute in the Patch.</summary>
    private void PatchRemove(object? original)
    {
        if (original is XNode node) node.Remove();
        else if (original is XAttribute attribute) attribute.Remove();
    }

    /// <summary>Add a node in the Patch.</summary>
    private void AddNode(XElement patch, XNode original)
    {
        var nodes = new List<XNode>(patch.Nodes());
        var position = patch.Attribute("pos")?.Value;
        if (string.IsNullOrEmpty(position) || position == "append")
        {
            if (original is not XContainer container)
                return;
            if (nodes.Count > 0 && CombineText(nodes[0], container.LastNode, false))
                nodes.RemoveAt(0);
            foreach (var node in nodes)
                container.Add(CloneNode(node));
        }
        else if (position == "prepend")
        {
            if (original is not XContainer container)
                return;
            if (nodes.Count > 0 && CombineText(nodes[^1], container.FirstNode, true))
                nodes.RemoveAt(nodes.Count - 1);
            for (var i = nodes.Count - 1; i >= 0; --i)
                container.AddFirst(CloneNode(nodes[i]));
        }
        else if (position == "before")
        {
            if (nodes.Count > 0 && CombineText(nodes[0], original.PreviousNode, false))
                nodes.RemoveAt(0);
            if (nodes.Count > 0 && CombineText(nodes[^1], original, true))
                nodes.RemoveAt(nodes.Count - 1);
            foreach (var node in nodes)
                original.AddBeforeSelf(CloneNode(node));
        }
        else if (position == "after")
        {
            if (nodes.Count > 0 && CombineText(nodes[0], original, false))
                nodes.RemoveAt(0);
            if (nodes.Count > 0 && CombineText(nodes[^1], original.NextNode, true))
                nodes.RemoveAt(nodes.Count - 1);
            var insertPosition = original;
            foreach (var node in nodes)
            {
                var clone = CloneNode(node);
                insertPosition.AddAfterSelf(clone);
                insertPosition = clone;
            }
        }
    }

    /// <summary>Add an attribute in the Patch.</summary>
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

    /// <summary>Combine two text nodes.</summary>
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

    private static XNode CloneNode(XNode node) => node switch
    {
        XElement element => new XElement(element),
        XCData cdata => new XCData(cdata.Value),
        XText text => new XText(text.Value),
        XComment comment => new XComment(comment.Value),
        XProcessingInstruction instruction => new XProcessingInstruction(instruction.Target, instruction.Data),
        _ => new XText(node.ToString())
    };

}
