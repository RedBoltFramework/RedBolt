namespace RedBolt;

/// <summary>Ogg Vorbis sound stream for compressed playback.</summary>
public sealed class OggVorbisSoundStream : SoundStream
{
    private readonly sbyte[] _data;
    private readonly uint _dataSize;
    private uint _decodePosition;

    public OggVorbisSoundStream(Sound sound)
    {
        ArgumentNullException.ThrowIfNull(sound);
        _data = sound.GetData() ?? [];
        _dataSize = (uint)_data.Length;
        Frequency = sound.GetIntFrequency();
        SixteenBit = sound.IsSixteenBit();
        Stereo = sound.IsStereo();
    }

    public override bool Seek(uint sampleNumber) { _decodePosition = sampleNumber; return true; }

    public override uint GetData(sbyte[] dest, uint numBytes)
    {
        // TODO: port actual Ogg Vorbis decoding via managed library
        // For now, returns silence
        Array.Clear(dest, 0, (int)numBytes);
        return numBytes;
    }
}
