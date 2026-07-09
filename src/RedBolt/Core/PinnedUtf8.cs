using System.Runtime.InteropServices;
using System.Text;

namespace RedBolt;

internal sealed class PinnedUtf8 : IDisposable
{
    private readonly byte[] _bytes;
    private readonly GCHandle _handle;

    public PinnedUtf8(string value)
    {
        _bytes = Encoding.UTF8.GetBytes(value + '\0');
        _handle = GCHandle.Alloc(_bytes, GCHandleType.Pinned);
    }

    public IntPtr Pointer => _handle.AddrOfPinnedObject();

    public ReadOnlySpan<byte> Bytes => _bytes;

    public void Dispose()
    {
        if (_handle.IsAllocated)
            _handle.Free();
    }
}
