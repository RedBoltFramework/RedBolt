using System.Globalization;
using System.Text;
using System.Text.Json;

namespace RedBolt;

public sealed class RenderPath : Resource
{
    private readonly List<RenderPass> passes = [];
    private RenderPassTraits traits;

    public RenderPath(string name)
        : base(name)
    {
    }

    public IReadOnlyList<RenderPass> GetPasses() => passes;

    public RenderPassTraits GetAggregatedPassTraits() => traits;

    public void CollectParameters(IDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        foreach (var pass in passes)
            pass.CollectParameters(parameters);
    }

    public void InitializeView(RenderPipelineView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        foreach (var pass in passes)
            pass.InitializeView(view);
    }

    public void UpdateParameters(RenderPipelineSettings settings, IReadOnlyList<RenderPassToggle> enabledPasses, IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(enabledPasses);
        ArgumentNullException.ThrowIfNull(parameters);

        if (enabledPasses.Count != passes.Count)
            throw new ArgumentException("Enabled passes count must match render path pass count.", nameof(enabledPasses));

        for (var i = 0; i < passes.Count; ++i)
        {
            var pass = passes[i];
            pass.SetEnabled(enabledPasses[i].Enabled);
            pass.UpdateParameters(settings, parameters);
        }

        traits = default;
        foreach (var pass in passes)
        {
            var passTraits = pass.GetTraits();
            traits = new RenderPassTraits(
                traits.NeedReadWriteColorBuffer || passTraits.NeedReadWriteColorBuffer,
                traits.NeedBilinearColorSampler || passTraits.NeedBilinearColorSampler);
        }
    }

    public void Update(SharedRenderPassState sharedState)
    {
        ArgumentNullException.ThrowIfNull(sharedState);

        foreach (var pass in passes)
        {
            if (pass.IsEnabledEffectively())
                pass.Update(sharedState);
        }
    }

    public void Render(SharedRenderPassState sharedState)
    {
        ArgumentNullException.ThrowIfNull(sharedState);

        foreach (var pass in passes)
        {
            if (pass.IsEnabledEffectively())
                pass.Render(sharedState);
        }
    }

    public RenderPath Clone()
    {
        var clone = new RenderPath(Name);
        foreach (var pass in passes)
            clone.AddPass(pass.Clone());
        return clone;
    }

