using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class UpstreamRbfxAssetTests
{
    private readonly HeadlessEngineFixture fixture;

    public UpstreamRbfxAssetTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private ResourceCache Resources => fixture.Resources;

    [Fact]
    public void Real_upstream_rbfx_assets_load_when_available()
    {
        var upstreamBin = FindUpstreamBin();
        if (upstreamBin is null)
            return;

        var coreDataDir = Path.Combine(upstreamBin, "CoreData");
        var dataDir = Path.Combine(upstreamBin, "Data");
        Resources.AddResourceDir(coreDataDir);
        Resources.AddResourceDir(dataDir);

        try
        {
            var technique = Resources.GetResource<Technique>("Techniques/Diff.xml");
            var material = Resources.GetResource<Material>("Materials/StoneTiled.xml");
            var plane = Resources.GetResource<Model>("Models/Plane.mdl");
            var mushroom = Resources.GetResource<Model>("Models/Mushroom.mdl");

            Assert.NotNull(technique.GetPass("base"));
            Assert.NotNull(material.GetTechnique(0));
            Assert.Equal("LitSolid", material.GetTechnique(0)?.GetPass("base")?.VertexShader);
            Assert.Equal(1u, plane.GetNumGeometries());
            Assert.NotNull(plane.GetGeometry(0));
            Assert.True(mushroom.GetNumGeometryLodLevels(0) >= 2u);
        }
        finally
        {
            Resources.RemoveResourceDir(dataDir);
            Resources.RemoveResourceDir(coreDataDir);
        }
    }

    private static string? FindUpstreamBin()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (!Directory.Exists(Path.Combine(current.FullName, ".git")))
                continue;

            var upstreamBin = Path.GetFullPath(Path.Combine(current.FullName, "../rbfx/bin"));
            return Directory.Exists(upstreamBin) ? upstreamBin : null;
        }

        return null;
    }
}
