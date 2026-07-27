using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.RenderAPI;

namespace RedBolt.Tests;

public sealed class PipelineStateDescParityTests
{
    [Fact]
    public void Native_pod_descriptions_are_managed_value_types()
    {
        Assert.True(typeof(WindowSettings).IsValueType);
        Assert.True(typeof(VulkanDescriptorPoolSize).IsValueType);
        Assert.True(typeof(RenderDeviceSettingsVulkan).IsValueType);
        Assert.True(typeof(RenderDeviceSettingsD3D12).IsValueType);
        Assert.True(typeof(RenderDeviceSettings).IsValueType);
        Assert.True(typeof(RenderDeviceCaps).IsValueType);
        Assert.True(typeof(RenderDeviceStats).IsValueType);
        Assert.True(typeof(RenderPoolSettings).IsValueType);
        Assert.True(typeof(InputLayoutDesc).IsValueType);
        Assert.True(typeof(ImmutableSamplersDesc).IsValueType);
        Assert.True(typeof(PipelineStateOutputDesc).IsValueType);
        Assert.True(typeof(GraphicsPipelineStateDesc).IsValueType);
        Assert.True(typeof(ComputePipelineStateDesc).IsValueType);
        Assert.True(typeof(PipelineStateDesc).IsClass);
    }

    [Fact]
    public void Inline_descriptor_storage_has_value_copy_semantics()
    {
        var inputLayout = new InputLayoutDesc { Size = 1 };
        inputLayout.Elements[0] = new InputLayoutElementDesc { BufferIndex = 1 };
        var inputLayoutCopy = inputLayout;
        inputLayout.Elements[0] = new InputLayoutElementDesc { BufferIndex = 2 };
        Assert.Equal(1, inputLayoutCopy.Elements[0].BufferIndex);

        var output = new PipelineStateOutputDesc { NumRenderTargets = 1 };
        output.RenderTargetFormats[0] = TextureFormat.TexFormatRgba8Unorm;
        var outputCopy = output;
        output.RenderTargetFormats[0] = TextureFormat.TexFormatBgra8Unorm;
        Assert.Equal(TextureFormat.TexFormatRgba8Unorm, outputCopy.RenderTargetFormats[0]);

        var samplers = new ImmutableSamplersDesc();
        samplers.Add(new StringHash("DiffMap"), new SamplerStateDesc { Anisotropy = 2 });
        var samplersCopy = samplers;
        samplers.Descriptions[0] = new SamplerStateDesc { Anisotropy = 4 };
        Assert.Equal((byte)2, samplersCopy.Descriptions[0].Anisotropy);
    }

    [Fact]
    public void Pipeline_state_description_contains_only_its_selected_variant()
    {
        var graphics = new GraphicsPipelineStateDesc();
        var compute = new ComputePipelineStateDesc();

        var graphicsDesc = new PipelineStateDesc(graphics);
        var computeDesc = new PipelineStateDesc(compute);

        Assert.NotNull(graphicsDesc.AsGraphics());
        Assert.Null(graphicsDesc.AsCompute());
        Assert.Null(computeDesc.AsGraphics());
        Assert.NotNull(computeDesc.AsCompute());
    }

    [Fact]
    public void Default_pipeline_state_description_uses_graphics_variant()
    {
        var desc = new PipelineStateDesc();

        Assert.Equal(PipelineStateType.Graphics, desc.GetPipelineType());
        Assert.NotNull(desc.AsGraphics());
        Assert.Null(desc.AsCompute());
    }
}
