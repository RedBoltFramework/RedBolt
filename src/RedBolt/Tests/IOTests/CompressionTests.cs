using System.Text;
using RedBolt;

using RedBolt.IO;
namespace RedBolt.Tests;

public sealed class CompressionTests
{
    [Fact]
    public void Compression_round_trips_data_and_streams()
    {
        var source = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("RedBolt compression data ", 32)));
        var compressed = new byte[Compression.EstimateCompressBound((uint)source.Length)];
        var compressedSize = Compression.CompressData(compressed, source, (uint)source.Length);

        Assert.NotEqual(0u, compressedSize);

        var result = new byte[source.Length];
        Assert.NotEqual(0u, Compression.DecompressData(result, compressed.AsSpan(0, (int)compressedSize),
            (uint)result.Length));
        Assert.Equal(source, result);

        using var sourceBuffer = new VectorBuffer(source);
        using var compressedBuffer = Compression.CompressVectorBuffer(sourceBuffer);
        using var decompressedBuffer = Compression.DecompressVectorBuffer(compressedBuffer);
        Assert.NotSame(sourceBuffer, compressedBuffer);
        Assert.NotSame(compressedBuffer, decompressedBuffer);
        Assert.Equal(sourceBuffer.Size, sourceBuffer.Position);
        Assert.Equal(compressedBuffer.Size, compressedBuffer.Position);
        Assert.Equal(0u, decompressedBuffer.Position);
        Assert.Equal(source, decompressedBuffer.GetBuffer());
    }

    [Fact]
    public void Compression_uses_explicit_source_and_destination_sizes()
    {
        byte[] source = [1, 2, 3, 4, 5, 6];
        const uint sourceSize = 4;
        var compressed = new byte[Compression.EstimateCompressBound(sourceSize)];

        var compressedSize = Compression.CompressData(compressed, source, sourceSize);
        var result = new byte[checked((int)sourceSize)];
        Compression.DecompressData(result, compressed.AsSpan(0, checked((int)compressedSize)), sourceSize);

        Assert.Equal(source.AsSpan(0, (int)sourceSize).ToArray(), result);
        Assert.Equal(0u, Compression.CompressData(compressed, source, 0));
        Assert.Equal(0u, Compression.DecompressData(result, compressed, 0));
    }
}
