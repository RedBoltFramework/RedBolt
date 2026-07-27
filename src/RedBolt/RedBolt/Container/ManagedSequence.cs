// This is a C#-only allocation-free SequenceEqual replacement needed to avoid GC churn.
namespace RedBolt;

/// <summary>
/// Allocation-free element-wise comparisons for managed collections. Replaces
/// <c>Enumerable.SequenceEqual</c>, whose enumerator over an interface source boxes onto the
/// heap; the indexed loop here uses the concrete indexer and allocates nothing.
/// </summary>
internal static class ManagedSequence
{
    public static bool Equal<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left.Count != right.Count)
            return false;
        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < left.Count; ++i)
        {
            if (!comparer.Equals(left[i], right[i]))
                return false;
        }

        return true;
    }
}
