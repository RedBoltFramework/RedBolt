namespace RedBolt;

public abstract class AbstractFile : Deserializer
{
    protected AbstractFile()
    {
    }

    protected AbstractFile(uint size)
        : base(size)
    {
    }

    public override string Name => _name;

    public virtual string AbsoluteName => _name;

    public virtual bool IsOpen => true;

    protected string _name = string.Empty;

    public virtual void SetName(string name)
    {
        _name = name ?? string.Empty;
    }

    public virtual void Close()
    {
    }
}
