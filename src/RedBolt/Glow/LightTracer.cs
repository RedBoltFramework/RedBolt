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

using RedBolt.Bindings.Embree;
using RedBolt.Graphics;
using RedBolt.Math;

/// <summary>Direct light accumulated for one lightmap chart.</summary>
public sealed class LightmapChartBakedDirect
{
    public LightmapChartBakedDirect(int lightmapSize)
    {
        LightmapSize = lightmapSize;
        RealLightmapSize = lightmapSize;
        DirectLight = new Vector3f[lightmapSize * lightmapSize];
        SurfaceLight = new Vector3f[lightmapSize * lightmapSize];
        Albedo = new Vector3f[lightmapSize * lightmapSize];
    }

    public int LightmapSize { get; }
    public float RealLightmapSize { get; }
    public Vector3f[] DirectLight { get; }
    public Vector3f[] SurfaceLight { get; }
    public Vector3f[] Albedo { get; }

    public Vector2i GetNearestLocation(Vector2f uv)
    {
        var x = (int)MathF.Floor(MathF.Min(uv.X * RealLightmapSize, RealLightmapSize - 1.0f));
        var y = (int)MathF.Floor(MathF.Min(uv.Y * RealLightmapSize, RealLightmapSize - 1.0f));
        return new Vector2i(x, y);
    }

    public Vector3f GetSurfaceLight(Vector2i location) =>
        SurfaceLight[location.X + location.Y * LightmapSize];

    public Vector3f GetAlbedo(Vector2i location) =>
        Albedo[location.X + location.Y * LightmapSize];
}

/// <summary>Indirect light accumulated for one lightmap chart.</summary>
public sealed class LightmapChartBakedIndirect
{
    public LightmapChartBakedIndirect(int lightmapSize)
    {
        LightmapSize = lightmapSize;
        Light = new Vector4f[lightmapSize * lightmapSize];
    }

    public int LightmapSize { get; }
    public Vector4f[] Light { get; }

    public void NormalizeLight()
    {
        for (var i = 0; i < Light.Length; ++i)
        {
            if (Light[i].W > 0.0f)
                Light[i] /= Light[i].W;
        }
    }
}

public static class LightTracer
{
    private static float CalculateBiasScale(Vector3f position) =>
        MathF.Min(1.0f, MathF.Max(MathF.Abs(position.X), MathF.Max(MathF.Abs(position.Y), MathF.Abs(position.Z))));

    private static Vector2f RandomCircleOffset()
    {
        var random = RandomEngine.GetDefaultEngine();
        Vector2f result;
        do
        {
            result = random.GetVector2(-Vector2f.One, Vector2f.One);
        } while (result.LengthSquared > 1.0f);
        return result;
    }

    private static void GetBasis(Vector3f e3, out Vector3f e1, out Vector3f e2)
    {
        e2 = MathF.Abs(e3.X) > MathF.Abs(e3.Y)
            ? new Vector3f(-e3.Z, 0.0f, e3.X).Normalized
            : new Vector3f(0.0f, e3.Z, -e3.Y).Normalized;
        e1 = e2.CrossProduct(e3);
    }

    private static Vector3f RandomHemisphereDirectionCos(Vector3f normal)
    {
        var random = RandomEngine.GetDefaultEngine();
        var phi = random.GetFloat() * 2.0f * MathF.PI;
        var sinPhi = MathF.Sin(phi);
        var cosPhi = MathF.Cos(phi);
        var sinThetaSquared = random.GetFloat();
        var sinTheta = MathF.Sqrt(sinThetaSquared);
        GetBasis(normal, out var e1, out var e2);
        return e1 * (cosPhi * sinTheta) + e2 * (sinPhi * sinTheta)
            + normal * MathF.Sqrt(1.0f - sinThetaSquared);
    }

    private static int CalculateNumSamples(BakedLight light, uint maxSamples) => light.LightType switch
    {
        LightType.Directional => light.Angle < MathDefs.LargeEpsilon ? 1 : checked((int)maxSamples),
        _ => light.Radius < MathDefs.LargeEpsilon ? 1 : checked((int)maxSamples)
    };

