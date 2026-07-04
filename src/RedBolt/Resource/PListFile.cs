namespace RedBolt;

public sealed class PListFile : Resource
{
    private readonly Dictionary<string, PListValue> root = new(StringComparer.Ordinal);

    public PListFile(string name)
        : base(name)
    {
    }

    public IReadOnlyDictionary<string, PListValue> Root => root;

    public IReadOnlyDictionary<string, PListValue> GetRoot() => Root;

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var xmlFile = new XMLFile(Name);
        if (!xmlFile.Load(source))
            return false;

        var plistElement = xmlFile.GetRoot("plist");
        if (plistElement.IsNull)
            return false;

        root.Clear();
        var dictElement = plistElement.GetChild("dict");
        if (!LoadDict(root, dictElement))
            return false;

        MemoryUse = source.CanSeek
            ? (uint)Math.Min(uint.MaxValue, source.Length)
            : (uint)System.Text.Encoding.UTF8.GetByteCount(xmlFile.ToString());
        return true;
    }

    private bool LoadDict(Dictionary<string, PListValue> destination, XMLElement dictElement)
    {
        if (dictElement.IsNull)
            return false;

        var keyElement = dictElement.GetChild("key");
        while (keyElement.NotNull)
        {
            var valueElement = keyElement.GetNext();
            if (valueElement.IsNull)
                return false;

            var value = new PListValue();
            if (!LoadValue(value, valueElement))
                return false;

            destination[keyElement.GetValue()] = value;
            keyElement = valueElement.GetNext("key");
        }

        return true;
    }

    private bool LoadArray(List<PListValue> destination, XMLElement arrayElement)
    {
        if (arrayElement.IsNull)
            return false;

        for (var valueElement = arrayElement.GetChild(); valueElement.NotNull; valueElement = valueElement.GetNext())
        {
            var value = new PListValue();
            if (!LoadValue(value, valueElement))
                return false;

            destination.Add(value);
        }

        return true;
    }

    private bool LoadValue(PListValue value, XMLElement valueElement)
    {
        ArgumentNullException.ThrowIfNull(value);
        var valueType = valueElement.Name;
        switch (valueType)
        {
            case "string":
                value.SetString(valueElement.GetValue());
                return true;

            case "real":
                value.SetFloat(float.TryParse(valueElement.GetValue(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var floatValue) ? floatValue : 0.0f);
                return true;

            case "integer":
                value.SetInt(int.TryParse(valueElement.GetValue(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var intValue) ? intValue : 0);
                return true;

            case "true":
                value.SetBool(true);
                return true;

            case "false":
                value.SetBool(false);
                return true;

            case "dict":
                return LoadDict(value.ConvertToValueMap(), valueElement);

            case "array":
                return LoadArray(value.ConvertToValueVector(), valueElement);

            default:
                return false;
        }
    }
}
