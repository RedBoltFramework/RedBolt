using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RedBolt;

public sealed class JSONFile : Resource
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    public JSONFile(string name)
        : base(name)
    {
    }

    public JsonNode? Root { get; private set; }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return FromString(reader.ReadToEnd());
    }

    public bool FromString(string source)
    {
        try
        {
            Root = JsonNode.Parse(source);
            MemoryUse = (uint)Encoding.UTF8.GetByteCount(source);
            return Root is not null;
        }
        catch (JsonException)
        {
            Root = null;
            MemoryUse = 0;
            return false;
        }
    }

    public JsonNode? GetRoot() => Root;

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
        if (Root is null)
            return string.Empty;

        return string.IsNullOrEmpty(indentation)
            ? Root.ToJsonString()
            : Root.ToJsonString(IndentedOptions);
    }

    public static bool ParseJSON(string json, out JsonNode? value, bool reportError = true)
    {
        try
        {
            value = JsonNode.Parse(json);
            return value is not null;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
    }
}
