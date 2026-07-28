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
using System.Diagnostics;
using System.Threading.Tasks;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Scene;

public static class Helpers
{
    /// <summary>Parallel loop.</summary>
    public static void ParallelFor(int count, int numTasks, Action<int, int> callback)
    {
        // Post async tasks
        var tasks = new Task[numTasks];
        var chunkSize = (count + numTasks - 1) / numTasks;
        for (var i = 0; i < numTasks; ++i)
        {
            var taskIndex = i;
            tasks[i] = Task.Run(() =>
            {
                var fromIndex = taskIndex * chunkSize;
                var toIndex = System.Math.Min(fromIndex + chunkSize, count);
                callback(fromIndex, toIndex);
            });
        }

        // Wait for async tasks
        for (var i = 0; i < tasks.Length; ++i)
            tasks[i].Wait();
    }

    /// <summary>Return whether the material is opaque.</summary>
    public static bool IsMaterialOpaque(Material material)
    {
        var technique = material.GetTechnique(0);
        if (technique is not null && technique.GetName().Contains("alpha", StringComparison.OrdinalIgnoreCase))
            return false;

        if (material.GetPixelShaderDefines().Contains("ALPHAMASK", StringComparison.Ordinal))
            return false;

        if (((Vector4f)material.GetShaderParameter("MatDiffColor")!).W < 1.0f - MathDefs.LargeEpsilon)
            return false;

        return true;
    }

    /// <summary>Return material diffuse color.</summary>
    public static Color GetMaterialDiffuseColor(Material material)
    {
        return new Color((Vector4f)material.GetShaderParameter("MatDiffColor")!);
    }

    /// <summary>Return material diffuse texture and UV offsets.</summary>
    public static Texture? GetMaterialDiffuseTexture(Material material, ref Vector4f uOffset, ref Vector4f vOffset)
    {
        var texture = material.GetTexture(ShaderResources.Albedo);
        if (texture is null)
            return null;

        uOffset = (Vector4f)material.GetShaderParameter("UOffset")!;
        vOffset = (Vector4f)material.GetShaderParameter("VOffset")!;
        return texture;
    }

    /// <summary>Set lightmap index for component.</summary>
    public static void SetLightmapIndex(Component component, int lightmapIndex)
    {
        if (component is StaticModel staticModel)
            staticModel.SetLightmapIndex((uint)lightmapIndex);
        else if (component is Terrain terrain)
            terrain.SetLightmapIndex((uint)lightmapIndex);
        else
            Debug.Assert(false);
    }

    /// <summary>Return lightmap index for component.</summary>
    public static int GetLightmapIndex(Component component)
    {
        if (component is StaticModel staticModel)
            return (int)staticModel.GetLightmapIndex();
        if (component is Terrain terrain)
            return (int)terrain.GetLightmapIndex();

        Debug.Assert(false);
        return 0;
    }

    /// <summary>Set lightmap scale and offset for component.</summary>
    public static void SetLightmapScaleOffset(Component component, in Vector4f scaleOffset)
    {
        if (component is StaticModel staticModel)
            staticModel.SetLightmapScaleOffset(scaleOffset);
        else if (component is Terrain terrain)
            terrain.SetLightmapScaleOffset(scaleOffset);
        else
            Debug.Assert(false);
    }

    /// <summary>Return lightmap scale and offset for component.</summary>
    public static Vector4f GetLightmapScaleOffset(Component component)
    {
        if (component is StaticModel staticModel)
            return staticModel.GetLightmapScaleOffset();
        if (component is Terrain terrain)
            return terrain.GetLightmapScaleOffset();

        Debug.Assert(false);
        return Vector4f.Zero;
    }

    /// <summary>Create material for geometry buffer baking.</summary>
    public static Material CreateBakingMaterial(Material bakingMaterial, Material? sourceMaterial,
        in Vector4f scaleOffset, int tapIndex, int numTaps, in Vector2f tapOffset,
        int geometryId, in Vector2f scaledAndConstBias)
    {
        var renderer = bakingMaterial.GetSubsystem<Renderer>();
        sourceMaterial ??= renderer.GetDefaultMaterial()!;

        var tapOffset4 = new Vector4f(0.0f, 0.0f, tapOffset.X, tapOffset.Y);
        var tapDepth = 1.0f - (float)(tapIndex + 1) / (numTaps + 1);

        var material = bakingMaterial.Clone();
        material.SetShaderParameter("LMOffset", scaleOffset + tapOffset4);
        material.SetShaderParameter("LightmapLayer", tapDepth);
        material.SetShaderParameter("LightmapGeometry", (float)geometryId);
        material.SetShaderParameter("LightmapPositionBias", scaledAndConstBias);
        material.SetShaderParameter("MatDiffColor", (Vector4f)sourceMaterial.GetShaderParameter("MatDiffColor")!);
        material.SetShaderParameter("MatEmissiveColor", (Vector3f)sourceMaterial.GetShaderParameter("MatEmissiveColor")!);
        material.SetShaderParameter("UOffset", (Vector4f)sourceMaterial.GetShaderParameter("UOffset")!);
        material.SetShaderParameter("VOffset", (Vector4f)sourceMaterial.GetShaderParameter("VOffset")!);

        var shaderDefines = string.Empty;

        var diffuseMap = sourceMaterial.GetTexture(ShaderResources.Albedo);
        if (diffuseMap is not null)
        {
            material.SetTexture(ShaderResources.Albedo, diffuseMap);
            shaderDefines += "DIFFMAP ";
        }
        var emissiveMap = sourceMaterial.GetTexture(ShaderResources.Emission);
        if (emissiveMap is not null)
        {
            material.SetTexture(ShaderResources.Emission, emissiveMap);
            shaderDefines += "EMISSIVEMAP ";
        }

        material.SetVertexShaderDefines(shaderDefines);
        material.SetPixelShaderDefines(shaderDefines);

        return material;
    }
}
