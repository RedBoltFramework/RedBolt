// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.RenderAPI;

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Diligent;
using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.Logging;

/// <summary>Common class for buffer on GPU and/or CPU.</summary>
public class RawBuffer : DeviceObject
{
    private RawBufferParams _parameters;

    private IBuffer? _handle;
    private Usage _internalUsage;
    private byte[] _shadowData = [];

    private bool _needsResolve;
    private FrameIndex? _lastUpdateFrameIndex;

    private MapKind _mapKind;
    private nint _mappedScratchBuffer;

    private enum MapKind
    {
        None,
        Shadow,
        Dynamic,
        Scratch
    }

    public RawBuffer(RawBufferParams parameters, byte[]? data = null, DeviceObjectFlag flags = DeviceObjectFlag.None)
        : base(flags)
    {
        Create(parameters, data);
    }

    public override void Dispose()
    {
        Destroy();
        base.Dispose();
    }

    public override void Invalidate()
    {
        Debug.Assert(!IsLocked());

        ReleaseIfNotNull(_handle);
        _handle = null;
    }

    public override void Restore()
    {
        Debug.Assert(!IsLocked());

        if (_parameters.Size == 0)
        {
            _dataLost = false;
        }
        else if (_shadowData.Length != 0)
        {
            var isDynamic = (_parameters.Flags & BufferFlag.Dynamic) != 0;
            if ((_parameters.Flags & BufferFlag.Dynamic) != 0)
            {
                CreateGPU(default);
                Update(_shadowData);
            }
            else
            {
                CreateGPU(_shadowData);
            }
            _dataLost = false;
        }
        else
        {
            CreateGPU(default);
            _dataLost = true;
        }
    }

    public override void Destroy()
    {
        Debug.Assert(!IsLocked());

        ReleaseIfNotNull(_handle);
        _handle = null;
        _shadowData = [];
    }

    /// <summary>Update buffer data. If size is not set, the buffer size is used.</summary>
    public void Update(byte[] data, int size = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        Update(data.AsSpan(), size);
    }

    /// <summary>Update buffer data without allocating. If size is not set, the buffer size is used.</summary>
    public void Update(ReadOnlySpan<byte> data, int size = 0)
    {
        var dataSize = size != 0 ? size : _parameters.Size;
        if (dataSize != 0)
            UpdateRange(data, 0, dataSize);
    }

    /// <summary>Update buffer data range.</summary>
    public void UpdateRange(byte[] data, int offset, int size)
    {
        ArgumentNullException.ThrowIfNull(data);
        UpdateRange(data.AsSpan(), offset, size);
    }

