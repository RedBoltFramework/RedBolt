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

    public ReadOnlySpan<byte> Bytes => bytes;

    public void Dispose()
    {
        if (handle.IsAllocated)
            handle.Free();
    }
}
