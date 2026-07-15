// C++: no direct C++ counterpart file. C#-only helper for approximate equality comparisons.
namespace RedBolt;

public interface IApproximateEquatable<in T>
{
    bool ApproximatelyEquivalent(T rhs, float epsilon = MathDefs.Epsilon);
}
