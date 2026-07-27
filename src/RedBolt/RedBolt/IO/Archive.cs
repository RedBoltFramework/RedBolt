using System.Diagnostics;

using RedBolt.Core;

namespace RedBolt.IO;

public enum ArchiveBlockType
{
    Sequential,
    Unordered,
    Array
}

public class ArchiveBlock : IDisposable
{
    public ArchiveBlock()
    {
    }

    public ArchiveBlock(Archive archive, int sizeHint = 0)
    {
        _archive = archive;
        _sizeHint = sizeHint;
    }

    public void Dispose()
    {
        if (_archive is null)
            return;

        var archive = _archive;
        _archive = null;
        archive.EndBlock();
    }

    public ArchiveBlock(ArchiveBlock other)
    {
        Swap(other);
    }

    public ArchiveBlock MoveAssign(ArchiveBlock other)
    {
        Swap(other);
        return this;
    }

    public void Swap(ArchiveBlock other)
    {
        var archive = _archive;
        _archive = other._archive;
        other._archive = archive;

        var sizeHint = _sizeHint;
        _sizeHint = other._sizeHint;
        other._sizeHint = sizeHint;
    }

    public bool IsValid => _archive is not null;

    public int SizeHint => _sizeHint;

    private Archive? _archive;
    private int _sizeHint;
}

public abstract class Archive : IDisposable
{
    public virtual void Dispose()
    {
    }

    public abstract Context? Context { get; }
    public abstract string Name { get; }
    public abstract uint Checksum { get; }
    public abstract bool IsInput { get; }
    public abstract bool IsHumanReadable { get; }
    public abstract bool IsUnorderedAccessSupportedInCurrentBlock { get; }
    public abstract bool HasElementOrBlock(string name);
    public abstract bool IsEof { get; }
    public abstract string CurrentBlockPath { get; }

    public abstract void BeginBlock(string name, ref int sizeHint, bool safe, ArchiveBlockType type);
    public abstract void EndBlock();
    public abstract void Flush();

    public abstract void Serialize(string name, ref bool value);
    public abstract void Serialize(string name, ref sbyte value);
    public abstract void Serialize(string name, ref byte value);
    public abstract void Serialize(string name, ref short value);
    public abstract void Serialize(string name, ref ushort value);
    public abstract void Serialize(string name, ref int value);
    public abstract void Serialize(string name, ref uint value);
    public abstract void Serialize(string name, ref long value);
    public abstract void Serialize(string name, ref ulong value);
    public abstract void Serialize(string name, ref float value);
    public abstract void Serialize(string name, ref double value);
    public abstract void Serialize(string name, ref string value);
    public abstract void SerializeBytes(string name, Span<byte> bytes);
    public abstract void SerializeVle(string name, ref uint value);

    public abstract uint SerializeVersion(uint version);

    public static bool ValidateName(string name)
    {
        if (name.Length == 0)
            return false;

        if (!char.IsAsciiLetter(name[0]) && name[0] != '_')
            return false;

        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '_' and not '.' and not ':')
                return false;
        }

        return true;
    }

    public ArchiveBlock OpenBlock(string name, int sizeHint, bool safe, ArchiveBlockType type)
    {
        BeginBlock(name, ref sizeHint, safe, type);
        return new ArchiveBlock(this, sizeHint);
    }

    public ArchiveBlock OpenSequentialBlock(string name) => OpenBlock(name, 0, false, ArchiveBlockType.Sequential);
    public ArchiveBlock OpenUnorderedBlock(string name) => OpenBlock(name, 0, false, ArchiveBlockType.Unordered);
    public ArchiveBlock OpenArrayBlock(string name, int sizeHint = 0) => OpenBlock(name, sizeHint, false, ArchiveBlockType.Array);
    public ArchiveBlock OpenSafeSequentialBlock(string name) => OpenBlock(name, 0, true, ArchiveBlockType.Sequential);
    public ArchiveBlock OpenSafeUnorderedBlock(string name) => OpenBlock(name, 0, true, ArchiveBlockType.Unordered);
}

public abstract class ArchiveBase : Archive
{
    public sealed override Context Context => _context;
    public override string Name => string.Empty;
    public override uint Checksum => 0;
    public sealed override bool IsEof => _isEof;

    public sealed override void Flush()
    {
        FlushDelayedException();
    }

    public override uint SerializeVersion(uint version)
    {
        SerializeVle(VersionElementName, ref version);
        return version;
    }

    public ArchiveException IoFailureException(string elementName) => new($"Unspecified I/O failure before '{CurrentBlockPath}/{elementName}'");
    public ArchiveException DuplicateElementException(string elementName) => new($"'{CurrentBlockPath}/{elementName}' is serialized several times");
    public ArchiveException ElementNotFoundException(string elementName) => new($"'{CurrentBlockPath}/{elementName}' is not found");
    public ArchiveException ElementNotFoundException(string elementName, int elementIndex) => new($"'{CurrentBlockPath}/{elementName}#{elementIndex}' is not found");
    public ArchiveException UnexpectedElementValueException(string elementName) => new($"'{CurrentBlockPath}/{elementName}' has unexpected type");
    public ArchiveException UnexpectedEofException(string elementName) => new($"Unexpected end of file before '{CurrentBlockPath}/{elementName}'");

    protected ArchiveBase(Context context)
    {
        _context = context;
    }

    public override void Dispose()
    {
        Debug.Assert(_delayedException is null, "Archive::Flush was not called while having delayed exception");
        base.Dispose();
    }

    protected const string RootBlockName = "Root";
    protected const string VersionElementName = "Version";

    protected void SetDelayedException(Exception exception)
    {
        if (_delayedException is null)
            _delayedException = exception;
    }

    protected void FlushDelayedException()
    {
        if (_delayedException is null)
            return;

        var exception = _delayedException;
        _delayedException = null!;
        throw exception;
    }

    protected void CheckIfNotEof(string elementName)
    {
        if (IsEof)
            throw UnexpectedEofException(elementName);
    }

    protected void CheckBlockOrElementName(string elementName)
    {
        Debug.Assert(ValidateName(elementName));
    }

    protected void CloseArchive()
    {
        _isEof = true;
    }

    protected void ReadBytesFromHexString(string elementName, string value, Span<byte> bytes)
    {
        byte[] decoded;
        try
        {
            decoded = Convert.FromHexString(value);
        }
        catch (FormatException)
        {
            throw UnexpectedElementValueException(elementName);
        }

        if (decoded.Length != bytes.Length)
            throw UnexpectedElementValueException(elementName);

        decoded.CopyTo(bytes);
    }

    private readonly Context _context;
    private Exception? _delayedException;
    private bool _isEof;
}

public class ArchiveBlockBase
{
    public ArchiveBlockBase(string name, ArchiveBlockType type)
    {
        _name = name;
        _type = type;
    }

    public string Name => _name;
    public ArchiveBlockType Type => _type;

    public void OpenInlineBlock()
    {
        ++_inlineBlockDepth;
    }

    public void CloseInlineBlock()
    {
        Debug.Assert(_inlineBlockDepth > 0);
        --_inlineBlockDepth;
    }

    public bool HasOpenInlineBlock => _inlineBlockDepth != 0;

    protected readonly string _name;
    protected readonly ArchiveBlockType _type;
    protected int _inlineBlockDepth;
}
