using Microsoft.Extensions.Logging;

namespace RedBolt;

public abstract class ApplicationState : Object
{
    private bool _active;
    private bool _mouseVisible = true;
    private bool _mouseGrabbed;
    private MouseMode _mouseMode = MouseMode.Free;
    private Color _fogColor = new(0.0f, 0.0f, 0.0f);
    private ContextEventSubscription? _updateSubscription;

    public bool IsActive() => _active;
    public bool IsMouseVisible() => _mouseVisible;
    public bool IsMouseGrabbed() => _mouseGrabbed;
    public MouseMode GetMouseMode() => _mouseMode;
    public Color GetDefaultFogColor() => _fogColor;

    public object? GetUIRoot() => null;
    public void SetUICustomSize(Vector2i size) { }
    public void SetUICustomSize(int width, int height) { }
    public Vector2i GetUICustomSize() => Vector2i.Zero;
    public object? GetCursor() => null;
    public object? GetViewport(uint index) => null;
    public object? GetViewportForScene(object? scene, uint index) => null;

    public void SetMouseVisible(bool enable) { _mouseVisible = enable; if (IsActive()) GetSubsystem<Input>()?.SetMouseVisible(enable); }
    public void SetMouseGrabbed(bool grab) { _mouseGrabbed = grab; if (IsActive()) GetSubsystem<Input>()?.SetMouseGrabbed(grab); }
    public void SetMouseMode(MouseMode mode) { _mouseMode = mode; if (IsActive()) GetSubsystem<Input>()?.SetMouseMode(mode); }
    public void SetCursor(object? cursor) { }
    public void SetNumViewports(uint num) { }
    public void SetViewport(uint index, object? viewport) { }
    public void SetDefaultFogColor(Color color) { _fogColor = color; }

    public virtual void Activate(Dictionary<string, Variant> bundle)
    {
        if (_active) return;
        _active = true;
        _updateSubscription = SubscribeToEvent<CoreEvents.UpdateEvent>(HandleUpdate);
    }

    public virtual void TransitionComplete() { }
    public virtual bool CanLeaveState() => true;
    public virtual void TransitionStarted() { }

    public virtual void Deactivate()
    {
        if (!_active) return;
        _active = false;
        _updateSubscription?.Dispose();
        _updateSubscription = null;
    }

    public virtual void Update(float timeStep) { }

    private void HandleUpdate(ref CoreEvents.UpdateEvent eventData)
    {
        if (!_active) return;
        Update(eventData.TimeStep);
    }
}

public sealed class StateManager : Object, IDisposable
{
    private struct QueueItem
    {
        public ApplicationState? State;
        public StringHash StateType;
        public Dictionary<string, Variant> Bundle;
    }

    private enum TransitionState { Sustain, FadeOut, FadeIn, WaitToExit }

    private StringHash _originState = StringHash.Empty;
    private StringHash _destinationState = StringHash.Empty;
    private readonly Dictionary<StringHash, ApplicationState?> _stateCache = [];
    private readonly Queue<QueueItem> _stateQueue = new();
    private ApplicationState? _activeState;
    private float _fadeTime;
    private float _fadeInDuration = MathDefs.Epsilon;
    private float _fadeOutDuration = MathDefs.Epsilon;
    private TransitionState _transitionState = TransitionState.Sustain;
    private ContextEventSubscription? _updateSubscription;

