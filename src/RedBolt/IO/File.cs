using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using RedBolt.Bindings.Sdl;

namespace RedBolt;

/// <summary>File opened through the filesystem.</summary>
/// <summary>File opened either through the filesystem or from within a package file.</summary>
public class File : AbstractFile
{
    private const uint ReadBufferSize = 32768;

    /// <summary>File open mode.</summary>
    public enum FileMode
    {
        Read,
        Write,
        ReadWrite
    }

    /// <summary>Absolute file name.</summary>
    private string _absoluteFileName = string.Empty;
    /// <summary>Open mode.</summary>
    private FileMode _mode;
    /// <summary>File handle.</summary>
    private Stream? _handle;
    /// <summary>Read buffer for Android asset or compressed file loading.</summary>
    private byte[]? _readBuffer;
    /// <summary>Decompression input buffer for compressed file loading.</summary>
    private byte[]? _inputBuffer;
    /// <summary>Read buffer position.</summary>
    private uint _readBufferOffset;
    /// <summary>Bytes in the current read buffer.</summary>
    private uint _readBufferSize;
    /// <summary>Start position within a package file, 0 for regular files.</summary>
    private uint _offset;
    /// <summary>Content checksum.</summary>
    private uint _checksum;
    /// <summary>Compression flag.</summary>
    private bool _compressed;
    /// <summary>Synchronization needed before read -flag.</summary>
    private bool _readSyncNeeded;
    /// <summary>Synchronization needed before write -flag.</summary>
    private bool _writeSyncNeeded;

    private SdlRWops? _assetHandle;

    /// <summary>Construct.</summary>
    public File()
    {
        _mode = FileMode.Read;
    }

    /// <summary>Construct and open a filesystem file.</summary>
    public File(string fileName, FileMode mode = FileMode.Read)
        : this()
    {
        Open(fileName, mode);
    }

    /// <summary>Construct and open from a package file.</summary>
    public File(PackageFile? package, string fileName)
        : this()
    {
        Open(package, fileName);
    }

