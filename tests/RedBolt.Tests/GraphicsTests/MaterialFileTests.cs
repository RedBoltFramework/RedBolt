using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class MaterialFileTests
{
    private readonly HeadlessEngineFixture fixture;

    public MaterialFileTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private ResourceCache Resources => fixture.Resources;

    [Fact]
    public void Xml_material_loads_techniques_and_parameters()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "RedBolt.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempDir, "Materials"));
        Directory.CreateDirectory(Path.Combine(tempDir, "Techniques"));

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Techniques", "Diff.xml"),
                "<technique vs=\"LitSolid\" ps=\"LitSolid\" psdefines=\"DIFFMAP\"><pass name=\"base\" /><pass name=\"light\" depthtest=\"equal\" depthwrite=\"false\" blend=\"add\" /></technique>");
            File.WriteAllText(Path.Combine(tempDir, "Materials", "Stone.xml"),
                "<material><technique name=\"Techniques/Diff.xml\" quality=\"1\" /><shader psdefines=\"PACKEDNORMAL\" /><parameter name=\"MatSpecColor\" value=\"0.3 0.3 0.3 16\" /><parameter name=\"UOffset\" value=\"4 0 0 0\" /><cull value=\"none\" /><renderorder value=\"42\" /></material>");

            Resources.AddResourceDir(tempDir);

            var material = Resources.GetResource<Material>("Materials/Stone.xml");

            Assert.Equal(1u, material.GetNumTechniques());
            Assert.Equal(MaterialQuality.Medium, material.GetTechniqueEntry(0).QualityLevel);
            Assert.Equal("LitSolid", material.GetPass(0, "base")?.VertexShader);
            Assert.Equal(BlendMode.Add, material.GetPass(0, "light")?.BlendMode);
            Assert.Equal(CullMode.None, material.GetCullMode());
            Assert.Equal((byte)42, material.GetRenderOrder());

            var specColor = Assert.IsType<Vector4>(material.GetShaderParameter("MatSpecColor"));
            Assert.Equal(new Vector4(0.3f, 0.3f, 0.3f, 16.0f), specColor);
            Assert.Contains("PACKEDNORMAL", material.GetPixelShaderDefines(), StringComparison.Ordinal);
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
