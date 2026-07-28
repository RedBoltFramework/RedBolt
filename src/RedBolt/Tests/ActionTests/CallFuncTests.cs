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

    private sealed class Receiver : RedObject
    {
        public RedObject? Target { get; private set; }

        public void Handle(RedObject? target)
        {
            Target = target;
        }
    }
}
