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
public class LightBaker : Component
{
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
        var defaultSettings = new LightBakingSettings();

        RegisterAttribute("Output Directory", typeof(string),
            self => ((LightBaker)self)._settings.Incremental.OutputDirectory,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var incremental = baker._settings.Incremental;
                incremental.OutputDirectory = value as string ?? string.Empty;
                baker._settings.Incremental = incremental;
            },
            string.Empty);
        RegisterAttribute("Lightmap Size", typeof(uint),
            self => ((LightBaker)self)._settings.Charting.LightmapSize,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var charting = baker._settings.Charting;
                charting.LightmapSize = (uint)value!;
                baker._settings.Charting = charting;
            },
            defaultSettings.Charting.LightmapSize);
        RegisterAttribute("Texel Density", typeof(float),
            self => ((LightBaker)self)._settings.Charting.TexelDensity,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var charting = baker._settings.Charting;
                charting.TexelDensity = (float)value!;
                baker._settings.Charting = charting;
            },
            defaultSettings.Charting.TexelDensity);
        RegisterAttribute("Quality", typeof(LightBakingQuality),
            self => ((LightBaker)self).GetQuality(),
            (self, value) => ((LightBaker)self).SetQuality((LightBakingQuality)value!),
            LightBakingQuality.Custom,
            enumNames: QualityNames);
        RegisterAttribute("Direct Samples (Lightmap)", typeof(uint),
            self => ((LightBaker)self)._settings.DirectChartTracing.MaxSamples,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var tracing = baker._settings.DirectChartTracing;
                tracing.MaxSamples = (uint)value!;
                baker._settings.DirectChartTracing = tracing;
            },
            defaultSettings.DirectChartTracing.MaxSamples);
        RegisterAttribute("Direct Samples (Light Probes)", typeof(uint),
            self => ((LightBaker)self)._settings.DirectProbesTracing.MaxSamples,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var tracing = baker._settings.DirectProbesTracing;
                tracing.MaxSamples = (uint)value!;
                baker._settings.DirectProbesTracing = tracing;
            },
            defaultSettings.DirectProbesTracing.MaxSamples);
        RegisterAttribute("Indirect Bounces", typeof(uint),
            self => ((LightBaker)self)._settings.IndirectChartTracing.MaxBounces,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var tracing = baker._settings.IndirectChartTracing;
                tracing.MaxBounces = (uint)value!;
                baker._settings.IndirectChartTracing = tracing;
            },
            defaultSettings.IndirectChartTracing.MaxBounces);
        RegisterAttribute("Indirect Samples (Texture)", typeof(uint),
            self => ((LightBaker)self)._settings.IndirectChartTracing.MaxSamples,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var tracing = baker._settings.IndirectChartTracing;
                tracing.MaxSamples = (uint)value!;
                baker._settings.IndirectChartTracing = tracing;
            },
            defaultSettings.IndirectChartTracing.MaxSamples);
        RegisterAttribute("Indirect Samples (Light Probes)", typeof(uint),
            self => ((LightBaker)self)._settings.IndirectProbesTracing.MaxSamples,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var tracing = baker._settings.IndirectProbesTracing;
                tracing.MaxSamples = (uint)value!;
                baker._settings.IndirectProbesTracing = tracing;
            },
            defaultSettings.IndirectProbesTracing.MaxSamples);
        RegisterAttribute("Filter Radius (Direct)", typeof(uint),
            self => (uint)((LightBaker)self)._settings.DirectFilter.KernelRadius,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var filter = baker._settings.DirectFilter;
                filter.KernelRadius = (int)(uint)value!;
                baker._settings.DirectFilter = filter;
            },
            (uint)defaultSettings.DirectFilter.KernelRadius);
        RegisterAttribute("Filter Radius (Indirect)", typeof(uint),
            self => (uint)((LightBaker)self)._settings.IndirectFilter.KernelRadius,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var filter = baker._settings.IndirectFilter;
                filter.KernelRadius = (int)(uint)value!;
                baker._settings.IndirectFilter = filter;
            },
            (uint)defaultSettings.IndirectFilter.KernelRadius);
        RegisterAttribute("Chunk Size", typeof(Vector3f),
            self => ((LightBaker)self)._settings.Incremental.ChunkSize,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var incremental = baker._settings.Incremental;
                incremental.ChunkSize = (Vector3f)value!;
                baker._settings.Incremental = incremental;
            },
            defaultSettings.Incremental.ChunkSize);
        RegisterAttribute("Chunk Indirect Padding", typeof(float),
            self => ((LightBaker)self)._settings.Incremental.IndirectPadding,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var incremental = baker._settings.Incremental;
                incremental.IndirectPadding = (float)value!;
                baker._settings.Incremental = incremental;
            },
            defaultSettings.Incremental.IndirectPadding);
        RegisterAttribute("Chunk Shadow Distance", typeof(float),
            self => ((LightBaker)self)._settings.Incremental.DirectionalLightShadowDistance,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var incremental = baker._settings.Incremental;
                incremental.DirectionalLightShadowDistance = (float)value!;
                baker._settings.Incremental = incremental;
            },
            defaultSettings.Incremental.DirectionalLightShadowDistance);
        RegisterAttribute("Stitch Iterations", typeof(uint),
            self => ((LightBaker)self)._settings.Stitching.NumIterations,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var stitching = baker._settings.Stitching;
                stitching.NumIterations = (uint)value!;
                baker._settings.Stitching = stitching;
            },
            defaultSettings.Stitching.NumIterations);
        RegisterAttribute("Constant Normal Offset", typeof(float),
            self => ((LightBaker)self)._settings.GeometryBufferBaking.ConstantPositionBias,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var geometry = baker._settings.GeometryBufferBaking;
                geometry.ConstantPositionBias = (float)value!;
                baker._settings.GeometryBufferBaking = geometry;
            },
            defaultSettings.GeometryBufferBaking.ConstantPositionBias);
        RegisterAttribute("Position Scaled Normal Offset", typeof(float),
            self => ((LightBaker)self)._settings.GeometryBufferBaking.ScaledPositionBias,
            (self, value) =>
            {
                var baker = (LightBaker)self;
                var geometry = baker._settings.GeometryBufferBaking;
                geometry.ScaledPositionBias = (float)value!;
                baker._settings.GeometryBufferBaking = geometry;
            },
            defaultSettings.GeometryBufferBaking.ScaledPositionBias);

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

    /// <summary>Register object factory. Drawable must be registered first.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<LightBaker>(ObjectCategory.Category_Subsystem);
        // TODO: port URHO3D_ACTION_DYNAMIC_LABEL("Bake", BakeAsync, GetBakeLabel);
    }

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
}
