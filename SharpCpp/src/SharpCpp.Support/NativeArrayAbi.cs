using System.Runtime.InteropServices;

namespace SharpCpp.Support;

[StructLayout(LayoutKind.Sequential)]
public readonly struct NativeArrayAbi
{
    public NativeArrayAbi(nint handle, nuint length)
    {
        Handle = handle;
        Length = length;
    }

    public nint Handle { get; }

    public nuint Length { get; }
}
