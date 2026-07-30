//
// Copyright (c) 2021 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Actions;

using RedBolt.Core;
using RedBolt.Math;

/// <summary>Action as resource.</summary>
public class ActionBuilder
{
    /// <summary>Construct.</summary>
    public ActionBuilder()
    {
    }

    /// <summary>Continue with provided action.</summary>
    public ActionBuilder Then(FiniteTimeAction? nextAction)
    {
        if (nextAction is null)
            return this;

        if (_action is not null)
        {
            var action = new Sequence();
            action.SetFirstAction(_action);
            action.SetSecondAction(nextAction);
            _action = action;
            return this;
        }

        _action = nextAction;
        return this;
    }

    /// <summary>Run action in parallel to current one.</summary>
    public ActionBuilder Also(FiniteTimeAction? parallelAction)
    {
        if (parallelAction is null)
            return this;

        if (_action is not null)
        {
            if (_action is not Parallel parallel)
            {
                parallel = new Parallel();
                parallel.AddAction(_action);
            }
            parallel.AddAction(parallelAction);
            _action = parallel;
            return this;
        }
        _action = parallelAction;
        return this;
    }

    /// <summary>Continue with MoveBy action.</summary>
    public ActionBuilder MoveBy(
        float duration, in Vector3f offset, string attributeName = AttributeActionConstants.PositionAttribute)
    {
        var action = new MoveBy();
        action.SetAttributeName(attributeName);
        action.SetDuration(duration);
        action.SetPositionDelta(offset);
        return Then(action);
    }

    /// <summary>Continue with MoveBy action.</summary>
    public ActionBuilder MoveBy(
        float duration, in Vector2f offset, string attributeName = AttributeActionConstants.PositionAttribute)
    {
        var action = new MoveBy();
        action.SetAttributeName(attributeName);
        action.SetDuration(duration);
        action.SetPositionDelta(offset.ToVector3());
        return Then(action);
    }

    /// <summary>Continue with MoveBy action with quad.</summary>
    public ActionBuilder MoveByQuadratic(float duration, in Vector3f controlOffset, in Vector3f targetOffset,
        string attributeName = AttributeActionConstants.PositionAttribute)
    {
        var action = new MoveByQuadratic();
        action.SetAttributeName(attributeName);
        action.SetDuration(duration);
        action.SetControlDelta(controlOffset);
        action.SetPositionDelta(targetOffset);
        return Then(action);
    }

    /// <summary>Continue with MoveBy action.</summary>
    public ActionBuilder MoveByQuadratic(float duration, in Vector2f controlOffset, in Vector2f targetOffset,
        string attributeName = AttributeActionConstants.PositionAttribute)
    {
        var action = new MoveByQuadratic();
        action.SetAttributeName(attributeName);
        action.SetDuration(duration);
        action.SetControlDelta(controlOffset.ToVector3());
        action.SetPositionDelta(targetOffset.ToVector3());
        return Then(action);
    }

    /// <summary>Continue with JumpBy action.</summary>
    public ActionBuilder JumpBy(
        in Vector3f offset, string attributeName = AttributeActionConstants.PositionAttribute)
    {
        var action = new JumpBy();
        action.SetAttributeName(attributeName);
        action.SetPositionDelta(offset);
        return Then(action);
    }

    /// <summary>Continue with MoveBy action.</summary>
    public ActionBuilder JumpBy(
        in Vector2f offset, string attributeName = AttributeActionConstants.PositionAttribute)
    {
        var action = new JumpBy();
        action.SetAttributeName(attributeName);
        action.SetPositionDelta(offset.ToVector3());
        return Then(action);
    }

    /// <summary>Continue with ScaleBy action.</summary>
    public ActionBuilder ScaleBy(
        float duration, in Vector3f delta, string attributeName = AttributeActionConstants.ScaleAttribute)
    {
        var action = new ScaleBy();
        action.SetAttributeName(attributeName);
        action.SetDuration(duration);
        action.SetScaleDelta(delta);
        return Then(action);
    }

    /// <summary>Continue with ScaleBy action.</summary>
    public ActionBuilder ScaleBy(
        float duration, in Vector2f delta, string attributeName = AttributeActionConstants.ScaleAttribute)
    {
        return ScaleBy(duration, new Vector3f(delta.X, delta.Y, 1.0f), attributeName);
    }

    /// <summary>Continue with RotateBy action.</summary>
    public ActionBuilder RotateBy(
        float duration, in Quaternion delta, string attributeName = AttributeActionConstants.RotationAttribute)
    {
        var action = new RotateBy();
        action.SetAttributeName(attributeName);
        action.SetDuration(duration);
        action.SetRotationDelta(delta);
        return Then(action);
    }

