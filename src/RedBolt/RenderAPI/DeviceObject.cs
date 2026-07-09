namespace RedBolt;

/// <summary>
/// Base class for GPU resources.
/// </summary>
public abstract class DeviceObject : Object
{
    /// <summary>
    /// Android only: whether the data is lost due to context loss.
    /// </summary>
    private bool dataLost;
    /// <summary>
    /// Debug name of the object.
    /// </summary>
    private string debugName = string.Empty;

    protected DeviceObject(DeviceObjectFlag flags = DeviceObjectFlag.None)
    {
        Flags = flags;
        if ((flags & DeviceObjectFlag.Headless) == 0)
            GetRenderDevice()?.AddDeviceObject(this);
    }

    public DeviceObjectFlag Flags { get; }

    /// <summary>
    /// Invalidate GPU data.
    /// </summary>
    public virtual void Invalidate()
    {
        dataLost = true;
    }

    /// <summary>
    /// Restore GPU data if possible.
    /// </summary>
    public virtual void Restore()
    {
        dataLost = false;
    }

    /// <summary>
    /// Destroy all GPU data on RenderDevice shutdown.
    /// </summary>
    public virtual void Destroy()
    {
    }

    public RenderDevice? GetRenderDevice() => GetSubsystem<RenderDevice>();

    public bool IsDataLost() => dataLost;

    public void ClearDataLost()
    {
        dataLost = false;
    }

    public string GetDebugName() => debugName;

    public void SetDebugName(string debugName)
    {
        this.debugName = debugName ?? string.Empty;
    }

    /// <summary>
    /// Internal: process device object event.
    /// </summary>
    internal void ProcessDeviceObjectEvent(DeviceObjectEvent deviceObjectEvent)
    {
        switch (deviceObjectEvent)
        {
            case DeviceObjectEvent.Invalidate:
                Invalidate();
                break;

            case DeviceObjectEvent.Restore:
                Restore();
                break;

            case DeviceObjectEvent.Destroy:
                Destroy();
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
