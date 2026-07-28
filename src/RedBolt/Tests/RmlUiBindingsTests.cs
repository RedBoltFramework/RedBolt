namespace RedBolt.Tests;

using System.Runtime.InteropServices;

using RedBolt.Bindings.RmlUi;

[Collection(nameof(RmlUiBindingsCollection))]
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

    [Fact]
    public void Font_engine_interface_round_trips_managed_director()
    {
        var original = RmlUiNativeBindings.GetFontEngineInterface();
        using var fontEngineInterface = new TestFontEngineInterface();
        try
        {
            RmlUiNativeBindings.SetFontEngineInterface(fontEngineInterface);

            var retrieved = RmlUiNativeBindings.GetFontEngineInterface();
            Assert.NotNull(retrieved);
            Assert.Equal(fontEngineInterface.Handle, retrieved.Handle);
        }
        finally
        {
            RmlUiNativeBindings.SetFontEngineInterface(original);
        }
    }

    [Fact]
    public void Core_lifecycle_creates_and_updates_context()
    {
        var originalFontEngineInterface = RmlUiNativeBindings.GetFontEngineInterface();
        var originalRenderInterface = RmlUiNativeBindings.GetRenderInterface();
        using var fontEngineInterface = new TestFontEngineInterface();
        using var renderInterface = new TestRenderInterface();
        using var name = new Std.String();
        name.Assign("RedBolt.Tests.RmlUiBindings");
        Context? context = null;
        var initialized = false;
        try
        {
            RmlUiNativeBindings.SetFontEngineInterface(fontEngineInterface);
            RmlUiNativeBindings.SetRenderInterface(renderInterface.GetAdaptedInterface());
            Assert.True(RmlUiNativeBindings.Initialise());
            initialized = true;
            Assert.True(fontEngineInterface.IsInitialized);

            context = RmlUiNativeBindings.CreateContext(
                name, new RmlUiLegacyVector2i { X = 320, Y = 200 });

            Assert.NotNull(context);
            Assert.Equal(320, context.GetDimensions().X);
            Assert.Equal(200, context.GetDimensions().Y);
            using var contextName = context.GetName();
            Assert.Equal("RedBolt.Tests.RmlUiBindings", contextName.ToString());

            context.SetDimensions(new RmlUiLegacyVector2i { X = 640, Y = 480 });
            context.SetDensityIndependentPixelRatio(1.5f);

            Assert.Equal(640, context.GetDimensions().X);
            Assert.Equal(480, context.GetDimensions().Y);
            Assert.Equal(1.5f, context.GetDensityIndependentPixelRatio());
            Assert.True(context.Update());
            Assert.True(context.Render());

            context.Dispose();
            context = null;
            Assert.True(RmlUiNativeBindings.RemoveContext(name));
            RmlUiNativeBindings.Shutdown();
            initialized = false;
            Assert.True(fontEngineInterface.IsShutdown);
        }
        finally
        {
            context?.Dispose();
            if (initialized)
            {
                RmlUiNativeBindings.RemoveContext(name);
                RmlUiNativeBindings.Shutdown();
            }
            RmlUiNativeBindings.SetFontEngineInterface(originalFontEngineInterface);
            RmlUiNativeBindings.SetRenderInterface(originalRenderInterface);
        }
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

    private sealed class TestFontEngineInterface : FontEngineInterface
    {
        public bool IsInitialized { get; private set; }

        public bool IsShutdown { get; private set; }

        public override void Initialize()
        {
            IsInitialized = true;
        }

        public override void Shutdown()
        {
            IsShutdown = true;
        }
    }

    private sealed class TestEventListener : EventListener
    {
        public override void ProcessEvent(Event @event)
        {
        }
    }

    private static nuint ExpectedFileHandle =>
        nuint.Size > sizeof(uint) ? unchecked((nuint)uint.MaxValue + 1) : uint.MaxValue;
}

[CollectionDefinition(nameof(RmlUiBindingsCollection), DisableParallelization = true)]
public sealed class RmlUiBindingsCollection;