    private static bool IsUnwantedLod(RaytracerGeometry currentGeometry, RaytracerGeometry hitGeometry)
    {
        var hitLod = hitGeometry.LodIndex != 0;
        var sameGeometry = currentGeometry.ObjectIndex == hitGeometry.ObjectIndex
            && currentGeometry.GeometryIndex == hitGeometry.GeometryIndex;
        return (!sameGeometry && hitLod) || (sameGeometry && hitGeometry.LodIndex != currentGeometry.LodIndex);
    }

    private static Color GetHitDiffuseTextureColor(RaytracerGeometry hitGeometry, RTCHit hit)
    {
        Span<float> uv = stackalloc float[2];
        Embree.RtcInterpolate0(hitGeometry.EmbreeGeometry, hit.PrimID, hit.U, hit.V,
            RTCBufferType.RtcBufferTypeVertexAttribute, RaytracerScene.UvAttribute, uv, 2);
        return hitGeometry.Material.SampleDiffuse(new Vector2f(uv[0], uv[1]));
    }

    private static bool IsTransparentForDirect(RaytracerGeometry hitGeometry, RTCHit hit, ref Vector3f incomingLight)
    {
        if (hitGeometry.Material.Opaque)
            return false;

        var hitSurfaceColor = hitGeometry.Material.DiffuseColor;
        var hitSurfaceAlpha = hitGeometry.Material.Alpha;
        if (hitGeometry.Material.DiffuseImage is not null)
        {
            var diffuseColor = GetHitDiffuseTextureColor(hitGeometry, hit);
            hitSurfaceColor *= diffuseColor.ToVector3();
            hitSurfaceAlpha *= diffuseColor.A;
        }

        var transparency = MathDefs.Clamp(1.0f - hitSurfaceAlpha, 0.0f, 1.0f);
        var filterIntensity = 1.0f - transparency;
        incomingLight *= transparency * Vector3f.One.Lerp(hitSurfaceColor, filterIntensity);
        return true;
    }

    private static bool IsTransparentForIndirect(RaytracerGeometry hitGeometry, RTCHit hit)
    {
        if (hitGeometry.Material.Opaque)
            return false;

        var sample = RandomEngine.GetDefaultEngine().GetFloat();
        var alpha = hitGeometry.Material.Alpha;
        if (alpha < sample)
            return true;
        if (hitGeometry.Material.DiffuseImage is not null)
            return alpha * GetHitDiffuseTextureColor(hitGeometry, hit).A < sample;
        return false;
    }

    private static RTCRayHit CreateRayHit(uint mask)
    {
        return new RTCRayHit
        {
            RayMask = mask,
            RayTnear = 0.0f,
            RayTime = 0.0f,
            RayId = 0,
            RayFlags = 0,
            HitGeomID = uint.MaxValue
        };
    }

    private static bool GenerateDirectRay(BakedLight light, float maxRayDistance, Vector3f position,
        out Vector3f rayOffset, out Vector3f lightIntensity, out Vector3f incomingDirection)
    {
        if (light.LightType == LightType.Directional)
        {
            var randomOffset = RandomCircleOffset() * maxRayDistance * light.HalfAngleTan;
            rayOffset = maxRayDistance * light.Direction + randomOffset.ToVector3();
            lightIntensity = light.Color.ToVector3();
            incomingDirection = -light.Direction;
            return true;
        }

        var radiusOffset = RandomCircleOffset() * light.Radius;
        rayOffset = position - light.Position;
        rayOffset += light.LightType == LightType.Spot
            ? light.Rotation * radiusOffset.ToVector3()
            : new Quaternion(Vector3f.Forward, rayOffset) * radiusOffset.ToVector3();

        var distance = rayOffset.Length;
        var distanceAttenuation = MathF.Max(0.0f,
            1.0f - (distance - light.Radius) / (light.Distance - light.Radius));
        incomingDirection = (light.Position - position).Normalized;

        if (light.LightType == LightType.Point)
        {
            lightIntensity = light.Color.ToVector3() * distanceAttenuation * distanceAttenuation;
            return distanceAttenuation > MathDefs.LargeEpsilon;
        }

        var rayDirection = rayOffset / distance;
        var dot = light.Direction.DotProduct(rayDirection);
        var spotAttenuation = MathDefs.Clamp((dot - light.Cutoff) / (1.0f - light.Cutoff), 0.0f, 1.0f);
        lightIntensity = light.Color.ToVector3() * distanceAttenuation * distanceAttenuation * spotAttenuation;
        return distanceAttenuation > MathDefs.LargeEpsilon && spotAttenuation > MathDefs.LargeEpsilon;
    }

