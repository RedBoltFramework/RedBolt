using System.Globalization;
using System.Text;
using System.Text.Json;

using System.Diagnostics;

namespace RedBolt;

/// <summary>
/// Base class for serializable resource that uses Archive serialization.
/// </summary>
// TODO: port - C++ code:
// C++ inherits from SimpleResource (not Resource) and serializes via SerializeInBlock(Archive& archive)
// instead of BeginLoad/Save with XML/JSON parsing.
public sealed class RenderPath : Resource
{
    private readonly List<RenderPass> _passes = [];
    private RenderPassTraits _traits;

    public RenderPath(string name)
        : base(name)
    {
    }

    public IReadOnlyList<RenderPass> GetPasses() => _passes;

    public RenderPassTraits GetAggregatedPassTraits() => _traits;

    /// <summary>
    /// Fill missing parameters with defaults.
    /// </summary>
    public void CollectParameters(IDictionary<string, object?> parameters)
    {
        foreach (var pass in _passes)
            pass.CollectParameters(parameters);
    }

    /// <summary>
    /// Initialize render path before using it in view.
    /// </summary>
    public void InitializeView(RenderPipelineView view)
    {
        foreach (var pass in _passes)
            pass.InitializeView(view);
    }

    /// <summary>
    /// Update settings for all passes.
    /// </summary>
    public void UpdateParameters(RenderPipelineSettings settings, IReadOnlyList<RenderPassToggle> enabledPasses, IReadOnlyDictionary<string, object?> parameters)
    {
        if (enabledPasses.Count != _passes.Count)
        {
            Debug.Assert(false, $"Number of passes in render path ({_passes.Count}) does not match with enabled passes array size ({enabledPasses.Count})");
            return;
        }

        for (var i = 0; i < _passes.Count; ++i)
        {
            var pass = _passes[i];
            pass.SetEnabled(enabledPasses[i].Enabled);
            pass.UpdateParameters(settings, parameters);
        }

        _traits = default;
        foreach (var pass in _passes)
        {
            var passTraits = pass.GetTraits();
            _traits = new RenderPassTraits(
                _traits.NeedReadWriteColorBuffer || passTraits.NeedReadWriteColorBuffer,
                _traits.NeedBilinearColorSampler || passTraits.NeedBilinearColorSampler);
        }
    }

    /// <summary>
    /// Perform update that does not invoke any rendering commands.
    /// </summary>
    public void Update(SharedRenderPassState sharedState)
    {
        foreach (var pass in _passes)
        {
            if (pass.IsEnabledEffectively())
                pass.Update(sharedState);
        }
    }

    /// <summary>
    /// Execute render commands.
    /// </summary>
    public void Render(SharedRenderPassState sharedState)
    {
        foreach (var pass in _passes)
        {
            if (pass.IsEnabledEffectively())
                pass.Render(sharedState);
        }
    }

    /// <summary>
    /// Make deep copy of RenderPath.
    /// </summary>
    public RenderPath Clone()
    {
        var clone = new RenderPath(Name);
        foreach (var pass in _passes)
            clone.AddPass(pass.Clone());
        return clone;
    }

