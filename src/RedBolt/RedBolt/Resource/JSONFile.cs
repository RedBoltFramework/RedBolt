namespace RedBolt.Resource;

using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Logging;

/// <summary>JSON document resource.</summary>
[Reflected]
public partial class JSONFile : Resource
{
    /// <summary>Construct.</summary>
    public JSONFile()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        Root.Dispose();
        base.Dispose();
    }


    /// <summary>Load resource from stream. May be called from a worker thread. Return true if successful.</summary>
    public override bool BeginLoad(Deserializer source)
    {
        var dataSize = source.Size;
        if (dataSize == 0 && !string.IsNullOrEmpty(source.Name))
        {
            Log.GetLogger().LogError("Zero sized JSON data in {Name}", source.Name);
            return false;
        }

        var data = new byte[checked((int)dataSize)];
        if (source.Read(data) != dataSize)
            return false;

        var text = Encoding.UTF8.GetString(data);
        var terminatorPosition = text.IndexOf('\0');
        if (terminatorPosition >= 0)
            text = text[..terminatorPosition];

        JSONValue value;
        try
        {
            var options = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            value = JSONValue.FromJsonNode(JsonNode.Parse(text, documentOptions: options));
        }
        catch (JsonException)
        {
            Log.GetLogger().LogError("Could not parse JSON data from {Name}", source.Name);
            return false;
        }

        using (value)
            Root.Set(value);

        SetMemoryUse(checked((int)dataSize));
        return true;
    }

    /// <summary>Save resource with default indentation (one tab). Return true if successful.</summary>
    public override bool Save(Serializer destination)
    {
        return Save(destination, "\t");
    }

    /// <summary>Save resource with user-defined indentation, only the first character (if any) of the string is used and the length of the string defines the character count. Return true if successful.</summary>
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
            Root.Clear();
            using var archive = new JSONOutputArchive(this);
            serializeValue(archive);
            return true;
        }
        catch (ArchiveException exception)
        {
            Root.Clear();
            Log.GetLogger().LogError("Failed to save object to JSON: {Error}", exception.Message);
            return false;
        }
    }

    /// <summary>Load objects using Archive serialization.</summary>
    public bool LoadObjectCallback(Action<Archive> serializeValue)
    {
        try
        {
            using var archive = new JSONInputArchive(this);
            serializeValue(archive);
            return true;
        }
        catch (ArchiveException exception)
        {
            Log.GetLogger().LogError("Failed to load object from JSON: {Error}", exception.Message);
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

        var bytes = Encoding.UTF8.GetBytes(source);
        using var buffer = new MemoryBuffer(bytes);
        return Load(buffer);
    }

    /// <summary>Save to a string.</summary>
    public override string ToString() => ToString("\t");

    /// <summary>Save to a string.</summary>
    public string ToString(string indentation)
    {
        var rootNode = Root.ToJsonNode();
        if (rootNode is null)
            return "null";

        var options = new JsonSerializerOptions { WriteIndented = true };
        var formatted = rootNode.ToJsonString(options);
        var lines = formatted.Split('\n');
        for (var i = 1; i < lines.Length; ++i)
        {
            var spaces = 0;
            while (spaces < lines[i].Length && lines[i][spaces] == ' ')
                ++spaces;
            var depth = spaces / 2;
            lines[i] = new string(indentation.Length != 0 ? indentation[0] : ' ', depth * indentation.Length)
                + lines[i][spaces..];
        }
        return string.Join('\n', lines);
    }

    /// <summary>Return root value.</summary>
    public JSONValue Root { get; } = new();

    /// <summary>Return true if parsing json string into JSONValue succeeds.</summary>
    public static bool ParseJSON(string json, JSONValue value, bool reportError = true)
    {
        try
        {
            using var parsedValue = JSONValue.FromJsonNode(JsonNode.Parse(json));
            value.Set(parsedValue);
            return true;
        }
        catch (JsonException exception)
        {
            if (reportError)
                Log.GetLogger().LogError("Could not parse JSON data from string with error: {Error}", exception.Message);
            return false;
        }
    }
}
