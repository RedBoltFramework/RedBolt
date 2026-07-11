using RedBolt;

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
        var pin = new GraphInPin();
        pin.SetValue(new Variant(42));

        Assert.False(pin.IsConnected());
        Assert.Equal(42, pin.GetValue().GetInt());
    }

    [Fact]
    public void Graph_exit_pin_disconnect_clears_target()
    {
        var pin = new GraphExitPin();

        pin.Disconnect();

        Assert.False(pin.IsConnected());
    }
}
