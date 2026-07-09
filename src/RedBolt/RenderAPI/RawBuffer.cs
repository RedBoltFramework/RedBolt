using System.Runtime.InteropServices;
using RedBolt.Bindings.Diligent;

namespace RedBolt;

/// <summary>Common class for buffer on GPU and/or CPU.</summary>
public class RawBuffer : DeviceObject
{
    private IBuffer? _handle;
    private RawBufferParams _parameters;
    private byte[] _shadowData = [];
    private bool _locked;
    private bool _needsResolve;

    public RawBuffer(DeviceObjectFlag flags = DeviceObjectFlag.None)
        : base(flags)
    {
    }

    public RawBuffer(RawBufferParams parameters, byte[]? data = null, DeviceObjectFlag flags = DeviceObjectFlag.None)
        : this(flags)
    {
        if (!Create(parameters, data))
            throw new InvalidOperationException("Failed to create RawBuffer.");
    }

    public override void Invalidate()
    {
        base.Invalidate();
        ReleaseIfNotNull(_handle);
        _handle = null;
    }

    public override void Restore()
    {
        base.Restore();
        if (_parameters.Size == 0)
        {
            ClearDataLost();
            return;
        }

        if (!CreateGPU(ShouldCreateWithInitialData() ? _shadowData : null))
        {
            Invalidate();
            return;
        }

        if (!ShouldCreateWithInitialData() && _shadowData.Length != 0)
            UploadRange(_shadowData, 0, _parameters.Size);

        ClearDataLost();
    }

    public override void Destroy()
    {
        base.Destroy();
        ReleaseIfNotNull(_handle);
        _handle = null;
        _shadowData = [];
        _locked = false;
        _needsResolve = false;
    }

    /// <summary>Create buffer. If data is provided, it should contain exactly size bytes.</summary>
    public bool Create(RawBufferParams parameters, byte[]? data = null)
    {
        Destroy();

        if ((parameters.Flags & BufferFlag.Dynamic) != 0 && (parameters.Flags & BufferFlag.Immutable) != 0)
            return false;
        if ((parameters.Flags & BufferFlag.BindUnorderedAccess) != 0 && (parameters.Flags & BufferFlag.Dynamic) != 0)
            return false;
        if (data is not null && data.Length != parameters.Size)
            return false;

        this._parameters = parameters;
        if (parameters.Size == 0)
        {
            ClearDataLost();
            return true;
        }

        var keepShadowData = (parameters.Flags & (BufferFlag.Shadowed | BufferFlag.Dynamic | BufferFlag.Immutable)) != 0 || data is not null;
        if (keepShadowData)
        {
            _shadowData = new byte[parameters.Size];
            if (data is not null)
                Buffer.BlockCopy(data, 0, _shadowData, 0, data.Length);
        }

        if (CreateGPU(ShouldCreateWithInitialData() ? _shadowData : null))
        {
            if (!ShouldCreateWithInitialData() && data is not null)
                UploadRange(data, 0, parameters.Size);
            ClearDataLost();
        }
        else if (_shadowData.Length != 0)
            ClearDataLost();

        return true;
    }

    /// <summary>Update buffer data. If size is not set, the buffer size is used.</summary>
    public void Update(byte[] data, uint size = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        var dataSize = size != 0 ? size : _parameters.Size;
        if (dataSize > data.Length)
            throw new ArgumentOutOfRangeException(nameof(size));

        UpdateRange(data, 0, dataSize);
    }

    /// <summary>Update buffer data range.</summary>
    public void UpdateRange(byte[] data, uint offset, uint size)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (_locked)
            throw new InvalidOperationException("Cannot update RawBuffer while it is mapped.");
        if (offset + size > _parameters.Size || size > data.Length)
            throw new ArgumentOutOfRangeException(nameof(size));
        if (size == 0)
            return;

        if (_shadowData.Length != 0)
            Buffer.BlockCopy(data, 0, _shadowData, (int)offset, (int)size);

        if ((_parameters.Flags & BufferFlag.Immutable) != 0)
        {
            RecreateImmutableBuffer();
            return;
        }

