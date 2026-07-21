using Microsoft.Extensions.Logging;

namespace RedBolt;

public class PListFile : Resource
{
    private readonly Dictionary<string, PListValue> _root = new(StringComparer.Ordinal);

    public PListFile(string name = "")
        : base(name)
    {
    }

    public override void Dispose()
    {
        foreach (var value in _root.Values)
            value.Dispose();
        _root.Clear();
        base.Dispose();
    }

    public static void RegisterObject(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    public IReadOnlyDictionary<string, PListValue> Root => _root;

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (Name.Length == 0 && source is FileStream fileStream)
            SetName(fileStream.Name);

        using var xmlFile = new XMLFile();
        if (!xmlFile.Load(source))
        {
            Log.GetLogger().LogError("Could not load property list");
            return false;
        }

        var plistElement = xmlFile.GetRoot("plist");
        if (plistElement.IsNull)
        {
            Log.GetLogger().LogError("Invalid property list file");
            return false;
        }

        foreach (var value in _root.Values)
            value.Dispose();
        _root.Clear();
        var dictElement = plistElement.GetChild("dict");
        if (!LoadDict(_root, dictElement))
            return false;

        MemoryUse = source.CanSeek ? checked((int)source.Length) : 0;
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

            using var value = new PListValue();
            if (!LoadValue(value, valueElement))
                return false;

            var key = keyElement.GetValue();
            if (destination.TryGetValue(key, out var previousValue))
                previousValue.Dispose();
            destination[key] = new PListValue(value);
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
            using var value = new PListValue();
            if (!LoadValue(value, valueElement))
                return false;

            destination.Add(new PListValue(value));
        }

        return true;
    }

    private bool LoadValue(PListValue value, XMLElement valueElement)
    {
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
                Log.GetLogger().LogError("Supported value type");
                return false;
        }
    }
}
