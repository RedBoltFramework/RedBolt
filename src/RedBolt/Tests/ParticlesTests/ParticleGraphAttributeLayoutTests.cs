namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Particles;

public class ParticleGraphAttributeLayoutTests
{
    [Fact]
    public void ResetClearsAttributesAndStartsAtOffset()
    {
        var layout = new ParticleGraphAttributeLayout();
        layout.Reset(24, 3);
        layout.GetOrAddAttribute("age", VariantType.Float);

        layout.Reset(12, 2);

        Assert.Equal(0, layout.NumAttributes);
        Assert.Equal(12, layout.RequiredMemory);
    }

    [Fact]
    public void GetOrAddAttributeAllocatesContiguousTypedSpans()
    {
        var layout = new ParticleGraphAttributeLayout();
        layout.Reset(24, 3);

        var age = layout.GetOrAddAttribute("age", VariantType.Float);
        var position = layout.GetOrAddAttribute("position", VariantType.Vector3);

        Assert.Equal(0, age);
        Assert.Equal(1, position);
        Assert.Equal("age", layout.GetName(age));
        Assert.Equal(VariantType.Float, layout.GetType(age));
        Assert.Equal(2, layout.NumAttributes);
        Assert.Equal(24, layout.GetSpan(age).Offset);
        Assert.Equal(sizeof(float) * 3, layout.GetSpan(age).Size);
        Assert.Equal(36, layout.GetSpan(position).Offset);
        Assert.Equal(12 * 3, layout.GetSpan(position).Size);
        Assert.Equal(72, layout.RequiredMemory);
    }

    [Fact]
    public void GetOrAddAttributeReusesMatchingNameAndType()
    {
        var layout = new ParticleGraphAttributeLayout();
        layout.Reset(0, 4);

        var first = layout.GetOrAddAttribute("value", VariantType.Int);
        var same = layout.GetOrAddAttribute("value", VariantType.Int);
        var differentType = layout.GetOrAddAttribute("value", VariantType.Float);

        Assert.Equal(first, same);
        Assert.NotEqual(first, differentType);
        Assert.Equal(2, layout.NumAttributes);
        Assert.Equal(sizeof(int) * 8, layout.RequiredMemory);
    }
}
