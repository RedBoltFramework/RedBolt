namespace RedBolt;

/// <summary>Scene pass that collects outlined drawables into a deferred outline mask.</summary>
public class OutlineScenePass : ScenePass
{
    private static readonly TextureFormat OutlineTextureFormat = TextureFormat.TexFormatRgba8Unorm;

    private readonly List<uint> _outlinedPasses = [];
    private readonly List<OutlineGroup> _outlineGroups = [];

    /// <summary>Internal temporary containers.</summary>
    private readonly ShaderProgramDesc _shaderProgramDesc = new();
    private readonly List<PipelineBatchByState> _sortedBatches = [];
    private PipelineBatchGroup<PipelineBatchByState> _batchGroup;

    public OutlineScenePass(IRenderPipelineInterface renderPipeline, DrawableProcessor drawableProcessor,
        BatchStateCacheCallback? callback, IReadOnlyList<string> outlinedPasses,
        DrawableProcessorPassFlag flags = DrawableProcessorPassFlag.None)
        : base(renderPipeline, drawableProcessor, callback,
            DrawableProcessorPassFlag.BatchCallback | DrawableProcessorPassFlag.PipelineStateCallback | flags,
            "base") // Pass here doesn't matter
    {
        foreach (var pass in outlinedPasses)
            _outlinedPasses.Add(Technique.GetPassIndex(pass));

        var outputDesc = new PipelineStateOutputDesc
        {
            DepthStencilFormat = TextureFormat.TexFormatUnknown
        };
        outputDesc.NumRenderTargets = 1;
        outputDesc.RenderTargetFormats[0] = OutlineTextureFormat;
        SetDeferredOutputDesc(outputDesc);
    }

    /// <summary>Initialize outline groups from scene. Should be called every frame.</summary>
    public void SetOutlineGroups(Scene scene, bool drawDebugOutlines)
    {
        ArgumentNullException.ThrowIfNull(scene);
        scene.GetComponents(_outlineGroups);

        if (!drawDebugOutlines)
            _outlineGroups.RemoveAll(static group => group.IsDebug());

        var hasDrawables = false;
        foreach (var group in _outlineGroups)
        {
            if (group.HasDrawables())
            {
                hasDrawables = true;
                break;
            }
        }

        SetEnabled(hasDrawables);
    }

    /// <summary>Implement ScenePass / DrawableProcessorPass batch callback.</summary>
    public override AddBatchResult AddCustomBatch(
        uint threadIndex, Drawable drawable, uint sourceBatchIndex, Technique technique)
    {
        if (_outlineGroups.Count == 0)
            return default;

        // TODO: Check if all groups are empty
        var batchAdded = false;
        foreach (var outlineGroup in _outlineGroups)
        {
            if (!outlineGroup.ContainsDrawable(drawable))
                continue;

            var referencePass = GetFirstPass(technique, _outlinedPasses);
            if (referencePass is not null)
            {
                GeometryBatches.PushBack(threadIndex,
                    GeometryBatch.Deferred(drawable, sourceBatchIndex, referencePass, outlineGroup));
                batchAdded = true;
            }
        }

        return new AddBatchResult(batchAdded, false);
    }

    public override bool CreatePipelineState(ref GraphicsPipelineStateDesc desc, PipelineStateBuilder? builder,
        BatchStateCreateKey key, BatchStateCreateContext ctx)
    {
        if (builder is null)
            return false;

        var compositor = builder.GetShaderProgramCompositor();

        desc.DebugName = $"Outline Pass for material '{key.Material.GetName()}'";

        desc.DepthWriteEnabled = false;
        desc.DepthCompareFunction = CompareMode.Always;

        desc.ColorWriteEnabled = true;
        desc.BlendMode = BlendMode.Replace;
        desc.AlphaToCoverageEnabled = false;

        desc.FillMode = FillMode.Solid;
        desc.CullMode = CullMode.None;

        desc.ScissorTestEnabled = true;

        _shaderProgramDesc.Clear();
        compositor.ProcessUserBatch(_shaderProgramDesc, GetFlags(),
            key.Drawable!, key.Geometry, key.GeometryType, key.Material, key.Pass,
            null, false, BatchCompositorSubpass.Ignored);

        _shaderProgramDesc.ShaderName[(int)ShaderType.PS] = "v2/M_OutlinePixel";
        _shaderProgramDesc.ShaderDefines[(int)ShaderType.PS] = string.Empty;

        var diffMap = key.Material.GetTexture(ShaderResources.Albedo);
        var needAlphaMask = (key.Pass.IsAlphaMask || key.Pass.BlendMode != BlendMode.Replace) && diffMap is not null;
        if (needAlphaMask)
        {
            _shaderProgramDesc.AddShaderDefines(ShaderType.PS, "ALPHAMASK");
            desc.Samplers.Add(ShaderResources.Albedo, diffMap!.GetSamplerStateDesc());
        }

        var isStereo = IsFlagSet(DrawableProcessorPassFlag.StereoInstancing);
        builder.SetupInputLayoutAndPrimitiveType(ref desc, _shaderProgramDesc, key.Geometry, isStereo);
        builder.SetupShaders(ref desc, _shaderProgramDesc);

        return true;
    }

    public override void PrepareInstancingBuffer(BatchRenderer batchRenderer)
    {
        batchRenderer.PrepareInstancingBuffer(ref _batchGroup);
    }

    public PipelineBatchGroup<PipelineBatchByState> GetBatches() => _batchGroup;

    public new bool HasBatches() => _sortedBatches.Count > 0;

    protected override void OnBatchesReady()
    {
        foreach (var batch in DeferredBatches)
        {
            var outlineGroup = (OutlineGroup)batch.UserData!;
            batch.Material = outlineGroup.GetOutlineMaterial(batch.Material!);
        }

        BatchCompositor.FillSortKeys(_sortedBatches, DeferredBatches);
        _sortedBatches.Sort();

        _batchGroup = new PipelineBatchGroup<PipelineBatchByState>
        {
            Batches = _sortedBatches,
            Flags = BatchRenderFlag.EnableInstancingForStaticGeometry
        };
        if (LinearColorSpace)
            _batchGroup.Flags |= BatchRenderFlag.LinearColorSpace;
    }

    private static Pass? GetFirstPass(Technique technique, List<uint> passIndices)
    {
        foreach (var index in passIndices)
        {
            var pass = technique.GetPass(index);
            if (pass is not null)
                return pass;
        }

        return null;
    }
}
