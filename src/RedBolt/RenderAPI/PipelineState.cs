namespace RedBolt;

public class PipelineState : DeviceObject
{
    private readonly WeakReference<PipelineStateCache>? owner;
    private readonly PipelineStateDesc desc = new();
    private readonly ShaderProgramReflection reflection = new();

    public PipelineState()
    {
    }

    internal PipelineState(PipelineStateCache owner, PipelineStateDesc desc)
    {
        this.owner = new WeakReference<PipelineStateCache>(owner ?? throw new ArgumentNullException(nameof(owner)));
        this.desc = desc ?? throw new ArgumentNullException(nameof(desc));
        reflection = ShaderProgramReflection.FromDesc(desc);
        IsValid = desc.AsGraphics()?.IsInitialized() == true || desc.AsCompute()?.IsInitialized() == true;
        SetDebugName($"{desc.GetDebugName()} #{desc.ToHash()}");
    }

    public string DebugName
    {
        get => GetDebugName();
        set => SetDebugName(value);
    }

    public bool IsValid { get; protected set; } = true;

    public PipelineStateType GetPipelineType() => desc.GetPipelineType();

    public PipelineStateDesc GetDesc() => desc;

    public ShaderProgramReflection GetReflection() => reflection;

    public override void Invalidate()
    {
        base.Invalidate();
        IsValid = false;
    }

    public override void Restore()
    {
        base.Restore();
        IsValid = desc.AsGraphics()?.IsInitialized() == true || desc.AsCompute()?.IsInitialized() == true || owner is null;
    }

    public override void Destroy()
    {
        base.Destroy();
        if (owner is not null && owner.TryGetTarget(out var pipelineStateCache))
            pipelineStateCache.ReleasePipelineState(desc);
    }
}
