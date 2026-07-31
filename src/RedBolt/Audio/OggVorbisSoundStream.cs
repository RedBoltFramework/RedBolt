namespace RedBolt.Audio;

using System.Diagnostics;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Stb;

/// <summary>Ogg Vorbis sound stream for compressed playback.</summary>
public class OggVorbisSoundStream : SoundStream, IDisposable
{
    protected readonly sbyte[] _data;
    protected readonly uint _dataSize;
    protected StbVorbis? _decoder;
    private GCHandle _dataHandle;

    public OggVorbisSoundStream(Sound sound)
    {
        ArgumentNullException.ThrowIfNull(sound);
        Debug.Assert(sound.IsCompressed());
        SetFormat(sound.GetIntFrequency(), sound.IsSixteenBit(), sound.IsStereo());
        SetStopAtEnd(!sound.IsLooped());

        _data = sound.GetData() ?? [];
        _dataSize = (uint)_data.Length;

        _dataHandle = GCHandle.Alloc(_data, GCHandleType.Pinned);
        _decoder = StbVorbisNativeBindings.StbVorbisOpenMemory(
            _dataHandle.AddrOfPinnedObject(), checked((int)_dataSize), out _, null!);
    }

    public void Dispose()
    {
        if (_decoder is { IsNull: false })
        {
            StbVorbisNativeBindings.StbVorbisClose(_decoder);
            _decoder = null;
        }

        if (_dataHandle.IsAllocated)
            _dataHandle.Free();

        GC.SuppressFinalize(this);
    }

    public override bool Seek(uint sampleNumber)
    {
        if (_decoder is null || _decoder.IsNull)
            return false;

        return StbVorbisNativeBindings.StbVorbisSeek(_decoder, sampleNumber) == 1;
    }

    public override uint GetData(sbyte[] dest, uint numBytes)
    {
        if (_decoder is null || _decoder.IsNull)
            return 0;

        var channels = Stereo ? 2u : 1u;
        var output = MemoryMarshal.Cast<sbyte, short>(dest.AsSpan());
        var outSamples = (uint)StbVorbisNativeBindings.StbVorbisGetSamplesShortInterleaved(
            _decoder, checked((int)channels), output, checked((int)(numBytes >> 1)));
        var outBytes = (outSamples * channels) << 1;

        if (outBytes < numBytes && !StopAtEnd)
        {
            numBytes -= outBytes;
            StbVorbisNativeBindings.StbVorbisSeekStart(_decoder);
            outSamples = (uint)StbVorbisNativeBindings.StbVorbisGetSamplesShortInterleaved(
                _decoder, checked((int)channels), output[checked((int)(outBytes >> 1))..], checked((int)(numBytes >> 1)));
            outBytes += (outSamples * channels) << 1;
        }

        return outBytes;
    }
}
