namespace RedBolt;

// C++: enum FileMode { FILE_READ = 0, FILE_WRITE, FILE_READWRITE }; — defined in AbstractFile.h, C# moves it to File.cs
// C++: class URHO3D_API AbstractFile : public Deserializer, public Serializer
// C# cannot express multiple class inheritance; Serializer is not in the base list.
// C++: using AbstractFilePtr = SharedPtr<AbstractFile, RefCounted>;
public abstract class AbstractFile : Deserializer
{
    // C++: AbstractFile() : Deserializer() { }
    protected AbstractFile()
    {
    }

    // C++: explicit AbstractFile(unsigned int size) : Deserializer(size) { }
    protected AbstractFile(uint size)
        : base(size)
    {
    }

    // C++: ~AbstractFile() override = default;
    // C# desctructor handled by GC; no Dispose needed on this abstract base.

    // C++: const ea::string& GetName() const override { return name_; } — SWIG workaround
    public override string Name => _name;

    // C++: virtual const ea::string& GetAbsoluteName() const { return name_; }
    public virtual string AbsoluteName => _name;

    // C++: virtual bool IsOpen() const { return true; }
    public virtual bool IsOpen => true;

    // C++: ea::string name_;
    protected string _name = string.Empty;

    // C++: virtual void SetName(const ea::string& name) { name_ = name; }
    public virtual void SetName(string name)
    {
        _name = name ?? string.Empty;
    }

    // C++: virtual void Close() {}
    public virtual void Close()
    {
    }
}
