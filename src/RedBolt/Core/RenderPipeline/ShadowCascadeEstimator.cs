// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.RenderPipeline;

using SystemMath = global::System.Math;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Settings used to estimate directional shadow cascades.</summary>
public struct ShadowCascadeEstimateSettings
{
    /// <summary>Construct with the maximum shadow distance and default quality settings.</summary>
    public ShadowCascadeEstimateSettings(float maxDistance)
    {
        MaxDistance = maxDistance;
        ShadowResolution = 1.0f;
        MaxCascades = 0;
        TargetPixelsPerTexel = 2.0f;
        SplitLambda = 0.7f;
        FadeStart = Light.DefaultShadowFadeStart;
        BlendFactor = 0.1f;
        BiasAutoAdjust = Light.DefaultBiasAutoAdjust;
    }

    /// <summary>Maximum distance from the camera that receives shadows.</summary>
    public float MaxDistance { get; set; }

    /// <summary>Shadow resolution scale applied by the light.</summary>
    public float ShadowResolution { get; set; }

    /// <summary>Maximum cascade count, or zero to select the platform default.</summary>
    public int MaxCascades { get; set; }

    /// <summary>Maximum desired screen pixels covered by one shadow texel.</summary>
    public float TargetPixelsPerTexel { get; set; }

    /// <summary>Blend between uniform and logarithmic split placement.</summary>
    public float SplitLambda { get; set; }

    /// <summary>Start of shadow fade relative to the maximum shadow distance.</summary>
    public float FadeStart { get; set; }

    /// <summary>Fraction of each cascade range used to blend into the next cascade.</summary>
    public float BlendFactor { get; set; }

    /// <summary>Automatic depth bias adjustment strength.</summary>
    public float BiasAutoAdjust { get; set; }
}

/// <summary>Estimates fixed directional shadow cascade parameters for the current environment.</summary>
public static class ShadowCascadeEstimator
{
    /// <summary>Estimate cascade count and split distances.</summary>
    public static CascadeParameters Estimate(Camera camera, Vector2i viewportSize,
        RenderPipelineSettings pipelineSettings, ShadowCascadeEstimateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (viewportSize.X <= 0 || viewportSize.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportSize));
        if (!(settings.MaxDistance > 0.0f))
            throw new ArgumentOutOfRangeException(nameof(settings), "Maximum shadow distance must be positive.");
        if (!(settings.ShadowResolution > 0.0f))
            throw new ArgumentOutOfRangeException(nameof(settings), "Shadow resolution must be positive.");
        if (!(settings.TargetPixelsPerTexel > 0.0f))
            throw new ArgumentOutOfRangeException(nameof(settings), "Target pixels per texel must be positive.");
        if (settings.MaxCascades < 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "Maximum cascade count cannot be negative.");

        var nearClip = SystemMath.Max(camera.GetNearClip(), MathDefs.Epsilon);
        var farClip = SystemMath.Min(camera.GetFarClip(), settings.MaxDistance);
        if (farClip <= nearClip)
            return CreateParameters(farClip, 0.0f, 0.0f, 0.0f, settings);

        var platformCap = ProcessUtils.IsMobile() ? 3 : Light.MaxCascadeSplits;
        var maxCascades = settings.MaxCascades == 0
            ? platformCap
            : SystemMath.Clamp(settings.MaxCascades, 1, platformCap);
        var lambda = MathDefs.Clamp(settings.SplitLambda, 0.0f, 1.0f);
        var selectedCount = maxCascades;
        Span<float> selectedSplits = stackalloc float[Light.MaxCascadeSplits];
        Span<float> candidateSplits = stackalloc float[Light.MaxCascadeSplits];

        for (var cascadeCount = 1; cascadeCount <= maxCascades; ++cascadeCount)
        {
            candidateSplits.Clear();
            CalculateSplits(candidateSplits, cascadeCount, nearClip, farClip, lambda);
            var splitResolution = GetSplitResolution(pipelineSettings, settings.ShadowResolution, cascadeCount);
            var viewportPixels = SystemMath.Max(viewportSize.X, viewportSize.Y);
            var splitNear = nearClip;
            var worstPixelsPerTexel = 0.0f;
            for (var i = 0; i < cascadeCount; ++i)
            {
                var splitFar = candidateSplits[i];
                worstPixelsPerTexel = SystemMath.Max(worstPixelsPerTexel,
                    viewportPixels / splitResolution * splitFar / splitNear);
                splitNear = splitFar;
            }

            if (worstPixelsPerTexel <= settings.TargetPixelsPerTexel || cascadeCount == maxCascades)
            {
                candidateSplits.CopyTo(selectedSplits);
                selectedCount = cascadeCount;
                break;
            }
        }

        for (var i = selectedCount; i < Light.MaxCascadeSplits; ++i)
            selectedSplits[i] = 0.0f;

        return CreateParameters(selectedSplits[0], selectedSplits[1], selectedSplits[2], selectedSplits[3], settings);
    }

    private static CascadeParameters CreateParameters(float split1, float split2, float split3, float split4,
        ShadowCascadeEstimateSettings settings)
    {
        var parameters = new CascadeParameters(split1, split2, split3, split4, settings.FadeStart,
            settings.BiasAutoAdjust)
        {
            BlendFactor = settings.BlendFactor
        };
        parameters.Validate();
        return parameters;
    }

    private static void CalculateSplits(Span<float> splits, int cascadeCount, float nearClip, float farClip,
        float lambda)
    {
        var clipRatio = farClip / nearClip;
        for (var i = 1; i <= cascadeCount; ++i)
        {
            var factor = (float)i / cascadeCount;
            var logarithmicSplit = nearClip * MathF.Pow(clipRatio, factor);
            var uniformSplit = nearClip + (farClip - nearClip) * factor;
            splits[i - 1] = MathDefs.Lerp(uniformSplit, logarithmicSplit, lambda);
        }
        splits[cascadeCount - 1] = farClip;
    }

    private static float GetSplitResolution(RenderPipelineSettings pipelineSettings, float resolutionScale,
        int cascadeCount)
    {
        var pageSize = pipelineSettings.ShadowMapAllocator.ShadowAtlasPageSize;
        var maxSplitSize = cascadeCount == 1 ? pageSize : pageSize / 2;
        var directionalSize = SystemMath.Min(pipelineSettings.SceneProcessor.DirectionalShadowSize, maxSplitSize);
        return SystemMath.Max(SceneProcessor.ShadowMinPixels, directionalSize * resolutionScale);
    }
}
