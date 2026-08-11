namespace RedBolt.Bindings.DearImGui;

using System.Runtime.InteropServices;

public static partial class ImGui
{
#if REDBOLT_PLATFORM_ANDROID
    private const string NativeLibraryName = "libRedBolt.Native.DearImGui.so";
#else
    private const string NativeLibraryName = "RedBolt.Native.DearImGui";
#endif

    [LibraryImport(NativeLibraryName, EntryPoint = "ImGuiBackend_CreateContext")]
    private static partial nint NativeImGuiBackendCreateContext();

    [LibraryImport(NativeLibraryName, EntryPoint = "ImGuiBackend_DestroyContext")]
    private static partial void NativeImGuiBackendDestroyContext(nint context);

    [LibraryImport(NativeLibraryName, EntryPoint = "ImGuiBackend_SetCurrentContext")]
    private static partial void NativeImGuiBackendSetCurrentContext(nint context);

    [LibraryImport(NativeLibraryName, EntryPoint = "ImGuiBackend_Sdl2InitForOpenGL")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool NativeImGuiBackendSdl2InitForOpenGL(nint window, nint context);

    [LibraryImport(NativeLibraryName, EntryPoint = "ImGuiBackend_Sdl2InitForOther")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool NativeImGuiBackendSdl2InitForOther(nint window);

    [LibraryImport(NativeLibraryName, EntryPoint = "ImGuiBackend_Sdl2InitForD3D")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool NativeImGuiBackendSdl2InitForD3D(nint window);

    public static nint CreateContext() => NativeImGuiBackendCreateContext();

    public static void DestroyContext(nint context) => NativeImGuiBackendDestroyContext(context);

    public static void SetCurrentContext(nint context) => NativeImGuiBackendSetCurrentContext(context);

    public static bool ImGuiImplSDL2InitForOpenGL(nint window, nint context) =>
        NativeImGuiBackendSdl2InitForOpenGL(window, context);

    public static bool ImGuiImplSDL2InitForOther(nint window) =>
        NativeImGuiBackendSdl2InitForOther(window);

    public static bool ImGuiImplSDL2InitForD3D(nint window) =>
        NativeImGuiBackendSdl2InitForD3D(window);
}
