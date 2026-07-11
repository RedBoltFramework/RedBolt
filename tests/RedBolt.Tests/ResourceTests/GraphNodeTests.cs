namespace RedBolt.Tests;

public sealed class GraphNodeTests
{
    [Fact]
    public void Graph_node_tracks_name_hash_and_properties()
    {
        var node = new GraphNode();

        node.SetName("Multiply");
        Assert.Equal("Multiply", node.GetName());
        Assert.Equal(new StringHash("Multiply"), node.GetNameHash());

        var value = new Variant(42);
        Assert.Same(node, node.WithProperty("value", value));
        Assert.Equal(42, node.GetProperty("value")!.Value.GetInt());
        Assert.Single(node.GetProperties());
    }

    [Fact]
    public void Graph_node_adds_and_finds_all_pin_kinds()
    {
        var node = new GraphNode();

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
        Assert.Equal(1u, node.GetNumInputs());
        Assert.Equal(1u, node.GetNumOutputs());
        Assert.Equal(1u, node.GetNumEnters());
        Assert.Equal(1u, node.GetNumExits());
    }

    [Fact]
    public void Graph_node_out_of_range_pin_is_empty()
    {
        var node = new GraphNode();

        Assert.Null(node.GetInput(0).GetPin());
        Assert.Null(node.GetOutput(0).GetPin());
        Assert.Null(node.GetEnter(0).GetPin());
        Assert.Null(node.GetExit(0).GetPin());
    }
}
