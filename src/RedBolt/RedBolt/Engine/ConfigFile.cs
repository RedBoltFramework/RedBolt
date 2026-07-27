using System.Text.Json.Nodes;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Configuration parameter description.</summary>
public sealed class ConfigVariableDefinition : ICloneable, IDisposable
{
    public ConfigVariableDefinition SetDefault(Variant value)
    {
        var replacement = value.Clone();
        DefaultValue.Dispose();
        DefaultValue = replacement;
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

    public ConfigVariableDefinition Clone()
    {
        return new ConfigVariableDefinition
        {
            IsOverridable = IsOverridable,
            HasCommandLinePriority = HasCommandLinePriority,
            DefaultValue = DefaultValue.Clone(),
            Type = Type
        };
    }

    object ICloneable.Clone() => Clone();

    public void Dispose()
    {
        DefaultValue.Dispose();
        DefaultValue = default;
    }
}

/// <summary>A class responsible for serializing configuration parameters.</summary>
public sealed class ConfigFile : RedBolt.Object
{
    private static readonly Variant EmptyVariable;

    public struct ConfigFlavor : ICloneable, IDisposable
    {
        public ApplicationFlavorPattern Flavor;
        public StringVariantMap Variables;

        public ConfigFlavor()
        {
            Flavor = new ApplicationFlavorPattern();
            Variables = new StringVariantMap();
        }

        public readonly ConfigFlavor Clone()
        {
            return new ConfigFlavor
            {
                Flavor = Flavor.Clone(),
                Variables = new StringVariantMap(Variables)
            };
        }

        readonly object ICloneable.Clone() => Clone();

        public void Dispose()
        {
            Variables?.Dispose();
            Variables = null!;
            Flavor = default;
        }

