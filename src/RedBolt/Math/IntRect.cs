using System.Globalization;
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

    public IntRect(IntVector2 min, IntVector2 max)
    {
        Left = min.X;
        Top = min.Y;
        Right = max.X;
        Bottom = max.Y;
    }

    public static readonly IntRect Zero = new(0, 0, 0, 0);

    public int Height => Bottom - Top;
    public IntVector2 Max => new(Right, Bottom);
    public IntVector2 Min => new(Left, Top);
    public IntVector2 Size => new(Width, Height);
    public int Width => Right - Left;

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

    public Intersection IsInside(IntVector2 point)
    {
        return point.X < Left || point.Y < Top || point.X >= Right || point.Y >= Bottom
            ? Intersection.Outside
            : Intersection.Inside;
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

    public bool Equals(IntRect other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
    public override bool Equals(object? obj) => obj is IntRect other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", Left, Top, Right, Bottom);

    public static bool operator ==(IntRect lhs, IntRect rhs) => lhs.Equals(rhs);
    public static bool operator !=(IntRect lhs, IntRect rhs) => !lhs.Equals(rhs);
    public static IntRect operator +(IntRect lhs, IntRect rhs) => new(lhs.Left + rhs.Left, lhs.Top + rhs.Top, lhs.Right + rhs.Right, lhs.Bottom + rhs.Bottom);
    public static IntRect operator -(IntRect lhs, IntRect rhs) => new(lhs.Left - rhs.Left, lhs.Top - rhs.Top, lhs.Right - rhs.Right, lhs.Bottom - rhs.Bottom);
    public static IntRect operator *(IntRect lhs, IntRect rhs) => new(lhs.Left * rhs.Left, lhs.Top * rhs.Top, lhs.Right * rhs.Right, lhs.Bottom * rhs.Bottom);
    public static IntRect operator /(IntRect lhs, IntRect rhs) => new(lhs.Left / rhs.Left, lhs.Top / rhs.Top, lhs.Right / rhs.Right, lhs.Bottom / rhs.Bottom);
}
