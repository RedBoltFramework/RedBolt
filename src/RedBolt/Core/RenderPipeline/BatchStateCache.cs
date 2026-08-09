namespace RedBolt.RenderPipeline;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.RenderAPI;

/// <summary>Key used to lookup cached pipeline states for PipelineBatch.</summary>
/// <remarks>
/// PipelineState creation may depend only on variables that contribute to BatchStateLookupKey:
/// Parameters of Drawable that contribute to hash calculation; parameters of per-pixel Light that contribute to hash
/// calculation; geometry type from SourceBatch; and hashed state of Geometry, Material, and Pass.
/// </remarks>
public struct BatchStateLookupKey : IEquatable<BatchStateLookupKey>
{
    public uint DrawableHash { get; set; }
    public uint PixelLightHash { get; set; }
    public GeometryType GeometryType { get; set; }
    public Geometry? Geometry { get; set; }
    public Material? Material { get; set; }
    public Pass? Pass { get; set; }

    public BatchStateLookupKey(uint drawableHash, uint pixelLightHash, GeometryType geometryType,
        Geometry? geometry, Material? material, Pass? pass)
    {
        DrawableHash = drawableHash;
        PixelLightHash = pixelLightHash;
        GeometryType = geometryType;
        Geometry = geometry;
        Material = material;
        Pass = pass;
    }

    public readonly bool Equals(BatchStateLookupKey rhs)
    {
        return DrawableHash == rhs.DrawableHash
            && PixelLightHash == rhs.PixelLightHash
            && GeometryType == rhs.GeometryType
            && ReferenceEquals(Geometry, rhs.Geometry)
            && ReferenceEquals(Material, rhs.Material)
            && ReferenceEquals(Pass, rhs.Pass);
    }

    public readonly uint ToHash()
    {
        var hash = 0u;
        CombineHash(ref hash, DrawableHash);
        CombineHash(ref hash, PixelLightHash);
        CombineHash(ref hash, (uint)GeometryType);
        CombineHash(ref hash, MakeHash(Geometry));
        CombineHash(ref hash, MakeHash(Material));
        CombineHash(ref hash, MakeHash(Pass));
        return hash;
    }

    public static bool operator ==(BatchStateLookupKey left, BatchStateLookupKey right) => left.Equals(right);

    public static bool operator !=(BatchStateLookupKey left, BatchStateLookupKey right) => !left.Equals(right);

    public override readonly bool Equals(object? obj) => obj is BatchStateLookupKey rhs && Equals(rhs);

    public override readonly int GetHashCode() => unchecked((int)ToHash());

    private static uint MakeHash(object? value) => value is null ? 0u : unchecked((uint)RuntimeHelpers.GetHashCode(value));

    private static void CombineHash(ref uint hash, uint value)
    {
        hash ^= value + 0x9e3779b9u + (hash << 6) + (hash >> 2);
    }
}

/// <summary>Key used to create cached pipeline states for PipelineBatch. Contains actual objects instead of just hashes.</summary>
public struct BatchStateCreateKey
{
    public uint DrawableHash { get; set; }
    public uint PixelLightHash { get; set; }
    public GeometryType GeometryType { get; set; }
    public Geometry? Geometry { get; set; }
    public Material? Material { get; set; }
    public Pass? Pass { get; set; }
    public Drawable? Drawable { get; set; }
    public LightProcessor? PixelLight { get; set; }
    public int PixelLightIndex { get; set; }

    public BatchStateCreateKey(uint drawableHash, uint pixelLightHash, GeometryType geometryType,
        Geometry? geometry, Material? material, Pass? pass, Drawable? drawable,
        LightProcessor? pixelLight, int pixelLightIndex)
    {
        DrawableHash = drawableHash;
        PixelLightHash = pixelLightHash;
        GeometryType = geometryType;
        Geometry = geometry;
        Material = material;
        Pass = pass;
        Drawable = drawable;
        PixelLight = pixelLight;
        PixelLightIndex = pixelLightIndex;
    }

    public readonly BatchStateLookupKey LookupKey => new(DrawableHash, PixelLightHash, GeometryType, Geometry, Material, Pass);
}

/// <summary>Pipeline state cache entry. May be invalid.</summary>
public struct CachedBatchState
{
    /// <summary>Whether the PipelineState is invalidated and should be recreated.</summary>
    public bool Invalidated = true;
    public PipelineState? PipelineState { get; set; }

