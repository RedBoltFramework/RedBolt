using RedBolt.Core;

namespace RedBolt.IO;

/// <summary>Base64 output archive.</summary>
public class Base64OutputArchive : BinaryOutputArchive
{
    public Base64OutputArchive()
        : this(new VectorBuffer())
    {
    }

    private Base64OutputArchive(VectorBuffer buffer)
        : base(Context.Instance, buffer)
    {
        _buffer = buffer;
    }

    /// <summary>Return base64-encoded result.</summary>
    public string GetBase64()
    {
        return Convert.ToBase64String(_buffer.GetBuffer());
    }

    public override void Dispose()
    {
        base.Dispose();
        _buffer.Dispose();
    }

    private readonly VectorBuffer _buffer;
}

/// <summary>Base64 input archive.</summary>
public class Base64InputArchive : BinaryInputArchive
{
    public Base64InputArchive(string base64)
        : this(new VectorBuffer(Convert.FromBase64String(base64)))
    {
    }

    private Base64InputArchive(VectorBuffer buffer)
        : base(Context.Instance, buffer)
    {
        _buffer = buffer;
    }

    public override void Dispose()
    {
        base.Dispose();
        _buffer.Dispose();
    }

    private readonly VectorBuffer _buffer;
}
