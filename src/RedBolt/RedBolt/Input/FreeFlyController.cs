namespace RedBolt.Input;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

[Reflected]
public partial class FreeFlyController : Component
{
    private const float DefaultMouseSensitivity = 0.1f;
    // 90' motion per inch
    private const float DefaultTouchRotationSensitivity = 1.0f;
    // Degrees per second
    private const float DefaultAxisRotationSensitivity = 100.0f;

    private struct Movement
    {
        public Vector3f Rotation;
        public Vector3f Translation;

        public static Movement operator +(Movement left, Movement right)
        {
            left.Rotation += right.Rotation;
            left.Translation += right.Translation;
            return left;
        }
    }

    /// <summary>Construct.</summary>
    public FreeFlyController()
    {
        _ignoreJoystickId = GetSubsystem<InputSystem>()!.FindAccelerometerJoystickId();
        SubscribeToEvent<InputEvents.MultitouchEvent>(_multitouchAdapter, HandleMultitouch);

    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _multitouchAdapter.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }


    /// <summary>Handle enabled/disabled state change. Changes update event subscription.</summary>
    public override void OnSetEnabled()
    {
        UpdateEventSubscription();
    }

    /// <summary>Attributes</summary>
    public void SetSpeed(float value) { _speed = value; }
    public float GetSpeed() { return _speed; }
    public void SetAcceleratedSpeed(float value) { _acceleratedSpeed = value; }
    public float GetAcceleratedSpeed() { return _acceleratedSpeed; }
    public float GetMinPitch() { return _minPitch; }
    public void SetMinPitch(float value) { _minPitch = value; }
    public float GetMaxPitch() { return _maxPitch; }
    public void SetMaxPitch(float value) { _maxPitch = value; }
    public float GetMouseSensitivity() { return _mouseSensitivity; }
    public void SetMouseSensitivity(float value) { _mouseSensitivity = value; }
    public float GetTouchSensitivity() { return _touchSensitivity; }
    public void SetTouchSensitivity(float value) { _touchSensitivity = value; }
    public float GetAxisSensitivity() { return _axisSensitivity; }
    public void SetAxisSensitivity(float value) { _axisSensitivity = value; }

