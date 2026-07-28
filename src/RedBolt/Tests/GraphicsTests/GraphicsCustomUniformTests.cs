namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.RenderAPI;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class GraphicsCustomUniformTests
{
    private readonly GraphicsEngineFixture _fixture;

    public GraphicsCustomUniformTests(GraphicsEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void CreateCustomUniformLayout_uses_reflected_outline_uniform_offsets()
    {
        WithInitializedGraphics(() =>
        {
            var layout = CreateLayout("P_Outline", "URHO3D_GEOMETRY_STATIC");
            var fields = GetFields(layout);

            Assert.Equal<uint>(16u, (uint)(GetProperty(layout, "Size") ?? 0u));
            Assert.Single(fields);
            Assert.Equal("InputInvSize", GetProperty(fields[0], "UniformName"));
            Assert.Equal("InputInvSize", GetProperty(fields[0], "ParameterName"));
            Assert.Equal(0, GetProperty(fields[0], "Offset"));
        });
    }

    [Fact]
    public void CreateCustomUniformLayout_reflects_pixel_only_custom_uniforms()
    {
        WithInitializedGraphics(() =>
        {
            var layout = CreateLayout("P_Outline", "URHO3D_GEOMETRY_STATIC", includeVertexShader: false);
            var fields = GetFields(layout);

            Assert.Equal<uint>(16u, (uint)(GetProperty(layout, "Size") ?? 0u));
            Assert.Single(fields);
            Assert.Equal("InputInvSize", GetProperty(fields[0], "UniformName"));
            Assert.Equal("InputInvSize", GetProperty(fields[0], "ParameterName"));
            Assert.Equal(0, GetProperty(fields[0], "Offset"));
        });
    }

    [Fact]
    public void WriteCustomParameterValue_packs_reflected_custom_uniform_values()
    {
        WithInitializedGraphics(() =>
        {
            var layout = CreateLayout("P_Outline", "URHO3D_GEOMETRY_STATIC");
            var fields = GetFields(layout);
            var inputInvSize = fields.Single(field => Equals(GetProperty(field, "UniformName"), "InputInvSize"));
            var inputOffset = (int)(GetProperty(inputInvSize, "Offset") ?? -1);

            var writeMethod = typeof(Graphics).GetMethod("WriteCustomParameterValue", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Graphics.WriteCustomParameterValue was not found.");
            var buffer = new byte[(int)(uint)(GetProperty(layout, "Size") ?? 0u)];

            writeMethod.Invoke(null, [buffer, inputInvSize, new Vector2f(0.25f, 0.5f)]);

            Assert.Equal(0.25f, BitConverter.ToSingle(buffer, inputOffset));
            Assert.Equal(0.5f, BitConverter.ToSingle(buffer, inputOffset + 4));
        });
    }

    [Fact]
    public void TryGetCustomParameterValue_reads_material_custom_parameters()
    {
        var field = GetFields(CreateFieldOnlyLayout("P_Outline", "URHO3D_GEOMETRY_STATIC")).Single();
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
            ["InputInvSize"] = new("InputInvSize", new Vector2f(0.5f, 0.25f), true)
        };
        object?[] arguments = [field, parameters, null];

        var found = (bool)(lookupMethod.Invoke(null, arguments) ?? false);

        Assert.True(found);
        Assert.Equal(new Vector2f(0.5f, 0.25f), Assert.IsType<Vector2f>(arguments[2]));
    }

    [Fact]
    public void TryGetCustomParameterValue_ignores_non_custom_material_parameters()
    {
        var field = GetFields(CreateFieldOnlyLayout("P_Outline", "URHO3D_GEOMETRY_STATIC")).Single();
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
            ["InputInvSize"] = new("InputInvSize", new Vector2f(0.5f, 0.25f), false)
        };
        object?[] arguments = [field, parameters, null];

        var found = (bool)(lookupMethod.Invoke(null, arguments) ?? false);

        Assert.False(found);
        Assert.Null(arguments[2]);
    }

    [Fact]
    public void StructCustomUniformWriter_packs_typed_parameters_using_shader_parameter_names()
    {
        WithInitializedGraphics(() =>
        {
            var layout = CreateLayout("P_Outline", "URHO3D_GEOMETRY_STATIC");
            var inputInvSizeOffset = GetOffset(layout, "InputInvSize");
            var writeMethod = GetStructWriterMethod(typeof(OutlineParameters));
            var buffer = new byte[(int)(uint)(GetProperty(layout, "Size") ?? 0u)];
            var parameters = new OutlineParameters
            {
                InputInvSize = new Vector2f(0.25f, 0.5f)
            };

            writeMethod.Invoke(null, [buffer, layout, parameters]);

            Assert.Equal(0.25f, BitConverter.ToSingle(buffer, inputInvSizeOffset));
            Assert.Equal(0.5f, BitConverter.ToSingle(buffer, inputInvSizeOffset + 4));
        });
    }

    private object CreateFieldOnlyLayout(string shaderName, string defines)
    {
        object? layout = null;
        WithInitializedGraphics(() => layout = CreateLayout(shaderName, defines));
        return layout ?? throw new InvalidOperationException("Custom uniform layout was null.");
    }

    private object CreateLayout(string shaderName, string defines, bool includeVertexShader = true, bool includePixelShader = true)
    {
        var vertexShader = includeVertexShader ? _fixture.Graphics.GetShader(ShaderType.VS, shaderName, defines) : null;
        var pixelShader = includePixelShader ? _fixture.Graphics.GetShader(ShaderType.PS, shaderName, defines) : null;

        if (includeVertexShader)
            Assert.NotNull(vertexShader);
        if (includePixelShader)
            Assert.NotNull(pixelShader);

        var reflection = new ShaderProgramReflection([vertexShader, pixelShader]);
        var layoutMethod = typeof(Graphics).GetMethod("CreateCustomUniformLayout", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Graphics.CreateCustomUniformLayout was not found.");
        return layoutMethod.Invoke(null, [reflection])
            ?? throw new InvalidOperationException("Custom uniform layout was null.");
    }

    private static object[] GetFields(object layout)
    {
        return ((System.Collections.IEnumerable)(GetProperty(layout, "Fields")
                ?? throw new InvalidOperationException("Custom uniform fields were not found.")))
            .Cast<object>()
            .ToArray();
    }

    private static int GetOffset(object layout, string uniformName)
    {
        var field = GetFields(layout).Single(candidate => Equals(GetProperty(candidate, "UniformName"), uniformName));
        return (int)(GetProperty(field, "Offset") ?? -1);
    }

    private static object? GetProperty(object instance, string name)
    {
        return instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(instance);
    }

    private static MethodInfo GetStructWriterMethod(Type parameterType)
    {
        var writerType = typeof(Graphics).GetNestedType("StructCustomUniformWriter`1", BindingFlags.NonPublic)
            ?.MakeGenericType(parameterType)
            ?? throw new InvalidOperationException("Graphics.StructCustomUniformWriter<T> was not found.");
        return writerType.GetMethod("Write", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Struct custom uniform writer method was not found.");
    }

    private void WithInitializedGraphics(Action action)
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.GraphicsCustomUniformTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };

        RbfxTestData.AddResourceDirs(_fixture.Resources);
        _fixture.InitializeGraphics(parameters);
        try
        {
            action();
        }
        finally
        {
            _fixture.ResetGraphics();
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }

    private readonly struct OutlineParameters
    {
        public Vector2f InputInvSize { get; init; }
    }
}
