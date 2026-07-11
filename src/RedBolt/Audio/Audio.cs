using RedBolt.Bindings.Sdl;

namespace RedBolt;

/// <summary>Audio subsystem.</summary>
public sealed class Audio : Object
{
    private const int MinBufferLength = 20;
    private const int MinMixRate = 11025;
    private const int MaxMixRate = 48000;
    private const uint SdlInitAudio = 0x00000010u;
    private const ushort AudioS16Sys = 0x8010;
    private const int SdlAudioAllowAnyChange = 0x0000000F;
    private const int SdlAudioAllowFormatChange = 0x00000002;

    private static readonly StringHash SoundMasterHash = new(AudioDefs.SoundMaster);

    private static readonly int[] AudioNumChannels = [6, 1, 2, 4, 6];
    private static readonly SpeakerMode[] ChannelsToMode = [SpeakerMode.Auto, SpeakerMode.Mono, SpeakerMode.Stereo, SpeakerMode.Auto, SpeakerMode.Quadrophonic, SpeakerMode.Auto, SpeakerMode.Surround51, SpeakerMode.Auto, SpeakerMode.Auto];
    private static readonly SpeakerMode[] AudioModeDowngrade = [SpeakerMode.Quadrophonic, SpeakerMode.Mono, SpeakerMode.Mono, SpeakerMode.Stereo, SpeakerMode.Quadrophonic];

    private readonly List<SoundSource> _soundSources = [];
    private SoundListener? _listener;
    private readonly Dictionary<StringHash, float> _masterGain = [];
    private readonly HashSet<StringHash> _pausedSoundTypes = [];
    private readonly object _audioMutex = new();

    private int[] _clipBuffer = [];
    private uint _sampleSize;
    private uint _fragmentSize;
    private uint _bufferLengthMSec;
    private int _mixRate;
    private bool _interpolation = true;
    private SpeakerMode _speakerMode = SpeakerMode.Auto;
    private bool _playing;
    private bool _initialized;
    private uint _deviceId;
    private ContextEventSubscription? _renderUpdateSubscription;

    public Audio()
    {
        Context.RequireSdl(SdlInitAudio);
        _masterGain[SoundMasterHash] = 1.0f;
        RegisterAudioLibrary();
        _renderUpdateSubscription = SubscribeToEvent<CoreEvents.RenderUpdateEvent>(HandleRenderUpdate);
    }

    ~Audio()
    {
        Release();
        Context.ReleaseSdl();
    }

    private static void RegisterAudioLibrary() { }

    /// <summary>Initialize sound output.</summary>
    public bool SetMode(int bufferLengthMSec, int mixRate, SpeakerMode speakerMode, bool interpolation = true)
    {
        Release();

        bufferLengthMSec = Math.Max(bufferLengthMSec, MinBufferLength);
        mixRate = Math.Clamp(mixRate, MinMixRate, MaxMixRate);

        using var desired = new SdlAudioSpec
        {
            Freq = mixRate,
            Format = AudioS16Sys,
            Channels = (byte)AudioNumChannels[(int)speakerMode],
            Samples = 0
        };

        var bufferSamples = mixRate * bufferLengthMSec / 1000;
        var samples = (ushort)NextPowerOfTwo((uint)bufferSamples);
        if (Math.Abs(samples / 2 - bufferSamples) < Math.Abs(samples - bufferSamples))
            samples /= 2;
        desired.Samples = samples;

        using var obtained = new SdlAudioSpec();

        var currentMode = speakerMode;
        var allowedChanges = SdlAudioAllowAnyChange & ~SdlAudioAllowFormatChange;

        if (speakerMode == SpeakerMode.Auto)
        {
            for (;;)
            {
                desired.Channels = (byte)AudioNumChannels[(int)currentMode];
                _deviceId = SdlNativeBindings.SdlOpenAudioDevice([], 0, desired, obtained, allowedChanges);

                if (_deviceId != 0) break;

                var nextMode = AudioModeDowngrade[(int)currentMode];
                if (nextMode == currentMode) break;
                currentMode = nextMode;
            }
        }
        else
        {
            desired.Channels = (byte)AudioNumChannels[(int)currentMode];
            _deviceId = SdlNativeBindings.SdlOpenAudioDevice([], 0, desired, obtained, allowedChanges);
        }

        if (_deviceId == 0)
            return false;

        if (obtained.Format != AudioS16Sys)
        {
            SdlNativeBindings.SdlCloseAudioDevice(_deviceId);
            _deviceId = 0;
            return false;
        }

        var numChannels = obtained.Channels;
        _speakerMode = numChannels > 8 ? SpeakerMode.Auto : ChannelsToMode[numChannels];

        if (_speakerMode == SpeakerMode.Auto)
        {
            SdlNativeBindings.SdlCloseAudioDevice(_deviceId);
            _deviceId = 0;
            return false;
        }

        _sampleSize = (uint)(sizeof(short) * AudioNumChannels[(int)_speakerMode]);
        _fragmentSize = (uint)(obtained.Samples * obtained.Channels);
        _mixRate = obtained.Freq;
        _bufferLengthMSec = (uint)bufferLengthMSec;
        _interpolation = interpolation;
        _initialized = true;

        _clipBuffer = new int[_fragmentSize];
        return true;
    }

