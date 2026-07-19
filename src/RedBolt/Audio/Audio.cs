using System.Text;
using Microsoft.Extensions.Logging;
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
    private const int SdlAudioAllowChannelsChange = 0x00000004;

    private static readonly StringHash SoundMasterHash = new(AudioDefs.SoundMaster);

    private static readonly int[] AudioNumChannels = [6, 1, 2, 4, 6];
    private static readonly SpeakerMode[] ChannelsToMode = [SpeakerMode.Auto, SpeakerMode.Mono, SpeakerMode.Stereo, SpeakerMode.Auto, SpeakerMode.Quadrophonic, SpeakerMode.Auto, SpeakerMode.Surround51, SpeakerMode.Auto, SpeakerMode.Auto];
    private static readonly SpeakerMode[] AudioModeDowngrade = [SpeakerMode.Quadrophonic, SpeakerMode.Mono, SpeakerMode.Mono, SpeakerMode.Stereo, SpeakerMode.Quadrophonic];

    private readonly List<SoundSource> _soundSources = [];
    private readonly List<Microphone> _microphones = [];
    private SoundListener? _listener;
    private readonly Dictionary<StringHash, float> _masterGain = [];
    private readonly HashSet<StringHash> _pausedSoundTypes = [];
    private readonly object _audioMutex = new();
    private int[] _clipBuffer = [];
    private short[] _outputBuffer = [];
    private uint _sampleSize;
    private uint _fragmentSize;
    private uint _bufferLengthMSec;
    private int _mixRate;
    private bool _interpolation;
    private SpeakerMode _speakerMode = SpeakerMode.Auto;
    private bool _playing;
    private uint _deviceId;
    private ContextEventSubscription? _renderUpdateSubscription;

    public Audio()
    {
        Context.RequireSdl(SdlInitAudio);
        _masterGain[SoundMasterHash] = 1.0f;
        RegisterAudioLibrary();
        _renderUpdateSubscription = SubscribeToEvent<CoreEvents.RenderUpdateEvent>(HandleRenderUpdate);
    }

    public override void Dispose()
    {
        Release();
        Context.ReleaseSdl();
        base.Dispose();
        GC.SuppressFinalize(this);
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
        var allowedChanges = SdlAudioAllowAnyChange & ~SdlAudioAllowFormatChange & ~SdlAudioAllowChannelsChange;

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
        {
            if (speakerMode == SpeakerMode.Auto)
                Log.GetLogger().LogError("Could not initialize audio output");
            else
                Log.GetLogger().LogError("Could not initialize audio output for speaker mode {SpeakerMode}", speakerMode);
            return false;
        }

        if (obtained.Format != AudioS16Sys)
        {
            Log.GetLogger().LogError("Could not initialize audio output, 16-bit buffer format not supported.");
            SdlNativeBindings.SdlCloseAudioDevice(_deviceId);
            _deviceId = 0;
            return false;
        }

        var numChannels = obtained.Channels;
        _speakerMode = numChannels > 8 ? SpeakerMode.Auto : ChannelsToMode[numChannels];

        if (_speakerMode == SpeakerMode.Auto)
        {
            Log.GetLogger().LogError("Could not identify channel configuration for audio output.");
            SdlNativeBindings.SdlCloseAudioDevice(_deviceId);
            _deviceId = 0;
            return false;
        }

        _sampleSize = (uint)(sizeof(short) * AudioNumChannels[(int)_speakerMode]);
        _fragmentSize = Math.Min(NextPowerOfTwo((uint)(mixRate >> 6)), obtained.Samples);
        _mixRate = obtained.Freq;
        _bufferLengthMSec = (uint)bufferLengthMSec;
        _interpolation = interpolation;
        _clipBuffer = new int[_fragmentSize * AudioNumChannels[(int)_speakerMode]];
        _outputBuffer = new short[_clipBuffer.Length];
        Log.GetLogger().LogInformation("Set audio mode {MixRate} Hz {SpeakerMode} {Interpolation}",
            _mixRate, _speakerMode, _interpolation ? "interpolated" : "");
        return Play();
    }

    public bool RefreshMode() => SetMode((int)_bufferLengthMSec, _mixRate, _speakerMode, _interpolation);

    public void Close()
    {
        Release();
    }

    public void Update(float timeStep)
    {
        // C# uses manual polling with SDL_QueueAudio in Update() because the generated SDL binding has no audio callback surface.
        if (!_playing) return;

        lock (_audioMutex)
        {
            UpdateInternal(timeStep);
            foreach (var microphone in _microphones)
            {
                microphone.UpdateNative();
                microphone.CheckDirtiness();
            }

            var clipSamples = (int)(_fragmentSize * AudioNumChannels[(int)_speakerMode]);
            MixOutput(_outputBuffer.AsSpan(0, clipSamples), _fragmentSize);

            var handle = System.Runtime.InteropServices.GCHandle.Alloc(_outputBuffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                SdlNativeBindings.SdlQueueAudio(_deviceId, handle.AddrOfPinnedObject(), (uint)(clipSamples * sizeof(short)));
            }
            finally
            {
                handle.Free();
            }
        }
    }

    public bool Play()
    {
        if (_playing) return true;
        if (_deviceId == 0)
        {
            Log.GetLogger().LogError("No audio mode set, can not start playback");
            return false;
        }
        SdlNativeBindings.SdlPauseAudioDevice(_deviceId, 0);
        UpdateInternal(0.0f);
        _playing = true;
        return true;
    }

    public void Stop()
    {
        _playing = false;
    }

    public void SetMasterGain(string type, float gain)
    {
        var hash = new StringHash(type);
        _masterGain[hash] = MathDefs.Clamp(gain, 0.0f, 1.0f);
        foreach (var source in _soundSources)
            source.UpdateMasterGain();
    }

    public void PauseSoundType(string type)
    {
        lock (_audioMutex)
            _pausedSoundTypes.Add(new StringHash(type));
    }

    public void ResumeSoundType(string type)
    {
        lock (_audioMutex)
        {
            _pausedSoundTypes.Remove(new StringHash(type));
            UpdateInternal(0.0f);
        }
    }

    public void ResumeAll()
    {
        lock (_audioMutex)
        {
            _pausedSoundTypes.Clear();
            UpdateInternal(0.0f);
        }
    }

    public void SetListener(SoundListener? listener) => _listener = listener;
    public void StopSound(Sound? sound)
    {
        for (var i = _soundSources.Count - 1; i >= 0; i--)
        {
            if (_soundSources[i].GetSound() == sound)
                _soundSources[i].Stop();
        }
    }

    public uint GetSampleSize() => _sampleSize;
    public int GetMixRate() => _mixRate;
    public uint GetBufferLengthMS() => _bufferLengthMSec;
    public bool GetInterpolation() => _interpolation;
    public SpeakerMode GetSpeakerMode() => _speakerMode;
    public bool IsPlaying() => _playing;
    public bool IsInitialized() => _deviceId != 0;

    public float GetMasterGain(string type)
    {
        var hash = new StringHash(type);
        return _masterGain.TryGetValue(hash, out var gain) ? gain : 1.0f;
    }

    public bool IsSoundTypePaused(string type) => _pausedSoundTypes.Contains(new StringHash(type));
    public SoundListener? GetListener() => _listener;
    public IReadOnlyList<SoundSource> GetSoundSources() => _soundSources;
    public bool HasMasterGain(string type) => _masterGain.ContainsKey(new StringHash(type));

    public object GetMutex() => _audioMutex;

    public void AddSoundSource(SoundSource soundSource)
    {
        lock (_audioMutex)
        {
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
        if (!_masterGain.TryGetValue(SoundMasterHash, out var master))
            return 1.0f;
        if (typeHash.Equals(default(StringHash)) || typeHash == SoundMasterHash)
            return master;
        return _masterGain.TryGetValue(typeHash, out var typeGain) ? master * typeGain : master;
    }

    public void MixOutput(Span<short> dest, uint samples)
    {
        if (!_playing || _clipBuffer.Length == 0)
        {
            dest.Clear();
            return;
        }

        var channels = AudioNumChannels[(int)_speakerMode];
        lock (_audioMutex)
        {
            var destinationOffset = 0;
            while (samples != 0)
            {
                var workSamples = Math.Min(samples, _fragmentSize);
                var clipSamples = checked((int)workSamples);
                if (_speakerMode != SpeakerMode.Mono)
                    clipSamples = checked(channels * (int)_fragmentSize);

                Array.Clear(_clipBuffer, 0, clipSamples);
                foreach (var source in _soundSources)
                {
                    if (_pausedSoundTypes.Contains(source.SoundTypeHash))
                        continue;
                    source.Mix(_clipBuffer, workSamples, _mixRate, _speakerMode, _interpolation);
                }

                for (var i = 0; i < clipSamples; i++)
                    dest[destinationOffset + i] = (short)Math.Clamp(_clipBuffer[i], short.MinValue, short.MaxValue);

                destinationOffset += checked((int)(_sampleSize * workSamples / sizeof(short)));
                samples -= workSamples;
            }
        }
    }

    public IReadOnlyList<string> EnumerateMicrophones()
    {
        var count = SdlNativeBindings.SdlGetNumAudioDevices(1);
        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = DecodeUtf8(SdlNativeBindings.SdlGetAudioDeviceName(i, 1));
            foreach (var microphone in _microphones)
            {
                if (string.Equals(microphone.GetName(), result[i], StringComparison.OrdinalIgnoreCase))
                    microphone.SetDeviceIndex((uint)i);
            }
        }
        return result;
    }

    public Microphone? CreateMicrophone(string name, bool forSpeechRecog, uint wantedFreq, uint silenceLevelLimit = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var deviceCount = SdlNativeBindings.SdlGetNumAudioDevices(1);
        var frequencies = forSpeechRecog
            ? new uint[] { 16000, 22050, 44100 }
            : new uint[] { 44100, 22050, 16000 };
        var attempts = wantedFreq == 0 ? frequencies : new[] { wantedFreq };

        for (var deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
        {
            var deviceName = DecodeUtf8(SdlNativeBindings.SdlGetAudioDeviceName(deviceIndex, 1));
            if (!string.Equals(name, deviceName, StringComparison.OrdinalIgnoreCase))
                continue;

            for (var attempt = 0; attempt < attempts.Length; attempt++)
            {
                var frequency = attempts[attempt];
                using var desired = new SdlAudioSpec
                {
                    Freq = checked((int)frequency),
                    Format = AudioS16Sys,
                    Channels = 1,
                    Samples = checked((ushort)(frequency / 2))
                };
                using var obtained = new SdlAudioSpec();
                using var deviceNameValue = new CString(deviceName);
                var deviceId = SdlNativeBindings.SdlOpenAudioDevice(deviceNameValue.Bytes, 1, desired, obtained, 0);
                if (deviceId == 0)
                    continue;

                var microphone = new Microphone();
                microphone.SetWakeThreshold(silenceLevelLimit);
                microphone.InitializeNative(deviceName, deviceId, desired.Samples, (uint)desired.Freq, (uint)attempt);
                _microphones.Add(microphone);
                return microphone;
            }

            return null;
        }

        return null;
    }

    public void CloseMicrophoneForLoss(uint which)
    {
        foreach (var microphone in _microphones)
        {
            if (microphone.GetDeviceIndex() == which)
            {
                microphone.CloseNative();
                microphone.SetDeviceIndex(uint.MaxValue);
            }
        }
    }

    private void HandleRenderUpdate(ref CoreEvents.RenderUpdateEvent eventData)
    {
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
        _clipBuffer = [];
        _outputBuffer = [];
    }

    private void UpdateInternal(float timeStep)
    {
        for (var i = _soundSources.Count - 1; i >= 0; i--)
        {
            var source = _soundSources[i];
            if (_pausedSoundTypes.Contains(source.SoundTypeHash))
                continue;
            source.Update(timeStep);
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

    private static string DecodeUtf8(ReadOnlySpan<byte> source)
    {
        var length = source.IndexOf((byte)0);
        return Encoding.UTF8.GetString(length < 0 ? source : source[..length]);
    }
}

// C++ SDL calls this callback from the audio thread (pull model).
// C# uses SDL_QueueAudio in Update() instead (push model).
