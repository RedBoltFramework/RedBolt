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
    private static RandomEngine _defaultEngine = null!;

    private uint _state;

    public RandomEngine()
        : this((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds())
    {
    }

    public RandomEngine(uint seed)
    {
        _state = seed % Modulus;
        if (_state == 0)
            _state = 1;
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

        _state = parsed;
    }

    public string Save() => _state.ToString(CultureInfo.InvariantCulture);

    public uint GetUInt()
    {
        _state = (uint)((ulong)_state * Multiplier % Modulus);
        return _state - EngineMin;
    }

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

    public Vector2f GetDirectionVector2()
    {
        Span<float> direction = stackalloc float[2];
        GetDirection(direction);
        return new Vector2f(direction[0], direction[1]);
    }

    public Vector3f GetDirectionVector3()
    {
        Span<float> direction = stackalloc float[3];
        GetDirection(direction);
        return new Vector3f(direction[0], direction[1], direction[2]);
    }

    public Quaternion GetQuaternion()
    {
        Span<float> direction = stackalloc float[4];
        GetDirection(direction);
        return new Quaternion(direction[0], direction[1], direction[2], direction[3]);
    }

    public Vector2f GetVector2(Vector2f min, Vector2f max) => new(GetFloat(min.X, max.X), GetFloat(min.Y, max.Y));

    public Vector3f GetVector3(Vector3f min, Vector3f max) => new(GetFloat(min.X, max.X), GetFloat(min.Y, max.Y), GetFloat(min.Z, max.Z));

    public Vector3f GetVector3(BoundingBox boundingBox) => GetVector3(boundingBox.Min, boundingBox.Max);

    public void GetStandardNormalFloatArray(Span<float> array)
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

    public void GetDirection(Span<float> direction)
    {
        if (direction.Length <= 0)
            throw new ArgumentOutOfRangeException(nameof(direction));

        for (var i = 0u; i < MaxIterations; ++i)
        {
            GetStandardNormalFloatArray(direction);

            var length = 0.0f;
            foreach (var x in direction)
                length += x * x;

            if (length > MathDefs.Epsilon)
            {
                var reciprocalLength = 1.0f / MathF.Sqrt(length);
                for (var j = 0; j < direction.Length; ++j)
                    direction[j] *= reciprocalLength;
                return;
            }
        }

        direction.Clear();
        direction[0] = 1.0f;
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
