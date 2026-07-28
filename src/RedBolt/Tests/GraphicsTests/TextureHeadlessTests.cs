namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Graphics;
using RedBolt.IO;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class TextureHeadlessTests
{
    [Fact]
    public void Texture_resources_skip_payload_loading_without_render_device()
    {
        Texture[] textures =
        [
            new Texture2D { Name = "Textures/Headless2D" },
            new Texture3D { Name = "Textures/Headless3D" },
            new TextureCube { Name = "Textures/HeadlessCube" },
            new Texture2DArray { Name = "Textures/HeadlessArray" }
        ];

        foreach (var texture in textures)
        {
            using (texture)
            using (var source = new VectorBuffer())
            {
                Assert.True(texture.BeginLoad(source));
                Assert.True(texture.EndLoad());
            }
        }
    }
}
