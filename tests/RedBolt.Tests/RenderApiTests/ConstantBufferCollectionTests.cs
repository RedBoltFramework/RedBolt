using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
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

        Assert.Equal(new ConstantBufferCollectionRef { Index = 0, Offset = 0, Size = 5 }, first.Ref);
        Assert.Equal(new ConstantBufferCollectionRef { Index = 0, Offset = 16, Size = 7 }, second.Ref);
        Assert.Equal(new ConstantBufferCollectionRef { Index = 1, Offset = 0, Size = 16353 }, third.Ref);
        Assert.Equal(2, collection.GetNumBuffers());
        Assert.Equal(32, collection.GetBufferSize(0));
        Assert.Equal(512, collection.GetGPUBufferSize(0));
        Assert.Equal(16384, collection.GetBufferData(0).Length);
        Assert.Equal(16368, collection.GetBufferSize(1));
        Assert.Equal(12, Unsafe.SizeOf<ConstantBufferCollectionRef>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<ConstantBufferCollectionRef>());
        Assert.False(typeof(ConstantBufferCollection).IsSealed);
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

        Assert.True(ConstantBufferCollection.StoreParameter(matrixBytes, new Variant(matrix)));
        Assert.Equal(
            [1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f, 0.0f, 0.0f, 0.0f, 1.0f],
            MemoryMarshal.Cast<byte, float>(matrixBytes).ToArray());

        var matricesBytes = new byte[96];
        Assert.True(ConstantBufferCollection.StoreParameter(matricesBytes, [matrix, Matrix3x4.Identity]));
        Assert.Equal(
            [1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f,
                1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f],
            MemoryMarshal.Cast<byte, float>(matricesBytes).ToArray());

        var matrix3Bytes = new byte[48];
        Assert.True(ConstantBufferCollection.StoreParameter(matrix3Bytes, new Variant(Matrix3.Identity)));
        Assert.Equal(
            [1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f],
            MemoryMarshal.Cast<byte, float>(matrix3Bytes).ToArray());

        var matrix4Bytes = new byte[64];
        Assert.True(ConstantBufferCollection.StoreParameter(matrix4Bytes, new Variant(Matrix4.Identity)));
        Assert.Equal(Matrix4.Identity.Data.ToArray(), MemoryMarshal.Cast<byte, float>(matrix4Bytes).ToArray());

        var doubleBytes = new byte[sizeof(float)];
        Assert.True(ConstantBufferCollection.StoreParameter(doubleBytes, new Variant(2.5)));
        Assert.Equal(2.5f, MemoryMarshal.Cast<byte, float>(doubleBytes)[0]);

        Assert.False(ConstantBufferCollection.StoreParameter(new byte[44], matrix));
        Assert.False(ConstantBufferCollection.StoreParameter(new byte[16], new Variant("unsupported")));
    }
}
