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
        AssertConcreteNoncopyableReferenceClass<Texture2D>();
        AssertConcreteNoncopyableReferenceClass<Texture2DArray>();

        using var resource = new Resource(string.Empty);
        using var resourceWithMetadata = new ResourceWithMetadata(string.Empty);
        using var texture = new Texture(string.Empty);
        using var texture2D = new Texture2D(string.Empty);
        using var texture2DArray = new Texture2DArray(string.Empty);
    }

    [Fact]
    public void Texture2D_dispose_releases_derived_load_state()
    {
        var texture = new Texture2D(string.Empty);
        var image = new Image(string.Empty);
        using var parameters = new XMLFile(string.Empty);
        var loadImage = typeof(Texture2D).GetField("_loadImage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var loadParameters = typeof(Texture2D).GetField("_loadParameters",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        loadImage.SetValue(texture, image);
        loadParameters.SetValue(texture, parameters);

        texture.Dispose();

        Assert.Null(loadImage.GetValue(texture));
        Assert.Null(loadParameters.GetValue(texture));
    }

    [Fact]
    public void Texture2DArray_dispose_releases_derived_load_state()
    {
        var texture = new Texture2DArray(string.Empty);
        var loadImages = typeof(Texture2DArray).GetField("_loadImages",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var loadParameters = typeof(Texture2DArray).GetField("_loadParameters",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var images = Assert.IsType<List<Image?>>(loadImages.GetValue(texture));
        images.Add(new Image(string.Empty));
        images.Add(new Image(string.Empty));
        loadParameters.SetValue(texture, new XMLFile(string.Empty));

        texture.Dispose();

        Assert.Empty(images);
        Assert.Null(loadParameters.GetValue(texture));
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
