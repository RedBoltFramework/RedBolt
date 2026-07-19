using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class GeometryGpuTests
{
    [Fact]
    public void Geometry_registers_factory_reflection()
    {
        var context = new Context();
        Geometry.RegisterObject(context);

        using var geometry = Assert.IsType<Geometry>(
            context.CreateObject(ObjectReflection<Geometry>.TypeId));
        Assert.Equal(typeof(Geometry).GetClassName(), geometry.GetTypeName());
    }

    [Fact]
    public void VertexBuffer_cpu_metadata_works()
    {
        var vb = new VertexBuffer();
        vb.SetShadowed(true);
        vb.SetSize(4, 12, dynamic: true);
        vb.Update(Enumerable.Range(0, 48).Select(v => (byte)v).ToArray());

        Assert.Equal(4u, vb.GetVertexCount());
        Assert.Equal(12u, vb.GetVertexSize());
        Assert.True(vb.IsShadowed());
        Assert.Equal(48, vb.GetShadowData().Length);
    }

    [Fact]
    public void IndexBuffer_cpu_metadata_works()
    {
        var ib = new IndexBuffer();
        ib.SetSize(6, largeIndices: false, dynamic: true);
        ib.SetShadowed(true);
        ib.SetUnpackedData([0u, 1u, 2u, 2u, 3u, 0u]);

        Assert.True(ib.GetUsedVertexRange(0, 6, out var minVertex, out var usedVertexCount));
        Assert.Equal(0u, minVertex);
        Assert.Equal(4u, usedVertexCount);
        Assert.Equal(IndexBufferType.UInt16, IndexBuffer.GetIndexBufferType(ib));
    }

    [Fact]
    public void Geometry_cpu_metadata_works()
    {
        var vb = new VertexBuffer();
        vb.SetSize(4, 12, dynamic: true);
        var ib = new IndexBuffer();
        ib.SetSize(6, largeIndices: false, dynamic: true);

        var geometry = new Geometry();
        geometry.SetVertexBuffers([vb]);
        geometry.SetIndexBuffer(ib);
        geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 6, 0, 4);
        geometry.SetLodDistance(5.0f);

        Assert.Equal(2u, geometry.GetPrimitiveCount());
        Assert.Equal(1u, geometry.GetNumVertexBuffers());
        Assert.True(geometry.IsInstanced(GeometryType.Static));
        Assert.True(MathDefs.Equals(geometry.GetLodDistance(), 5.0f));
    }

    [Fact]
    public void Geometry_draw_range_uses_forced_headless_index_shadow_data()
    {
        var vb = new VertexBuffer();
        vb.SetSize(4, 12, dynamic: true);
        var ib = new IndexBuffer();
        ib.SetSize(6, largeIndices: false, dynamic: true);

        var geometry = new Geometry();
        geometry.SetVertexBuffer(0, vb);
        geometry.SetIndexBuffer(ib);

        Assert.True(geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 6));
        Assert.Equal(0u, geometry.GetVertexStart());
        Assert.Equal(1u, geometry.GetVertexCount());
    }

    [Fact]
    public void Texture2D_size_and_sampler_work()
    {
        var tex = new Texture2D("Textures/Smoke2D");
        tex.SetNumLevels(0);
        tex.SetSRGB(true);
        Assert.True(tex.SetSize(4, 4, TextureFormat.RGBA8));
        Assert.Equal(3, tex.GetLevels());

        tex.SetFilterMode(TextureFilterMode.Bilinear);
        tex.SetAddressMode(TextureCoordinate.U, TextureAddressMode.Clamp);
        tex.SetAnisotropy(8u);
        tex.SetShadowCompare(true);
        tex.SetLinear(true);
        tex.SetMipsToSkip(MaterialQuality.Low, 3);
        tex.SetMipsToSkip(MaterialQuality.Medium, 1);

        Assert.Equal(TextureFilterMode.Bilinear, tex.GetFilterMode());
        Assert.Equal(TextureAddressMode.Clamp, tex.GetAddressMode(TextureCoordinate.U));
        Assert.Equal(8u, tex.GetAnisotropy());
        Assert.True(tex.GetShadowCompare());
        Assert.True(tex.GetLinear());
        Assert.True(tex.GetSRGB());
        Assert.Equal(3, tex.GetMipsToSkip(MaterialQuality.Low));
        Assert.Equal(1, tex.GetMipsToSkip(MaterialQuality.Medium));

        Assert.False(tex.SetSize(0, 4, TextureFormat.RGBA8));
        Assert.Equal(0, tex.Width);
        Assert.Equal(4, tex.Height);
    }

    [Fact]
    public void Texture2D_cpu_data_works()
    {
        var tex = new Texture2D("Textures/Smoke2D");
        tex.SetSize(4, 4, TextureFormat.RGBA8);
        var data = Enumerable.Range(0, 64).Select(v => (byte)v).ToArray();
        Assert.True(tex.SetData(0, 0, 0, 4, 4, data));
        Assert.Equal(4, tex.GetComponents());
        Assert.Equal(64, tex.GetDataSize(4, 4));
    }

    [Fact]
    public void TextureCube_face_data_works()
    {
        var tex = new TextureCube("Textures/SmokeCube");
        tex.SetNumLevels(1);
        Assert.True(tex.SetSize(2, TextureFormat.RGBA8));
        Assert.True(tex.SetData(CubeMapFace.PositiveX, 0u, 0, 0, 2, 2,
            Enumerable.Repeat((byte)7, 16).ToArray()));
        Assert.False(tex.GetData(CubeMapFace.PositiveX, 0u, new byte[16]));
    }

    [Fact]
    public void Texture3D_volume_data_works()
    {
        var tex = new Texture3D("Textures/Smoke3D");
        tex.SetNumLevels(1);
        Assert.True(tex.SetSize(2, 2, 2, TextureFormat.R8));
        Assert.True(tex.SetData(0, 0, 0, 0, 2, 2, 2,
            Enumerable.Range(0, 8).Select(v => (byte)v).ToArray()));
        Assert.False(tex.GetData(0, new byte[8]));

        Assert.False(tex.SetSize(0, 2, 2, TextureFormat.R8));
        Assert.Equal(0, tex.Width);
        Assert.Equal(2, tex.Depth);
    }

    [Fact]
    public void Texture2DArray_layered_data_works()
    {
        var tex = new Texture2DArray("Textures/SmokeArray");
        tex.SetNumLevels(1);
        Assert.True(tex.SetSize(2, 2, 2, TextureFormat.R8));
        Assert.True(tex.SetData(1, 0, 0, 0, 2, 2, new byte[] { 1, 2, 3, 4 }));
        Assert.False(tex.GetData(1, 0, new byte[4]));
        Assert.Equal(2, tex.GetRawTexture()!.GetParams().ArraySize);
        Assert.Equal(2, tex.Layers);

        Assert.False(tex.SetSize(0, 0, 2, TextureFormat.R8));
        Assert.Equal(0, tex.Width);
        Assert.Equal(2, tex.Layers);
    }
}
