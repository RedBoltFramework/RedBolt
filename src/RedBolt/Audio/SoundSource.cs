namespace RedBolt;

/// <summary>Compressed audio decode buffer length in milliseconds.</summary>
public static class SoundSourceConstants
{
    public const int StreamBufferLength = 100;
}

/// <summary>Sound source component with stereo position.</summary>
public class SoundSource : Component
{
    protected Audio Audio = null!;
    protected string SoundType = AudioDefs.SoundEffect;
    internal StringHash SoundTypeHash = new(AudioDefs.SoundEffect);
    internal float Frequency;
    protected float Gain = 1.0f;
    protected float Attenuation = 1.0f;
    protected float Panning;
    protected float Reach;
    protected float MasterGain;
    protected AutoRemoveMode AutoRemove;

    private Sound? _sound;
    private SoundStream? _soundStream;
    private int? _position;
    private int _fractPosition;
    private float _timePosition;
    private Sound? _streamBuffer;
    private int _unusedStreamSize;
    private bool _ignoreSceneTimeScale;
    private bool _sendFinishedEvent;
    private bool _lowFrequency;
    private readonly object _playLock = new();

    public SoundSource()
    {
        Audio = GetSubsystem<Audio>()!;
        Audio.AddSoundSource(this);
        UpdateMasterGain();
    }