    /// <summary>Update buffer data range without allocating.</summary>
    public unsafe void UpdateRange(ReadOnlySpan<byte> data, int offset, int size)
    {
        Debug.Assert(!IsLocked());
        Debug.Assert(!data.IsEmpty || size == 0, "Data must not be null");
        Debug.Assert(offset + size <= _parameters.Size, "Range must be within buffer size");
        Debug.Assert(
            offset == 0 || ((_parameters.Flags & BufferFlag.Dynamic) == 0 && (_parameters.Flags & BufferFlag.Immutable) == 0),
            "Dynamic and immutable buffers cannot be partially updated");
        Debug.Assert(size <= data.Length, "Data must contain the updated range");

        if ((_parameters.Flags & BufferFlag.Immutable) != 0 && _renderDevice?.GetRenderDevice() is not null && _handle is not null)
        {
            Log.GetLogger().LogWarning(
                "Recreating immutable buffer '{DebugName}' due to RawBuffer::UpdateRange call", GetDebugName());
        }

        if (size == 0)
        {
            Log.GetLogger().LogWarning(
                "RawBuffer::UpdateRange is called with zero size for buffer '{DebugName}'", GetDebugName());
            return;
        }

        if ((_parameters.Flags & BufferFlag.Shadowed) != 0)
        {
            var shadowData = _shadowData.AsSpan(offset, size);
            ref var source = ref MemoryMarshal.GetReference(data);
            ref var destination = ref MemoryMarshal.GetReference(shadowData);
            if (!Unsafe.AreSame(ref source, ref destination))
                Unsafe.CopyBlockUnaligned(ref destination, ref source, checked((uint)size));
        }

        var renderDevice = _renderDevice?.GetRenderDevice();
        if (renderDevice is not null)
        {
            var immediateContext = GetSubsystem<Graphics>()!.GetImmediateContext()!;
            fixed (byte* dataPointer = data)
            {
                if (_internalUsage == Usage.UsageDynamic)
                {
                    var gpuBuffer = IntPtr.Zero;
                    immediateContext.MapBuffer(_handle!, MapType.MapWrite, MapFlags.MapFlagDiscard, ref gpuBuffer);
                    if (gpuBuffer != IntPtr.Zero)
                    {
                        Buffer.MemoryCopy(dataPointer, (void*)gpuBuffer, size, size);
                        immediateContext.UnmapBuffer(_handle!, MapType.MapWrite);
                    }
                }
                else if (_internalUsage == Usage.UsageImmutable)
                {
                    CreateGPU(data);
                }
                else
                {
                    immediateContext.UpdateBuffer(_handle!, checked((nuint)offset), checked((nuint)size),
                        (IntPtr)dataPointer, ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
                }
            }

            _lastUpdateFrameIndex = GetRenderDevice()!.GetFrameIndex();
        }

        ClearDataLost();
    }

    /// <summary>Map buffer contents onto CPU-writeable memory. Return data pointer if successful. Old buffer data is discarded.</summary>
    /// <remarks>Memory is not guaranteed to be CPU-readable! Consider using GetShadowData() if you need to read the data.</remarks>
    public unsafe Span<byte> Map()
    {
        Debug.Assert(!IsLocked());

        if (_parameters.Size == 0)
        {
            Log.GetLogger().LogWarning("RawBuffer::Map is called for empty buffer '{DebugName}'", GetDebugName());
            return Span<byte>.Empty;
        }

        // If shadowed, return shadow data and upload it on unlock
        if ((_parameters.Flags & BufferFlag.Shadowed) != 0)
        {
            _mapKind = MapKind.Shadow;
            return _shadowData;
        }

        // If hardware dynamic buffer, map it
        if (_internalUsage == Usage.UsageDynamic)
        {
            var immediateContext = GetSubsystem<Graphics>()!.GetImmediateContext();
            Debug.Assert(immediateContext is not null);

            var gpuBuffer = IntPtr.Zero;
            immediateContext!.MapBuffer(_handle!, MapType.MapWrite, MapFlags.MapFlagDiscard, ref gpuBuffer);
            if (gpuBuffer == IntPtr.Zero)
                return Span<byte>.Empty;

            _mapKind = MapKind.Dynamic;
            return new Span<byte>((void*)gpuBuffer, _parameters.Size);
        }

        // If hardware static buffer, return temporary buffer
        var renderPool = GetRenderDevice()!.GetRenderPool();

        _mappedScratchBuffer = renderPool.AllocateScratchBuffer(_parameters.Size);
        _mapKind = MapKind.Scratch;
        return new Span<byte>((void*)_mappedScratchBuffer, _parameters.Size);
    }

    /// <summary>Unmap the buffer. Should always be called before buffer is used or frame ends.</summary>
    public unsafe void Unmap()
    {
        Debug.Assert(IsLocked());
        var mapKind = _mapKind;
        _mapKind = MapKind.None;

        switch (mapKind)
        {
            case MapKind.Shadow:
                Update(_shadowData);
                break;
            case MapKind.Dynamic:
                GetSubsystem<Graphics>()!.GetImmediateContext()!.UnmapBuffer(_handle!, MapType.MapWrite);
                break;
            case MapKind.Scratch:
            {
                var renderPool = GetRenderDevice()!.GetRenderPool();
                try
                {
                    Update(new ReadOnlySpan<byte>((void*)_mappedScratchBuffer, _parameters.Size));
                }
                finally
                {
                    renderPool.ReleaseScratchBuffer(_mappedScratchBuffer);
                    _mappedScratchBuffer = 0;
                }
                break;
            }
        }

        if (_renderDevice?.GetRenderDevice() is not null)
            _lastUpdateFrameIndex = GetRenderDevice()!.GetFrameIndex();

        ClearDataLost();
    }

    /// <summary>For dynamic buffers, ensure that the buffer can be used in this frame.</summary>
    /// <remarks>Don't access the GPU buffer data until the buffer was resolved!</remarks>
    public void Resolve()
    {
        if (!_needsResolve)
            return;
        if (_lastUpdateFrameIndex != GetRenderDevice()!.GetFrameIndex())
            Update(_shadowData);
    }

    public BufferType GetBufferType() => _parameters.Type;

    public int GetSize() => _parameters.Size;

    public int GetStride() => _parameters.Stride;

    public BufferFlag GetFlags() => _parameters.Flags;

    public bool IsLocked() => _mapKind != MapKind.None;

    public bool IsShadowed() => (_parameters.Flags & BufferFlag.Shadowed) != 0;

    public byte[] GetShadowData() => _shadowData;

    public byte[] GetShadowDataShared() => _shadowData;

    public IBuffer? GetHandle() => _handle;

    protected RawBuffer(DeviceObjectFlag flags = DeviceObjectFlag.None)
        : base(flags)
    {
    }

    /// <summary>Create buffer. If data is provided, it should contain exactly size bytes.</summary>
    protected bool Create(RawBufferParams parameters, byte[]? data = null)
    {
        Destroy();

        _parameters = parameters;
        _needsResolve = false;
        if (_parameters.Size == 0)
            return true;

        // TODO: This is a workaround for a bug in Vulkan driver, maybe it should be configured in real-time.
        const bool disableDefaultBuffers = false;

        if (disableDefaultBuffers && (_parameters.Flags & BufferFlag.Dynamic) == 0)
            _parameters = _parameters with { Flags = _parameters.Flags | BufferFlag.Immutable };

        if ((_parameters.Flags & BufferFlag.Dynamic) != 0 && (_parameters.Flags & BufferFlag.Immutable) != 0)
            throw new ArgumentException("Dynamic buffer cannot be immutable.", nameof(parameters));

        var renderDevice = _renderDevice?.GetRenderDevice();
        if (renderDevice is null)
        {
            // If there's no render device, buffer must be shadowed
            _parameters = _parameters with { Flags = _parameters.Flags | BufferFlag.Shadowed };
        }
        else
        {
            // If buffer is dynamic, next-gen backend is used, and Discard is not set, shadow buffer is required
            var backend = GetRenderDevice()!.GetBackend();
            var isNextGen = backend != RenderBackend.D3D11 && backend != RenderBackend.OpenGL;
            if (isNextGen && (_parameters.Flags & BufferFlag.Dynamic) != 0 && (_parameters.Flags & BufferFlag.Discard) == 0)
            {
                _parameters = _parameters with { Flags = _parameters.Flags | BufferFlag.Shadowed };
                _needsResolve = true;
            }
            else if ((_parameters.Flags & BufferFlag.Immutable) != 0)
            {
                // Immutable buffer is always shadowed
                _parameters = _parameters with { Flags = _parameters.Flags | BufferFlag.Shadowed };
            }
        }

        // Dynamic buffer cannot be bound as UAV
        if ((_parameters.Flags & BufferFlag.BindUnorderedAccess) != 0 && (_parameters.Flags & BufferFlag.Dynamic) != 0)
            throw new ArgumentException("Dynamic buffer cannot be bound as UAV.", nameof(parameters));

        // Dynamic buffer cannot have initial data
        if ((_parameters.Flags & BufferFlag.Dynamic) != 0 && data is not null)
            throw new ArgumentException("Dynamic buffer cannot have initial data.", nameof(data));

        // Dynamic buffers on OpenGL are weird, don't use them
        if (renderDevice is not null && GetRenderDevice()!.GetBackend() == RenderBackend.OpenGL)
            _parameters = _parameters with { Flags = _parameters.Flags & ~BufferFlag.Dynamic };

        // Create CPU buffer
        if ((_parameters.Flags & BufferFlag.Shadowed) != 0)
        {
            _shadowData = new byte[_parameters.Size];
            if (data is not null)
                data.AsSpan(0, _parameters.Size).CopyTo(_shadowData);
        }

        // Create GPU buffer, postpone if Immutable and no data
        if (renderDevice is not null && ((_parameters.Flags & BufferFlag.Immutable) == 0 || data is not null))
        {
            if (!CreateGPU(data))
                return false;
        }

        ClearDataLost();
        return true;
    }

    private unsafe bool CreateGPU(ReadOnlySpan<byte> data)
    {
        using var nativeName = new CString(GetDebugName());
        using var bufferDesc = new BufferDesc
        {
            Name = nativeName.Pointer,
            BindFlags = GetBindFlags(_parameters.Type, _parameters.Flags),
            Usage = (_parameters.Flags & BufferFlag.Immutable) != 0 ? Usage.UsageImmutable : Usage.UsageDefault,
            CPUAccessFlags = CpuAccessFlags.CpuAccessNone,
            Mode = BufferMode.BufferModeUndefined,
            Size = checked((nuint)_parameters.Size),
            ElementByteStride = checked((uint)_parameters.Stride)
        };

        if (GetRenderDevice()!.GetBackend() != RenderBackend.OpenGL && (_parameters.Flags & BufferFlag.Dynamic) != 0)
        {
            bufferDesc.Usage = Usage.UsageDynamic;
            bufferDesc.CPUAccessFlags = CpuAccessFlags.CpuAccessWrite;
        }
        _internalUsage = bufferDesc.Usage;

        var graphics = GetSubsystem<Graphics>()!;
        var device = _renderDevice!.GetRenderDevice()!;
        var immediateContext = graphics.GetImmediateContext();

        ReleaseIfNotNull(_handle);
        _handle = null;
        if (data.IsEmpty)
        {
            device.CreateBuffer(bufferDesc, null!, out _handle);
        }
        else
        {
            fixed (byte* dataPointer = data)
            {
                using var bufferData = new BufferData((IntPtr)dataPointer, checked((nuint)_parameters.Size), immediateContext);
                device.CreateBuffer(bufferDesc, bufferData, out _handle);
            }
        }

        if (_handle is null || _handle.IsNull)
        {
            _handle = null;
            Log.GetLogger().LogError(
                "Failed to create buffer: type={Type} size={Size} stride={Stride} flags=0b{Flags}",
                _parameters.Type, _parameters.Size, _parameters.Stride, Convert.ToString((int)_parameters.Flags, 2));
            return false;
        }

        return true;
    }

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

    private static void ReleaseIfNotNull(IObject? value)
    {
        if (value is not null && !value.IsNull)
            _ = value.Release();
    }
}
