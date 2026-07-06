using System.Reflection;

namespace RedBolt.Tests;

public sealed class GraphicsCustomUniformTests
{
    [Fact]
    public void ParseCustomUniformLayout_merges_stage_blocks_without_duplicate_fields()
    {
        var vertexSource = """
            UNIFORM_BUFFER_BEGIN(6, Custom)
                UNIFORM(vec4 cOutlineColor)
            UNIFORM_BUFFER_END(6, Custom)
            """;
        var pixelSource = """
            UNIFORM_BUFFER_BEGIN(6, Custom)
                UNIFORM(vec4 cOutlineColor)
                UNIFORM(float cOutlineThickness)
            UNIFORM_BUFFER_END(6, Custom)
            """;

        var parseMethod = typeof(Graphics).GetMethod("ParseCustomUniformLayout", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Graphics.ParseCustomUniformLayout was not found.");
        var layout = parseMethod.Invoke(null, [new[] { vertexSource, pixelSource }])
            ?? throw new InvalidOperationException("Custom uniform layout was null.");

        Assert.Equal<uint>(32u, (uint)(GetProperty(layout, "Size") ?? 0u));

        var fields = ((System.Collections.IEnumerable)GetProperty(layout, "Fields")!).Cast<object>().ToArray();
        Assert.Equal(2, fields.Length);
        Assert.Equal("cOutlineColor", GetProperty(fields[0], "UniformName"));
        Assert.Equal("OutlineColor", GetProperty(fields[0], "ParameterName"));
        Assert.Equal(0, GetProperty(fields[0], "Offset"));
        Assert.Equal("cOutlineThickness", GetProperty(fields[1], "UniformName"));
        Assert.Equal("OutlineThickness", GetProperty(fields[1], "ParameterName"));
        Assert.Equal(16, GetProperty(fields[1], "Offset"));
    }

    [Fact]
    public void WriteCustomParameterValue_packs_rbfx_std140_values()
    {
        var source = """
            UNIFORM_BUFFER_BEGIN(6, Custom)
                UNIFORM(vec4 cOutlineColor)
                UNIFORM(float cOutlineThickness)
            UNIFORM_BUFFER_END(6, Custom)
            """;
        var layout = ParseLayout(source);
        var fields = ((System.Collections.IEnumerable)GetProperty(layout, "Fields")!).Cast<object>().ToArray();
        var writeMethod = typeof(Graphics).GetMethod("WriteCustomParameterValue", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Graphics.WriteCustomParameterValue was not found.");
        var buffer = new byte[(int)(uint)(GetProperty(layout, "Size") ?? 0u)];

        writeMethod.Invoke(null, [buffer, fields[0], new Vector4(0.1f, 0.2f, 0.3f, 0.4f)]);
        writeMethod.Invoke(null, [buffer, fields[1], 3.5f]);

        Assert.Equal(0.1f, BitConverter.ToSingle(buffer, 0));
        Assert.Equal(0.2f, BitConverter.ToSingle(buffer, 4));
        Assert.Equal(0.3f, BitConverter.ToSingle(buffer, 8));
        Assert.Equal(0.4f, BitConverter.ToSingle(buffer, 12));
        Assert.Equal(3.5f, BitConverter.ToSingle(buffer, 16));
    }

    [Fact]
    public void TryGetCustomParameterValue_reads_material_custom_parameters()
    {
        var field = ((System.Collections.IEnumerable)GetProperty(ParseLayout("""
            UNIFORM_BUFFER_BEGIN(6, Custom)
                UNIFORM(vec4 cOutlineColor)
            UNIFORM_BUFFER_END(6, Custom)
            """), "Fields")!).Cast<object>().Single();
        var lookupMethod = typeof(Graphics).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method =>
            {
                if (method.Name != "TryGetCustomParameterValue")
                    return false;

                var parameters = method.GetParameters();
                return parameters.Length == 3
                    && parameters[0].ParameterType.Name.Contains("CustomUniformField", StringComparison.Ordinal)
                    && parameters[1].ParameterType.GenericTypeArguments.Any(type => type == typeof(MaterialShaderParameter));
            });
        var parameters = new Dictionary<string, MaterialShaderParameter>(StringComparer.Ordinal)
        {
            ["OutlineColor"] = new(new Vector4(1.0f, 0.5f, 0.25f, 1.0f), true)
        };
        object?[] arguments = [field, parameters, null];

        var found = (bool)(lookupMethod.Invoke(null, arguments) ?? false);

        Assert.True(found);
        Assert.Equal(new Vector4(1.0f, 0.5f, 0.25f, 1.0f), Assert.IsType<Vector4>(arguments[2]));
    }

    [Fact]
    public void TryGetCustomParameterValue_ignores_non_custom_material_parameters()
    {
        var field = ((System.Collections.IEnumerable)GetProperty(ParseLayout("""
            UNIFORM_BUFFER_BEGIN(6, Custom)
                UNIFORM(vec4 cOutlineColor)
            UNIFORM_BUFFER_END(6, Custom)
            """), "Fields")!).Cast<object>().Single();
        var lookupMethod = typeof(Graphics).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method =>
            {
                if (method.Name != "TryGetCustomParameterValue")
                    return false;

                var parameters = method.GetParameters();
                return parameters.Length == 3
                    && parameters[0].ParameterType.Name.Contains("CustomUniformField", StringComparison.Ordinal)
                    && parameters[1].ParameterType.GenericTypeArguments.Any(type => type == typeof(MaterialShaderParameter));
            });
        var parameters = new Dictionary<string, MaterialShaderParameter>(StringComparer.Ordinal)
        {
            ["OutlineColor"] = new(new Vector4(1.0f, 0.5f, 0.25f, 1.0f), false)
        };
        object?[] arguments = [field, parameters, null];

        var found = (bool)(lookupMethod.Invoke(null, arguments) ?? false);

        Assert.False(found);
        Assert.Null(arguments[2]);
    }

    private static object ParseLayout(string source)
    {
        var parseMethod = typeof(Graphics).GetMethod("ParseCustomUniformLayout", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Graphics.ParseCustomUniformLayout was not found.");
        return parseMethod.Invoke(null, [new[] { source }])
            ?? throw new InvalidOperationException("Custom uniform layout was null.");
    }

    private static object? GetProperty(object instance, string name)
    {
        return instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(instance);
    }
}