    /// <summary>Handle scene node being assigned at creation.</summary>
    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        UpdateEventSubscription();
    }

    /// <summary>Subscribe/unsubscribe to update events based on current enabled state and update event mask.</summary>
    private void UpdateEventSubscription()
    {
        var enabled = IsEnabledEffective;

        _multitouchAdapter.SetEnabled(enabled);

        if (enabled && !_subscribed)
        {
            SubscribeToEvent<CoreEvents.UpdateEvent>(HandleUpdate);
            _subscribed = true;
        }
        else if (_subscribed)
        {
            UnsubscribeFromEvent<CoreEvents.UpdateEvent>();
            _subscribed = false;
        }
    }

    /// <summary>Handle scene update event.</summary>
    private void HandleUpdate(ref CoreEvents.UpdateEvent eventData)
    {
        // Then execute user-defined update function
        Update(eventData.TimeStep);
    }

    /// <summary>Handle scene update. Called by LogicComponent base class.</summary>
    private void Update(float timeStep)
    {
        // TODO: port: if (GetSubsystem<UI>()->GetFocusElement()) return;

        var input = GetSubsystem<InputSystem>()!;
        if (_isActive || input.MouseMode == MouseMode.Free)
        {
            if (!_isActive && input.GetMouseButtonPress(MouseButton.Right))
            {
                _isActive = true;
                _oldMouseVisible = input.MouseVisible;
                _oldMouseMode = input.MouseMode;
                input.SetMouseVisible(false);
                input.SetMouseMode(MouseMode.Relative);
            }
            else if (_isActive && !input.GetMouseButtonDown(MouseButton.Right))
            {
                _isActive = false;
                input.SetMouseVisible(_oldMouseVisible);
                input.SetMouseMode(_oldMouseMode);
            }

            if (_isActive)
                HandleKeyboardMouseAndJoysticks(timeStep);
        }
        else
            HandleKeyboardMouseAndJoysticks(timeStep);
    }

    /// <summary>Handle mouse input.</summary>
    private Movement HandleMouse()
    {
        var input = GetSubsystem<InputSystem>()!;
        var mouseMove = input.GetMouseMove();
        var movement = new Movement();
        movement.Rotation.Y += _mouseSensitivity * mouseMove.X;
        movement.Rotation.X += _mouseSensitivity * mouseMove.Y;
        return movement;
    }

    /// <summary>Handle keyboard input.</summary>
    private Movement HandleKeyboard(float timeStep)
    {
        var movement = new Movement();
        var input = GetSubsystem<InputSystem>()!;

        var speed = input.GetKeyDown(Key.Shift) ? _acceleratedSpeed : _speed;
        if (input.GetScancodeDown(Scancode.W))
            movement.Translation.Z += speed * timeStep;
        if (input.GetScancodeDown(Scancode.S))
            movement.Translation.Z -= speed * timeStep;
        if (input.GetScancodeDown(Scancode.A))
            movement.Translation.X -= speed * timeStep;
        if (input.GetScancodeDown(Scancode.D))
            movement.Translation.X += speed * timeStep;
        if (input.GetScancodeDown(Scancode.Q))
            movement.Translation.Y -= speed * timeStep;
        if (input.GetScancodeDown(Scancode.E))
            movement.Translation.Y += speed * timeStep;

        return movement;
    }

    /// <summary>Handle controller input.</summary>
    private Movement HandleController(JoystickState state, float timeStep)
    {
        var movement = new Movement();

        var speed = _speed;
        // Apply acceleration
        if (state.HasAxisPosition((uint)ControllerAxis.TriggerLeft))
        {
            var triggerAdapter = (AxisAdapter)_axisAdapter.Clone();
            triggerAdapter.SetNeutralValue(-1.0f);
            var value = _axisAdapter.Transform(state.GetAxisPosition((uint)ControllerAxis.TriggerLeft));
            speed = MathDefs.Lerp(_speed, _acceleratedSpeed, MathDefs.Clamp(value, 0.0f, 1.0f));
        }
        {
            var value = _axisAdapter.Transform(state.GetAxisPosition((uint)ControllerAxis.LeftX));
            movement.Translation.X += value * speed * timeStep;
        }
        {
            var value = _axisAdapter.Transform(state.GetAxisPosition((uint)ControllerAxis.LeftY));
            movement.Translation.Z -= value * speed * timeStep;
        }
        {
            var value = _axisAdapter.Transform(state.GetAxisPosition((uint)ControllerAxis.RightX));
            movement.Rotation.Y += value * timeStep * _axisSensitivity;
        }
        {
            var value = _axisAdapter.Transform(state.GetAxisPosition((uint)ControllerAxis.RightY));
            movement.Rotation.X += value * timeStep * _axisSensitivity;
        }

        var numHats = state.GetNumHats();
        if (numHats > 0)
        {
            var value = state.GetHatPosition(0);
            if (0 != (value & (int)HatPosition.Up))
                movement.Translation.Z += speed * timeStep;
            if (0 != (value & (int)HatPosition.Down))
                movement.Translation.Z -= speed * timeStep;
            if (0 != (value & (int)HatPosition.Left))
                movement.Translation.X -= speed * timeStep;
            if (0 != (value & (int)HatPosition.Right))
                movement.Translation.X += speed * timeStep;
        }

        return movement;
    }

    /// <summary>Handle controller input.</summary>
    private Movement HandleGenericJoystick(JoystickState state, float timeStep)
    {
        var movement = new Movement();

        var speed = _speed;
        // Apply acceleration
        if (state.HasAxisPosition(4))
        {
            var triggerAdapter = (AxisAdapter)_axisAdapter.Clone();
            triggerAdapter.SetNeutralValue(-1.0f);
            var value = (1.0f + _axisAdapter.Transform(state.GetAxisPosition(4))) * 0.5f;
            speed = MathDefs.Lerp(_speed, _acceleratedSpeed, MathDefs.Clamp(value, 0.0f, 1.0f));
        }

        if (state.HasAxisPosition(0))
        {
            var value = _axisAdapter.Transform(state.GetAxisPosition(0));
            if (value != 0)
                movement.Translation.X += value * speed * timeStep;
        }
        if (state.HasAxisPosition(1))
        {
            var value = _axisAdapter.Transform(state.GetAxisPosition(1));
            movement.Translation.Z -= value * speed * timeStep;
        }
        if (state.HasAxisPosition(2))
        {
            var value = _axisAdapter.Transform(state.GetAxisPosition(2));
            movement.Rotation.Y += value * timeStep * _axisSensitivity;
        }
        if (state.HasAxisPosition(3))
        {
            var value = _axisAdapter.Transform(state.GetAxisPosition(3));
            movement.Rotation.X += value * timeStep * _axisSensitivity;
        }

        var numHats = state.GetNumHats();
        if (numHats > 0)
        {
            var value = state.GetHatPosition(0);
            if (0 != (value & (int)HatPosition.Up))
                movement.Translation.Z += speed * timeStep;
            if (0 != (value & (int)HatPosition.Down))
                movement.Translation.Z -= speed * timeStep;
            if (0 != (value & (int)HatPosition.Left))
                movement.Translation.X -= speed * timeStep;
            if (0 != (value & (int)HatPosition.Right))
                movement.Translation.X += speed * timeStep;
        }

        return movement;
    }

    /// <summary>Handle wheel input.</summary>
    private Movement HandleWheel(JoystickState state, float timeStep)
    {
        var movement = new Movement();

        var numAxes = state.GetNumAxes();
        var speed = _speed;

        // Wheel
        {
            var value = _axisAdapter.Transform(state.GetAxisPosition(0));
            movement.Rotation.Y += value * timeStep * _axisSensitivity;
        }
        // Acceleration
        if (numAxes > 1)
        {
            var pedalAdapter = (AxisAdapter)_axisAdapter.Clone();
            pedalAdapter.SetInverted(true);
            pedalAdapter.SetNeutralValue(-1.0f);
            if (state.HasAxisPosition(1))
            {
                var value = pedalAdapter.Transform(state.GetAxisPosition(1));
                movement.Translation.Z += value * speed * timeStep;
            }
            // Brake
            if (state.HasAxisPosition(2))
            {
                var value = pedalAdapter.Transform(state.GetAxisPosition(2));
                movement.Translation.Z -= value * speed * timeStep;
            }
        }
        return movement;
    }

    /// <summary>Handle flight stick input.</summary>
    private Movement HandleFlightStick(JoystickState state, float timeStep)
    {
        var movement = new Movement();

        var numAxes = state.GetNumAxes();
        var speed = _speed;
        // Roll
        {
            var value = _axisAdapter.Transform(state.GetAxisPosition(0));
            movement.Rotation.Z -= value * timeStep * _axisSensitivity;
        }
        // Pitch
        {
            var value = _axisAdapter.Transform(state.GetAxisPosition(1));
            movement.Rotation.X -= value * timeStep * _axisSensitivity;
        }
        // Yaw
        {
            var value = _axisAdapter.Transform(state.GetAxisPosition(3));
            movement.Rotation.Y += value * timeStep * _axisSensitivity;
        }
        // Throttle
        if (state.HasAxisPosition(2))
        {
            var pedalAdapter = (AxisAdapter)_axisAdapter.Clone();
            pedalAdapter.SetInverted(true);
            pedalAdapter.SetNeutralValue(-1.0f);
            var value = pedalAdapter.Transform(state.GetAxisPosition(2));
            movement.Translation.Z += value * speed * timeStep;
        }
        // Rocker
        if (state.HasAxisPosition(4))
        {
            var value = _axisAdapter.Transform(state.GetAxisPosition(4));
            movement.Translation.X += value * speed * timeStep;
        }
        return movement;
    }

    /// <summary>Handle keyboard and mouse input.</summary>
    private void HandleKeyboardMouseAndJoysticks(float timeStep)
    {
        var input = GetSubsystem<InputSystem>()!;

        // Use this frame's mouse motion to adjust camera node yaw and pitch. Clamp the pitch between -90 and 90 degrees
        UpdateCameraAngles();

        // World space rotation (first person shooter)
        var worldMovement = new Movement();
        // Local space rotation (flight sim)
        var localMovement = new Movement();

        worldMovement += HandleMouse();
        worldMovement += HandleKeyboard(timeStep);

        var numJoysticks = input.GetNumJoysticks();
        for (var joystickIndex = 0u; joystickIndex < numJoysticks; ++joystickIndex)
        {
            var state = input.GetJoystickByIndex(joystickIndex);
            if (state.HasValue)
            {
                var joystick = state.Value;
                var isAccelerometer = joystick.GetNumAxes() == 3 && joystick.GetNumButtons() == 0 && joystick.GetNumHats() == 0;
                if (!isAccelerometer)
                {
                    if (joystick.JoystickID == _ignoreJoystickId)
                        continue;

                    switch (joystick.DeviceType)
                    {
                        // Ignore odd devices
                        case JoystickDeviceType.Guitar: break;
                        case JoystickDeviceType.DrumKit: break;
                        case JoystickDeviceType.Throttle: break;
                        // Handle known devices
                        case JoystickDeviceType.Wheel: worldMovement += HandleWheel(joystick, timeStep); break;
                        case JoystickDeviceType.FlightStick: localMovement += HandleFlightStick(joystick, timeStep); break;
                        case JoystickDeviceType.GameController: worldMovement += HandleController(joystick, timeStep); break;
                        default: worldMovement += HandleGenericJoystick(joystick, timeStep); break;
                    }
                }
            }
        }

        if (localMovement.Rotation == Vector3f.Zero)
        {
            var eulerAngles = _lastKnownEulerAngles + worldMovement.Rotation;
            SetCameraAngles(eulerAngles);
        }
        else
        {
            var eulerAngles = _lastKnownEulerAngles + worldMovement.Rotation;
            eulerAngles.X = MathDefs.Clamp(eulerAngles.X, _minPitch, _maxPitch);
            SetCameraRotation(new Quaternion(eulerAngles) * new Quaternion(localMovement.Rotation));
        }

        var translation = localMovement.Translation + worldMovement.Translation;
        if (!translation.ApproximatelyEquivalent(Vector3f.Zero))
            Node.Translate(translation, TransformSpace.Local);
    }

    /// <summary>Handle multitouch input event.</summary>
    private void HandleMultitouch(ref InputEvents.MultitouchEvent eventData)
    {
        var eventType = (MultitouchEventType)eventData.EventType;

        if (eventType == MultitouchEventType.MultitouchMove)
        {
            var graphics = GetSubsystem<GraphicsSystem>();
            var camera = Node.GetComponent<Camera>();
            var sensitivity = _touchSensitivity * 90.0f / 1080.0f;
            if (graphics is not null && camera is not null)
                sensitivity = _touchSensitivity * camera.Fov / graphics.Height;
            var numFingers = eventData.NumFingers;
            var dx = eventData.DX;
            var dy = eventData.DY;
            if (numFingers == 1)
            {
                UpdateCameraAngles();
                var eulerAngles = _lastKnownEulerAngles;
                eulerAngles.Y -= sensitivity * dx;
                eulerAngles.X -= sensitivity * dy;
                SetCameraAngles(eulerAngles);
            }
            else if (numFingers == 2)
            {
                var position = Node.Position;
                var dsize = eventData.DSize;

                position += -Node.Right * (sensitivity * dx);
                position += Node.Up * (sensitivity * dy);
                position += Node.Direction * (sensitivity * (dsize.X + dsize.Y));

                Node.SetPosition(position);
            }
        }
    }

    /// <summary>Detect camera angles if camera has changed.</summary>
    private void UpdateCameraAngles()
    {
        var rotation = Node.Rotation;
        if (!_lastKnownCameraRotation.HasValue || !_lastKnownCameraRotation.Value.ApproximatelyEquivalent(rotation))
        {
            _lastKnownCameraRotation = rotation;
            _lastKnownEulerAngles = rotation.EulerAngles;
        }
    }

    /// <summary>Set camera rotation.</summary>
    private void SetCameraRotation(Quaternion quaternion)
    {
        _lastKnownEulerAngles = quaternion.EulerAngles;
        _lastKnownCameraRotation = quaternion;

        // Construct new orientation for the camera scene node from yaw and pitch. Roll is fixed to zero
        Node.SetRotation(_lastKnownCameraRotation.Value);
    }

    /// <summary>Update camera rotation.</summary>
    private void SetCameraAngles(Vector3f eulerAngles)
    {
        eulerAngles.X = MathDefs.Clamp(eulerAngles.X, _minPitch, _maxPitch);
        _lastKnownEulerAngles = eulerAngles;
        _lastKnownCameraRotation = new Quaternion(eulerAngles);

        // Construct new orientation for the camera scene node from yaw and pitch. Roll is fixed to zero
        Node.SetRotation(_lastKnownCameraRotation.Value);
    }

    /// <summary>Camera speed.</summary>
    [Serialized("Speed")]
    private float _speed = 20.0f;
    /// <summary>Camera accelerated speed.</summary>
    [Serialized("Accelerated Speed")]
    private float _acceleratedSpeed = 100.0f;
    /// <summary>Mouse sensitivity</summary>
    [Serialized("Mouse Sensitivity")]
    private float _mouseSensitivity = DefaultMouseSensitivity;
    /// <summary>Touch sensitivity</summary>
    [Serialized("Touch Sensitivity")]
    private float _touchSensitivity = DefaultTouchRotationSensitivity;
    /// <summary>Axis sensitivity</summary>
    [Serialized("Axis Sensitivity")]
    private float _axisSensitivity = DefaultAxisRotationSensitivity;
    /// <summary>Pitch range</summary>
    [Serialized("Min Pitch")]
    private float _minPitch = -90.0f;
    [Serialized("Max Pitch")]
    private float _maxPitch = 90.0f;
    /// <summary>Gamepad default axis adapter</summary>
    private AxisAdapter _axisAdapter = new();
    /// <summary>Is subscribed to update</summary>
    private bool _subscribed;
    /// <summary>Multitouch input adapter</summary>
    private MultitouchAdapter _multitouchAdapter = new();
    /// <summary>Last known camera rotation to keep track of yaw and pitch.</summary>
    private Quaternion? _lastKnownCameraRotation;
    /// <summary>Last known yaw, pitch and roll to prevent gimbal lock.</summary>
    private Vector3f _lastKnownEulerAngles;
    /// <summary>Joystick to ignore (SDL gyroscope virtual joystick)</summary>
    private int _ignoreJoystickId = -1;

    /// <summary>Whether the rotation is performing now.</summary>
    private bool _isActive;
    private bool _oldMouseVisible;
    private MouseMode _oldMouseMode;

}
