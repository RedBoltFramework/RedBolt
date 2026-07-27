using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using RedBolt.Bindings.Sdl;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Logging;
using RedBolt.Math;
namespace RedBolt.Audio;
using Math = global::System.Math;


/// <summary>Audio subsystem.</summary>
public class AudioSystem : RedObject
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
    private static readonly string[] SpeakerModeNames = ["Auto", "Mono", "Stereo", "Quadrophonic", "5.1 Surround"];
    private static readonly int[,] RecordingFrequencies = { { 44100, 22050, 16000 }, { 16000, 22050, 44100 } };

    private int[] _clipBuffer = [];
    private readonly object _audioMutex = new();
    private uint _deviceId;
    private uint _sampleSize;
    private uint _fragmentSize;
    private uint _bufferLengthMSec = 0;
    private int _mixRate;
    private bool _interpolation;
    private SpeakerMode _speakerMode = SpeakerMode.Auto;
    private bool _playing;
    private readonly Dictionary<StringHash, float> _masterGain = [];
    private readonly HashSet<StringHash> _pausedSoundTypes = [];
    private readonly List<SoundSource> _soundSources = [];
    private SoundListener? _listener;
    private readonly List<WeakReference<Microphone>> _microphones = [];
    private GCHandle _callbackHandle;

    public AudioSystem()
    {
        Context.RequireSdl(SdlInitAudio);
        _masterGain[SoundMasterHash] = 1.0f;
        RegisterAudioLibrary(Context);
        SubscribeToEvent<CoreEvents.RenderUpdateEvent>(HandleRenderUpdate);
        _callbackHandle = GCHandle.Alloc(this);
    }

    public override void Dispose()
    {
        Release();
        if (_callbackHandle.IsAllocated)
            _callbackHandle.Free();
        Context.ReleaseSdl();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Register Audio library objects.</summary>
    public static void RegisterAudioLibrary(Context context)
    {
        Sound.RegisterObject(context);
        SoundSource.RegisterObject(context);
        SoundSource3D.RegisterObject(context);
        SoundListener.RegisterObject(context);
    }

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
            Samples = 0,
            Callback = GetAudioCallback(),
            Userdata = GCHandle.ToIntPtr(_callbackHandle)
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
                Log.GetLogger().LogError("Could not initialize audio output for speaker mode {SpeakerMode}", SpeakerModeNames[(int)speakerMode]);
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
        _interpolation = interpolation;
        _clipBuffer = new int[_fragmentSize * AudioNumChannels[(int)_speakerMode]];
        Log.GetLogger().LogInformation("Set audio mode {MixRate} Hz {SpeakerMode} {Interpolation}",
            _mixRate, SpeakerModeNames[(int)_speakerMode], _interpolation ? "interpolated" : "");
        return Play();
    }

    public bool RefreshMode() => SetMode((int)_bufferLengthMSec, _mixRate, _speakerMode, _interpolation);

    public void Close()
    {
        Release();
    }

    public void Update(float timeStep)
    {
        if (!_playing) return;

        UpdateInternal(timeStep);

        for (var i = 0; i < _microphones.Count; ++i)
        {
            if (_microphones[i].TryGetTarget(out var microphone))
            {
                microphone.CheckDirtiness();
            }
            else
            {
                _microphones.RemoveAt(i);
                --i;
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
        for (var i = 0; i < _soundSources.Count; ++i)
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

    public void AddSoundSource(SoundSource soundSource)
    {
        lock (_audioMutex)
        {
            _soundSources.Add(soundSource);
        }
    }

    public void RemoveSoundSource(SoundSource soundSource)
    {
        var index = _soundSources.IndexOf(soundSource);
        if (index < 0)
            return;

        lock (_audioMutex)
        {
            _soundSources.RemoveAt(index);
        }
    }

    public object GetMutex() => _audioMutex;

    public float GetSoundSourceMasterGain(StringHash typeHash)
    {
        var master = _masterGain[SoundMasterHash];
        if (typeHash.Equals(default(StringHash)))
            return master;
        return !_masterGain.TryGetValue(typeHash, out var typeGain) || typeHash == SoundMasterHash
            ? master
            : master * typeGain;
    }

    public void MixOutput(Span<short> dest, uint samples)
    {
        if (!_playing || _clipBuffer.Length == 0)
        {
            dest.Clear();
            return;
        }

        var channels = AudioNumChannels[(int)_speakerMode];
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
                if (_pausedSoundTypes.Count != 0)
                {
                    if (_pausedSoundTypes.Contains(source.SoundTypeHash))
                        continue;
                }
                source.Mix(_clipBuffer, workSamples, _mixRate, _speakerMode, _interpolation);
            }

            for (var i = 0; i < clipSamples; i++)
                dest[destinationOffset + i] = (short)Math.Clamp(_clipBuffer[i], short.MinValue, short.MaxValue);

            destinationOffset += checked((int)(_sampleSize * workSamples / sizeof(short)));
            samples -= workSamples;
        }
    }

    public IReadOnlyList<string> EnumerateMicrophones()
    {
        var count = SdlNativeBindings.SdlGetNumAudioDevices(1);
        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = DecodeUtf8(SdlNativeBindings.SdlGetAudioDeviceName(i, 1));
            foreach (var weakMicrophone in _microphones)
            {
                if (weakMicrophone.TryGetTarget(out var microphone)
                    && string.Equals(microphone._name, result[i], StringComparison.OrdinalIgnoreCase))
                {
                    microphone._which = (uint)i;
                }
            }
        }
        return result;
    }

    public Microphone? CreateMicrophone(string name, bool forSpeechRecog, uint wantedFreq, uint silenceLevelLimit = 0)
    {
        var deviceCount = SdlNativeBindings.SdlGetNumAudioDevices(1);

        for (var deviceIndex = 0; deviceIndex < deviceCount; ++deviceIndex)
        {
            var deviceName = DecodeUtf8(SdlNativeBindings.SdlGetAudioDeviceName(deviceIndex, 1));
            foreach (var weakMicrophone in _microphones)
            {
                if (weakMicrophone.TryGetTarget(out var microphone)
                    && string.Equals(microphone._name, deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    microphone._which = (uint)deviceIndex;
                }
            }
        }

        for (var deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
        {
            var deviceName = DecodeUtf8(SdlNativeBindings.SdlGetAudioDeviceName(deviceIndex, 1));
            if (!string.Equals(name, deviceName, StringComparison.OrdinalIgnoreCase))
                continue;

            var microphone = new Microphone();
            var attempts = wantedFreq == 0 ? 3 : 1;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var frequency = wantedFreq == 0
                    ? (uint)RecordingFrequencies[forSpeechRecog ? 1 : 0, attempt]
                    : wantedFreq;
                using var desired = new SdlAudioSpec
                {
                    Freq = checked((int)frequency),
                    Format = AudioS16Sys,
                    Channels = 1,
                    Samples = checked((ushort)(frequency / 2)),
                    Callback = GetAudioRecordingCallback(),
                    Userdata = microphone.CallbackUserData
                };
                using var obtained = new SdlAudioSpec();
                using var deviceNameValue = new CString(deviceName);
                var deviceId = SdlNativeBindings.SdlOpenAudioDevice(deviceNameValue.Bytes, 1, desired, obtained, 0);
                if (deviceId == 0)
                    continue;

                microphone.SetWakeThreshold(silenceLevelLimit);
                microphone.Init(deviceName, deviceId, desired.Samples, (uint)desired.Freq, (uint)attempt);
                _microphones.Add(new WeakReference<Microphone>(microphone));
                return microphone;
            }

            Log.GetLogger().LogError("Could not open access to microphone {Microphone}", deviceName);
            microphone.Dispose();
        }

        return null;
    }

    public void CloseMicrophoneForLoss(uint which)
    {
        foreach (var weakMicrophone in _microphones)
        {
            if (weakMicrophone.TryGetTarget(out var microphone) && microphone._which == which)
            {
                SdlNativeBindings.SdlCloseAudioDevice(microphone._deviceId);
                microphone._which = uint.MaxValue;
                microphone._deviceId = 0;
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
            _clipBuffer = [];
        }
    }

    private void UpdateInternal(float timeStep)
    {
        for (var i = _soundSources.Count - 1; i >= 0; i--)
        {
            var source = _soundSources[i];
            if (_pausedSoundTypes.Count != 0)
            {
                if (_pausedSoundTypes.Contains(source.SoundTypeHash))
                    continue;
            }
            source.Update(timeStep);
        }
    }

    private static unsafe IntPtr GetAudioCallback()
    {
        return (IntPtr)(delegate* unmanaged[Cdecl]<void*, byte*, int, void>)&SdlAudioCallback;
    }

    private static unsafe IntPtr GetAudioRecordingCallback()
    {
        return (IntPtr)(delegate* unmanaged[Cdecl]<void*, byte*, int, void>)&SdlAudioRecordingCallback;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void SdlAudioCallback(void* userData, byte* stream, int length)
    {
        var audio = (AudioSystem)GCHandle.FromIntPtr((IntPtr)userData).Target!;
        lock (audio._audioMutex)
        {
            audio.MixOutput(new Span<short>(stream, length / sizeof(short)),
                (uint)(length / audio._sampleSize));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void SdlAudioRecordingCallback(void* userData, byte* stream, int length)
    {
        var microphone = (Microphone)GCHandle.FromIntPtr((IntPtr)userData).Target!;
        microphone.Update(new ReadOnlySpan<byte>(stream, length), length);
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
