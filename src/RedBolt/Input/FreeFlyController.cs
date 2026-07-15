namespace RedBolt;

public sealed class FreeFlyController : Component, ISceneUpdateListener
{
    public const float DefaultMouseSensitivity = 0.1f;
    public const float DefaultTouchRotationSensitivity = 1.0f;
    public const float DefaultAxisRotationSensitivity = 100.0f;

    private struct Movement
    {
        public Vector3f Rotation;
        public Vector3f Translation;

        public static Movement operator +(Movement left, Movement right)
        {
            return new Movement
            {
                Rotation = left.Rotation + right.Rotation,
                Translation = left.Translation + right.Translation
            };
        }
    }

    private float _speed = 20.0f;
    private float _acceleratedSpeed = 100.0f;
    private float _mouseSensitivity = DefaultMouseSensitivity;
    private float _touchSensitivity = DefaultTouchRotationSensitivity;
    private float _axisSensitivity = DefaultAxisRotationSensitivity;
    private float _minPitch = -90.0f;
    private float _maxPitch = 90.0f;
    private bool _isActive;
    private bool _oldMouseVisible = true;
    private MouseMode _oldMouseMode = MouseMode.Absolute;
    private Quaternion? _lastKnownCameraRotation;
    private Vector3f _lastKnownEulerAngles;
    private readonly AxisAdapter _axisAdapter = new();
    private ContextEventSubscription? _multitouchSubscription;

    public FreeFlyController()
    {
        RegisterAttribute("Speed", typeof(float), self => ((FreeFlyController)self).GetSpeed(), (self, value) => ((FreeFlyController)self).SetSpeed(value is float typed ? typed : 20.0f), 20.0f);
        RegisterAttribute("Accelerated Speed", typeof(float), self => ((FreeFlyController)self).GetAcceleratedSpeed(), (self, value) => ((FreeFlyController)self).SetAcceleratedSpeed(value is float typed ? typed : 100.0f), 100.0f);
        RegisterAttribute("Min Pitch", typeof(float), self => ((FreeFlyController)self).GetMinPitch(), (self, value) => ((FreeFlyController)self).SetMinPitch(value is float typed ? typed : -90.0f), -90.0f);
        RegisterAttribute("Max Pitch", typeof(float), self => ((FreeFlyController)self).GetMaxPitch(), (self, value) => ((FreeFlyController)self).SetMaxPitch(value is float typed ? typed : 90.0f), 90.0f);
        RegisterAttribute("Mouse Sensitivity", typeof(float), self => ((FreeFlyController)self).GetMouseSensitivity(), (self, value) => ((FreeFlyController)self).SetMouseSensitivity(value is float typed ? typed : DefaultMouseSensitivity), DefaultMouseSensitivity);
        RegisterAttribute("Touch Sensitivity", typeof(float), self => ((FreeFlyController)self).GetTouchSensitivity(), (self, value) => ((FreeFlyController)self).SetTouchSensitivity(value is float typed ? typed : DefaultTouchRotationSensitivity), DefaultTouchRotationSensitivity);
        RegisterAttribute("Axis Sensitivity", typeof(float), self => ((FreeFlyController)self).GetAxisSensitivity(), (self, value) => ((FreeFlyController)self).SetAxisSensitivity(value is float typed ? typed : DefaultAxisRotationSensitivity), DefaultAxisRotationSensitivity);
    }

    public void SetSpeed(float value)
    {
        _speed = Math.Max(value, 0.0f);
    }

    public float GetSpeed() => _speed;

    public void SetAcceleratedSpeed(float value)
    {
        _acceleratedSpeed = Math.Max(value, 0.0f);
    }

    public float GetAcceleratedSpeed() => _acceleratedSpeed;

    public void SetMinPitch(float value)
    {
        _minPitch = value;
    }

    public float GetMinPitch() => _minPitch;

    public void SetMaxPitch(float value)
    {
        _maxPitch = value;
    }

    public float GetMaxPitch() => _maxPitch;

    public void SetMouseSensitivity(float value)
    {
        _mouseSensitivity = value;
    }

    public float GetMouseSensitivity() => _mouseSensitivity;

