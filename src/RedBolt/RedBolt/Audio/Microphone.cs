namespace RedBolt.Audio;

using SystemMath = global::System.Math;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Sdl;
using RedBolt.Core;

/// <summary>Microphone audio input device.</summary>
[Reflected]
public partial class Microphone : RedObject
{
    private BufferedSoundStream? _linkedStream;
    internal string _name = string.Empty;
    private readonly List<short> _buffer = [];
    internal uint _deviceId;
    internal uint _which;
    private readonly object _lock = new();
    private uint _frequency;
    private uint _wakeThreshold = uint.MaxValue;
    private bool _enabled;
    private bool _isSleeping;
    private bool _isDirty;
    private GCHandle _callbackHandle;

    public Microphone()
    {
        _callbackHandle = GCHandle.Alloc(this);
    }

    public override void Dispose()
    {
        if (_deviceId != 0)
            SdlNativeBindings.SdlCloseAudioDevice(_deviceId);
        _deviceId = 0;
        if (_callbackHandle.IsAllocated)
            _callbackHandle.Free();
        base.Dispose();
        GC.SuppressFinalize(this);
    }


    public List<short> GetData() => _buffer;

    public void CopyData(List<short> dest)
    {
        lock (_lock)
        {
            dest.Clear();
            dest.AddRange(_buffer);
        }
    }

    public void ClearData()
    {
        lock (_lock)
            _buffer.Clear();
    }

    public void Update(ReadOnlySpan<byte> rawData, int rawDataLen)
    {
        if (!_enabled)
            return;

        var samples = MemoryMarshal.Cast<byte, short>(rawData[..rawDataLen]);
        var sampleCount = rawDataLen / sizeof(short);
        if (_wakeThreshold != uint.MaxValue)
        {
            var needsWake = false;
            for (var i = 0; i < sampleCount; ++i)
                needsWake |= SystemMath.Abs((int)samples[i]) > _wakeThreshold;

            if (!needsWake)
            {
                _isSleeping = true;
                return;
            }

            _isSleeping = false;
        }

        lock (_lock)
        {
            for (var i = 0; i < sampleCount; ++i)
                _buffer.Add(samples[i]);
            if (_linkedStream is not null)
                _linkedStream.AddData(rawData[..rawDataLen].ToArray(), (uint)rawDataLen);
            _isDirty = true;
            _isSleeping = false;
        }
    }

    public uint GetFrequency() => _frequency;
    public string GetName() => _name;
    public bool IsEnabled() => _enabled && _deviceId != 0;
    public uint GetWakeThreshold() => _wakeThreshold;
    public void SetWakeThreshold(uint value) => _wakeThreshold = value;
    public bool IsSleeping() => _isSleeping;

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled)
            return;

        lock (_lock)
        {
            if (enabled)
                _buffer.Clear();
            _enabled = enabled;

            if (enabled)
            {
                var eventData = new AudioEvents.RecordingStartedEvent { Microphone = this };
                SendEvent(ref eventData);
            }

            SdlNativeBindings.SdlPauseAudioDevice(_deviceId, enabled ? 0 : 1);

            if (!enabled)
            {
                if (enabled)
                {
                    var eventData = new AudioEvents.RecordingEndedEvent
                    {
                        Microphone = this,
                        DataLength = _buffer.Count,
                        ClearData = false
                    };
                    SendEvent(ref eventData);
                    if (eventData.ClearData)
                        _buffer.Clear();
                }
            }
        }
    }

    public BufferedSoundStream? GetLinked() => _linkedStream;

    public void Link(BufferedSoundStream? stream)
    {
        _linkedStream = stream;
        if (_linkedStream != null)
            _linkedStream.SetFormat(_frequency, sixteenBit: true, stereo: false);
    }

    public void Unlink() => _linkedStream = null;

    internal void Init(string name, uint deviceId, int bufferSize, uint frequency, uint which)
    {
        lock (_lock)
        {
            _name = name;
            _deviceId = deviceId;
            _which = which;
            _frequency = frequency;
            if (bufferSize > 0)
                _buffer.Capacity = SystemMath.Max(_buffer.Capacity, bufferSize);
            SetEnabled(true);
        }
    }

    internal void CheckDirtiness()
    {
        if (!_enabled)
            return;

        lock (_lock)
        {
            if (!_isDirty)
                return;
            var eventData = new AudioEvents.RecordingUpdatedEvent
            {
                Microphone = this,
                DataLength = _buffer.Count,
                ClearData = false
            };
            SendEvent(ref eventData);
            if (eventData.ClearData)
                _buffer.Clear();
            _isDirty = false;
        }
    }

    internal IntPtr CallbackUserData => GCHandle.ToIntPtr(_callbackHandle);
}
