namespace RedBolt;

public sealed class FreeFlyController : Component, ISceneUpdateListener
{
    public const float DefaultMouseSensitivity = 0.1f;
    public const float DefaultTouchRotationSensitivity = 1.0f;
    public const float DefaultAxisRotationSensitivity = 100.0f;

    private float speed = 20.0f;
    private float acceleratedSpeed = 100.0f;
    private float mouseSensitivity = DefaultMouseSensitivity;
    private float touchSensitivity = DefaultTouchRotationSensitivity;
    private float axisSensitivity = DefaultAxisRotationSensitivity;
    private float minPitch = -90.0f;
    private float maxPitch = 90.0f;
    private Input? input;
    private bool isActive;
    private bool oldMouseVisible = true;
    private MouseMode oldMouseMode = MouseMode.Absolute;
    private Quaternion? lastKnownCameraRotation;
    private Vector3 lastKnownEulerAngles;

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
        this.input = input;
    }

    public Input? GetInput() => input;

    public void SetSpeed(float value)
    {
        speed = Math.Max(value, 0.0f);
    }

    public float GetSpeed() => speed;

    public void SetAcceleratedSpeed(float value)
    {
        acceleratedSpeed = Math.Max(value, 0.0f);
    }

    public float GetAcceleratedSpeed() => acceleratedSpeed;

    public void SetMinPitch(float value)
    {
        minPitch = value;
    }

    public float GetMinPitch() => minPitch;

    public void SetMaxPitch(float value)
    {
        maxPitch = value;
    }

    public float GetMaxPitch() => maxPitch;

    public void SetMouseSensitivity(float value)
    {
        mouseSensitivity = value;
    }

    public float GetMouseSensitivity() => mouseSensitivity;

    public void SetTouchSensitivity(float value)
    {
        touchSensitivity = value;
    }

    public float GetTouchSensitivity() => touchSensitivity;

    public void SetAxisSensitivity(float value)
    {
        axisSensitivity = value;
    }

    public float GetAxisSensitivity() => axisSensitivity;

    protected internal override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (currentNode is null && isActive && input is not null)
        {
            input.SetMouseVisible(oldMouseVisible);
            input.SetMouseMode(oldMouseMode);
            isActive = false;
        }
    }

    void ISceneUpdateListener.OnSceneUpdate(float timeStep)
    {
        if (!IsEnabledEffective || !IsAttached || input is null)
            return;

        if (isActive || input.MouseMode == MouseMode.Free)
        {
            if (!isActive && input.GetMouseButtonPress(MouseButton.Right))
            {
                isActive = true;
                oldMouseVisible = input.IsMouseVisible();
                oldMouseMode = input.MouseMode;
                input.SetMouseVisible(false);
                input.SetMouseMode(MouseMode.Relative);
            }
            else if (isActive && !input.GetMouseButtonDown(MouseButton.Right))
            {
                isActive = false;
                input.SetMouseVisible(oldMouseVisible);
                input.SetMouseMode(oldMouseMode);
            }

            if (isActive)
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
        var eulerAngles = lastKnownEulerAngles + HandleMouse();
        SetCameraAngles(eulerAngles);

        var translation = HandleKeyboard(timeStep);
        if (translation != Vector3.Zero)
            Node.Translate(translation, TransformSpace.Local);
    }

    private Vector3 HandleMouse()
    {
        if (input is null)
            return Vector3.Zero;

        var mouseMove = input.GetMouseMove();
        return new Vector3(mouseSensitivity * mouseMove.Y, mouseSensitivity * mouseMove.X, 0.0f);
    }

    private Vector3 HandleKeyboard(float timeStep)
    {
        if (input is null)
            return Vector3.Zero;

        var effectiveSpeed = input.GetKeyDown(Key.LeftShift) || input.GetKeyDown(Key.RightShift)
            ? acceleratedSpeed
            : speed;
        var result = Vector3.Zero;
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

    private void UpdateCameraAngles()
    {
        var rotation = Node.Rotation;
        if (!lastKnownCameraRotation.HasValue || !lastKnownCameraRotation.Value.Equals(rotation))
        {
            lastKnownCameraRotation = rotation;
            lastKnownEulerAngles = rotation.EulerAngles;
        }
    }

    private void SetCameraAngles(Vector3 eulerAngles)
    {
        eulerAngles.X = MathDefs.Clamp(eulerAngles.X, minPitch, maxPitch);
        lastKnownEulerAngles = eulerAngles;
        lastKnownCameraRotation = new Quaternion(eulerAngles);
        Node.SetRotation(lastKnownCameraRotation.Value);
    }
}
