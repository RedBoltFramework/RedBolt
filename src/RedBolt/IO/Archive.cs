namespace RedBolt;

public enum ArchiveBlockType
{
    Sequential,
    Unordered,
    Array
}

public sealed class ArchiveBlock : IDisposable
{
    private Archive _archive = null!;

    public ArchiveBlock()
    {
    }

    public ArchiveBlock(Archive archive, uint sizeHint = 0)
    {
        _archive = archive;
        SizeHint = sizeHint;
    }

    public bool IsValid => _archive is not null;

    public uint SizeHint { get; }

    public void Dispose()
    {
        if (_archive is null)
            return;

        var archive = _archive;
        _archive = null!;
        archive.EndBlock();
    }
}

public abstract class Archive
{
    public abstract Context Context { get; }
    public abstract string Name { get; }
    public abstract uint Checksum { get; }
    public abstract bool IsInput { get; }
    public abstract bool IsHumanReadable { get; }
    public abstract bool IsUnorderedAccessSupportedInCurrentBlock { get; }
    public abstract bool IsEof { get; }
    public abstract string CurrentBlockPath { get; }

    public abstract bool HasElementOrBlock(string name);
    public abstract void BeginBlock(string name, ref uint sizeHint, bool safe, ArchiveBlockType type);
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
        if (string.IsNullOrEmpty(name))
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

    public ArchiveBlock OpenBlock(string name, uint sizeHint, bool safe, ArchiveBlockType type)
    {
        BeginBlock(name, ref sizeHint, safe, type);
        return new ArchiveBlock(this, sizeHint);
    }

    public ArchiveBlock OpenSequentialBlock(string name) => OpenBlock(name, 0, false, ArchiveBlockType.Sequential);
    public ArchiveBlock OpenUnorderedBlock(string name) => OpenBlock(name, 0, false, ArchiveBlockType.Unordered);
    public ArchiveBlock OpenArrayBlock(string name, uint sizeHint = 0) => OpenBlock(name, sizeHint, false, ArchiveBlockType.Array);
    public ArchiveBlock OpenSafeSequentialBlock(string name) => OpenBlock(name, 0, true, ArchiveBlockType.Sequential);
    public ArchiveBlock OpenSafeUnorderedBlock(string name) => OpenBlock(name, 0, true, ArchiveBlockType.Unordered);
}

public abstract class ArchiveBase : Archive
{
    private Exception _delayedException = null!;
    private bool _isEof;

    protected ArchiveBase(Context context)
    {
        Context = context;
    }

    public sealed override Context Context { get; }
    public override string Name => string.Empty;
    public override uint Checksum => 0;
    public sealed override bool IsEof => _isEof;

    public override uint SerializeVersion(uint version)
    {
        SerializeVle("Version", ref version);
        return version;
    }

    public sealed override void Flush()
    {
        FlushDelayedException();
    }

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
        if (!ValidateName(elementName))
            throw new ArgumentException($"Invalid archive element name '{elementName}'.", nameof(elementName));
    }

    protected void CloseArchive()
    {
        _isEof = true;
    }

    protected InvalidOperationException IoFailureException(string elementName) => new($"Unspecified I/O failure before '{CurrentBlockPath}/{elementName}'.");
    protected InvalidOperationException DuplicateElementException(string elementName) => new($"'{CurrentBlockPath}/{elementName}' is serialized several times.");
    protected InvalidOperationException ElementNotFoundException(string elementName) => new($"'{CurrentBlockPath}/{elementName}' is not found.");
    protected InvalidOperationException ElementNotFoundException(string elementName, uint elementIndex) => new($"'{CurrentBlockPath}/{elementName}#{elementIndex}' is not found.");
    protected InvalidOperationException UnexpectedElementValueException(string elementName) => new($"'{CurrentBlockPath}/{elementName}' has unexpected type.");
    protected InvalidOperationException UnexpectedEofException(string elementName) => new($"Unexpected end of file before '{CurrentBlockPath}/{elementName}'.");
}

public class ArchiveBlockBase
{
    private uint _inlineBlockDepth;

    public ArchiveBlockBase(string name, ArchiveBlockType type)
    {
        Name = name;
        Type = type;
    }

    public string Name { get; }
    public ArchiveBlockType Type { get; }
    public bool HasOpenInlineBlock => _inlineBlockDepth != 0;

    public void OpenInlineBlock()
    {
        ++_inlineBlockDepth;
    }

    public void CloseInlineBlock()
    {
        if (_inlineBlockDepth == 0)
            throw new InvalidOperationException("No inline archive block is open.");
        --_inlineBlockDepth;
    }
}