    /// <summary>Preprocess geometry buffer and fix shadow bleeding.</summary>
    public static void PreprocessGeometryBuffer(LightmapChartGeometryBuffer geometryBuffer,
        RaytracerScene raytracerScene, IReadOnlyList<int> geometryBufferToRaytracer,
        GeometryBufferPreprocessSettings settings)
    {
        Helpers.ParallelFor(geometryBuffer.Positions.Count, checked((int)settings.NumTasks), (fromIndex, toIndex) =>
        {
            using var arguments = RaytracerScene.CreateIntersectArguments();
            using var rayHit = CreateRayHit(RaytracerScene.AllGeometry);
            RaytracerGeometry? currentGeometry = null;
            bool Filter(RTCHit hit) => !IsUnwantedLod(currentGeometry!, raytracerScene.Geometries[(int)hit.GeomID]);

            for (var elementIndex = fromIndex; elementIndex < toIndex; ++elementIndex)
            {
                var geometryId = geometryBuffer.GeometryIds[elementIndex];
                if (geometryId == 0)
                    continue;

                currentGeometry = raytracerScene.Geometries[geometryBufferToRaytracer[geometryId]];
                var mutablePosition = geometryBuffer.Positions[elementIndex];
                var bias = CalculateBiasScale(mutablePosition) * settings.ScaledPositionBackfaceBias
                    + settings.ConstPositionBackfaceBias;
                var faceNormal = geometryBuffer.FaceNormals[elementIndex];
                var texelRadius = geometryBuffer.TexelRadiuses[elementIndex];
                var basis = new Quaternion(Vector3f.Forward, faceNormal);
                ReadOnlySpan<Vector3f> sampleRays = [Vector3f.Left, Vector3f.Right, Vector3f.Up, Vector3f.Down];
                var closestHitDistance = MathDefs.LargeValue;
                var closestHitDirection = Vector3f.Zero;
                var closestRayOrigin = Vector3f.Zero;

                foreach (var sampleRay in sampleRays)
                {
                    var rayDirection = basis * sampleRay;
                    var rayOrigin = mutablePosition - rayDirection * bias;
                    rayHit.RayOrgX = rayOrigin.X;
                    rayHit.RayOrgY = rayOrigin.Y;
                    rayHit.RayOrgZ = rayOrigin.Z;
                    rayHit.RayDirX = rayDirection.X * (1.0f + bias);
                    rayHit.RayDirY = rayDirection.Y * (1.0f + bias);
                    rayHit.RayDirZ = rayDirection.Z * (1.0f + bias);
                    rayHit.RayTfar = texelRadius;
                    rayHit.HitGeomID = uint.MaxValue;
                    raytracerScene.Intersect(rayHit, arguments, Filter);
                    if (rayHit.HitGeomID == uint.MaxValue)
                        continue;

                    var dot = rayHit.HitNgX * rayHit.RayDirX
                        + rayHit.HitNgY * rayHit.RayDirY + rayHit.HitNgZ * rayHit.RayDirZ;
                    if (dot < 0.0f)
                        continue;

                    if (rayHit.RayTfar < closestHitDistance)
                    {
                        closestHitDistance = rayHit.RayTfar;
                        closestHitDirection = rayDirection;
                        closestRayOrigin = rayOrigin;
                    }
                }

                if (closestHitDistance != MathDefs.LargeValue)
                {
                    var offset = closestHitDistance + settings.ConstPositionBackfaceBias
                        + settings.ScaledPositionBackfaceBias * CalculateBiasScale(mutablePosition);
                    geometryBuffer.Positions[elementIndex] = closestRayOrigin + closestHitDirection * offset;
                }
            }
        });
    }

    public static void BakeEmissionLight(LightmapChartBakedDirect bakedDirect,
        LightmapChartGeometryBuffer geometryBuffer, EmissionLightTracingSettings settings,
        float indirectBrightnessMultiplier)
    {
        Helpers.ParallelFor(bakedDirect.DirectLight.Length, checked((int)settings.NumTasks), (fromIndex, toIndex) =>
        {
            for (var i = fromIndex; i < toIndex; ++i)
            {
                if (geometryBuffer.GeometryIds[i] == 0)
                    continue;
                var albedo = geometryBuffer.Albedo[i];
                var emission = geometryBuffer.Emission[i];
                bakedDirect.DirectLight[i] += emission;
                bakedDirect.SurfaceLight[i] += indirectBrightnessMultiplier * emission;
                bakedDirect.Albedo[i] = albedo;
            }
        });
    }

