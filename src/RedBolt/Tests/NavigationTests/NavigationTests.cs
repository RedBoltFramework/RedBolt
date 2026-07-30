namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Navigation;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class NavigationTests
{
    private sealed class EventReceiver : RedObject
    {
    }

    private readonly HeadlessEngineFixture _fixture;

    public NavigationTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Navigation_mesh_builds_and_finds_path()
    {
        using var scene = CreateNavigationScene();
        var navMesh = scene.GetComponent<NavigationMesh>()!;

        Assert.True(navMesh.Rebuild());
        Assert.NotEmpty(navMesh.GetAllTileIndices());

        var path = new List<Vector3f>();
        navMesh.FindPath(path, new Vector3f(-5.0f, 0.0f, 0.0f), new Vector3f(5.0f, 0.0f, 0.0f));

        Assert.NotEmpty(path);
        Assert.True((path[^1] - new Vector3f(5.0f, 0.0f, 0.0f)).Length < 1.0f);
    }

    [Fact]
    public void Baked_lighting_navigation_skips_empty_boundary_tiles()
    {
        var log = _fixture.Engine.GetSubsystem<Log>()!;
        using var receiver = new EventReceiver();
        var errors = new List<string>();
        receiver.SubscribeToEvent<IOEvents.LogMessage>(log, (ref IOEvents.LogMessage eventData) =>
        {
            if (eventData.Level == (int)LogLevel.Error)
                errors.Add(eventData.Message);
        });
        using var scene = new Scene();
        using var root = _fixture.Resources.Get<XMLFile>("Scenes/BakedLightingExample.xml").GetRoot();
        scene.LoadXML(root);

        Assert.True(scene.FindComponent<NavigationMesh>()!.Rebuild());
        Assert.Empty(errors);
    }

    [Fact]
    public void Crowd_agent_moves_and_invokes_callbacks()
    {
        using var scene = CreateNavigationScene();
        var navMesh = scene.GetComponent<NavigationMesh>()!;
        Assert.True(navMesh.Rebuild());

        var crowdManager = scene.CreateComponent<CrowdManager>();
        var velocityUpdates = 0;
        var positionUpdates = 0;
        crowdManager.VelocityCallback = (CrowdAgent _, float _, ref Vector3f _, ref float _) => ++velocityUpdates;
        crowdManager.PositionCallback = (CrowdAgent _, float _, ref Vector3f _) => ++positionUpdates;

        var agentNode = scene.CreateChild("Agent");
        agentNode.SetPosition(new Vector3f(-5.0f, 0.0f, 0.0f));
        var agent = agentNode.CreateComponent<CrowdAgent>();
        agent.Radius = 0.6f;
        agent.Height = 2.0f;
        agent.MaxSpeed = 3.0f;
        agent.MaxAccel = 5.0f;
        agent.TargetPosition = new Vector3f(5.0f, 0.0f, 0.0f);

        for (var i = 0; i < 120; ++i)
            scene.Update(1.0f / 60.0f);

        Assert.True(agent.Position.X > -4.5f);
        Assert.True(velocityUpdates > 0);
        Assert.True(positionUpdates > 0);
    }

    [Fact]
    public void Crowd_manager_reflection_contains_cpp_attributes()
    {
        var attributes = Context.Instance.GetReflection(ObjectReflection<CrowdManager>.TypeId)!.Attributes!;
        Assert.Contains(attributes, attribute => attribute.Name == "Navigation Mesh");
        Assert.Contains(attributes, attribute => attribute.Name == "Filter Types");
        Assert.Contains(attributes, attribute => attribute.Name == "Obstacle Avoidance Types");
    }

    private Scene CreateNavigationScene()
    {
        var scene = new Scene();
        var planeNode = scene.CreateChild("Plane");
        planeNode.SetScale(new Vector3f(20.0f, 1.0f, 20.0f));
        var model = planeNode.CreateComponent<StaticModel>();
        model.SetModel(_fixture.Resources.Get<Model>("Models/Plane.mdl"));
        var navMesh = scene.CreateComponent<NavigationMesh>();
        navMesh.TileSize = 30;
        scene.CreateComponent<Navigable>();
        return scene;
    }
}
