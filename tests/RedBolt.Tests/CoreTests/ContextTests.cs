using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ContextTests
{
    private readonly HeadlessEngineFixture fixture;

    public ContextTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Engine_registers_global_context_and_subsystems()
    {
        Assert.Same(fixture.Engine.Context, Context.Instance);
        Assert.Same(fixture.Engine, fixture.Engine.Context.GetSubsystem<Engine>());
        Assert.Same(fixture.Engine.Resources, fixture.Engine.Context.GetSubsystem<ResourceCache>());
        Assert.Same(fixture.Engine.Renderer, fixture.Engine.Context.GetSubsystem<Renderer>());
    }

    [Fact]
    public void Context_delivers_named_events_and_sender_filters()
    {
        var previousContext = Context.TryGetInstance();
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var senderA = new TestObject();
            var senderB = new TestObject();
            var receiver = new TestObject();

            var anySenderCalls = 0;
            var filteredSenderCalls = 0;
            VariantMap? receivedData = null;

            using var subscriptionA = receiver.SubscribeToEvent("NamedEvent", (sender, eventType, eventData) =>
            {
                ++anySenderCalls;
                Assert.Equal(new StringHash("NamedEvent"), eventType);
                receivedData = eventData;
                Assert.Same(senderA, sender);
            });
            using var subscriptionB = receiver.SubscribeToEvent(senderB, "NamedEvent", (_, _, _) => ++filteredSenderCalls);

            senderA.SendEvent("NamedEvent", new VariantMap
            {
                ["Flag"] = true
            });

            Assert.Equal(1, anySenderCalls);
            Assert.Equal(0, filteredSenderCalls);
            Assert.NotNull(receivedData);
            Assert.True(Assert.IsType<bool>(receivedData![new StringHash("Flag")]));
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_unsubscribe_removes_receiver_handlers()
    {
        var previousContext = Context.TryGetInstance();
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var sender = new TestObject();
            var receiver = new TestObject();
            var calls = 0;

            receiver.SubscribeToEvent("Ping", (_, _, _) => ++calls);
            receiver.UnsubscribeFromAllEvents();
            sender.SendEvent("Ping");

            Assert.Equal(0, calls);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Engine_shutdown_clears_global_context_instance()
    {
        var engine = new Engine();
        engine.Initialize(new EngineParameters
        {
            ApplicationName = "ContextLifecycle",
            Headless = true
        });

        Assert.Same(engine.Context, Context.Instance);

        engine.Shutdown();

        Assert.Null(Context.TryGetInstance());
    }

    private sealed class TestObject : RedBolt.Object
    {
    }
}
