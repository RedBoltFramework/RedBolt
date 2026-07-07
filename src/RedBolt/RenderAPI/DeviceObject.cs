namespace RedBolt;

public abstract class DeviceObject : Object
{
    private bool dataLost;
    private string debugName = string.Empty;

    protected DeviceObject(DeviceObjectFlag flags = DeviceObjectFlag.None)
    {
        Flags = flags;
        if ((flags & DeviceObjectFlag.Headless) == 0)
            GetRenderDevice()?.AddDeviceObject(this);
    }

    public DeviceObjectFlag Flags { get; }

    public virtual void Invalidate()
    {
        dataLost = true;
    }

    public virtual void Restore()
    {
        dataLost = false;
    }

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

    protected void RestoreDependency(DeviceObject? dependency)
    {
        dependency?.Restore();
    }
}
