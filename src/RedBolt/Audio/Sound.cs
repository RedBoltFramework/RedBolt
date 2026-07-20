using System.Runtime.InteropServices;
using RedBolt.Bindings.Stb;

namespace RedBolt;

/// <summary>Sound resource.</summary>
public sealed class Sound : ResourceWithMetadata
{
    private sbyte[]? _data;
    private int _repeat;
    private int _end;
    private uint _dataSize;
    private uint _frequency = 44100;
    private bool _looped;
    private bool _sixteenBit;
    private bool _stereo;
    private bool _compressed;
    private float _compressedLength;

    public Sound()
        : base(string.Empty)
    {
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var name = Name;
        var ext = Path.GetExtension(name).ToLowerInvariant();
        bool success = ext switch
        {
            ".ogg" => LoadOggVorbis(source),
            ".wav" => LoadWav(source),
            _ => LoadRaw(source)
        };

        // Load optional parameters.
        if (success)
            LoadParameters();
        return success;
    }

    public bool LoadRaw(Stream source)
    {
        var dataSize = (uint)source.Length;
        SetSize(dataSize);
        var buffer = new byte[dataSize];
        source.ReadExactly(buffer, 0, (int)dataSize);
        _data = CopyToSByteArray(buffer);
        return true;
    }

    public bool LoadWav(Stream source)
    {
        var reader = new BinaryReader(source);
        var riffText = new string(reader.ReadChars(4));
        if (riffText != "RIFF") return false;
        reader.ReadUInt32(); // totalLength
        var waveText = new string(reader.ReadChars(4));
        if (waveText != "WAVE") return false;

        for (;;)
        {
            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadUInt32();
            if (chunkId == "fmt ")
            {
                var format = reader.ReadUInt16();
                var channels = reader.ReadUInt16();
                var frequency = reader.ReadUInt32();
                reader.ReadUInt32(); // avgBytes
                reader.ReadUInt16(); // blockAlign
                var bits = reader.ReadUInt16();
                if (format != 1) return false;

                // skip extra fmt data
                var pos = source.Position;
                var remaining = (int)(chunkSize - 16);
                if (remaining > 0) source.Seek(pos + remaining, SeekOrigin.Begin);

                // find data chunk
                while (source.Position < source.Length)
                {
                    var dataId = new string(reader.ReadChars(4));
                    var dataLen = reader.ReadUInt32();
                    if (dataId == "data")
                    {
                        SetSize(dataLen);
                        SetFormat(frequency, bits == 16, channels == 2);
                        var rawData = reader.ReadBytes((int)dataLen);
                        _data = CopyToSByteArray(rawData);
                        if (bits != 16)
                        {
                            for (var i = 0; i < dataLen; i++)
                                _data[i] = unchecked((sbyte)((byte)_data[i] - 128));
                        }
                        return true;
                    }
                    // skip unknown chunk
                    source.Seek(source.Position + dataLen, SeekOrigin.Begin);
                }
                return false;
            }
            // skip unknown chunk
            source.Seek(source.Position + chunkSize, SeekOrigin.Begin);
            if (chunkSize == 0 || source.Position >= source.Length)
                return false;
        }
    }

    public bool LoadOggVorbis(Stream source)
    {
        var dataSize = (uint)source.Length;
        var data = new byte[dataSize];
        source.ReadExactly(data, 0, (int)dataSize);

        var compressedData = CopyToSByteArray(data);
        var handle = GCHandle.Alloc(compressedData, GCHandleType.Pinned);
        StbVorbis? decoder;
        try
        {
            decoder = StbVorbisNativeBindings.StbVorbisOpenMemory(
                handle.AddrOfPinnedObject(), checked((int)dataSize), out _, null!);
        }
        finally
        {
            handle.Free();
        }

        if (decoder.IsNull)
            return false;

        try
        {
            var info = StbVorbisNativeBindings.StbVorbisGetInfo(decoder);
            _compressedLength = StbVorbisNativeBindings.StbVorbisStreamLengthInSeconds(decoder);
            _frequency = info.SampleRate;
            _stereo = info.Channels > 1;
        }
        finally
        {
            StbVorbisNativeBindings.StbVorbisClose(decoder);
        }

        _data = compressedData;
        _dataSize = dataSize;
        _sixteenBit = true;
        _compressed = true;

        SetMemoryUse(checked((int)dataSize));
        return true;
    }

    public void SetSize(uint dataSize)
    {
        if (dataSize == 0) return;
        _data = new sbyte[dataSize + 4]; // +4 for IP_SAFETY
        _dataSize = dataSize;
        _compressed = false;
        SetLooped(false);
        SetMemoryUse(checked((int)dataSize + 4));
    }

