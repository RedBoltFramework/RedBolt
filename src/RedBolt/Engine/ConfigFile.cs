using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Configuration parameter description.</summary>
public sealed class ConfigVariableDefinition
{
    public ConfigVariableDefinition SetDefault(Variant value)
    {
        DefaultValue = value;
        Type = value.Type;
        return this;
    }

    public ConfigVariableDefinition SetOptional(VariantType type) { Type = type; return this; }
    public ConfigVariableDefinition Overridable() { IsOverridable = true; return this; }
    public ConfigVariableDefinition CommandLinePriority() { HasCommandLinePriority = true; return this; }

    public bool IsOverridable;
    public bool HasCommandLinePriority;
    public Variant DefaultValue;
    public VariantType Type;
}

/// <summary>A class responsible for serializing configuration parameters.</summary>
public sealed class ConfigFile : Object
{
    public struct ConfigFlavor
    {
        public ApplicationFlavorPattern Flavor;
        public Dictionary<string, Variant> Variables;

        public void SerializeInBlock(Archive archive)
        {
            var count = archive.IsInput ? 0u : (uint)(Flavor.Components?.Count ?? 0);
            using var arrayBlock = archive.OpenArrayBlock("Flavor", count);
            if (archive.IsInput)
            {
                Flavor.Components ??= new ApplicationFlavorMap();
                for (var i = 0u; i < arrayBlock.SizeHint; ++i)
                {
                    using var itemBlock = archive.OpenUnorderedBlock("item");
                    var key = string.Empty;
                    var tags = new List<string>();
                    ArchiveSerialization.SerializeValue(archive, "key", ref key);
                    ArchiveSerialization.SerializeStringVector(archive, "tags", tags);
                    if (!string.IsNullOrEmpty(key))
                    {
                        Flavor.Components[key] = new ApplicationFlavorComponent(tags);
                    }
                }
            }
            else
            {
                foreach (var (key, tags) in Flavor.Components ?? new ApplicationFlavorMap())
                {
                    using var itemBlock = archive.OpenUnorderedBlock("item");
                    var k = key;
                    var tagList = tags.ToList();
                    ArchiveSerialization.SerializeValue(archive, "key", ref k);
                    ArchiveSerialization.SerializeStringVector(archive, "tags", tagList);
                }
            }

            var varMap = Variables ??= new Dictionary<string, Variant>(StringComparer.Ordinal);
            var varCount = archive.IsInput ? 0u : (uint)varMap.Count;
            using var varArrayBlock = archive.OpenArrayBlock("Variables", varCount);
            if (archive.IsInput)
            {
                varMap.Clear();
                for (var i = 0u; i < varArrayBlock.SizeHint; ++i)
                {
                    using var itemBlock = archive.OpenUnorderedBlock("item");
                    var key = string.Empty;
                    var value = default(Variant);
                    ArchiveSerialization.SerializeValue(archive, "key", ref key);
                    ArchiveSerialization.SerializeVariantInBlock(archive, ref value);
                    if (!string.IsNullOrEmpty(key))
                        varMap[key] = value;
                }
            }
            else
            {
                foreach (var (name, value) in varMap)
                {
                    using var itemBlock = archive.OpenUnorderedBlock("item");
                    var key = name;
                    var v = value;
                    ArchiveSerialization.SerializeValue(archive, "key", ref key);
                    ArchiveSerialization.SerializeVariantInBlock(archive, ref v);
                }
            }
        }
    }

    private readonly List<ConfigFlavor> _variablesPerFlavor = [];
    private readonly Dictionary<string, ConfigVariableDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Variant> _variables = new(StringComparer.Ordinal);

    public ConfigFile()
    {
    }

    public List<ConfigFlavor> VariablesPerFlavor => _variablesPerFlavor;
    public Dictionary<string, ConfigVariableDefinition> Definitions => _definitions;
    public Dictionary<string, Variant> Variables => _variables;