    public static void BakeDirectLightForCharts(LightmapChartBakedDirect bakedDirect,
        LightmapChartGeometryBuffer geometryBuffer, RaytracerScene raytracerScene,
        IReadOnlyList<int> geometryBufferToRaytracer, BakedLight light, DirectLightTracingSettings settings)
    {
        var numSamples = CalculateNumSamples(light, settings.MaxSamples);
        var bakeDirect = light.LightMode == LightMode.Baked;
        Helpers.ParallelFor(bakedDirect.DirectLight.Length, checked((int)settings.NumTasks), (fromIndex, toIndex) =>
        {
            using var arguments = RaytracerScene.CreateIntersectArguments();
            using var rayHit = CreateRayHit(RaytracerScene.AllGeometry);
            RaytracerGeometry? currentGeometry = null;
            var incomingLight = Vector3f.Zero;
            bool Filter(RTCHit hit)
            {
                var hitGeometry = raytracerScene.Geometries[(int)hit.GeomID];
                if (IsUnwantedLod(currentGeometry!, hitGeometry))
                    return false;
                return !IsTransparentForDirect(hitGeometry, hit, ref incomingLight);
            }

            for (var elementIndex = fromIndex; elementIndex < toIndex; ++elementIndex)
            {
                var geometryId = geometryBuffer.GeometryIds[elementIndex];
                if (geometryId == 0 || (geometryBuffer.LightMasks[elementIndex] & light.LightMask) == 0)
                    continue;
                currentGeometry = raytracerScene.Geometries[geometryBufferToRaytracer[geometryId]];
                var position = geometryBuffer.Positions[elementIndex];
                var accumulatedLight = Vector3f.Zero;
                for (var sampleIndex = 0; sampleIndex < numSamples; ++sampleIndex)
                {
                    if (!GenerateDirectRay(light, raytracerScene.MaxDistance, position,
                        out var rayOffset, out incomingLight, out var incomingDirection))
                        continue;

                    rayHit.RayDirX = rayOffset.X;
                    rayHit.RayDirY = rayOffset.Y;
                    rayHit.RayDirZ = rayOffset.Z;
                    rayHit.RayOrgX = position.X - rayOffset.X;
                    rayHit.RayOrgY = position.Y - rayOffset.Y;
                    rayHit.RayOrgZ = position.Z - rayOffset.Z;
                    rayHit.RayTfar = 1.0f;
                    rayHit.HitGeomID = uint.MaxValue;
                    raytracerScene.Intersect(rayHit, arguments, Filter);
                    if (rayHit.HitGeomID == uint.MaxValue)
                    {
                        var intensity = MathF.Max(0.0f,
                            geometryBuffer.SmoothNormals[elementIndex].DotProduct(incomingDirection));
                        accumulatedLight += incomingLight * intensity;
                    }
                }

                var directLight = accumulatedLight / numSamples;
                if (bakeDirect)
                    bakedDirect.DirectLight[elementIndex] += directLight;
                bakedDirect.SurfaceLight[elementIndex] += light.IndirectBrightness
                    * geometryBuffer.Albedo[elementIndex] * directLight;
            }
        });
    }