    /// <summary>
    /// Repair the array of enabled render passes.
    /// </summary>
    public IReadOnlyList<RenderPassToggle> RepairEnabledRenderPasses(IEnumerable<RenderPassToggle> sourcePasses)
    {
        var sourceList = sourcePasses as IReadOnlyList<RenderPassToggle> ?? sourcePasses.ToArray();

        if (sourceList.Count == _passes.Count)
        {
            var matches = true;
            for (var i = 0; i < _passes.Count; ++i)
            {
                if (!string.Equals(_passes[i].GetPassName(), sourceList[i].Name, StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return sourceList;
        }

        var result = new RenderPassToggle[_passes.Count];
        for (var i = 0; i < _passes.Count; ++i)
        {
            var pass = _passes[i];
            RenderPassToggle match = default;
            for (var j = 0; j < sourceList.Count; ++j)
            {
                if (string.Equals(sourceList[j].Name, pass.GetPassName(), StringComparison.Ordinal))
                {
                    match = sourceList[j];
                    break;
                }
            }

            var enabled = match.Name is not null ? match.Enabled : pass.IsEnabledByDefault();
            result[i] = new RenderPassToggle(pass.GetPassName(), enabled);
        }
        return result;
    }

    public void AddPass(RenderPass pass)
    {
        _passes.Add(pass);
    }

    public void RemovePass(RenderPass pass)
    {
        _passes.Remove(pass);
    }

    public void ReorderPass(RenderPass pass, uint index)
    {
        if (index >= _passes.Count)
            return;

        var oldIndex = _passes.IndexOf(pass);
        if (oldIndex < 0)
            return;

        _passes.RemoveAt(oldIndex);
        _passes.Insert((int)index, pass);
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        _passes.Clear();
        var trimmed = text.AsSpan().TrimStart();
        var loaded = trimmed.Length > 0 && trimmed[0] switch
        {
            '<' => LoadXml(text),
            '{' => LoadJson(text),
            _ => false
        };
        if (!loaded)
            return false;

        MemoryUse = (uint)(_passes.Count * 256);
        return true;
    }

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var xml = new XMLFile(Name);
        var root = xml.CreateRoot("renderpath");
        foreach (var pass in _passes)
        {
            var passElement = root.CreateChild("pass");
            passElement.SetAttribute("name", pass.GetPassName());
            passElement.SetBool("enabled", pass.IsEnabledByDefault());
            if (!string.IsNullOrWhiteSpace(pass.GetComment()))
                passElement.SetAttribute("comment", pass.GetComment());

            if (pass is not SerializedRenderPass serializedPass)
                continue;

            foreach (var parameter in serializedPass.Parameters)
            {
                var type = VariantTypeUtility.GetVariantType(parameter.Value);
                if (type == VariantType.None)
                    continue;

                var parameterElement = passElement.CreateChild("parameter");
                parameterElement.SetAttribute("name", parameter.Key);
                parameterElement.SetAttribute("type", type.ToString());
                parameterElement.SetAttribute("value", VariantTypeUtility.Serialize(parameter.Value, type));
            }
        }

        return xml.Save(destination);
    }

    private bool LoadXml(string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var xml = new XMLFile($"{Name}#xml");
        if (!xml.BeginLoad(stream))
            return false;

        var root = xml.GetRoot("renderpath");
        if (root.IsNull)
            return false;

        for (var passElement = root.GetChild("pass"); passElement.NotNull; passElement = passElement.GetNext("pass"))
        {
            var passName = passElement.GetAttribute("name");
            if (string.IsNullOrWhiteSpace(passName))
                return false;

            var pass = new SerializedRenderPass(passName);
            if (passElement.HasAttribute("enabled"))
                pass.SetEnabledByDefault(passElement.GetBool("enabled"));
            if (passElement.HasAttribute("comment"))
                pass.SetComment(passElement.GetAttribute("comment"));

            for (var parameterElement = passElement.GetChild("parameter"); parameterElement.NotNull; parameterElement = parameterElement.GetNext("parameter"))
            {
                var parameterName = parameterElement.GetAttribute("name");
                if (string.IsNullOrWhiteSpace(parameterName))
                    return false;

                var parameterType = ParseVariantType(parameterElement.GetAttribute("type"));
                if (parameterType == VariantType.None)
                    return false;

                var parameterValue = parameterElement.HasAttribute("value")
                    ? parameterElement.GetAttribute("value")
                    : parameterElement.GetValue();
                pass.SetParameter(parameterName, DeserializeVariantValue(parameterValue, parameterType));
            }

            AddPass(pass);
        }

        return true;
    }

    private bool LoadJson(string text)
    {
        using var document = JsonDocument.Parse(text);
        if (!document.RootElement.TryGetProperty("passes", out var passesElement) || passesElement.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var passElement in passesElement.EnumerateArray())
        {
            var typeName = passElement.TryGetProperty("type", out var typeNode) && typeNode.ValueKind == JsonValueKind.String
                ? typeNode.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(typeName))
                return false;

            if (!passElement.TryGetProperty("value", out var valueElement)
                || !valueElement.TryGetProperty("attributes", out var attributesElement)
                || attributesElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var pass = RenderPathBuiltInPassFactory.Create(typeName);
            if (pass is null)
                return false;

            foreach (var attributeElement in attributesElement.EnumerateArray())
            {
                if (!attributeElement.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                    return false;
                if (!attributeElement.TryGetProperty("type", out var attributeTypeElement) || attributeTypeElement.ValueKind != JsonValueKind.String)
                    return false;
                if (!attributeElement.TryGetProperty("value", out var attributeValueElement))
                    return false;

                var attributeName = nameElement.GetString();
                var attributeTypeName = attributeTypeElement.GetString();
                if (string.IsNullOrWhiteSpace(attributeName) || string.IsNullOrWhiteSpace(attributeTypeName))
                    return false;

                var attributeValue = DeserializeAttributeValue(attributeValueElement, attributeTypeName);
                if (!pass.SetAttribute(attributeName, attributeValue))
                    return false;
            }

            AddPass(pass);
        }

        return true;
    }

    private static VariantType ParseVariantType(string value)
    {
        if (Enum.TryParse<VariantType>(value, ignoreCase: true, out var type))
            return type;

        return Variant.GetTypeFromName(value);
    }

    private static object? DeserializeAttributeValue(JsonElement valueElement, string typeName)
    {
        if (string.Equals(typeName, "StringVariantMap", StringComparison.Ordinal))
            return DeserializeStringVariantMap(valueElement);

        var variantType = ParseVariantType(typeName);
        return variantType == VariantType.None
            ? valueElement.ValueKind == JsonValueKind.String ? valueElement.GetString() : valueElement.GetRawText()
            : DeserializeVariantElement(valueElement, variantType);
    }

    private static Dictionary<string, object?> DeserializeStringVariantMap(JsonElement valueElement)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (valueElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in valueElement.EnumerateArray())
        {
            if (!item.TryGetProperty("key", out var keyElement) || keyElement.ValueKind != JsonValueKind.String)
                continue;
            if (!item.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
                continue;
            if (!item.TryGetProperty("value", out var innerValueElement))
                continue;

            var key = keyElement.GetString();
            var typeName = typeElement.GetString();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(typeName))
                continue;

            var variantType = ParseVariantType(typeName);
            if (variantType == VariantType.None)
                continue;

            result[key] = DeserializeVariantElement(innerValueElement, variantType);
        }

        return result;
    }

    private static object? DeserializeVariantElement(JsonElement valueElement, VariantType type)
    {
        return valueElement.ValueKind == JsonValueKind.String
            ? DeserializeVariantValue(valueElement.GetString() ?? string.Empty, type)
            : DeserializeVariantValue(valueElement.GetRawText(), type);
    }

    private static object? DeserializeVariantValue(string value, VariantType type)
    {
        if (type == VariantType.String)
        {
            try
            {
                return VariantTypeUtility.Deserialize(value.Trim(), type) ?? string.Empty;
            }
            catch (JsonException)
            {
                return value;
            }
            catch (NotSupportedException)
            {
                return value;
            }
        }

        var trimmed = value.Trim();
        try
        {
            var deserialized = VariantTypeUtility.Deserialize(trimmed, type);
            if (deserialized is not null || type is VariantType.Bool or VariantType.Int or VariantType.Float)
                return deserialized;
        }
        catch (JsonException)
        {
        }
        catch (NotSupportedException)
        {
        }

        return type switch
        {
            VariantType.Bool => bool.TryParse(trimmed, out var boolValue) ? boolValue : false,
            VariantType.Int => int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue) ? intValue : 0,
            VariantType.Float => float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue) ? floatValue : 0.0f,
            VariantType.Vector2 => ParseVector2(trimmed),
            VariantType.Vector3 => ParseVector3(trimmed),
            VariantType.Vector4 => ParseVector4(trimmed),
            VariantType.Quaternion => ParseQuaternion(trimmed),
            VariantType.Color => ParseColor(trimmed),
            VariantType.IntRect => ParseIntRect(trimmed),
            VariantType.Vector2i => ParseIntVector2(trimmed),
            VariantType.Vector3i => ParseIntVector3(trimmed),
            _ => null
        };
    }

