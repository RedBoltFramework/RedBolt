using System.Runtime.CompilerServices;

namespace RedBolt;

/// <summary>
/// Light parameters needed to calculate SH lighting.
/// </summary>
public struct LightDataForAccumulator
{
    public Color Color;
    public Vector3f Position;
    public Vector3f Direction;
    public LightType LightType;
    public float Range;
    public float SpotCutoff;
    public float InverseSpotCutoff;

    /// <summary>
    /// Construct from light.
    /// </summary>
    public LightDataForAccumulator(Light light)
    {
        Color = light.GetEffectiveColor().GammaToLinear();
        Position = light.Node!.WorldPosition;
        Direction = -light.Node.WorldDirection; // negate because we need direction _to_ light
        LightType = light.GetLightType();
        Range = light.GetRange();
        var cutoffParams = light.GetCutoffParams();
        SpotCutoff = cutoffParams.Cutoff;
        InverseSpotCutoff = cutoffParams.InverseCutoff;
    }

    /// <summary>
    /// Return normalized direction and inverted normalized distance to light for given point in world.
    /// </summary>
    public readonly (Vector3f Direction, float Attenuation) GetDirectionToLight(Vector3f worldPos)
    {
        if (LightType == LightType.Directional)
            return (Direction, 1.0f);
        else
        {
            var lightVector = Position - worldPos;
            var distance = lightVector.Length;
            if (distance > MathDefs.Epsilon)
                return (lightVector / distance, MathF.Max(0.0f, 1.0f - distance / Range));
            else
                return (Vector3f.Right, 1.0f);
        }
    }

    /// <summary>
    /// Return direction-based spot attenuation.
    /// </summary>
    public readonly float GetSpotAttenuation(Vector3f worldDir)
    {
        var spotAngle = Direction.DotProduct(worldDir);
        return Math.Clamp((spotAngle - SpotCutoff) * InverseSpotCutoff, 0.0f, 1.0f);
    }

    /// <summary>
    /// Return lighting at the point as SH.
    /// </summary>
    public readonly SphericalHarmonicsDot9 GetLightingAtPoint(Vector3f worldPos)
    {
        var pair = GetDirectionToLight(worldPos);
        var dirToLight = pair.Direction;
        var distanceAttenuation = pair.Attenuation * pair.Attenuation;
        var spotAttenuation = GetSpotAttenuation(dirToLight);
        var sh = new SphericalHarmonicsColor9(dirToLight, Color.ToVector3());
        return new SphericalHarmonicsDot9(sh) * (MathF.PI * distanceAttenuation * spotAttenuation);
    }
}

/// <summary>
/// Common parameters for light accumulation.
/// </summary>
public struct LightAccumulatorContext
{
    public uint MaxVertexLights = 4;
    public uint MaxPixelLights = 1;
    /// <summary>
    /// Array of lights to be indexed.
    /// </summary>
    public IReadOnlyList<LightDataForAccumulator>? Lights;

    public LightAccumulatorContext()
    {
    }

    public LightAccumulatorContext(IReadOnlyList<LightDataForAccumulator> lights,
        uint maxVertexLights = 4, uint maxPixelLights = 1)
    {
        MaxVertexLights = maxVertexLights;
        MaxPixelLights = maxPixelLights;
        Lights = lights;
    }
}

/// <summary>
/// Accumulated light for forward rendering.
/// </summary>
public sealed class LightAccumulator
{
    /// <summary>
    /// Hints used for small buffer optimization
    /// </summary>
    public const uint MaxPixelLights = 4;
    public const uint MaxVertexLights = 4;
    public const uint NumElements = 9;

    public readonly record struct LightData(float Penalty, uint LightIndex);

    [InlineArray((int)MaxVertexLights)]
    public struct VertexLightContainer
    {
        private uint _element0;
    }

    [InlineArray((int)NumElements)]
    private struct LightContainer
    {
        private LightData _element0;
    }

