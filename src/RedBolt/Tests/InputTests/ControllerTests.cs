using RedBolt;
using System.Reflection;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ControllerTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ControllerTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private RedBolt.Input Input => _fixture.Input;

    private static void SendUpdate(float timeStep)
    {
        var update = new CoreEvents.UpdateEvent { TimeStep = timeStep };
        Context.Instance.SendEvent(ref update);
    }

    [Fact]
    public void FreeFlyController_matches_native_class_and_value_shape()
    {
        var type = typeof(FreeFlyController);
        Assert.Equal(typeof(Component), type.BaseType);
        Assert.False(type.IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(type));

        var movementType = type.GetNestedType("Movement", BindingFlags.NonPublic);
        Assert.NotNull(movementType);
        Assert.True(movementType.IsValueType);

        using var reflected = Assert.IsType<FreeFlyController>(
            Context.Instance.CreateObject(ObjectReflection<FreeFlyController>.TypeId));
    }

    [Fact]
    public void FreeFlyController_speed_attributes_preserve_raw_values()
    {
        using var controller = new FreeFlyController();
        controller.SetSpeed(-20.0f);
        controller.SetAcceleratedSpeed(-100.0f);

        Assert.Equal(-20.0f, controller.GetSpeed());
        Assert.Equal(-100.0f, controller.GetAcceleratedSpeed());
    }

    [Fact]
    public void FreeFlyController_keyboard_translation_works()
    {
        var scene = new Scene();
        var node = scene.CreateChild("FreeFly");
        using var freeFly = node.CreateComponent<FreeFlyController>();

        Input.BeginFrame();
        Input.SetMouseMode(MouseMode.Relative);
        Input.SetKey(Key.W, Scancode.W, true);
        SendUpdate(0.5f);
        Input.SetKey(Key.W, Scancode.W, false);

        Assert.True(node.Position.ApproximatelyEquivalent(new Vector3f(0.0f, 0.0f, 10.0f)));
    }

    [Fact]
    public void FreeFlyController_activation_hides_and_locks_mouse()
    {
        var scene = new Scene();
        using var freeFly = scene.CreateChild("FreeFly").CreateComponent<FreeFlyController>();

        Input.BeginFrame();
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
        Input.SetMouseButton(MouseButton.Right, true);
        SendUpdate(0.1f);

        Assert.False(Input.IsMouseVisible());
        Assert.Equal(MouseMode.Relative, Input.MouseMode);
    }

    [Fact]
    public void FreeFlyController_deactivation_restores_mouse()
    {
        var scene = new Scene();
        using var freeFly = scene.CreateChild("FreeFly").CreateComponent<FreeFlyController>();

        Input.BeginFrame();
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
        Input.SetMouseButton(MouseButton.Right, true);
        SendUpdate(0.1f);

        Input.BeginFrame();
        Input.SetMouseButton(MouseButton.Right, false);
        SendUpdate(0.1f);

        Assert.True(Input.IsMouseVisible());
        Assert.Equal(MouseMode.Free, Input.MouseMode);
    }

    [Fact]
    public void MoveAndOrbitController_action_mapping_works()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Controller");
        var movement = node.CreateComponent<MoveAndOrbitComponent>();
        var controller = node.CreateComponent<MoveAndOrbitController>();

        var map = new InputMap { Name = "Input/MoveController" };
        map.MapKeyboardKey(MoveAndOrbitController.ActionForward, Scancode.W);
        map.MapKeyboardKey(MoveAndOrbitController.ActionRight, Scancode.D);
        map.MapKeyboardKey(MoveAndOrbitController.ActionTurnRight, Scancode.E);
        map.MapKeyboardKey(MoveAndOrbitController.ActionLookDown, Scancode.S);
        map.AddMetadata(MoveAndOrbitController.AxisRotationSensitivity, 50.0f);
        controller.SetInputMap(map);

        _fixture.Time.BeginFrame(0.5f);
        try
        {
            Input.SetKey(Key.W, Scancode.W, true);
            Input.SetKey(Key.D, Scancode.D, true);
            Input.SetKey(Key.E, Scancode.E, true);
            Input.SetKey(Key.S, Scancode.S, true);
            Input.SendEvent<InputEvents.InputEndEvent>();
        }
        finally
        {
            Input.SetKey(Key.W, Scancode.W, false);
            Input.SetKey(Key.D, Scancode.D, false);
            Input.SetKey(Key.E, Scancode.E, false);
            Input.SetKey(Key.S, Scancode.S, false);
            _fixture.Time.EndFrame();
        }

        Assert.True(movement.GetVelocity().ApproximatelyEquivalent(new Vector3f(1.0f, 0.0f, 1.0f)));
        Assert.True(MathDefs.Equals(movement.GetYaw(), 25.0f));
        Assert.True(MathDefs.Equals(movement.GetPitch(), 25.0f));
        Assert.Equal("Input/MoveController", controller.GetInputMapAttr().Name);
    }

    [Fact]
    public void MoveAndOrbitController_matches_native_class_shape()
    {
        var type = typeof(MoveAndOrbitController);
        Assert.Equal(typeof(Component), type.BaseType);
        Assert.False(type.IsValueType);
        Assert.False(type.IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(type));
        Assert.DoesNotContain(type.GetInterfaces(), interfaceType => interfaceType.Name == "ISceneUpdateListener");
        Assert.False(typeof(InputMap).IsValueType);
        Assert.True(typeof(TouchState).IsValueType);
        Assert.True(typeof(Vector2i).IsValueType);

        using var reflected = Assert.IsType<MoveAndOrbitController>(
            Context.Instance.CreateObject(ObjectReflection<MoveAndOrbitController>.TypeId));
    }

    [Fact]
    public void MoveAndOrbitComponent_matches_native_class_shape()
    {
        var type = typeof(MoveAndOrbitComponent);
        Assert.Equal(typeof(LogicComponent), type.BaseType);
        Assert.False(type.IsValueType);
        Assert.False(type.IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(type));
        Assert.True(typeof(Vector3f).IsValueType);

        using var reflected = Assert.IsType<MoveAndOrbitComponent>(
            Context.Instance.CreateObject(ObjectReflection<MoveAndOrbitComponent>.TypeId));
    }

    [Fact]
    public void MoveAndOrbitComponent_initializes_rotation_and_distance_limits()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Orbit");
        node.SetRotation(new Quaternion(20.0f, 35.0f, 0.0f));

        var movement = node.CreateComponent<MoveAndOrbitComponent>();
        movement.SetDistanceLimits(2.0f, 40.0f);

        Assert.True(MathDefs.Equals(movement.GetYaw(), node.Rotation.YawAngle));
        Assert.True(MathDefs.Equals(movement.GetPitch(), node.Rotation.PitchAngle));
        Assert.True(MathDefs.Equals(movement.GetMinDistance(), 2.0f));
        Assert.True(MathDefs.Equals(movement.GetMaxDistance(), 40.0f));
    }

    [Fact]
    public void FreeFlyController_controller_axes_move_and_rotate()
    {
        var scene = new Scene();
        var node = scene.CreateChild("FreeFlyControllerAxes");
        using var controller = node.CreateComponent<FreeFlyController>();
        Input.SetMouseMode(MouseMode.Absolute);
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.5f);
        Input.SetControllerAxis(ControllerAxis.LeftY, 0.0f);
        Input.SetControllerAxis(ControllerAxis.RightX, 0.5f);
        Input.SetControllerAxis(ControllerAxis.RightY, 0.0f);

        SendUpdate(0.5f);

        var transformedAxis = new AxisAdapter().Transform(0.5f);
        Assert.True(MathDefs.Equals(node.Position.Length, transformedAxis * 10.0f, 0.01f));
        Assert.True(MathDefs.Equals(node.Rotation.YawAngle, transformedAxis * 50.0f, 0.1f));
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.0f);
        Input.SetControllerAxis(ControllerAxis.RightX, 0.0f);
    }

    [Fact]
    public void FreeFlyController_multitouch_rotates_and_pans()
    {
        var scene = new Scene();
        var node = scene.CreateChild("FreeFlyTouch");
        using var controller = node.CreateComponent<FreeFlyController>();
        var touches = Assert.IsType<Dictionary<int, TouchState>>(typeof(RedBolt.Input)
            .GetField("_touches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Input));
        try
        {
            touches[1] = new TouchState { TouchID = 1, Position = new Vector2i(100, 100) };
            var firstTouch = new InputEvents.TouchBeginEvent { TouchID = 1, X = 100, Y = 100 };
            Context.Instance.SendEvent(ref firstTouch);

            touches[1] = new TouchState { TouchID = 1, Position = new Vector2i(112, 94) };
            var rotate = new InputEvents.TouchMoveEvent { TouchID = 1, X = 112, Y = 94, DX = 12, DY = -6 };
            Context.Instance.SendEvent(ref rotate);
            Assert.False(node.Rotation.Equals(Quaternion.IDENTITY));

            var previousPosition = node.Position;
            touches[2] = new TouchState { TouchID = 2, Position = new Vector2i(130, 120) };
            var secondTouch = new InputEvents.TouchBeginEvent { TouchID = 2, X = 130, Y = 120 };
            Context.Instance.SendEvent(ref secondTouch);

            touches[1] = new TouchState { TouchID = 1, Position = new Vector2i(122, 99) };
            var pan = new InputEvents.TouchMoveEvent { TouchID = 1, X = 122, Y = 99, DX = 10, DY = 5 };
            Context.Instance.SendEvent(ref pan);
            Assert.False(node.Position.ApproximatelyEquivalent(previousPosition));
        }
        finally
        {
            touches.Clear();
        }
    }

    [Fact]
    public void MoveAndOrbitController_resource_attribute_uses_cache()
    {
        var cachedMap = new InputMap { Name = "Input/CachedMoveMap" };
        cachedMap.MapKeyboardKey(MoveAndOrbitController.ActionForward, Scancode.W);
        _fixture.Resources.ReleaseResource<InputMap>(cachedMap.Name);
        Assert.True(_fixture.Resources.AddManualResource(cachedMap));
        var scene = new Scene();
        var controller = scene.CreateChild("Controller").CreateComponent<MoveAndOrbitController>();
        controller.SetInputMapAttr(new ResourceRef(new StringHash(nameof(InputMap)), cachedMap.Name));

        Assert.Same(cachedMap, controller.GetInputMap());
    }
}
