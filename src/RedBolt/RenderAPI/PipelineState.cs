namespace RedBolt;

/// <summary>
/// Cooked pipeline state.
/// </summary>
public class PipelineState : DeviceObject
{
    private readonly WeakReference<PipelineStateCache>? _owner;
    private readonly PipelineStateDesc _desc = new();
    private readonly ShaderProgramReflection _reflection = new();

    public PipelineState()
    {
    }

    internal PipelineState(PipelineStateCache owner, PipelineStateDesc desc)
    {
        this._owner = new WeakReference<PipelineStateCache>(owner ?? throw new ArgumentNullException(nameof(owner)));
        this._desc = desc ?? throw new ArgumentNullException(nameof(desc));
        _reflection = ShaderProgramReflection.FromDesc(desc);
        IsValid = desc.AsGraphics()?.IsInitialized() == true || desc.AsCompute()?.IsInitialized() == true;
        SetDebugName($"{desc.GetDebugName()} #{desc.ToHash()}");
    }

    public string DebugName
    {
        get => GetDebugName();
        set => SetDebugName(value);
    }

    public bool IsValid { get; protected set; } = true;

    public PipelineStateType GetPipelineType() => _desc.GetPipelineType();

    public PipelineStateDesc GetDesc() => _desc;

    public ShaderProgramReflection GetReflection() => _reflection;

    public override void Invalidate()
    {
        base.Invalidate();
        IsValid = false;
    }

    public override void Restore()
    {
        base.Restore();
        IsValid = _desc.AsGraphics()?.IsInitialized() == true || _desc.AsCompute()?.IsInitialized() == true || _owner is null;
    }

    public override void Destroy()
    {
        base.Destroy();
        if (_owner is not null && _owner.TryGetTarget(out var pipelineStateCache))
            pipelineStateCache.ReleasePipelineState(_desc);
    }
}
