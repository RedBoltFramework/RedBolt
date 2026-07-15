namespace RedBolt;

public sealed class MoveAndOrbitController : Component, ISceneUpdateListener
{
    public const float DefaultMouseSensitivity = 0.1f;
    public const float DefaultTouchMovementSensitivity = 1.0f;
    public const float DefaultTouchRotationSensitivity = 1.0f;
    public const float DefaultAxisRotationSensitivity = 100.0f;

    public const string MouseSensitivityMetadata = "MouseSensitivity";
    public const string AxisRotationSensitivityMetadata = "AxisRotationSensitivity";
    public const string TouchMovementSensitivityMetadata = "TouchMovementSensitivity";
    public const string TouchRotationSensitivityMetadata = "TouchRotationSensitivity";

    public const string ActionForward = "Forward";
    public const string ActionBack = "Back";
    public const string ActionLeft = "Left";
    public const string ActionRight = "Right";
    public const string ActionTurnLeft = "TurnLeft";
    public const string ActionTurnRight = "TurnRight";
    public const string ActionLookUp = "LookUp";
    public const string ActionLookDown = "LookDown";

    private Input? _input;
    private InputMap? _inputMap;
    private MoveAndOrbitComponent? _component;
    private bool _connectToComponentCalled;
    private object? _movementUIElement;
    private object? _rotationUIElement;

    public MoveAndOrbitController()
    {
        RegisterAttribute(
            "Input Map",
            typeof(ResourceRef),
            self => ((MoveAndOrbitController)self).GetInputMapAttr(),
            (self, value) => ((MoveAndOrbitController)self).SetInputMapAttr(value is ResourceRef resourceRef ? resourceRef : ResourceRef.Empty),
            ResourceRef.Empty);
    }

    public void LoadInputMap(string name)
    {
        SetInputMapAttr(new ResourceRef(new StringHash(nameof(InputMap)), name));
    }

    public void SetInput(Input? input)
    {
        this._input = input;
        if (_inputMap is not null)
            _inputMap.SetInput(input);
    }

    public Input? GetInput() => _input;

    public void SetInputMap(InputMap? inputMap)
    {
        this._inputMap = inputMap;
        if (this._inputMap is not null)
            this._inputMap.SetInput(_input);
    }

    public InputMap? GetInputMap() => _inputMap;

    public void SetInputMapAttr(ResourceRef value)
    {
        if (ResourceRef.IsNullOrEmpty(value))
        {
            _inputMap = null;
            return;
        }

        var cache = GetSubsystem<ResourceCache>()!;
        _inputMap = cache.GetResourceOrNull<InputMap>(value.Name) ?? new InputMap(value.Name, _input);
        _inputMap.SetInput(_input);
    }

    public ResourceRef GetInputMapAttr()
    {
        return _inputMap is null
            ? ResourceRef.Empty
            : new ResourceRef(new StringHash(nameof(InputMap)), _inputMap.Name);
    }

    public void SetMovementUIElement(object? element) => _movementUIElement = element;
    public object? GetMovementUIElement() => _movementUIElement;
    public void SetRotationUIElement(object? element) => _rotationUIElement = element;
    public object? GetRotationUIElement() => _rotationUIElement;

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        base.OnNodeSet(previousNode, currentNode);
        _connectToComponentCalled = false;
        if (currentNode is null)
            _component = null;
    }

    void ISceneUpdateListener.OnSceneUpdate(float timeStep)
    {
        if (!IsEnabledEffective || _input is null)
            return;

        if (!_connectToComponentCalled)
            ConnectToComponent();

        if (_component is null)
            return;

        var yaw = _component.GetYaw();
        var pitch = _component.GetPitch();
        var originalYaw = yaw;
        var originalPitch = pitch;

        var forward = Evaluate(ActionForward) - Evaluate(ActionBack);
        var right = Evaluate(ActionRight) - Evaluate(ActionLeft);
        var turnRight = Evaluate(ActionTurnRight) - Evaluate(ActionTurnLeft);
        var lookDown = Evaluate(ActionLookDown) - Evaluate(ActionLookUp);

        var axisSensitivity = GetSensitivity(AxisRotationSensitivityMetadata, DefaultAxisRotationSensitivity);
        yaw += turnRight * axisSensitivity * timeStep;
        pitch += lookDown * axisSensitivity * timeStep;

        if (_input.MouseMode != MouseMode.Free || _input.GetMouseButtonDown(MouseButton.Right))
        {
            var sensitivity = GetSensitivity(MouseSensitivityMetadata, DefaultMouseSensitivity);
            var mouseMove = _input.GetMouseMove();
            yaw += mouseMove.X * sensitivity;
            pitch += mouseMove.Y * sensitivity;
        }

        pitch = MathDefs.Clamp(pitch, -90.0f, 90.0f);
        if (!MathDefs.ApproximatelyEquivalent(originalYaw, yaw))
            _component.SetYaw(yaw);
        if (!MathDefs.ApproximatelyEquivalent(originalPitch, pitch))
            _component.SetPitch(pitch);

        var velocity = new Vector3f(MathDefs.Clamp(right, -1.0f, 1.0f), 0.0f, MathDefs.Clamp(forward, -1.0f, 1.0f));
        if (!_component.GetVelocity().ApproximatelyEquivalent(velocity))
            _component.SetVelocity(velocity);
    }

    void ISceneUpdateListener.OnScenePostUpdate(float timeStep)
    {
    }

    private void ConnectToComponent()
    {
        _component = IsAttached ? Node.GetDerivedComponent<MoveAndOrbitComponent>() : null;
        _connectToComponentCalled = true;
    }

    private float Evaluate(string action)
    {
        return _input is not null && _inputMap is not null ? _inputMap.Evaluate(action, _input) : 0.0f;
    }

    private float GetSensitivity(string key, float defaultValue)
    {
        if (_inputMap is not null)
        {
            var value = _inputMap.GetMetadata(key);
            if (value.Type != VariantType.None)
            {
                var sensitivity = value.GetFloat();
                if (!MathDefs.ApproximatelyEquivalent(sensitivity, 0.0f))
                    return sensitivity;
            }
        }

        return defaultValue;
    }
}
