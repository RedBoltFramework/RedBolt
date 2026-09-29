// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Math;

using System.Runtime.InteropServices;

/// <summary>Packed three-float storage matching Jolt's <c>JPH::Float3</c> layout (12 bytes, no
/// SIMD padding). Kept distinct from <see cref="BaseRealVector3{T}"/> (which projects Jolt's
/// 16-byte-aligned <c>JPH::Vec3</c>) so native collection bindings can tell the two element
/// types apart; use the implicit conversions to move between this and <see cref="Vector3f"/>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Float3(float x, float y, float z) : IEquatable<Float3>
{
    public float X = x;
    public float Y = y;
    public float Z = z;

    public static implicit operator Float3(BaseRealVector3<float> value) => new(value.X, value.Y, value.Z);

    public static implicit operator BaseRealVector3<float>(Float3 value) => new(value.X, value.Y, value.Z);

    public static bool operator ==(Float3 lhs, Float3 rhs) => lhs.Equals(rhs);

    public static bool operator !=(Float3 lhs, Float3 rhs) => !lhs.Equals(rhs);

    public readonly bool Equals(Float3 other) => X == other.X && Y == other.Y && Z == other.Z;

    public override readonly bool Equals(object? obj) => obj is Float3 other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z);
}
