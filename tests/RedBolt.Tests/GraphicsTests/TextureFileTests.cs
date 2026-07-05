using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class TextureFileTests
{
    private readonly HeadlessEngineFixture fixture;

    public TextureFileTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private ResourceCache Resources => fixture.Resources;

    [Fact]
    public void Repo_backed_rbfx_dds_textures_load_through_resource_cache()
    {
        RbfxTestData.AddResourceDirs(Resources);
        try
        {
            var texture = Resources.GetResource<Texture2D>("Textures/StoneDiffuse.dds");

            Assert.True(texture.Width > 0);
            Assert.True(texture.Height > 0);
            Assert.Equal(TextureFormat.RGBA8, texture.Format);
            Assert.NotNull(texture.GetData(0));
            Assert.Equal(texture.Width * texture.Height * 4, texture.GetData(0)!.Length);
            Assert.Contains(texture.GetData(0)!, static value => value != 0);
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(Resources);
        }
    }
}
