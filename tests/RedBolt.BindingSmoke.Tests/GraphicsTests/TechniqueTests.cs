using RedBolt;

namespace RedBolt.BindingSmoke.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class TechniqueTests
{
    [Fact]
    public void Pass_helpers_work()
    {
        var technique = new Technique("Techniques/Smoke");
        var pass = technique.CreatePass("base");
        pass.SetVertexShader("LitSolid");
        pass.SetPixelShader("LitSolid");
        pass.SetBlendMode(BlendMode.Alpha);
        pass.SetDepthTestMode(CompareMode.GreaterEqual);
        pass.SetPixelShaderDefines("ALPHAMASK DIFFMAP");

        Assert.True(pass.IsAlphaMask);
        Assert.NotEqual(0u, pass.GetPipelineStateHash());
        Assert.Equal("ALPHAMASK DIFFMAP", pass.GetEffectivePixelShaderDefines());
    }

    [Fact]
    public void Clone_with_defines_works()
    {
        var technique = new Technique("Techniques/Smoke");
        technique.CreatePass("base").SetVertexShaderDefines("ORIGINAL");
        var clone = technique.CloneWithDefines("SKINNED", "FOG");

        Assert.NotSame(clone, technique);
        Assert.Contains("SKINNED", clone.GetPass("base")?.VertexShaderDefines ?? "", StringComparison.Ordinal);
        Assert.Contains("FOG", clone.GetPass("base")?.PixelShaderDefines ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void Xml_loading_works()
    {
        using var xml = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            "<technique vs=\"VS\" ps=\"PS\" vsdefines=\"GLOBALVS\" psdefines=\"GLOBALPS\">" +
            "<pass name=\"alpha\" blend=\"addalpha\" cull=\"cw\" depthtest=\"greaterequal\" " +
            "colorwrite=\"false\" depthwrite=\"false\" alphatocoverage=\"true\" psdefines=\"ALPHAMASK\" />" +
            "</technique>"));
        var technique = new Technique("Techniques/Xml");
        Assert.True(technique.Load(xml));

        var pass = technique.GetPass("alpha");
        Assert.NotNull(pass);
        Assert.Equal(BlendMode.AddAlpha, pass.BlendMode);
        Assert.Equal(CullMode.Clockwise, pass.CullMode);
        Assert.Equal(CompareMode.GreaterEqual, pass.DepthTestMode);
        Assert.False(pass.ColorWrite);
        Assert.False(pass.DepthWrite);
        Assert.True(pass.AlphaToCoverage);
        Assert.True(pass.IsAlphaMask);
    }
}
