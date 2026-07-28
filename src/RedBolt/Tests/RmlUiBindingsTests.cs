namespace RedBolt.Tests;

using System.Runtime.InteropServices;

using RedBolt.Bindings.RmlUi;

public sealed class RmlUiBindingsTests
{
    [Fact]
    public void System_interface_round_trips_managed_director()
    {
        var original = RmlUiNativeBindings.GetSystemInterface();
        using var systemInterface = new TestSystemInterface();
        try
        {
            RmlUiNativeBindings.SetSystemInterface(systemInterface);

            var retrieved = RmlUiNativeBindings.GetSystemInterface();
            Assert.NotNull(retrieved);
            Assert.Equal(systemInterface.Handle, retrieved.Handle);
            Assert.Equal(42.5, retrieved.GetElapsedTime());
        }
        finally
        {
            RmlUiNativeBindings.SetSystemInterface(original);
        }
    }

    [Fact]
    public void File_interface_round_trips_pointer_width_file_handle()
    {
        var original = RmlUiNativeBindings.GetFileInterface();
        using var fileInterface = new TestFileInterface();
        using var path = new Std.String();
        try
        {
            RmlUiNativeBindings.SetFileInterface(fileInterface);

            var retrieved = RmlUiNativeBindings.GetFileInterface();
            Assert.NotNull(retrieved);
            Assert.Equal(fileInterface.Handle, retrieved.Handle);
            Assert.Equal(ExpectedFileHandle, retrieved.Open(path));
        }
        finally
        {
            RmlUiNativeBindings.SetFileInterface(original);
        }
    }

    [Fact]
    public void Render_interface_compatibility_round_trips_adapted_pointer()
    {
        var original = RmlUiNativeBindings.GetRenderInterface();
        using var renderInterface = new TestRenderInterface();
        var adaptedInterface = renderInterface.GetAdaptedInterface();
        try
        {
            RmlUiNativeBindings.SetRenderInterface(adaptedInterface);

            Assert.NotEqual(nint.Zero, adaptedInterface);
            Assert.Equal(adaptedInterface, RmlUiNativeBindings.GetRenderInterface());
            Assert.Equal(8, Marshal.SizeOf<RmlUiLegacyVector2f>());
            Assert.Equal(8, Marshal.SizeOf<RmlUiLegacyVector2i>());
            Assert.Equal(20, Marshal.SizeOf<RmlUiLegacyVertex>());
            Assert.Equal(64, Marshal.SizeOf<RmlUiLegacyMatrix4f>());
        }
        finally
        {
            RmlUiNativeBindings.SetRenderInterface(original);
        }
    }

    [Fact]
    public void Event_listener_constructs_managed_director()
    {
        using var eventListener = new TestEventListener();

        Assert.NotEqual(nint.Zero, eventListener.Handle);
        Assert.Same(eventListener, EventListener.CreateManagedWrapper(eventListener.Handle, false));
    }

    private sealed class TestSystemInterface : SystemInterface
    {
        public override double GetElapsedTime()
        {
            return 42.5;
        }
    }

    private sealed class TestFileInterface : FileInterface
    {
        public override nuint Open(Std.String path)
        {
            return ExpectedFileHandle;
        }

        public override void Close(nuint file)
        {
        }

        public override nuint Read(nint buffer, nuint size, nuint file)
        {
            return 0;
        }

        public override bool Seek(nuint file, nint offset, int origin)
        {
            return false;
        }

        public override nuint Tell(nuint file)
        {
            return 0;
        }
    }

    private sealed class TestRenderInterface : RenderInterfaceCompatibility
    {
        public override void RenderGeometry(
            nint vertices, int numVertices, nint indices, int numIndices, nuint texture, nint translation)
        {
        }

        public override void EnableScissorRegion(bool enable)
        {
        }

        public override void SetScissorRegion(int x, int y, int width, int height)
        {
        }
    }

    private sealed class TestEventListener : EventListener
    {
        public override void ProcessEvent(nint @event)
        {
        }
    }

    private static nuint ExpectedFileHandle =>
        nuint.Size > sizeof(uint) ? unchecked((nuint)uint.MaxValue + 1) : uint.MaxValue;
}
