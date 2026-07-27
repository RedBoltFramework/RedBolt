using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using EventHandler = global::RedBolt.Core.EventHandler;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.RenderAPI;
using RedBolt.Resource;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ContextTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ContextTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Engine_uses_context_registered_subsystems()
    {
        Assert.Same(_fixture.Engine.Context, Context.Instance);
        Assert.Same(_fixture.Engine, _fixture.Engine.Context.GetSubsystem<EngineSystem>());
        Assert.Same(_fixture.Time, _fixture.Engine.Context.GetSubsystem<Time>());
        Assert.Same(_fixture.Resources, _fixture.Engine.Context.GetSubsystem<ResourceCache>());
        Assert.Same(_fixture.Input, _fixture.Engine.Context.GetSubsystem<InputSystem>());
        Assert.Same(_fixture.Graphics, _fixture.Engine.Context.GetSubsystem<GraphicsSystem>());
        Assert.Null(_fixture.Engine.Context.GetSubsystem<RenderDevice>());
        Assert.Same(_fixture.PipelineStateCache, _fixture.Engine.Context.GetSubsystem<PipelineStateCache>());
        Assert.Same(_fixture.Renderer, _fixture.Engine.Context.GetSubsystem<Renderer>());
    }

    [Fact]
    public void Interface_subsystem_lookup_does_not_allocate()
    {
        var context = new Context();
        var subsystem = new TestInterfaceSubsystem();
        context.RegisterSubsystem(subsystem);

        Assert.Same(subsystem, context.GetSubsystem<ITestInterfaceSubsystem>());

        for (var i = 0; i < 32; ++i)
            _ = context.GetSubsystem<ITestInterfaceSubsystem>();

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; ++i)
            _ = context.GetSubsystem<ITestInterfaceSubsystem>();
        allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBytes;

        Assert.Equal(0, allocatedBytes);
    }

    [Fact]
    public void Context_delivers_named_events_and_sender_filters()
    {
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var senderA = new TestObject();
            var senderB = new TestObject();
            var receiver = new TestObject();

            var anySenderCalls = 0;
            var filteredSenderCalls = 0;

            receiver.SubscribeToEvent<NamedEvent>((ref NamedEvent eventData) =>
            {
                ++anySenderCalls;
                Assert.True(eventData.Flag);
                Assert.Same(senderA, eventData.Sender);
            });
            receiver.SubscribeToEvent<NamedEvent>(senderB, (ref NamedEvent eventData) =>
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
    public void Context_gives_sender_specific_handler_priority_over_general_handler()
    {
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var sender = new TestObject();
            var receiver = new TestObject();
            var generalCalls = 0;
            var specificCalls = 0;
            receiver.SubscribeToEvent<PingEvent>((ref PingEvent _) => ++generalCalls);
            receiver.SubscribeToEvent<PingEvent>(sender, (ref PingEvent _) => ++specificCalls);

            sender.SendEvent<PingEvent>();

            Assert.Equal(0, generalCalls);
            Assert.Equal(1, specificCalls);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_unsubscribe_removes_receiver_handlers()
    {
        var previousContext = Context.Instance;
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
    public void Context_unsubscribe_stops_future_event_delivery()
    {
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var sender = new TestObject();
            var receiver = new TestObject();
            var calls = 0;

            receiver.SubscribeToEvent<DisposeMeEvent>((ref DisposeMeEvent _) =>
            {
                ++calls;
            });

            sender.SendEvent<DisposeMeEvent>();
            receiver.UnsubscribeFromEvent<DisposeMeEvent>();
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
        var previousContext = Context.Instance;
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
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var sender = new TestObject();
            var receiver = new TestObject();
            var pingCalls = 0;
            var pongCalls = 0;

            receiver.SubscribeToEvent<PongEvent>((ref PongEvent _) => ++pongCalls);

            receiver.SubscribeToEvent<PingEvent>((ref PingEvent _) => ++pingCalls);
            receiver.UnsubscribeFromEvent<PingEvent>();

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
        var previousContext = Context.Instance;
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
    public void Context_unsubscribe_from_event_removes_only_requested_struct_event_type()
    {
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var sender = new TestObject();
            var receiver = new TestObject();
            receiver.SubscribeToEvent<PingEvent>((ref PingEvent _) => { });
            receiver.SubscribeToEvent<PingEvent>(sender, (ref PingEvent _) => { });
            receiver.SubscribeToEvent<PongEvent>(sender, (ref PongEvent _) => { });

            receiver.UnsubscribeFromEvent<PingEvent>(sender);

            Assert.True(receiver.HasSubscribedToEvent<PingEvent>());
            Assert.False(receiver.HasSubscribedToEvent<PingEvent>(sender));
            Assert.True(receiver.HasSubscribedToEvent<PongEvent>(sender));

            receiver.UnsubscribeFromEvent<PingEvent>();

            Assert.False(receiver.HasSubscribedToEvent<PingEvent>());
            Assert.True(receiver.HasSubscribedToEvent<PongEvent>(sender));
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_unsubscribe_from_all_events_except_keeps_requested_struct_event_types()
    {
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var receiver = new TestObject();
            receiver.SubscribeToEvent<PingEvent>((ref PingEvent _) => { });
            receiver.SubscribeToEvent<PongEvent>((ref PongEvent _) => { });

            receiver.UnsubscribeFromAllEventsExcept([typeof(PongEvent)]);

            Assert.False(receiver.HasSubscribedToEvent<PingEvent>());
            Assert.True(receiver.HasSubscribedToEvent<PongEvent>());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_supports_unsubscribe_during_dispatch()
    {
        var previousContext = Context.Instance;
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
        var previousContext = Context.Instance;
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
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var key = new StringHash("Renderer.Debug");
            var sender = new TestObject();

            context.SetGlobalVar(key, 42);
            ref readonly var borrowedValue = ref sender.GetGlobalVar(key);

            Assert.Equal(42, sender.GetGlobalVar(key).GetInt());
            Assert.Equal(42, context.GetGlobalVars()[key].GetInt());

            sender.SetGlobalVar(key, 64);

            Assert.Equal(64, context.GetGlobalVar(key).GetInt());
            Assert.Equal(64, borrowedValue.GetInt());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_clones_replaces_and_destroys_global_vars()
    {
        using var context = new Context();
        var key = new StringHash("Owned");
        using var firstText = new CString("first");
        var firstValue = new Variant(firstText);

        context.SetGlobalVar(key, firstValue);
        ref readonly var firstStoredValue = ref context.GetGlobalVar(key);
        firstValue.Dispose();

        Assert.Equal("first", firstStoredValue.GetCString().ToString());

        using var secondText = new CString("second");
        var secondValue = new Variant(secondText);
        context.SetGlobalVar(key, secondValue);
        ref readonly var secondStoredValue = ref context.GetGlobalVar(key);
        secondValue.Dispose();

        Assert.Equal("second", firstStoredValue.GetCString().ToString());
        Assert.Equal("second", secondStoredValue.GetCString().ToString());

        context.Reset();

        Assert.True(context.GetGlobalVar(key).IsEmpty);
        Assert.False(typeof(Context).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(Context)));
        Assert.Equal(typeof(IReadOnlyDictionary<StringHash, Variant>), typeof(Context).GetMethod(nameof(Context.GetGlobalVars))!.ReturnType);
        Assert.Equal(typeof(IReadOnlyDictionary<StringHash, Variant>), typeof(RedObject).GetMethod(nameof(RedObject.GetGlobalVars))!.ReturnType);
    }

    [Fact]
    public void Context_reports_event_handler_queries()
    {
        var previousContext = Context.Instance;
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
    public void Context_exposes_active_event_sender_and_handler_during_dispatch()
    {
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var sender = new TestObject();
            var receiver = new TestObject();
            object? observedSender = null;
            EventHandler? observedHandler = null;

            receiver.SubscribeToEvent<PingEvent>((ref PingEvent _) =>
            {
                observedSender = receiver.GetEventSender();
                observedHandler = receiver.GetEventHandler();
            });

            sender.SendEvent<PingEvent>();

            Assert.Same(sender, observedSender);
            Assert.NotNull(observedHandler);
            Assert.Same(receiver, observedHandler!.Receiver);
            Assert.Null(observedHandler.Sender);
            Assert.Equal(typeof(PingEvent), observedHandler.EventType);
            Assert.Null(receiver.GetEventSender());
            Assert.Null(receiver.GetEventHandler());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Context_can_unsubscribe_from_all_events_except_selected_event_types_and_senders()
    {
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var senderA = new TestObject();
            var senderB = new TestObject();
            var receiver = new TestObject();

            receiver.SubscribeToEvent<KeepEvent>((ref KeepEvent _) => { });
            receiver.SubscribeToEvent<DropEvent>(static (ref DropEvent _) => { });
            receiver.UnsubscribeFromEvent<DropEvent>();

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
        var previousContext = Context.Instance;
        var engine = new EngineSystem();
        try
        {
            engine.Initialize(new EngineParameters
            {
                ApplicationName = "ContextLifecycle",
                Headless = true
            });

            Assert.Same(engine.Context, Context.Instance);

            engine.Shutdown();

            Assert.Same(previousContext, Context.Instance);
        }
        finally
        {
            Context.SetInstance(_fixture.Engine.Context);
        }
    }

    [Fact]
    public void Reset_disposes_owned_subsystems()
    {
        var context = new Context();
        var subsystem = new DisposableSubsystem();
        context.RegisterSubsystem(subsystem);

        context.Reset();

        Assert.True(subsystem.IsDisposed);
        Assert.Null(context.GetSubsystem<DisposableSubsystem>());
    }

    private sealed class TestObject : RedObject
    {
    }

    private sealed class DisposableSubsystem : RedObject
    {
        public bool IsDisposed { get; private set; }

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }
    }

    private interface ITestInterfaceSubsystem
    {
    }

    private sealed class TestInterfaceSubsystem : ITestInterfaceSubsystem
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
