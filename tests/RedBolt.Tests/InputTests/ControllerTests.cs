using RedBolt;

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

    [Fact]
    public void FreeFlyController_keyboard_translation_works()
    {
        var scene = new Scene();
        var node = scene.CreateChild("FreeFly");
        var freeFly = node.CreateComponent<FreeFlyController>();
        freeFly.SetInput(Input);

        Input.BeginFrame();
        Input.SetMouseMode(MouseMode.Absolute);
        Input.SetKey(Key.W, Scancode.W, true);
        scene.Update(0.5f);
        Input.SetKey(Key.W, Scancode.W, false);

        Assert.True(node.Position.ApproximatelyEquivalent(new Vector3(0.0f, 0.0f, 10.0f)));
    }

    [Fact]
    public void FreeFlyController_activation_captures_mouse()
    {
        var scene = new Scene();
        var freeFly = scene.CreateChild("FreeFly").CreateComponent<FreeFlyController>();
        freeFly.SetInput(Input);

        Input.BeginFrame();
        Input.SetMouseVisible(true);
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseButton(MouseButton.Right, true);
        scene.Update(0.1f);

        Assert.False(Input.IsMouseVisible());
        Assert.Equal(MouseMode.Relative, Input.MouseMode);
    }

    [Fact]
    public void FreeFlyController_deactivation_restores_mouse()
    {
        var scene = new Scene();
        var freeFly = scene.CreateChild("FreeFly").CreateComponent<FreeFlyController>();
        freeFly.SetInput(Input);

        Input.BeginFrame();
        Input.SetMouseVisible(true);
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseButton(MouseButton.Right, true);
        scene.Update(0.1f);

        Input.BeginFrame();
        Input.SetMouseButton(MouseButton.Right, false);
        scene.Update(0.1f);

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
        controller.SetInput(Input);

        var map = new InputMap("Input/MoveController", Input);
        map.MapKeyboardKey(MoveAndOrbitController.ActionForward, Scancode.W);
        map.MapKeyboardKey(MoveAndOrbitController.ActionRight, Scancode.D);
        map.MapKeyboardKey(MoveAndOrbitController.ActionTurnRight, Scancode.E);
        map.MapKeyboardKey(MoveAndOrbitController.ActionLookDown, Scancode.S);
        map.AddMetadata(MoveAndOrbitController.AxisRotationSensitivityMetadata, 50.0f);
        controller.SetInputMap(map);

        Input.BeginFrame();
        Input.SetKey(Key.W, Scancode.W, true);
        Input.SetKey(Key.D, Scancode.D, true);
        Input.SetKey(Key.E, Scancode.E, true);
        Input.SetKey(Key.S, Scancode.S, true);
        scene.Update(0.5f);
        Input.SetKey(Key.W, Scancode.W, false);
        Input.SetKey(Key.D, Scancode.D, false);
        Input.SetKey(Key.E, Scancode.E, false);
        Input.SetKey(Key.S, Scancode.S, false);

        Assert.True(movement.GetVelocity().ApproximatelyEquivalent(new Vector3(1.0f, 0.0f, 1.0f)));
        Assert.True(MathDefs.ApproximatelyEquivalent(movement.GetYaw(), 25.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(movement.GetPitch(), 25.0f));
        Assert.Equal("Input/MoveController", controller.GetInputMapAttr().Name);
    }
}
