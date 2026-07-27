using RedBolt.Graphics;
using RedBolt.RenderAPI;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;

namespace RedBolt.Tests.GraphicsTests;

public sealed class ShaderCompilerTests
{
    [Fact]
    public void Hlsl_compilation_matches_platform_support()
    {
        const string sourceCode = "float4 main() : SV_Target { return float4(1, 0, 0, 1); }";

        var result = ShaderCompiler.CompileHLSLToBinary(
            out var bytecode, out var compilerOutput, sourceCode, ShaderType.PS);

        if (OperatingSystem.IsWindows())
        {
            Assert.True(result, compilerOutput);
            Assert.NotEmpty(bytecode);
        }
        else
        {
            Assert.False(result);
            Assert.Empty(bytecode);
            Assert.Empty(compilerOutput);
        }
    }

    [Fact]
    public void Unsupported_hlsl_compiler_clears_outputs_for_every_shader_type()
    {
        if (OperatingSystem.IsWindows())
            return;

        foreach (var shaderType in Enum.GetValues<ShaderType>())
        {
            var result = ShaderCompiler.CompileHLSLToBinary(
                out var bytecode, out var compilerOutput, "invalid", shaderType);

            Assert.False(result);
            Assert.Empty(bytecode);
            Assert.Empty(compilerOutput);
        }
    }
}
