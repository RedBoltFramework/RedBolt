namespace RedBolt.Tests;

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

    private static nuint ExpectedFileHandle =>
        nuint.Size > sizeof(uint) ? unchecked((nuint)uint.MaxValue + 1) : uint.MaxValue;
}
