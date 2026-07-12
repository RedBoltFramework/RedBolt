using System.Runtime.InteropServices;
using RedBolt.Bindings.Stb;

namespace RedBolt;

/// <summary>Ogg Vorbis sound stream for compressed playback.</summary>
public sealed class OggVorbisSoundStream : SoundStream, IDisposable
{
    private readonly sbyte[] _data;
    private readonly uint _dataSize;
    private StbVorbis? _decoder;

    public OggVorbisSoundStream(Sound sound)
    {
        ArgumentNullException.ThrowIfNull(sound);
        SetFormat(sound.GetIntFrequency(), sound.IsSixteenBit(), sound.IsStereo());
        SetStopAtEnd(!sound.IsLooped());

        _data = sound.GetData() ?? [];
        _dataSize = (uint)_data.Length;

        var handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
        try
        {
            _decoder = StbVorbisNativeBindings.StbVorbisOpenMemory(
                handle.AddrOfPinnedObject(), checked((int)_dataSize), out _, null!);
        }
        finally
        {
            handle.Free();
        }
    }

    public void Dispose()
    {
        if (_decoder is { IsNull: false })
        {
            StbVorbisNativeBindings.StbVorbisClose(_decoder);
            _decoder = null;
        }

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
        var handle = GCHandle.Alloc(dest, GCHandleType.Pinned);
        try
        {
            var output = handle.AddrOfPinnedObject();
            var outSamples = (uint)StbVorbisNativeBindings.StbVorbisGetSamplesShortInterleaved(
                _decoder, checked((int)channels), output, checked((int)(numBytes >> 1)));
            var outBytes = (outSamples * channels) << 1;

            if (outBytes < numBytes && !StopAtEnd)
            {
                numBytes -= outBytes;
                StbVorbisNativeBindings.StbVorbisSeekStart(_decoder);
                outSamples = (uint)StbVorbisNativeBindings.StbVorbisGetSamplesShortInterleaved(
                    _decoder, checked((int)channels), IntPtr.Add(output, checked((int)outBytes)), checked((int)(numBytes >> 1)));
                outBytes += (outSamples * channels) << 1;
            }

            return outBytes;
        }
        finally
        {
            handle.Free();
        }
    }
}
