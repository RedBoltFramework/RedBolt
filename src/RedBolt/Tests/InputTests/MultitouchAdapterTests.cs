namespace RedBolt.Tests;

using System.Reflection;

using RedBolt;
using RedBolt.Core;
using RedBolt.Input;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class MultitouchAdapterTests
{
    private readonly HeadlessEngineFixture _fixture;

    public MultitouchAdapterTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private InputSystem Input => _fixture.Input;

    [Fact]
    public void Matches_native_noncopyable_class_and_pod_active_touch_kinds()
    {
        var type = typeof(MultitouchAdapter);
        Assert.Equal(typeof(RedObject), type.BaseType);
        Assert.False(type.IsValueType);
        Assert.False(type.IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(type));
        Assert.Equal(typeof(RedObject), type.GetMethod(nameof(IDisposable.Dispose))!.DeclaringType);
        Assert.True(typeof(MultitouchEventType).IsValueType);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(MultitouchEventType)));
        Assert.True(typeof(Vector2i).IsValueType);

        var activeTouch = type.GetNestedType("ActiveTouch", BindingFlags.NonPublic);
        Assert.NotNull(activeTouch);
        Assert.True(activeTouch.IsValueType);
    }

    [Fact]
    public void Touch_begin_requires_matching_non_ui_input_touch()
    {
        var touches = Assert.IsType<Dictionary<int, TouchState>>(typeof(InputSystem)
            .GetField("_touches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Input));
        using var adapter = new MultitouchAdapter();
        adapter.SetEnabled(true);
        var events = new List<InputEvents.MultitouchEvent>();
        Context.Instance.SubscribeToEvent<InputEvents.MultitouchEvent>(adapter,
            (ref InputEvents.MultitouchEvent value) => events.Add(value));

        try
        {
            var begin = new InputEvents.TouchBeginEvent { TouchID = 7, X = 20, Y = 30 };
            Input.SendEvent(ref begin);
            Assert.Empty(events);

            touches[7] = new TouchState
            {
                TouchID = 7,
                Position = new Vector2i(20, 30),
                TouchedElement = new object()
            };
            Input.SendEvent(ref begin);
            Assert.Empty(events);

            touches[7] = new TouchState { TouchID = 7, Position = new Vector2i(20, 30) };
            Input.SendEvent(ref begin);

            var multitouch = Assert.Single(events);
            Assert.Equal((int)MultitouchEventType.MultitouchBegin, multitouch.EventType);
            Assert.Equal(1, multitouch.NumFingers);
            Assert.Equal(20, multitouch.X);
            Assert.Equal(30, multitouch.Y);
        }
        finally
        {
            touches.Clear();
        }
    }
}
