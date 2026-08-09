namespace RedBolt.Math;

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

/// <summary>Two-dimensional bounding rectangle with integer values.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct IntRect : IEquatable<IntRect>
{
    /// <summary>Construct a zero rect.</summary>
    public IntRect()
    {
        Left = 0;
        Top = 0;
        Right = 0;
        Bottom = 0;
    }

    /// <summary>Construct from minimum and maximum vectors.</summary>
    public IntRect(Vector2i min, Vector2i max)
    {
        Left = min.X;
        Top = min.Y;
        Right = max.X;
        Bottom = max.Y;
    }

    /// <summary>Construct from coordinates.</summary>
    public IntRect(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>Construct from an int array.</summary>
    public IntRect(ReadOnlySpan<int> data)
    {
        Left = data[0];
        Top = data[1];
        Right = data[2];
        Bottom = data[3];
    }

    /// <summary>Test for equality with another rect.</summary>
    public static bool operator ==(IntRect lhs, IntRect rhs) => lhs.Equals(rhs);

    /// <summary>Test for inequality with another rect.</summary>
    public static bool operator !=(IntRect lhs, IntRect rhs) => !lhs.Equals(rhs);

    /// <summary>Divide by scalar.</summary>
    public static IntRect operator /(IntRect lhs, float rhs) => new((int)(lhs.Left / rhs), (int)(lhs.Top / rhs), (int)(lhs.Right / rhs), (int)(lhs.Bottom / rhs));

    /// <summary>Multiply by scalar.</summary>
    public static IntRect operator *(IntRect lhs, float rhs) => new((int)(lhs.Left * rhs), (int)(lhs.Top * rhs), (int)(lhs.Right * rhs), (int)(lhs.Bottom * rhs));

    /// <summary>Add another rect.</summary>
    public static IntRect operator +(IntRect lhs, IntRect rhs) => new(lhs.Left + rhs.Left, lhs.Top + rhs.Top, lhs.Right + rhs.Right, lhs.Bottom + rhs.Bottom);

    /// <summary>Subtract another rect.</summary>
    public static IntRect operator -(IntRect lhs, IntRect rhs) => new(lhs.Left - rhs.Left, lhs.Top - rhs.Top, lhs.Right - rhs.Right, lhs.Bottom - rhs.Bottom);

    /// <summary>Return size.</summary>
    public readonly Vector2i Size => new(Width, Height);

    /// <summary>Return width.</summary>
    public readonly int Width => Right - Left;

    /// <summary>Return height.</summary>
    public readonly int Height => Bottom - Top;

    /// <summary>Test whether a point is inside.</summary>
    public readonly Intersection IsInside(Vector2i point)
    {
        return point.X < Left || point.Y < Top || point.X >= Right || point.Y >= Bottom
            ? Intersection.Outside
            : Intersection.Inside;
    }

    /// <summary>Test if another rect is inside, outside or intersects.</summary>
    public readonly Intersection IsInside(IntRect rect)
    {
        if (rect.Right < Left || rect.Left >= Right || rect.Bottom < Top || rect.Top >= Bottom)
            return Intersection.Outside;
        if (rect.Left < Left || rect.Right > Right || rect.Top < Top || rect.Bottom > Bottom)
            return Intersection.Intersects;
        return Intersection.Inside;
    }

    /// <summary>Clip with another rect. Since IntRect does not have an undefined state like Rect, return (0, 0, 0, 0) if the result is empty.</summary>
    public void Clip(IntRect rect)
    {
        if (rect.Left > Left)
            Left = rect.Left;
        if (rect.Right < Right)
            Right = rect.Right;
        if (rect.Top > Top)
            Top = rect.Top;
        if (rect.Bottom < Bottom)
            Bottom = rect.Bottom;

        if (Left >= Right || Top >= Bottom)
            this = new IntRect();
    }

    /// <summary>Merge a rect. If this rect was empty, become the other rect. If the other rect is empty, do nothing.</summary>
    public void Merge(IntRect rect)
    {
        if (Width <= 0 || Height <= 0)
        {
            Left = rect.Left;
            Top = rect.Top;
            Right = rect.Right;
            Bottom = rect.Bottom;
        }
        else if (rect.Width > 0 && rect.Height > 0)
        {
            if (rect.Left < Left)
                Left = rect.Left;
            if (rect.Top < Top)
                Top = rect.Top;
            if (rect.Right > Right)
                Right = rect.Right;
            if (rect.Bottom > Bottom)
                Bottom = rect.Bottom;
        }
    }

    /// <summary>Return integer data.</summary>
    public ReadOnlySpan<int> Data() => MemoryMarshal.CreateReadOnlySpan(ref Left, 4);

    /// <summary>Return as string.</summary>
    public override readonly string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", Left, Top, Right, Bottom);

    /// <summary>Return hash value for HashSet &amp; HashMap.</summary>
    public readonly uint ToHash() => unchecked(
        (uint)Left * 31u * 31u * 31u
        + (uint)Right * 31u * 31u
        + (uint)Top * 31u
        + (uint)Bottom);

    /// <summary>Return left-top corner position.</summary>
    public readonly Vector2i Min => new(Left, Top);

    /// <summary>Return right-bottom corner position.</summary>
    public readonly Vector2i Max => new(Right, Bottom);

    /// <summary>Return true if specified point is within rectangle bounds.</summary>
    public readonly bool Contains(Vector2i point) => Left <= point.X && point.X < Right && Top <= point.Y && point.Y < Bottom;

    public readonly bool Equals(IntRect other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
    public override bool Equals(object? obj) => obj is IntRect other && Equals(other);
    public override readonly int GetHashCode() => unchecked((int)ToHash());

    /// <summary>Return begin iterator of IntRect.</summary>
    public readonly IntRectIterator Begin() => new(this, Min);

    /// <summary>Return end iterator of IntRect.</summary>
    public readonly IntRectIterator End() => new(this, new Vector2i(Left, Bottom));

    /// <summary>Left coordinate.</summary>
    public int Left;

    /// <summary>Top coordinate.</summary>
    public int Top;

    /// <summary>Right coordinate.</summary>
    public int Right;

    /// <summary>Bottom coordinate.</summary>
    public int Bottom;

    /// <summary>Zero-sized rect.</summary>
    public static readonly IntRect Zero = new(0, 0, 0, 0);
}

/// <summary>Iterator that iterates through all elements of IntRect row-by-row.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct IntRectIterator : IEquatable<IntRectIterator>
{
    /// <summary>Construct valid. Iterators with different rectangles are incompatible.</summary>
    public IntRectIterator(IntRect rect, Vector2i index)
    {
        _rect = rect;
        _index = index;
    }

    /// <summary>Pre-increment.</summary>
    public static IntRectIterator operator ++(IntRectIterator value)
    {
        ++value._index.X;
        if (value._index.X >= value._rect.Right)
        {
            ++value._index.Y;
            value._index.X = value._rect.Left;

            if (value._index.Y >= value._rect.Bottom)
                value._index.Y = value._rect.Bottom;
        }

        return value;
    }

    /// <summary>Compare for equality.</summary>
    public readonly bool Equals(IntRectIterator other)
    {
        Debug.Assert(_rect == other._rect);
        return _index == other._index;
    }

    public override readonly bool Equals(object? obj) => obj is IntRectIterator other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(_rect, _index);

    /// <summary>Compare for equality.</summary>
    public static bool operator ==(IntRectIterator lhs, IntRectIterator rhs) => lhs.Equals(rhs);

    /// <summary>Compare for non-equality.</summary>
    public static bool operator !=(IntRectIterator lhs, IntRectIterator rhs) => !lhs.Equals(rhs);

    /// <summary>Dereference.</summary>
    public readonly Vector2i Current => _index;

    /// <summary>Iterated rectangle.</summary>
    private IntRect _rect;

    /// <summary>Current index within rectangle.</summary>
    private Vector2i _index;
}