    public IReadOnlyList<RenderPassToggle> RepairEnabledRenderPasses(IEnumerable<RenderPassToggle> sourcePasses)
    {
        ArgumentNullException.ThrowIfNull(sourcePasses);
        var sourceList = sourcePasses.ToArray();

        if (sourceList.Length == passes.Count)
        {
            var matches = true;
            for (var i = 0; i < passes.Count; ++i)
            {
                if (!string.Equals(passes[i].GetPassName(), sourceList[i].Name, StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return sourceList;
        }

        var result = new List<RenderPassToggle>(passes.Count);
        foreach (var pass in passes)
        {
            var match = sourceList.FirstOrDefault(staticToggle => string.Equals(staticToggle.Name, pass.GetPassName(), StringComparison.Ordinal));
            var enabled = match.Name is not null ? match.Enabled : pass.IsEnabledByDefault();
            result.Add(new RenderPassToggle(pass.GetPassName(), enabled));
        }
        return result;
    }

    public void AddPass(RenderPass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        passes.Add(pass);
    }

    public bool RemovePass(RenderPass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        return passes.Remove(pass);
    }

    public bool ReorderPass(RenderPass pass, uint index)
    {
        ArgumentNullException.ThrowIfNull(pass);

        if (index >= passes.Count)
            return false;

        var oldIndex = passes.IndexOf(pass);
        if (oldIndex < 0)
            return false;

        passes.RemoveAt(oldIndex);
        passes.Insert((int)index, pass);
        return true;
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        passes.Clear();
        var trimmed = text.AsSpan().TrimStart();
        var loaded = trimmed.Length > 0 && trimmed[0] switch
        {
            '<' => LoadXml(text),
            '{' => LoadJson(text),
            _ => false
        };
        if (!loaded)
            return false;

        MemoryUse = (uint)(passes.Count * 256);
        return true;
    }

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var xml = new XMLFile(Name);
        var root = xml.CreateRoot("renderpath");
        foreach (var pass in passes)
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
        return Enum.TryParse<VariantType>(value, ignoreCase: true, out var type) ? type : VariantType.None;
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
            VariantType.IntVector2 => ParseIntVector2(trimmed),
            VariantType.IntVector3 => ParseIntVector3(trimmed),
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

    private static Vector2 ParseVector2(string value)
    {
        var components = ParseFloatComponents(value, 2);
        return new Vector2(components[0], components[1]);
    }

    private static Vector3 ParseVector3(string value)
    {
        var components = ParseFloatComponents(value, 3);
        return new Vector3(components[0], components[1], components[2]);
    }

    private static Vector4 ParseVector4(string value)
    {
        var components = ParseFloatComponents(value, 4);
        return new Vector4(components[0], components[1], components[2], components[3]);
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

    private static IntVector2 ParseIntVector2(string value)
    {
        var components = ParseIntComponents(value, 2);
        return new IntVector2(components[0], components[1]);
    }

    private static IntVector3 ParseIntVector3(string value)
    {
        var components = ParseIntComponents(value, 3);
        return new IntVector3(components[0], components[1], components[2]);
    }

    private sealed class SerializedRenderPass : RenderPass
    {
        private readonly Dictionary<string, object?> attributes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object?> parameters = new(StringComparer.Ordinal);

        public SerializedRenderPass(string passName)
            : base(passName)
        {
        }

        public IReadOnlyDictionary<string, object?> Parameters => parameters;

        public void SetParameter(string name, object? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            parameters[name] = value;
        }

        public bool ApplyAttribute(string name, object? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (string.Equals(name, "Parameters", StringComparison.Ordinal))
            {
                if (value is Dictionary<string, object?> parameterValues)
                {
                    parameters.Clear();
                    foreach (var (parameterName, parameterValue) in parameterValues)
                        parameters[parameterName] = parameterValue;
                    return true;
                }

                return false;
            }

            if (SetAttribute(name, value))
                return true;

            attributes[name] = value;
            return true;
        }

        public override void CollectParameters(IDictionary<string, object?> target)
        {
            ArgumentNullException.ThrowIfNull(target);

            foreach (var parameter in parameters)
            {
                var parameterName = GetExternalParameterName(parameter.Key);
                DeclareParameter(parameterName, parameter.Value, target);
            }
        }

        public override void UpdateParameters(RenderPipelineSettings settings, IReadOnlyDictionary<string, object?> parameters)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(parameters);

            var needReadWriteColorBuffer = attributes.TryGetValue("Read+Write Color Output", out var readWriteValue)
                && readWriteValue is bool enableReadWrite
                && enableReadWrite;
            var needBilinearColorSampler = attributes.TryGetValue("Bilinear Color Sampler", out var bilinearValue)
                && bilinearValue is bool enableBilinear
                && enableBilinear;
            var disableOnDefaultParameters = attributes.TryGetValue("Disable On Default Parameters", out var disableValue)
                && disableValue is bool disableOnDefaults
                && disableOnDefaults;

            var shouldBeDisabled = disableOnDefaultParameters;
            foreach (var (parameterName, defaultValue) in this.parameters)
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
            foreach (var attribute in attributes)
                clone.attributes[attribute.Key] = attribute.Value;
            foreach (var parameter in parameters)
                clone.SetParameter(parameter.Key, parameter.Value);
            return clone;
        }

        private string GetExternalParameterName(string parameterName)
        {
            var prefix = attributes.TryGetValue("Parameters Prefix", out var prefixValue)
                ? prefixValue as string
                : null;
            return string.IsNullOrEmpty(prefix) ? parameterName : prefix + parameterName;
        }
    }
}
