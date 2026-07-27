using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.Resource;

/// <summary>Element in an XML file.</summary>
public class XMLElement : ICloneable, IDisposable
{
    private static readonly Regex NumberPattern = new(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

    private WeakReference<XMLFile>? _file;
    private XNode? _node;
    private XPathResultSet? _xpathResultSet;
    private XPathNavigator? _xpathNode;
    private int _xpathResultIndex;

    /// <summary>Construct null element.</summary>
    public XMLElement()
    {
    }

    internal XMLElement(XMLFile? file, XNode? node)
    {
        _file = file is not null ? new WeakReference<XMLFile>(file) : null;
        _node = node;
    }

    internal XMLElement(XMLFile file, XPathNavigator xpathNode)
    {
        _file = new WeakReference<XMLFile>(file);
        _xpathNode = xpathNode.Clone();
    }

    internal XMLElement(XMLFile? file, XPathResultSet resultSet, XPathNavigator xpathNode, int xpathResultIndex)
    {
        _file = file is not null ? new WeakReference<XMLFile>(file) : null;
        _xpathResultSet = resultSet;
        _xpathNode = xpathNode;
        _xpathResultIndex = xpathResultIndex;
    }

    /// <summary>Copy-construct from another element.</summary>
    public object Clone()
    {
        return new XMLElement
        {
            _file = _file,
            _node = _node,
            _xpathResultSet = _xpathResultSet,
            _xpathNode = _xpathResultSet is not null ? _xpathNode : _xpathNode?.Clone(),
            _xpathResultIndex = _xpathResultIndex
        };
    }

    /// <summary>Destruct.</summary>
    public void Dispose()
    {
        _xpathNode = null;
        _xpathResultSet = null;
        _node = null;
        _file = null;
        _xpathResultIndex = 0;
    }

    public XMLElement CopyFrom(XMLElement rhs)
    {
        _file = rhs._file;
        _node = rhs._node;
        _xpathResultSet = rhs._xpathResultSet;
        _xpathNode = rhs._xpathResultSet is not null ? rhs._xpathNode : rhs._xpathNode?.Clone();
        _xpathResultIndex = rhs._xpathResultIndex;
        return this;
    }

    public XMLElement CreateChild(string name)
    {
        if (File is not { } file || ElementNode is not { } element)
            return new XMLElement();

        var child = new XElement(name);
        element.Add(child);
        return new XMLElement(file, child);
    }

    public XMLElement GetOrCreateChild(string name)
    {
        return GetChild(name) is { NotNull: true } child ? child : CreateChild(name);
    }

    public bool AppendChild(XMLElement other, bool asCopy = false)
    {
        if (File is null || ElementNode is not { } element || other.GetNode() is not { } otherNode)
            return false;

        var child = asCopy ? CloneNode(otherNode) : otherNode;
        element.Add(child);
        return true;
    }

    public bool Remove()
    {
        if (File is null || GetNode()?.Parent is null)
            return false;

        GetNode()!.Remove();
        return true;
    }

    public bool RemoveChild(XMLElement child)
    {
        if (File is null || ElementNode is not { } element || child.GetNode()?.Parent != element)
            return false;

        child.GetNode()!.Remove();
        return true;
    }

    public bool RemoveChild(string name)
    {
        var child = GetChild(name);
        return child.NotNull && RemoveChild(child);
    }

    public bool RemoveChildren(string name = "")
    {
        if (File is null || ElementNode is not { } element)
            return false;

        for (;;)
        {
            var child = string.IsNullOrEmpty(name)
                ? element.Nodes().LastOrDefault()
                : element.Element(name);
            if (child is null)
                break;
            child.Remove();
        }

        return true;
    }

    public bool RemoveAttribute(string name = "")
    {
        if (File is null)
            return false;
        var attribute = XPathAttribute ?? ElementNode?.Attribute(name);
        if (attribute is null)
            return false;

        attribute.Remove();
        return true;
    }

    public XMLElement SelectSingle(string query)
    {
        if (File is not { } file || GetNavigator() is not { } navigator)
            return new XMLElement();
        var result = navigator.SelectSingleNode(query);
        return result is not null ? new XMLElement(file, result) : new XMLElement();
    }

    public XMLElement SelectSinglePrepared(XPathQuery query) => query.EvaluateSingle(this);

    public XPathResultSet Select(string query)
    {
        if (File is not { } file || GetNavigator() is not { } navigator)
            return new XPathResultSet();
        var expression = XPathExpression.Compile(query);
        var iterator = navigator.Select(expression);
        var results = new List<XPathNavigator>();
        while (iterator.MoveNext())
            if (iterator.Current is not null)
                results.Add(iterator.Current.Clone());
        return new XPathResultSet(results, file);
    }

    public XPathResultSet SelectPrepared(XPathQuery query)
    {
        return query.Evaluate(this);
    }

    public bool SetValue(string value)
    {
        if (File is null || ElementNode is not { } element)
            return false;

        foreach (var node in element.Nodes())
        {
            if (node is XText text && node is not XCData)
            {
                text.Value = value;
                return true;
            }
        }

        element.Add(new XText(value));
        return true;
    }

    public bool SetAttribute(string name, string value)
    {
        if (File is null)
            return false;

        if (XPathAttribute is { } attribute)
            attribute.Value = value ?? string.Empty;
        else if (ElementNode is { } element)
            element.SetAttributeValue(name, value ?? string.Empty);
        else
            return false;
        return true;
    }

    public bool SetAttribute(string value)
    {
        if (File is null || XPathAttribute is not { } attribute)
            return false;
        attribute.Value = value;
        return true;
    }

    public bool SetBool(string name, bool value) => SetAttribute(name, value ? "true" : "false");

    public bool SetFloat(string name, float value) => SetAttribute(name, value.ToString(CultureInfo.InvariantCulture));

    public bool SetDouble(string name, double value) => SetAttribute(name, value.ToString(CultureInfo.InvariantCulture));

    public bool SetUInt(string name, uint value) => SetAttribute(name, value.ToString(CultureInfo.InvariantCulture));

    public bool SetInt(string name, int value) => SetAttribute(name, value.ToString(CultureInfo.InvariantCulture));

    public bool SetUInt64(string name, ulong value) => SetAttribute(name, value.ToString(CultureInfo.InvariantCulture));

    public bool SetInt64(string name, long value) => SetAttribute(name, value.ToString(CultureInfo.InvariantCulture));

    public bool SetString(string name, string value) => SetAttribute(name, value);

    public bool SetColor(string name, Color value) => SetAttribute(name, value.ToString());

    public bool SetVector2(string name, Vector2f value) => SetAttribute(name, value.ToString());

    public bool SetVector3(string name, Vector3f value) => SetAttribute(name, value.ToString());

    public bool SetVector4(string name, Vector4f value) => SetAttribute(name, value.ToString());

    public bool SetVectorVariant(string name, Variant value)
    {
        if (value.Type is VariantType.Float or VariantType.Vector2 or VariantType.Vector3 or VariantType.Vector4
            or VariantType.Matrix3 or VariantType.Matrix3x4 or VariantType.Matrix4)
            return SetAttribute(name, value.ToString());
        return false;
    }

    public bool SetQuaternion(string name, Quaternion value) => SetAttribute(name, value.ToString());

    public bool SetRect(string name, Rect value) => SetAttribute(name, value.ToString());

    public bool SetIntRect(string name, IntRect value) => SetAttribute(name, value.ToString());

    public bool SetIntVector2(string name, Vector2i value) => SetAttribute(name, value.ToString());

    public bool SetIntVector3(string name, Vector3i value) => SetAttribute(name, value.ToString());

    public bool SetMatrix3(string name, Matrix3 value) => SetAttribute(name, value.ToString());

    public bool SetMatrix3x4(string name, Matrix3x4 value) => SetAttribute(name, value.ToString());

    public bool SetMatrix4(string name, Matrix4 value) => SetAttribute(name, value.ToString());

    public bool SetBoundingBox(BoundingBox value)
    {
        if (!SetVector3("min", value.Min))
            return false;
        return SetVector3("max", value.Max);
    }

    public bool SetResourceRef(ResourceRef value) => File is { } file && SetAttribute("value", value.ToString(file.Context));

    public bool SetResourceRefList(ResourceRefList value) => File is { } file && SetAttribute("value", value.ToString(file.Context));

    public bool SetBuffer(string name, ReadOnlySpan<byte> value)
    {
        var result = new StringBuilder(value.Length * 4);
        for (var i = 0; i < value.Length; ++i)
        {
            if (i != 0)
                result.Append(' ');
            result.Append(value[i].ToString(CultureInfo.InvariantCulture));
        }
        return SetAttribute(name, result.ToString());
    }

    public bool SetVariant(Variant value)
    {
        return SetAttribute("type", value.GetTypeName()) && SetVariantValue(value);
    }

    public bool SetVariantValue(Variant value)
    {
        return value.Type switch
        {
            VariantType.VariantVector => SetVariantVector(value.GetVariantVector()),
            VariantType.StringVector => SetStringVector(value.GetStringVector()),
            VariantType.VariantMap => SetVariantMap(value.GetVariantMap()),
            VariantType.StringVariantMap => SetStringVariantMap(value.GetStringVariantMap()),
            VariantType.ResourceRef => SetResourceRef(value.GetResourceRef()),
            VariantType.ResourceRefList => SetResourceRefList(value.GetResourceRefList()),
            VariantType.Custom => SetCustomVariantValue(value),
            _ => SetAttribute("value", value.ToString())
        };
    }

    public bool SetVariantVector(VariantVector values)
    {
        if (!RemoveChildren("variant"))
            return false;
        foreach (var value in values)
            if (!CreateChild("variant").SetVariant(value)) return false;
        return true;
    }

    public bool SetStringVector(StringVector values)
    {
        if (!RemoveChildren("string"))
            return false;
        foreach (var value in values)
            if (!CreateChild("string").SetAttribute("value", value)) return false;
        return true;
    }

    public bool SetVariantMap(VariantMap values)
    {
        if (!RemoveChildren("variant"))
            return false;
        foreach (var (key, value) in values)
        {
            var element = CreateChild("variant");
            element.SetUInt("hash", key.Value);
            if (!element.SetVariant(value)) return false;
        }
        return true;
    }

    public bool SetStringVariantMap(StringVariantMap values)
    {
        if (!RemoveChildren("variant"))
            return false;
        foreach (var (key, value) in values)
        {
            var element = CreateChild("variant");
            element.SetString("name", key);
            if (!element.SetVariant(value)) return false;
        }
        return true;
    }

    public bool IsNull => _node is null && _xpathNode is null;

    public bool NotNull => _node is not null || _xpathNode is not null;

    public string Name
    {
        get => XPathAttribute?.Name.LocalName ?? ElementNode?.Name.LocalName ?? string.Empty;
        set
        {
            if (File is not null && ElementNode is { } element)
                element.Name = value;
        }
    }

    public bool HasChild(string name)
    {
        return File is not null && ElementNode?.Element(name) is not null;
    }

    public XMLElement GetChild(string name = "")
    {
        if (File is not { } file || ElementNode is not { } element)
            return new XMLElement();

        XNode? child = string.IsNullOrEmpty(name)
            ? element.Nodes().FirstOrDefault()
            : element.Element(name);
        return new XMLElement(file, child);
    }

    public XMLElement GetNext(string name = "")
    {
        if (File is not { } file || GetNode() is not { } node)
            return new XMLElement();

        XNode? sibling = string.IsNullOrEmpty(name)
            ? node.NextNode
            : node.ElementsAfterSelf(name).FirstOrDefault();
        return new XMLElement(file, sibling);
    }

    public XMLElement GetParent() => File is { } file ? new XMLElement(file, GetNode()?.Parent) : new XMLElement();

    public int GetNumAttributes() => File is not null ? ElementNode?.Attributes().Count() ?? 0 : 0;

    public bool HasAttribute(string name)
    {
        return File is not null && (XPathAttribute is { } attribute
            ? attribute.Name.LocalName == name
            : ElementNode?.Attribute(name) is not null);
    }

    public string Value
    {
        get
        {
            if (File is null || ElementNode is not { } element)
                return string.Empty;
            return element.Nodes().FirstOrDefault() switch
            {
                XText text => text.Value,
                XElement child => GetChildValue(child),
                _ => string.Empty
            };
        }
    }

    public string GetAttribute(string name = "")
    {
        if (File is null)
            return string.Empty;
        return XPathAttribute?.Value ?? ElementNode?.Attribute(name)?.Value ?? string.Empty;
    }

    public string GetAttributeLower(string name) => GetAttribute(name).ToLowerInvariant();

    public string GetAttributeUpper(string name) => GetAttribute(name).ToUpperInvariant();

    public CString GetAttributeCString(string name) => new(GetAttribute(name));

    public IReadOnlyList<string> GetAttributeNames()
    {
        return File is not null
            ? ElementNode?.Attributes().Select(static attribute => attribute.Name.LocalName).ToArray() ?? Array.Empty<string>()
            : Array.Empty<string>();
    }

    public bool GetBool(string name)
    {
        var value = GetAttribute(name);
        return bool.TryParse(value, out var result)
            ? result
            : value == "1";
    }

    public byte[] GetBuffer(string name)
    {
        var value = GetAttribute(name);
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var values = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new byte[values.Length];
        for (var i = 0; i < values.Length; ++i)
        {
            byte.TryParse(values[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out result[i]);
        }
        return result;
    }

    public bool GetBuffer(string name, Span<byte> destination)
    {
        var value = GetAttribute(name);
        var values = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (destination.Length < values.Length)
            return false;

        for (var i = 0; i < values.Length; ++i)
            byte.TryParse(values[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out destination[i]);
        return true;
    }

    public Variant GetVariant()
    {
        return GetVariantValue(Variant.GetTypeFromName(GetAttribute("type")));
    }

    public Variant GetVariantValue(VariantType type, Context? context = null)
    {
        var result = type switch
        {
            VariantType.VariantVector => new Variant(GetVariantVector()),
            VariantType.StringVector => new Variant(GetStringVector()),
            VariantType.VariantMap => new Variant(GetVariantMap()),
            VariantType.StringVariantMap => new Variant(GetStringVariantMap()),
            VariantType.ResourceRef => new Variant(GetResourceRef()),
            VariantType.ResourceRefList => new Variant(GetResourceRefList()),
            VariantType.Custom => GetCustomVariantValue(context),
            _ => default
        };

        if (type is not (VariantType.VariantVector or VariantType.StringVector or VariantType.VariantMap or VariantType.StringVariantMap
            or VariantType.ResourceRef or VariantType.ResourceRefList or VariantType.Custom))
            result.FromString(type, GetAttribute("value"));

        return result;
    }

    public VariantVector GetVariantVector()
    {
        var result = new VariantVector();
        for (var element = GetChild("variant"); element.NotNull; element = element.GetNext("variant"))
            result.Add(element.GetVariant());
        return result;
    }

    public StringVector GetStringVector()
    {
        var result = new StringVector();
        for (var element = GetChild("string"); element.NotNull; element = element.GetNext("string"))
            result.Add(element.GetAttribute("value"));
        return result;
    }

    public VariantMap GetVariantMap()
    {
        var result = new VariantMap();
        for (var element = GetChild("variant"); element.NotNull; element = element.GetNext("variant"))
        {
            StringHash key;
            if (element.HasAttribute("name"))
                key = new StringHash(element.GetAttribute("name"));
            else if (element.HasAttribute("hash"))
                key = new StringHash(element.GetUInt("hash"));
            else
                continue;
            result[key] = element.GetVariant();
        }
        return result;
    }

    public StringVariantMap GetStringVariantMap()
    {
        var result = new StringVariantMap();
        for (var element = GetChild("variant"); element.NotNull; element = element.GetNext("variant"))
            result[element.GetAttribute("name")] = element.GetVariant();
        return result;
    }

    public BoundingBox GetBoundingBox() => new(GetVector3("min"), GetVector3("max"));

    public Color GetColor(string name) => ParseColor(GetAttribute(name));

    public float GetFloat(string name) => float.TryParse(GetAttribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0.0f;

    public double GetDouble(string name) => double.TryParse(GetAttribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0.0;

    public uint GetUInt(string name) => uint.TryParse(GetAttribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0u;

    public int GetInt(string name) => int.TryParse(GetAttribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    public ulong GetUInt64(string name) => ulong.TryParse(GetAttribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0ul;

    public long GetInt64(string name) => long.TryParse(GetAttribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0L;

    public IntRect GetIntRect(string name) => ParseIntRect(GetAttribute(name));

    public Vector2i GetIntVector2(string name) => ParseIntVector2(GetAttribute(name));

    public Vector3i GetIntVector3(string name) => ParseIntVector3(GetAttribute(name));

    public Rect GetRect(string name) => ParseRect(GetAttribute(name));

    public Quaternion GetQuaternion(string name) => ParseQuaternion(GetAttribute(name));

    public ResourceRef GetResourceRef() => ResourceRef.Parse(GetAttribute("value"));

    public ResourceRefList GetResourceRefList() => ResourceRefList.Parse(GetAttribute("value"));

    public Vector2f GetVector2(string name) => ParseVector2(GetAttribute(name));

    public Vector3f GetVector3(string name) => ParseVector3(GetAttribute(name));

    public Vector4f GetVector4(string name) => ParseVector4(GetAttribute(name));

    public Vector4f GetVector(string name)
    {
        var parsed = ParseFloats(GetAttribute(name));
        return parsed.Length switch
        {
            1 => new Vector4f(parsed[0], 0.0f, 0.0f, 0.0f),
            2 => new Vector4f(parsed[0], parsed[1], 0.0f, 0.0f),
            3 => new Vector4f(parsed[0], parsed[1], parsed[2], 0.0f),
            >= 4 => new Vector4f(parsed[0], parsed[1], parsed[2], parsed[3]),
            _ => Vector4f.Zero
        };
    }

    public Variant GetVectorVariant(string name)
    {
        var parsed = ParseFloats(GetAttribute(name));
        return parsed.Length switch
        {
            1 => new Variant(parsed[0]),
            2 => new Variant(new Vector2f(parsed[0], parsed[1])),
            3 => new Variant(new Vector3f(parsed[0], parsed[1], parsed[2])),
            >= 4 => new Variant(new Vector4f(parsed[0], parsed[1], parsed[2], parsed[3])),
            _ => new Variant(VariantType.None)
        };
    }

    public Matrix3 GetMatrix3(string name) => ParseMatrix3(GetAttribute(name));

    public Matrix3x4 GetMatrix3x4(string name) => ParseMatrix3x4(GetAttribute(name));

    public Matrix4 GetMatrix4(string name) => ParseMatrix4(GetAttribute(name));

    public XMLFile? File => _file is not null && _file.TryGetTarget(out var file) ? file : null;

    public XNode? Node => GetNode();

    public XPathResultSet? XPathResultSet => _xpathResultSet;

    public XPathNavigator? XPathNode => _xpathNode;

    public XMLElement NextResult()
    {
        if (_xpathResultSet is null || _xpathNode is null)
            return new XMLElement();
        return _xpathResultSet[++_xpathResultIndex];
    }

    private XElement? ElementNode => GetNode() as XElement;

    private XAttribute? XPathAttribute => _xpathNode?.UnderlyingObject as XAttribute;

    private XNode? GetNode()
    {
        return _xpathNode?.UnderlyingObject switch
        {
            XAttribute attribute => attribute.Parent,
            XNode node => node,
            _ => _node
        };
    }

    internal XPathNavigator? GetNavigator()
    {
        return _xpathNode?.Clone() ?? _node?.CreateNavigator();
    }

    private static XNode CloneNode(XNode node)
    {
        return node switch
        {
            XElement element => new XElement(element),
            XCData cdata => new XCData(cdata.Value),
            XText text => new XText(text.Value),
            XComment comment => new XComment(comment.Value),
            XProcessingInstruction instruction => new XProcessingInstruction(instruction.Target, instruction.Data),
            _ => new XText(node.ToString())
        };
    }

    private static string GetChildValue(XElement element)
    {
        return element.Nodes().FirstOrDefault() switch
        {
            XText text => text.Value,
            XElement child => GetChildValue(child),
            _ => string.Empty
        };
    }

    private bool SetCustomVariantValue(Variant value)
    {
        if (!value.TryGetCustom<Serializable>(out var serializable) || serializable is null)
            return false;
        SetAttribute("type", serializable.GetTypeName());
        if (serializable.SaveXML(this))
            return true;
        RemoveAttribute("type");
        RemoveChildren();
        return false;
    }

    private Variant GetCustomVariantValue(Context? context)
    {
        if (context is null)
            return default;
        var typeName = GetAttribute("type");
        if (string.IsNullOrEmpty(typeName))
            return default;
        if (context.CreateObject(new StringHash(typeName)) is not Serializable serializable)
            return default;
        return serializable.LoadXML(this) ? Variant.MakeCustomValue(serializable) : default;
    }

    private static float[] ParseFloats(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return NumberPattern.Matches(value)
            .Select(static match => float.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0.0f)
            .ToArray();
    }

    private static int[] ParseInts(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return NumberPattern.Matches(value)
            .Select(static match => int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0)
            .ToArray();
    }

    private static Color ParseColor(string value)
    {
        var parsed = ParseFloats(value);
        return parsed.Length >= 4
            ? new Color(parsed[0], parsed[1], parsed[2], parsed[3])
            : parsed.Length >= 3
                ? new Color(parsed[0], parsed[1], parsed[2])
                : new Color();
    }

    private static IntRect ParseIntRect(string value)
    {
        var parsed = ParseInts(value);
        return parsed.Length >= 4 ? new IntRect(parsed[0], parsed[1], parsed[2], parsed[3]) : new IntRect();
    }

    private static Vector2i ParseIntVector2(string value)
    {
        var parsed = ParseInts(value);
        return parsed.Length >= 2 ? new Vector2i(parsed[0], parsed[1]) : new Vector2i();
    }

    private static Vector3i ParseIntVector3(string value)
    {
        var parsed = ParseInts(value);
        return parsed.Length >= 3 ? new Vector3i(parsed[0], parsed[1], parsed[2]) : new Vector3i();
    }

    private static Matrix3 ParseMatrix3(string value)
    {
        var parsed = ParseFloats(value);
        return parsed.Length >= 9
            ? new Matrix3(parsed[0], parsed[1], parsed[2], parsed[3], parsed[4], parsed[5], parsed[6], parsed[7], parsed[8])
            : new Matrix3();
    }

    private static Matrix3x4 ParseMatrix3x4(string value)
    {
        var parsed = ParseFloats(value);
        return parsed.Length >= 12
            ? new Matrix3x4(parsed[0], parsed[1], parsed[2], parsed[3], parsed[4], parsed[5], parsed[6], parsed[7], parsed[8], parsed[9], parsed[10], parsed[11])
            : new Matrix3x4();
    }

    private static Matrix4 ParseMatrix4(string value)
    {
        var parsed = ParseFloats(value);
        return parsed.Length >= 16
            ? new Matrix4(parsed[0], parsed[1], parsed[2], parsed[3], parsed[4], parsed[5], parsed[6], parsed[7], parsed[8], parsed[9], parsed[10], parsed[11], parsed[12], parsed[13], parsed[14], parsed[15])
            : new Matrix4();
    }

    private static Quaternion ParseQuaternion(string value)
    {
        var parsed = ParseFloats(value);
        return parsed.Length >= 4 ? new Quaternion(parsed[0], parsed[1], parsed[2], parsed[3]) : Quaternion.IDENTITY;
    }

    private static Rect ParseRect(string value)
    {
        var parsed = ParseFloats(value);
        return parsed.Length >= 4 ? new Rect(parsed[0], parsed[1], parsed[2], parsed[3]) : new Rect();
    }

    private static Vector2f ParseVector2(string value)
    {
        var parsed = ParseFloats(value);
        return parsed.Length >= 2 ? new Vector2f(parsed[0], parsed[1]) : Vector2f.Zero;
    }

    private static Vector3f ParseVector3(string value)
    {
        var parsed = ParseFloats(value);
        return parsed.Length >= 3 ? new Vector3f(parsed[0], parsed[1], parsed[2]) : Vector3f.Zero;
    }

    private static Vector4f ParseVector4(string value)
    {
        var parsed = ParseFloats(value);
        return parsed.Length >= 4 ? new Vector4f(parsed[0], parsed[1], parsed[2], parsed[3]) : Vector4f.Zero;
    }

}
