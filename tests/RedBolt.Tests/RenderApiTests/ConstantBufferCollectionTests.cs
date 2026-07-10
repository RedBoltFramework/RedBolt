using System.Runtime.InteropServices;
using RedBolt;

namespace RedBolt.Tests;

public sealed class ConstantBufferCollectionTests
{
    [Fact]
    public void Constant_buffer_collection_aligns_blocks_and_grows_buffers()
    {
        var collection = new ConstantBufferCollection();
        collection.ClearAndInitialize(16);

        var first = collection.AddBlock(5);
        var second = collection.AddBlock(7);
        var third = collection.AddBlock(16353);

        Assert.Equal(new ConstantBufferCollectionRef(0, 0, 5), first.Ref);
        Assert.Equal(new ConstantBufferCollectionRef(0, 16, 7), second.Ref);
        Assert.Equal(new ConstantBufferCollectionRef(1, 0, 16353), third.Ref);
        Assert.Equal<uint>(2u, collection.GetNumBuffers());
        Assert.Equal<uint>(32u, collection.GetBufferSize(0));
        Assert.Equal<uint>(512u, collection.GetGPUBufferSize(0));
        Assert.Equal(32, collection.GetBufferData(0).Length);
        Assert.Equal<uint>(16368u, collection.GetBufferSize(1));
    }

    [Fact]
    public void Constant_buffer_collection_stores_variant_and_matrix_parameters()
    {
        var vectorBytes = new byte[16];
        Assert.True(ConstantBufferCollection.StoreParameter(vectorBytes, new Variant(new Vector4f(1.0f, 2.0f, 3.0f, 4.0f))));
        Assert.Equal([1.0f, 2.0f, 3.0f, 4.0f], MemoryMarshal.Cast<byte, float>(vectorBytes).ToArray());

        var matrixBytes = new byte[64];
        var matrix = new Matrix3x4(
            1.0f, 2.0f, 3.0f, 4.0f,
            5.0f, 6.0f, 7.0f, 8.0f,
            9.0f, 10.0f, 11.0f, 12.0f);

        Assert.True(ConstantBufferCollection.StoreParameter(matrixBytes, matrix));
        Assert.Equal(
            [1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f, 0.0f, 0.0f, 0.0f, 1.0f],
            MemoryMarshal.Cast<byte, float>(matrixBytes).ToArray());
    }
}
