namespace RedBolt.Tests;

public sealed class GraphTests
{
    [Fact]
    public void Graph_assigns_ids_and_tracks_nodes()
    {
        var graph = new Graph();
        var first = graph.Create("First");
        var second = graph.Create("Second");
        var ids = new List<uint>();

        graph.GetNodeIds(ids);

        Assert.Equal(2u, graph.GetNumNodes());
        Assert.Equal(1u, first.GetID());
        Assert.Equal(2u, second.GetID());
        Assert.Same(first, graph.GetNode(first.GetID()));
        Assert.Contains(first.GetID(), ids);
        Assert.Contains(second.GetID(), ids);
    }

    [Fact]
    public void Graph_resolves_connected_data_and_execution_pins()
    {
        var graph = new Graph();
        var source = graph.Create("Source");
        var target = graph.Create("Target");
        var output = source.GetOrAddOutput("value");
        var input = target.GetOrAddInput("value");
        var enter = target.GetOrAddEnter("enter");
        var exit = source.GetOrAddExit("exit");

        input.GetPin()!.ConnectTo(output);
        exit.GetPin()!.ConnectTo(enter);

        var connectedOutput = input.GetConnectedPin<GraphOutPin>();
        var connectedEnter = exit.GetConnectedPin<GraphEnterPin>();

        Assert.Same(output.GetPin(), connectedOutput.GetPin());
        Assert.Same(enter.GetPin(), connectedEnter.GetPin());
    }

    [Fact]
    public void Graph_remove_and_clear_detach_nodes()
    {
        var graph = new Graph();
        var node = graph.Create("Node");

        graph.Remove(node);

        Assert.Equal(0u, graph.GetNumNodes());
        Assert.Null(node.GetGraph());
        Assert.Equal(1u, node.GetID());

        graph.Add(node);
        graph.Clear();

        Assert.Equal(0u, graph.GetNumNodes());
        Assert.Null(node.GetGraph());
    }
}
