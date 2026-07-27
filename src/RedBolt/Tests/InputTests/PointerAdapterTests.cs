using System.Reflection;

using RedBolt;
using RedBolt.Core;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PointerAdapterTests
{
    private readonly HeadlessEngineFixture _fixture;

    public PointerAdapterTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private InputSystem Input => _fixture.Input;

    [Fact]
    public void Matches_native_extensible_noncopyable_class_shape()
    {
        var type = typeof(PointerAdapter);
        Assert.Equal(typeof(RedObject), type.BaseType);
        Assert.False(type.IsValueType);
        Assert.False(type.IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(type));
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(PointerAdapterMask)));
        Assert.True(typeof(Vector2f).IsValueType);
        Assert.True(typeof(Vector2i).IsValueType);
    }

    [Fact]
    public void Mouse_input_updates_pointer_state_and_sends_synthesized_events()
    {
        using var adapter = new PointerAdapter();
        adapter.SetSubscriptionMask(PointerAdapterMask.Mouse);
        adapter.SetEnabled(true);
        InputEvents.MouseMoveEvent synthesizedMove = default;
        InputEvents.MouseButtonDownEvent synthesizedDown = default;
        Context.Instance.SubscribeToEvent<InputEvents.MouseMoveEvent>(adapter,
            (ref InputEvents.MouseMoveEvent value) => synthesizedMove = value);
        Context.Instance.SubscribeToEvent<InputEvents.MouseButtonDownEvent>(adapter,
            (ref InputEvents.MouseButtonDownEvent value) => synthesizedDown = value);

        var move = new InputEvents.MouseMoveEvent { X = 12, Y = 34 };
        Input.SendEvent(ref move);
        var down = new InputEvents.MouseButtonDownEvent { Button = MouseButton.Right };
        Input.SendEvent(ref down);

        Assert.Equal(new Vector2i(12, 34), adapter.GetPointerPosition());
        Assert.True(adapter.IsButtonDown());
        Assert.Equal(12, synthesizedMove.X);
        Assert.Equal(34, synthesizedMove.Y);
        Assert.Equal(MouseButton.Left, synthesizedDown.Button);
        Assert.Equal(1, synthesizedDown.Clicks);
    }

    [Fact]
    public void Direction_adapter_moves_pointer_on_update_event()
    {
        using var adapter = new PointerAdapter();
        adapter.SetSubscriptionMask(PointerAdapterMask.Keyboard);
        adapter.SetEnabled(true);
        var initialPosition = adapter.GetPointerPosition();
        Input.SetKey(Key.D, Scancode.D, true);
        try
        {
            var update = new CoreEvents.UpdateEvent { TimeStep = 0.5f };
            Context.Instance.SendEvent(ref update);
        }
        finally
        {
            Input.SetKey(Key.D, Scancode.D, false);
        }

        Assert.Equal(new Vector2i(initialPosition.X + (int)(adapter.GetCursorVelocity() * 0.5f), initialPosition.Y),
            adapter.GetPointerPosition());
    }

    [Fact]
    public void Joystick_button_sets_pressed()
    {
        using var adapter = new PointerAdapter();
        adapter.SetSubscriptionMask(PointerAdapterMask.Joystick);
        adapter.SetEnabled(true);

        Input.SetControllerButton(ControllerButton.A, true);
        Assert.True(adapter.IsButtonDown());
        Input.SetControllerButton(ControllerButton.A, false);
        Assert.False(adapter.IsButtonDown());
    }

    [Fact]
    public void Configuration_preserves_native_raw_setter_behavior()
    {
        using var adapter = new PointerAdapter();
        adapter.SetEnabled(true);
        adapter.SetSubscriptionMask(PointerAdapterMask.Keyboard | PointerAdapterMask.Joystick);
        var maxCursorSpeed = adapter.GetCursorVelocity();
        adapter.SetCursorSpeed(-150.0f);
        adapter.SetCursorAcceleration(-2.0f);

        Assert.True(adapter.IsEnabled());
        Assert.Equal(PointerAdapterMask.Keyboard | PointerAdapterMask.Joystick, adapter.GetSubscriptionMask());
        Assert.True(MathDefs.Equals(adapter.GetCursorVelocity(), maxCursorSpeed));
        Assert.True(MathDefs.Equals(adapter.GetCursorAcceleration(), -2.0f));
        Assert.Same(adapter.GetDirectionAggregator(), adapter.GetDirectionAggregator());
    }

    [Fact]
    public void Touch_owns_pointer_until_matching_release()
    {
        var touches = Assert.IsType<Dictionary<int, TouchState>>(typeof(InputSystem)
            .GetField("_touches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Input));
        using var adapter = new PointerAdapter();
        adapter.SetSubscriptionMask(PointerAdapterMask.Mouse | PointerAdapterMask.Touch);
        adapter.SetEnabled(true);
        try
        {
            touches[4] = new TouchState { TouchID = 4, Position = new Vector2i(20, 30) };
            var begin = new InputEvents.TouchBeginEvent { TouchID = 4, X = 20, Y = 30 };
            Input.SendEvent(ref begin);
            Assert.Equal(new Vector2i(20, 30), adapter.GetPointerPosition());
            Assert.True(adapter.IsButtonDown());

            var mouseMove = new InputEvents.MouseMoveEvent { X = 100, Y = 100 };
            Input.SendEvent(ref mouseMove);
            Assert.Equal(new Vector2i(20, 30), adapter.GetPointerPosition());

            var wrongEnd = new InputEvents.TouchEndEvent { TouchID = 5, X = 40, Y = 50 };
            Input.SendEvent(ref wrongEnd);
            Assert.True(adapter.IsButtonDown());

            var end = new InputEvents.TouchEndEvent { TouchID = 4, X = 25, Y = 35 };
            Input.SendEvent(ref end);
            Assert.Equal(new Vector2i(25, 35), adapter.GetPointerPosition());
            Assert.False(adapter.IsButtonDown());
        }
        finally
        {
            touches.Clear();
        }
    }
}
