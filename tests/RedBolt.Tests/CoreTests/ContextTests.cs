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
        Assert.Same(fixture.RenderDevice, fixture.Engine.Context.GetSubsystem<RenderDevice>());
        Assert.Same(fixture.RenderContext, fixture.RenderDevice.GetRenderContext());
        Assert.Same(fixture.PipelineStateCache, fixture.Engine.Context.GetSubsystem<PipelineStateCache>());
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

            using var subscriptionA = receiver.SubscribeToEvent<NamedEvent>((ref NamedEvent eventData) =>
            {
                ++anySenderCalls;
                Assert.True(eventData.Flag);
                Assert.Same(senderA, eventData.Sender);
            });
            using var subscriptionB = receiver.SubscribeToEvent<NamedEvent>(senderB, (ref NamedEvent eventData) =>
            {
                Assert.Same(senderB, eventData.Sender);
                ++filteredSenderCalls;
            });

            var namedEvent = new NamedEvent { Sender = senderA, Flag = true };
            senderA.SendEvent(ref namedEvent);

            Assert.Equal(1, anySenderCalls);
            Assert.Equal(0, filteredSenderCalls);
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

            receiver.SubscribeToEvent<PingEvent>((ref PingEvent _) => ++calls);
            receiver.UnsubscribeFromAllEvents();
            sender.SendEvent<PingEvent>();

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

            var subscription = receiver.SubscribeToEvent<DisposeMeEvent>((ref DisposeMeEvent _) =>
            {
                ++calls;
            });

            sender.SendEvent<DisposeMeEvent>();
            subscription.Dispose();
            sender.SendEvent<DisposeMeEvent>();

            Assert.Equal(1, calls);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_garbage_collected_receivers_are_automatically_dropped()
    {
        var previousContext = Context.TryGetInstance();
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var sender = new TestObject();
            WeakReference<TestObject> receiverRef;
            var calls = 0;

            void SubscribeAndDrop()
            {
                var receiver = new TestObject();
                receiverRef = new WeakReference<TestObject>(receiver);
                receiver.SubscribeToEvent<PingEvent>((ref PingEvent _) => ++calls);
            }

            SubscribeAndDrop();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(receiverRef.TryGetTarget(out _));

            sender.SendEvent<PingEvent>();
            Assert.Equal(0, calls);
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

            receiver.SubscribeToEvent<PongEvent>((ref PongEvent _) => ++pongCalls);

            using var pingSubscription = receiver.SubscribeToEvent<PingEvent>((ref PingEvent _) => ++pingCalls);
            pingSubscription.Dispose();

            sender.SendEvent<PingEvent>();
            sender.SendEvent<PongEvent>();

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

            receiver.SubscribeToEvent(senderA, (ref PingEvent _) => ++calls);
            receiver.SubscribeToEvent(senderB, (ref PingEvent _) => ++calls);

            receiver.UnsubscribeFromEvents(senderA);

            senderA.SendEvent<PingEvent>();
            senderB.SendEvent<PingEvent>();

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

            firstReceiver.SubscribeToEvent<PingEvent>((ref PingEvent _) =>
            {
                ++firstCalls;
                secondReceiver.UnsubscribeFromAllEvents();
            });
            secondReceiver.SubscribeToEvent<PingEvent>((ref PingEvent _) => ++secondCalls);

            sender.SendEvent<PingEvent>();
            sender.SendEvent<PingEvent>();

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

            receiver.SubscribeToEvent<PingEvent>((ref PingEvent _) => ++calls);

            sender.SetBlockEvents(true);
            sender.SendEvent<PingEvent>();

            sender.SetBlockEvents(false);
            receiver.SetBlockEvents(true);
            sender.SendEvent<PingEvent>();

            receiver.SetBlockEvents(false);
            sender.SendEvent<PingEvent>();

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

            receiver.SubscribeToEvent<PingEvent>((ref PingEvent _) => { });
            receiver.SubscribeToEvent(sender, (ref PongEvent _) => { });

            Assert.True(receiver.HasSubscribedToEvent<PingEvent>());
            Assert.True(receiver.HasSubscribedToEvent<PongEvent>(sender));
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

            receiver.SubscribeToEvent<KeepEvent>((ref KeepEvent _) => { });
            var dropEventSubscription = receiver.SubscribeToEvent<DropEvent>(static (ref DropEvent _) => { });
            dropEventSubscription.Dispose();

            Assert.True(receiver.HasSubscribedToEvent<KeepEvent>());
            Assert.False(receiver.HasSubscribedToEvent<DropEvent>());

            receiver.SubscribeToEvent<PingEvent>(senderA, (ref PingEvent _) => { });
            receiver.SubscribeToEvent<PingEvent>(senderB, (ref PingEvent _) => { });
            receiver.UnsubscribeFromAllEventsExcept([senderB]);

            Assert.False(receiver.HasSubscribedToEvent<PingEvent>(senderA));
            Assert.True(receiver.HasSubscribedToEvent<PingEvent>(senderB));
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Engine_shutdown_clears_global_context_instance()
    {
        var previousContext = Context.TryGetInstance();
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

            Assert.Same(previousContext, Context.TryGetInstance());
        }
        finally
        {
            Context.SetInstance(fixture.Engine.Context);
        }
    }

    private sealed class TestObject : RedBolt.Object
    {
    }

    private struct NamedEvent
    {
        public required object? Sender { get; init; }

        public bool Flag { get; init; }
    }

    private struct PingEvent;

    private struct PongEvent;

    private struct DisposeMeEvent;

    private struct KeepEvent;

    private struct DropEvent;
}
