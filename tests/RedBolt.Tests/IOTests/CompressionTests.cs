using System.Text;
using RedBolt;

namespace RedBolt.Tests;

public sealed class CompressionTests
{
    [Fact]
    public void Compression_round_trips_data_and_streams()
    {
        var source = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("RedBolt compression data ", 32)));
        var compressed = new byte[Compression.EstimateCompressBound((uint)source.Length)];
        var compressedSize = Compression.CompressData(compressed, source);

        Assert.NotEqual(0u, compressedSize);

        var result = new byte[source.Length];
        Assert.NotEqual(0u, Compression.DecompressData(result, compressed.AsSpan(0, (int)compressedSize)));
        Assert.Equal(source, result);

        var sourceBuffer = new VectorBuffer(source);
        var compressedBuffer = Compression.CompressVectorBuffer(sourceBuffer);
        var decompressedBuffer = Compression.DecompressVectorBuffer(compressedBuffer);
        Assert.Equal(source, decompressedBuffer.GetBuffer());
    }
}
