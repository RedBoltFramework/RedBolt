using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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

    // TODO: port - C++ code:
    // ~JSONFile() override;

    public JSONValue Root { get; private set; } = new();

    public static void RegisterObject(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    // C++: unsigned dataSize = source.GetSize();
    // C++: if (!dataSize && !source.GetName().empty()) { URHO3D_LOGERROR("Zero sized JSON data in " + source.GetName()); return false; }
    // C++: SetMemoryUse(dataSize) — raw data size, not UTF8 byte count.
    // C++: uses rapidjson::Document::Parse with kParseCommentsFlag | kParseTrailingCommasFlag.
    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = reader.ReadToEnd();
        if (!ParseResourceJSON(text, out var value))
            return false;
        Root = value;
        SetMemoryUse((uint)Encoding.UTF8.GetByteCount(text));
        return true;
    }

    // C++: if (source.empty()) return false;
    // C++: MemoryBuffer buffer(source.c_str(), source.length()); return Load(buffer);
    public bool FromString(string source)
    {
        if (source.Length == 0 || !ParseResourceJSON(source, out var value))
            return false;

        Root = value;
        SetMemoryUse((uint)Encoding.UTF8.GetByteCount(source));
        return true;
    }

    public JSONValue GetRoot() => Root;

    // C++: delegates to Save(dest, "\t").
    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var bytes = Encoding.UTF8.GetBytes(ToString());
        destination.Write(bytes);
        return true;
    }

    // C++: writer.SetIndent(!indendation.empty() ? indendation.front() : '\0', indendation.length());
    // C++: uses rapidjson PrettyWriter with character-count indentation (first char is the indent char,
    // length determines how many times to repeat).
    public bool Save(Stream destination, string indentation)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var bytes = Encoding.UTF8.GetBytes(ToString(indentation));
        destination.Write(bytes);
        return true;
    }

    // C++: root_.Clear(); JSONOutputArchive archive{this}; serializeValue(archive); return true;
    // C++: passes 'this' (the JSONFile) to the archive, which writes directly into the JSONFile's root.
    public bool SaveObjectCallback(Action<Archive> serializeValue)
    {
        ArgumentNullException.ThrowIfNull(serializeValue);
        try
        {
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

    // C++: JSONInputArchive archive{this}; serializeValue(archive); return true;
    // C++: passes 'this' (the JSONFile) to the archive, which reads from the JSONFile's root directly.
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

    // C++: uses rapidjson PrettyWriter with SetIndent; empty indentation → '\0' indent char (no indent).
    public override string ToString() => ToString("\t");

    // C++: writer.SetIndent(!indendation.empty() ? indendation.front() : ' ', indendation.length());
    // C++: default indentation is "\t", default in ToString(dest) overload is "\t".
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

    // C++: uses rapidjson document.Parse; if (reportError) logs URHO3D_LOGERRORF with parse error.
    // C++: return false when parse fails.
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