    public static void BakeDirectLightForLightProbes(LightProbeCollectionBakedData bakedData,
        LightProbeCollectionForBaking collection, RaytracerScene raytracerScene,
        BakedLight light, DirectLightTracingSettings settings)
    {
        var numSamples = CalculateNumSamples(light, settings.MaxSamples);
        var bakeDirect = light.LightMode == LightMode.Baked;
        Helpers.ParallelFor(checked((int)bakedData.Size), checked((int)settings.NumTasks), (fromIndex, toIndex) =>
        {
            using var arguments = RaytracerScene.CreateIntersectArguments();
            using var rayHit = CreateRayHit(RaytracerScene.PrimaryLodGeometry);
            var incomingLight = Vector3f.Zero;
            bool Filter(RTCHit hit)
            {
                var hitGeometry = raytracerScene.Geometries[(int)hit.GeomID];
                if (hitGeometry.LodIndex != 0)
                    return false;
                return !IsTransparentForDirect(hitGeometry, hit, ref incomingLight);
            }

            for (var elementIndex = fromIndex; elementIndex < toIndex; ++elementIndex)
            {
                if ((collection.LightMasks[elementIndex] & light.LightMask) == 0)
                    continue;
                var position = collection.WorldPositions[elementIndex];
                var accumulated = SphericalHarmonicsColor9.Zero;
                for (var sampleIndex = 0; sampleIndex < numSamples; ++sampleIndex)
                {
                    if (!GenerateDirectRay(light, raytracerScene.MaxDistance, position,
                        out var rayOffset, out incomingLight, out var incomingDirection))
                        continue;
                    rayHit.RayDirX = rayOffset.X;
                    rayHit.RayDirY = rayOffset.Y;
                    rayHit.RayDirZ = rayOffset.Z;
                    rayHit.RayOrgX = position.X - rayOffset.X;
                    rayHit.RayOrgY = position.Y - rayOffset.Y;
                    rayHit.RayOrgZ = position.Z - rayOffset.Z;
                    rayHit.RayTfar = 1.0f;
                    rayHit.HitGeomID = uint.MaxValue;
                    raytracerScene.Intersect(rayHit, arguments, Filter);
                    if (rayHit.HitGeomID == uint.MaxValue)
                        accumulated += new SphericalHarmonicsColor9(incomingDirection, incomingLight);
                }

                if (bakeDirect)
                    bakedData.SphericalHarmonics[elementIndex] +=
                        new SphericalHarmonicsDot9(accumulated * (MathF.PI / numSamples));
            }
        });
    }