    /// <summary>Hashes of corresponding objects at the moment of pipeline state creation.</summary>
    public uint GeometryHash { get; set; }
    public uint MaterialHash { get; set; }
    public uint PassHash { get; set; }

    public CachedBatchState()
    {
    }
}

/// <summary>External context that is not present in the key but is necessary to create new pipeline state.</summary>
public struct BatchStateCreateContext
{
    /// <summary>Object that owns BatchStateCache that invoked the callback.</summary>
    public RedObject? Pass { get; set; }

    /// <summary>Index of subpass. Exact meaning depends on actual type of owner pass.</summary>
    public int SubpassIndex { get; set; }

    public int ShadowSplitIndex { get; set; }

    public BatchStateCreateContext(RedObject? pass, int subpassIndex, int shadowSplitIndex)
    {
        Pass = pass;
        SubpassIndex = subpassIndex;
        ShadowSplitIndex = shadowSplitIndex;
    }
}

/// <summary>Pipeline state cache for RenderPipeline batches.</summary>
public class BatchStateCache : IDisposable
{

    private PipelineStateOutputDesc? _outputDesc;
    private readonly Dictionary<BatchStateLookupKey, CachedBatchState> _cache = [];
    private readonly Dictionary<int, PipelineState?> _placeholderCache = [];

    public void Dispose()
    {
        _placeholderCache.Clear();
        _cache.Clear();
        _outputDesc = null;
    }

    /// <summary>Invalidate cache.</summary>
    public void Invalidate() => _cache.Clear();

    /// <summary>Set currently used output description. Invalidates cache if it has changed.</summary>
    public void SetOutputDesc(PipelineStateOutputDesc outputDesc)
    {
        if (_outputDesc?.Equals(outputDesc) != true)
        {
            _outputDesc = outputDesc;
            Invalidate();
        }
    }

    /// <summary>Return existing pipeline state or null if not found. Thread-safe. Resulting state may be invalid.</summary>
    public PipelineState? GetPipelineState(BatchStateLookupKey key)
    {
        System.Diagnostics.Debug.Assert(_outputDesc is not null);
        ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(_cache, key);
        if (Unsafe.IsNullRef(ref entry) || Volatile.Read(ref entry.Invalidated))
            return null;
        if (entry.PipelineState is null
            || key.Geometry!.GetPipelineStateHash() != entry.GeometryHash
            || key.Material!.GetPipelineStateHash() != entry.MaterialHash
            || key.Pass!.GetPipelineStateHash() != entry.PassHash)
        {
            Volatile.Write(ref entry.Invalidated, true);
            return null;
        }
        return entry.PipelineState;
    }

    /// <summary>Return existing or create new pipeline state. Not thread safe. Resulting state may be invalid.</summary>
    public PipelineState? GetOrCreatePipelineState(BatchStateCreateKey key,
        BatchStateCreateContext context, BatchStateCacheCallback callback)
    {
        System.Diagnostics.Debug.Assert(_outputDesc is not null);
        var lookupKey = key.LookupKey;
        ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(_cache, lookupKey, out var exists);
        if (!exists)
            entry.Invalidated = true;
        if (entry.PipelineState is null || Volatile.Read(ref entry.Invalidated)
            || key.Geometry!.GetPipelineStateHash() != entry.GeometryHash
            || key.Material!.GetPipelineStateHash() != entry.MaterialHash
            || key.Pass!.GetPipelineStateHash() != entry.PassHash)
        {
            entry.PipelineState = callback.CreateBatchPipelineState(key, context, _outputDesc!.Value);
            entry.GeometryHash = key.Geometry!.GetPipelineStateHash();
            entry.MaterialHash = key.Material!.GetPipelineStateHash();
            entry.PassHash = key.Pass!.GetPipelineStateHash();
            Volatile.Write(ref entry.Invalidated, false);
        }
        return entry.PipelineState;
    }

    /// <summary>Return existing or create new placeholder pipeline state. Not thread safe.</summary>
    /// <remarks>Resulting state may be invalid in case of emergencies.</remarks>
    public PipelineState? GetOrCreatePlaceholderPipelineState(int vertexStride, BatchStateCacheCallback callback)
    {
        if (!_placeholderCache.TryGetValue(vertexStride, out var entry) || entry is null)
        {
            entry = callback.CreateBatchPipelineStatePlaceholder(vertexStride, _outputDesc!.Value);
            _placeholderCache[vertexStride] = entry;
        }
        return entry?.IsValid == true ? entry : null;
    }
}

