namespace RedBolt;

public class PipelineState : DeviceObject
{
    public string DebugName
    {
        get => GetDebugName();
        set => SetDebugName(value);
    }

    public bool IsValid { get; protected set; } = true;
}
