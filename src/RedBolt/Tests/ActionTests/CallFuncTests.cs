namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CallFuncTests
{
    [Fact]
    public void Action_call_handler_invokes_receiver_function()
    {
        using var receiver = new Receiver();
        using var target = new BaseAction();
        var userData = new object();
        using var handler = new ActionCallHandlerImpl<Receiver>(
            receiver, static (instance, actionTarget) => instance.Handle(actionTarget), userData);

        handler.Invoke(target);

        Assert.Same(receiver, handler.Receiver);
        Assert.Same(userData, handler.UserData);
        Assert.Same(target, receiver.Target);
    }

    [Fact]
    public void Call_func_state_invokes_snapshotted_handler()
    {
        using var manager = new ActionManager(false);
        using var receiver = new Receiver();
        using var replacementReceiver = new Receiver();
        using var target = new BaseAction();
        using var handler = new ActionCallHandlerImpl<Receiver>(
            receiver, static (instance, actionTarget) => instance.Handle(actionTarget));
        using var replacementHandler = new ActionCallHandlerImpl<Receiver>(
            replacementReceiver, static (instance, actionTarget) => instance.Handle(actionTarget));
        using var action = new CallFunc();
        action.SetCallHandler(handler);

        Assert.Same(handler, action.CallHandler);
        Assert.NotNull(manager.AddAction(action, target));
        action.SetCallHandler(replacementHandler);
        manager.Update(0.0f);

        Assert.Same(target, receiver.Target);
        Assert.Null(replacementReceiver.Target);
    }

    [Fact]
    public void Send_event_state_dispatches_snapshotted_typed_event_from_target()
    {
        using var manager = new ActionManager(false);
        using var receiver = new Receiver();
        using var target = new BaseAction();
        using var action = new SendEvent<TestEvent>();
        var eventData = new TestEvent { Value = 42 };
        action.SetEventData(eventData);
        receiver.SubscribeToEvent<TestEvent>(target, receiver.Handle);

        Assert.NotNull(manager.AddAction(action, target));
        eventData.Value = 99;
        action.SetEventData(eventData);
        manager.Update(0.0f);

        Assert.Equal(42, receiver.Value);
        Assert.Same(target, receiver.Sender);
    }

    private struct TestEvent
    {
        public int Value;
    }

    private sealed class Receiver : RedObject
    {
        public RedObject? Target { get; private set; }
        public RedObject? Sender { get; private set; }
        public int Value { get; private set; }

        public void Handle(RedObject? target)
        {
            Target = target;
        }

        public void Handle(ref TestEvent eventData)
        {
            Sender = GetEventSender();
            Value = eventData.Value;
        }
    }
}
