namespace RedBolt.Bindings.Diligent;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public readonly struct BlendFactors(float red, float green, float blue, float alpha)
{
    public static BlendFactors One => new(1.0f, 1.0f, 1.0f, 1.0f);

    public readonly float Red = red;
    public readonly float Green = green;
    public readonly float Blue = blue;
    public readonly float Alpha = alpha;
}
