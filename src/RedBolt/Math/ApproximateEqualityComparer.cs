namespace RedBolt;

public sealed class ApproximateEqualityComparer<T> : IEqualityComparer<T>
    where T : IApproximateEquatable<T>
{
    public static readonly ApproximateEqualityComparer<T> Default = new();

    private readonly float _epsilon;

    public ApproximateEqualityComparer(float epsilon = MathDefs.Epsilon)
    {
        this._epsilon = epsilon;
    }

    public bool Equals(T? x, T? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;

        return x.ApproximatelyEquivalent(y, _epsilon);
    }

    public int GetHashCode(T obj) => obj.GetHashCode();
}
