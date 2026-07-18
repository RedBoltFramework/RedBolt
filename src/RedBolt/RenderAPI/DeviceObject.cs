namespace RedBolt;

/// <summary>
/// Base class for GPU resources.
/// </summary>
public abstract class DeviceObject : Object
{
    private RenderDevice? _renderDevice;
    /// <summary>
    /// Debug name of the object.
    /// </summary>
    private string _debugName = string.Empty;
    /// <summary>
    /// Android only: whether the data is lost due to context loss.
    /// </summary>
    private bool _dataLost;

    protected DeviceObject(DeviceObjectFlag flags = DeviceObjectFlag.None)
    {
        Flags = flags;
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

    public DeviceObjectFlag Flags { get; }

    /// <summary>
    /// Invalidate GPU data.
    /// </summary>
    public virtual void Invalidate()
    {
        _dataLost = true;
    }

    /// <summary>
    /// Restore GPU data if possible.
    /// </summary>
    public virtual void Restore()
    {
        _dataLost = false;
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

    protected void SetDataLost(bool dataLost = true)
    {
        _dataLost = dataLost;
    }

    public string GetDebugName() => _debugName;

    public void SetDebugName(string debugName)
    {
        this._debugName = debugName ?? string.Empty;
    }

    /// <summary>
    /// Internal: process device object event.
    /// </summary>
    internal void ProcessDeviceObjectEvent(DeviceObjectEvent deviceObjectEvent)
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
                _renderDevice?.RemoveDeviceObject(this);
                _renderDevice = null;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(deviceObjectEvent), deviceObjectEvent, null);
        }
    }

    /// <summary>
    /// Helper function to restore another object if present.
    /// </summary>
    protected void RestoreDependency(DeviceObject? dependency)
    {
        dependency?.Restore();
    }
}
