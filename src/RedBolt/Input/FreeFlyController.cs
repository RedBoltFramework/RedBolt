namespace RedBolt;

public sealed class FreeFlyController : Component, ISceneUpdateListener
{
    public const float DefaultMouseSensitivity = 0.1f;
    public const float DefaultTouchRotationSensitivity = 1.0f;
    public const float DefaultAxisRotationSensitivity = 100.0f;

    private float _speed = 20.0f;
    private float _acceleratedSpeed = 100.0f;
    private float _mouseSensitivity = DefaultMouseSensitivity;
    private float _touchSensitivity = DefaultTouchRotationSensitivity;
    private float _axisSensitivity = DefaultAxisRotationSensitivity;
    private float _minPitch = -90.0f;
    private float _maxPitch = 90.0f;
    private Input? _input;
    private bool _isActive;
    private bool _oldMouseVisible = true;
    private MouseMode _oldMouseMode = MouseMode.Absolute;
    private Quaternion? _lastKnownCameraRotation;
    private Vector3 _lastKnownEulerAngles;

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

    public void SetInput(Input? input)
    {
        this._input = input;
    }

    public Input? GetInput() => _input;

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

    protected internal override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (currentNode is null && _isActive && _input is not null)
        {
            _input.SetMouseVisible(_oldMouseVisible);
            _input.SetMouseMode(_oldMouseMode);
            _isActive = false;
        }
    }

    void ISceneUpdateListener.OnSceneUpdate(float timeStep)
    {
        if (!IsEnabledEffective || !IsAttached || _input is null)
            return;

        var dearImGui = Context.Instance.GetSubsystem<IDearImGuiInputSink>();
        if (dearImGui?.WantCaptureMouse == true || dearImGui?.WantCaptureKeyboard == true)
            return;

        if (_isActive || _input.MouseMode == MouseMode.Free)
        {
            if (!_isActive && _input.GetMouseButtonPress(MouseButton.Right))
            {
                _isActive = true;
                _oldMouseVisible = _input.IsMouseVisible();
                _oldMouseMode = _input.MouseMode;
                _input.SetMouseVisible(false);
                _input.SetMouseMode(MouseMode.Relative);
            }
            else if (_isActive && !_input.GetMouseButtonDown(MouseButton.Right))
            {
                _isActive = false;
                _input.SetMouseVisible(_oldMouseVisible);
                _input.SetMouseMode(_oldMouseMode);
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
        UpdateCameraAngles();
        var eulerAngles = _lastKnownEulerAngles + HandleMouse();
        SetCameraAngles(eulerAngles);

        var translation = HandleKeyboard(timeStep);
        if (translation != Vector3.Zero)
            Node.Translate(translation, TransformSpace.Local);
    }

    private Vector3 HandleMouse()
    {
        if (_input is null)
            return Vector3.Zero;

        var mouseMove = _input.GetMouseMove();
        return new Vector3(_mouseSensitivity * mouseMove.Y, _mouseSensitivity * mouseMove.X, 0.0f);
    }

    private Vector3 HandleKeyboard(float timeStep)
    {
        if (_input is null)
            return Vector3.Zero;

        var effectiveSpeed = _input.GetKeyDown(Key.LeftShift) || _input.GetKeyDown(Key.RightShift)
            ? _acceleratedSpeed
            : _speed;
        var result = Vector3.Zero;
        if (_input.GetScancodeDown(Scancode.W))
            result.Z += effectiveSpeed * timeStep;
        if (_input.GetScancodeDown(Scancode.S))
            result.Z -= effectiveSpeed * timeStep;
        if (_input.GetScancodeDown(Scancode.A))
            result.X -= effectiveSpeed * timeStep;
        if (_input.GetScancodeDown(Scancode.D))
            result.X += effectiveSpeed * timeStep;
        if (_input.GetScancodeDown(Scancode.Q))
            result.Y -= effectiveSpeed * timeStep;
        if (_input.GetScancodeDown(Scancode.E))
            result.Y += effectiveSpeed * timeStep;
        return result;
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

    private void SetCameraAngles(Vector3 eulerAngles)
    {
        eulerAngles.X = MathDefs.Clamp(eulerAngles.X, _minPitch, _maxPitch);
        _lastKnownEulerAngles = eulerAngles;
        _lastKnownCameraRotation = new Quaternion(eulerAngles);
        Node.SetRotation(_lastKnownCameraRotation.Value);
    }
}
