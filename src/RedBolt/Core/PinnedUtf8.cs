using System.Runtime.InteropServices;
using System.Text;

namespace RedBolt;

internal sealed class PinnedUtf8 : IDisposable
{
    private readonly byte[] bytes;
    private readonly GCHandle handle;

    public PinnedUtf8(string value)
    {
        bytes = Encoding.UTF8.GetBytes(value + '\0');
        handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
    }

    public IntPtr Pointer => handle.AddrOfPinnedObject();

    public ref sbyte FirstByte => ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, sbyte>(bytes.AsSpan()));

    public void Dispose()
    {
        if (handle.IsAllocated)
            handle.Free();
    }
}
