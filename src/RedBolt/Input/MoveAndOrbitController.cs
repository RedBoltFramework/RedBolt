using Microsoft.Extensions.Logging;

namespace RedBolt;

public class MoveAndOrbitController : Component
{
    public const float DefaultMouseSensitivity = 0.1f;
    //Full motion per inch
    public const float DefaultTouchMovementSensitivity = 1.0f;
    //90' motion per inch
    public const float DefaultTouchRotationSensitivity = 1.0f;
    //Degrees per second
    public const float DefaultAxisRotationSensitivity = 100.0f;

    public const string MouseSensitivity = "MouseSensitivity";
    public const string AxisRotationSensitivity = "AxisRotationSensitivity";
    public const string TouchMovementSensitivity = "TouchMovementSensitivity";
    public const string TouchRotationSensitivity = "TouchRotationSensitivity";

    public const string ActionForward = "Forward";
    public const string ActionBack = "Back";
    public const string ActionLeft = "Left";
    public const string ActionRight = "Right";
    public const string ActionTurnLeft = "TurnLeft";
    public const string ActionTurnRight = "TurnRight";
    public const string ActionLookUp = "LookUp";
    public const string ActionLookDown = "LookDown";

    /// <summary>Construct.</summary>
    public MoveAndOrbitController()
    {
        UpdateEventSubscription();

        RegisterAttribute(
            "Input Map",
            typeof(ResourceRef),
            self => ((MoveAndOrbitController)self).GetInputMapAttr(),
            (self, value) => ((MoveAndOrbitController)self).SetInputMapAttr(value is ResourceRef resourceRef ? resourceRef : ResourceRef.Empty),
            new ResourceRef(ObjectReflection<InputMap>.TypeId));
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Register object factory and attributes.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<MoveAndOrbitController>(ObjectCategory.Category_Logic);
    }

    /// <summary>Apply attribute changes that can not be applied immediately. Called after scene load or a network update.</summary>
    public override void ApplyAttributes()
    {
        base.ApplyAttributes();
    }

    /// <summary>Handle enabled/disabled state change. Changes update event subscription.</summary>
    public override void OnSetEnabled()
    {
        base.OnSetEnabled();
    }

    /// <summary>Load input map from config file or resource.</summary>
    public void LoadInputMap(string name)
    {
        SetInputMapAttr(new ResourceRef(ObjectReflection<InputMap>.TypeId, name));
    }

    /// <summary>Set input map.</summary>
    public void SetInputMap(InputMap? inputMap)
    {
        _inputMap = inputMap;
    }

    /// <summary>Return input map.</summary>
    public InputMap? GetInputMap()
    {
        return _inputMap;
    }

    /// <summary>Set input map attribute.</summary>
    public void SetInputMapAttr(ResourceRef value)
    {
        SetInputMap(InputMap.Load(value.Name));
    }

    /// <summary>Return input map attribute.</summary>
    public ResourceRef GetInputMapAttr()
    {
        return Resource.GetResourceRef(GetInputMap(), ObjectReflection<InputMap>.TypeId);
    }

    // TODO: port: void SetMovementUIElement(UIElement* element);
    // TODO: port: void SetRotationUIElement(UIElement* element);
    // TODO: port: UIElement* GetMovementUIElement() const { return movementUIElement_; }
    // TODO: port: UIElement* GetRotationUIElement() const { return rotationUIElement_; }

    /// <summary>Handle scene node being assigned at creation.</summary>
    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        base.OnNodeSet(previousNode, currentNode);

