namespace RedBolt;

/// <summary>A common root class for objects that implement both Serializer and Deserializer.</summary>
public abstract class AbstractFile : Deserializer, IDisposable
{
    /// <summary>Construct.</summary>
    public AbstractFile()
    {
    }

    /// <summary>Construct.</summary>
    public AbstractFile(uint size)
        : base(size)
    {
    }

    /// <summary>Destruct.</summary>
    public virtual void Dispose()
    {
    }

    /// <summary>Change the file name. Used by the resource system.</summary>
    public virtual void SetName(string name)
    {
        _name = name;
    }

    /// <summary>Return whether is open.</summary>
    public virtual bool IsOpen => true;

    /// <summary>Return absolute file name in file system.</summary>
    public virtual string AbsoluteName => _name;

    /// <summary>Close the file.</summary>
    public virtual void Close()
    {
    }

    /// <summary>Return the file name.</summary>
    public override string Name => _name;

    /// <summary>Return the file name.</summary>
    public override string GetName() => _name;

    /// <summary>File name.</summary>
    protected string _name = string.Empty;
}