    public bool RefreshMode() => SetMode((int)_bufferLengthMSec, _mixRate, _speakerMode, _interpolation);

    public void Close()
    {
        Release();
        _initialized = false;
    }

    public void Update(float timeStep)
    {
        if (!_initialized || !_playing) return;

        lock (_audioMutex)
        {
            Array.Clear(_clipBuffer, 0, _clipBuffer.Length);
            foreach (var source in _soundSources)
            {
                if (_pausedSoundTypes.Contains(source.SoundTypeHash)) continue;
                source.UpdateMasterGain();
                source.Mix(_clipBuffer, _fragmentSize / _sampleSize, _mixRate, _speakerMode, _interpolation);
            }

            var sampleCount = (int)_fragmentSize;
            var shortBuffer = new short[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var val = _clipBuffer[i];
                if (val < short.MinValue) val = short.MinValue;
                if (val > short.MaxValue) val = short.MaxValue;
                shortBuffer[i] = (short)val;
            }

            var handle = System.Runtime.InteropServices.GCHandle.Alloc(shortBuffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                SdlNativeBindings.SdlQueueAudio(_deviceId, handle.AddrOfPinnedObject(), (uint)(sampleCount * sizeof(short)));
            }
            finally
            {
                handle.Free();
            }
        }
    }

    public bool Play()
    {
        if (!_initialized) return false;
        SdlNativeBindings.SdlPauseAudioDevice(_deviceId, 0);
        _playing = true;
        return true;
    }

    public void Stop()
    {
        if (_initialized)
            SdlNativeBindings.SdlPauseAudioDevice(_deviceId, 1);
        _playing = false;
    }

    public void SetMasterGain(string type, float gain)
    {
        var hash = new StringHash(type);
        _masterGain[hash] = MathDefs.Clamp(gain, 0.0f, 1.0f);
    }

    public void PauseSoundType(string type) => _pausedSoundTypes.Add(new StringHash(type));
    public void ResumeSoundType(string type) => _pausedSoundTypes.Remove(new StringHash(type));
    public void ResumeAll() => _pausedSoundTypes.Clear();

    public void SetListener(SoundListener? listener) => _listener = listener;
    public void StopSound(Sound? sound) { /* TODO: port */ }

    public uint GetSampleSize() => _sampleSize;
    public int GetMixRate() => _mixRate;
    public uint GetBufferLengthMS() => _bufferLengthMSec;
    public bool GetInterpolation() => _interpolation;
    public SpeakerMode GetSpeakerMode() => _speakerMode;
    public bool IsPlaying() => _playing;
    public bool IsInitialized() => _initialized;

    public float GetMasterGain(string type)
    {
        var hash = new StringHash(type);
        return _masterGain.TryGetValue(hash, out var gain) ? gain : 1.0f;
    }

    public bool IsSoundTypePaused(string type) => _pausedSoundTypes.Contains(new StringHash(type));
    public SoundListener? GetListener() => _listener;
    public IReadOnlyList<SoundSource> GetSoundSources() => _soundSources;
    public bool HasMasterGain(string type) => _masterGain.ContainsKey(new StringHash(type));

    public void AddSoundSource(SoundSource soundSource)
    {
        lock (_audioMutex)
        {
            if (!_soundSources.Contains(soundSource))
                _soundSources.Add(soundSource);
        }
    }

    public void RemoveSoundSource(SoundSource soundSource)
    {
        lock (_audioMutex)
        {
            _soundSources.Remove(soundSource);
        }
    }

    public float GetSoundSourceMasterGain(StringHash typeHash)
    {
        return _masterGain.TryGetValue(SoundMasterHash, out var master) &&
               _masterGain.TryGetValue(typeHash, out var typeGain)
            ? master * typeGain
            : 1.0f;
    }

    private void HandleRenderUpdate(ref CoreEvents.RenderUpdateEvent eventData)
    {
        if (_playing)
            Update(eventData.TimeStep);
    }

    private void Release()
    {
        Stop();
        if (_deviceId != 0)
        {
            SdlNativeBindings.SdlCloseAudioDevice(_deviceId);
            _deviceId = 0;
        }
    }

    private static uint NextPowerOfTwo(uint x)
    {
        if (x == 0) return 1;
        x--;
        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        return x + 1;
    }
}