    /// <summary>Continue with RotateAround action.</summary>
    public ActionBuilder RotateAround(float duration, in Vector3f pivot, in Quaternion delta)
    {
        var action = new RotateAround();
        action.SetDuration(duration);
        action.SetRotationDelta(delta);
        action.SetPivot(pivot);
        return Then(action);
    }

    /// <summary>Continue with Hide action.</summary>
    public ActionBuilder Hide(string attributeName = AttributeActionConstants.IsVisibleAttribute)
    {
        var action = new Hide();
        action.SetAttributeName(attributeName);
        return Then(action);
    }

    /// <summary>Continue with Show action.</summary>
    public ActionBuilder Show(string attributeName = AttributeActionConstants.IsVisibleAttribute)
    {
        var action = new Show();
        action.SetAttributeName(attributeName);
        return Then(action);
    }

    /// <summary>Continue with Enable action.</summary>
    public ActionBuilder Enable(string attributeName = AttributeActionConstants.IsEnabledAttribute)
    {
        var action = new Enable();
        action.SetAttributeName(attributeName);
        return Then(action);
    }

    /// <summary>Continue with Disable action.</summary>
    public ActionBuilder Disable(string attributeName = AttributeActionConstants.IsEnabledAttribute)
    {
        var action = new Disable();
        action.SetAttributeName(attributeName);
        return Then(action);
    }

    /// <summary>Continue with AttributeBlink action.</summary>
    public ActionBuilder Blink(
        float duration, int numOfBlinks, string attributeName = AttributeActionConstants.IsEnabledAttribute)
    {
        var action = new Blink();
        action.SetDuration(duration);
        action.SetNumOfBlinks(numOfBlinks);
        action.SetAttributeName(attributeName);
        return Then(action);
    }

    /// <summary>Continue with AttributeTo action.</summary>
    public ActionBuilder AttributeTo(float duration, string attributeName, in Variant to)
    {
        var action = new AttributeTo();
        action.SetDuration(duration);
        action.SetAttributeName(attributeName);
        action.SetTo(to);
        return Then(action);
    }

    /// <summary>Continue with AttributeFromTo action.</summary>
    public ActionBuilder AttributeFromTo(
        float duration, string attributeName, in Variant from, in Variant to)
    {
        var action = new AttributeFromTo();
        action.SetDuration(duration);
        action.SetAttributeName(attributeName);
        action.SetFrom(from);
        action.SetTo(to);
        return Then(action);
    }

    /// <summary>Continue with ShaderParameterTo action.</summary>
    public ActionBuilder ShaderParameterTo(float duration, string parameter, in Variant to)
    {
        var action = new ShaderParameterTo();
        action.SetDuration(duration);
        action.SetName(parameter);
        action.SetTo(to);
        return Then(action);
    }

    /// <summary>Continue with ShaderParameterFromTo action.</summary>
    public ActionBuilder ShaderParameterFromTo(
        float duration, string parameter, in Variant from, in Variant to)
    {
        var action = new ShaderParameterFromTo();
        action.SetDuration(duration);
        action.SetName(parameter);
        action.SetFrom(from);
        action.SetTo(to);
        return Then(action);
    }

    // TODO: port
    // ActionBuilder& ActionBuilder::SendEvent(ea::string_view eventType, const StringVariantMap& data)
    // {
    //     const auto action = MakeShared<Actions::SendEvent>(context_);
    //     action->SetEventType(eventType);
    //     action->SetEventData(data);
    //     return Then(action);
    // }

    /// <summary>Continue with CallFunc action.</summary>
    public ActionBuilder CallFunc(ActionCallHandler? handler)
    {
        var action = new CallFunc();
        action.SetCallHandler(handler);
        return Then(action);
    }

    /// <summary>Continue with CallFunc action.</summary>
    public ActionBuilder CallFunc<T>(
        T receiver, ActionCallHandlerImpl<T>.HandlerFunctionPtr function, object? userData = null)
        where T : RedObject
    {
        return CallFunc(new ActionCallHandlerImpl<T>(receiver, function, userData));
    }

    /// <summary>Combine with BackIn action.</summary>
    public ActionBuilder BackIn()
    {
        var action = new EaseBackIn();
        action.SetInnerAction(_action);
        _action = action;
        return this;
    }

    /// <summary>Combine with BackOut action.</summary>
    public ActionBuilder BackOut()
    {
        var action = new EaseBackOut();
        action.SetInnerAction(_action);
        _action = action;
        return this;
    }

