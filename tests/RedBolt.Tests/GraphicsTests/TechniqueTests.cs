using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class TechniqueTests
{
    [Fact]
    public void Pass_matches_native_class_shape_and_exact_define_semantics()
    {
        Assert.False(typeof(Pass).IsSealed);
        Assert.False(typeof(Pass).IsValueType);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(Pass)));
        Assert.False(typeof(Technique).IsSealed);
        Assert.False(typeof(Technique).IsValueType);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(Technique)));
        Assert.Equal(typeof(int), typeof(Pass).GetProperty(nameof(Pass.Index))!.PropertyType);
        Assert.Equal(typeof(int), typeof(Technique).GetMethod(nameof(Technique.GetPassIndex))!.ReturnType);
        Assert.Equal(typeof(int), typeof(Technique).GetMethod(nameof(Technique.GetNumPasses))!.ReturnType);
        Assert.Equal(int.MaxValue, Technique.GetPassIndex(string.Empty));

        using var technique = new Technique { Name = "Techniques/Smoke" };
        var pass = technique.CreatePass("base");
        pass.SetVertexShader("LitSolid");
        pass.SetPixelShader("LitSolid");
        pass.SetBlendMode(BlendMode.Alpha);
        pass.SetDepthTestMode(CompareMode.GreaterEqual);
        pass.SetPixelShaderDefines("  XALPHAMASKY  alphamask ");
        pass.SetVertexShaderDefines("A A B");
        pass.SetVertexShaderDefineExcludes("A");
        pass.SetVertexTextureDefines([" First ", string.Empty, "Second"]);

        Assert.True(pass.IsAlphaMask);
        Assert.NotEqual(0u, pass.GetPipelineStateHash());
        Assert.Equal("  XALPHAMASKY  alphamask ", pass.GetEffectivePixelShaderDefines());
        Assert.Equal("A B", pass.GetEffectiveVertexShaderDefines());
        Assert.Equal([" First ", string.Empty, "Second"], pass.VertexTextureDefines);

        pass.SetPixelShaderDefines("alphamask");
        Assert.False(pass.IsAlphaMask);

        Assert.Same(pass.GetVertexShaders(), pass.GetVertexShaders(StringHash.Empty));
        Assert.Same(pass.GetPixelShaders(), pass.GetPixelShaders(StringHash.Empty));

        var previousHash = pass.GetPipelineStateHash();
        pass.SetDepthWrite(false);
        Assert.NotEqual(previousHash, pass.GetPipelineStateHash());
    }

    [Fact]
    public void Clone_with_defines_works()
    {
        using var technique = new Technique { Name = "Techniques/Smoke" };
        var sourcePass = technique.CreatePass("base");
        sourcePass.SetVertexShaderDefines("ORIGINAL");
        sourcePass.MarkShadersLoaded(42);

        using var unnamedClone = technique.Clone();
        Assert.Equal(string.Empty, unnamedClone.Name);
        Assert.Equal(0u, unnamedClone.GetPass("base")?.ShadersLoadedFrameNumber);
        Assert.NotSame(sourcePass, unnamedClone.GetPass("base"));

        var clone = technique.CloneWithDefines("SKINNED", "FOG");

        Assert.NotSame(clone, technique);
        Assert.Equal("ORIGINAL SKINNED", clone.GetPass("base")?.VertexShaderDefines);
        Assert.Equal(" FOG", clone.GetPass("base")?.PixelShaderDefines);
        Assert.Same(clone, technique.CloneWithDefines("SKINNED", "FOG"));

        var whitespaceClone = technique.CloneWithDefines(" ", string.Empty);
        Assert.NotSame(technique, whitespaceClone);
        Assert.Equal("ORIGINAL  ", whitespaceClone.GetPass("base")?.VertexShaderDefines);
    }

    [Fact]
    public void Unknown_pass_lookup_does_not_allocate_an_index()
    {
        var passIndices = typeof(Technique).GetField("PassIndices", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var dictionary = (Dictionary<string, int>)passIndices.GetValue(null)!;
        var count = dictionary.Count;
        var unknownName = $"unknown-{Guid.NewGuid():N}";

        using var technique = new Technique { Name = "Techniques/Lookup" };
        Assert.Null(technique.GetPass(unknownName));
        Assert.False(technique.HasPass(unknownName));
        technique.RemovePass(unknownName);
        Assert.Equal(count, dictionary.Count);
    }

    [Fact]
    public void Xml_loading_continues_after_unnamed_pass_and_sets_texture_defaults()
    {
        using var xml = new MemoryBuffer(System.Text.Encoding.UTF8.GetBytes(
            "<technique vs=\"VS\" ps=\"PS\" vsdefines=\"GLOBALVS\" psdefines=\"GLOBALPS\">" +
            "<pass />" +
            "<pass name=\"shadow\" />" +
            "<pass name=\"base\" />" +
            "<pass name=\"alpha\" blend=\"addalpha\" cull=\"cw\" depthtest=\"greaterequal\" " +
            "colorwrite=\"false\" depthwrite=\"false\" alphatocoverage=\"true\" psdefines=\"ALPHAMASK\" />" +
            "</technique>"));
        using var technique = new Technique { Name = "Techniques/Xml" };
        Assert.True(technique.Load(xml));

        Assert.Empty(technique.GetPass("shadow")!.VertexTextureDefines);
        Assert.Empty(technique.GetPass("shadow")!.PixelTextureDefines);
        Assert.Equal(["Albedo", "Normal", "Properties", "Emission"], technique.GetPass("base")!.PixelTextureDefines);

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

    [Fact]
    public void Dispose_releases_owned_pass_state()
    {
        var technique = new Technique { Name = "Techniques/Dispose" };
        var pass = technique.CreatePass("base");
        var shaders = pass.GetVertexShaders();
        shaders.Add(null);

        technique.Dispose();

        Assert.Empty(shaders);
        Assert.Empty(technique.GetPasses());
    }
}