        public void SerializeInBlock(Archive archive)
        {
            var count = archive.IsInput ? 0 : Flavor.Components?.Count ?? 0;
            using var arrayBlock = archive.OpenArrayBlock("Flavor", count);
            if (archive.IsInput)
            {
                Flavor.Components ??= new ApplicationFlavorMap();
                Flavor.Components.Clear();
                for (var i = 0; i < arrayBlock.SizeHint; ++i)
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

            var varMap = Variables ??= new StringVariantMap();
            var varCount = archive.IsInput ? 0 : varMap.Count;
            using var varArrayBlock = archive.OpenArrayBlock("Variables", varCount);
            if (archive.IsInput)
            {
                varMap.Dispose();
                for (var i = 0; i < varArrayBlock.SizeHint; ++i)
                {
                    using var itemBlock = archive.OpenUnorderedBlock("item");
                    var key = string.Empty;
                    var value = default(Variant);
                    try
                    {
                        ArchiveSerialization.SerializeValue(archive, "key", ref key);
                        ArchiveSerialization.SerializeVariantInBlock(archive, ref value);
                        if (!string.IsNullOrEmpty(key))
                            varMap[key] = value;
                    }
                    finally
                    {
                        value.Dispose();
                    }
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
    private readonly StringVariantMap _variables = new();

    public ConfigFile()
    {
    }

    public override void Dispose()
    {
        _variables.Dispose();
        foreach (var definition in _definitions.Values)
            definition.Dispose();
        _definitions.Clear();
        for (var i = _variablesPerFlavor.Count - 1; i >= 0; --i)
        {
            var flavor = _variablesPerFlavor[i];
            flavor.Dispose();
        }
        _variablesPerFlavor.Clear();
        base.Dispose();
    }

    public List<ConfigFlavor> VariablesPerFlavor => _variablesPerFlavor;
    public Dictionary<string, ConfigVariableDefinition> Definitions => _definitions;
    public StringVariantMap Variables => _variables;

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
        if (!value.IsEmpty)
            _variables[name] = value;
        else
            _variables.Remove(name);
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

    public ref readonly Variant GetVariable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ref var value = ref CollectionsMarshal.GetValueRefOrNullRef(_variables, name);
        if (!Unsafe.IsNullRef(ref value) && !value.IsEmpty)
            return ref value;
        if (_definitions.TryGetValue(name, out var definition))
            return ref definition.DefaultValue;
        return ref EmptyVariable;
    }

    /// <summary>Serialize persistent variable configuration. Current values are not serialized.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        var count = archive.IsInput ? 0 : _variablesPerFlavor.Count;
        using var arrayBlock = archive.OpenArrayBlock("Default", count);
        if (archive.IsInput)
        {
            for (var i = _variablesPerFlavor.Count - 1; i >= 0; --i)
            {
                var oldFlavor = _variablesPerFlavor[i];
                oldFlavor.Dispose();
            }
            _variablesPerFlavor.Clear();
            for (var i = 0; i < arrayBlock.SizeHint; ++i)
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
        if (!fileSystem!.Exists(fileIdentifier))
        {
            Log.GetLogger().LogInformation("ConfigFile '{FileName}' is not found.", fileName);
            return false;
        }

        using var jsonFile = new JSONFile();
        if (!jsonFile.FromString(fileSystem.ReadAllText(fileIdentifier)))
        {
            Log.GetLogger().LogError("Failed to load JSON settings file '{FileName}'.", fileName);
            return false;
        }

        if (jsonFile.Root.ToJsonNode() is not JsonObject rootObject
            || rootObject["Default"] is not JsonArray defaults)
        {
            Log.GetLogger().LogError("Failed to parse settings file '{FileName}'.", fileName);
            return false;
        }

        for (var i = _variablesPerFlavor.Count - 1; i >= 0; --i)
        {
            var oldFlavor = _variablesPerFlavor[i];
            oldFlavor.Dispose();
        }
        _variablesPerFlavor.Clear();
        foreach (var defaultNode in defaults)
        {
            if (defaultNode is not JsonObject defaultObject)
                continue;

            var configFlavor = new ConfigFlavor
            {
                Flavor = new ApplicationFlavorPattern(),
                Variables = new StringVariantMap()
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
                    {
                        using var value = JSONValue.FromJsonNode(variableObject).GetVariant();
                        configFlavor.Variables[key] = value;
                    }
                }
            }

            _variablesPerFlavor.Add(configFlavor);
        }

        using var flavorVariables = GetFlavorVariables(flavor);
        foreach (var (name, value) in flavorVariables)
            SetVariable(name, value);

        Log.GetLogger().LogInformation("ConfigFile '{FileName}' is loaded with flavor '{Flavor}'.", fileName, flavor);
        return true;
    }

    /// <summary>Load variable overrides from a file in the virtual file system.</summary>
    public bool LoadOverrides(string fileName)
    {
        var fileSystem = GetSubsystem<VirtualFileSystem>();
        var fileIdentifier = FileIdentifier.FromUri(fileName);
        if (!fileSystem!.Exists(fileIdentifier))
        {
            Log.GetLogger().LogInformation("ConfigFile overrides '{FileName}' are not found.", fileName);
            return false;
        }

        using var jsonFile = new JSONFile();
        if (!jsonFile.FromString(fileSystem.ReadAllText(fileIdentifier)))
        {
            Log.GetLogger().LogError("Failed to load JSON settings file '{FileName}'.", fileName);
            return false;
        }

        var rootNode = jsonFile.Root.ToJsonNode();
        if (rootNode is JsonObject rootObject)
        {
            foreach (var (name, node) in rootObject)
            {
                if (node is not JsonObject)
                    continue;

                using var value = JSONValue.FromJsonNode(node).GetVariant();
                ApplyOverride(name, value);
            }
        }
        else
        {
            using var overrides = jsonFile.Root.GetStringVariantMap();
            foreach (var (name, value) in overrides)
                ApplyOverride(name, value);
        }

        Log.GetLogger().LogInformation("ConfigFile overrides '{FileName}' are loaded.", fileName);
        return true;
    }

    /// <summary>Save variable overrides to a file in the virtual file system.</summary>
    public bool SaveOverrides(string fileName, ApplicationFlavor flavor)
    {
        var fileSystem = GetSubsystem<VirtualFileSystem>();
        using var file = fileSystem!.OpenFile(FileIdentifier.FromUri(fileName), FileMode.Write);
        if (file is null)
        {
            Log.GetLogger().LogError("Failed to write JSON settings to file '{FileName}'.", fileName);
            return false;
        }

        using var overrides = GetChangedVariables(flavor);
        using var jsonFile = new JSONFile();
        jsonFile.Root.SetStringVariantMap(overrides);
        if (!jsonFile.Save(file))
        {
            Log.GetLogger().LogError("Failed to save JSON settings to file '{FileName}'.", fileName);
            return false;
        }

        return true;
    }

    public StringVariantMap GetFlavorVariables(ApplicationFlavor flavor)
    {
        var filtered = new List<(uint penalty, int index)>();
        for (var i = 0; i < _variablesPerFlavor.Count; ++i)
        {
            var cf = _variablesPerFlavor[i];
            var penalty = flavor.Matches(cf.Flavor);
            if (penalty.HasValue)
                filtered.Add(((uint)penalty.Value, i));
        }

        filtered.Sort((a, b) =>
        {
            var penaltyComparison = b.penalty.CompareTo(a.penalty);
            return penaltyComparison != 0 ? penaltyComparison : a.index.CompareTo(b.index);
        });

        var result = new StringVariantMap();
        foreach (var (_, index) in filtered)
        {
            var variables = _variablesPerFlavor[index].Variables;
            if (variables is null)
                continue;
            foreach (var (key, value) in variables)
                result[key] = value;
        }
        return result;
    }

    public StringVariantMap GetChangedVariables(ApplicationFlavor flavor)
    {
        using var defaultValues = new StringVariantMap();
        foreach (var (name, def) in _definitions)
            defaultValues[name] = def.DefaultValue;

        using var flavorVars = GetFlavorVariables(flavor);
        foreach (var (name, value) in flavorVars)
            defaultValues[name] = value;

        var overrides = new StringVariantMap();
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
            Log.GetLogger().LogWarning("Ignoring override for unknown variable '{Name}'.", name);
            return;
        }

        if (!definition.IsOverridable)
        {
            Log.GetLogger().LogWarning("Ignoring override for non-overridable variable '{Name}'.", name);
            return;
        }

        if (definition.Type != value.Type)
        {
            Log.GetLogger().LogWarning(
                "Ignoring override for variable '{Name}' with invalid type {ActualType} ({ExpectedType} was expected).",
                name, value.Type, definition.Type);
            return;
        }

        SetVariable(name, value);
    }
}