    /// <summary>Destruct. Close the file if open.</summary>
    public override void Dispose()
    {
        Close();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Read bytes from the file. Return number of bytes actually read.</summary>
    public override uint Read(Span<byte> destination)
    {
        if (!IsOpen)
            return 0;

        if (_mode == FileMode.Write)
        {
            Log.Logger.LogError("File not opened for reading");
            return 0;
        }

        var size = Math.Min((uint)destination.Length, Size > Position ? Size - Position : 0);
        if (size == 0)
            return 0;

        if (_assetHandle is not null && !_compressed)
        {
            if (_readBuffer is null)
            {
                _readBuffer = new byte[ReadBufferSize];
                _readBufferOffset = 0;
                _readBufferSize = 0;
            }

            var sizeLeft = size;
            var destinationOffset = 0;
            while (sizeLeft != 0)
            {
                if (_readBufferOffset >= _readBufferSize)
                {
                    _readBufferSize = Math.Min(Size - Position, ReadBufferSize);
                    _readBufferOffset = 0;
                    if (!ReadInternal(_readBuffer.AsSpan(0, checked((int)_readBufferSize))))
                        return 0;
                }

                var copySize = Math.Min(_readBufferSize - _readBufferOffset, sizeLeft);
                _readBuffer.AsSpan(checked((int)_readBufferOffset), checked((int)copySize))
                    .CopyTo(destination.Slice(destinationOffset, checked((int)copySize)));
                sizeLeft -= copySize;
                _readBufferOffset += copySize;
                Position += copySize;
                destinationOffset += checked((int)copySize);
            }

            return size;
        }

        if (_readSyncNeeded)
        {
            SeekInternal(Position + _offset);
            _readSyncNeeded = false;
        }

        if (_compressed)
        {
            var sizeLeft = size;
            var destinationOffset = 0;
            Span<byte> blockHeader = stackalloc byte[4];
            while (sizeLeft != 0)
            {
                if (_readBuffer is null || _readBufferOffset >= _readBufferSize)
                {
                    if (!ReadInternal(blockHeader))
                        return 0;

                    var unpackedSize = BinaryPrimitives.ReadUInt16LittleEndian(blockHeader);
                    var packedSize = BinaryPrimitives.ReadUInt16LittleEndian(blockHeader[2..]);
                    if (_readBuffer is null)
                    {
                        _readBuffer = new byte[unpackedSize];
                        _inputBuffer = new byte[packedSize];
                    }
                    else if (_inputBuffer is null || _inputBuffer.Length < packedSize)
                        _inputBuffer = new byte[packedSize];

                    if (!ReadInternal(_inputBuffer.AsSpan(0, packedSize)))
                        return 0;

                    Compression.DecompressData(_readBuffer.AsSpan(0, unpackedSize), _inputBuffer.AsSpan(0, packedSize));
                    _readBufferSize = unpackedSize;
                    _readBufferOffset = 0;
                }

                var copySize = Math.Min(_readBufferSize - _readBufferOffset, sizeLeft);
                _readBuffer.AsSpan(checked((int)_readBufferOffset), checked((int)copySize))
                    .CopyTo(destination.Slice(destinationOffset, checked((int)copySize)));
                sizeLeft -= copySize;
                _readBufferOffset += copySize;
                Position += copySize;
                destinationOffset += checked((int)copySize);
            }

            return size;
        }

        if (!ReadInternal(destination[..checked((int)size)]))
        {
            SeekInternal(Position + _offset);
            Log.Logger.LogError("Error while reading from file {Name}", Name);
            return 0;
        }

        _writeSyncNeeded = true;
        Position += size;
        return size;
    }

    /// <summary>Set position from the beginning of the file.</summary>
    public override uint Seek(uint position)
    {
        if (!IsOpen)
            return 0;

        if (_mode == FileMode.Read && position > Size)
            position = Size;

        if (_compressed)
        {
            if (position == 0)
            {
                Position = 0;
                _readBufferOffset = 0;
                _readBufferSize = 0;
                SeekInternal(_offset);
            }
            else if (position >= Position)
            {
                Span<byte> skipBuffer = stackalloc byte[4096];
                while (position > Position)
                    Read(skipBuffer[..checked((int)Math.Min(position - Position, (uint)skipBuffer.Length))]);
            }
            else
                Log.Logger.LogError("Seeking backward in a compressed file is not supported");

            return Position;
        }

        SeekInternal(position + _offset);
        Position = position;
        _readSyncNeeded = false;
        _writeSyncNeeded = false;
        return position;
    }

    /// <summary>Write bytes to the file. Return number of bytes actually written.</summary>
    public override uint Write(ReadOnlySpan<byte> data)
    {
        if (!IsOpen || data.IsEmpty)
            return 0;

        if (_mode == FileMode.Read)
        {
            Log.Logger.LogError("File not opened for writing");
            return 0;
        }

        if (_writeSyncNeeded)
        {
            SeekInternal(Position + _offset);
            _writeSyncNeeded = false;
        }

        try
        {
            _handle!.Write(data);
        }
        catch (IOException)
        {
            SeekInternal(Position + _offset);
            Log.Logger.LogError("Error while writing to file {Name}", Name);
            return 0;
        }

        _readSyncNeeded = true;
        Position += (uint)data.Length;
        if (Position > Size)
            Size = Position;
        return (uint)data.Length;
    }

    /// <summary>Return absolute file name in file system.</summary>
    public override string AbsoluteName => _absoluteFileName;

    /// <summary>Return a checksum of the file contents using the SDBM hash algorithm.</summary>
    public override uint GetChecksum()
    {
        if (_offset != 0 || _checksum != 0 || !IsOpen || _mode == FileMode.Write)
            return _checksum;

        var oldPosition = Position;
        _checksum = 0;
        Seek(0);
        var block = new byte[1024];
        while (!IsEof())
        {
            var readBytes = Read(block);
            for (var i = 0u; i < readBytes; ++i)
                _checksum = MathDefs.SDBMHash(_checksum, block[i]);
        }
        Seek(oldPosition);
        return _checksum;
    }

    /// <summary>Open a filesystem file. Return true if successful.</summary>
    public bool Open(string fileName, FileMode mode = FileMode.Read)
    {
        return OpenInternal(fileName, mode);
    }

    /// <summary>Open from within a package file. Return true if successful.</summary>
    public bool Open(PackageFile? package, string fileName)
    {
        if (package is null)
            return false;

        var entry = package.GetEntry(fileName);
        if (entry is null)
            return false;

        if (!OpenInternal(package.GetName(), FileMode.Read, true))
            return false;

        SetName(fileName);
        _offset = entry.Value.Offset;
        _checksum = entry.Value.Checksum;
        Size = entry.Value.Size;
        _compressed = package.IsCompressed();
        SeekInternal(_offset);
        return true;
    }

    /// <summary>Open file internally using either C standard IO functions or SDL RWops for Android asset files. Return true if successful.</summary>
    private bool OpenInternal(string fileName, FileMode mode, bool fromPackage = false)
    {
        Close();
        _compressed = false;
        _readSyncNeeded = false;
        _writeSyncNeeded = false;

        if (string.IsNullOrEmpty(fileName))
            return false;

        if (OperatingSystem.IsAndroid() && fileName.StartsWith("/apk/", StringComparison.Ordinal))
        {
            if (mode != FileMode.Read)
                return false;

            using var assetName = new CString(fileName[5..]);
            using var readMode = new CString("rb");
            _assetHandle = SdlNativeBindings.SdlRWFromFile(assetName.Bytes, readMode.Bytes);
            if (_assetHandle.IsNull)
            {
                _assetHandle = null;
                return false;
            }

            _absoluteFileName = fileName;
            _mode = mode;
            Position = 0;
            if (!fromPackage)
            {
                var size = SdlNativeBindings.SdlRWsize(_assetHandle);
                if (size < 0 || size > uint.MaxValue)
                {
                    Close();
                    return false;
                }
                Size = (uint)size;
                _offset = 0;
            }
            _checksum = 0;
            return true;
        }

        try
        {
            var systemMode = mode switch
            {
                FileMode.Read => System.IO.FileMode.Open,
                FileMode.Write => System.IO.FileMode.Create,
                FileMode.ReadWrite => System.IO.FileMode.OpenOrCreate,
                _ => System.IO.FileMode.Open
            };
            var access = mode == FileMode.Read ? FileAccess.Read : FileAccess.ReadWrite;
            _handle = new FileStream(fileName, systemMode, access, FileShare.Read);
            if (!fromPackage && _handle.Length > uint.MaxValue)
            {
                Close();
                return false;
            }
            _absoluteFileName = Path.GetFullPath(fileName);
            _mode = mode;
            Position = 0;
            if (!fromPackage)
            {
                Size = checked((uint)_handle.Length);
                _offset = 0;
            }
            _checksum = 0;
            return true;
        }
        catch (IOException)
        {
            Close();
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            Close();
            return false;
        }
    }

    /// <summary>Close the file.</summary>
    public override void Close()
    {
        _readBuffer = null;
        _inputBuffer = null;
        if (_assetHandle is not null)
        {
            SdlNativeBindings.SdlRWclose(_assetHandle);
            _assetHandle = null;
        }
        _handle?.Dispose();
        _handle = null;
        Position = 0;
        Size = 0;
        _offset = 0;
        _checksum = 0;
    }

    /// <summary>Flush any buffered output to the file.</summary>
    public void Flush() => _handle?.Flush();

    /// <summary>Return the open mode.</summary>
    public FileMode Mode => _mode;

    /// <summary>Return whether is open.</summary>
    public override bool IsOpen => _handle is not null || _assetHandle is not null && !_assetHandle.IsNull;

    /// <summary>Return the file handle.</summary>
    public Stream? Handle => _handle;

    /// <summary>Return whether the file originates from a package.</summary>
    public bool IsPackaged => _offset != 0;

    /// <summary>Reads a binary file to buffer.</summary>
    public void ReadBinary(List<byte> buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        buffer.Clear();
        if (Size == 0)
            return;

        var data = new byte[Size];
        _ = Read(data);
        buffer.AddRange(data);
    }

    /// <summary>Reads a binary file to buffer.</summary>
    public byte[] ReadBinary()
    {
        var result = new List<byte>();
        ReadBinary(result);
        return result.ToArray();
    }

    /// <summary>Reads a text file, ensuring data from file is 0 terminated.</summary>
    public void ReadText(out string text)
    {
        text = string.Empty;
        if (Size == 0)
            return;

        var data = new byte[checked((int)Size)];
        _ = Read(data);
        text = Encoding.UTF8.GetString(data);
    }

    /// <summary>Reads a text file, ensuring data from file is 0 terminated.</summary>
    public string ReadText()
    {
        ReadText(out var text);
        return text;
    }

    /// <summary>Copy a file from a source file, must be opened and FILE_WRITE.</summary>
    /// <remarks>Unlike FileSystem.Copy this copy works when the source file is in a package file.</remarks>
    public bool Copy(File? sourceFile)
    {
        if (sourceFile is null || !sourceFile.IsOpen || sourceFile.Mode != FileMode.Read)
            return false;
        if (!IsOpen || Mode != FileMode.Write)
            return false;

        var fileSize = sourceFile.Size;
        var buffer = new byte[checked((int)fileSize)];
        var bytesRead = sourceFile.Read(buffer);
        var bytesWritten = Write(buffer);
        return bytesRead == fileSize && bytesWritten == fileSize;
    }

    /// <summary>Perform the file read internally using either C standard IO functions or SDL RWops for Android asset files. Return true if successful. This does not handle compressed package file reading.</summary>
    private unsafe bool ReadInternal(Span<byte> destination)
    {
        try
        {
            if (_assetHandle is not null)
            {
                fixed (byte* pointer = destination)
                {
                    return SdlNativeBindings.SdlRWread(_assetHandle, (IntPtr)pointer, 1, (nuint)destination.Length)
                        == (nuint)destination.Length;
                }
            }
            return _handle!.Read(destination) == destination.Length;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>Seek in file internally using either C standard IO functions or SDL RWops for Android asset files.</summary>
    private void SeekInternal(uint newPosition)
    {
        if (_assetHandle is not null)
            _ = SdlNativeBindings.SdlRWseek(_assetHandle, (nint)newPosition, 0);
        else if (_handle is not null)
            _handle.Position = newPosition;
    }
}
