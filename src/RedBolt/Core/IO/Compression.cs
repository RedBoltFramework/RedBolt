namespace RedBolt.IO;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Lz4;
using RedBolt.Resource;

public static class Compression
{
    /// <summary>Estimate and return worst case LZ4 compressed output size in bytes for given input size.</summary>
    public static uint EstimateCompressBound(uint srcSize)
    {
        return unchecked((uint)Lz4.Lz4CompressBound(unchecked((int)srcSize)));
    }

    /// <summary>Compress data using the LZ4 algorithm and return the compressed data size. The needed destination buffer worst-case size is given by EstimateCompressBound().</summary>
    public static uint CompressData(Span<byte> dest, ReadOnlySpan<byte> src, uint srcSize)
    {
        if (dest.IsEmpty || src.IsEmpty || srcSize == 0)
            return 0;
        else
        {
            ref var srcReference = ref Unsafe.As<byte, sbyte>(ref MemoryMarshal.GetReference(src));
            return unchecked((uint)Lz4.Lz4CompressHc(
                in srcReference,
                MemoryMarshal.Cast<byte, sbyte>(dest),
                unchecked((int)srcSize),
                Lz4.Lz4CompressBound(unchecked((int)srcSize)),
                0));
        }
    }

    /// <summary>Uncompress data using the LZ4 algorithm. The uncompressed data size must be known. Return the number of compressed data bytes consumed.</summary>
    public static uint DecompressData(Span<byte> dest, ReadOnlySpan<byte> src, uint destSize)
    {
        if (dest.IsEmpty || src.IsEmpty || destSize == 0)
            return 0;
        else
        {
            ref var srcReference = ref Unsafe.As<byte, sbyte>(ref MemoryMarshal.GetReference(src));
            return unchecked((uint)Lz4.Lz4DecompressFast(
                in srcReference,
                MemoryMarshal.Cast<byte, sbyte>(dest),
                unchecked((int)destSize)));
        }
    }

    /// <summary>Compress a source stream (from current position to the end) to the destination stream using the LZ4 algorithm. Return true on success.</summary>
    public static bool CompressStream(Serializer dest, Deserializer src, uint compressionLevel = 0)
    {
        var srcSize = src.GetSize() - src.GetPosition();
        // Prepend the source and dest. data size in the stream so that we know to buffer & uncompress the right amount
        if (srcSize == 0)
        {
            dest.WriteUInt(0);
            dest.WriteUInt(0);
            return true;
        }

        var maxDestSize = EstimateCompressBound(srcSize);
        var srcBuffer = new byte[checked((int)srcSize)];
        var destBuffer = new byte[checked((int)maxDestSize)];

        if (src.Read(srcBuffer) != srcSize)
            return false;

        ref var srcReference = ref Unsafe.As<byte, sbyte>(ref MemoryMarshal.GetReference(srcBuffer.AsSpan()));
        var destSize = unchecked((uint)Lz4.Lz4CompressHc(
            in srcReference,
            MemoryMarshal.Cast<byte, sbyte>(destBuffer.AsSpan()),
            srcBuffer.Length,
            Lz4.Lz4CompressBound(srcBuffer.Length),
            unchecked((int)compressionLevel)));
        var success = true;
        success &= dest.WriteUInt(srcSize);
        success &= dest.WriteUInt(destSize);
        success &= dest.Write(destBuffer.AsSpan(0, checked((int)destSize))) == destSize;
        return success;
    }

    /// <summary>Decompress a compressed source stream produced using CompressStream() to the destination stream. Return true on success.</summary>
    public static bool DecompressStream(Serializer dest, Deserializer src)
    {
        if (src.IsEof())
            return false;

        var destSize = src.ReadUInt();
        var srcSize = src.ReadUInt();
        if (srcSize == 0 || destSize == 0)
            return true; // No data

        if (srcSize > src.GetSize())
            return false; // Illegal source (packed data) size reported, possibly not valid data

        var srcBuffer = new byte[checked((int)srcSize)];
        var destBuffer = new byte[checked((int)destSize)];

        if (src.Read(srcBuffer) != srcSize)
            return false;

        ref var srcReference = ref Unsafe.As<byte, sbyte>(ref MemoryMarshal.GetReference(srcBuffer.AsSpan()));
        Lz4.Lz4DecompressFast(
            in srcReference,
            MemoryMarshal.Cast<byte, sbyte>(destBuffer.AsSpan()),
            destBuffer.Length);
        return dest.Write(destBuffer) == destSize;
    }

    /// <summary>Compress a VectorBuffer using the LZ4 algorithm and return the compressed result buffer.</summary>
    public static VectorBuffer CompressVectorBuffer(VectorBuffer src)
    {
        var ret = new VectorBuffer();
        src.Seek(0);
        CompressStream(ret, src);
        ret.Seek(0);
        return ret;
    }

    /// <summary>Decompress a VectorBuffer produced using CompressVectorBuffer().</summary>
    public static VectorBuffer DecompressVectorBuffer(VectorBuffer src)
    {
        var ret = new VectorBuffer();
        src.Seek(0);
        DecompressStream(ret, src);
        ret.Seek(0);
        return ret;
    }
}
