//
// Copyright (c) 2017-2020 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using SystemMath = global::System.Math;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.Glow;

/// <summary>Region on the lightmap chart.</summary>
public struct LightmapChartRegion
{
    /// <summary>Construct default.</summary>
    public LightmapChartRegion()
    {
        RectTexels = new IntRect();
        RectUv = new Rect();
    }

    /// <summary>Construct actual region.</summary>
    public LightmapChartRegion(int index, in Vector2i position, in Vector2i size, int maxSize)
    {
        ChartIndex = index;
        RectTexels = new IntRect(position, position + size);
        RectUv = new Rect
        {
            Min = RectTexels.Min.ToVector2() / maxSize,
            Max = RectTexels.Max.ToVector2() / maxSize
        };
    }

    /// <summary>Return lightmap scale of the region.</summary>
    public readonly Vector2f GetScale() => RectUv.Size;
    /// <summary>Return lightmap offset of the region.</summary>
    public readonly Vector2f GetOffset() => RectUv.Min;
    /// <summary>Return lightmap scale &amp; offset vector.</summary>
    public readonly Vector4f GetScaleOffset()
    {
        var offset = GetOffset();
        var size = GetScale();
        return new Vector4f(size.X, size.Y, offset.X, offset.Y);
    }

    /// <summary>Lightmap chart index.</summary>
    public int ChartIndex;
    /// <summary>Lightmap rectangle on the chart (in texels).</summary>
    public IntRect RectTexels;
    /// <summary>Lightmap rectangle on the chart (in normalized coordinates).</summary>
    public Rect RectUv;
}

/// <summary>Individual element of the lightmap chart.</summary>
public struct LightmapChartElement
{
    /// <summary>Component.</summary>
    public Component? Component;
    /// <summary>Object index, unique within array of charts.</summary>
    public int ObjectIndex;
    /// <summary>Allocated region.</summary>
    public LightmapChartRegion Region;
}

/// <summary>Lightmap chart description.</summary>
public struct LightmapChart : ICloneable
{
    /// <summary>Lightmap chart index.</summary>
    public int Index;
    /// <summary>Lightmap chart size.</summary>
    public int LightmapSize;
    /// <summary>Used region allocator.</summary>
    public AreaAllocator Allocator;
    /// <summary>Allocated elements.</summary>
    public List<LightmapChartElement> Elements;

    /// <summary>Construct default.</summary>
    public LightmapChart()
    {
        Allocator = new AreaAllocator();
        Elements = [];
    }

    /// <summary>Construct valid.</summary>
    public LightmapChart(int index, int size)
    {
        Index = index;
        LightmapSize = size;
        Allocator = new AreaAllocator(size, size, 0, 0, false);
        Elements = [];
    }

    public readonly LightmapChart Clone()
    {
        var clone = this;
        clone.Allocator = (AreaAllocator)Allocator.Clone();
        clone.Elements = new List<LightmapChartElement>(Elements);
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();
}

public static class LightmapCharter
{
    /// <summary>Calculate lightmap size for given model with given scale.</summary>
    private static Vector2i CalculateModelLightmapSize(float texelDensity, float minObjectScale,
        Model? model, in Vector3f scale, float scaleInLightmap, in Vector2i defaultChartSize)
    {
        ref readonly var modelLightmapSizeVar = ref model!.GetMetadata(LightmapUVGenerationSettings.LightmapSizeKey);
        ref readonly var modelLightmapDensityVar = ref model.GetMetadata(LightmapUVGenerationSettings.LightmapDensityKey);

        if (modelLightmapSizeVar.IsEmpty || modelLightmapDensityVar.IsEmpty)
        {
            Log.GetLogger().LogWarning("Cannot calculate chart size for model '{Model}', fallback to default.",
                model.GetName());
            return defaultChartSize;
        }

        var modelLightmapSize = modelLightmapSizeVar.GetIntVector2().ToVector2();
        var modelLightmapDensity = modelLightmapDensityVar.GetFloat();

        var nodeScale = MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z));
        var rescaleFactor = nodeScale * texelDensity / modelLightmapDensity;
        var clampedRescaleFactor = MathF.Max(minObjectScale, rescaleFactor);

