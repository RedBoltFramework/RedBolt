using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

using RedBolt.Core;
using Timer = global::RedBolt.Core.Timer;
using RedBolt.Logging;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Graphics;

/// <summary>Light baking quality settings.</summary>
public enum LightBakingQuality
{
    /// <summary>Custom quality.</summary>
    Custom,
    /// <summary>Fast baking, low quality.</summary>
    Low,
    /// <summary>Slower baking, medium quality.</summary>
    Medium,
    /// <summary>Slow baking, high quality.</summary>
    High
}

/// <summary>Light baker component.</summary>
[Reflected(ObjectCategory.Category_Subsystem)]
public partial class LightBaker : Component
{
    private static readonly LightBakingSettings DefaultSettings = new();
    private static readonly string[] QualityNames =
    [
        "Custom",
        "Low",
        "Medium",
        "High"
    ];

    /// <summary>Baking task data.</summary>
    private sealed class TaskData
    {
        /// <summary>Caller.</summary>
        public WeakReference<LightBaker>? WeakSelf = null;
        /// <summary>Stop token.</summary>
        public StopToken StopToken = new();
        /// <summary>Timer to measure total time.</summary>
        public Timer Timer = new();
        // TODO: port URHO3D_GLOW IncrementalLightBaker, DefaultBakedSceneCollector, BakedLightMemoryCache
    }

    /// <summary>Internal baking state.</summary>
    private enum InternalState
    {
        /// <summary>Baking is not started.</summary>
        NotStarted,
        /// <summary>Synchronous baking scheduled.</summary>
        ScheduledSync,
        /// <summary>Asynchronous baking scheduled.</summary>
        ScheduledAsync,
        /// <summary>Baking in progress.</summary>
        InProgress,
        /// <summary>Commit from main thread is pending.</summary>
        CommitPending
    }

    /// <summary>Quality.</summary>
    [Serialized(
        "Quality",
        DefaultSource = nameof(GetQualityAttributeDefault),
        EnumNamesSource = nameof(QualityNames),
        Setter = nameof(SetQuality),
        Order = 3
    )]
    private LightBakingQuality _quality;

    /// <summary>Light baking settings.</summary>
    private LightBakingSettings _settings = new();

    /// <summary>Current state.</summary>
    private InternalState _state;

    /// <summary>Async baking task.</summary>
    private Task? _task;

    /// <summary>Task data.</summary>
    private TaskData? _taskData;

    private readonly object _stateLock = new();

    /// <summary>Construct.</summary>
    public LightBaker()
    {
        SubscribeToEvent<CoreEvents.UpdateEvent>((ref CoreEvents.UpdateEvent _) => Update());
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        if (_state != InternalState.NotStarted)
        {
            _taskData!.StopToken.Stop();
            ((IAsyncResult)_task!).AsyncWaitHandle.WaitOne();
        }

        _taskData = null;
        _task?.Dispose();
        _task = null;
        base.Dispose();
    }

    // TODO: port URHO3D_ACTION_DYNAMIC_LABEL("Bake", BakeAsync, GetBakeLabel);

