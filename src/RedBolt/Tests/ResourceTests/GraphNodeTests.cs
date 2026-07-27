using System.Runtime.CompilerServices;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Resource;
namespace RedBolt.Tests;

public sealed class GraphNodeTests
{
    [Fact]
    public void Graph_node_tracks_name_hash_and_properties()
    {
        using var node = new GraphNode();

        node.SetName("Multiply");
        Assert.Equal("Multiply", node.GetName());
        Assert.Equal(new StringHash("Multiply"), node.GetNameHash());

        var value = new Variant(42);
        Assert.Same(node, node.WithProperty("value", value));
        ref var property = ref node.GetProperty("value");
        Assert.False(Unsafe.IsNullRef(ref property));
        Assert.Equal(42, property.GetInt());
        Assert.Single(node.GetProperties());
    }

    [Fact]
    public void Graph_node_owns_independent_property_values()
    {
        using var values = new VariantVector();
        values.Add(new Variant(42));
        using var source = new Variant(values);
        using var node = new GraphNode();

        node.WithProperty("value", source);
        source.GetVariantVector().Clear();

        ref var property = ref node.GetProperty("value");
        Assert.Equal(42, property.GetVariantVector()[0].GetInt());

        var previousValue = property.GetVariantVector();
        node.WithProperty("value", new Variant(7));

        Assert.Empty(previousValue);
        Assert.Equal(7, property.GetInt());

        ref var missing = ref node.GetProperty("missing");
        Assert.True(Unsafe.IsNullRef(ref missing));
    }

    [Fact]
    public void Graph_node_adds_and_finds_all_pin_kinds()
    {
        using var node = new GraphNode();

        Assert.Same(node, node.WithInput("input", VariantType.Float));
        Assert.Same(node, node.WithOutput("output", VariantType.Int));
        node.GetOrAddEnter("enter");
        node.GetOrAddExit("exit");

        var input = node.GetInput("input");
        var output = node.GetOutput(0);
        Assert.NotNull(input.GetPin());
        Assert.NotNull(output.GetPin());
        Assert.Equal(VariantType.Float, input.GetPin()!.GetType());
        Assert.Equal(VariantType.Int, output.GetPin()!.GetType());
        Assert.Equal(1, node.GetNumInputs());
        Assert.Equal(1, node.GetNumOutputs());
        Assert.Equal(1, node.GetNumEnters());
        Assert.Equal(1, node.GetNumExits());
    }

    [Fact]
    public void Graph_node_out_of_range_pin_is_empty()
    {
        using var node = new GraphNode();

        Assert.Null(node.GetInput(0).GetPin());
        Assert.Null(node.GetInput(-1).GetPin());
        Assert.Null(node.GetOutput(0).GetPin());
        Assert.Null(node.GetOutput(-1).GetPin());
        Assert.Null(node.GetEnter(0).GetPin());
        Assert.Null(node.GetEnter(-1).GetPin());
        Assert.Null(node.GetExit(0).GetPin());
        Assert.Null(node.GetExit(-1).GetPin());
    }

    [Fact]
    public void Graph_node_disposes_owned_input_values()
    {
        using var values = new VariantVector();
        values.Add(new Variant(42));
        using var source = new Variant(values);
        var node = new GraphNode();

        node.WithInput("input", source);
        var ownedValue = node.GetInput("input").GetPin()!.GetValue().GetVariantVector();

        node.Dispose();

        Assert.Empty(ownedValue);
    }
}
