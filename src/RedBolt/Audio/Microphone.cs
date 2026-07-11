namespace RedBolt;

/// <summary>Microphone for audio recording.</summary>
public sealed class Microphone : Object
{
    private BufferedSoundStream? _linkedStream;
    private string _name = string.Empty;
    private readonly List<byte> _buffer = [];
    private uint _frequency = 44100;
    private bool _enabled;

    public Microphone()
    {
    }

    /// <summary>Get recorded data.</summary>
    public byte[] GetData() => _buffer.ToArray();

    /// <summary>Copy recorded data into destination.</summary>
    public uint CopyData(byte[] dest, uint maxBytes)
    {
        var count = Math.Min((uint)_buffer.Count, maxBytes);
        Array.Copy(_buffer.ToArray(), 0, dest, 0, count);
        return count;
    }

    /// <summary>Clear recorded data.</summary>
    public void ClearData() => _buffer.Clear();

    /// <summary>Update microphone.</summary>
    public void Update(float timeStep) { /* TODO: port SDL microphone capture */ }

    public uint GetFrequency() => _frequency;
    public string GetName() => _name;
    public bool IsEnabled() => _enabled;

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (enabled)
            ClearData();
    }

    public BufferedSoundStream? GetLinked() => _linkedStream;
    public void Link(BufferedSoundStream? stream) => _linkedStream = stream;
    public void Unlink() => _linkedStream = null;
}
