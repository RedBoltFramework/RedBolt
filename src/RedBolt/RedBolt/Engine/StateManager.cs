using Microsoft.Extensions.Logging;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Scene;
namespace RedBolt.Engine;
using Math = global::System.Math;

using Scene = global::RedBolt.Scene.Scene;

/// <summary>Base class for an application state. Examples of a state would be a loading screen, a menu or a game screen.</summary>
public class ApplicationState : RedObject
{
    private bool _active;
    private readonly List<Viewport?> _viewports = [];
    private bool _mouseVisible = true;
    private bool _mouseGrabbed;
    private MouseMode _mouseMode = MouseMode.Free;
    private Color _fogColor = new(0.0f, 0.0f, 0.0f);
    private Color _savedFogColor = new();
    /// <summary>Construct.</summary>
    public ApplicationState()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _viewports.Clear();
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        var reflection = context.AddReflection<ApplicationState>();
        reflection.SetObjectFactory(static () => new ApplicationState());
    }

    /// <summary>Activate game state. Executed by StateManager.</summary>
    public virtual void Activate(StringVariantMap bundle)
    {
        if (_active)
            return;

        _active = true;

        // Subscribe HandleUpdate() method for processing update events
        SubscribeToEvent<CoreEvents.UpdateEvent>(HandleUpdate);

        var input = GetSubsystem<InputSystem>()!;
        InitMouseMode();
        input.SetMouseVisible(_mouseVisible);
        input.SetMouseGrabbed(_mouseGrabbed);

        var renderer = GetSubsystem<Renderer>()!;
        _savedFogColor = renderer.GetDefaultZone().GetFogColor();
        renderer.GetDefaultZone().SetFogColor(_fogColor);

        if (_viewports.Count != 0)
        {
            renderer.SetNumViewports((uint)_viewports.Count);
            for (var i = 0; i < _viewports.Count; ++i)
                renderer.SetViewport((uint)i, _viewports[i]);
        }
    }

    /// <summary>Transition into the state complete. Executed by StateManager.</summary>
    public virtual void TransitionComplete()
    {
    }

    /// <summary>Return true if state is ready to be deactivated. Executed by StateManager.</summary>
    public virtual bool CanLeaveState() => true;

    /// <summary>Transition out of the state started. Executed by StateManager.</summary>
    public virtual void TransitionStarted()
    {
    }

    /// <summary>Deactivate game state. Executed by StateManager.</summary>
    public virtual void Deactivate()
    {
        if (!_active)
            return;
        _active = false;

        // Subscribe HandleUpdate() method for processing update events
        UnsubscribeFromEvent<CoreEvents.UpdateEvent>();

        var renderer = GetSubsystem<Renderer>()!;
        _fogColor = renderer.GetDefaultZone().GetFogColor();
        renderer.GetDefaultZone().SetFogColor(_savedFogColor);

        if (_viewports.Count != 0)
            renderer.SetNumViewports(0);
    }

    /// <summary>Handle the logic update event.</summary>
    public virtual void Update(float timeStep)
    {
    }

    /// <summary>Get activation flag. Returns true if game screen is active.</summary>
    public bool IsActive => _active;

    /// <summary>Set whether the operating system mouse cursor is visible.</summary>
    public void SetMouseVisible(bool enable)
    {
        _mouseVisible = enable;
        if (IsActive)
            GetSubsystem<InputSystem>()!.SetMouseVisible(enable);
    }

    /// <summary>Set whether the mouse is currently being grabbed by an operation.</summary>
    public void SetMouseGrabbed(bool grab)
    {
        _mouseGrabbed = grab;
        if (IsActive)
            GetSubsystem<InputSystem>()!.SetMouseGrabbed(_mouseGrabbed);
    }

    /// <summary>Set the mouse mode.</summary>
    public void SetMouseMode(MouseMode mode)
    {
        _mouseMode = mode;
        if (IsActive)
            InitMouseMode();
    }

    /// <summary>Return whether the operating system mouse cursor is visible.</summary>
    public bool IsMouseVisible => _mouseVisible;

    /// <summary>Return whether the mouse is currently being grabbed by an operation.</summary>
    public bool IsMouseGrabbed => _mouseGrabbed;

    /// <summary>Return the mouse mode.</summary>
    public MouseMode MouseMode => _mouseMode;

    /// <summary>Set number of backbuffer viewports to render.</summary>
    public void SetNumViewports(int num)
    {
        while (_viewports.Count < num)
            _viewports.Add(null);
        if (_viewports.Count > num)
            _viewports.RemoveRange(num, _viewports.Count - num);

        if (_active)
            GetSubsystem<Renderer>()!.SetNumViewports((uint)num);
    }

    /// <summary>Set a backbuffer viewport.</summary>
    public void SetViewport(int index, Viewport? viewport)
    {
        while (_viewports.Count <= index)
            _viewports.Add(null);

        _viewports[index] = viewport;
        if (_active)
            GetSubsystem<Renderer>()!.SetViewport((uint)index, viewport);
    }

    /// <summary>Set default zone fog color.</summary>
    public void SetDefaultFogColor(Color color)
    {
        _fogColor = color;
        if (_active)
            GetSubsystem<Renderer>()!.GetDefaultZone().SetFogColor(color);
    }

    /// <summary>Return backbuffer viewport by index.</summary>
    public Viewport? GetViewport(int index) => index >= 0 && index < _viewports.Count ? _viewports[index] : null;

    /// <summary>Return nth backbuffer viewport associated to a scene. Index 0 returns the first.</summary>
    public Viewport? GetViewportForScene(Scene? scene, int index)
    {
        for (var i = 0; i < _viewports.Count; ++i)
        {
            var viewport = _viewports[i];
            if (viewport is not null && viewport.GetScene() == scene)
            {
                if (index == 0)
                    return viewport;
                --index;
            }
        }
        return null;
    }

    /// <summary>Get default zone fog color.</summary>
    public ref readonly Color DefaultFogColor => ref _fogColor;

    // TODO: port URHO3D_ACTIONS when the Actions subsystem is available.
    // ActionManager* GetActionManager() const { return actionManager_; }
    // Actions::ActionState* AddAction(Actions::BaseAction* action, Object* target, bool paused = false);

    /// <summary>Initialize mouse mode on non-web platform.</summary>
    private void InitMouseMode()
    {
        var input = GetSubsystem<InputSystem>()!;

        if (ProcessUtils.GetPlatform() != PlatformId.Web)
        {
            input.SetMouseMode(_mouseMode);
        }
        else
        {
            input.SetMouseVisible(true);
            SubscribeToEvent<InputEvents.MouseButtonDownEvent>(HandleMouseModeRequest);
            SubscribeToEvent<InputEvents.MouseModeChangedEvent>(HandleMouseModeChange);
        }
    }

    /// <summary>Handle request for mouse mode on web platform.</summary>
    private void HandleMouseModeRequest(ref InputEvents.MouseButtonDownEvent eventData)
    {
        var input = GetSubsystem<InputSystem>()!;
        if (_mouseMode == MouseMode.Absolute)
            input.SetMouseVisible(false);
        else if (_mouseMode == MouseMode.Free)
            input.SetMouseVisible(true);
        input.SetMouseMode(_mouseMode);
    }

    /// <summary>Handle request for mouse mode change on web platform.</summary>
    private void HandleMouseModeChange(ref InputEvents.MouseModeChangedEvent eventData)
    {
        GetSubsystem<InputSystem>()!.SetMouseVisible(!eventData.MouseLocked);
    }

    /// <summary>Handle the logic update event.</summary>
    private void HandleUpdate(ref CoreEvents.UpdateEvent eventData)
    {
        // Update can be still executed if Deacivate was called during calling Update subscribers.
        if (!_active)
            return;

        // Take the frame time step, which is stored as a float
        var timeStep = eventData.TimeStep;

        // Move the camera, scale movement with time step
        Update(timeStep);

        // TODO: port URHO3D_ACTIONS when the Actions subsystem is available.
        // actionManager_->Update(timeStep);
    }

    // TODO: port URHO3D_ACTIONS when the Actions subsystem is available.
    // SharedPtr<ActionManager> actionManager_;
}