        _connectToComponentCalled = false;
        UpdateEventSubscription();
    }

    /// <summary>Handle scene being assigned.</summary>
    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        UpdateEventSubscription();
    }

    /// <summary>Called on input end.</summary>
    private void HandleInputEnd(ref InputEvents.InputEndEvent eventData)
    {
        if (!_connectToComponentCalled)
        {
            ConnectToComponent();
        }

        if (_component is null)
            return;

        var input = GetSubsystem<Input>()!;
        var yaw = _component.GetYaw();
        var pitch = _component.GetPitch();
        var originalYaw = yaw;
        var originalPitch = pitch;
        var originalVelocity = _component.GetVelocity();

        var forward = 0.0f;
        var right = 0.0f;
        var turnRight = 0.0f;
        var lookDown = 0.0f;

        if (_inputMap is not null)
        {
            forward = _inputMap.Evaluate(ActionForward) - _inputMap.Evaluate(ActionBack);
            right = _inputMap.Evaluate(ActionRight) - _inputMap.Evaluate(ActionLeft);
            turnRight = _inputMap.Evaluate(ActionTurnRight) - _inputMap.Evaluate(ActionTurnLeft);
            lookDown = _inputMap.Evaluate(ActionLookDown) - _inputMap.Evaluate(ActionLookUp);
        }

        {
            var sensitivity = GetSensitivity(AxisRotationSensitivity, DefaultAxisRotationSensitivity);
            var timeStep = GetSubsystem<Time>()!.GetTimeStep();
            yaw += turnRight * sensitivity * timeStep;
            pitch += lookDown * sensitivity * timeStep;
        }

        EvaluateTouchRects(out var movementRect, out var rotationRect);
        TouchState? movementTouch = null;
        TouchState? rotationTouch = null;
        FindTouchStates(movementRect, rotationRect, ref movementTouch, ref rotationTouch);

        var dpi = GetSubsystem<Graphics>()!.GetDisplayDPI().Z;
        dpi = dpi != 0.0f ? dpi : 96.0f;

        if (movementTouch is not null)
        {
            var sensitivity = GetSensitivity(TouchMovementSensitivity, DefaultTouchMovementSensitivity);
            var halfAreaSize = Math.Min(movementRect.Width, movementRect.Height) * 0.45f;
            var fullMotion = Math.Min(dpi / sensitivity, halfAreaSize);
            var delta = movementTouch.Value.Position - _movementTouchOrigin;
            right += MathDefs.Clamp(delta.X / fullMotion, -1.0f, 1.0f);
            forward -= MathDefs.Clamp(delta.Y / fullMotion, -1.0f, 1.0f);
        }

        if (rotationTouch is not null)
        {
            var sensitivity = GetSensitivity(TouchRotationSensitivity, DefaultTouchRotationSensitivity);
            var halfAreaSize = Math.Min(rotationRect.Width, rotationRect.Height) * 0.45f;
            var halfPiDistance = Math.Min(dpi / sensitivity, halfAreaSize);
            yaw += rotationTouch.Value.Delta.X / halfPiDistance * 90.0f;
            pitch += rotationTouch.Value.Delta.Y / halfPiDistance * 90.0f;
        }

        if (input.MouseMode != MouseMode.Free || input.GetMouseButtonDown(MouseButton.Right))
        {
            var sensitivity = GetSensitivity(MouseSensitivity, DefaultMouseSensitivity);
            var move = input.GetMouseMove();
            yaw += move.X * sensitivity;
            pitch += move.Y * sensitivity;
        }
        pitch = MathDefs.Clamp(pitch, -90.0f, 90.0f);
        if (!MathDefs.ApproximatelyEquivalent(originalPitch, pitch))
            _component.SetPitch(pitch);
        if (!MathDefs.ApproximatelyEquivalent(originalYaw, yaw))
            _component.SetYaw(yaw);
        var velocity = new Vector3f(MathDefs.Clamp(right, -1.0f, 1.0f), 0.0f, MathDefs.Clamp(forward, -1.0f, 1.0f));
        if (!originalVelocity.ApproximatelyEquivalent(velocity))
            _component.SetVelocity(velocity);
    }

    /// <summary>Subscribe/unsubscribe to update events based on current enabled state and update event mask.</summary>
    private void UpdateEventSubscription()
    {
        var subscribe = IsEnabledEffective && (Scene is not null && Scene.IsUpdateEnabled());
        if (_subscribed != subscribe)
        {
            _subscribed = subscribe;
            if (_subscribed)
            {
                SubscribeToEvent<InputEvents.InputEndEvent>(HandleInputEnd);
            }
            else
            {
                UnsubscribeFromEvent<InputEvents.InputEndEvent>();
            }
        }
    }

    /// <summary>Connect to MoveAndOrbitComponent if possible.</summary>
    private void ConnectToComponent()
    {
        if (IsAttached)
        {
            _component = Node.GetDerivedComponent<MoveAndOrbitComponent>();
            if (_component is null)
            {
                Log.Logger.LogError("MoveAndOrbitComponent not found on the same node as MoveAndOrbitController");
            }
        }
        else
        {
            _component = null;
        }

        _connectToComponentCalled = true;
    }

    /// <summary>Evaluate active touch areas.</summary>
    private void EvaluateTouchRects(out IntRect movementRect, out IntRect rotationRect)
    {
        var screenRect = IntRect.Zero;

        var renderer = GetSubsystem<Renderer>();
        var viewport = renderer?.GetViewportForScene(Scene, 0);
        if (viewport is not null)
        {
            screenRect = viewport.GetRect();
        }

        if (screenRect == IntRect.Zero)
        {
            var graphics = GetSubsystem<Graphics>();
            if (graphics is not null)
            {
                screenRect = new IntRect(Vector2i.Zero, new Vector2i(graphics.Width, graphics.Height));
            }
        }

        // TODO: port:
        // if (movementUIElement_)
        //     movementRect = movementUIElement_->GetCombinedScreenRect();
        // else
        movementRect = screenRect;
        // TODO: port:
        // if (rotationUIElement_)
        //     rotationRect = rotationUIElement_->GetCombinedScreenRect();
        // else
        rotationRect = screenRect;
        // TODO: port: if (movementUIElement_ == rotationUIElement_)
        // TODO: port: {
        var halfSize = new Vector2i(movementRect.Width / 2, movementRect.Height);
        movementRect = new IntRect(movementRect.Min, movementRect.Min + halfSize);
        rotationRect = new IntRect(rotationRect.Min + new Vector2i(halfSize.X, 0), rotationRect.Max);
        // TODO: port: }
    }

    /// <summary>Find touch states for movement and rotation.</summary>
    private void FindTouchStates(in IntRect movementRect, in IntRect rotationRect,
        ref TouchState? movementTouch, ref TouchState? rotationTouch)
    {
        var input = GetSubsystem<Input>()!;
        for (var touchIndex = 0u; touchIndex < input.GetNumTouches(); ++touchIndex)
        {
            var touchValue = input.GetTouch(touchIndex)!.Value;
            if (_movementTouchId < 0 && touchValue.TouchedElement is null
                && movementRect.Contains(touchValue.Position))
            {
                _movementTouchId = touchValue.TouchID;
                _movementTouchOrigin = touchValue.Position;
            }
            if (touchValue.TouchID == _movementTouchId)
            {
                movementTouch = touchValue;
            }
            if (_rotationTouchId < 0 && touchValue.TouchedElement is null
                && rotationRect.Contains(touchValue.Position))
            {
                _rotationTouchId = touchValue.TouchID;
            }
            if (touchValue.TouchID == _rotationTouchId)
            {
                rotationTouch = touchValue;
            }
        }
        if (movementTouch is null)
        {
            _movementTouchId = -1;
        }
        if (rotationTouch is null)
        {
            _rotationTouchId = -1;
        }
    }

    /// <summary>Get sensitivity value from input map or default one.</summary>
    private float GetSensitivity(string key, float defaultValue)
    {
        if (_inputMap is null)
            return defaultValue;
        var sensitivity = _inputMap.GetMetadata(key).GetFloat();
        if (sensitivity == 0.0f)
            return defaultValue;
        return sensitivity;
    }

    /// <summary>Input map.</summary>
    private InputMap? _inputMap;
    /// <summary>Component that provides animation states for the model.</summary>
    private MoveAndOrbitComponent? _component;

    // TODO: port: WeakPtr<UIElement> movementUIElement_{};
    // TODO: port: WeakPtr<UIElement> rotationUIElement_{};
    /// <summary>Is controller subscribed to events.</summary>
    private bool _subscribed;
    /// <summary>Is ConnectToComponent already called for the current node.</summary>
    private bool _connectToComponentCalled;
    /// <summary>Touch id for movement gesture.</summary>
    private int _movementTouchId = -1;
    /// <summary>Touch id for rotation gesture.</summary>
    private int _rotationTouchId = -1;
    /// <summary>Position where the touch gesture started.</summary>
    private Vector2i _movementTouchOrigin = Vector2i.Zero;
}
