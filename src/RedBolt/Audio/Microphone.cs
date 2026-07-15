using RedBolt.Bindings.Sdl;

namespace RedBolt;

/// <summary>Microphone audio input device.</summary>
public sealed class Microphone : Object
{
    private BufferedSoundStream? _linkedStream;
    private string _name = string.Empty;
    private readonly List<short> _buffer = [];
    private readonly object _lock = new();
    private uint _frequency;
    private uint _wakeThreshold = uint.MaxValue;
    private bool _enabled;
    private bool _isSleeping;
    private bool _isDirty;
    private uint _deviceId;
    private uint _which;

    public Microphone()
    {
    }

    public override void Dispose()
    {
        CloseNative();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    public short[] GetData()
    {
        lock (_lock)
            return _buffer.ToArray();
    }

    public void CopyData(short[] dest)
    {
        ArgumentNullException.ThrowIfNull(dest);
        lock (_lock)
            _buffer.CopyTo(dest, 0);
    }

    public void ClearData()
    {
        lock (_lock)
            _buffer.Clear();
    }

    public void Update(byte[] rawData, int rawDataLen)
    {
        if (!_enabled || rawDataLen == 0)
            return;

        var sampleCount = rawDataLen / sizeof(short);
        if (_wakeThreshold != uint.MaxValue)
        {
            var needsWake = false;
            for (var i = 0; i < sampleCount; ++i)
            {
                var sample = BitConverter.ToInt16(rawData, i * sizeof(short));
                if (Math.Abs((int)sample) > _wakeThreshold)
                {
                    needsWake = true;
                    break;
                }
            }

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
                _buffer.Add(BitConverter.ToInt16(rawData, i * sizeof(short)));
            _linkedStream?.AddData(rawData, (uint)rawDataLen);
            _isDirty = true;
            _isSleeping = false;
        }
    }

    public uint GetFrequency() => _frequency;
    public string GetName() => _name;
    public bool IsEnabled() => _enabled;
    public uint GetWakeThreshold() => _wakeThreshold;
    public void SetWakeThreshold(uint value) => _wakeThreshold = value;
    public bool IsSleeping() => _isSleeping;

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled)
            return;

        if (enabled)
            ClearData();
        _enabled = enabled;

        if (enabled)
        {
            var eventData = new AudioEvents.RecordingStartedEvent { Microphone = this };
            SendEvent(ref eventData);
        }

        SdlNativeBindings.SdlPauseAudioDevice(_deviceId, enabled ? 0 : 1);
    }

    public BufferedSoundStream? GetLinked() => _linkedStream;

    public void Link(BufferedSoundStream? stream)
    {
        _linkedStream = stream;
        if (_linkedStream != null)
            _linkedStream.SetFormat(_frequency, sixteenBit: true, stereo: false);
    }

    public void Unlink() => _linkedStream = null;

    internal void InitializeNative(string name, uint deviceId, int bufferSize, uint frequency, uint which)
    {
        _name = name;
        _deviceId = deviceId;
        _which = which;
        _frequency = frequency;
        if (bufferSize > 0)
        {
            lock (_lock)
                _buffer.Capacity = Math.Max(_buffer.Capacity, bufferSize);
        }
        SetEnabled(true);
    }

    internal uint GetDeviceIndex() => _which;

    internal void SetDeviceIndex(uint which) => _which = which;

    internal void CloseNative()
    {
        if (_deviceId != 0)
        {
            SdlNativeBindings.SdlCloseAudioDevice(_deviceId);
            _deviceId = 0;
        }
    }

    internal void UpdateNative()
    {
        if (!_enabled || _deviceId == 0)
            return;

        var queuedSize = SdlNativeBindings.SdlGetQueuedAudioSize(_deviceId);
        if (queuedSize == 0)
            return;

        var rawData = new byte[queuedSize];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(rawData, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            var read = SdlNativeBindings.SdlDequeueAudio(_deviceId, handle.AddrOfPinnedObject(), queuedSize);
            if (read != 0)
                Update(rawData, checked((int)read));
        }
        finally
        {
            handle.Free();
        }
    }

    internal void CheckDirtiness()
    {
        if (!_enabled || !_isDirty)
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
}
