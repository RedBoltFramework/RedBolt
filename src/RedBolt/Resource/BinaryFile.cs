using System.Text;

namespace RedBolt;

public sealed class BinaryFile : Resource
{
    private byte[] _data = [];

    public BinaryFile(string name)
        : base(name)
    {
    }

    public IReadOnlyList<byte> Data => _data;

    public void Clear()
    {
        _data = [];
        MemoryUse = 0;
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var memory = new MemoryStream();
        source.CopyTo(memory);
        SetData(memory.ToArray());
        return true;
    }

    public string GetText()
    {
        return Encoding.UTF8.GetString(_data);
    }

    public string[] ReadLines()
    {
        return GetText().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(_data);
        return true;
    }

    public void SetData(IEnumerable<byte> value)
    {
        _data = value.ToArray();
        MemoryUse = (uint)_data.Length;
    }

    public void SetText(string text)
    {
        _data = Encoding.UTF8.GetBytes(text);
        MemoryUse = (uint)_data.Length;
    }
}
