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

using RedBolt.Graphics;
using RedBolt.Math;

public static class LightmapFilter
{
    private static ReadOnlySpan<float> GetKernel(int radius) => radius switch
    {
        0 => [1.0f],
        1 => [0.684538f, 0.157731f],
        2 => [0.38774f, 0.24477f, 0.06136f],
        3 => [0.266346f, 0.215007f, 0.113085f, 0.038735f],
        4 => [0.20236f, 0.179044f, 0.124009f, 0.067234f, 0.028532f],
        5 => [0.163053f, 0.150677f, 0.118904f, 0.080127f, 0.046108f, 0.022657f],
        _ => throw new ArgumentOutOfRangeException(nameof(radius))
    };

    private static float GetLuminance(Vector3f color) => new Color(color).Luma();
    private static float GetLuminance(Vector4f color) => new Color(color.ToVector3()).Luma();

    private static float CalculateEdgeWeight(float luminance1, float luminance2, float luminanceSigma,
        Vector3f position1, Vector3f position2, float positionSigma,
        Vector3f normal1, Vector3f normal2, float normalPower)
    {
        var colorWeight = MathF.Abs(luminance1 - luminance2) / luminanceSigma;
        var positionWeight = positionSigma > MathDefs.Epsilon
            ? (position1 - position2).LengthSquared / positionSigma : 0.0f;
        var normalWeight = MathF.Pow(MathF.Max(0.0f, normal1.DotProduct(normal2)), normalPower);
        return MathF.Exp(-colorWeight - positionWeight) * normalWeight;
    }

    public static void FilterDirectLight(LightmapChartBakedDirect bakedDirect, Vector3f[] output,
        LightmapChartGeometryBuffer geometryBuffer, EdgeStoppingGaussFilterParameters parameters, int numTasks)
    {
        FilterArray(bakedDirect.DirectLight, output, geometryBuffer, parameters, numTasks, GetLuminance,
            static (value, weight) => value * weight,
            static (lhs, rhs) => lhs + rhs,
            static (value, weight) => value / weight);
    }

    public static void FilterIndirectLight(LightmapChartBakedIndirect bakedIndirect, Vector4f[] output,
        LightmapChartGeometryBuffer geometryBuffer, EdgeStoppingGaussFilterParameters parameters, int numTasks)
    {
        FilterArray(bakedIndirect.Light, output, geometryBuffer, parameters, numTasks, GetLuminance,
            static (value, weight) => value * weight,
            static (lhs, rhs) => lhs + rhs,
            static (value, weight) => value / weight);
    }

    private static void FilterArray<T>(T[] input, T[] output, LightmapChartGeometryBuffer geometryBuffer,
        EdgeStoppingGaussFilterParameters parameters, int numTasks, Func<T, float> getLuminance,
        Func<T, float, T> scale, Func<T, T, T> add, Func<T, float, T> divide)
        where T : struct
    {
        var kernelWeights = GetKernel(parameters.KernelRadius).ToArray();
        Helpers.ParallelFor(input.Length, numTasks, (fromIndex, toIndex) =>
        {
            for (var index = fromIndex; index < toIndex; ++index)
            {
                if (geometryBuffer.GeometryIds[index] == 0)
                {
                    output[index] = default;
                    continue;
                }

                var centerLocation = geometryBuffer.IndexToLocation(index);
                var centerColor = input[index];
                var centerLuminance = getLuminance(centerColor);
                var centerPosition = geometryBuffer.Positions[index];
                var centerNormal = geometryBuffer.SmoothNormals[index];
                var colorWeight = kernelWeights[0] * kernelWeights[0];
                var colorSum = scale(centerColor, colorWeight);

                for (var dy = -parameters.KernelRadius; dy <= parameters.KernelRadius; ++dy)
                {
                    for (var dx = -parameters.KernelRadius; dx <= parameters.KernelRadius; ++dx)
                    {
                        if (dx == 0 && dy == 0)
                            continue;
                        var otherLocation = centerLocation + new Vector2i(dx, dy) * parameters.Upscale;
                        if (!geometryBuffer.IsValidLocation(otherLocation))
                            continue;
                        var otherIndex = geometryBuffer.LocationToIndex(otherLocation);
                        if (geometryBuffer.GeometryIds[otherIndex] == 0)
                            continue;

                        var dxdy = new Vector2f(dx, dy).Length;
                        var kernel = kernelWeights[Math.Abs(dx)] * kernelWeights[Math.Abs(dy)];
                        var otherColor = input[otherIndex];
                        var weight = CalculateEdgeWeight(centerLuminance, getLuminance(otherColor),
                            parameters.LuminanceSigma, centerPosition, geometryBuffer.Positions[otherIndex],
                            dxdy * parameters.PositionSigma, centerNormal,
                            geometryBuffer.SmoothNormals[otherIndex], parameters.NormalPower);
                        colorSum = add(colorSum, scale(otherColor, weight * kernel));
                        colorWeight += weight * kernel;
                    }
                }

                output[index] = divide(colorSum, MathF.Max(MathDefs.Epsilon, colorWeight));
            }
        });
    }
}