    public override void Dispose()
    {
        Audio.RemoveSoundSource(this);
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Play a sound.</summary>
    public void Play(Sound? sound)
    {
        if (Frequency == 0.0f && sound != null)
            Frequency = sound.GetFrequency();
        PlayCore(sound);
    }

    /// <summary>Play a sound with specified frequency.</summary>
    public void Play(Sound? sound, float frequency)
    {
        SetFrequency(frequency);
        PlayCore(sound);
    }

    /// <summary>Play a sound with specified frequency and gain.</summary>
    public void Play(Sound? sound, float frequency, float gain)
    {
        SetFrequency(frequency);
        SetGain(gain);
        PlayCore(sound);
    }

    /// <summary>Play a sound with specified frequency, gain and panning.</summary>
    public void Play(Sound? sound, float frequency, float gain, float panning)
    {
        SetFrequency(frequency);
        SetGain(gain);
        SetPanning(panning);
        PlayCore(sound);
    }

    private void PlayCore(Sound? sound)
    {
        lock (_playLock)
            PlayLockless(sound);
    }

    /// <summary>Start playing a sound stream.</summary>
    public void Play(SoundStream? stream)
    {
        if (stream == null) return;
        lock (_playLock)
        {
            _sound = null;
            if (Frequency == 0.0f)
                Frequency = stream.GetFrequency();
            PlayLockless(stream);
        }
    }

    /// <summary>Stop playback.</summary>
    public void Stop()
    {
        lock (_playLock)
            StopLockless();
    }

    public void Seek(float seekTime)
    {
        if (_sound == null || (_soundStream != null && !_sound.IsCompressed()))
            return;

        seekTime = MathDefs.Clamp(seekTime, 0.0f, _sound.GetLength());

        if (_soundStream == null)
        {
            SetPlayPosition((int)(seekTime * (_sound.GetSampleSize() * _sound.GetFrequency())));
        }
        else
        {
            if (_soundStream.Seek((uint)(seekTime * _soundStream.GetIntFrequency())))
                _timePosition = seekTime;
        }
    }

    public void SetSoundType(string type)
    {
        if (type == AudioDefs.SoundMaster)
            return;
        SoundType = type;
        SoundTypeHash = new StringHash(type);
        UpdateMasterGain();
    }

    public void SetFrequency(float frequency) => Frequency = MathDefs.Clamp(frequency, 0.0f, 535232.0f);
    public void SetGain(float gain) { Gain = Math.Max(gain, 0.0f); }
    public void SetAttenuation(float attenuation) => Attenuation = MathDefs.Clamp(attenuation, 0.0f, 1.0f);
    public void SetPanning(float panning) { Panning = MathDefs.Clamp(panning, -1.0f, 1.0f); }
    public void SetReach(float reach) { Reach = MathDefs.Clamp(reach, -1.0f, 1.0f); }
    public void SetLowFrequency(bool state) => _lowFrequency = state;
    public void SetAutoRemoveMode(AutoRemoveMode mode) => AutoRemove = mode;
    public void SetIgnoreSceneTimeScale(bool ignoreSceneTimeScale) => _ignoreSceneTimeScale = ignoreSceneTimeScale;

    public void SetSoundAttr(ResourceRef value)
    {
        var sound = ResourceRef.IsNullOrEmpty(value)
            ? null
            : GetSubsystem<ResourceCache>()!.GetResourceOrNull<Sound>(value.Name);

        if (IsPlaying())
        {
            Play(sound);
            return;
        }

        lock (_playLock)
        {
            _soundStream = null;
            _streamBuffer = null;
            _sound = sound;
        }
    }

    public Sound? GetSound() => _sound;
    public int GetPlayPosition() => _position ?? 0;
    public string GetSoundType() => SoundType;
    public float GetTimePosition() => _timePosition;
    public float GetFrequency() => Frequency;
    public float GetGain() => Gain;
    public float GetAttenuation() => Attenuation;
    public float GetPanning() => Panning;
    public float GetReach() => Reach;
    public bool IsLowFrequency() => _lowFrequency;
    public AutoRemoveMode GetAutoRemoveMode() => AutoRemove;
    public bool GetIgnoreSceneTimeScale() => _ignoreSceneTimeScale;
    public bool IsPlaying() => _position.HasValue && (_sound != null || _soundStream != null);

    public void SetPlayPosition(int position)
    {
        lock (_playLock)
            SetPlayPositionLockless(position);
    }

    public virtual void Update(float timeStep)
    {
        var effectiveTimeScale = GetEffectiveTimeScale();
        if (effectiveTimeScale == 0.0f)
            return;

        if (!Audio.IsInitialized())
            MixNull(timeStep, Frequency * effectiveTimeScale);

        lock (_playLock)
        {
            if (_soundStream != null && !_position.HasValue)
                StopLockless();
        }

        if (_position.HasValue || !_sendFinishedEvent || !IsAttached)
            return;

        _sendFinishedEvent = false;
        var eventData = new AudioEvents.SoundFinishedEvent
        {
            Node = Node,
            SoundSource = this,
            Sound = _sound
        };
        Node.SendEvent(ref eventData);
        DoAutoRemove(AutoRemove);
    }

    /// <summary>Mix sound source output to a 32-bit clipping buffer. Called by Audio.</summary>
    public void Mix(int[] dest, uint samples, int mixRate, SpeakerMode mode, bool interpolation)
    {
        lock (_playLock)
        {
            if (!_position.HasValue || (_sound == null && _soundStream == null))
                return;

            var effectiveTimeScale = GetEffectiveTimeScale();
            if (effectiveTimeScale == 0.0f)
                return;

            var stream = _soundStream;
            var effectiveFrequency = Frequency * effectiveTimeScale;
            var sound = _sound;
            var streamFilledSize = 0;
            var decoded = 0u;

            if (stream != null && _streamBuffer != null)
            {
                var streamBufferSize = (int)_streamBuffer.GetDataSize();
                var sampleSize = (int)stream.GetSampleSize();
                var neededSize = (int)(samples * effectiveFrequency / mixRate);
                neededSize = checked((neededSize + 4) * sampleSize);
                neededSize = Math.Clamp(neededSize - _unusedStreamSize, 0, streamBufferSize - _unusedStreamSize);

                _position = 0;
                var decodeBuffer = new sbyte[Math.Max(neededSize, 1)];
                if (neededSize != 0)
                    decoded = stream.GetData(decodeBuffer, (uint)neededSize);
                _streamBuffer.CopyData(decodeBuffer, 0, _unusedStreamSize, (int)decoded);
                if (decoded < neededSize)
                    _streamBuffer.ClearData(_unusedStreamSize + (int)decoded, neededSize - (int)decoded);
                streamFilledSize = neededSize + _unusedStreamSize;
                sound = _streamBuffer;
            }

            if (sound == null)
                return;

            MixSound(sound, dest, samples, mixRate, effectiveFrequency, mode, interpolation);

            if (stream != null && _streamBuffer != null)
            {
                _unusedStreamSize = Math.Max(streamFilledSize - (_position ?? 0), 0);
                if (_unusedStreamSize != 0)
                {
                    var data = _streamBuffer.GetData()!;
                    Buffer.BlockCopy(data, _position ?? 0, data, 0, _unusedStreamSize);
                }

                if (decoded == 0 && stream.GetStopAtEnd())
                {
                    _position = null;
                }
            }
        }
    }

    public void UpdateMasterGain()
    {
        MasterGain = Audio.GetSoundSourceMasterGain(SoundTypeHash);
    }

    /// <summary>Set sound attribute for serialization.</summary>
    public ResourceRef GetSoundAttr() => GetSound() != null ? new ResourceRef(new StringHash(nameof(Sound)), GetSound()!.Name) : ResourceRef.Empty;

    public void SetPlayingAttr(bool value)
    {
        if (value && !IsPlaying() && _sound != null)
            Play(_sound);
        else if (!value)
            Stop();
    }

    public int GetPositionAttr() => _position ?? 0;

    public void SetPositionAttr(int value) => SetPlayPosition(value);

    private void PlayLockless(Sound? sound)
    {
        _timePosition = 0.0f;

        if (sound != null)
        {
            if (!sound.IsCompressed())
            {
                var data = sound.GetData();
                if (data is { Length: > 0 })
                {
                    _soundStream = null;
                    _streamBuffer = null;
                    _sound = sound;
                    _position = 0;
                    _fractPosition = 0;
                    _sendFinishedEvent = true;
                    return;
                }
            }
            else
            {
                PlayLockless(sound.GetDecoderStream());
                _sound = sound;
                return;
            }
        }

        StopLockless();
        _sound = null;
    }

    private void PlayLockless(SoundStream? stream)
    {
        _timePosition = 0.0f;

        if (stream != null)
        {
            var sampleSize = stream.GetSampleSize();
            var streamBufferSize = checked(sampleSize * stream.GetIntFrequency() * SoundSourceConstants.StreamBufferLength / 1000u);
            _streamBuffer = new Sound();
            _streamBuffer.SetSize(streamBufferSize);
            _streamBuffer.SetFormat(stream.GetIntFrequency(), stream.IsSixteenBit(), stream.IsStereo());
            _streamBuffer.SetLooped(true);
            _soundStream = stream;
            _unusedStreamSize = 0;
            _position = 0;
            _fractPosition = 0;
            _sendFinishedEvent = true;
            return;
        }

        StopLockless();
    }

    private void StopLockless()
    {
        _position = null;
        _timePosition = 0.0f;
        _soundStream = null;
        _streamBuffer = null;
        _unusedStreamSize = 0;
    }

    private void SetPlayPositionLockless(int position)
    {
        if (_sound == null || _soundStream != null)
            return;

        _position = Math.Clamp(position, 0, (int)_sound.GetDataSize());
        if (_sound.IsSixteenBit() && (_position.Value & 1) != 0)
            _position++;
        if (_position.Value > (int)_sound.GetDataSize())
            _position = (int)_sound.GetDataSize();
        if (_sound.GetSampleSize() != 0 && _sound.GetFrequency() != 0)
            _timePosition = _position.Value / (float)(_sound.GetSampleSize() * _sound.GetFrequency());
    }

    private void MixNull(float timeStep, float effectiveFrequency)
    {
        lock (_playLock)
        {
            if (!_position.HasValue || _sound == null || !IsEnabledEffective)
                return;

            _timePosition += timeStep * effectiveFrequency / _sound.GetFrequency();
            if (_sound.IsLooped())
            {
                if (_timePosition >= _sound.GetLength())
                    _timePosition -= _sound.GetLength();
            }
            else if (_timePosition >= _sound.GetLength())
            {
                _position = null;
                _timePosition = 0.0f;
            }
        }
    }

    private float GetEffectiveTimeScale()
    {
        if (_ignoreSceneTimeScale)
            return 1.0f;
        var scene = Scene;
        if (!IsAttached || scene == null || !IsEnabledEffective)
            return 0.0f;
        return scene.GetEffectiveTimeScale();
    }

    // C++: has 13+ separate optimized mix methods (MixMonoToMono, MixMonoToStereo, MixMonoToMonoIP,
    // MixMonoToStereoIP, MixStereoToMono, MixStereoToStereo, MixStereoToMonoIP, MixStereoToStereoIP,
    // MixMonoToSurround, MixMonoToSurroundIP, MixStereoToMulti, MixStereoToMultiIP, MixZeroVolume)
    // The C# version combines them into a single MixSound method.
    private void MixSound(Sound sound, int[] dest, uint samples, int mixRate, float effectiveFrequency, SpeakerMode mode, bool interpolation)
    {
        var data = sound.GetData();
        if (data == null)
            return;

        var outputChannels = mode switch
        {
            SpeakerMode.Mono => 1,
            SpeakerMode.Stereo => 2,
            SpeakerMode.Quadrophonic => 4,
            SpeakerMode.Surround51 => 6,
            _ => 0
        };
        if (outputChannels == 0)
            return;

        var sampleSize = (int)sound.GetSampleSize();
        var channelSize = sound.IsSixteenBit() ? 2 : 1;
        var sourceChannels = sound.IsStereo() ? 2 : 1;
        var end = sound.GetEndOffset();
        var repeat = sound.GetRepeatOffset();
        var position = _position ?? 0;
        var fractPosition = _fractPosition;
        var add = effectiveFrequency / mixRate;
        var intAdd = (int)add;
        var fractAdd = (int)((add - intAdd) * 65536.0f);
        var gain = Gain * MasterGain * Attenuation;
        var volume = (int)(256.0f * gain + 0.5f);
        var leftVolume = (int)((-Panning + 1.0f) * (256.0f * gain + 0.5f));
        var rightVolume = (int)((Panning + 1.0f) * (256.0f * gain + 0.5f));
        var frontLeftVolume = (int)((-Panning + 1.0f) * (Reach + 1.0f) * (256.0f * gain + 0.5f));
        var frontRightVolume = (int)((Panning + 1.0f) * (Reach + 1.0f) * (256.0f * gain + 0.5f));
        var rearLeftVolume = (int)((-Panning + 1.0f) * (-Reach + 1.0f) * (256.0f * gain + 0.5f));
        var rearRightVolume = (int)((Panning + 1.0f) * (-Reach + 1.0f) * (256.0f * gain + 0.5f));
        var centerVolume = (int)(((frontLeftVolume + frontRightVolume) * 0.5f) * MathDefs.Clamp(Reach, 0.0f, 1.0f));

        for (var sample = 0u; sample < samples && _position.HasValue; sample++)
        {
            if (position >= end)
            {
                if (sound.IsLooped() && end > repeat)
                    position = repeat + (position - end) % (end - repeat);
                else
                {
                    _position = null;
                    _sendFinishedEvent = true;
                    position = 0;
                    fractPosition = 0;
                    _timePosition = 0.0f;
                    break;
                }
            }

            var nextPosition = position + sampleSize;
            if (nextPosition >= end)
                nextPosition = sound.IsLooped() && end > repeat ? repeat : position;

            var firstLeft = ReadSample(data, position, sound.IsSixteenBit());
            var firstRight = sourceChannels == 2 ? ReadSample(data, position + channelSize, sound.IsSixteenBit()) : firstLeft;
            var nextLeft = ReadSample(data, nextPosition, sound.IsSixteenBit());
            var nextRight = sourceChannels == 2 ? ReadSample(data, nextPosition + channelSize, sound.IsSixteenBit()) : nextLeft;
            if (interpolation)
            {
                firstLeft = Interpolate(firstLeft, nextLeft, fractPosition);
                firstRight = Interpolate(firstRight, nextRight, fractPosition);
            }

            var destination = checked((int)(sample * (uint)outputChannels));
            if (sourceChannels == 1)
            {
                if (!_lowFrequency || mode == SpeakerMode.Surround51)
                {
                    if (mode == SpeakerMode.Mono)
                        dest[destination] += firstLeft * volume / 256;
                    else if (mode == SpeakerMode.Stereo)
                    {
                        dest[destination] += firstLeft * leftVolume / 256;
                        dest[destination + 1] += firstLeft * rightVolume / 256;
                    }
                    else
                    {
                        if (_lowFrequency && mode == SpeakerMode.Surround51)
                            dest[destination + 3] += firstLeft * volume / 256;
                        else
                        {
                            dest[destination] += firstLeft * frontLeftVolume / 256;
                            dest[destination + 1] += firstLeft * frontRightVolume / 256;
                            if (mode == SpeakerMode.Surround51)
                                dest[destination + 2] += firstLeft * centerVolume / 256;
                            dest[destination + outputChannels - 2] += firstLeft * rearLeftVolume / 256;
                            dest[destination + outputChannels - 1] += firstLeft * rearRightVolume / 256;
                        }
                    }
                }
            }
            else if (mode == SpeakerMode.Mono)
                dest[destination] += ((firstLeft + firstRight) / 2) * volume / 256;
            else
            {
                dest[destination] += firstLeft * volume / 256;
                dest[destination + 1] += firstRight * volume / 256;
                if (outputChannels > 2)
                {
                    var rearOffset = mode == SpeakerMode.Surround51 ? 4 : 2;
                    dest[destination + rearOffset] += firstLeft * volume / 256;
                    dest[destination + rearOffset + 1] += firstRight * volume / 256;
                }
            }

            position += intAdd * sampleSize;
            fractPosition += fractAdd;
            if (fractPosition > 65535)
            {
                fractPosition &= 65535;
                position += sampleSize;
            }

            if (!sound.IsLooped() && position >= end)
            {
                _position = null;
                _sendFinishedEvent = true;
                position = 0;
                fractPosition = 0;
                _timePosition = 0.0f;
                break;
            }
        }

        if (_position.HasValue)
            _position = position;
        _fractPosition = fractPosition;
        if (_soundStream != null && _soundStream.GetFrequency() != 0)
            _timePosition += samples / (float)mixRate * effectiveFrequency / _soundStream.GetFrequency();
        else if (_sound != null && _position.HasValue && _sound.GetSampleSize() != 0 && _sound.GetFrequency() != 0)
            _timePosition = _position.Value / (float)(_sound.GetSampleSize() * _sound.GetFrequency());
    }

    private static int ReadSample(sbyte[] data, int offset, bool sixteenBit)
    {
        if (sixteenBit)
            return (short)((byte)data[offset] | ((byte)data[offset + 1] << 8));
        return data[offset];
    }

    private static int Interpolate(int first, int second, int fraction)
    {
        return ((second - first) * fraction / 65536) + first;
    }
}