    /// <summary>Set baking quality.</summary>
    public void SetQuality(LightBakingQuality quality)
    {
        _quality = quality;
        switch (quality)
        {
            case LightBakingQuality.Low:
            {
                var directChart = _settings.DirectChartTracing;
                directChart.MaxSamples = 10;
                _settings.DirectChartTracing = directChart;
                var directProbes = _settings.DirectProbesTracing;
                directProbes.MaxSamples = 32;
                _settings.DirectProbesTracing = directProbes;
                var indirectChart = _settings.IndirectChartTracing;
                indirectChart.MaxSamples = 10;
                _settings.IndirectChartTracing = indirectChart;
                var indirectProbes = _settings.IndirectProbesTracing;
                indirectProbes.MaxSamples = 64;
                _settings.IndirectProbesTracing = indirectProbes;
                break;
            }
            case LightBakingQuality.Medium:
            {
                var directChart = _settings.DirectChartTracing;
                directChart.MaxSamples = 32;
                _settings.DirectChartTracing = directChart;
                var directProbes = _settings.DirectProbesTracing;
                directProbes.MaxSamples = 32;
                _settings.DirectProbesTracing = directProbes;
                var indirectChart = _settings.IndirectChartTracing;
                indirectChart.MaxSamples = 64;
                _settings.IndirectChartTracing = indirectChart;
                var indirectProbes = _settings.IndirectProbesTracing;
                indirectProbes.MaxSamples = 256;
                _settings.IndirectProbesTracing = indirectProbes;
                break;
            }
            case LightBakingQuality.High:
            {
                var directChart = _settings.DirectChartTracing;
                directChart.MaxSamples = 32;
                _settings.DirectChartTracing = directChart;
                var directProbes = _settings.DirectProbesTracing;
                directProbes.MaxSamples = 32;
                _settings.DirectProbesTracing = directProbes;
                var indirectChart = _settings.IndirectChartTracing;
                indirectChart.MaxSamples = 256;
                _settings.IndirectChartTracing = indirectChart;
                var indirectProbes = _settings.IndirectProbesTracing;
                indirectProbes.MaxSamples = 256;
                _settings.IndirectProbesTracing = indirectProbes;
                break;
            }
            case LightBakingQuality.Custom:
            default:
                break;
        }
    }

    /// <summary>Return baking quality.</summary>
    public LightBakingQuality GetQuality() => _quality;

    /// <summary>Bake light in main thread. Must be called outside rendering.</summary>
    public void Bake()
    {
        lock (_stateLock)
        {
            if (_state == InternalState.NotStarted)
            {
                _state = InternalState.ScheduledSync;
                Update();
            }
        }
    }

    /// <summary>Bake light in worker thread.</summary>
    public void BakeAsync()
    {
        lock (_stateLock)
        {
            if (_state == InternalState.NotStarted)
                _state = InternalState.ScheduledAsync;
        }
    }

    /// <summary>Update settings before baking.</summary>
    private bool UpdateSettings()
    {
        var scene = Scene!;
        var octree = scene.GetComponent<Octree>()!;
        var gi = scene.GetComponent<GlobalIllumination>();
        var zone = octree.GetBackgroundZone();

        if (gi is null)
        {
            Log.GetLogger().LogError("GlobalIllumination scene system is required to bake light");
            return false;
        }

        // Fill settings
        var indirectProbes = _settings.IndirectProbesTracing;
        indirectProbes.MaxBounces = _settings.IndirectChartTracing.MaxBounces;
        _settings.IndirectProbesTracing = indirectProbes;

        var numTasks = _settings.Charting.LightmapSize;
        var geometryPre = _settings.GeometryBufferPreprocessing;
        geometryPre.NumTasks = numTasks;
        _settings.GeometryBufferPreprocessing = geometryPre;
        var emission = _settings.EmissionTracing;
        emission.NumTasks = numTasks;
        _settings.EmissionTracing = emission;
        var directChart = _settings.DirectChartTracing;
        directChart.NumTasks = numTasks;
        _settings.DirectChartTracing = directChart;
        var directProbes = _settings.DirectProbesTracing;
        directProbes.NumTasks = numTasks;
        _settings.DirectProbesTracing = directProbes;
        var indirectChart = _settings.IndirectChartTracing;
        indirectChart.NumTasks = numTasks;
        _settings.IndirectChartTracing = indirectChart;
        indirectProbes = _settings.IndirectProbesTracing;
        indirectProbes.NumTasks = numTasks;
        _settings.IndirectProbesTracing = indirectProbes;

        var properties = _settings.Properties;
        properties.EmissionBrightness = gi.GetEmissionBrightness();
        _settings.Properties = properties;
        return true;
    }

