// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.RenderAPI;

using RedBolt.Core;

/// <summary>
/// Base class for GPU resources.
/// </summary>
public class DeviceObject : RedObject
{
    public DeviceObject(DeviceObjectFlag flags = DeviceObjectFlag.None)
    {
        if ((flags & DeviceObjectFlag.Headless) == 0)
        {
            _renderDevice = GetSubsystem<RenderDevice>();
            _renderDevice?.AddDeviceObject(this);
        }
    }

    public override void Dispose()
    {
        _renderDevice?.RemoveDeviceObject(this);
        _renderDevice = null;
        base.Dispose();
    }

    /// <summary>
    /// Invalidate GPU data.
    /// </summary>
    public virtual void Invalidate()
    {
    }

    /// <summary>
    /// Restore GPU data if possible.
    /// </summary>
    public virtual void Restore()
    {
    }

    /// <summary>
    /// Destroy all GPU data on RenderDevice shutdown.
    /// </summary>
    public virtual void Destroy()
    {
    }

    public RenderDevice? GetRenderDevice() => _renderDevice;

    public bool IsDataLost() => _dataLost;

    public void ClearDataLost()
    {
        _dataLost = false;
    }

    public string GetDebugName() => _debugName;

    public void SetDebugName(string debugName)
    {
        _debugName = debugName;
    }

    /// <summary>
    /// Internal: process device object event.
    /// </summary>
    public void ProcessDeviceObjectEvent(DeviceObjectEvent deviceObjectEvent)
    {
        switch (deviceObjectEvent)
        {
            case DeviceObjectEvent.Invalidate:
                _dataLost = true;
                Invalidate();
                break;

            case DeviceObjectEvent.Restore:
                Restore();
                break;

            case DeviceObjectEvent.Destroy:
                Destroy();
                _renderDevice = null;
                break;
        }
    }

    /// <summary>
    /// Helper function to restore another object if present.
    /// </summary>
    protected void RestoreDependency(DeviceObject? dependency)
    {
        dependency?.Restore();
    }

    /// <summary>
    /// Render device that owns this object.
    /// </summary>
    protected RenderDevice? _renderDevice;
    /// <summary>
    /// Debug name of the object.
    /// </summary>
    protected string _debugName = string.Empty;
    /// <summary>
    /// Android only: whether the data is lost due to context loss.
    /// </summary>
    protected bool _dataLost;
}
