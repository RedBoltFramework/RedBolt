using System.Runtime.InteropServices;
using RedBolt.Bindings.Diligent;

namespace RedBolt;

/// <summary>Common class for buffer on GPU and/or CPU.</summary>
public class RawBuffer : DeviceObject
{
    private IBuffer? handle;
    private RawBufferParams parameters;
    private byte[] shadowData = [];
    private bool locked;
    private bool needsResolve;

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
        ReleaseIfNotNull(handle);
        handle = null;
    }

    public override void Restore()
    {
        base.Restore();
        if (parameters.Size == 0)
        {
            ClearDataLost();
            return;
        }

        if (!CreateGPU(ShouldCreateWithInitialData() ? shadowData : null))
        {
            Invalidate();
            return;
        }

        if (!ShouldCreateWithInitialData() && shadowData.Length != 0)
            UploadRange(shadowData, 0, parameters.Size);

        ClearDataLost();
    }

    public override void Destroy()
    {
        base.Destroy();
        ReleaseIfNotNull(handle);
        handle = null;
        shadowData = [];
        locked = false;
        needsResolve = false;
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

        this.parameters = parameters;
        if (parameters.Size == 0)
        {
            ClearDataLost();
            return true;
        }

        var keepShadowData = (parameters.Flags & (BufferFlag.Shadowed | BufferFlag.Dynamic | BufferFlag.Immutable)) != 0 || data is not null;
        if (keepShadowData)
        {
            shadowData = new byte[parameters.Size];
            if (data is not null)
                Buffer.BlockCopy(data, 0, shadowData, 0, data.Length);
        }

        if (CreateGPU(ShouldCreateWithInitialData() ? shadowData : null))
        {
            if (!ShouldCreateWithInitialData() && data is not null)
                UploadRange(data, 0, parameters.Size);
            ClearDataLost();
        }
        else if (shadowData.Length != 0)
            ClearDataLost();

        return true;
    }

    /// <summary>Update buffer data. If size is not set, the buffer size is used.</summary>
    public void Update(byte[] data, uint size = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        var dataSize = size != 0 ? size : parameters.Size;
        if (dataSize > data.Length)
            throw new ArgumentOutOfRangeException(nameof(size));

        UpdateRange(data, 0, dataSize);
    }

    /// <summary>Update buffer data range.</summary>
    public void UpdateRange(byte[] data, uint offset, uint size)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (locked)
            throw new InvalidOperationException("Cannot update RawBuffer while it is mapped.");
        if (offset + size > parameters.Size || size > data.Length)
            throw new ArgumentOutOfRangeException(nameof(size));
        if (size == 0)
            return;

        if (shadowData.Length != 0)
            Buffer.BlockCopy(data, 0, shadowData, (int)offset, (int)size);

        if ((parameters.Flags & BufferFlag.Immutable) != 0)
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
        if (locked)
            throw new InvalidOperationException("RawBuffer is already mapped.");
        if (shadowData.Length == 0)
            shadowData = new byte[parameters.Size];

        locked = true;
        return shadowData;
    }

    /// <summary>Unmap the buffer. Should always be called before buffer is used or frame ends.</summary>
    public void Unmap()
    {
        if (!locked)
            return;

        locked = false;
        if ((parameters.Flags & BufferFlag.Immutable) != 0)
            RecreateImmutableBuffer();
        else if (shadowData.Length != 0)
            UploadRange(shadowData, 0, parameters.Size);
    }

    /// <summary>For dynamic buffers, ensure that the buffer can be used in this frame.
    /// Don't access the GPU buffer data until the buffer was resolved!</summary>
    public void Resolve()
    {
        if (!needsResolve || shadowData.Length == 0)
            return;

        UploadRange(shadowData, 0, parameters.Size);
    }

    public BufferType GetBufferType() => parameters.Type;

    public uint GetSize() => parameters.Size;

    public uint GetStride() => parameters.Stride;

    public BufferFlag GetFlags() => parameters.Flags;

    public bool IsLocked() => locked;

    public bool IsShadowed() => shadowData.Length != 0;

    public byte[] GetShadowData() => shadowData;

    public IBuffer? GetHandle() => handle;

    private bool CreateGPU(byte[]? data)
    {
        ReleaseIfNotNull(handle);
        handle = null;

        var graphics = GetSubsystem<Graphics>();
        var device = graphics?.GetNativeRenderDevice();
        if (device is null || device.IsNull)
            return false;

        using var nativeName = new PinnedUtf8(GetDebugName());
        using var bufferDesc = new BufferDesc
        {
            Name = nativeName.Pointer,
            Size = parameters.Size,
            BindFlags = GetBindFlags(parameters.Type, parameters.Flags),
            Usage = GetUsage(parameters.Flags),
            CPUAccessFlags = GetCpuAccessFlags(parameters.Flags),
            ElementByteStride = parameters.Stride,
            Mode = BufferMode.BufferModeUndefined
        };

        if (data is null)
        {
            device.CreateBuffer(bufferDesc, null!, out handle);
        }
        else
        {
            var immediateContext = graphics?.GetImmediateContext();
            var dataHandle = default(GCHandle);
            try
            {
                dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
                using var bufferData = new BufferData(dataHandle.AddrOfPinnedObject(), parameters.Size, immediateContext);
                device.CreateBuffer(bufferDesc, bufferData, out handle);
            }
            finally
            {
                if (dataHandle.IsAllocated)
                    dataHandle.Free();
            }
        }

        if (handle is { IsNull: true })
            handle = null;

        return handle is not null;
    }

    private void UploadRange(byte[] data, uint offset, uint size)
    {
        if (size == 0)
            return;

        if (handle is null || handle.IsNull)
        {
            if (!CreateGPU(null))
            {
                needsResolve = true;
                return;
            }
        }

        var immediateContext = GetSubsystem<Graphics>()?.GetImmediateContext();
        if (immediateContext is null || immediateContext.IsNull)
        {
            needsResolve = true;
            return;
        }

        var dataHandle = default(GCHandle);
        try
        {
            dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            var pointer = IntPtr.Add(dataHandle.AddrOfPinnedObject(), (int)offset);
            immediateContext.UpdateBuffer(handle!, offset, size, pointer, ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
            needsResolve = false;
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
        if (shadowData.Length == 0)
            return;

        if (!CreateGPU(shadowData))
            needsResolve = true;
        else
        {
            needsResolve = false;
            ClearDataLost();
        }
    }

    private bool ShouldCreateWithInitialData() => (parameters.Flags & (BufferFlag.Immutable | BufferFlag.Dynamic)) == BufferFlag.Immutable;

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
