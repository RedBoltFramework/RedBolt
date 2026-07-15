using System.Text;

namespace RedBolt;

public sealed class BinaryFile : Resource
{
    private readonly VectorBuffer _buffer = new();

    public BinaryFile(string name = "")
        : base(name)
    {
    }

    public IReadOnlyList<byte> Data => _buffer.GetBuffer();

    public static void RegisterObject(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Seek(0, SeekOrigin.Begin);
        using var memory = new MemoryStream();
        source.CopyTo(memory);
        _buffer.SetData(memory.ToArray());
        SetMemoryUse((uint)_buffer.Buffer.Length);
        return true;
    }

    // C++: checks dest.Write(buffer_.GetData(), buffer_.GetSize()) != buffer_.GetSize() → URHO3D_LOGERROR("Can not save binary file") & returns false.
    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(_buffer.Buffer);
        return true;
    }

    public bool SaveObjectCallback(Action<Archive> serializeValue)
    {
        ArgumentNullException.ThrowIfNull(serializeValue);
        try
        {
            _buffer.Clear();
            var archive = new BinaryOutputArchive(Context.Instance, AsSerializer());
            serializeValue(archive);
            return true;
        }
        catch
        {
            _buffer.Clear();
            return false;
        }
    }

    public bool LoadObjectCallback(Action<Archive> serializeValue)
    {
        ArgumentNullException.ThrowIfNull(serializeValue);
        try
        {
            var readBuffer = new MemoryBuffer(_buffer.GetBuffer());
            var archive = new BinaryInputArchive(Context.Instance, readBuffer);
            serializeValue(archive);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool SaveObject(Object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return SaveObjectCallback(value.SerializeInBlock);
    }

    public bool LoadObject(Object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return LoadObjectCallback(value.SerializeInBlock);
    }

    public void Clear()
    {
        _buffer.Clear();
    }

    public void SetData(IEnumerable<byte> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _buffer.SetData(value.ToArray());
        SetMemoryUse((uint)_buffer.Buffer.Length);
    }

    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _buffer.SetData(Encoding.UTF8.GetBytes(text));
        SetMemoryUse((uint)_buffer.Buffer.Length);
    }

    public IReadOnlyList<byte> GetData() => _buffer.GetBuffer();

    public string GetText()
    {
        return Encoding.UTF8.GetString(_buffer.Buffer);
    }

    public string[] ReadLines()
    {
        var result = new List<string>();
        var readBuffer = new MemoryBuffer(_buffer.GetBuffer());
        while (!readBuffer.IsEof())
            result.Add(readBuffer.ReadLine());
        return result.ToArray();
    }

    public VectorBuffer GetMutableBuffer() => _buffer;

    public Serializer AsSerializer() => _buffer;

    public Deserializer AsDeserializer() => _buffer;
}