    /// <summary>Update baker. May start or finish baking depending on current state.</summary>
    private void Update()
    {
        InternalState state;
        lock (_stateLock)
            state = _state;

        // Start baking
        if (state is InternalState.ScheduledSync or InternalState.ScheduledAsync)
        {
            // Glow (URHO3D_GLOW) is not yet ported — match C++ #else path
            Log.GetLogger().LogError("Enable URHO3D_GLOW in build options");
            lock (_stateLock)
                _state = InternalState.NotStarted;
            return;
        }

        // Commit changes
        if (state == InternalState.CommitPending)
        {
            // If was async task, close future
            if (_task is not null)
            {
                _task.Wait();
                _task.Dispose();
                _task = null;
            }

            // TODO: port URHO3D_GLOW taskData_->baker_.CommitScene()

            // Compile light probes
            var gi = Scene!.GetComponent<GlobalIllumination>()!;
            gi.CompileLightProbes();

            // Log overall time
            var totalMSec = _taskData!.Timer.GetMSec(true);
            Log.GetLogger().LogInformation("Light baking is finished in {Seconds} seconds", totalMSec / 1000);

            // Reset
            lock (_stateLock)
            {
                _state = InternalState.NotStarted;
                _taskData = null;
            }
        }
    }

    /// <summary>Return baking status.</summary>
    private string GetBakeLabel()
    {
        // TODO: port URHO3D_GLOW IncrementalLightBakerStatus.ToString()
        return "Baking is disabled in build options.";
    }

    private static object? GetOutputDirectoryAttributeDefault() => string.Empty;

