namespace RedBolt.Tests;

public sealed class ObjectTests
{
    [Fact]
    public void Object_forwards_dynamic_subsystem_access_to_context()
    {
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            var subsystem = new TestSubsystem();
            var type = new StringHash("TestSubsystem");
            context.RegisterSubsystem(subsystem, type);
            var receiver = new TestObject();

            Assert.Same(subsystem, receiver.GetSubsystem(type));
            Assert.Same(subsystem, receiver.GetSubsystems().Get(type));
        }
        finally
        {
            Context.SetInstance(null);
        }
    }

    private sealed class TestObject : RedBolt.Object
    {
    }

    private sealed class TestSubsystem
    {
    }
}
