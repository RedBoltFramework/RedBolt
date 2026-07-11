using System.Linq;

namespace RedBolt;

/// <summary>Configuration parameter description.</summary>
public struct ConfigVariableDefinition
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
                    var name = string.Empty;
                    var value = default(Variant);
                    ArchiveSerialization.SerializeValue(archive, "name", ref name);
                    using (archive.OpenUnorderedBlock("value"))
                        ArchiveSerialization.SerializeVariantInBlock(archive, ref value);
                    if (!string.IsNullOrEmpty(name))
                        varMap[name] = value;
                }
            }
            else
            {
                foreach (var (name, value) in varMap)
                {
                    using var itemBlock = archive.OpenUnorderedBlock("item");
                    var n = name;
                    var v = value;
                    ArchiveSerialization.SerializeValue(archive, "name", ref n);
                    using (archive.OpenUnorderedBlock("value"))
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

    public IReadOnlyList<ConfigFlavor> VariablesPerFlavor => _variablesPerFlavor;
    public IReadOnlyDictionary<string, ConfigVariableDefinition> Definitions => _definitions;
    public IReadOnlyDictionary<string, Variant> Variables => _variables;

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
}
