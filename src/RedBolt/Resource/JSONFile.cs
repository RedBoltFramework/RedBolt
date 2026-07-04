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
        Root.SetChangeCallbackRecursive(RefreshMemoryUse);
    }

    public JSONValue Root { get; private set; } = new();

    public JsonNode? RootNode => Root.ToJsonNode();

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return FromString(reader.ReadToEnd());
    }

    public bool FromString(string source)
    {
        if (!ParseJSON(source, out var value))
        {
            Root = new JSONValue();
            Root.SetChangeCallbackRecursive(RefreshMemoryUse);
            MemoryUse = 0;
            return false;
        }

        Root = value;
        Root.SetChangeCallbackRecursive(RefreshMemoryUse);
        RefreshMemoryUse();
        return true;
    }

    public JSONValue GetRoot() => Root;

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
        var rootNode = Root.ToJsonNode();
        if (rootNode is null)
            return string.Empty;

        return string.IsNullOrEmpty(indentation)
            ? rootNode.ToJsonString()
            : rootNode.ToJsonString(IndentedOptions);
    }

    public static bool ParseJSON(string json, out JSONValue value, bool reportError = true)
    {
        try
        {
            value = JSONValue.FromJsonNode(JsonNode.Parse(json));
            return true;
        }
        catch (JsonException)
        {
            value = new JSONValue();
            return false;
        }
    }

    private void RefreshMemoryUse()
    {
        MemoryUse = (uint)Encoding.UTF8.GetByteCount(ToString());
    }
}
