using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class MaterialFileTests
{
    private readonly HeadlessEngineFixture _fixture;

    public MaterialFileTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Repo_backed_rbfx_materials_load_techniques_and_parameters()
    {
        RbfxTestData.AddResourceDirs(Resources);
        try
        {
            var material = Resources.GetResource<Material>("Materials/StoneTiled.xml");
            var mushroom = Resources.GetResource<Material>("Materials/Mushroom.xml");

            Assert.Equal(2u, material.GetNumTechniques());
            Assert.Equal(MaterialQuality.Medium, material.GetTechniqueEntry(0).QualityLevel);
            Assert.Equal("LitSolid", material.GetPass(0, "base")?.VertexShader);
            Assert.Equal(BlendMode.Add, material.GetPass(0, "light")?.BlendMode);

            var specColor = Assert.IsType<Vector4f>(material.GetShaderParameter("MatSpecColor"));
            Assert.Equal(new Vector4f(0.3f, 0.3f, 0.3f, 16.0f), specColor);
            Assert.Contains("PACKEDNORMAL", material.GetPixelShaderDefines(), StringComparison.Ordinal);
            Assert.NotNull(material.GetTexture("Albedo"));
            Assert.NotNull(material.GetTexture("Normal"));

            Assert.Equal(1u, mushroom.GetNumTechniques());
            Assert.Equal("LitSolid", mushroom.GetPass(0, "base")?.PixelShader);
            Assert.NotNull(mushroom.GetTexture("Albedo"));
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(Resources);
        }
    }
}
