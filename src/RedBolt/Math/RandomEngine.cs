using System.Globalization;

namespace RedBolt;

public sealed class RandomEngine
{
    public const uint MaxIterations = 32;

    private const uint Multiplier = 48271;
    private const uint Modulus = 2147483647;
    private const uint EngineMin = 1;
    private const uint EngineMax = Modulus - 1;

    [ThreadStatic]
    private static RandomEngine? _defaultEngine;

    private uint _state;

    public RandomEngine()
        : this((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds())
    {
    }

    public RandomEngine(uint seed)
    {
        SetSeed(seed);
    }

    public RandomEngine(string state)
    {
        Load(state);
    }

    public static RandomEngine GetDefaultEngine() => _defaultEngine ??= new RandomEngine();

    public static uint MaxRange() => EngineMax - EngineMin + 1;

    public void Load(string state)
    {
        if (!uint.TryParse(state, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            parsed = 1;
        SetSeed(parsed);
    }

    public string Save() => _state.ToString(CultureInfo.InvariantCulture);

    public uint GetUInt() => NextRaw() - EngineMin;

    public uint GetUInt(uint range)
    {
        if (range == 0)
            return 0;
        if (range > MaxRange())
            throw new ArgumentOutOfRangeException(nameof(range), "Range must not exceed RandomEngine.MaxRange().");

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

    public uint GetUInt(uint min, uint max)
    {
        if (min > max)
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum must not exceed maximum.");
        return GetUInt(max - min) + min;
    }

    public int GetInt(int min, int max)
    {
        if (min > max)
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum must not exceed maximum.");

        var range = unchecked((uint)((long)max - min));
        return (int)((long)GetUInt(range) + min);
    }

    public void Shuffle<T>(IList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        for (var i = values.Count - 1; i > 0; --i)
        {
            var j = (int)GetUInt((uint)(i + 1));
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    public double GetDouble() => GetUInt() / (double)(MaxRange() - 1);

    public double GetDouble(double min, double max) => GetDouble() * (max - min) + min;

    public bool GetBool(float probability) => GetDouble() <= probability && probability != 0.0f;

    public float GetFloat() => (float)GetDouble();

    public float GetFloat(float min, float max) => (float)GetDouble(min, max);

    public (float First, float Second) GetStandardNormalFloatPair()
    {
        var u1 = GetDouble(MathDefs.Epsilon, 1.0);
        var u2 = GetDouble();
        var magnitude = Math.Sqrt(-2.0 * Math.Log(u1));
        return ((float)(magnitude * MathDefs.Cos((float)(360.0 * u2))), (float)(magnitude * MathDefs.Sin((float)(360.0 * u2))));
    }

    public float GetStandardNormalFloat() => GetStandardNormalFloatPair().First;

    public float GetNormalFloat(float mean, float sigma) => GetStandardNormalFloat() * sigma + mean;

    public Vector2 GetDirectionVector2()
    {
        var direction = GetDirection(2);
        return new Vector2(direction[0], direction[1]);
    }

    public Vector3 GetDirectionVector3()
    {
        var direction = GetDirection(3);
        return new Vector3(direction[0], direction[1], direction[2]);
    }

    public Quaternion GetQuaternion()
    {
        var direction = GetDirection(4);
        return new Quaternion(direction[0], direction[1], direction[2], direction[3]);
    }

    public Vector2 GetVector2(Vector2 min, Vector2 max) => new(GetFloat(min.X, max.X), GetFloat(min.Y, max.Y));

    public Vector3 GetVector3(Vector3 min, Vector3 max) => new(GetFloat(min.X, max.X), GetFloat(min.Y, max.Y), GetFloat(min.Z, max.Z));

    public Vector3 GetVector3(BoundingBox boundingBox) => GetVector3(boundingBox.Min, boundingBox.Max);

    private void SetSeed(uint seed)
    {
        _state = seed % Modulus;
        if (_state == 0)
            _state = 1;
    }

    private uint NextRaw()
    {
        _state = (uint)((ulong)_state * Multiplier % Modulus);
        return _state;
    }

    private float[] GetDirection(int dimensions)
    {
        var direction = new float[dimensions];
        for (var i = 0u; i < MaxIterations; ++i)
        {
            FillStandardNormal(direction);
            var lengthSquared = direction.Sum(static value => value * value);
            if (lengthSquared > MathDefs.Epsilon)
            {
                var inverseLength = 1.0f / MathF.Sqrt(lengthSquared);
                for (var j = 0; j < direction.Length; ++j)
                    direction[j] *= inverseLength;
                return direction;
            }
        }

        direction[0] = 1.0f;
        return direction;
    }

    private void FillStandardNormal(float[] values)
    {
        for (var i = 0; i < values.Length / 2; ++i)
        {
            var pair = GetStandardNormalFloatPair();
            values[i * 2] = pair.First;
            values[i * 2 + 1] = pair.Second;
        }
        if (values.Length % 2 != 0)
            values[^1] = GetStandardNormalFloat();
    }
}

public static partial class MathDefs
{
    private static uint _randomSeed = 1;

    public static void SetRandomSeed(uint seed)
    {
        _randomSeed = seed;
    }

    public static uint GetRandomSeed() => _randomSeed;

    public static int Rand()
    {
        _randomSeed = unchecked(_randomSeed * 214013u + 2531011u);
        return (int)((_randomSeed >> 16) & 32767u);
    }

    public static float RandStandardNormal()
    {
        var value = 0.0f;
        for (var i = 0; i < 12; ++i)
            value += Rand() / 32768.0f;
        return value - 6.0f;
    }

    public static float Random() => Rand() / 32768.0f;

    public static float Random(float range) => Rand() * range / 32767.0f;

    public static float Random(float min, float max) => Rand() * (max - min) / 32767.0f + min;

    public static int Random(int range) => (int)(Random() * range);

    public static int Random(int min, int max) => (int)(Random() * (max - min)) + min;

    public static float RandomNormal(float mean, float variance) => RandStandardNormal() * MathF.Sqrt(variance) + mean;
}
