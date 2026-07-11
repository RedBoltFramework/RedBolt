using System.IO;
using System.Text;

namespace RedBolt;

/// <summary>File opened through the filesystem.</summary>
/// <summary>File opened either through the filesystem or from within a package file.</summary>
public class File : AbstractFile
{
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

    // TODO: port C++ SDL_RWops* assetHandle_; when managed Android asset access exists.

    // These forwards preserve existing managed call sites that used System.IO.File before this class was ported.
    public static bool Exists(string path) => System.IO.File.Exists(path);
    public static FileStream OpenRead(string path) => System.IO.File.OpenRead(path);
    public static FileStream Create(string path) => System.IO.File.Create(path);
    public static void Delete(string path) => System.IO.File.Delete(path);
    public static void AppendAllText(string path, string contents) => System.IO.File.AppendAllText(path, contents);
    public static void WriteAllText(string path, string contents) => System.IO.File.WriteAllText(path, contents);
    public static string ReadAllText(string path) => System.IO.File.ReadAllText(path);
    public static IEnumerable<string> ReadLines(string path) => System.IO.File.ReadLines(path);
    public static FileAttributes GetAttributes(string path) => System.IO.File.GetAttributes(path);
    public static DateTime GetLastWriteTimeUtc(string path) => System.IO.File.GetLastWriteTimeUtc(path);

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

    /// <summary>Destruct. Close the file if open.</summary>
    ~File()
    {
        Close();
    }

    /// <summary>Read bytes from the file. Return number of bytes actually read.</summary>
    public override uint Read(Span<byte> destination)
    {
        if (!IsOpen || _mode == FileMode.Write)
            return 0;

        var size = Math.Min((uint)destination.Length, Size > Position ? Size - Position : 0);
        if (size == 0)
            return 0;

        if (_readSyncNeeded)
        {
            SeekInternal(Position + _offset);
            _readSyncNeeded = false;
        }

        // TODO: port compressed package and Android asset read paths from C++.
        if (!ReadInternal(destination[..checked((int)size)]))
        {
            SeekInternal(Position + _offset);
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

        SeekInternal(position + _offset);
        Position = position;
        _readSyncNeeded = false;
        _writeSyncNeeded = false;
        return position;
    }

    /// <summary>Write bytes to the file. Return number of bytes actually written.</summary>
    public override uint Write(ReadOnlySpan<byte> data)
    {
        if (!IsOpen || _mode == FileMode.Read || data.IsEmpty)
            return 0;

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

    // TODO: port C++ bool Open(PackageFile* package, const ea::string& fileName).
    // The managed PackageFile type and package-entry/compression API do not exist yet.

    /// <summary>Open file internally using either C standard IO functions or SDL RWops for Android asset files. Return true if successful.</summary>
    private bool OpenInternal(string fileName, FileMode mode, bool fromPackage = false)
    {
        Close();
        _compressed = false;
        _readSyncNeeded = false;
        _writeSyncNeeded = false;

        if (string.IsNullOrEmpty(fileName))
            return false;

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
            if (_handle.Length > uint.MaxValue)
            {
                Close();
                return false;
            }
            _absoluteFileName = Path.GetFullPath(fileName);
            _mode = mode;
            Position = 0;
            Size = checked((uint)_handle.Length);
            _offset = 0;
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
    public override bool IsOpen => _handle is not null;

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
    private bool ReadInternal(Span<byte> destination)
    {
        try
        {
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
        if (_handle is not null)
            _handle.Position = newPosition;
    }
}
