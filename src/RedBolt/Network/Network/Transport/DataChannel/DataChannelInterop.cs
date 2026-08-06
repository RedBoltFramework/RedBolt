// Copyright (c) 2025-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Network;

using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Bindings.LibDataChannel;

internal static class DataChannelInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void DescriptionCallback(int peer, nint sdp, nint type, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void CandidateCallback(int peer, nint candidate, nint mid, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void DataChannelCallback(int peer, int dataChannel, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void OpenCallback(int id, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void ClosedCallback(int id, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void MessageCallback(int id, nint message, int size, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void WebSocketClientCallback(int server, int webSocket, nint userData);

    internal static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value + '\0');

    internal static string StringFromUtf8(nint value) =>
        value == 0 ? string.Empty : Marshal.PtrToStringUTF8(value) ?? string.Empty;

    internal static byte[] CopyBytes(nint data, int size)
    {
        var result = new byte[size];
        if (size != 0)
            Marshal.Copy(data, result, 0, size);
        return result;
    }

    internal static T? GetTarget<T>(nint userData)
        where T : class
    {
        if (userData == 0)
            return null;
        try
        {
            return GCHandle.FromIntPtr(userData).Target as T;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    internal static void Guard(Action callback)
    {
        try
        {
            callback();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }
}

internal sealed class NativeUtf8String : IDisposable
{
    public NativeUtf8String(string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        var bytes = DataChannelInterop.Utf8(value);
        Pointer = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, Pointer, bytes.Length);
    }

    public nint Pointer { get; private set; }

    public void Dispose()
    {
        if (Pointer != 0)
        {
            Marshal.FreeHGlobal(Pointer);
            Pointer = 0;
        }
    }
}

internal sealed class NativeUtf8StringArray : IDisposable
{
    public NativeUtf8StringArray(IReadOnlyList<string> values)
    {
        _strings = values.Select(value => new NativeUtf8String(value)).ToArray();
        if (_strings.Length == 0)
            return;

        Pointer = Marshal.AllocHGlobal(checked(_strings.Length * nint.Size));
        for (var i = 0; i < _strings.Length; ++i)
            Marshal.WriteIntPtr(Pointer, i * nint.Size, _strings[i].Pointer);
    }

    public nint Pointer { get; private set; }

    public void Apply(RtcConfiguration configuration)
    {
        // iceServers is the first field of rtcConfiguration in rtc/rtc.h.
        Marshal.WriteIntPtr(configuration.Handle, Pointer);
        configuration.IceServersCount = _strings.Length;
    }

    public void Dispose()
    {
        if (Pointer != 0)
        {
            Marshal.FreeHGlobal(Pointer);
            Pointer = 0;
        }
        foreach (var value in _strings)
            value.Dispose();
    }

    private readonly NativeUtf8String[] _strings;
}
