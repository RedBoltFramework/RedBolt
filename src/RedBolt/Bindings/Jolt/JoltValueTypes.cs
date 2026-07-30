namespace RedBolt.Bindings.Jolt;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct JoltRayCastResult
{
    public uint BodyId;
    public float Fraction;
    public uint SubShapeId;
}