    public void SetTouchSensitivity(float value)
    {
        _touchSensitivity = value;
    }

    public float GetTouchSensitivity() => _touchSensitivity;

    public void SetAxisSensitivity(float value)
    {
        _axisSensitivity = value;
    }

    public float GetAxisSensitivity() => _axisSensitivity;

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        base.OnNodeSet(previousNode, currentNode);
        _multitouchSubscription?.Dispose();
        _multitouchSubscription = null;
        if (currentNode is not null)
            _multitouchSubscription = SubscribeToEvent<InputEvents.MultitouchEvent>(HandleMultitouch);
        var input = GetSubsystem<Input>();
        if (currentNode is null && _isActive && input is not null)
        {
            input.SetMouseVisible(_oldMouseVisible);
            input.SetMouseMode(_oldMouseMode);
            _isActive = false;
        }
    }

    void ISceneUpdateListener.OnSceneUpdate(float timeStep)
    {
        var input = GetSubsystem<Input>();
        if (!IsEnabledEffective || !IsAttached || input is null)
            return;

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
                HandleKeyboardAndMouse(timeStep);
        }
        else
        {
            HandleKeyboardAndMouse(timeStep);
        }
    }

    void ISceneUpdateListener.OnScenePostUpdate(float timeStep)
    {
    }

    private void HandleKeyboardAndMouse(float timeStep)
    {
        var input = GetSubsystem<Input>();
        if (input is null)
            return;

        UpdateCameraAngles();
        var worldMovement = new Movement
        {
            Rotation = HandleMouse(),
            Translation = HandleKeyboard(timeStep)
        };
        var localMovement = new Movement();

        for (var index = 0u; index < input.GetNumJoysticks(); ++index)
        {
            var state = input.GetJoystickByIndex(index);
            if (!state.HasValue)
                continue;

            var joystick = state.Value;
            var isAccelerometer = joystick.GetNumAxes() == 3 && joystick.GetNumButtons() == 0 && joystick.GetNumHats() == 0;
            if (isAccelerometer)
                continue;

            switch (joystick.DeviceType)
            {
                case JoystickDeviceType.Guitar:
                case JoystickDeviceType.DrumKit:
                case JoystickDeviceType.Throttle:
                    break;
                case JoystickDeviceType.Wheel:
                    worldMovement += HandleWheel(joystick, timeStep);
                    break;
                case JoystickDeviceType.FlightStick:
                    localMovement += HandleFlightStick(joystick, timeStep);
                    break;
                case JoystickDeviceType.GameController:
                    worldMovement += HandleController(joystick, timeStep);
                    break;
                default:
                    worldMovement += HandleGenericJoystick(joystick, timeStep);
                    break;
            }
        }

        if (localMovement.Rotation == Vector3f.Zero)
        {
            SetCameraAngles(_lastKnownEulerAngles + worldMovement.Rotation);
        }
        else
        {
            var eulerAngles = _lastKnownEulerAngles + worldMovement.Rotation;
            eulerAngles.X = MathDefs.Clamp(eulerAngles.X, _minPitch, _maxPitch);
            SetCameraRotation(new Quaternion(eulerAngles) * new Quaternion(localMovement.Rotation));
        }

        var translation = localMovement.Translation + worldMovement.Translation;
        if (translation != Vector3f.Zero)
            Node.Translate(translation, TransformSpace.Local);
    }

    private Vector3f HandleMouse()
    {
        var input = GetSubsystem<Input>();
        if (input is null)
            return Vector3f.Zero;

        var mouseMove = input.GetMouseMove();
        return new Vector3f(_mouseSensitivity * mouseMove.Y, _mouseSensitivity * mouseMove.X, 0.0f);
    }

    private Vector3f HandleKeyboard(float timeStep)
    {
        var input = GetSubsystem<Input>();
        if (input is null)
            return Vector3f.Zero;

        var effectiveSpeed = input.GetKeyDown(Key.LeftShift) || input.GetKeyDown(Key.RightShift)
            ? _acceleratedSpeed
            : _speed;
        var result = Vector3f.Zero;
        if (input.GetScancodeDown(Scancode.W))
            result.Z += effectiveSpeed * timeStep;
        if (input.GetScancodeDown(Scancode.S))
            result.Z -= effectiveSpeed * timeStep;
        if (input.GetScancodeDown(Scancode.A))
            result.X -= effectiveSpeed * timeStep;
        if (input.GetScancodeDown(Scancode.D))
            result.X += effectiveSpeed * timeStep;
        if (input.GetScancodeDown(Scancode.Q))
            result.Y -= effectiveSpeed * timeStep;
        if (input.GetScancodeDown(Scancode.E))
            result.Y += effectiveSpeed * timeStep;
        return result;
    }

    private Vector3f HandleController(float timeStep)
    {
        var input = GetSubsystem<Input>();
        if (input is null) return Vector3f.Zero;
        var acceleration = MathDefs.Clamp(_axisAdapter.Transform(input.GetControllerAxisPosition(ControllerAxis.TriggerLeft)), 0.0f, 1.0f);
        var speed = MathDefs.Lerp(_speed, _acceleratedSpeed, acceleration);
        var result = new Vector3f(
            _axisAdapter.Transform(input.GetControllerAxisPosition(ControllerAxis.LeftX)) * speed * timeStep,
            0.0f,
            -_axisAdapter.Transform(input.GetControllerAxisPosition(ControllerAxis.LeftY)) * speed * timeStep);
        var hat = input.GetHatPosition();
        if ((hat & HatPosition.Up) != 0) result.Z += speed * timeStep;
        if ((hat & HatPosition.Down) != 0) result.Z -= speed * timeStep;
        if ((hat & HatPosition.Left) != 0) result.X -= speed * timeStep;
        if ((hat & HatPosition.Right) != 0) result.X += speed * timeStep;
        return result;
    }

    private Vector3f HandleGenericJoystick(float timeStep)
    {
        var input = GetSubsystem<Input>();
        if (input is null) return Vector3f.Zero;
        var speed = _speed;
        if (input.HasJoystickAxisPosition(4))
        {
            var acceleration = MathDefs.Clamp((1.0f + _axisAdapter.Transform(input.GetJoystickAxisPosition(4))) * 0.5f, 0.0f, 1.0f);
            speed = MathDefs.Lerp(_speed, _acceleratedSpeed, acceleration);
        }
        return new Vector3f(
            _axisAdapter.Transform(input.GetJoystickAxisPosition(0)) * speed * timeStep,
            0.0f,
            -_axisAdapter.Transform(input.GetJoystickAxisPosition(1)) * speed * timeStep);
    }

    private void HandleMultitouch(ref InputEvents.MultitouchEvent value)
    {
        const int multitouchMove = 2;
        if (value.EventType != multitouchMove || !IsAttached) return;
        var sensitivity = _touchSensitivity * 90.0f / 1080.0f;
        if (value.NumFingers == 1)
        {
            UpdateCameraAngles();
            var eulerAngles = _lastKnownEulerAngles;
            eulerAngles.Y -= sensitivity * value.DX;
            eulerAngles.X -= sensitivity * value.DY;
            SetCameraAngles(eulerAngles);
        }
        else if (value.NumFingers == 2)
        {
            var position = Node.Position;
            position += -Node.Right * (sensitivity * value.DX);
            position += Node.Up * (sensitivity * value.DY);
            position += Node.Direction * (sensitivity * (value.DSize.X + value.DSize.Y));
            Node.SetPosition(position);
        }
    }

    private void UpdateCameraAngles()
    {
        var rotation = Node.Rotation;
        if (!_lastKnownCameraRotation.HasValue || !_lastKnownCameraRotation.Value.Equals(rotation))
        {
            _lastKnownCameraRotation = rotation;
            _lastKnownEulerAngles = rotation.EulerAngles;
        }
    }

    private void SetCameraAngles(Vector3f eulerAngles)
    {
        eulerAngles.X = MathDefs.Clamp(eulerAngles.X, _minPitch, _maxPitch);
        _lastKnownEulerAngles = eulerAngles;
        _lastKnownCameraRotation = new Quaternion(eulerAngles);
        Node.SetRotation(_lastKnownCameraRotation.Value);
    }
}