        UploadRange(data, offset, size);
    }

    /// <summary>Map buffer contents onto CPU-writeable memory. Old buffer data is discarded.
    /// Memory is not guaranteed to be CPU-readable — consider using GetShadowData() if you need to read the data.</summary>
    public Memory<byte> Map()
    {
        if (_locked)
            throw new InvalidOperationException("RawBuffer is already mapped.");
        if (_shadowData.Length == 0)
            _shadowData = new byte[_parameters.Size];

        _locked = true;
        return _shadowData;
    }

    /// <summary>Unmap the buffer. Should always be called before buffer is used or frame ends.</summary>
    public void Unmap()
    {
        if (!_locked)
            return;

        _locked = false;
        if ((_parameters.Flags & BufferFlag.Immutable) != 0)
            RecreateImmutableBuffer();
        else if (_shadowData.Length != 0)
            UploadRange(_shadowData, 0, _parameters.Size);
    }

    /// <summary>For dynamic buffers, ensure that the buffer can be used in this frame.
    /// Don't access the GPU buffer data until the buffer was resolved!</summary>
    public void Resolve()
    {
        if (!_needsResolve || _shadowData.Length == 0)
            return;

        UploadRange(_shadowData, 0, _parameters.Size);
    }

    public BufferType GetBufferType() => _parameters.Type;

    public uint GetSize() => _parameters.Size;

    public uint GetStride() => _parameters.Stride;

    public BufferFlag GetFlags() => _parameters.Flags;

    public bool IsLocked() => _locked;

    public bool IsShadowed() => _shadowData.Length != 0;

    public byte[] GetShadowData() => _shadowData;

    public IBuffer? GetHandle() => _handle;

    private bool CreateGPU(byte[]? data)
    {
        ReleaseIfNotNull(_handle);
        _handle = null;

        var graphics = GetSubsystem<Graphics>();
        var device = graphics?.GetNativeRenderDevice();
        if (device is null || device.IsNull)
            return false;

        using var nativeName = new PinnedUtf8(GetDebugName());
        using var bufferDesc = new BufferDesc
        {
            Name = nativeName.Pointer,
            Size = _parameters.Size,
            BindFlags = GetBindFlags(_parameters.Type, _parameters.Flags),
            Usage = GetUsage(_parameters.Flags),
            CPUAccessFlags = GetCpuAccessFlags(_parameters.Flags),
            ElementByteStride = _parameters.Stride,
            Mode = BufferMode.BufferModeUndefined
        };

        if (data is null)
        {
            device.CreateBuffer(bufferDesc, null!, out _handle);
        }
        else
        {
            var immediateContext = graphics?.GetImmediateContext();
            var dataHandle = default(GCHandle);
            try
            {
                dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
                using var bufferData = new BufferData(dataHandle.AddrOfPinnedObject(), _parameters.Size, immediateContext);
                device.CreateBuffer(bufferDesc, bufferData, out _handle);
            }
            finally
            {
                if (dataHandle.IsAllocated)
                    dataHandle.Free();
            }
        }

        if (_handle is { IsNull: true })
            _handle = null;

        return _handle is not null;
    }

    private void UploadRange(byte[] data, uint offset, uint size)
    {
        if (size == 0)
            return;

        if (_handle is null || _handle.IsNull)
        {
            if (!CreateGPU(null))
            {
                _needsResolve = true;
                return;
            }
        }

        var immediateContext = GetSubsystem<Graphics>()?.GetImmediateContext();
        if (immediateContext is null || immediateContext.IsNull)
        {
            _needsResolve = true;
            return;
        }

        var dataHandle = default(GCHandle);
        try
        {
            dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            var pointer = IntPtr.Add(dataHandle.AddrOfPinnedObject(), (int)offset);
            immediateContext.UpdateBuffer(_handle!, offset, size, pointer, ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
            _needsResolve = false;
            ClearDataLost();
        }
        finally
        {
            if (dataHandle.IsAllocated)
                dataHandle.Free();
        }
    }

    private void RecreateImmutableBuffer()
    {
        if (_shadowData.Length == 0)
            return;

        if (!CreateGPU(_shadowData))
            _needsResolve = true;
        else
        {
            _needsResolve = false;
            ClearDataLost();
        }
    }

    private bool ShouldCreateWithInitialData() => (_parameters.Flags & (BufferFlag.Immutable | BufferFlag.Dynamic)) == BufferFlag.Immutable;

    private static BindFlags GetBindFlags(BufferType type, BufferFlag flags)
    {
        var bindFlags = type switch
        {
            BufferType.Vertex => BindFlags.BindVertexBuffer,
            BufferType.Index => BindFlags.BindIndexBuffer,
            BufferType.Uniform => BindFlags.BindUniformBuffer,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        if ((flags & BufferFlag.BindUnorderedAccess) != 0)
            bindFlags |= BindFlags.BindUnorderedAccess;
        return bindFlags;
    }

    private static Usage GetUsage(BufferFlag flags)
    {
        if ((flags & BufferFlag.Dynamic) != 0)
            return Usage.UsageDynamic;
        if ((flags & BufferFlag.Immutable) != 0)
            return Usage.UsageImmutable;
        return Usage.UsageDefault;
    }

    private static CpuAccessFlags GetCpuAccessFlags(BufferFlag flags)
    {
        return (flags & BufferFlag.Dynamic) != 0 ? CpuAccessFlags.CpuAccessWrite : CpuAccessFlags.CpuAccessNone;
    }

    private static void ReleaseIfNotNull(IObject? value)
    {
        if (value is not null && !value.IsNull)
            _ = value.Release();
    }
}
