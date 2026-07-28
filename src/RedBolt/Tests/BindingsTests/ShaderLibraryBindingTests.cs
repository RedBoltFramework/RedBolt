namespace RedBolt.Tests.BindingsTests;

using RedBolt.Bindings.Glslang;
using RedBolt.Bindings.SpirVCross;
using RedBolt.Bindings.SpirVTools;

public sealed class ShaderLibraryBindingTests
{
    [Fact]
    public void Glslang_process_can_be_initialized_and_finalized()
    {
        Assert.NotEqual(0, GlslangNativeBindings.GlslangInitializeProcess());
        GlslangNativeBindings.GlslangFinalizeProcess();
    }

    [Fact]
    public void Spirv_cross_context_can_be_created_and_destroyed()
    {
        var result = SpirVCrossNativeBindings.SpvcContextCreate(out var context);

        Assert.Equal(SpvcResult.SpvcSuccess, result);
        Assert.NotNull(context);
        Assert.NotEqual(0, context.Handle);

        SpirVCrossNativeBindings.SpvcContextDestroy(context);
    }

    [Fact]
    public void Spirv_tools_context_can_be_created_and_destroyed()
    {
        var context = SpirVToolsNativeBindings.SpvContextCreate(SpvTargetEnv.SpvEnvUniversal10);

        Assert.NotNull(context);
        Assert.NotEqual(0, context.Handle);

        SpirVToolsNativeBindings.SpvContextDestroy(context);
    }
}