    /// <summary>Combine with BackInOut action.</summary>
    public ActionBuilder BackInOut()
    {
        var action = new EaseBackInOut();
        action.SetInnerAction(_action);
        _action = action;
        return this;
    }

    /// <summary>Combine with BounceOut action.</summary>
    public ActionBuilder BounceOut()
    {
        var action = new EaseBounceOut();
        action.SetInnerAction(_action);
        _action = action;
        return this;
    }

    /// <summary>Combine with BounceIn action.</summary>
    public ActionBuilder BounceIn()
    {
        var action = new EaseBounceIn();
        action.SetInnerAction(_action);
        _action = action;
        return this;
    }

    /// <summary>Combine with BounceInOut action.</summary>
    public ActionBuilder BounceInOut()
    {
        var action = new EaseBounceInOut();
        action.SetInnerAction(_action);
        _action = action;
        return this;
    }

    /// <summary>Combine with SineOut action.</summary>
    public ActionBuilder SineOut()
    {
        var action = new EaseSineOut();
        action.SetInnerAction(_action);
        _action = action;
        return this;
    }

    /// <summary>Combine with SineIn action.</summary>
    public ActionBuilder SineIn()
    {
        var action = new EaseSineIn();
        action.SetInnerAction(_action);
        _action = action;
        return this;
    }

    /// <summary>Combine with SineInOut action.</summary>
    public ActionBuilder SineInOut()
    {
        var action = new EaseSineInOut();
        action.SetInnerAction(_action);
        _action = action;
        return this;
    }

    /// <summary>Combine with ExponentialOut action.</summary>
    public ActionBuilder ExponentialOut()
    {
        var action = new EaseExponentialOut();
        action.SetInnerAction(_action);
        _action = action;
        return this;
    }

    /// <summary>Combine with ExponentialIn action.</summary>
    public ActionBuilder ExponentialIn()
    {
        var action = new EaseExponentialIn();
        action.SetInnerAction(_action);
        _action = action;
        return this;
    }

    /// <summary>Combine with ExponentialInOut action.</summary>
    public ActionBuilder ExponentialInOut()
    {
        var action = new EaseExponentialInOut();
        action.SetInnerAction(_action);
        _action = action;
        return this;
    }

    /// <summary>Combine with ElasticIn action.</summary>
    public ActionBuilder ElasticIn(float period = 0.3f)
    {
        var action = new EaseElasticIn();
        action.SetInnerAction(_action);
        action.SetPeriod(period);
        _action = action;
        return this;
    }

    /// <summary>Combine with ElasticOut action.</summary>
    public ActionBuilder ElasticOut(float period = 0.3f)
    {
        var action = new EaseElasticOut();
        action.SetInnerAction(_action);
        action.SetPeriod(period);
        _action = action;
        return this;
    }

    /// <summary>Combine with ElasticInOut action.</summary>
    public ActionBuilder ElasticInOut(float period = 0.3f)
    {
        var action = new EaseElasticInOut();
        action.SetInnerAction(_action);
        action.SetPeriod(period);
        _action = action;
        return this;
    }

    /// <summary>Combine with RemoveSelf action.</summary>
    public ActionBuilder RemoveSelf() => Then(new RemoveSelf());

    /// <summary>Combine with DelayTime action.</summary>
    public ActionBuilder DelayTime(float duration)
    {
        var action = new DelayTime();
        action.SetDuration(duration);
        return Then(action);
    }

    /// <summary>Repeat current action.</summary>
    public ActionBuilder Repeat(int times)
    {
        var action = new Repeat();
        action.SetInnerAction(_action);
        action.SetTimes(times);
        _action = action;
        return this;
    }

    /// <summary>Repeat current action forever (until canceled).</summary>
    public ActionBuilder RepeatForever()
    {
        var action = new RepeatForever();
        action.SetInnerAction(_action);
        _action = action;
        return this;
    }

    /// <summary>Complete action building and produce result.</summary>
    public FiniteTimeAction? Build() => _action;

    /// <summary>Run current action on object.</summary>
    /// <remarks>Use Build() instead of Run() if you run the action more than once to reduce allocations.</remarks>
    public ActionState? Run(RedObject? target)
    {
        return Run(Context.Instance.GetSubsystem<ActionManager>(), target);
    }

    /// <summary>Run current action on object via action manager.</summary>
    /// <remarks>Use Build() instead of Run() if you run the action more than once to reduce allocations.</remarks>
    public ActionState? Run(ActionManager? actionManager, RedObject? target)
    {
        if (actionManager is not null)
            return actionManager.AddAction(_action, target);
        return null;
    }

    /// <summary>Action on top of stack (current).</summary>
    private FiniteTimeAction? _action;
}