    public void SetData(byte[] data, uint dataSize)
    {
        if (dataSize == 0) return;
        SetSize(dataSize);
        Array.Copy(data, _data!, dataSize);
    }

    internal void CopyData(sbyte[] source, int sourceOffset, int destinationOffset, int dataSize)
    {
        if (_data == null || dataSize <= 0)
            return;
        Buffer.BlockCopy(source, sourceOffset, _data, destinationOffset, dataSize);
    }

    internal void ClearData(int offset, int dataSize)
    {
        if (_data == null || dataSize <= 0)
            return;
        Array.Clear(_data, offset, dataSize);
    }

    public void SetFormat(uint frequency, bool sixteenBit, bool stereo)
    {
        _frequency = frequency;
        _sixteenBit = sixteenBit;
        _stereo = stereo;
        _compressed = false;
    }

    public void SetLooped(bool enable)
    {
        if (enable)
            SetLoop(0, _dataSize);
        else
        {
            if (!_compressed)
            {
                _end = (int)_dataSize;
                _looped = false;
                FixInterpolation();
            }
            else
                _looped = false;
        }
    }

    public void SetLoop(uint repeatOffset, uint endOffset)
    {
        if (_compressed) { _looped = true; return; }

        if (repeatOffset > _dataSize) repeatOffset = _dataSize;
        if (endOffset > _dataSize) endOffset = _dataSize;

        var sampleSize = (int)GetSampleSize();
        repeatOffset = (uint)((int)repeatOffset & -sampleSize);
        endOffset = (uint)((int)endOffset & -sampleSize);

        _repeat = (int)repeatOffset;
        _end = (int)endOffset;
        _looped = true;
        FixInterpolation();
    }

    public SoundStream? GetDecoderStream() => _compressed ? new OggVorbisSoundStream(this) : null;

    public sbyte[]? GetData() => _data;
    public ref sbyte GetStart() => ref _data![0];
    public ref sbyte GetRepeat() => ref _data![_repeat];
    public int GetRepeatOffset() => _repeat;
    public int GetEndOffset() => _end;

    public float GetLength()
    {
        if (!_compressed)
        {
            if (_frequency == 0) return 0.0f;
            return _dataSize / (float)GetSampleSize() / _frequency;
        }
        return _compressedLength;
    }

    public uint GetDataSize() => _dataSize;
    public uint GetSampleSize()
    {
        uint size = 1;
        if (_sixteenBit) size <<= 1;
        if (_stereo) size <<= 1;
        return size;
    }
    public float GetFrequency() => _frequency;
    public uint GetIntFrequency() => _frequency;
    public bool IsLooped() => _looped;
    public bool IsSixteenBit() => _sixteenBit;
    public bool IsStereo() => _stereo;
    public bool IsCompressed() => _compressed;

    public void FixInterpolation()
    {
        if (_data == null || _compressed) return;
        if (_looped && _repeat < _data.Length && _end < _data.Length)
        {
            for (var i = 0; i < 4; i++)
                _data[_end + i] = _data[_repeat + i];
        }
        else if (_end + 4 <= _data.Length)
        {
            for (var i = 0; i < 4; i++)
                _data[_end + i] = 0;
        }
    }

    private void LoadParameters()
    {
        var cache = GetSubsystem<ResourceCache>();
        var xmlName = Path.ChangeExtension(GetName(), ".xml");
        var file = cache.GetTempResource<XMLFile>(xmlName, false);
        if (file is null)
            return;

        var rootElement = file.GetRoot();
        LoadMetadataFromXML(rootElement);

        for (var parameterElement = rootElement.GetChild(); parameterElement.NotNull; parameterElement = parameterElement.GetNext())
        {
            var name = parameterElement.Name;

            if (name == "format" && !_compressed)
            {
                if (parameterElement.HasAttribute("frequency"))
                    _frequency = (uint)parameterElement.GetInt("frequency");
                if (parameterElement.HasAttribute("sixteenbit"))
                    _sixteenBit = parameterElement.GetBool("sixteenbit");
                if (parameterElement.HasAttribute("16bit"))
                    _sixteenBit = parameterElement.GetBool("16bit");
                if (parameterElement.HasAttribute("stereo"))
                    _stereo = parameterElement.GetBool("stereo");
            }

            if (name == "loop")
            {
                if (parameterElement.HasAttribute("enable"))
                    SetLooped(parameterElement.GetBool("enable"));
                if (parameterElement.HasAttribute("start") && parameterElement.HasAttribute("end"))
                    SetLoop((uint)parameterElement.GetInt("start"), (uint)parameterElement.GetInt("end"));
            }
        }
    }

    internal static sbyte[] CopyToSByteArray(byte[] source)
    {
        var result = new sbyte[source.Length];
        Buffer.BlockCopy(source, 0, result, 0, source.Length);
        return result;
    }
}
