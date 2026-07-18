using RedBolt;

namespace RedBolt.Tests;

public sealed class TextureClassKindTests
{
    [Fact]
    public void Texture_base_chain_matches_cpp_concrete_noncopyable_reference_classes()
    {
        AssertConcreteNoncopyableReferenceClass<Resource>();
        AssertConcreteNoncopyableReferenceClass<ResourceWithMetadata>();
        AssertConcreteNoncopyableReferenceClass<Texture>();

        using var resource = new Resource(string.Empty);
        using var resourceWithMetadata = new ResourceWithMetadata(string.Empty);
        using var texture = new Texture(string.Empty);
    }

    private static void AssertConcreteNoncopyableReferenceClass<T>()
    {
        var type = typeof(T);
        Assert.True(type.IsClass);
        Assert.False(type.IsAbstract);
        Assert.False(type.IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(type));
        Assert.NotNull(type.GetConstructor([typeof(string)]));
    }
}
