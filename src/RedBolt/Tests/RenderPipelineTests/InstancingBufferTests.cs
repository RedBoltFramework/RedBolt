namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;

public sealed class InstancingBufferTests
{
    [Fact]
    public void Disabled_settings_leave_buffer_uninitialized()
    {
        using var buffer = new InstancingBuffer();

        Assert.False(buffer.IsEnabled);
        Assert.Null(buffer.VertexBuffer);
    }

    [Fact]
    public void Settings_are_pod_values_with_native_defaults_copy_and_hash()
    {
        Assert.True(typeof(InstancingBufferSettings).IsValueType);
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers
            .IsReferenceOrContainsReferences<InstancingBufferSettings>());
        Assert.False(typeof(InstancingBuffer).IsSealed);

        var settings = new InstancingBufferSettings
        {
            EnableInstancing = true,
            FirstInstancingTexCoord = 4,
            NumInstancingTexCoords = 10
        };
        var copy = settings;
        copy.NumInstancingTexCoords = 3;

        var hash = 0u;
        hash ^= 1u + 0x9e3779b9u + (hash << 6) + (hash >> 2);
        hash ^= 4u + 0x9e3779b9u + (hash << 6) + (hash >> 2);
        hash ^= 10u + 0x9e3779b9u + (hash << 6) + (hash >> 2);
        hash ^= 1u + 0x9e3779b9u + (hash << 6) + (hash >> 2);

        Assert.Equal(1, settings.StepRate);
        Assert.Equal(10, settings.NumInstancingTexCoords);
        Assert.Equal(3, copy.NumInstancingTexCoords);
        Assert.NotEqual(settings, copy);
        Assert.Equal(hash, settings.CalculatePipelineStateHash());
    }

    [Fact]
    public void Settings_assignment_copies_value_and_replacement_releases_buffer()
    {
        using var buffer = new InstancingBuffer();
        var settings = new InstancingBufferSettings
        {
            EnableInstancing = true,
            NumInstancingTexCoords = 1
        };
        ref readonly var borrowedSettings = ref buffer.Settings;

        buffer.SetSettings(settings);
        settings.EnableInstancing = false;

        Assert.True(buffer.Settings.EnableInstancing);
        Assert.True(borrowedSettings.EnableInstancing);
        Assert.NotNull(buffer.VertexBuffer);

        buffer.SetSettings(new InstancingBufferSettings());

        Assert.False(buffer.IsEnabled);
        Assert.Null(buffer.VertexBuffer);
    }

    [Fact]
    public void Enabled_settings_create_cpp_instancing_elements()
    {
        using var buffer = new InstancingBuffer();
        buffer.SetSettings(new InstancingBufferSettings
        {
            EnableInstancing = true,
            FirstInstancingTexCoord = 4,
            NumInstancingTexCoords = 3,
            StepRate = 2
        });

        var vertexBuffer = Assert.IsType<VertexBuffer>(buffer.VertexBuffer);
        Assert.True(buffer.IsEnabled);
        Assert.Equal(3, vertexBuffer.GetElements().Count);
        Assert.Collection(vertexBuffer.GetElements(),
            element => AssertElement(element, 4),
            element => AssertElement(element, 5),
            element => AssertElement(element, 6));
    }

    [Fact]
    public void Composition_tracks_indices_and_copies_elements()
    {
        using var buffer = new InstancingBuffer();
        buffer.SetSettings(new InstancingBufferSettings
        {
            EnableInstancing = true,
            FirstInstancingTexCoord = 4,
            NumInstancingTexCoords = 2
        });

        buffer.Begin();
        Assert.Equal(0, buffer.NextInstanceIndex);
        Assert.Equal(0, buffer.AddInstance());
        buffer.SetElements(new byte[checked((int)(2 * InstancingBuffer.ElementStride))], 0, 2);
        Assert.Equal(1, buffer.NextInstanceIndex);
        Assert.Equal(1, buffer.AddInstance());
        Assert.Equal(2, buffer.NextInstanceIndex);

        buffer.Begin();
        Assert.Equal(0, buffer.NextInstanceIndex);
    }

    private static void AssertElement(VertexElement element, byte index)
    {
        Assert.Equal(VertexElementType.Vector4, element.Type);
        Assert.Equal(VertexElementSemantic.TexCoord, element.Semantic);
        Assert.Equal(index, element.Index);
        Assert.Equal(2u, element.StepRate);
    }
}