        return Vector2f.CeilToInt(modelLightmapSize * clampedRescaleFactor * scaleInLightmap);
    }

    /// <summary>Adjust size to lightmap chart size.</summary>
    private static Vector2i AdjustRegionSize(in Vector2i desiredSize, int maxSize)
    {
        var desiredDimensions = SystemMath.Max(desiredSize.X, desiredSize.Y);
        if (desiredDimensions <= maxSize)
            return desiredSize;

        var scale = (float)maxSize / desiredDimensions;
        var newSize = desiredSize.ToVector2() * scale;
        return Vector2i.Max(Vector2i.One, Vector2i.Min(Vector2f.CeilToInt(newSize), Vector2i.One * maxSize));
    }

    /// <summary>Allocate region in the set of lightmap charts.</summary>
    private static LightmapChartRegion AllocateLightmapChartRegion(in LightmapChartingSettings settings,
        List<LightmapChart> charts, in Vector2i size, int baseChartIndex)
    {
        var padding = (int)settings.Padding;
        var paddedSize = size + 2 * padding * Vector2i.One;

        // Try existing charts
        var chartIndex = 0;
        foreach (var lightmapDesc in charts)
        {
            var paddedPosition = new Vector2i();
            if (lightmapDesc.Allocator.Allocate(
                paddedSize.X, paddedSize.Y, ref paddedPosition.X, ref paddedPosition.Y))
            {
                var position = paddedPosition + padding * Vector2i.One;
                return new LightmapChartRegion(chartIndex, position, size, (int)settings.LightmapSize);
            }
            ++chartIndex;
        }

        // Create general-purpose chart
        var chart = new LightmapChart(chartIndex + baseChartIndex, (int)settings.LightmapSize);
        charts.Add(chart);

        // Allocate region from the new chart
        var newPaddedPosition = new Vector2i();
        var success = chart.Allocator.Allocate(
            paddedSize.X, paddedSize.Y, ref newPaddedPosition.X, ref newPaddedPosition.Y);

        Debug.Assert(success);
        Debug.Assert(newPaddedPosition == Vector2i.Zero);

        var newPosition = newPaddedPosition + padding * Vector2i.One;
        return new LightmapChartRegion(chartIndex, newPosition, size, (int)settings.LightmapSize);
    }

    /// <summary>Calculate size in lightmap for StaticModel component.</summary>
    private static Vector2i CalculateStaticModelLightmapSize(
        StaticModel? staticModel, in LightmapChartingSettings settings)
    {
        var node = staticModel!.Node!;
        var model = staticModel.GetModel();
        var defaultChartSize = Vector2i.One * (int)settings.DefaultChartSize;
        return CalculateModelLightmapSize(settings.TexelDensity, settings.MinObjectScale, model,
            node.GetWorldScale(), staticModel.GetScaleInLightmap(), defaultChartSize);
    }

    /// <summary>Calculate size in lightmap for Terrain component.</summary>
    private static Vector2i CalculateTerrainLightmapSize(Terrain? terrain, in LightmapChartingSettings settings)
    {
        var node = terrain!.Node!;
        var spacing = terrain.GetSpacing();
        var worldScale = node.GetWorldScale();
        var size = terrain.GetNumPatches().ToVector2() * terrain.GetPatchSize();
        var dimensions = size * new Vector2f(worldScale.X, worldScale.Z) * new Vector2f(spacing.X, spacing.Z);
        var scaleInLightmap = terrain.GetScaleInLightmap();
        return Vector2f.CeilToInt(dimensions * settings.TexelDensity * scaleInLightmap);
    }

    /// <summary>Calculate size in lightmap for component.</summary>
    private static Vector2i CalculateGeometryLightmapSize(Component? component, in LightmapChartingSettings settings)
    {
        if (component is StaticModel staticModel)
            return CalculateStaticModelLightmapSize(staticModel, settings);
        if (component is Terrain terrain)
            return CalculateTerrainLightmapSize(terrain, settings);
        return new Vector2i();
    }

    /// <summary>Region to be requested for chunk.</summary>
    private struct RequestedChartRegion
    {
        /// <summary>Index of the object.</summary>
        public int ObjectIndex;
        /// <summary>Adjusted region size.</summary>
        public Vector2i AdjustedRegionSize;
        /// <summary>Component to bake.</summary>
        public Component? Component;
    }

    /// <summary>Generate lightmap charts for given nodes.</summary>
    public static List<LightmapChart> GenerateLightmapCharts(
        IReadOnlyList<Component?> geometries, in LightmapChartingSettings settings, int baseChartIndex = 0)
    {
        // Collect object regions
        var maxRegionSize = unchecked((int)(settings.LightmapSize - settings.Padding * 2));
        var requestedRegions = new List<RequestedChartRegion>();
        for (var objectIndex = 0; objectIndex < geometries.Count; ++objectIndex)
        {
            var component = geometries[objectIndex];
            var node = component!.Node!;

            var regionSize = CalculateGeometryLightmapSize(component, settings);
            var adjustedRegionSize = AdjustRegionSize(regionSize, maxRegionSize);

            if (regionSize != adjustedRegionSize)
            {
                Log.GetLogger().LogWarning(
                    "Object '{Object}' doesn't fit the lightmap chart, texel density is lowered.", node.GetName());
            }

            requestedRegions.Add(new RequestedChartRegion
            {
                ObjectIndex = objectIndex,
                AdjustedRegionSize = adjustedRegionSize,
                Component = component
            });
        }

        // Sort regions by max dimensions
        requestedRegions.Sort(static (lhs, rhs) =>
        {
            var lhsRegion = lhs.AdjustedRegionSize;
            var rhsRegion = rhs.AdjustedRegionSize;
            return SystemMath.Max(rhsRegion.X, rhsRegion.Y).CompareTo(SystemMath.Max(lhsRegion.X, lhsRegion.Y));
        });

        // Generate charts
        var charts = new List<LightmapChart>();
        foreach (var requestedRegion in requestedRegions)
        {
            var region = AllocateLightmapChartRegion(
                settings, charts, requestedRegion.AdjustedRegionSize, baseChartIndex);

            var chartElement = new LightmapChartElement
            {
                Component = requestedRegion.Component,
                ObjectIndex = requestedRegion.ObjectIndex,
                Region = region
            };
            charts[region.ChartIndex].Elements.Add(chartElement);
        }
        return charts;
    }

    /// <summary>Apply lightmap charts to scene components.</summary>
    public static void ApplyLightmapCharts(IReadOnlyList<LightmapChart> charts)
    {
        foreach (var chart in charts)
        {
            foreach (var element in chart.Elements)
            {
                var component = element.Component;
                Helpers.SetLightmapIndex(component!, chart.Index);
                Helpers.SetLightmapScaleOffset(component!, element.Region.GetScaleOffset());
            }
        }
    }
}