public class StateManager : RedObject
{
    /// <summary>Queue item.</summary>
    private struct QueueItem : ICloneable, IDisposable
    {
        public ApplicationState? State;
        public StringHash StateType;
        public StringVariantMap Bundle;

        public QueueItem(ApplicationState? state, StringHash stateType, StringVariantMap bundle)
        {
            State = state;
            StateType = stateType;
            Bundle = new StringVariantMap(bundle);
        }

        public readonly QueueItem Clone() => new(State, StateType, Bundle);

        readonly object ICloneable.Clone() => Clone();

        public readonly void Dispose()
        {
            Bundle.Dispose();
        }
    }

    private enum TransitionState
    {
        Sustain,
        FadeOut,
        FadeIn,
        WaitToExit,
    }

    private StringHash _originState = StringHash.Empty;
    private StringHash _destinationState = StringHash.Empty;
    private readonly Dictionary<StringHash, WeakReference<ApplicationState>> _stateCache = [];
    private readonly Queue<QueueItem> _stateQueue = new();
    private ApplicationState? _activeState;
    private float _fadeTime;
    private float _fadeInDuration = MathDefs.Epsilon;
    private float _fadeOutDuration = MathDefs.Epsilon;
    private TransitionState _transitionState = TransitionState.Sustain;
    /// <summary>Construct.</summary>
    public StateManager()
    {
    }

