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
            Assert.Equal(systemInterface.Handle, retrieved.Handle);
            Assert.Equal(42.5, retrieved.GetElapsedTime());
        }
        finally
        {
            RmlUiNativeBindings.SetSystemInterface(original.IsNull ? null! : original);
        }
    }

    private sealed class TestSystemInterface : SystemInterface
    {
        public override double GetElapsedTime()
        {
            return 42.5;
        }
    }
}
