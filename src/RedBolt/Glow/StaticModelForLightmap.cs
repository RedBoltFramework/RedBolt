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

namespace RedBolt.Glow;

using System;
using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;

/// <summary>Static model for rendering into lightmap. Lods, culling and features unrelated to rendering are disabled.</summary>
[Reflected(ObjectCategory.Category_Geometry, InheritAttributes = false)]
public partial class StaticModelForLightmap : Drawable
{
    /// <summary>Construct.</summary>
    public StaticModelForLightmap()
        : base(DrawableFlags.Geometry)
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        base.Dispose();
    }

    /// <summary>Initialize. Return mapping for each batch.</summary>
    public List<GeometryIdToObjectMapping> Initialize(
        int objectIndex, StaticModel? sourceObject, Material? bakingMaterial, int baseGeometryId,
        ReadOnlySpan<Vector2f> multiTapOffsets, in Vector2f texelSize, in Vector4f scaleOffset,
        in Vector2f scaledAndConstBias)
    {
        SetDistance(0.0f);
        SetLodDistance(0.0f);
        _worldBoundingBox = new BoundingBox(-Vector3f.One * MathDefs.LargeValue, Vector3f.One * MathDefs.LargeValue);

        var sourceModel = sourceObject!.GetModel()!;

        var sharedLightmapUv = sourceModel.GetMetadata(LightmapUVGenerationSettings.LightmapSharedUV).GetBool();

        var mapping = new List<GeometryIdToObjectMapping>();
        for (var geometryIndex = 0; geometryIndex < (int)sourceModel.GetNumGeometries(); ++geometryIndex)
        {
            var sourceMaterial = sourceObject.GetMaterial((uint)geometryIndex);
            var numLods = (int)sourceModel.GetNumGeometryLodLevels((uint)geometryIndex);
            if (numLods == 0)
                continue;

            // Render all lods if necessary, render only one otherwise
            var numLodsToRender = !sharedLightmapUv ? numLods : 1;
            for (var lodIndex = 0; lodIndex < numLodsToRender; ++lodIndex)
            {
                for (var tap = 0; tap < multiTapOffsets.Length; ++tap)
                {
                    var tapOffset = multiTapOffsets[tap] * texelSize;
                    var tapOffset4 = new Vector4f(0.0f, 0.0f, tapOffset.X, tapOffset.Y);
                    var tapDepth = 1.0f - (float)(tap + 1) / (multiTapOffsets.Length + 1);

                    var material = Helpers.CreateBakingMaterial(bakingMaterial!, sourceMaterial,
                        scaleOffset, tap, multiTapOffsets.Length, tapOffset, baseGeometryId + mapping.Count,
                        scaledAndConstBias);

                    _worldTransform[0] = Node!.GetWorldTransform();
                    var batch = new SourceBatch
                    {
                        Distance = 0.0f,
                        Geometry = sourceModel.GetGeometry((uint)geometryIndex, (uint)lodIndex),
                        GeometryType = GeometryType.StaticNoInstancing,
                        Material = material,
                        NumWorldTransforms = 1,
                        WorldTransform = _worldTransform
                    };
                    _batches.Add(batch);
                }

                mapping.Add(new GeometryIdToObjectMapping
                {
                    ObjectIndex = objectIndex,
                    GeometryIndex = geometryIndex,
                    LodIndex = lodIndex
                });
            }
        }

        return mapping;
    }

    /// <summary>Recalculate the world-space bounding box.</summary>
    protected override void OnWorldBoundingBoxUpdate()
    {
    }
}