    public override void Dispose()
    {
        Reset();
        _stateCache.Clear();
        base.Dispose();
    }

    /// <summary>Hard reset of state manager. Current state will be set to null and the queue is purged.</summary>
    public void Reset()
    {
        var hasState = _activeState is not null;
        if (hasState)
        {
            _destinationState = StringHash.Empty;
            StartTransition();
            DeactivateState();
        }
        while (_stateQueue.TryDequeue(out var queueItem))
            queueItem.Dispose();

        SetTransitionState(TransitionState.Sustain);
        if (hasState)
            CompleteTransition();
    }

    /// <summary>Update state manager. This is called automatically by the engine every frame.</summary>
    public void Update(float timeStep)
    {
        var iterationCount = 0;
        do
        {
            switch (_transitionState)
            {
                case TransitionState.Sustain:
                    return;
                case TransitionState.WaitToExit:
                    if (_activeState is not null && _activeState.CanLeaveState())
                        SetTransitionState(TransitionState.FadeOut);
                    else
                        return;
                    break;
                case TransitionState.FadeIn:
                    _fadeTime += timeStep;
                    if (_fadeTime >= _fadeInDuration)
                    {
                        timeStep = _fadeTime - _fadeInDuration;
                        CompleteTransition();

                        if (_stateQueue.Count == 0)
                            SetTransitionState(TransitionState.Sustain);
                        else if (_activeState is not null && !_activeState.CanLeaveState())
                            SetTransitionState(TransitionState.WaitToExit);
                        else
                            SetTransitionState(TransitionState.FadeOut);
                    }
                    else
                    {
                        return;
                    }
                    break;
                case TransitionState.FadeOut:
                    _fadeTime += timeStep;
                    if (_fadeTime >= _fadeOutDuration)
                    {
                        timeStep = _fadeTime - _fadeInDuration;
                        CreateNextState();
                    }
                    else
                    {
                        return;
                    }
                    break;
            }
            // Limit number of actions per frame
            ++iterationCount;
        } while (timeStep > 0.0f && iterationCount < 16);
    }

    /// <summary>Cache state without using it.</summary>
    public void CacheState(ApplicationState state)
    {
        _stateCache[state.GetTypeNameHash()] = new WeakReference<ApplicationState>(state);
    }

