using RedBolt;
using RedBolt.Core;
using RedBolt.Resource;

namespace RedBolt.Tests;

public sealed class GraphPinTests
{
    [Fact]
    public void Graph_data_pin_tracks_type_and_serialization_defaults()
    {
        var pin = new GraphDataPin();

        Assert.Equal(string.Empty, pin.GetName());
        Assert.Equal(VariantType.None, pin.GetType());

        pin.SetType(VariantType.Float);

        Assert.Equal(VariantType.Float, pin.GetType());
    }

    [Fact]
    public void Graph_input_pin_value_disconnects_target()
    {
        using var pin = new GraphInPin();
        pin.SetValue(new Variant(42));

        Assert.False(pin.IsConnected());
        Assert.Equal(42, pin.GetValue().GetInt());
    }

    [Fact]
    public void Graph_input_pin_owns_an_independent_variant_value()
    {
        using var values = new VariantVector();
        values.Add(new Variant(42));
        using var source = new Variant(values);
        using var pin = new GraphInPin();

        pin.SetValue(source);
        source.GetVariantVector().Clear();

        Assert.Equal(42, pin.GetValue().GetVariantVector()[0].GetInt());

        var previousValue = pin.GetValue().GetVariantVector();
        pin.SetValue(new Variant(7));

        Assert.Empty(previousValue);
        Assert.Equal(7, pin.GetValue().GetInt());
    }

    [Fact]
    public void Graph_exit_pin_disconnect_clears_target()
    {
        var pin = new GraphExitPin();

        pin.Disconnect();

        Assert.False(pin.IsConnected());
    }
}
