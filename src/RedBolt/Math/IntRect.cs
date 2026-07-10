using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct IntRect : IEquatable<IntRect>
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public IntRect(int left = 0, int top = 0, int right = 0, int bottom = 0)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public IntRect(Vector2i min, Vector2i max)
    {
        Left = min.X;
        Top = min.Y;
        Right = max.X;
        Bottom = max.Y;
    }

    public IntRect(int[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 4)
            throw new ArgumentException("Expected at least four elements.", nameof(data));

        Left = data[0];
        Top = data[1];
        Right = data[2];
        Bottom = data[3];
    }

    public static readonly IntRect Zero = new(0, 0, 0, 0);

    public readonly int[] Data => [Left, Top, Right, Bottom];

    public readonly int Height => Bottom - Top;
    public readonly Vector2i Max => new(Right, Bottom);
    public readonly Vector2i Min => new(Left, Top);
    public readonly Vector2i Size => new(Width, Height);
    public readonly int Width => Right - Left;

    public readonly IntRectIterator Begin() => new(this, Min);

    public readonly IntRectIterator End() => new(this, new Vector2i(Left, Bottom));

    public void Clear()
    {
        Left = 0;
        Top = 0;
        Right = 0;
        Bottom = 0;
    }

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
            Clear();
    }

    public readonly Intersection IsInside(Vector2i point)
    {
        return point.X < Left || point.Y < Top || point.X >= Right || point.Y >= Bottom
            ? Intersection.Outside
            : Intersection.Inside;
    }

    public readonly Intersection IsInside(IntRect rect)
    {
        if (rect.Right < Left || rect.Left > Right || rect.Bottom < Top || rect.Top > Bottom)
            return Intersection.Outside;
        if (rect.Left < Left || rect.Right > Right || rect.Top < Top || rect.Bottom > Bottom)
            return Intersection.Intersects;
        return Intersection.Inside;
    }

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

    public readonly bool Contains(Vector2i point) => IsInside(point) == Intersection.Inside;

    public readonly bool Equals(IntRect other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
    public override bool Equals(object? obj) => obj is IntRect other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
    public readonly uint ToHash() => (uint)GetHashCode();
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", Left, Top, Right, Bottom);

    public static bool operator ==(IntRect lhs, IntRect rhs) => lhs.Equals(rhs);
    public static bool operator !=(IntRect lhs, IntRect rhs) => !lhs.Equals(rhs);
    public static IntRect operator +(IntRect lhs, IntRect rhs) => new(lhs.Left + rhs.Left, lhs.Top + rhs.Top, lhs.Right + rhs.Right, lhs.Bottom + rhs.Bottom);
    public static IntRect operator -(IntRect lhs, IntRect rhs) => new(lhs.Left - rhs.Left, lhs.Top - rhs.Top, lhs.Right - rhs.Right, lhs.Bottom - rhs.Bottom);
    public static IntRect operator *(IntRect lhs, float rhs) => new((int)(lhs.Left * rhs), (int)(lhs.Top * rhs), (int)(lhs.Right * rhs), (int)(lhs.Bottom * rhs));
    public static IntRect operator /(IntRect lhs, float rhs) => new((int)(lhs.Left / rhs), (int)(lhs.Top / rhs), (int)(lhs.Right / rhs), (int)(lhs.Bottom / rhs));
}

[StructLayout(LayoutKind.Sequential)]
public struct IntRectIterator : IEquatable<IntRectIterator>
{
    private IntRect _rect;
    private Vector2i _index;

    public IntRectIterator(IntRect rect, Vector2i index)
    {
        _rect = rect;
        _index = index;
    }

    public readonly Vector2i Current => _index;

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

    public readonly bool Equals(IntRectIterator other)
    {
        Debug.Assert(_rect == other._rect);
        return _index == other._index;
    }

    public override readonly bool Equals(object? obj) => obj is IntRectIterator other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(_rect, _index);

    public static bool operator ==(IntRectIterator lhs, IntRectIterator rhs) => lhs.Equals(rhs);

    public static bool operator !=(IntRectIterator lhs, IntRectIterator rhs) => !lhs.Equals(rhs);
}