/// <summary>Key used to lookup cached pipeline states for UI batches.</summary>
/// <remarks>It's assumed that all UI batches use the same vertex and index buffer formats and material pass.</remarks>
public struct UIBatchStateKey : IEquatable<UIBatchStateKey>
{
    public bool LinearOutput { get; set; }
    public PipelineStateOutputDesc OutputDesc { get; set; }
    public Material? Material { get; set; }
    public Pass? Pass { get; set; }
    public BlendMode BlendMode { get; set; }
    public uint SamplerStateHash { get; set; }

    public UIBatchStateKey(bool linearOutput, PipelineStateOutputDesc outputDesc, Material? material,
        Pass? pass, BlendMode blendMode, uint samplerStateHash)
    {
        LinearOutput = linearOutput;
        OutputDesc = outputDesc;
        Material = material;
        Pass = pass;
        BlendMode = blendMode;
        SamplerStateHash = samplerStateHash;
    }

    public readonly bool Equals(UIBatchStateKey rhs)
    {
        return LinearOutput == rhs.LinearOutput
            && OutputDesc.Equals(rhs.OutputDesc)
            && ReferenceEquals(Material, rhs.Material)
            && ReferenceEquals(Pass, rhs.Pass)
            && BlendMode == rhs.BlendMode
            && SamplerStateHash == rhs.SamplerStateHash;
    }

    public readonly uint ToHash()
    {
        var hash = 0u;
        CombineHash(ref hash, LinearOutput ? 1u : 0u);
        CombineHash(ref hash, OutputDesc.ToHash());
        CombineHash(ref hash, MakeHash(Material));
        CombineHash(ref hash, MakeHash(Pass));
        CombineHash(ref hash, (uint)BlendMode);
        CombineHash(ref hash, SamplerStateHash);
        return hash;
    }

    public static bool operator ==(UIBatchStateKey left, UIBatchStateKey right) => left.Equals(right);

    public static bool operator !=(UIBatchStateKey left, UIBatchStateKey right) => !left.Equals(right);

    public override readonly bool Equals(object? obj) => obj is UIBatchStateKey rhs && Equals(rhs);

    public override readonly int GetHashCode() => unchecked((int)ToHash());

    private static uint MakeHash(object? value) => value is null ? 0u : unchecked((uint)RuntimeHelpers.GetHashCode(value));

    private static void CombineHash(ref uint hash, uint value)
    {
        hash ^= value + 0x9e3779b9u + (hash << 6) + (hash >> 2);
    }
}

/// <summary>Pipeline state UI batch cache entry. May be invalid.</summary>
public struct CachedUIBatchState
{
    /// <summary>Whether the PipelineState is invalidated and should be recreated.</summary>
    public bool Invalidated = true;
    public PipelineState? PipelineState { get; set; }

    /// <summary>Hashes of corresponding objects at the moment of pipeline state creation.</summary>
    public uint MaterialHash { get; set; }
    public uint PassHash { get; set; }

    public CachedUIBatchState()
    {
    }
}

/// <summary>External context that is not present in the key but is necessary to create new pipeline state for UI batch.</summary>
public struct UIBatchStateCreateContext
{
    public VertexBuffer? VertexBuffer { get; set; }
    public IndexBuffer? IndexBuffer { get; set; }
    public SamplerStateDesc? DefaultSampler { get; set; }

    public UIBatchStateCreateContext(
        VertexBuffer? vertexBuffer, IndexBuffer? indexBuffer, SamplerStateDesc? defaultSampler = null)
    {
        VertexBuffer = vertexBuffer;
        IndexBuffer = indexBuffer;
        DefaultSampler = defaultSampler;
    }
}

/// <summary>Pipeline state cache for UI batches.</summary>
public class UIBatchStateCache : IDisposable
{
    private readonly Dictionary<UIBatchStateKey, CachedUIBatchState> _cache = [];

    public void Dispose() => _cache.Clear();

    /// <summary>Invalidate cache.</summary>
    public void Invalidate() => _cache.Clear();

