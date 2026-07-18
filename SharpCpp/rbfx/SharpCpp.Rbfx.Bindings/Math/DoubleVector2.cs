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
    /// Two-dimensional double-precision vector.
    [StructLayout(LayoutKind.Sequential)]
    public struct DoubleVector2 : IEquatable<DoubleVector2>
    {
        /// Construct from coordinates.
        public DoubleVector2(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// Construct from a double array.
        public DoubleVector2(IReadOnlyList<double> data)
        {
            X = data[0];
            Y = data[1];
        }

        /// Test for equality with another vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in DoubleVector2 lhs, in DoubleVector2 rhs)
        {
            return lhs.X == rhs.X && lhs.Y == rhs.Y;
        }

        /// Test for inequality with another vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in DoubleVector2 lhs, in DoubleVector2 rhs)
        {
            return lhs.X != rhs.X || lhs.Y != rhs.Y;
        }

        /// Add a vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 operator +(in DoubleVector2 lhs, in DoubleVector2 rhs)
        {
            return new DoubleVector2(lhs.X + rhs.X, lhs.Y + rhs.Y);
        }

        /// Return negation.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 operator -(in DoubleVector2 v)
        {
            return new DoubleVector2(-v.X, -v.Y);
        }

        /// Subtract a vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 operator -(in DoubleVector2 lhs, in DoubleVector2 rhs)
        {
            return new DoubleVector2(lhs.X - rhs.X, lhs.Y - rhs.Y);
        }

        /// Multiply with a scalar.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 operator *(in DoubleVector2 lhs, double rhs)
        {
            return new DoubleVector2(lhs.X * rhs, lhs.Y * rhs);
        }

        /// Multiply with a vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 operator *(in DoubleVector2 lhs, in DoubleVector2 rhs)
        {
            return new DoubleVector2(lhs.X * rhs.X, lhs.Y * rhs.Y);
        }

        /// Divide by a scalar.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 operator /(in DoubleVector2 lhs, double rhs)
        {
            return new DoubleVector2(lhs.X / rhs, lhs.Y / rhs);
        }

        /// Divide by a vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 operator /(in DoubleVector2 lhs, in DoubleVector2 rhs)
        {
            return new DoubleVector2(lhs.X / rhs.X, lhs.Y / rhs.Y);
        }

        /// Multiply DoubleVector2 with a scalar
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 operator *(double lhs, in DoubleVector2 rhs)
        {
            return rhs * lhs;
        }

        /// Return value by index.
        public double this[int index]
        {
            get
            {
                if (index < 0 || index > 1)
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
                if (index < 0 || index > 1)
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
            }
        }

        /// Return length.
        public double Length => Math.Sqrt(X * X + Y * Y);

        /// Return squared length.
        public double LengthSquared => X * X + Y * Y;

        /// Calculate dot product.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double DotProduct(in DoubleVector2 rhs)
        {
            return X * rhs.X + Y * rhs.Y;
        }

        /// Calculate absolute dot product.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double AbsDotProduct(in DoubleVector2 rhs)
        {
            return Math.Abs(X * rhs.X) + Math.Abs(Y * rhs.Y);
        }

        /// Project vector onto axis.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ProjectOntoAxis(in DoubleVector2 axis)
        {
            return DotProduct(axis.Normalized);
        }

        /// Returns the angle between this vector and another vector in degrees.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Angle(in DoubleVector2 rhs)
        {
            return Math.Acos(DotProduct(rhs) / (Length * rhs.Length));
        }

        /// Return absolute vector.
        public DoubleVector2 Abs => new DoubleVector2(Math.Abs(X), Math.Abs(Y));

        /// Linear interpolation with another vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DoubleVector2 Lerp(in DoubleVector2 rhs, double t)
        {
            return this * (1.0 - t) + rhs * t;
        }

        /// Test for equality with another vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(DoubleVector2 rhs)
        {
            return X == rhs.X && Y == rhs.Y;
        }

        /// Return whether is NaN.
        public bool IsNaN => double.IsNaN(X) || double.IsNaN(Y);

        /// Return normalized to unit length.
        public DoubleVector2 Normalized
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
        public DoubleVector2 NormalizedOrDefault
        {
            get
            {
                double lenSquared = LengthSquared;
                if (lenSquared < MathDefs.LargeEpsilon * MathDefs.LargeEpsilon)
                    return DoubleVector2.Zero;
                double invLen = 1.0 / Math.Sqrt(lenSquared);
                return this * invLen;
            }
        }

        /// Return double data.
        public double[] Data => new double[] {X, Y};

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}", X, Y);
        }

        /// <summary>
        /// Converts the string representation of a DoubleVector2 into value.
        /// </summary>
        public static DoubleVector2 Parse(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? default : MathParsing.ToDoubleVector2(value);
        }

        /// <summary>Returns the hash code for this instance.</summary>
        /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        /// Per-component linear interpolation between two 2-vectors.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 Lerp(in DoubleVector2 lhs, in DoubleVector2 rhs, in DoubleVector2 t)
        {
            return lhs + (rhs - lhs) * t;
        }

        /// Per-component min of two 2-vectors.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 Min(in DoubleVector2 lhs, in DoubleVector2 rhs)
        {
            return new DoubleVector2(Math.Min(lhs.X, rhs.X), Math.Min(lhs.Y, rhs.Y));
        }

        /// Per-component max of two 2-vectors.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 Max(in DoubleVector2 lhs, in DoubleVector2 rhs)
        {
            return new DoubleVector2(Math.Max(lhs.X, rhs.X), Math.Max(lhs.Y, rhs.Y));
        }

        /// Per-component floor of 2-vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 Floor(in DoubleVector2 vec)
        {
            return new DoubleVector2(Math.Floor(vec.X), Math.Floor(vec.Y));
        }

        /// Per-component round of 2-vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 Round(in DoubleVector2 vec)
        {
            return new DoubleVector2(Math.Round(vec.X), Math.Round(vec.Y));
        }

        /// Per-component ceil of 2-vector.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DoubleVector2 Ceil(in DoubleVector2 vec)
        {
            return new DoubleVector2(Math.Ceiling(vec.X), Math.Ceiling(vec.Y));
        }

        /// X coordinate.
        public double X;

        /// Y coordinate.
        public double Y;

        /// Zero vector.
        public static readonly DoubleVector2 Zero;

        /// (-1,0) vector.
        public static readonly DoubleVector2 Left = new DoubleVector2(-1, 0);

        /// (1,0) vector.
        public static readonly DoubleVector2 Right = new DoubleVector2(1, 0);

        /// (0,1) vector.
        public static readonly DoubleVector2 Up = new DoubleVector2(0, 1);

        /// (0,-1) vector.
        public static readonly DoubleVector2 Down = new DoubleVector2(0, -1);

        /// (1,1) vector.
        public static readonly DoubleVector2 One = new DoubleVector2(1, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj)
        {
            return obj is DoubleVector2 other && Equals(other);
        }
    }
}