    [Serialized(
        "Output Directory",
        typeof(string),
        DefaultSource = nameof(GetOutputDirectoryAttributeDefault),
        Setter = nameof(SetSerializedOutputDirectory),
        Order = 0
    )]
    private object? GetSerializedOutputDirectory()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.Incremental.OutputDirectory;
    }

    private void SetSerializedOutputDirectory(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var incremental = baker._settings.Incremental;
        incremental.OutputDirectory = value as string ?? string.Empty;
        baker._settings.Incremental = incremental;
    }

    private static object? GetLightmapSizeAttributeDefault() => DefaultSettings.Charting.LightmapSize;

    [Serialized(
        "Lightmap Size",
        typeof(uint),
        DefaultSource = nameof(GetLightmapSizeAttributeDefault),
        Setter = nameof(SetSerializedLightmapSize),
        Order = 1
    )]
    private object? GetSerializedLightmapSize()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.Charting.LightmapSize;
    }

    private void SetSerializedLightmapSize(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var charting = baker._settings.Charting;
        charting.LightmapSize = (uint)value!;
        baker._settings.Charting = charting;
    }

    private static object? GetTexelDensityAttributeDefault() => DefaultSettings.Charting.TexelDensity;

    [Serialized(
        "Texel Density",
        typeof(float),
        DefaultSource = nameof(GetTexelDensityAttributeDefault),
        Setter = nameof(SetSerializedTexelDensity),
        Order = 2
    )]
    private object? GetSerializedTexelDensity()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.Charting.TexelDensity;
    }

    private void SetSerializedTexelDensity(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var charting = baker._settings.Charting;
        charting.TexelDensity = (float)value!;
        baker._settings.Charting = charting;
    }

    private static object? GetQualityAttributeDefault() => LightBakingQuality.Custom;

    private static object? GetDirectSamplesLightmapAttributeDefault() => DefaultSettings.DirectChartTracing.MaxSamples;

    [Serialized(
        "Direct Samples (Lightmap)",
        typeof(uint),
        DefaultSource = nameof(GetDirectSamplesLightmapAttributeDefault),
        Setter = nameof(SetSerializedDirectSamplesLightmap),
        Order = 4
    )]
    private object? GetSerializedDirectSamplesLightmap()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.DirectChartTracing.MaxSamples;
    }

    private void SetSerializedDirectSamplesLightmap(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var tracing = baker._settings.DirectChartTracing;
        tracing.MaxSamples = (uint)value!;
        baker._settings.DirectChartTracing = tracing;
    }

    private static object? GetDirectSamplesLightProbesAttributeDefault() => DefaultSettings.DirectProbesTracing.MaxSamples;

    [Serialized(
        "Direct Samples (Light Probes)",
        typeof(uint),
        DefaultSource = nameof(GetDirectSamplesLightProbesAttributeDefault),
        Setter = nameof(SetSerializedDirectSamplesLightProbes),
        Order = 5
    )]
    private object? GetSerializedDirectSamplesLightProbes()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.DirectProbesTracing.MaxSamples;
    }

    private void SetSerializedDirectSamplesLightProbes(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var tracing = baker._settings.DirectProbesTracing;
        tracing.MaxSamples = (uint)value!;
        baker._settings.DirectProbesTracing = tracing;
    }

    private static object? GetIndirectBouncesAttributeDefault() => DefaultSettings.IndirectChartTracing.MaxBounces;

    [Serialized(
        "Indirect Bounces",
        typeof(uint),
        DefaultSource = nameof(GetIndirectBouncesAttributeDefault),
        Setter = nameof(SetSerializedIndirectBounces),
        Order = 6
    )]
    private object? GetSerializedIndirectBounces()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.IndirectChartTracing.MaxBounces;
    }

    private void SetSerializedIndirectBounces(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var tracing = baker._settings.IndirectChartTracing;
        tracing.MaxBounces = (uint)value!;
        baker._settings.IndirectChartTracing = tracing;
    }

    private static object? GetIndirectSamplesTextureAttributeDefault() => DefaultSettings.IndirectChartTracing.MaxSamples;

    [Serialized(
        "Indirect Samples (Texture)",
        typeof(uint),
        DefaultSource = nameof(GetIndirectSamplesTextureAttributeDefault),
        Setter = nameof(SetSerializedIndirectSamplesTexture),
        Order = 7
    )]
    private object? GetSerializedIndirectSamplesTexture()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.IndirectChartTracing.MaxSamples;
    }

    private void SetSerializedIndirectSamplesTexture(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var tracing = baker._settings.IndirectChartTracing;
        tracing.MaxSamples = (uint)value!;
        baker._settings.IndirectChartTracing = tracing;
    }

    private static object? GetIndirectSamplesLightProbesAttributeDefault() => DefaultSettings.IndirectProbesTracing.MaxSamples;

    [Serialized(
        "Indirect Samples (Light Probes)",
        typeof(uint),
        DefaultSource = nameof(GetIndirectSamplesLightProbesAttributeDefault),
        Setter = nameof(SetSerializedIndirectSamplesLightProbes),
        Order = 8
    )]
    private object? GetSerializedIndirectSamplesLightProbes()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.IndirectProbesTracing.MaxSamples;
    }

    private void SetSerializedIndirectSamplesLightProbes(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var tracing = baker._settings.IndirectProbesTracing;
        tracing.MaxSamples = (uint)value!;
        baker._settings.IndirectProbesTracing = tracing;
    }

    private static object? GetFilterRadiusDirectAttributeDefault() => (uint)DefaultSettings.DirectFilter.KernelRadius;

    [Serialized(
        "Filter Radius (Direct)",
        typeof(uint),
        DefaultSource = nameof(GetFilterRadiusDirectAttributeDefault),
        Setter = nameof(SetSerializedFilterRadiusDirect),
        Order = 9
    )]
    private object? GetSerializedFilterRadiusDirect()
    {
        Serializable self = this;
        return (uint)((LightBaker)self)._settings.DirectFilter.KernelRadius;
    }

    private void SetSerializedFilterRadiusDirect(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var filter = baker._settings.DirectFilter;
        filter.KernelRadius = (int)(uint)value!;
        baker._settings.DirectFilter = filter;
    }

    private static object? GetFilterRadiusIndirectAttributeDefault() => (uint)DefaultSettings.IndirectFilter.KernelRadius;

    [Serialized(
        "Filter Radius (Indirect)",
        typeof(uint),
        DefaultSource = nameof(GetFilterRadiusIndirectAttributeDefault),
        Setter = nameof(SetSerializedFilterRadiusIndirect),
        Order = 10
    )]
    private object? GetSerializedFilterRadiusIndirect()
    {
        Serializable self = this;
        return (uint)((LightBaker)self)._settings.IndirectFilter.KernelRadius;
    }

    private void SetSerializedFilterRadiusIndirect(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var filter = baker._settings.IndirectFilter;
        filter.KernelRadius = (int)(uint)value!;
        baker._settings.IndirectFilter = filter;
    }

    private static object? GetChunkSizeAttributeDefault() => DefaultSettings.Incremental.ChunkSize;

    [Serialized(
        "Chunk Size",
        typeof(Vector3f),
        DefaultSource = nameof(GetChunkSizeAttributeDefault),
        Setter = nameof(SetSerializedChunkSize),
        Order = 11
    )]
    private object? GetSerializedChunkSize()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.Incremental.ChunkSize;
    }

    private void SetSerializedChunkSize(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var incremental = baker._settings.Incremental;
        incremental.ChunkSize = (Vector3f)value!;
        baker._settings.Incremental = incremental;
    }

    private static object? GetChunkIndirectPaddingAttributeDefault() => DefaultSettings.Incremental.IndirectPadding;

    [Serialized(
        "Chunk Indirect Padding",
        typeof(float),
        DefaultSource = nameof(GetChunkIndirectPaddingAttributeDefault),
        Setter = nameof(SetSerializedChunkIndirectPadding),
        Order = 12
    )]
    private object? GetSerializedChunkIndirectPadding()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.Incremental.IndirectPadding;
    }

    private void SetSerializedChunkIndirectPadding(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var incremental = baker._settings.Incremental;
        incremental.IndirectPadding = (float)value!;
        baker._settings.Incremental = incremental;
    }

    private static object? GetChunkShadowDistanceAttributeDefault() => DefaultSettings.Incremental.DirectionalLightShadowDistance;

    [Serialized(
        "Chunk Shadow Distance",
        typeof(float),
        DefaultSource = nameof(GetChunkShadowDistanceAttributeDefault),
        Setter = nameof(SetSerializedChunkShadowDistance),
        Order = 13
    )]
    private object? GetSerializedChunkShadowDistance()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.Incremental.DirectionalLightShadowDistance;
    }

    private void SetSerializedChunkShadowDistance(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var incremental = baker._settings.Incremental;
        incremental.DirectionalLightShadowDistance = (float)value!;
        baker._settings.Incremental = incremental;
    }

    private static object? GetStitchIterationsAttributeDefault() => DefaultSettings.Stitching.NumIterations;

    [Serialized(
        "Stitch Iterations",
        typeof(uint),
        DefaultSource = nameof(GetStitchIterationsAttributeDefault),
        Setter = nameof(SetSerializedStitchIterations),
        Order = 14
    )]
    private object? GetSerializedStitchIterations()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.Stitching.NumIterations;
    }

    private void SetSerializedStitchIterations(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var stitching = baker._settings.Stitching;
        stitching.NumIterations = (uint)value!;
        baker._settings.Stitching = stitching;
    }

    private static object? GetConstantNormalOffsetAttributeDefault() => DefaultSettings.GeometryBufferBaking.ConstantPositionBias;

    [Serialized(
        "Constant Normal Offset",
        typeof(float),
        DefaultSource = nameof(GetConstantNormalOffsetAttributeDefault),
        Setter = nameof(SetSerializedConstantNormalOffset),
        Order = 15
    )]
    private object? GetSerializedConstantNormalOffset()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.GeometryBufferBaking.ConstantPositionBias;
    }

    private void SetSerializedConstantNormalOffset(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var geometry = baker._settings.GeometryBufferBaking;
        geometry.ConstantPositionBias = (float)value!;
        baker._settings.GeometryBufferBaking = geometry;
    }

    private static object? GetPositionScaledNormalOffsetAttributeDefault() => DefaultSettings.GeometryBufferBaking.ScaledPositionBias;

    [Serialized(
        "Position Scaled Normal Offset",
        typeof(float),
        DefaultSource = nameof(GetPositionScaledNormalOffsetAttributeDefault),
        Setter = nameof(SetSerializedPositionScaledNormalOffset),
        Order = 16
    )]
    private object? GetSerializedPositionScaledNormalOffset()
    {
        Serializable self = this;
        return ((LightBaker)self)._settings.GeometryBufferBaking.ScaledPositionBias;
    }

    private void SetSerializedPositionScaledNormalOffset(object? value)
    {
        Serializable self = this;
        var baker = (LightBaker)self;
        var geometry = baker._settings.GeometryBufferBaking;
        geometry.ScaledPositionBias = (float)value!;
        baker._settings.GeometryBufferBaking = geometry;
    }
}
