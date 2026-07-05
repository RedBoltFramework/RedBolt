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
    public void Engine_uses_context_registered_subsystems()
    {
        Assert.Same(fixture.Engine.Context, Context.Instance);
        Assert.Same(fixture.Engine, fixture.Engine.Context.GetSubsystem<Engine>());
        Assert.Same(fixture.Engine.Time, fixture.Engine.Context.GetSubsystem<EngineTime>());
        Assert.Same(fixture.Engine.Resources, fixture.Engine.Context.GetSubsystem<ResourceCache>());
        Assert.Same(fixture.Engine.Input, fixture.Engine.Context.GetSubsystem<Input>());
        Assert.Same(fixture.Engine.Graphics, fixture.Engine.Context.GetSubsystem<Graphics>());
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
    public void Context_subscription_dispose_stops_future_event_delivery()
    {
        var previousContext = Context.TryGetInstance();
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var sender = new TestObject();
            var receiver = new TestObject();
            var calls = 0;

            var subscription = receiver.SubscribeToEvent(new StringHash("DisposeMe"), (_, eventType, eventData) =>
            {
                ++calls;
                Assert.Equal(new StringHash("DisposeMe"), eventType);
                Assert.Empty(eventData);
            });

            sender.SendEvent(new StringHash("DisposeMe"));
            subscription.Dispose();
            sender.SendEvent(new StringHash("DisposeMe"));

            Assert.Equal(1, calls);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_unsubscribe_from_specific_event_keeps_other_subscriptions()
    {
        var previousContext = Context.TryGetInstance();
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var sender = new TestObject();
            var receiver = new TestObject();
            var pingCalls = 0;
            var pongCalls = 0;

            receiver.SubscribeToEvent("Ping", (_, _, _) => ++pingCalls);
            receiver.SubscribeToEvent("Pong", (_, _, _) => ++pongCalls);

            receiver.UnsubscribeFromEvent("Ping");

            sender.SendEvent("Ping");
            sender.SendEvent("Pong");

            Assert.Equal(0, pingCalls);
            Assert.Equal(1, pongCalls);
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
