// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Math;

public interface IApproximateEquatable<in T>
{
    bool ApproximatelyEquivalent(T rhs, float epsilon = MathDefs.Epsilon);
}
