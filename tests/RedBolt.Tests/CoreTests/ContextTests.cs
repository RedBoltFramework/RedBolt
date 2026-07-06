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
        Assert.Same(fixture.Time, fixture.Engine.Context.GetSubsystem<EngineTime>());
        Assert.Same(fixture.Resources, fixture.Engine.Context.GetSubsystem<ResourceCache>());
        Assert.Same(fixture.Input, fixture.Engine.Context.GetSubsystem<Input>());
        Assert.Same(fixture.Graphics, fixture.Engine.Context.GetSubsystem<Graphics>());
        Assert.Same(fixture.Renderer, fixture.Engine.Context.GetSubsystem<Renderer>());
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
            Assert.True(receivedData![new StringHash("Flag")].GetBool());
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
    public void Context_unsubscribe_from_sender_keeps_other_senders()
    {
        var previousContext = Context.TryGetInstance();
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var senderA = new TestObject();
            var senderB = new TestObject();
            var receiver = new TestObject();
            var calls = 0;

            receiver.SubscribeToEvent(senderA, "Ping", (_, _, _) => ++calls);
            receiver.SubscribeToEvent(senderB, "Ping", (_, _, _) => ++calls);

            receiver.UnsubscribeFromEvents(senderA);

            senderA.SendEvent("Ping");
            senderB.SendEvent("Ping");

            Assert.Equal(1, calls);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_supports_unsubscribe_during_dispatch()
    {
        var previousContext = Context.TryGetInstance();
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var sender = new TestObject();
            var firstReceiver = new TestObject();
            var secondReceiver = new TestObject();
            var firstCalls = 0;
            var secondCalls = 0;

            firstReceiver.SubscribeToEvent("Ping", (_, _, _) =>
            {
                ++firstCalls;
                secondReceiver.UnsubscribeFromAllEvents();
            });
            secondReceiver.SubscribeToEvent("Ping", (_, _, _) => ++secondCalls);

            sender.SendEvent("Ping");
            sender.SendEvent("Ping");

            Assert.Equal(2, firstCalls);
            Assert.Equal(0, secondCalls);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_respects_block_events_for_sender_and_receiver()
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

            sender.SetBlockEvents(true);
            sender.SendEvent("Ping");

            sender.SetBlockEvents(false);
            receiver.SetBlockEvents(true);
            sender.SendEvent("Ping");

            receiver.SetBlockEvents(false);
            sender.SendEvent("Ping");

            Assert.Equal(1, calls);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_exposes_global_vars_through_context_and_object()
    {
        var previousContext = Context.TryGetInstance();
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var key = new StringHash("Renderer.Debug");
            var sender = new TestObject();

            context.SetGlobalVar(key, 42);

            Assert.Equal(42, sender.GetGlobalVar(key).GetInt());
            Assert.Equal(42, context.GetGlobalVars()[key].GetInt());

            sender.SetGlobalVar(key, 64);

            Assert.Equal(64, context.GetGlobalVar(key).GetInt());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_reports_event_handler_queries()
    {
        var previousContext = Context.TryGetInstance();
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var sender = new TestObject();
            var receiver = new TestObject();

            receiver.SubscribeToEvent("Ping", (_, _, _) => { });
            receiver.SubscribeToEvent(sender, "Pong", (_, _, _) => { });

            Assert.True(receiver.HasSubscribedToEvent(new StringHash("Ping")));
            Assert.True(receiver.HasSubscribedToEvent(sender, new StringHash("Pong")));
            Assert.True(receiver.HasEventHandlers());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_can_unsubscribe_from_all_events_except_selected_event_types_and_senders()
    {
        var previousContext = Context.TryGetInstance();
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var senderA = new TestObject();
            var senderB = new TestObject();
            var receiver = new TestObject();

            receiver.SubscribeToEvent("KeepEvent", (_, _, _) => { });
            receiver.SubscribeToEvent("DropEvent", (_, _, _) => { });
            receiver.UnsubscribeFromAllEventsExcept([new StringHash("KeepEvent")]);

            Assert.True(receiver.HasSubscribedToEvent(new StringHash("KeepEvent")));
            Assert.False(receiver.HasSubscribedToEvent(new StringHash("DropEvent")));

            receiver.SubscribeToEvent(senderA, "Ping", (_, _, _) => { });
            receiver.SubscribeToEvent(senderB, "Ping", (_, _, _) => { });
            receiver.UnsubscribeFromAllEventsExcept([senderB]);

            Assert.False(receiver.HasSubscribedToEvent(senderA, new StringHash("Ping")));
            Assert.True(receiver.HasSubscribedToEvent(senderB, new StringHash("Ping")));
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
        try
        {
            engine.Initialize(new EngineParameters
            {
                ApplicationName = "ContextLifecycle",
                Headless = true
            });

            Assert.Same(engine.Context, Context.Instance);

            engine.Shutdown();

            Assert.Null(Context.TryGetInstance());
        }
        finally
        {
            Context.SetInstance(fixture.Engine.Context);
        }
    }

    private sealed class TestObject : RedBolt.Object
    {
    }
}
