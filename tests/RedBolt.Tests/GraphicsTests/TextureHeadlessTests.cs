using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class TextureHeadlessTests
{
    [Fact]
    public void Texture_resources_skip_payload_loading_without_render_device()
    {
        Texture[] textures =
        [
            new Texture2D("Textures/Headless2D"),
            new Texture3D("Textures/Headless3D"),
            new TextureCube("Textures/HeadlessCube"),
            new Texture2DArray("Textures/HeadlessArray")
        ];

        foreach (var texture in textures)
        {
            using (texture)
            using (var source = new MemoryStream())
            {
                Assert.True(texture.BeginLoad(source));
                Assert.True(texture.EndLoad());
            }
        }
    }
}
