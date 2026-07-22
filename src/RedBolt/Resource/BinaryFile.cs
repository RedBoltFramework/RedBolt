using System.Text;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Resource for generic binary file.</summary>
public class BinaryFile : Resource
{
    /// <summary>Construct empty.</summary>
    public BinaryFile()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _buffer.Dispose();
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AddFactoryReflection<BinaryFile>();
    }

    /// <summary>Load resource from stream. May be called from a worker thread. Return true if successful.</summary>
    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Seek(0, SeekOrigin.Begin);
        using var memory = new MemoryStream();
        source.CopyTo(memory);
        _buffer.SetData(memory.ToArray());
        SetMemoryUse(_buffer.Buffer.Length);
        return true;
    }

    /// <summary>Save resource to a stream.</summary>
    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        try
        {
            destination.Write(_buffer.Buffer);
            return true;
        }
        catch (Exception exception)
        {
            Log.GetLogger().LogError(exception, "Can not save binary file {Name}", Name);
            return false;
        }
    }

    /// <summary>Save object using Archive serialization.</summary>
    public bool SaveObjectCallback(Action<Archive> serializeValue)
    {
        ArgumentNullException.ThrowIfNull(serializeValue);
        try
        {
            _buffer.Clear();
            using var archive = new BinaryOutputArchive(Context.Instance, AsSerializer());
            serializeValue(archive);
            return true;
        }
        catch
        {
            _buffer.Clear();
            return false;
        }
    }

    /// <summary>Load object using Archive serialization.</summary>
    public bool LoadObjectCallback(Action<Archive> serializeValue)
    {
        ArgumentNullException.ThrowIfNull(serializeValue);
        try
        {
            using var readBuffer = new MemoryBuffer(_buffer.GetBuffer());
            using var archive = new BinaryInputArchive(Context.Instance, readBuffer);
            serializeValue(archive);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Save object using Archive serialization.</summary>
    public bool SaveObject(Object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return SaveObjectCallback(value.SerializeInBlock);
    }

    /// <summary>Load object using Archive serialization.</summary>
    public bool LoadObject(Object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return LoadObjectCallback(value.SerializeInBlock);
    }

    /// <summary>Clear data.</summary>
    public void Clear()
    {
        _buffer.Clear();
    }

    /// <summary>Set data.</summary>
    public void SetData(IEnumerable<byte> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _buffer.SetData(value.ToArray());
        SetMemoryUse(_buffer.Buffer.Length);
    }

    /// <summary>Set data from text.</summary>
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _buffer.SetData(Encoding.UTF8.GetBytes(text));
        SetMemoryUse(_buffer.Buffer.Length);
    }

    /// <summary>Return immutable data.</summary>
    public IReadOnlyList<byte> GetData() => _buffer.GetBuffer();

    /// <summary>Return immutable data as string.</summary>
    public string GetText()
    {
        return Encoding.UTF8.GetString(_buffer.Buffer);
    }

    /// <summary>Return data as text lines.</summary>
    public string[] ReadLines()
    {
        var result = new List<string>();
        using var readBuffer = new MemoryBuffer(_buffer.GetBuffer());
        while (!readBuffer.IsEof())
            result.Add(readBuffer.ReadLine());
        return result.ToArray();
    }

    /// <summary>Return mutable internal buffer.</summary>
    public VectorBuffer GetMutableBuffer() => _buffer;

    /// <summary>Cast to Serializer.</summary>
    public Serializer AsSerializer() => _buffer;

    /// <summary>Cast to Deserializer.</summary>
    public Deserializer AsDeserializer() => _buffer;

    private readonly VectorBuffer _buffer = new();
}
