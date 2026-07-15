using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace RedBolt;

public sealed class JSONFile : Resource
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };
    private static readonly JsonDocumentOptions ResourceParseOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public JSONFile(string name = "")
        : base(name)
    {
    }

    public JSONValue Root { get; private set; } = new();

    public static void RegisterObject(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var dataSize = source.CanSeek ? source.Length - source.Position : -1;
        if (dataSize == 0 && !string.IsNullOrEmpty(Name))
        {
            Log.Logger.LogError("Zero sized JSON data in {Name}", Name);
            return false;
        }

        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = reader.ReadToEnd();
        if (!ParseResourceJSON(text, out var value))
        {
            Log.Logger.LogError("Could not parse JSON data from {Name}", Name);
            return false;
        }

        Root = value;
        SetMemoryUse((uint)(dataSize >= 0 ? dataSize : Encoding.UTF8.GetByteCount(text)));
        return true;
    }

    public bool FromString(string source)
    {
        if (source.Length == 0 || !ParseResourceJSON(source, out var value))
            return false;

        Root = value;
        SetMemoryUse((uint)Encoding.UTF8.GetByteCount(source));
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

    public bool SaveObjectCallback(Action<Archive> serializeValue)
    {
        ArgumentNullException.ThrowIfNull(serializeValue);
        try
        {
            Root.Clear();
            var archive = new JSONOutputArchive(Context.Instance);
            serializeValue(archive);
            archive.Flush();
            Root = JSONValue.FromJsonNode(archive.Root);
            return true;
        }
        catch
        {
            Root.Clear();
            return false;
        }
    }

    public bool LoadObjectCallback(Action<Archive> serializeValue)
    {
        ArgumentNullException.ThrowIfNull(serializeValue);
        try
        {
            var archive = new JSONInputArchive(Context.Instance, Root.ToJsonNode()?.ToJsonString() ?? "null");
            serializeValue(archive);
            archive.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool SaveObject(Object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return SaveObjectCallback(value.SerializeInBlock);
    }

    public bool LoadObject(Object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return LoadObjectCallback(value.SerializeInBlock);
    }

    public override string ToString() => ToString("\t");

    public string ToString(string indentation)
    {
        var rootNode = Root.ToJsonNode();
        if (rootNode is null)
            return string.Empty;

        if (string.IsNullOrEmpty(indentation))
            return rootNode.ToJsonString();

        var formatted = rootNode.ToJsonString(IndentedOptions);
        var lines = formatted.Split('\n');
        for (var i = 1; i < lines.Length; ++i)
        {
            var spaces = 0;
            while (spaces < lines[i].Length && lines[i][spaces] == ' ')
                ++spaces;
            var depth = spaces / 2;
            lines[i] = new string(indentation[0], depth * indentation.Length) + lines[i][spaces..];
        }
        return string.Join('\n', lines);
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
            if (reportError)
                Log.Logger.LogError("Could not parse JSON data from string");
            return false;
        }
    }

    private static bool ParseResourceJSON(string json, out JSONValue value)
    {
        try
        {
            value = JSONValue.FromJsonNode(JsonNode.Parse(json, documentOptions: ResourceParseOptions));
            return true;
        }
        catch (JsonException)
        {
            value = new JSONValue();
            return false;
        }
    }
}
