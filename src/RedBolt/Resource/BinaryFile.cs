using System.Text;

namespace RedBolt;

public sealed class BinaryFile : Resource
{
    private byte[] data = [];

    public BinaryFile(string name)
        : base(name)
    {
    }

    public IReadOnlyList<byte> Data => data;

    public void Clear()
    {
        data = [];
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
        return Encoding.UTF8.GetString(data);
    }

    public string[] ReadLines()
    {
        return GetText().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(data);
        return true;
    }

    public void SetData(IEnumerable<byte> value)
    {
        data = value.ToArray();
        MemoryUse = (uint)data.Length;
    }

    public void SetText(string text)
    {
        data = Encoding.UTF8.GetBytes(text);
        MemoryUse = (uint)data.Length;
    }
}
