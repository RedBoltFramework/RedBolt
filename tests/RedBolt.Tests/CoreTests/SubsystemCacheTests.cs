namespace RedBolt.Tests;

public class SubsystemCacheTests
{
    [Fact]
    public void ObjectReflection_uses_fully_qualified_type_name()
    {
        Assert.Equal("RedBolt.Tests.SubsystemCacheTests.TestReflectionObject", ObjectReflection<TestReflectionObject>.ClassName);
        Assert.Equal(new StringHash("RedBolt.Tests.SubsystemCacheTests.TestReflectionObject"), ObjectReflection<TestReflectionObject>.TypeId);
    }

    [Fact]
    public void Cache_stores_cached_and_dynamic_subsystems_and_supports_removal_and_clear()
    {
        var cache = new SubsystemCache();
        var time = new Time();
        var dynamicSubsystem = new TestDynamicSubsystem();

        cache.Add(ObjectReflection<Time>.TypeId, time);
        cache.Add(ObjectReflection<TestDynamicSubsystem>.TypeId, dynamicSubsystem);

        Assert.Same(time, cache.Get<Time>());
        Assert.Same(dynamicSubsystem, cache.Get<TestDynamicSubsystem>());
        Assert.Equal(2, cache.GetContainer().Count);

        cache.Remove(ObjectReflection<Time>.TypeId);

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

    private sealed class TestReflectionObject : RedBolt.Object
    {
    }

    private sealed class TestDynamicSubsystem
    {
    }
}