    private static float[] ParseFloatComponents(string value, int expectedCount)
    {
        var parts = value.Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != expectedCount)
            return Enumerable.Repeat(0.0f, expectedCount).ToArray();

        var result = new float[expectedCount];
        for (var i = 0; i < expectedCount; ++i)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]))
                return new float[expectedCount];
        }

        return result;
    }

    private static int[] ParseIntComponents(string value, int expectedCount)
    {
        var parts = value.Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != expectedCount)
            return new int[expectedCount];

        var result = new int[expectedCount];
        for (var i = 0; i < expectedCount; ++i)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out result[i]))
                return new int[expectedCount];
        }

        return result;
    }

    private static Vector2f ParseVector2(string value)
    {
        var components = ParseFloatComponents(value, 2);
        return new Vector2f(components[0], components[1]);
    }

    private static Vector3f ParseVector3(string value)
    {
        var components = ParseFloatComponents(value, 3);
        return new Vector3f(components[0], components[1], components[2]);
    }

    private static Vector4f ParseVector4(string value)
    {
        var components = ParseFloatComponents(value, 4);
        return new Vector4f(components[0], components[1], components[2], components[3]);
    }

    private static Quaternion ParseQuaternion(string value)
    {
        var components = ParseFloatComponents(value, 4);
        return new Quaternion(components[0], components[1], components[2], components[3]);
    }

    private static Color ParseColor(string value)
    {
        var components = ParseFloatComponents(value, 4);
        return new Color(components[0], components[1], components[2], components[3]);
    }

    private static IntRect ParseIntRect(string value)
    {
        var components = ParseIntComponents(value, 4);
        return new IntRect(components[0], components[1], components[2], components[3]);
    }

    private static Vector2i ParseIntVector2(string value)
    {
        var components = ParseIntComponents(value, 2);
        return new Vector2i(components[0], components[1]);
    }

    private static Vector3i ParseIntVector3(string value)
    {
        var components = ParseIntComponents(value, 3);
        return new Vector3i(components[0], components[1], components[2]);
    }

    private sealed class SerializedRenderPass : RenderPass
    {
        private readonly Dictionary<string, object?> _attributes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object?> _parameters = new(StringComparer.Ordinal);

        public SerializedRenderPass(string passName)
            : base(passName)
        {
        }

        public IReadOnlyDictionary<string, object?> Parameters => _parameters;

        public void SetParameter(string name, object? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            _parameters[name] = value;
        }

        public bool ApplyAttribute(string name, object? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (string.Equals(name, "Parameters", StringComparison.Ordinal))
            {
                if (value is Dictionary<string, object?> parameterValues)
                {
                    _parameters.Clear();
                    foreach (var (parameterName, parameterValue) in parameterValues)
                        _parameters[parameterName] = parameterValue;
                    return true;
                }

                return false;
            }

            if (SetAttribute(name, value))
                return true;

            _attributes[name] = value;
            return true;
        }

        public override void CollectParameters(IDictionary<string, object?> target)
        {
            ArgumentNullException.ThrowIfNull(target);

            foreach (var parameter in _parameters)
            {
                var parameterName = GetExternalParameterName(parameter.Key);
                DeclareParameter(parameterName, parameter.Value, target);
            }
        }

        public override void UpdateParameters(RenderPipelineSettings settings, IReadOnlyDictionary<string, object?> parameters)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(parameters);

            var needReadWriteColorBuffer = _attributes.TryGetValue("Read+Write Color Output", out var readWriteValue)
                && readWriteValue is bool enableReadWrite
                && enableReadWrite;
            var needBilinearColorSampler = _attributes.TryGetValue("Bilinear Color Sampler", out var bilinearValue)
                && bilinearValue is bool enableBilinear
                && enableBilinear;
            var disableOnDefaultParameters = _attributes.TryGetValue("Disable On Default Parameters", out var disableValue)
                && disableValue is bool disableOnDefaults
                && disableOnDefaults;

            var shouldBeDisabled = disableOnDefaultParameters;
            foreach (var (parameterName, defaultValue) in this._parameters)
            {
                var externalName = GetExternalParameterName(parameterName);
                if (parameters.TryGetValue(externalName, out var value))
                {
                    if (shouldBeDisabled && !Equals(value, defaultValue))
                        shouldBeDisabled = false;
                }
            }

            SetTraits(new RenderPassTraits(needReadWriteColorBuffer, needBilinearColorSampler));
            SetInternallyEnabled(!shouldBeDisabled);
        }

        public override RenderPass Clone()
        {
            var clone = new SerializedRenderPass(GetPassName());
            clone.SetEnabledByDefault(IsEnabledByDefault());
            clone.SetComment(GetComment());
            foreach (var attribute in _attributes)
                clone._attributes[attribute.Key] = attribute.Value;
            foreach (var parameter in _parameters)
                clone.SetParameter(parameter.Key, parameter.Value);
            return clone;
        }

        private string GetExternalParameterName(string parameterName)
        {
            var prefix = _attributes.TryGetValue("Parameters Prefix", out var prefixValue)
                ? prefixValue as string
                : null;
            return string.IsNullOrEmpty(prefix) ? parameterName : prefix + parameterName;
        }
    }
}
