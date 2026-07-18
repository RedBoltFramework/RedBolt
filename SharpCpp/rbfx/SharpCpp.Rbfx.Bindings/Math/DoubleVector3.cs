//
// Copyright (c) 2008-2019 the Urho3D project.
// Copyright (c) 2017-2023 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Urho3D.Math
{
    /// Three-dimensional double-precision vector.
    [StructLayout(LayoutKind.Sequential)]
    public struct DoubleVector3 : IEquatable<DoubleVector3>
    {
        /// Construct from coordinates.
        public DoubleVector3(double x = 0, double y = 0, double z = 0)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// Construct from a double array.
        public DoubleVector3(IReadOnlyList<double> data)
        {
            X = data[0];
            Y = data[1];
            Z = data[2];
        }

        /// Test for equality with another vector without epsilon.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in DoubleVector3 lhs, in DoubleVector3 rhs)
        {
            return lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;
        }

        /// Test for inequality with another vector without epsilon.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in DoubleVector3 lhs, in DoubleVector3 rhs)
        {
            return lhs.X != rhs.X || lhs.Y != rhs.Y || lhs.Z != rhs.Z;
        }

        /// Add a vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 operator +(in DoubleVector3 lhs, in DoubleVector3 rhs)
        {
            return new DoubleVector3(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);
        }

        /// Return negation.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 operator -(in DoubleVector3 rhs)
        {
            return new DoubleVector3(-rhs.X, -rhs.Y, -rhs.Z);
        }

        /// Subtract a vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 operator -(in DoubleVector3 lhs, in DoubleVector3 rhs)
        {
            return new DoubleVector3(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);
        }

        /// Multiply with a scalar.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 operator *(in DoubleVector3 lhs, double rhs)
        {
            return new DoubleVector3(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs);
        }

        /// Multiply with a vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 operator *(in DoubleVector3 lhs, in DoubleVector3 rhs)
        {
            return new DoubleVector3(lhs.X * rhs.X, lhs.Y * rhs.Y, lhs.Z * rhs.Z);
        }

        /// Divide by a scalar.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 operator /(in DoubleVector3 lhs, double rhs)
        {
            return new DoubleVector3(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs);
        }

        /// Divide by a vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 operator /(in DoubleVector3 lhs, in DoubleVector3 rhs)
        {
            return new DoubleVector3(lhs.X / rhs.X, lhs.Y / rhs.Y, lhs.Z / rhs.Z);
        }

        /// Multiply DoubleVector3 with a scalar.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 operator *(double lhs, in DoubleVector3 rhs)
        {
            return rhs * lhs;
        }

        /// Return value by index.
        public double this[int index]
        {
            get
            {
                if (index < 0 || index > 2)
                    throw new IndexOutOfRangeException();
                unsafe
                {
                    fixed (double* p = &X)
                    {
                        return p[index];
                    }
                }
            }
            set
            {
                if (index < 0 || index > 2)
                    throw new IndexOutOfRangeException();
                unsafe
                {
                    fixed (double* p = &X)
                    {
                        p[index] = value;
                    }
                }
            }
        }

        /// Normalize to unit length.
        public void Normalize()
        {
            double lenSquared = LengthSquared;
            if (!MathDefs.Equals(lenSquared, 1.0) && lenSquared > 0.0)
            {
                double invLen = 1.0 / Math.Sqrt(lenSquared);
                X *= invLen;
                Y *= invLen;
                Z *= invLen;
            }
        }

        /// Return length.
        public double Length => Math.Sqrt(LengthSquared);

        /// Return squared length.
        public double LengthSquared => X * X + Y * Y + Z * Z;

        /// Calculate dot product.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double DotProduct(in DoubleVector3 rhs)
        {
            return X * rhs.X + Y * rhs.Y + Z * rhs.Z;
        }

        /// Calculate absolute dot product.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double AbsDotProduct(in DoubleVector3 rhs)
        {
            return Math.Abs(X * rhs.X) + Math.Abs(Y * rhs.Y) + Math.Abs(Z * rhs.Z);
        }

        /// Project direction vector onto axis.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ProjectOntoAxis(in DoubleVector3 axis)
        {
            return DotProduct(axis.Normalized);
        }

        /// Project position vector onto plane with given origin and normal.
        public DoubleVector3 ProjectOntoPlane(in DoubleVector3 origin, in DoubleVector3 normal)
        {
            DoubleVector3 delta = this - origin;
            return this - normal.Normalized * delta.ProjectOntoAxis(normal);
        }

        /// Project position vector onto line segment.
        public DoubleVector3 ProjectOntoLine(in DoubleVector3 from, in DoubleVector3 to, bool clamped = false)
        {
            DoubleVector3 direction = to - from;
            double lengthSquared = direction.LengthSquared;
            double factor = (this - from).DotProduct(direction) / lengthSquared;

            if (clamped)
                factor = MathDefs.Clamp(factor, 0.0, 1.0);

            return from + direction * factor;
        }

        /// Calculate distance to another position vector.
        public double DistanceToPoint(in DoubleVector3 point)
        {
            return (this - point).Length;
        }

        /// Calculate distance to the plane with given origin and normal.
        public double DistanceToPlane(in DoubleVector3 origin, in DoubleVector3 normal)
        {
            return (this - origin).ProjectOntoAxis(normal);
        }

        /// Make vector orthogonal to the axis.
        public DoubleVector3 Orthogonalize(in DoubleVector3 axis)
        {
            return axis.CrossProduct(this).CrossProduct(axis).Normalized;
        }

        /// Calculate cross product.
        public DoubleVector3 CrossProduct(in DoubleVector3 rhs)
        {
            return new DoubleVector3(
                Y * rhs.Z - Z * rhs.Y,
                Z * rhs.X - X * rhs.Z,
                X * rhs.Y - Y * rhs.X
            );
        }

        /// Return absolute vector.
        public DoubleVector3 Abs => new DoubleVector3(Math.Abs(X), Math.Abs(Y), Math.Abs(Z));

        /// Linear interpolation with another vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DoubleVector3 Lerp(in DoubleVector3 rhs, double t)
        {
            return this * (1.0 - t) + rhs * t;
        }

        /// Test for equality with another vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(DoubleVector3 rhs)
        {
            return X == rhs.X && Y == rhs.Y && Z == rhs.Z;
        }

        /// Test for equality with another vector.
        public override bool Equals(object? obj)
        {
            return obj is DoubleVector3 other && Equals(other);
        }

        /// Returns the angle between this vector and another vector in degrees.
        public double Angle(in DoubleVector3 rhs)
        {
            return MathDefs.Acos(DotProduct(rhs) / (Length * rhs.Length));
        }

        /// Return whether is NaN.
        public bool IsNaN => double.IsNaN(X) || double.IsNaN(Y) || double.IsNaN(Z);

        /// Return normalized to unit length.
        public DoubleVector3 Normalized
        {
            get
            {
                double lenSquared = LengthSquared;
                if (!MathDefs.Equals(lenSquared, 1.0) && lenSquared > 0.0)
                {
                    double invLen = 1.0 / Math.Sqrt(lenSquared);
                    return this * invLen;
                }
                else
                    return this;
            }
        }

        /// Return normalized to unit length or zero if length is too small.
        public DoubleVector3 NormalizedOrDefault
        {
            get
            {
                double lenSquared = LengthSquared;
                if (lenSquared < MathDefs.LargeEpsilon * MathDefs.LargeEpsilon)
                    return DoubleVector3.Zero;
                double invLen = 1.0 / Math.Sqrt(lenSquared);
                return this * invLen;
            }
        }

        /// Return double data.
        public double[] Data => new double[] {X, Y, Z};

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", X, Y, Z);
        }

        /// <summary>
        /// Converts the string representation of a DoubleVector3 into value.
        /// </summary>
        public static DoubleVector3 Parse(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? default : MathParsing.ToDoubleVector3(value);
        }

        /// <summary>Returns the hash code for this instance.</summary>
        /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        /// Per-component linear interpolation between two 3-vectors.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 Lerp(in DoubleVector3 lhs, in DoubleVector3 rhs, in DoubleVector3 t)
        {
            return lhs + (rhs - lhs) * t;
        }

        /// Per-component min of two 3-vectors.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 Min(in DoubleVector3 lhs, in DoubleVector3 rhs)
        {
            return new DoubleVector3(Math.Min(lhs.X, rhs.X), Math.Min(lhs.Y, rhs.Y), Math.Min(lhs.Z, rhs.Z));
        }

        /// Per-component max of two 3-vectors.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 Max(in DoubleVector3 lhs, in DoubleVector3 rhs)
        {
            return new DoubleVector3(Math.Max(lhs.X, rhs.X), Math.Max(lhs.Y, rhs.Y), Math.Max(lhs.Z, rhs.Z));
        }

        /// Per-component floor of 3-vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 Floor(in DoubleVector3 vec)
        {
            return new DoubleVector3(Math.Floor(vec.X), Math.Floor(vec.Y), Math.Floor(vec.Z));
        }

        /// Per-component round of 3-vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 Round(in DoubleVector3 vec)
        {
            return new DoubleVector3(Math.Round(vec.X), Math.Round(vec.Y), Math.Round(vec.Z));
        }

        /// Per-component ceil of 3-vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector3 Ceil(in DoubleVector3 vec)
        {
            return new DoubleVector3(Math.Ceiling(vec.X), Math.Ceiling(vec.Y), Math.Ceiling(vec.Z));
        }

        /// X coordinate.
        public double X;

        /// Y coordinate.
        public double Y;

        /// Z coordinate.
        public double Z;

        /// Zero vector.
        public static readonly DoubleVector3 Zero;

        /// (-1,0,0) vector.
        public static readonly DoubleVector3 Left = new DoubleVector3(-1, 0, 0);

        /// (1,0,0) vector.
        public static readonly DoubleVector3 Right = new DoubleVector3(1, 0, 0);

        /// (0,1,0) vector.
        public static readonly DoubleVector3 Up = new DoubleVector3(0, 1, 0);

        /// (0,-1,0) vector.
        public static readonly DoubleVector3 Down = new DoubleVector3(0, -1, 0);

        /// (0,0,1) vector.
        public static readonly DoubleVector3 Forward = new DoubleVector3(0, 0, 1);

        /// (0,0,-1) vector.
        public static readonly DoubleVector3 Back = new DoubleVector3(0, 0, -1);

        /// (1,1,1) vector.
        public static readonly DoubleVector3 One = new DoubleVector3(1, 1, 1);
    }
}
