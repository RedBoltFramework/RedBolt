using System.IO.Compression;

namespace RedBolt;

internal static class ImageFileWriter
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static void Save(VirtualFileSystem virtualFileSystem, string fileName, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ArgumentNullException.ThrowIfNull(virtualFileSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        using var stream = new MemoryStream();
        var extension = Path.GetExtension(fileName);
        if (extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
            SaveBmp(stream, width, height, rgbaPixels);
        else
            SavePng(stream, width, height, rgbaPixels);

        var file = virtualFileSystem.OpenFile(new FileIdentifier(fileName), File.FileMode.Write)
            ?? throw new IOException($"Failed to open screenshot file '{fileName}'.");
        try
        {
            var data = stream.ToArray();
            if (file.Write(data) != data.Length)
                throw new IOException($"Failed to write screenshot file '{fileName}'.");
        }
        finally
        {
            file.Close();
        }
    }

    private static void SaveBmp(Stream stream, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (rgbaPixels.Length != checked(width * height * 4))
            throw new ArgumentException("Pixel data size does not match the requested image dimensions.", nameof(rgbaPixels));

        const int fileHeaderSize = 14;
        const int dibHeaderSize = 40;
        var pixelDataSize = checked(width * height * 4);
        var fileSize = checked(fileHeaderSize + dibHeaderSize + pixelDataSize);

        using var writer = new BinaryWriter(stream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write(fileHeaderSize + dibHeaderSize);

        writer.Write(dibHeaderSize);
        writer.Write(width);
        writer.Write(-height);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(0);
        writer.Write(pixelDataSize);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        var row = new byte[width * 4];
        for (var y = 0; y < height; ++y)
        {
            var sourceRow = rgbaPixels.Slice(y * width * 4, width * 4);
            for (var x = 0; x < width; ++x)
            {
                var sourceOffset = x * 4;
                row[sourceOffset + 0] = sourceRow[sourceOffset + 2];
                row[sourceOffset + 1] = sourceRow[sourceOffset + 1];
                row[sourceOffset + 2] = sourceRow[sourceOffset + 0];
                row[sourceOffset + 3] = sourceRow[sourceOffset + 3];
            }

            writer.Write(row);
        }
    }

    private static void SavePng(Stream stream, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (rgbaPixels.Length != checked(width * height * 4))
            throw new ArgumentException("Pixel data size does not match the requested image dimensions.", nameof(rgbaPixels));

        using var writer = new BinaryWriter(stream);

        writer.Write(PngSignature);

        var ihdr = new byte[13];
        WriteBigEndian(ihdr.AsSpan(0, 4), width);
        WriteBigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WriteChunk(writer, "IHDR", ihdr);

        var scanlineBytes = width * 4;
        using var rawImageData = new MemoryStream();
        for (var y = 0; y < height; ++y)
        {
            rawImageData.WriteByte(0);
            rawImageData.Write(rgbaPixels.Slice(y * scanlineBytes, scanlineBytes));
        }

        rawImageData.Position = 0;
        using var compressedData = new MemoryStream();
        using (var zlib = new ZLibStream(compressedData, CompressionLevel.Optimal, leaveOpen: true))
            rawImageData.CopyTo(zlib);
        WriteChunk(writer, "IDAT", compressedData.ToArray());
        WriteChunk(writer, "IEND", []);
    }

    private static void WriteChunk(BinaryWriter writer, string chunkType, ReadOnlySpan<byte> payload)
    {
        writer.Write(BitConverter.GetBytes(BitConverter.IsLittleEndian
            ? ReverseEndianness(payload.Length)
            : payload.Length));

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(chunkType);
        writer.Write(typeBytes);
        writer.Write(payload);

        var crcBuffer = new byte[typeBytes.Length + payload.Length];
        Buffer.BlockCopy(typeBytes, 0, crcBuffer, 0, typeBytes.Length);
        payload.CopyTo(crcBuffer.AsSpan(typeBytes.Length));
        var crc = ComputeCrc32(crcBuffer);
        writer.Write(BitConverter.GetBytes(BitConverter.IsLittleEndian
            ? ReverseEndianness((int)crc)
            : (int)crc));
    }

    private static void WriteBigEndian(Span<byte> destination, int value)
    {
        destination[0] = (byte)((value >> 24) & 0xff);
        destination[1] = (byte)((value >> 16) & 0xff);
        destination[2] = (byte)((value >> 8) & 0xff);
        destination[3] = (byte)(value & 0xff);
    }

    private static int ReverseEndianness(int value)
    {
        return ((value & 0x000000ff) << 24)
            | ((value & 0x0000ff00) << 8)
            | ((value & 0x00ff0000) >> 8)
            | ((int)((uint)value >> 24) & 0x000000ff);
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (var index = 0; index < table.Length; ++index)
        {
            var crc = (uint)index;
            for (var bit = 0; bit < 8; ++bit)
                crc = (crc & 1) != 0 ? 0xedb88320u ^ (crc >> 1) : crc >> 1;
            table[index] = crc;
        }

        return table;
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xffffffffu;
        foreach (var value in data)
            crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        return crc ^ 0xffffffffu;
    }
}