    public ConfigVariableDefinition DefineVariable(string name, Variant defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_definitions.TryGetValue(name, out var desc))
        {
            desc = new ConfigVariableDefinition();
            _definitions[name] = desc;
        }
        return desc.SetDefault(defaultValue);
    }

    public void DefineVariables(IReadOnlyDictionary<string, Variant> defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        foreach (var (name, value) in defaults)
            DefineVariable(name, value);
    }

    public void UpdatePriorityVariables(IReadOnlyDictionary<string, Variant> defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        foreach (var (name, value) in defaults)
        {
            if (!_definitions.TryGetValue(name, out var desc)) continue;
            if (desc.HasCommandLinePriority) desc.SetDefault(value);
        }
    }

    public void SetVariable(string name, Variant value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!value.IsEmpty) _variables[name] = value;
        else _variables.Remove(name);
    }

    public bool HasVariable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _variables.ContainsKey(name);
    }

    public ConfigVariableDefinition? GetVariableDefinition(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _definitions.TryGetValue(name, out var def) ? def : null;
    }

    public Variant GetVariable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_variables.TryGetValue(name, out var value) && !value.IsEmpty)
            return value;
        return _definitions.TryGetValue(name, out var def) ? def.DefaultValue : default;
    }

    /// <summary>Serialize persistent variable configuration. Current values are not serialized.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        var count = archive.IsInput ? 0u : (uint)_variablesPerFlavor.Count;
        using var arrayBlock = archive.OpenArrayBlock("Default", count);
        if (archive.IsInput)
        {
            _variablesPerFlavor.Clear();
            for (var i = 0u; i < arrayBlock.SizeHint; ++i)
            {
                using var itemBlock = archive.OpenUnorderedBlock("item");
                var flavor = new ConfigFlavor();
                flavor.SerializeInBlock(archive);
                _variablesPerFlavor.Add(flavor);
            }
        }
        else
        {
            foreach (var flavor in _variablesPerFlavor)
            {
                using var itemBlock = archive.OpenUnorderedBlock("item");
                var f = flavor;
                f.SerializeInBlock(archive);
            }
        }
    }

    /// <summary>Load default variables from a file in the virtual file system.</summary>
    public bool LoadDefaults(string fileName, ApplicationFlavor flavor)
    {
        var fileSystem = GetSubsystem<VirtualFileSystem>();
        var fileIdentifier = FileIdentifier.FromUri(fileName);
        if (fileSystem is null || !fileSystem.Exists(fileIdentifier))
        {
            Log.Logger.LogInformation("ConfigFile '{FileName}' is not found.", fileName);
            return false;
        }

        var jsonFile = new JSONFile();
        if (!jsonFile.FromString(fileSystem.ReadAllText(fileIdentifier)))
        {
            Log.Logger.LogError("Failed to load JSON settings file '{FileName}'.", fileName);
            return false;
        }

        if (jsonFile.Root.ToJsonNode() is not JsonObject rootObject
            || rootObject["Default"] is not JsonArray defaults)
        {
            Log.Logger.LogError("Failed to parse settings file '{FileName}'.", fileName);
            return false;
        }

        _variablesPerFlavor.Clear();
        foreach (var defaultNode in defaults)
        {
            if (defaultNode is not JsonObject defaultObject)
                continue;

            var configFlavor = new ConfigFlavor
            {
                Flavor = new ApplicationFlavorPattern(),
                Variables = new Dictionary<string, Variant>(StringComparer.Ordinal)
            };

            if (defaultObject["Flavor"] is JsonArray flavorArray)
            {
                foreach (var flavorNode in flavorArray)
                {
                    if (flavorNode is not JsonObject flavorObject)
                        continue;

                    var key = flavorObject["key"]?.GetValue<string>() ?? string.Empty;
                    if (string.IsNullOrEmpty(key))
                        continue;

                    IEnumerable<string> tags = flavorObject["tags"] is JsonArray tagArray
                        ? tagArray.Select(static tag => tag?.GetValue<string>() ?? string.Empty)
                            .Where(static tag => !string.IsNullOrEmpty(tag))
                        : Array.Empty<string>();
                    configFlavor.Flavor.Components[key] = new ApplicationFlavorComponent(tags);
                }
            }

            if (defaultObject["Variables"] is JsonArray variablesArray)
            {
                foreach (var variableNode in variablesArray)
                {
                    if (variableNode is not JsonObject variableObject)
                        continue;

                    var key = variableObject["key"]?.GetValue<string>() ?? string.Empty;
                    if (!string.IsNullOrEmpty(key))
                        configFlavor.Variables[key] = JSONValue.FromJsonNode(variableObject).GetVariant();
                }
            }

            _variablesPerFlavor.Add(configFlavor);
        }

        foreach (var (name, value) in GetFlavorVariables(flavor))
            SetVariable(name, value);

        Log.Logger.LogInformation("ConfigFile '{FileName}' is loaded with flavor '{Flavor}'.", fileName, flavor);
        return true;
    }

    /// <summary>Load variable overrides from a file in the virtual file system.</summary>
    public bool LoadOverrides(string fileName)
    {
        var fileSystem = GetSubsystem<VirtualFileSystem>();
        var fileIdentifier = FileIdentifier.FromUri(fileName);
        if (fileSystem is null || !fileSystem.Exists(fileIdentifier))
        {
            Log.Logger.LogInformation("ConfigFile overrides '{FileName}' are not found.", fileName);
            return false;
        }

        var jsonFile = new JSONFile();
        if (!jsonFile.FromString(fileSystem.ReadAllText(fileIdentifier)))
        {
            Log.Logger.LogError("Failed to load JSON settings file '{FileName}'.", fileName);
            return false;
        }

        var rootNode = jsonFile.Root.ToJsonNode();
        if (rootNode is JsonObject rootObject)
        {
            foreach (var (name, node) in rootObject)
            {
                if (node is not JsonObject)
                    continue;

                var value = JSONValue.FromJsonNode(node).GetVariant();
                ApplyOverride(name, value);
            }
        }
        else
        {
            foreach (var (name, value) in jsonFile.Root.GetStringVariantMap())
                ApplyOverride(name, value);
        }

        Log.Logger.LogInformation("ConfigFile overrides '{FileName}' are loaded.", fileName);
        return true;
    }

    /// <summary>Save variable overrides to a file in the virtual file system.</summary>
    public bool SaveOverrides(string fileName, ApplicationFlavor flavor)
    {
        var fileSystem = GetSubsystem<VirtualFileSystem>();
        if (fileSystem is null)
            return false;

        var root = new JsonObject();
        foreach (var (name, value) in GetChangedVariables(flavor))
        {
            var jsonValue = new JSONValue();
            jsonValue.SetVariant(value);
            root[name] = jsonValue.ToJsonNode();
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        return fileSystem.WriteAllText(FileIdentifier.FromUri(fileName), root.ToJsonString(options));
    }

    public Dictionary<string, Variant> GetFlavorVariables(ApplicationFlavor flavor)
    {
        var filtered = new List<(uint penalty, ConfigFlavor configFlavor)>();
        foreach (var cf in _variablesPerFlavor)
        {
            var penalty = flavor.Matches(cf.Flavor);
            if (penalty.HasValue)
                filtered.Add(((uint)penalty.Value, cf));
        }

        filtered.Sort((a, b) => b.penalty.CompareTo(a.penalty));

        var result = new Dictionary<string, Variant>(StringComparer.Ordinal);
        foreach (var (_, cf) in filtered)
        {
            foreach (var (key, value) in cf.Variables ?? new Dictionary<string, Variant>())
                result[key] = value;
        }
        return result;
    }

    public Dictionary<string, Variant> GetChangedVariables(ApplicationFlavor flavor)
    {
        var defaultValues = new Dictionary<string, Variant>(StringComparer.Ordinal);
        foreach (var (name, def) in _definitions)
            defaultValues[name] = def.DefaultValue;

        var flavorVars = GetFlavorVariables(flavor);
        foreach (var (name, value) in flavorVars)
            defaultValues[name] = value;

        var overrides = new Dictionary<string, Variant>(StringComparer.Ordinal);
        foreach (var (name, value) in _variables)
        {
            if (!defaultValues.TryGetValue(name, out var defVal)) continue;
            if (!defVal.Equals(value)) overrides[name] = value;
        }
        return overrides;
    }

    private void ApplyOverride(string name, Variant value)
    {
        var definition = GetVariableDefinition(name);
        if (definition is null)
        {
            Log.Logger.LogWarning("Ignoring override for unknown variable '{Name}'.", name);
            return;
        }

        if (!definition.IsOverridable)
        {
            Log.Logger.LogWarning("Ignoring override for non-overridable variable '{Name}'.", name);
            return;
        }

        if (definition.Type != value.Type)
        {
            Log.Logger.LogWarning(
                "Ignoring override for variable '{Name}' with invalid type {ActualType} ({ExpectedType} was expected).",
                name, value.Type, definition.Type);
            return;
        }

        SetVariable(name, value);
    }
}
