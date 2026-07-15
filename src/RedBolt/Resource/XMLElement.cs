using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace RedBolt;

public sealed class XMLElement
{
    private static readonly Regex NumberPattern = new(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

    private readonly XMLFile? _file;
    private readonly XElement? _element;

    internal XMLElement(XMLFile? file, XElement? element)
    {
        this._file = file;
        this._element = element;
    }

    public bool IsNull => _element is null;

    public bool NotNull => _element is not null;

    public string Name => _element?.Name.LocalName ?? string.Empty;

    public string Value => _element?.Value ?? string.Empty;

    public XElement? Element => _element;

    public void SetName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_element is null)
            return;

        _element.Name = name;
        NotifyChanged();
    }

    public XMLElement CreateChild(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_element is null)
            return new XMLElement(_file, null);

        var child = new XElement(name);
        _element.Add(child);
        NotifyChanged();
        return new XMLElement(_file, child);
    }

    public XMLElement GetOrCreateChild(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetChild(name) is { NotNull: true } child ? child : CreateChild(name);
    }

    public bool AppendChild(XMLElement other, bool asCopy = false)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (_element is null || other._element is null)
            return false;

        var child = asCopy ? new XElement(other._element) : other._element;
        _element.Add(child);
        NotifyChanged();
        other._file?.RefreshMemoryUse();
        return true;
    }

    public bool Remove()
    {
        if (_element?.Parent is null)
            return false;

        _element.Remove();
        NotifyChanged();
        return true;
    }

    public bool RemoveChild(XMLElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (_element is null || child._element?.Parent != _element)
            return false;

        child._element.Remove();
        NotifyChanged();
        return true;
    }

    public bool RemoveChild(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var child = GetChild(name);
        return child.NotNull && RemoveChild(child);
    }

    public bool RemoveChildren(string name = "")
    {
        if (_element is null)
            return false;

        var children = string.IsNullOrEmpty(name)
            ? _element.Elements().ToArray()
            : _element.Elements(name).ToArray();
        if (children.Length == 0)
            return false;

        foreach (var child in children)
            child.Remove();

        NotifyChanged();
        return true;
    }

    public bool RemoveAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var attribute = _element?.Attribute(name);
        if (attribute is null)
            return false;

        attribute.Remove();
        NotifyChanged();
        return true;
    }

    public XMLElement SelectSingle(string query) => Select(query).FirstResult();

    public XMLElement SelectSinglePrepared(XPathQuery query) => SelectPrepared(query).FirstResult();

    public XPathResultSet Select(string query)
    {
        if (_element is null || string.IsNullOrWhiteSpace(query)) return new XPathResultSet();
        return new XPathResultSet(_element.XPathSelectElements(query), _file);
    }

    public XPathResultSet SelectPrepared(XPathQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Select(query.GetExpandedQuery());
    }

    public bool SetValue(string value)
    {
        if (_element is null)
            return false;

        _element.Value = value ?? string.Empty;
        NotifyChanged();
        return true;
    }

    public bool SetAttribute(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_element is null)
            return false;

        _element.SetAttributeValue(name, value ?? string.Empty);
        NotifyChanged();
        return true;
    }

    public bool SetBool(string name, bool value) => SetAttribute(name, value ? "true" : "false");

    public bool SetFloat(string name, float value) => SetAttribute(name, value.ToString(CultureInfo.InvariantCulture));

    public bool SetDouble(string name, double value) => SetAttribute(name, value.ToString(CultureInfo.InvariantCulture));

    public bool SetUInt(string name, uint value) => SetAttribute(name, value.ToString(CultureInfo.InvariantCulture));

    public bool SetInt(string name, int value) => SetAttribute(name, value.ToString(CultureInfo.InvariantCulture));

    public bool SetString(string name, string value) => SetAttribute(name, value);

    public bool SetColor(string name, Color value) => SetAttribute(name, value.ToString());

    public bool SetVector2(string name, Vector2f value) => SetAttribute(name, value.ToString());

    public bool SetVector3(string name, Vector3f value) => SetAttribute(name, value.ToString());

    public bool SetVector4(string name, Vector4f value) => SetAttribute(name, value.ToString());

    public bool SetQuaternion(string name, Quaternion value) => SetAttribute(name, value.ToString());

    public bool SetRect(string name, Rect value) => SetAttribute(name, value.ToString());

    public bool SetIntRect(string name, IntRect value) => SetAttribute(name, value.ToString());

    public bool SetIntVector2(string name, Vector2i value) => SetAttribute(name, value.ToString());

    public bool SetIntVector3(string name, Vector3i value) => SetAttribute(name, value.ToString());

    public bool SetMatrix3(string name, Matrix3 value) => SetAttribute(name, value.ToString());

    public bool SetMatrix3x4(string name, Matrix3x4 value) => SetAttribute(name, value.ToString());

    public bool SetMatrix4(string name, Matrix4 value) => SetAttribute(name, value.ToString());

    public bool SetBoundingBox(string name, BoundingBox value) => SetAttribute(name, FormatFloats(value.Min.X, value.Min.Y, value.Min.Z, value.Max.X, value.Max.Y, value.Max.Z));

    public bool SetBoundingBox(BoundingBox value) => SetValue(FormatFloats(value.Min.X, value.Min.Y, value.Min.Z, value.Max.X, value.Max.Y, value.Max.Z));

    public bool SetResourceRef(string name, ResourceRef value) => SetAttribute(name, value.ToString());

    public bool SetResourceRefList(string name, ResourceRefList value) => SetAttribute(name, value.ToString());

    public bool SetBuffer(string name, ReadOnlySpan<byte> value) => SetAttribute(name, Convert.ToBase64String(value));

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
            VariantType.ResourceRef => SetAttribute("value", value.GetResourceRef().ToString()),
            VariantType.ResourceRefList => SetAttribute("value", value.GetResourceRefList().ToString()),
            _ => SetAttribute("value", value.ToString())
        };
    }

    public bool SetVariantVector(VariantVector values)
    {
        RemoveChildren("variant");
        foreach (var value in values)
            if (!CreateChild("variant").SetVariant(value)) return false;
        return true;
    }

    public bool SetStringVector(StringVector values)
    {
        RemoveChildren("string");
        foreach (var value in values)
            if (!CreateChild("string").SetAttribute("value", value)) return false;
        return true;
    }

    public bool SetVariantMap(VariantMap values)
    {
        RemoveChildren("variant");
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
        RemoveChildren("variant");
        foreach (var (key, value) in values)
        {
            var element = CreateChild("variant");
            element.SetString("name", key);
            if (!element.SetVariant(value)) return false;
        }
        return true;
    }

    public bool HasChild(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _element?.Element(name) is not null;
    }

    public XMLElement GetChild(string name = "")
    {
        if (_element is null)
            return new XMLElement(_file, null);

        var child = string.IsNullOrEmpty(name)
            ? _element.Elements().FirstOrDefault()
            : _element.Element(name);
        return new XMLElement(_file, child);
    }

    public XMLElement GetNext(string name = "")
    {
        if (_element is null)
            return new XMLElement(_file, null);

        var sibling = string.IsNullOrEmpty(name)
            ? _element.ElementsAfterSelf().FirstOrDefault()
            : _element.ElementsAfterSelf(name).FirstOrDefault();
        return new XMLElement(_file, sibling);
    }

    public XMLElement GetParent() => new(_file, _element?.Parent);

    public uint GetNumAttributes() => (uint)(_element?.Attributes().Count() ?? 0);

    public bool HasAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _element?.Attribute(name) is not null;
    }

    public string GetValue() => Value;

    public string GetAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _element?.Attribute(name)?.Value ?? string.Empty;
    }

    // TODO: port - C++ code:
    // const char* GetAttributeCString(const char* name) const;
    // ea::string GetAttributeLower(const ea::string& name) const;
    // ea::string GetAttributeUpper(const ea::string& name) const;
    // unsigned long long GetUInt64(const ea::string& name) const;
    // long long GetInt64(const ea::string& name) const;
    // Vector4 GetVector(const ea::string& name) const;
    // Variant GetVectorVariant(const ea::string& name) const;
    // bool SetUInt64(const ea::string& name, unsigned long long value);
    // bool SetInt64(const ea::string& name, long long value);
    // bool SetVectorVariant(const ea::string& name, const Variant& value);
    // static const XMLElement EMPTY;

    public IReadOnlyList<string> GetAttributeNames()
    {
        return _element?.Attributes().Select(static attribute => attribute.Name.LocalName).ToArray() ?? Array.Empty<string>();
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
        if (string.IsNullOrEmpty(value))
            return [];

        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return [];
        }
    }

    public Variant GetVariant()
    {
        return GetVariantValue(Variant.GetTypeFromName(GetAttribute("type")));
    }

    public Variant GetVariantValue(VariantType type)
    {
        var result = type switch
        {
            VariantType.VariantVector => new Variant(GetVariantVector()),
            VariantType.StringVector => new Variant(GetStringVector()),
            VariantType.VariantMap => new Variant(GetVariantMap()),
            VariantType.StringVariantMap => new Variant(GetStringVariantMap()),
            _ => default
        };

        if (type is not (VariantType.VariantVector or VariantType.StringVector or VariantType.VariantMap or VariantType.StringVariantMap))
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
            result[new StringHash(element.GetUInt("hash"))] = element.GetVariant();
        return result;
    }

    public StringVariantMap GetStringVariantMap()
    {
        var result = new StringVariantMap();
        for (var element = GetChild("variant"); element.NotNull; element = element.GetNext("variant"))
            result[element.GetAttribute("name")] = element.GetVariant();
        return result;
    }

    public BoundingBox GetBoundingBox() => ParseBoundingBox(GetValue());

    public BoundingBox GetBoundingBox(string name) => ParseBoundingBox(GetAttribute(name));

    public Color GetColor(string name) => ParseColor(GetAttribute(name));

    public float GetFloat(string name) => float.TryParse(GetAttribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0.0f;

    public double GetDouble(string name) => double.TryParse(GetAttribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0.0;

    public uint GetUInt(string name) => uint.TryParse(GetAttribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0u;

    public int GetInt(string name) => int.TryParse(GetAttribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    public IntRect GetIntRect(string name) => ParseIntRect(GetAttribute(name));

    public Vector2i GetIntVector2(string name) => ParseIntVector2(GetAttribute(name));

    public Vector3i GetIntVector3(string name) => ParseIntVector3(GetAttribute(name));

    public Rect GetRect(string name) => ParseRect(GetAttribute(name));

    public Quaternion GetQuaternion(string name) => ParseQuaternion(GetAttribute(name));

    public string GetString(string name) => GetAttribute(name);

    public ResourceRef GetResourceRef(string name) => ResourceRef.Parse(GetAttribute(name));

    public ResourceRefList GetResourceRefList(string name) => ResourceRefList.Parse(GetAttribute(name));

    public Vector2f GetVector2(string name) => ParseVector2(GetAttribute(name));

    public Vector3f GetVector3(string name) => ParseVector3(GetAttribute(name));

    public Vector4f GetVector4(string name) => ParseVector4(GetAttribute(name));

    public Matrix3 GetMatrix3(string name) => ParseMatrix3(GetAttribute(name));

    public Matrix3x4 GetMatrix3x4(string name) => ParseMatrix3x4(GetAttribute(name));

    public Matrix4 GetMatrix4(string name) => ParseMatrix4(GetAttribute(name));

    public override string ToString() => _element?.ToString() ?? string.Empty;

    private static string FormatFloats(params float[] values)
    {
        return string.Join(' ', values.Select(static value => value.ToString(CultureInfo.InvariantCulture)));
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

    private static BoundingBox ParseBoundingBox(string value)
    {
        var parsed = ParseFloats(value);
        return parsed.Length >= 6
            ? new BoundingBox(new Vector3f(parsed[0], parsed[1], parsed[2]), new Vector3f(parsed[3], parsed[4], parsed[5]))
            : new BoundingBox();
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
        return parsed.Length >= 4 ? new Quaternion(parsed[0], parsed[1], parsed[2], parsed[3]) : Quaternion.Identity;
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

    private void NotifyChanged() => _file?.RefreshMemoryUse();
}
