namespace RedBolt;

/// <summary>Compressed audio decode buffer length in milliseconds.</summary>
public static class SoundSourceConstants
{
    public const int StreamBufferLength = 100;
}

/// <summary>Sound source component with stereo position.</summary>
public class SoundSource : Component
{
    protected Audio? Audio;
    protected string SoundType = AudioDefs.SoundEffect;
    internal StringHash SoundTypeHash = new(AudioDefs.SoundEffect);
    internal float Frequency = 1.0f;
    protected float Gain = 1.0f;
    protected float Attenuation = 1.0f;
    protected float Panning;
    protected float Reach;
    protected float MasterGain;
    protected AutoRemoveMode AutoRemove;

    private Sound? _sound;
    private SoundStream? _soundStream;
    private int _position;
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
        SetEnabled(false);
    }

    ~SoundSource()
    {
        Stop();
    }

    /// <summary>Play a sound.</summary>
    public void Play(Sound? sound) => Play(sound, 1.0f, 1.0f, 0.0f);

    /// <summary>Play a sound with specified frequency.</summary>
    public void Play(Sound? sound, float frequency) => Play(sound, frequency, 1.0f, 0.0f);

    /// <summary>Play a sound with specified frequency and gain.</summary>
    public void Play(Sound? sound, float frequency, float gain) => Play(sound, frequency, gain, 0.0f);

    /// <summary>Play a sound with specified frequency, gain and panning.</summary>
    public void Play(Sound? sound, float frequency, float gain, float panning)
    {
        if (sound == null) return;
        lock (_playLock)
        {
            Stop();
            _sound = sound;
            _soundStream = sound.IsCompressed() ? sound.GetDecoderStream() : null;
            Frequency = frequency;
            Gain = gain;
            Panning = panning;
            _timePosition = 0.0f;
            _position = 0;
            _fractPosition = 0;
            _sendFinishedEvent = false;

            if (_soundStream != null)
            {
                _streamBuffer = null;
                _unusedStreamSize = 0;
            }
        }
        SetEnabled(true);
        Audio?.AddSoundSource(this);
    }

    /// <summary>Start playing a sound stream.</summary>
    public void Play(SoundStream? stream)
    {
        if (stream == null) return;
        lock (_playLock)
        {
            Stop();
            _soundStream = stream;
            _sound = null;
            _position = 0;
            _fractPosition = 0;
            _timePosition = 0.0f;
            _sendFinishedEvent = false;
            _streamBuffer = null;
            _unusedStreamSize = 0;
            Frequency = 1.0f;
        }
        SetEnabled(true);
        Audio?.AddSoundSource(this);
    }

    /// <summary>Stop playback.</summary>
    public void Stop()
    {
        Audio?.RemoveSoundSource(this);
        lock (_playLock)
        {
            _sound = null;
            _soundStream = null;
            _streamBuffer = null;
        }
        SetEnabled(false);
    }

    public void Seek(float seekTime) { /* TODO: port */ }

    public void SetSoundType(string type)
    {
        SoundType = type;
        SoundTypeHash = new StringHash(type);
    }

    public void SetFrequency(float frequency) => Frequency = frequency;
    public void SetGain(float gain) { Gain = MathDefs.Clamp(gain, 0.0f, 1.0f); }
    public void SetAttenuation(float attenuation) => Attenuation = attenuation;
    public void SetPanning(float panning) { Panning = MathDefs.Clamp(panning, -1.0f, 1.0f); }
    public void SetReach(float reach) { Reach = MathDefs.Clamp(reach, -1.0f, 1.0f); }
    public void SetLowFrequency(bool state) => _lowFrequency = state;
    public void SetAutoRemoveMode(AutoRemoveMode mode) => AutoRemove = mode;
    public void SetIgnoreSceneTimeScale(bool ignoreSceneTimeScale) => _ignoreSceneTimeScale = ignoreSceneTimeScale;

    public Sound? GetSound() => _sound;
    public int GetPlayPosition() => _position;
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
    public bool IsPlaying() => GetSound() != null || _soundStream != null;

    public virtual void Update(float timeStep)
    {
    }

    /// <summary>Mix sound source output to a 32-bit clipping buffer. Called by Audio.</summary>
    public void Mix(int[] dest, uint samples, int mixRate, SpeakerMode mode, bool interpolation)
    {
        lock (_playLock)
        {
            if (_sound == null && _soundStream == null)
                return;

            var sound = _sound;
            var stream = _soundStream;
            float effectiveFrequency;
            int sampleSize;

            if (sound != null)
            {
                // Check for streamed (compressed) playback
                if (sound.IsCompressed())
                {
                    if (stream == null) return;
                    effectiveFrequency = Frequency * stream.GetIntFrequency();
                    sampleSize = (int)stream.GetSampleSize();
                    // Decode stream data into buffer
                    var bufferBytes = mixRate * SoundSourceConstants.StreamBufferLength * sampleSize;
                    var decodeBuffer = new sbyte[bufferBytes];
                    var decoded = stream.GetData(decodeBuffer, (uint)bufferBytes);
                    sound = _streamBuffer;
                    if (decoded == 0) { Stop(); return; }
                }
                else
                {
                    effectiveFrequency = Frequency * sound.GetIntFrequency();
                    sampleSize = (int)sound.GetSampleSize();
                }
            }
            else if (stream != null)
            {
                effectiveFrequency = Frequency * stream.GetIntFrequency();
                sampleSize = (int)stream.GetSampleSize();
                var bufferBytes = (uint)(mixRate * SoundSourceConstants.StreamBufferLength / 1000 * sampleSize);
                var decodeBuffer = new sbyte[bufferBytes];
                var decoded = stream.GetData(decodeBuffer, bufferBytes);
                sound = _streamBuffer;
                if (stream.GetStopAtEnd() && decoded < bufferBytes) { Stop(); return; }
            }
            else
            {
                return;
            }

            if (sound == null) return;

            MixMono(sound, dest, samples, mixRate, effectiveFrequency, sampleSize);
        }
    }

    public void UpdateMasterGain()
    {
        MasterGain = Audio?.GetSoundSourceMasterGain(SoundTypeHash) ?? 1.0f;
    }

    /// <summary>Set sound attribute for serialization.</summary>
    public ResourceRef GetSoundAttr() => GetSound() != null ? new ResourceRef(new StringHash(nameof(Sound)), GetSound()!.Name) : ResourceRef.Empty;

    private void MixMono(Sound sound, int[] dest, uint samples, int mixRate, float effectiveFrequency, int sampleSize)
    {
        var data = sound.GetData();
        if (data == null) return;

        var freqFactor = effectiveFrequency / mixRate;
        var gain = Gain * MasterGain * Attenuation;
        if (gain <= 0.0f) return;

        var panLeft = MathDefs.Clamp(1.0f - Panning, 0.0f, 1.0f) * gain;
        var panRight = MathDefs.Clamp(1.0f + Panning, 0.0f, 1.0f) * gain;

        var intGain = (int)(gain * 32768);
        var intLeft = (int)(panLeft * 32768);
        var intRight = (int)(panRight * 32768);

        var loops = 2; // assume stereo output, 2 channels
        for (var i = 0u; i < samples * (uint)loops; i += (uint)loops)
        {
            var sampleIndex = (int)((i / (uint)loops) * freqFactor * sampleSize);
            if (sound.IsSixteenBit())
            {
                var sampleBytes = new sbyte[2];
                sampleBytes[0] = data[Math.Min(sampleIndex, data.Length - 2)];
                sampleBytes[1] = data[Math.Min(sampleIndex + 1, data.Length - 1)];
                var sample = BitConverter.ToInt16((byte[])(Array)sampleBytes, 0);
                dest[i] += (sample * intLeft) >> 15;
                dest[i + 1] += (sample * intRight) >> 15;
            }
        }
    }
}
