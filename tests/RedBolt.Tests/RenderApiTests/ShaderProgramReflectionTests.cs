using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ShaderProgramReflectionTests
{
    private readonly HeadlessEngineFixture fixture;

    public ShaderProgramReflectionTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Shader_program_reflection_discovers_native_shader_resources_from_compiled_shader_variations()
    {
        WithInitializedGraphics(() =>
        {
            var vertexShader = fixture.Graphics.GetShader(ShaderType.VS, "X_CopyTexture");
            var pixelShader = fixture.Graphics.GetShader(ShaderType.PS, "X_CopyTexture");

            Assert.NotNull(vertexShader);
            Assert.NotNull(pixelShader);

            var reflection = new ShaderProgramReflection([vertexShader!, pixelShader!]);
            var albedo = reflection.GetShaderResource(new StringHash("Albedo"));

            Assert.NotNull(albedo);
            Assert.Equal("sAlbedo", albedo!.InternalName);
        });
    }

    [Fact]
    public void Shader_program_reflection_discovers_native_uniform_buffers_from_compiled_shader_variations()
    {
        WithInitializedGraphics(() =>
        {
            var vertexShader = fixture.Graphics.GetShader(ShaderType.VS, "P_Outline", "URHO3D_GEOMETRY_STATIC");
            var pixelShader = fixture.Graphics.GetShader(ShaderType.PS, "P_Outline", "URHO3D_GEOMETRY_STATIC");

            Assert.NotNull(vertexShader);
            Assert.NotNull(pixelShader);

            var reflection = new ShaderProgramReflection([vertexShader!, pixelShader!]);
            var frameBuffer = reflection.GetUniformBuffer(ShaderParameterGroup.Frame);
            var cameraBuffer = reflection.GetUniformBuffer(ShaderParameterGroup.Camera);
            var customBuffer = reflection.GetUniformBuffer(ShaderParameterGroup.Custom);

            Assert.NotNull(frameBuffer);
            Assert.Equal("Frame", frameBuffer!.InternalName);
            Assert.True(frameBuffer.Size > 0u);
            Assert.True(frameBuffer.Hash != 0u);

            Assert.NotNull(cameraBuffer);
            Assert.Equal("Camera", cameraBuffer!.InternalName);
            Assert.True(cameraBuffer.Size > 0u);

            Assert.NotNull(customBuffer);
            Assert.Equal("Custom", customBuffer!.InternalName);
            Assert.True(customBuffer.Size > 0u);
        });
    }

    [Fact]
    public void Pipeline_state_exposes_native_shader_program_reflection_for_compiled_shaders()
    {
        WithInitializedGraphics(() =>
        {
            var graphicsDesc = new GraphicsPipelineStateDesc
            {
                DebugName = "ReflectionPipeline",
                VertexShader = fixture.Graphics.GetShader(ShaderType.VS, "P_Outline", "URHO3D_GEOMETRY_STATIC"),
                PixelShader = fixture.Graphics.GetShader(ShaderType.PS, "P_Outline", "URHO3D_GEOMETRY_STATIC")
            };
            graphicsDesc.Output.SetRenderTargetFormats(TextureFormat.RGBA8);

            var pipelineState = fixture.PipelineStateCache.GetGraphicsPipelineState(graphicsDesc);

            Assert.NotNull(pipelineState);
            Assert.NotNull(pipelineState!.GetReflection().GetUniformBuffer(ShaderParameterGroup.Camera));
            Assert.NotNull(pipelineState.GetReflection().GetShaderResource(new StringHash("Albedo")));
        });
    }

    private void WithInitializedGraphics(Action action)
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.ShaderProgramReflectionTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };

        RbfxTestData.AddResourceDirs(fixture.Resources);
        fixture.Input.Initialize(parameters);
        fixture.Graphics.Initialize(parameters, fixture.Input);
        try
        {
            action();
        }
        finally
        {
            fixture.Graphics.Shutdown();
            fixture.Input.Shutdown();
            RbfxTestData.RemoveResourceDirs(fixture.Resources);
        }
    }
}
