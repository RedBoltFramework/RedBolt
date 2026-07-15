namespace RedBolt;

public interface IApproximateEquatable<in T>
{
    bool ApproximatelyEquivalent(T rhs, float epsilon = MathDefs.Epsilon);
}