    /// <summary>Return existing pipeline state or null if not found. Thread-safe. Resulting state may be invalid.</summary>
    public PipelineState? GetPipelineState(UIBatchStateKey key)
    {
        ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(_cache, key);
        if (Unsafe.IsNullRef(ref entry) || Volatile.Read(ref entry.Invalidated))
            return null;

        if (entry.PipelineState is null
            || key.Material!.GetPipelineStateHash() != entry.MaterialHash
            || key.Pass!.GetPipelineStateHash() != entry.PassHash)
        {
            Volatile.Write(ref entry.Invalidated, true);
            return null;
        }

        return entry.PipelineState;
    }

    /// <summary>Return existing or create new pipeline state. Not thread safe. Resulting state may be invalid.</summary>
    public PipelineState? GetOrCreatePipelineState(
        UIBatchStateKey key, UIBatchStateCreateContext context, UIBatchStateCacheCallback callback)
    {
        ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(_cache, key, out var exists);
        if (!exists)
            entry.Invalidated = true;
        if (entry.PipelineState is null || Volatile.Read(ref entry.Invalidated)
            || key.Material!.GetPipelineStateHash() != entry.MaterialHash
            || key.Pass!.GetPipelineStateHash() != entry.PassHash)
        {
            entry.PipelineState = callback.CreateUIBatchPipelineState(key, context);
            entry.MaterialHash = key.Material!.GetPipelineStateHash();
            entry.PassHash = key.Pass!.GetPipelineStateHash();
            Volatile.Write(ref entry.Invalidated, false);
        }

        return entry.PipelineState;
    }
}

/// <summary>Default implementation of UIBatchStateCache.</summary>
public class DefaultUIBatchStateCache : RedObject, UIBatchStateCacheCallback
{
    private readonly UIBatchStateCache _cache = new();
    private string _vertexShaderDefines = string.Empty;
    private string _pixelShaderDefines = string.Empty;

    public override void Dispose()
    {
        _cache.Dispose();
        base.Dispose();
    }

    public void Invalidate() => _cache.Invalidate();

    public PipelineState? GetPipelineState(UIBatchStateKey key) => _cache.GetPipelineState(key);

    public PipelineState? GetOrCreatePipelineState(UIBatchStateKey key, UIBatchStateCreateContext context)
    {
        return _cache.GetOrCreatePipelineState(key, context, this);
    }

    PipelineState? UIBatchStateCacheCallback.CreateUIBatchPipelineState(
        UIBatchStateKey key, UIBatchStateCreateContext context)
    {
        var graphics = GetSubsystem<Graphics>();
        var pipelineStateCache = GetSubsystem<PipelineStateCache>();

        var desc = new GraphicsPipelineStateDesc
        {
            DebugName = $"UI Batch for '{key.Material!.Name}'"
        };

        GraphicsUtils.InitializeInputLayout(ref desc.InputLayout, [context.VertexBuffer]);
        desc.PrimitiveType = PrimitiveType.TriangleList;
        desc.Output = key.OutputDesc;
        desc.ColorWriteEnabled = true;
        desc.CullMode = CullMode.None;
        desc.DepthCompareFunction = CompareMode.Always;
        desc.DepthWriteEnabled = false;
        desc.FillMode = FillMode.Solid;
        desc.StencilTestEnabled = false;
        desc.BlendMode = key.BlendMode;
        desc.ScissorTestEnabled = true;

        foreach (var (nameHash, texture) in key.Material.GetTextures())
            desc.Samplers.Add(nameHash, texture.Texture.GetSamplerStateDesc());
        if (context.DefaultSampler is { } defaultSampler)
            desc.Samplers.Add(ShaderResources.Albedo, defaultSampler);

        _vertexShaderDefines = key.Pass!.GetEffectiveVertexShaderDefines();
        _pixelShaderDefines = key.Pass.GetEffectivePixelShaderDefines();

        if (key.LinearOutput)
        {
            _vertexShaderDefines += " URHO3D_LINEAR_OUTPUT";
            _pixelShaderDefines += " URHO3D_LINEAR_OUTPUT";
        }

        desc.VertexShader = graphics!.GetShader(ShaderType.VS, key.Pass.VertexShader, _vertexShaderDefines);
        desc.PixelShader = graphics.GetShader(ShaderType.PS, key.Pass.PixelShader, _pixelShaderDefines);

        return pipelineStateCache!.GetGraphicsPipelineState(desc);
    }
}