    [InlineArray(2)]
    public struct ReflectionProbeContainer
    {
        private StrongBox<ReflectionProbeData>? _element0;
    }

    /// <summary>
    /// Accumulated SH lights and ambient light.
    /// </summary>
    public SphericalHarmonicsDot9 SphericalHarmonics;
    /// <summary>
    /// Reflection probe.
    /// </summary>
    public ReflectionProbeContainer ReflectionProbes;
    public float ReflectionProbesBlendFactor;

    private LightContainer _lights;
    private int _numLights;

    private uint _numImportantLights;
    private uint _numAutoLights;
    private uint _firstVertexLight;

    /// <summary>
    /// Hash of vertex lights. Non-zero.
    /// </summary>
    private uint _vertexLightsHash;

    /// <summary>
    /// Reset lights.
    /// </summary>
    public void ResetLights()
    {
        _numLights = 0;
        _firstVertexLight = 0;
        _numImportantLights = 0;
        _numAutoLights = 0;
        _vertexLightsHash = 0;
    }

    /// <summary>
    /// Accumulate light.
    /// </summary>
    public void AccumulateLight(in LightAccumulatorContext ctx,
        Drawable geometry, LightImportance lightImportance, uint lightIndex, float penalty)
    {
        System.Diagnostics.Debug.Assert(_vertexLightsHash == 0);
        if (lightImportance == LightImportance.Important)
            ++_numImportantLights;
        else if (lightImportance == LightImportance.Auto)
            ++_numAutoLights;

        var index = 0;
        while (index < _numLights && penalty > _lights[index].Penalty)
            ++index;
        for (var i = _numLights; i > index; --i)
            _lights[i] = _lights[i - 1];
        _lights[index] = new LightData(penalty, lightIndex);
        ++_numLights;

        _firstVertexLight = Math.Max(_numImportantLights,
            Math.Min(_numImportantLights + _numAutoLights, ctx.MaxPixelLights));

        var maxLights = ctx.MaxVertexLights + _firstVertexLight;
        if (_numLights > maxLights)
        {
            var lightData = ctx.Lights![(int)_lights[_numLights - 1].LightIndex];
            var samplePosition = geometry.GetWorldBoundingBox().Center;
            SphericalHarmonics += lightData.GetLightingAtPoint(samplePosition);
            --_numLights;
        }
    }

    /// <summary>
    /// Cook light if necessary.
    /// </summary>
    public void Cook()
    {
        if (_vertexLightsHash != 0)
            return;

        var numLights = (uint)_numLights;
        if (_firstVertexLight < numLights)
        {
            for (var i = (int)_firstVertexLight + 1; i < _numLights; ++i)
            {
                var light = _lights[i];
                var j = i;
                while (j > _firstVertexLight && _lights[j - 1].LightIndex > light.LightIndex)
                {
                    _lights[j] = _lights[j - 1];
                    --j;
                }
                _lights[j] = light;
            }
        }

        for (var i = _firstVertexLight; i < numLights; ++i)
            CombineHash(ref _vertexLightsHash, (_lights[(int)i].LightIndex + 1) * 2654435761);
        _vertexLightsHash += _vertexLightsHash == 0 ? 1u : 0u;
    }

    /// <summary>
    /// Return light info, valid after cooking
    /// </summary>
    public VertexLightContainer GetVertexLights()
    {
        VertexLightContainer vertexLights = default;
        for (uint i = 0; i < 4; ++i)
        {
            var index = i + _firstVertexLight;
            vertexLights[(int)i] = index < _numLights ? _lights[(int)index].LightIndex : uint.MaxValue;
        }
        return vertexLights;
    }

    public ReadOnlySpan<LightData> GetPixelLights() =>
        _lights[..Math.Min(_numLights, (int)_firstVertexLight)];

    public uint GetVertexLightsHash() => _vertexLightsHash;

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}
