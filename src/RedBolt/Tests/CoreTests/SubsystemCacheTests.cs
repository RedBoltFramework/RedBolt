using RedBolt.Container;
using RedBolt.Core;

namespace RedBolt.Tests;

public class SubsystemCacheTests
{
    [Fact]
    public void Cache_uses_fully_qualified_csharp_type_name()
    {
        Assert.Equal("TestReflectionObject", typeof(TestReflectionObject).GetClassName());
        Assert.Equal(new StringHash("TestReflectionObject"), new StringHash(typeof(TestReflectionObject).GetClassName()));
    }

    [Fact]
    public void Cache_stores_cached_and_dynamic_subsystems_and_supports_removal_and_clear()
    {
        var cache = new SubsystemCache();
        var time = new Time();
        var dynamicSubsystem = new TestDynamicSubsystem();

        cache.Add(typeof(Time).GetClassName(), time);
        cache.Add(typeof(TestDynamicSubsystem).GetClassName(), dynamicSubsystem);

        Assert.Same(time, cache.Get<Time>());
        Assert.Same(dynamicSubsystem, cache.Get<TestDynamicSubsystem>());
        Assert.Equal(2, cache.GetContainer().Count);

        cache.Remove(typeof(Time).GetClassName());

        Assert.Null(cache.Get<Time>());
        Assert.Same(dynamicSubsystem, cache.Get<TestDynamicSubsystem>());
        Assert.Single(cache.GetContainer());

        cache.Clear();

        Assert.Null(cache.Get<TestDynamicSubsystem>());
        Assert.Empty(cache.GetContainer());
    }

    [Fact]
    public void Context_matches_rbfx_explicit_subsystem_type_contract()
    {
        var context = new Context();
        var subsystem = new TestDynamicSubsystem();
        var type = new StringHash("ExplicitSubsystem");

        context.RegisterSubsystem(subsystem, type);

        Assert.Same(subsystem, context.GetSubsystem(type));
        Assert.Same(subsystem, context.GetSubsystems().Get(type));

        context.RemoveSubsystem(type);

        Assert.Null(context.GetSubsystem(type));
    }

    private sealed class TestReflectionObject : RedObject
    {
    }

    private sealed class TestDynamicSubsystem
    {
    }
}
