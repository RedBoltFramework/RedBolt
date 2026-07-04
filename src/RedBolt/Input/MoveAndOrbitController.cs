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

    private Input? input;
    private InputMap? inputMap;
    private MoveAndOrbitComponent? component;
    private bool connectToComponentCalled;

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
        SetInputMapAttr(new ResourceRef(StringHash.Calculate(nameof(InputMap)), name));
    }

    public void SetInput(Input? input)
    {
        this.input = input;
        if (inputMap is not null)
            inputMap.SetInput(input);
    }

    public Input? GetInput() => input;

    public void SetInputMap(InputMap? inputMap)
    {
        this.inputMap = inputMap;
        if (this.inputMap is not null)
            this.inputMap.SetInput(input);
    }

    public InputMap? GetInputMap() => inputMap;

    public void SetInputMapAttr(ResourceRef value)
    {
        if (ResourceRef.IsNullOrEmpty(value))
        {
            inputMap = null;
            return;
        }

        inputMap = new InputMap(value.Name, input);
    }

    public ResourceRef GetInputMapAttr()
    {
        return inputMap is null
            ? ResourceRef.Empty
            : new ResourceRef(StringHash.Calculate(nameof(InputMap)), inputMap.Name);
    }

    protected internal override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        connectToComponentCalled = false;
        if (currentNode is null)
            component = null;
    }

    void ISceneUpdateListener.OnSceneUpdate(float timeStep)
    {
        if (!IsEnabledEffective || input is null)
            return;

        if (!connectToComponentCalled)
            ConnectToComponent();

        if (component is null)
            return;

        var yaw = component.GetYaw();
        var pitch = component.GetPitch();
        var originalYaw = yaw;
        var originalPitch = pitch;

        var forward = Evaluate(ActionForward) - Evaluate(ActionBack);
        var right = Evaluate(ActionRight) - Evaluate(ActionLeft);
        var turnRight = Evaluate(ActionTurnRight) - Evaluate(ActionTurnLeft);
        var lookDown = Evaluate(ActionLookDown) - Evaluate(ActionLookUp);

        var axisSensitivity = GetSensitivity(AxisRotationSensitivityMetadata, DefaultAxisRotationSensitivity);
        yaw += turnRight * axisSensitivity * timeStep;
        pitch += lookDown * axisSensitivity * timeStep;

        if (input.MouseMode != MouseMode.Free || input.GetMouseButtonDown(MouseButton.Right))
        {
            var sensitivity = GetSensitivity(MouseSensitivityMetadata, DefaultMouseSensitivity);
            var mouseMove = input.GetMouseMove();
            yaw += mouseMove.X * sensitivity;
            pitch += mouseMove.Y * sensitivity;
        }

        pitch = MathDefs.Clamp(pitch, -90.0f, 90.0f);
        if (!MathDefs.ApproximatelyEquivalent(originalYaw, yaw))
            component.SetYaw(yaw);
        if (!MathDefs.ApproximatelyEquivalent(originalPitch, pitch))
            component.SetPitch(pitch);

        var velocity = new Vector3(MathDefs.Clamp(right, -1.0f, 1.0f), 0.0f, MathDefs.Clamp(forward, -1.0f, 1.0f));
        if (!component.GetVelocity().ApproximatelyEquivalent(velocity))
            component.SetVelocity(velocity);
    }

    void ISceneUpdateListener.OnScenePostUpdate(float timeStep)
    {
    }

    private void ConnectToComponent()
    {
        component = IsAttached ? Node.GetDerivedComponent<MoveAndOrbitComponent>() : null;
        connectToComponentCalled = true;
    }

    private float Evaluate(string action)
    {
        return input is not null && inputMap is not null ? inputMap.Evaluate(action, input) : 0.0f;
    }

    private float GetSensitivity(string key, float defaultValue)
    {
        if (inputMap?.GetMetadata(key) is float sensitivity && !MathDefs.ApproximatelyEquivalent(sensitivity, 0.0f))
            return sensitivity;

        return defaultValue;
    }
}