    public static void BakeIndirectLightForCharts(LightmapChartBakedIndirect bakedIndirect,
        IReadOnlyList<LightmapChartBakedDirect?> bakedDirect, LightmapChartGeometryBuffer geometryBuffer,
        TetrahedralMesh lightProbesMesh, LightProbeCollectionBakedData lightProbesData,
        RaytracerScene raytracerScene, IReadOnlyList<int> geometryBufferToRaytracer,
        IndirectLightTracingSettings settings)
    {
        if (settings.MaxBounces == 0)
            return;

        Helpers.ParallelFor(bakedIndirect.Light.Length, checked((int)settings.NumTasks), (fromIndex, toIndex) =>
        {
            uint lightProbesMeshHint = 0;
            using var arguments = RaytracerScene.CreateIntersectArguments();
            using var rayHit = CreateRayHit(RaytracerScene.PrimaryLodGeometry);
            bool Filter(RTCHit hit) => !IsTransparentForIndirect(raytracerScene.Geometries[(int)hit.GeomID], hit);
            Span<float> interpolated = stackalloc float[3];
            Span<float> uv = stackalloc float[2];

            for (var elementIndex = fromIndex; elementIndex < toIndex; ++elementIndex)
            {
                var geometryId = geometryBuffer.GeometryIds[elementIndex];
                if (geometryId == 0)
                    continue;
                var currentPosition = geometryBuffer.Positions[elementIndex];
                var currentFaceNormal = geometryBuffer.FaceNormals[elementIndex];
                var currentSmoothNormal = geometryBuffer.SmoothNormals[elementIndex];
                var raytracerGeometry = raytracerScene.Geometries[geometryBufferToRaytracer[geometryId]];
                if (raytracerGeometry.NumLods > 1)
                {
                    var sh = lightProbesMesh.Sample(lightProbesData.SphericalHarmonics,
                        currentPosition, ref lightProbesMeshHint);
                    var value = Vector3f.Max(Vector3f.Zero, sh.Evaluate(currentSmoothNormal));
                    bakedIndirect.Light[elementIndex] += new Vector4f(value, 1.0f);
                    continue;
                }

                var background = raytracerScene.Backgrounds[geometryBuffer.BackgroundIds[elementIndex]];
                var accumulated = Vector4f.Zero;
                for (var sampleIndex = 0; sampleIndex < settings.MaxSamples; ++sampleIndex)
                {
                    var position = currentPosition;
                    var faceNormal = currentFaceNormal;
                    var smoothNormal = currentSmoothNormal;
                    var rayDirection = RandomHemisphereDirectionCos(faceNormal);
                    var sampleColor = Vector3f.Zero;
                    var incomingFactor = Vector3f.One;
                    for (var bounceIndex = 0; bounceIndex < settings.MaxBounces; ++bounceIndex)
                    {
                        rayHit.RayOrgX = position.X;
                        rayHit.RayOrgY = position.Y;
                        rayHit.RayOrgZ = position.Z;
                        rayHit.RayDirX = rayDirection.X;
                        rayHit.RayDirY = rayDirection.Y;
                        rayHit.RayDirZ = rayDirection.Z;
                        rayHit.RayTfar = raytracerScene.MaxDistance;
                        rayHit.HitGeomID = uint.MaxValue;
                        raytracerScene.Intersect(rayHit, arguments, Filter);
                        if (rayHit.HitGeomID == uint.MaxValue)
                        {
                            sampleColor += incomingFactor * background.SampleLinear(rayDirection);
                            break;
                        }

                        var hitNormal = new Vector3f(rayHit.HitNgX, rayHit.HitNgY, rayHit.HitNgZ);
                        if (rayDirection.DotProduct(hitNormal) > 0.0f)
                            break;
                        var hitGeometry = raytracerScene.Geometries[(int)rayHit.HitGeomID];
                        Embree.RtcInterpolate0(hitGeometry.EmbreeGeometry,
                            rayHit.HitPrimID, rayHit.HitU, rayHit.HitV,
                            RTCBufferType.RtcBufferTypeVertexAttribute, RaytracerScene.LightmapUvAttribute, uv, 2);
                        var direct = bakedDirect[hitGeometry.LightmapIndex]!;
                        var sampleLocation = direct.GetNearestLocation(new Vector2f(uv[0], uv[1]));
                        sampleColor += incomingFactor * direct.GetSurfaceLight(sampleLocation);
                        if (bounceIndex + 1 >= settings.MaxBounces)
                            break;

                        incomingFactor *= direct.GetAlbedo(sampleLocation);
                        position = new Vector3f(
                            rayHit.RayOrgX + rayHit.RayDirX * rayHit.RayTfar,
                            rayHit.RayOrgY + rayHit.RayDirY * rayHit.RayTfar,
                            rayHit.RayOrgZ + rayHit.RayDirZ * rayHit.RayTfar);
                        hitNormal = hitNormal.Normalized;
                        var bias = settings.ScaledPositionBounceBias * CalculateBiasScale(position);
                        position.X += MathF.Sign(hitNormal.X) * bias + hitNormal.X * settings.ConstPositionBounceBias;
                        position.Y += MathF.Sign(hitNormal.Y) * bias + hitNormal.Y * settings.ConstPositionBounceBias;
                        position.Z += MathF.Sign(hitNormal.Z) * bias + hitNormal.Z * settings.ConstPositionBounceBias;
                        Embree.RtcInterpolate0(hitGeometry.EmbreeGeometry,
                            rayHit.HitPrimID, rayHit.HitU, rayHit.HitV,
                            RTCBufferType.RtcBufferTypeVertexAttribute, RaytracerScene.NormalAttribute, interpolated, 3);
                        smoothNormal = new Vector3f(interpolated[0], interpolated[1], interpolated[2]).Normalized;
                        faceNormal = hitNormal;
                        rayDirection = RandomHemisphereDirectionCos(faceNormal);
                    }
                    accumulated += new Vector4f(sampleColor, 1.0f);
                }
                bakedIndirect.Light[elementIndex] += accumulated;
            }
        });
    }