    /// <summary>Transition to the application state.</summary>
    public void EnqueueState(ApplicationState? gameScreen, StringVariantMap bundle)
    {
        if (gameScreen is null)
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default, "No target state provided", null, static (state, _) => state);
            return;
        }
        if (Environment.CurrentManagedThreadId != WorkQueue.GetMainThreadId())
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default, "State transition could only be scheduled from the main thread", null, static (state, _) => state);
            return;
        }
        _stateQueue.Enqueue(new QueueItem(gameScreen, gameScreen.GetTypeNameHash(), bundle));
        InitiateTransition();
    }

    /// <summary>Transition to the application state.</summary>
    public void EnqueueState(ApplicationState? gameScreen)
    {
        using var bundle = new StringVariantMap();
        EnqueueState(gameScreen, bundle);
    }

    /// <summary>Transition to the application state.</summary>
    public void EnqueueState(StringHash type, StringVariantMap bundle)
    {
        if (Environment.CurrentManagedThreadId != WorkQueue.GetMainThreadId())
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default, "State transition could only be scheduled from the main thread", null, static (state, _) => state);
            return;
        }
        _stateQueue.Enqueue(new QueueItem(null, type, bundle));
        InitiateTransition();
    }

    /// <summary>Transition to the application state.</summary>
    public void EnqueueState(StringHash type)
    {
        using var bundle = new StringVariantMap();
        EnqueueState(type, bundle);
    }

    /// <summary>Transition to the application state.</summary>
    public void EnqueueState<T>(StringVariantMap bundle) where T : ApplicationState
    {
        EnqueueState(new StringHash(typeof(T).GetClassName()), bundle);
    }

    /// <summary>Transition to the application state.</summary>
    public void EnqueueState<T>() where T : ApplicationState
    {
        using var bundle = new StringVariantMap();
        EnqueueState<T>(bundle);
    }

    /// <summary>Get cached state if present.</summary>
    public ApplicationState? GetCachedState(StringHash type)
    {
        return _stateCache.TryGetValue(type, out var weakState) && weakState.TryGetTarget(out var state) ? state : null;
    }

    /// <summary>Get cached state if present.</summary>
    public T? GetCachedState<T>() where T : ApplicationState
    {
        return GetCachedState(new StringHash(typeof(T).GetClassName())) as T;
    }

    /// <summary>Get current application state.</summary>
    public ApplicationState? State => _activeState;

    /// <summary>Get target application state.</summary>
    public StringHash TargetState
    {
        get
        {
            if (_stateQueue.Count == 0)
                return _activeState?.GetTypeNameHash() ?? StringHash.Empty;

            var stateType = StringHash.Empty;
            foreach (var queueItem in _stateQueue)
                stateType = queueItem.StateType;
            return stateType;
        }
    }

    /// <summary>Set or get fade in animation duration.</summary>
    public float FadeInDuration
    {
        get => _fadeInDuration;
        set => _fadeInDuration = Math.Clamp(value, MathDefs.Epsilon, float.MaxValue);
    }

    /// <summary>Set or get fade out animation duration.</summary>
    public float FadeOutDuration
    {
        get => _fadeOutDuration;
        set => _fadeOutDuration = Math.Clamp(value, MathDefs.Epsilon, float.MaxValue);
    }

    /// <summary>Initiate state transition if necessary.</summary>
    private void InitiateTransition()
    {
        if (_stateQueue.Count == 0)
        {
            SetTransitionState(TransitionState.Sustain);
            return;
        }

        if (_transitionState == TransitionState.Sustain)
        {
            if (_activeState is not null)
                SetTransitionState(TransitionState.FadeOut);
            else
                CreateNextState();
        }
    }

    /// <summary>Handle update event to animate state transitions.</summary>
    private void HandleUpdate(ref CoreEvents.UpdateEvent eventData)
    {
        Update(eventData.TimeStep);
    }

    /// <summary>Set current transition state and initialize related values.</summary>
    private void SetTransitionState(TransitionState state)
    {
        if (_transitionState == state)
            return;

        _transitionState = state;

        switch (_transitionState)
        {
            case TransitionState.Sustain:
                UnsubscribeFromEvent<CoreEvents.UpdateEvent>();
                break;
            case TransitionState.FadeIn:
            case TransitionState.FadeOut:
                _fadeTime = 0.0f;
                SubscribeToEvent<CoreEvents.UpdateEvent>(HandleUpdate);
                break;
            case TransitionState.WaitToExit:
                SubscribeToEvent<CoreEvents.UpdateEvent>(HandleUpdate);
                break;
        }
        if (_transitionState == TransitionState.FadeOut)
        {
            _destinationState = _stateQueue.Count == 0 ? StringHash.Empty : _stateQueue.Peek().StateType;
            StartTransition();
        }
    }

    /// <summary>Start transition out of the current state.</summary>
    private void StartTransition()
    {
        if (_activeState is not null)
        {
            _originState = _activeState.GetTypeNameHash();
            _activeState.TransitionStarted();
        }
        else
        {
            _originState = StringHash.Empty;
        }
        var eventData = new StateManagerEvents.StateTransitionStartedEvent { From = _originState, To = _destinationState };
        SendEvent(ref eventData);
    }

    /// <summary>Complete transition into the current state.</summary>
    private void CompleteTransition()
    {
        if (_activeState is not null)
            _activeState.TransitionComplete();
        var eventData = new StateManagerEvents.StateTransitionCompleteEvent { From = _originState, To = _destinationState };
        SendEvent(ref eventData);

        _originState = _destinationState;
        _destinationState = StringHash.Empty;
    }

    /// <summary>Deactivate state.</summary>
    private void DeactivateState()
    {
        if (_activeState is not null)
        {
            var eventData = new StateManagerEvents.LeavingApplicationStateEvent { From = _originState, To = _destinationState };
            SendEvent(ref eventData);

            _activeState.Deactivate();
            _activeState = null;
        }
    }

    /// <summary>Dequeue and set next state as active.</summary>
    private void CreateNextState()
    {
        DeactivateState();

        while (_stateQueue.Count != 0)
        {
            using var nextQueueItem = _stateQueue.Peek().Clone();
            var queuedItem = _stateQueue.Dequeue();
            queuedItem.Dispose();
            var nextState = nextQueueItem.State;
            if (nextState is null)
            {
                nextState = GetCachedState(nextQueueItem.StateType);
                if (nextState is null)
                {
                    nextState = Context.CreateObject(nextQueueItem.StateType) as ApplicationState;
                    if (nextState is null)
                    {
                        Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default, "Can't create application state object", null, static (state, _) => state);
                        continue;
                    }
                }
            }
            _destinationState = nextState.GetTypeNameHash();
            _stateCache[_destinationState] = new WeakReference<ApplicationState>(nextState);

            if (_originState == StringHash.Empty)
                StartTransition();
            _activeState = nextState;
            var eventData = new StateManagerEvents.EnteringApplicationStateEvent { From = _originState, To = _destinationState };
            SendEvent(ref eventData);

            SetTransitionState(TransitionState.FadeIn);
            _activeState.Activate(nextQueueItem.Bundle);
            return;
        }
        _destinationState = StringHash.Empty;
        SetTransitionState(TransitionState.Sustain);
        CompleteTransition();
    }
}
