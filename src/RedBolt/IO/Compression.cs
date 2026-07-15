using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RedBolt.Bindings.Lz4;

namespace RedBolt;

// C++: namespace-level free functions (CompressData, DecompressData, CompressStream, etc.)
// C# groups them into a static class.
// C++: namespace-level free functions (CompressData, DecompressData, CompressStream, etc.)
// C# groups them into a static class.
/// <summary>LZ4 compression helpers.</summary>
public static class Compression
{
    /// <summary>Estimate and return worst case LZ4 compressed output size in bytes for given input size.</summary>
    public static uint EstimateCompressBound(uint sourceSize)
    {
        return unchecked((uint)Lz4NativeBindings.Lz4CompressBound(unchecked((int)sourceSize)));
    }

    /// <summary>Compress data using the LZ4 algorithm and return the compressed data size. The needed destination buffer worst-case size is given by EstimateCompressBound().</summary>
    public static uint CompressData(Span<byte> destination, ReadOnlySpan<byte> source)
    {
        if (destination.IsEmpty || source.IsEmpty)
            return 0;

        ref var sourceReference = ref Unsafe.As<byte, sbyte>(ref MemoryMarshal.GetReference(source));
        ref var destinationReference = ref Unsafe.As<byte, sbyte>(ref MemoryMarshal.GetReference(destination));
        var compressedSize = Lz4NativeBindings.Lz4CompressHc(
            in sourceReference,
            out destinationReference,
            source.Length,
            Lz4NativeBindings.Lz4CompressBound(source.Length),
            0);
        return compressedSize > 0 ? (uint)compressedSize : 0;
    }

    /// <summary>Uncompress data using the LZ4 algorithm. The uncompressed data size must be known. Return the number of compressed data bytes consumed.</summary>
    public static uint DecompressData(Span<byte> destination, ReadOnlySpan<byte> source)
    {
        if (destination.IsEmpty || source.IsEmpty)
            return 0;

        ref var sourceReference = ref Unsafe.As<byte, sbyte>(ref MemoryMarshal.GetReference(source));
        ref var destinationReference = ref Unsafe.As<byte, sbyte>(ref MemoryMarshal.GetReference(destination));
        var compressedSize = Lz4NativeBindings.Lz4DecompressFast(
            in sourceReference,
            out destinationReference,
            destination.Length);
        return compressedSize > 0 ? (uint)compressedSize : 0;
    }

    /// <summary>Compress a source stream (from current position to the end) to the destination stream using the LZ4 algorithm. Return true on success.</summary>
    public static bool CompressStream(Serializer destination, Deserializer source, uint compressionLevel = 0)
    {
        var sourceSize = source.GetSize() - source.GetPosition();
        // Prepend the source and dest. data size in the stream so that we know to buffer & uncompress the right amount
        if (sourceSize == 0)
        {
            destination.WriteUInt(0);
            destination.WriteUInt(0);
            return true;
        }

        var maxDestinationSize = EstimateCompressBound(sourceSize);
        var sourceBuffer = new byte[checked((int)sourceSize)];
        var destinationBuffer = new byte[checked((int)maxDestinationSize)];

        if (source.Read(sourceBuffer) != sourceSize)
            return false;

        ref var sourceReference = ref Unsafe.As<byte, sbyte>(ref MemoryMarshal.GetReference(sourceBuffer.AsSpan()));
        ref var destinationReference = ref Unsafe.As<byte, sbyte>(ref MemoryMarshal.GetReference(destinationBuffer.AsSpan()));
        var nativeDestinationSize = Lz4NativeBindings.Lz4CompressHc(
            in sourceReference,
            out destinationReference,
            sourceBuffer.Length,
            Lz4NativeBindings.Lz4CompressBound(sourceBuffer.Length),
            unchecked((int)compressionLevel));
        var destinationSize = nativeDestinationSize > 0 ? (uint)nativeDestinationSize : 0;
        var success = true;
        success &= destination.WriteUInt(sourceSize);
        success &= destination.WriteUInt(destinationSize);
        success &= destination.Write(destinationBuffer.AsSpan(0, checked((int)destinationSize))) == destinationSize;
        return success;
    }

    /// <summary>Decompress a compressed source stream produced using CompressStream() to the destination stream. Return true on success.</summary>
    public static bool DecompressStream(Serializer destination, Deserializer source)
    {
        if (source.IsEof())
            return false;

        var destinationSize = source.ReadUInt();
        var sourceSize = source.ReadUInt();
        if (sourceSize == 0 || destinationSize == 0)
            return true;

        if (sourceSize > source.GetSize())
            return false;

        var sourceBuffer = new byte[checked((int)sourceSize)];
        var destinationBuffer = new byte[checked((int)destinationSize)];

        if (source.Read(sourceBuffer) != sourceSize)
            return false;

        ref var sourceReference = ref Unsafe.As<byte, sbyte>(ref MemoryMarshal.GetReference(sourceBuffer.AsSpan()));
        ref var destinationReference = ref Unsafe.As<byte, sbyte>(ref MemoryMarshal.GetReference(destinationBuffer.AsSpan()));
        Lz4NativeBindings.Lz4DecompressFast(
            in sourceReference,
            out destinationReference,
            destinationBuffer.Length);
        return destination.Write(destinationBuffer) == destinationSize;
    }

    /// <summary>Compress a VectorBuffer using the LZ4 algorithm and return the compressed result buffer.</summary>
    public static VectorBuffer CompressVectorBuffer(VectorBuffer source)
    {
        var result = new VectorBuffer();
        source.Seek(0);
        CompressStream(result, source);
        result.Seek(0);
        return result;
    }

    /// <summary>Decompress a VectorBuffer produced using CompressVectorBuffer().</summary>
    public static VectorBuffer DecompressVectorBuffer(VectorBuffer source)
    {
        var result = new VectorBuffer();
        source.Seek(0);
        DecompressStream(result, source);
        result.Seek(0);
        return result;
    }

}