    public override void Dispose()
    {
        _updateSubscription?.Dispose();
        Reset();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Reset()
    {
        var hasState = _activeState != null;
        if (hasState) { _destinationState = StringHash.Empty; StartTransition(); DeactivateState(); }
        _stateQueue.Clear();
        SetTransitionState(TransitionState.Sustain);
        if (hasState) CompleteTransition();
    }

    public void Update(float timeStep)
    {
        var iterationCount = 0;
        do
        {
            switch (_transitionState)
            {
                case TransitionState.Sustain: return;
                case TransitionState.WaitToExit:
                    if (_activeState != null && _activeState.CanLeaveState()) SetTransitionState(TransitionState.FadeOut);
                    else return;
                    break;
                case TransitionState.FadeIn:
                    _fadeTime += timeStep;
                    if (_fadeTime >= _fadeInDuration)
                    {
                        timeStep = _fadeTime - _fadeInDuration;
                        CompleteTransition();
                        if (_stateQueue.Count == 0) SetTransitionState(TransitionState.Sustain);
                        else if (_activeState != null && !_activeState.CanLeaveState()) SetTransitionState(TransitionState.WaitToExit);
                        else SetTransitionState(TransitionState.FadeOut);
                    }
                    else return;
                    break;
                case TransitionState.FadeOut:
                    _fadeTime += timeStep;
                    if (_fadeTime >= _fadeOutDuration) { timeStep = _fadeTime - _fadeOutDuration; CreateNextState(); }
                    else return;
                    break;
            }
            ++iterationCount;
        } while (timeStep > 0.0f && iterationCount < 16);
    }

    public void CacheState(ApplicationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _stateCache[state.GetTypeNameHash()] = state;
    }

    public void EnqueueState(ApplicationState gameScreen, Dictionary<string, Variant>? bundle = null)
    {
        ArgumentNullException.ThrowIfNull(gameScreen);
        if (Environment.CurrentManagedThreadId != WorkQueue.GetMainThreadId())
        {
            Log.Logger.Log(LogLevel.Error, default, "State transition could only be scheduled from the main thread", null, static (state, _) => state);
            return;
        }
        bundle ??= new Dictionary<string, Variant>(StringComparer.Ordinal);
        _stateQueue.Enqueue(new QueueItem { State = gameScreen, StateType = gameScreen.GetTypeNameHash(), Bundle = bundle });
        InitiateTransition();
    }

    public void EnqueueState(StringHash type, Dictionary<string, Variant>? bundle = null)
    {
        if (Environment.CurrentManagedThreadId != WorkQueue.GetMainThreadId())
        {
            Log.Logger.Log(LogLevel.Error, default, "State transition could only be scheduled from the main thread", null, static (state, _) => state);
            return;
        }
        bundle ??= new Dictionary<string, Variant>(StringComparer.Ordinal);
        _stateQueue.Enqueue(new QueueItem { StateType = type, Bundle = bundle });
        InitiateTransition();
    }

    public ApplicationState? GetCachedState(StringHash type) { _stateCache.TryGetValue(type, out var s); return s; }
    public ApplicationState? GetState() => _activeState;

    public StringHash GetTargetState()
    {
        if (_stateQueue.Count == 0) return _activeState?.GetTypeNameHash() ?? StringHash.Empty;
        var array = _stateQueue.ToArray();
        return array[^1].StateType;
    }

    public void SetFadeInDuration(float d) { _fadeInDuration = Math.Max(d, MathDefs.Epsilon); }
    public void SetFadeOutDuration(float d) { _fadeOutDuration = Math.Max(d, MathDefs.Epsilon); }
    public float GetFadeInDuration() => _fadeInDuration;
    public float GetFadeOutDuration() => _fadeOutDuration;

    private void InitiateTransition()
    {
        if (_stateQueue.Count == 0) { SetTransitionState(TransitionState.Sustain); return; }
        if (_transitionState == TransitionState.Sustain)
        {
            if (_activeState != null) SetTransitionState(TransitionState.FadeOut);
            else CreateNextState();
        }
    }

    private void HandleUpdate(ref CoreEvents.UpdateEvent e) => Update(e.TimeStep);

    private void SetTransitionState(TransitionState state)
    {
        if (_transitionState == state) return;
        _transitionState = state;
        switch (_transitionState)
        {
            case TransitionState.Sustain: _updateSubscription?.Dispose(); _updateSubscription = null; break;
            case TransitionState.FadeIn:
            case TransitionState.FadeOut: _fadeTime = 0.0f; _updateSubscription = SubscribeToEvent<CoreEvents.UpdateEvent>(HandleUpdate); break;
            case TransitionState.WaitToExit: _updateSubscription = SubscribeToEvent<CoreEvents.UpdateEvent>(HandleUpdate); break;
        }
        if (_transitionState == TransitionState.FadeOut)
        {
            _destinationState = _stateQueue.Count == 0 ? StringHash.Empty : _stateQueue.Peek().StateType;
            StartTransition();
        }
    }

    private void StartTransition()
    {
        _originState = _activeState?.GetTypeNameHash() ?? StringHash.Empty;
        _activeState?.TransitionStarted();
        var d = new StateManagerEvents.StateTransitionStartedEvent { From = _originState, To = _destinationState };
        SendEvent(ref d);
    }

    private void CompleteTransition()
    {
        _activeState?.TransitionComplete();
        var d = new StateManagerEvents.StateTransitionCompleteEvent { From = _originState, To = _destinationState };
        SendEvent(ref d);
        _originState = _destinationState;
        _destinationState = StringHash.Empty;
    }

    private void DeactivateState()
    {
        if (_activeState == null) return;
        var leaving = new StateManagerEvents.LeavingApplicationStateEvent { From = _originState, To = _destinationState };
        SendEvent(ref leaving);
        _activeState.Deactivate();
        _activeState = null;
    }

    private void CreateNextState()
    {
        DeactivateState();
        while (_stateQueue.Count > 0)
        {
            var nextItem = _stateQueue.Dequeue();
            var nextState = nextItem.State;
            if (nextState == null)
            {
                _stateCache.TryGetValue(nextItem.StateType, out nextState);
                nextState ??= (ApplicationState?)Context.CreateObject(nextItem.StateType);
                if (nextState == null) continue;
            }
            _destinationState = nextState.GetTypeNameHash();
            _stateCache[_destinationState] = nextState;
            if (_originState == StringHash.Empty) StartTransition();
            _activeState = nextState;
            var entering = new StateManagerEvents.EnteringApplicationStateEvent { From = _originState, To = _destinationState };
            SendEvent(ref entering);
            SetTransitionState(TransitionState.FadeIn);
            _activeState.Activate(nextItem.Bundle);
            return;
        }
        _destinationState = StringHash.Empty;
        SetTransitionState(TransitionState.Sustain);
        CompleteTransition();
    }
}
