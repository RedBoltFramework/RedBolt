namespace RedBolt.Math;

using System.Diagnostics;
using System.Globalization;
using SystemMath = global::System.Math;

using RedBolt.Logging;

/// <summary>Random number generator. Stable across platforms and runs for any given seed.</summary>
public class RandomEngine : ICloneable
{
    /// <summary>Max number of tries to produce "good" random values.</summary>
    public const uint MaxIterations = 32;

    /// <summary>Construct with random seed.</summary>
    public RandomEngine()
        : this((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds())
    {
    }

    /// <summary>Construct with seed.</summary>
    public RandomEngine(uint seed)
    {
        _state = seed % Modulus;
        if (_state == 0)
            _state = 1;
    }

    /// <summary>Construct from state.</summary>
    public RandomEngine(string state)
    {
        _state = EngineMin;
        Load(state);
    }

    public RandomEngine(RandomEngine engine)
    {
        _state = engine._state;
    }

    public object Clone() => new RandomEngine(this);

    /// <summary>Return default thread-local random engine.</summary>
    public static RandomEngine GetDefaultEngine() => _defaultEngine ??= new RandomEngine();

    /// <summary>Load state from string.</summary>
    public void Load(string state)
    {
        if (uint.TryParse(state, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed is >= EngineMin and <= EngineMax)
            _state = parsed;
    }

    /// <summary>Save state as string.</summary>
    public string Save() => _state.ToString(CultureInfo.InvariantCulture);

    /// <summary>Return random generator range (2147483646).</summary>
    public static uint MaxRange() => EngineMax - EngineMin + 1;

    /// <summary>Return random unsigned integer number in range [0, MaxRange) with uniform distribution.</summary>
    public uint GetUInt()
    {
        _state = (uint)((ulong)_state * Multiplier % Modulus);
        return _state - EngineMin;
    }

    /// <summary>Return random unsigned integer number in range [0, range) with uniform distribution. Range should not exceed MaxRange.</summary>
    public uint GetUInt(uint range)
    {
        Debug.Assert(range <= MaxRange());
        if (range == 0)
            return 0;

        var repeats = MaxRange() / range;
        var limit = repeats * range;
        var result = 0u;
        for (var i = 0u; i < MaxIterations; ++i)
        {
            result = GetUInt();
            if (result < limit)
                break;
        }

        return result % range;
    }

    /// <summary>Return random unsigned int in range [min, max) with uniform distribution. Range should not exceed MaxRange.</summary>
    public uint GetUInt(uint min, uint max)
    {
        Debug.Assert(min <= max);
        return GetUInt(max - min) + min;
    }

    /// <summary>Return random int in range [min, max) with uniform distribution. Range should not exceed MaxRange.</summary>
    public int GetInt(int min, int max)
    {
        // Avoid signed integer overflow
        Debug.Assert(min <= max);
        var range = unchecked((uint)max - (uint)min);
        return unchecked((int)(GetUInt(range) + (uint)min));
    }

    /// <summary>Shuffle range in random order.</summary>
    public void Shuffle<T>(Span<T> values)
    {
        var n = values.Length;
        for (var i = n - 1; i > 0; --i)
        {
            var j = (int)GetUInt((uint)(i + 1));
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    /// <summary>Return random double in range [0, 1] with uniform distribution.</summary>
    public double GetDouble() => GetUInt() / (double)(MaxRange() - 1);

    /// <summary>Return random double in range [min, max] with uniform distribution.</summary>
    public double GetDouble(double min, double max) => GetDouble() * (max - min) + min;

    /// <summary>Return random boolean with given probability of returning true.</summary>
    public bool GetBool(float probability) => GetDouble() <= probability && probability != 0.0f;

    /// <summary>Return random float in range [0, 1] with uniform distribution.</summary>
    public float GetFloat() => (float)GetDouble();

    /// <summary>Return random float in range [min, max] with uniform distribution.</summary>
    public float GetFloat(float min, float max) => (float)GetDouble(min, max);

    /// <summary>Return pair of random floats with standard normal distribution.</summary>
    public (float First, float Second) GetStandardNormalFloatPair()
    {
        var u1 = GetDouble(MathDefs.Epsilon, 1.0);
        var u2 = GetDouble();

        var z0 = SystemMath.Sqrt(-2.0 * SystemMath.Log(u1)) * SystemMath.Cos(360.0f * u2 * MathDefs.DegToRad);
        var z1 = SystemMath.Sqrt(-2.0 * SystemMath.Log(u1)) * SystemMath.Sin(360.0f * u2 * MathDefs.DegToRad);
        return ((float)z0, (float)z1);
    }

    /// <summary>Return random float with standard normal distribution.</summary>
    public float GetStandardNormalFloat() => GetStandardNormalFloatPair().First;

    /// <summary>Return random float with normal distribution.</summary>
    public float GetNormalFloat(float mean, float sigma) => GetStandardNormalFloat() * sigma + mean;

    /// <summary>Return random 2D direction (normalized).</summary>
    public Vector2f GetDirectionVector2()
    {
        Span<float> direction = stackalloc float[2];
        GetDirection(direction);
        return new Vector2f(direction[0], direction[1]);
    }

    /// <summary>Return random 3D direction (normalized).</summary>
    public Vector3f GetDirectionVector3()
    {
        Span<float> direction = stackalloc float[3];
        GetDirection(direction);
        return new Vector3f(direction[0], direction[1], direction[2]);
    }

    /// <summary>Return random quaternion (normalized).</summary>
    public Quaternion GetQuaternion()
    {
        Span<float> direction = stackalloc float[4];
        GetDirection(direction);
        return new Quaternion(direction[0], direction[1], direction[2], direction[3]);
    }

    /// <summary>Return random 2D vector in 2D volume.</summary>
    public Vector2f GetVector2(Vector2f min, Vector2f max) => new(GetFloat(min.X, max.X), GetFloat(min.Y, max.Y));

    /// <summary>Return random 3D vector in 3D volume.</summary>
    public Vector3f GetVector3(Vector3f min, Vector3f max) => new(GetFloat(min.X, max.X), GetFloat(min.Y, max.Y), GetFloat(min.Z, max.Z));

    /// <summary>Return random 3D vector in 3D volume.</summary>
    public Vector3f GetVector3(BoundingBox boundingBox) => GetVector3(boundingBox.Min, boundingBox.Max);

    /// <summary>Return random array of floats with standard normal distribution.</summary>
    private void GetStandardNormalFloatArray(Span<float> array)
    {
        for (var i = 0; i < array.Length / 2; ++i)
        {
            var pair = GetStandardNormalFloatPair();
            array[i * 2] = pair.First;
            array[i * 2 + 1] = pair.Second;
        }

        if (array.Length % 2 != 0)
            array[^1] = GetStandardNormalFloat();
    }

    /// <summary>Return random N-dimensional direction (normalized).</summary>
    private void GetDirection(Span<float> direction) => GetDirection(direction, false);

    private void GetDirection(float[] direction, bool forceFallback) => GetDirection(direction.AsSpan(), forceFallback);

    private void GetDirection(Span<float> direction, bool forceFallback)
    {
        Debug.Assert(direction.Length > 0);

        for (var i = 0u; i < MaxIterations; ++i)
        {
            GetStandardNormalFloatArray(direction);

            // Compute length
            var length = 0.0f;
            foreach (var x in direction)
                length += x * x;

            if (forceFallback)
                length = 0.0f;

            // Normalize and return if long enough
            if (length > MathDefs.Epsilon)
            {
                var reciprocalLength = 1.0f / MathF.Sqrt(length);
                for (var j = 0; j < direction.Length; ++j)
                    direction[j] *= reciprocalLength;
                return;
            }
        }

        // Return default direction
        direction.Clear();
        direction[0] = 1.0f;
    }

    private const uint Multiplier = 48271;
    private const uint Modulus = 2147483647;
    private const uint EngineMin = 1;
    private const uint EngineMax = Modulus - 1;

    [ThreadStatic]
    private static RandomEngine? _defaultEngine;

    /// <summary>Underlying engine.</summary>
    private uint _state;
}

public static partial class MathDefs
{
    private static uint _randomSeed = 1;

    /// <summary>
    /// Set the random seed. The default seed is 1.
    /// </summary>
    public static void SetRandomSeed(uint seed)
    {
        _randomSeed = seed;
    }

    /// <summary>
    /// Return the current random seed.
    /// </summary>
    public static uint GetRandomSeed() => _randomSeed;

    /// <summary>
    /// Return a random number between 0-32767. Should operate similarly to MSVC rand().
    /// </summary>
    /// <remarks>Alias: RandomInt.</remarks>
    public static int Rand()
    {
        _randomSeed = unchecked(_randomSeed * 214013u + 2531011u);
        return (int)((_randomSeed >> 16) & 32767u);
    }

    /// <summary>
    /// Return a standard normal distributed number.
    /// </summary>
    public static float RandStandardNormal()
    {
        var value = 0.0f;
        for (var i = 0; i < 12; ++i)
            value += Rand() / 32768.0f;

        // Now val is approximatly standard normal distributed
        return value - 6.0f;
    }

    public static float Random() => Rand() / 32768.0f;

    public static float Random(float range) => Rand() * range / 32767.0f;

    public static float Random(float min, float max) => Rand() * (max - min) / 32767.0f + min;

    public static int Random(int range) => (int)(Random() * range);

    public static int Random(int min, int max) => (int)(Random() * (max - min)) + min;

    public static float RandomNormal(float mean, float variance) => RandStandardNormal() * MathF.Sqrt(variance) + mean;
}
