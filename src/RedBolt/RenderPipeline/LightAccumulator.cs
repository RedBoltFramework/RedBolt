using System.Diagnostics.CodeAnalysis;
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

    public LightDataForAccumulator()
    {
        Color = new Color();
    }

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
    public int MaxVertexLights = 4;
    public int MaxPixelLights = 1;
    /// <summary>
    /// Array of lights to be indexed.
    /// </summary>
    public IReadOnlyList<LightDataForAccumulator>? Lights;

    public LightAccumulatorContext()
    {
    }
}

/// <summary>
/// Accumulated light for forward rendering.
/// </summary>
public struct LightAccumulator
{
    /// <summary>
    /// Hints used for small buffer optimization
    /// </summary>
    public const int MaxPixelLights = 4;
    public const int MaxVertexLights = 4;
    public const int NumElements = 9;

    public struct LightData
    {
        public LightData(float penalty, int lightIndex)
        {
            Penalty = penalty;
            LightIndex = lightIndex;
        }

        public float Penalty;
        public int LightIndex;
    }

    [InlineArray(MaxVertexLights)]
    public struct VertexLightContainer
    {
        private int _element0;
    }

    [InlineArray(NumElements)]
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
        Drawable geometry, LightImportance lightImportance, int lightIndex, float penalty)
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
            var lightData = ctx.Lights![_lights[_numLights - 1].LightIndex];
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

        var numLights = _numLights;
        if (_firstVertexLight < numLights)
            _lights[_firstVertexLight..numLights].Sort(
                static (lhs, rhs) => lhs.LightIndex.CompareTo(rhs.LightIndex));

        for (var i = _firstVertexLight; i < numLights; ++i)
            CombineHash(ref _vertexLightsHash,
                unchecked(((uint)_lights[i].LightIndex + 1u) * 2654435761u));
        _vertexLightsHash += _vertexLightsHash == 0 ? 1u : 0u;
    }

    /// <summary>
    /// Return light info, valid after cooking
    /// </summary>
    public readonly VertexLightContainer VertexLights
    {
        get
        {
            VertexLightContainer vertexLights = default;
            for (var i = 0; i < 4; ++i)
            {
                var index = i + _firstVertexLight;
                vertexLights[i] = index < _numLights ? _lights[index].LightIndex : int.MaxValue;
            }
            return vertexLights;
        }
    }

    [UnscopedRef]
    public readonly ReadOnlySpan<LightData> PixelLights =>
        _lights[..Math.Min(_numLights, _firstVertexLight)];

    public readonly uint VertexLightsHash => _vertexLightsHash;

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

    private int _numImportantLights;
    private int _numAutoLights;
    private int _firstVertexLight;

    /// <summary>
    /// Hash of vertex lights. Non-zero.
    /// </summary>
    private uint _vertexLightsHash;

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}