    public static void BakeIndirectLightForLightProbes(LightProbeCollectionBakedData bakedData,
        LightProbeCollectionForBaking collection, IReadOnlyList<LightmapChartBakedDirect?> bakedDirect,
        RaytracerScene raytracerScene, IndirectLightTracingSettings settings)
    {
        if (settings.MaxBounces == 0)
            return;

        Helpers.ParallelFor(checked((int)bakedData.Size), checked((int)settings.NumTasks), (fromIndex, toIndex) =>
        {
            using var arguments = RaytracerScene.CreateIntersectArguments();
            using var rayHit = CreateRayHit(RaytracerScene.PrimaryLodGeometry);
            bool Filter(RTCHit hit) => !IsTransparentForIndirect(raytracerScene.Geometries[(int)hit.GeomID], hit);
            Span<float> interpolated = stackalloc float[3];
            Span<float> uv = stackalloc float[2];

            for (var elementIndex = fromIndex; elementIndex < toIndex; ++elementIndex)
            {
                var background = raytracerScene.Backgrounds[checked((int)collection.BackgroundIds[elementIndex])];
                var accumulated = SphericalHarmonicsColor9.Zero;
                for (var sampleIndex = 0; sampleIndex < settings.MaxSamples; ++sampleIndex)
                {
                    var sampleDirection = RandomEngine.GetDefaultEngine().GetDirectionVector3();
                    var position = collection.WorldPositions[elementIndex];
                    var rayDirection = sampleDirection;
                    var incomingFactor = Vector3f.One;
                    var sampleColor = Vector3f.Zero;
                    for (var bounceIndex = 0; bounceIndex < settings.MaxBounces; ++bounceIndex)
                    {
                        rayHit.RayOrgX = position.X;
                        rayHit.RayOrgY = position.Y;
                        rayHit.RayOrgZ = position.Z;
                        rayHit.RayDirX = rayDirection.X;
                        rayHit.RayDirY = rayDirection.Y;
                        rayHit.RayDirZ = rayDirection.Z;
                        rayHit.RayTfar = raytracerScene.MaxDistance;
                        rayHit.HitGeomID = uint.MaxValue;
                        raytracerScene.Intersect(rayHit, arguments, Filter);
                        if (rayHit.HitGeomID == uint.MaxValue)
                        {
                            sampleColor += incomingFactor * background.SampleLinear(rayDirection);
                            break;
                        }

                        var hitNormal = new Vector3f(rayHit.HitNgX, rayHit.HitNgY, rayHit.HitNgZ);
                        if (rayDirection.DotProduct(hitNormal) > 0.0f)
                            break;
                        var hitGeometry = raytracerScene.Geometries[(int)rayHit.HitGeomID];
                        Embree.RtcInterpolate0(hitGeometry.EmbreeGeometry,
                            rayHit.HitPrimID, rayHit.HitU, rayHit.HitV,
                            RTCBufferType.RtcBufferTypeVertexAttribute, RaytracerScene.LightmapUvAttribute, uv, 2);
                        var direct = bakedDirect[hitGeometry.LightmapIndex]!;
                        var sampleLocation = direct.GetNearestLocation(new Vector2f(uv[0], uv[1]));
                        sampleColor += incomingFactor * direct.GetSurfaceLight(sampleLocation);
                        if (bounceIndex + 1 >= settings.MaxBounces)
                            break;

                        incomingFactor *= direct.GetAlbedo(sampleLocation);
                        position = new Vector3f(
                            rayHit.RayOrgX + rayHit.RayDirX * rayHit.RayTfar,
                            rayHit.RayOrgY + rayHit.RayDirY * rayHit.RayTfar,
                            rayHit.RayOrgZ + rayHit.RayDirZ * rayHit.RayTfar);
                        hitNormal = hitNormal.Normalized;
                        var bias = settings.ScaledPositionBounceBias * CalculateBiasScale(position);
                        position.X += MathF.Sign(hitNormal.X) * bias + hitNormal.X * settings.ConstPositionBounceBias;
                        position.Y += MathF.Sign(hitNormal.Y) * bias + hitNormal.Y * settings.ConstPositionBounceBias;
                        position.Z += MathF.Sign(hitNormal.Z) * bias + hitNormal.Z * settings.ConstPositionBounceBias;
                        Embree.RtcInterpolate0(hitGeometry.EmbreeGeometry,
                            rayHit.HitPrimID, rayHit.HitU, rayHit.HitV,
                            RTCBufferType.RtcBufferTypeVertexAttribute, RaytracerScene.NormalAttribute, interpolated, 3);
                        rayDirection = RandomHemisphereDirectionCos(hitNormal);
                    }
                    accumulated += new SphericalHarmonicsColor9(sampleDirection, sampleColor);
                }

                bakedData.SphericalHarmonics[elementIndex] += new SphericalHarmonicsDot9(
                    accumulated * (4.0f * MathF.PI / settings.MaxSamples));
            }
        });
    }
}
