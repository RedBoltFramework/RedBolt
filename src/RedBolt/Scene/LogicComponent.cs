using System.Reflection;

namespace RedBolt;

/// <summary>Helper base class for user-defined game logic components that hooks up to update events and forwards them to virtual functions similar to ScriptInstance class.</summary>
public class LogicComponent : Component
{
    private delegate void SceneUpdateHandler(float timeStep);

    private static readonly MethodInfo SubscribeToSceneUpdateEventMethod = typeof(LogicComponent).GetMethod(
        nameof(SubscribeToSceneUpdateEventCore), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private UpdateEvent _updateEventMask;
    private UpdateEvent _currentEventMask;
    private bool _delayedStartCalled;
    private ContextEventSubscription? _updateSubscription;
    private ContextEventSubscription? _postUpdateSubscription;
    private ContextEventSubscription? _worldOriginUpdateSubscription;
    private ContextEventSubscription? _worldOriginPostUpdateSubscription;

    /// <summary>Construct.</summary>
    public LogicComponent()
    {
        _updateEventMask = UpdateEvent.UseUpdate | UpdateEvent.UsePostUpdate | UpdateEvent.UseFixedUpdate | UpdateEvent.UseFixedPostUpdate;
        _currentEventMask = UpdateEvent.UseNoEvent;
    }

    /// <summary>Handle enabled/disabled state change. Changes update event subscription.</summary>
    public override void OnSetEnabled()
    {
        UpdateEventSubscription();
    }

    /// <summary>Called when the component is added to a scene node. Other components may not yet exist.</summary>
    public virtual void Start()
    {
    }

    /// <summary>Called before the first update. At this point all other components of the node should exist. Will also be called if update events are not wanted; in that case the event is immediately unsubscribed afterward.</summary>
    public virtual void DelayedStart()
    {
    }

    /// <summary>Called when the component is detached from a scene node, usually on destruction. Note that you will no longer have access to the node and scene at that point.</summary>
    public virtual void Stop()
    {
    }

    /// <summary>Called on scene update, variable timestep.</summary>
    public virtual void Update(float timeStep)
    {
    }

    /// <summary>Called on scene post-update, variable timestep.</summary>
    public virtual void PostUpdate(float timeStep)
    {
    }

    /// <summary>Called on physics update, fixed timestep.</summary>
    public virtual void FixedUpdate(float timeStep)
    {
    }

    /// <summary>Called on physics post-update, fixed timestep.</summary>
    public virtual void FixedPostUpdate(float timeStep)
    {
    }

    /// <summary>Called on world origin update.</summary>
    public virtual void UpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta)
    {
    }

    /// <summary>Called on world origin post-update.</summary>
    public virtual void PostUpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta)
    {
    }

    /// <summary>Return update event type. Should stay the same for any given instance of the component. DelayedStart is invoked before the first update event of this type.</summary>
    public virtual Type GetUpdateEvent() => typeof(SceneEvents.SceneUpdateEvent);

    /// <summary>Return post update event type. Should stay the same for any given instance of the component.</summary>
    public virtual Type GetPostUpdateEvent() => typeof(SceneEvents.ScenePostUpdateEvent);

    /// <summary>Set what update events should be subscribed to. Use this for optimization: by default all are in use. Note that this is not an attribute and is not saved or network-serialized, therefore it should always be called eg. in the subclass constructor.</summary>
    public void SetUpdateEventMask(UpdateEvent mask)
    {
        if (_updateEventMask != mask)
        {
            _updateEventMask = mask;
            UpdateEventSubscription();
        }
    }

    /// <summary>Return what update events are subscribed to.</summary>
    public UpdateEvent GetUpdateEventMask() => _updateEventMask;

    /// <summary>Return whether the DelayedStart() function has been called.</summary>
    public bool IsDelayedStartCalled() => _delayedStartCalled;

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (Node is not null)
            Start();
        else
            Stop();
    }

    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        if (scene is not null)
            UpdateEventSubscription();
        else
        {
            _updateSubscription?.Dispose();
            _updateSubscription = null;
            _postUpdateSubscription?.Dispose();
            _postUpdateSubscription = null;
            _worldOriginUpdateSubscription?.Dispose();
            _worldOriginUpdateSubscription = null;
            _worldOriginPostUpdateSubscription?.Dispose();
            _worldOriginPostUpdateSubscription = null;
            _currentEventMask = UpdateEvent.UseNoEvent;
        }
    }

    private void UpdateEventSubscription()
    {
        var scene = Scene;
        if (scene is null)
            return;

        var enabled = IsEnabledEffective;

        var hasSubscription = (_currentEventMask & UpdateEvent.UseUpdate) != 0;
        var needSubscription = enabled && (((_updateEventMask & UpdateEvent.UseUpdate) != 0) || !_delayedStartCalled);
        if (needSubscription && !hasSubscription)
        {
            _updateSubscription = SubscribeToSceneUpdateEvent(scene, GetUpdateEvent(), HandleSceneUpdate);
            _currentEventMask |= UpdateEvent.UseUpdate;
        }
        else if (!needSubscription && hasSubscription)
        {
            _updateSubscription?.Dispose();
            _updateSubscription = null;
            _currentEventMask &= ~UpdateEvent.UseUpdate;
        }

        hasSubscription = (_currentEventMask & UpdateEvent.UsePostUpdate) != 0;
        needSubscription = enabled && (_updateEventMask & UpdateEvent.UsePostUpdate) != 0;
        if (needSubscription && !hasSubscription)
        {
            _postUpdateSubscription = SubscribeToSceneUpdateEvent(scene, GetPostUpdateEvent(), HandleScenePostUpdate);
            _currentEventMask |= UpdateEvent.UsePostUpdate;
        }
        else if (!needSubscription && hasSubscription)
        {
            _postUpdateSubscription?.Dispose();
            _postUpdateSubscription = null;
            _currentEventMask &= ~UpdateEvent.UsePostUpdate;
        }

        hasSubscription = (_currentEventMask & UpdateEvent.UseWorldOriginUpdate) != 0;
        needSubscription = enabled && (_updateEventMask & UpdateEvent.UseWorldOriginUpdate) != 0;
        if (needSubscription && !hasSubscription)
        {
            _worldOriginUpdateSubscription = SubscribeToEvent<SceneEvents.WorldOriginUpdateEvent>(scene, HandleWorldOriginUpdate);
            _currentEventMask |= UpdateEvent.UseWorldOriginUpdate;
        }
        else if (!needSubscription && hasSubscription)
        {
            _worldOriginUpdateSubscription?.Dispose();
            _worldOriginUpdateSubscription = null;
            _currentEventMask &= ~UpdateEvent.UseWorldOriginUpdate;
        }

        hasSubscription = (_currentEventMask & UpdateEvent.UseWorldOriginPostUpdate) != 0;
        needSubscription = enabled && (_updateEventMask & UpdateEvent.UseWorldOriginPostUpdate) != 0;
        if (needSubscription && !hasSubscription)
        {
            _worldOriginPostUpdateSubscription = SubscribeToEvent<SceneEvents.WorldOriginPostUpdateEvent>(scene, HandleWorldOriginPostUpdate);
            _currentEventMask |= UpdateEvent.UseWorldOriginPostUpdate;
        }
        else if (!needSubscription && hasSubscription)
        {
            _worldOriginPostUpdateSubscription?.Dispose();
            _worldOriginPostUpdateSubscription = null;
            _currentEventMask &= ~UpdateEvent.UseWorldOriginPostUpdate;
        }
    }

    private ContextEventSubscription SubscribeToSceneUpdateEvent(
        Scene scene, Type eventType, SceneUpdateHandler handler)
    {
        return (ContextEventSubscription)SubscribeToSceneUpdateEventMethod.MakeGenericMethod(eventType)
            .Invoke(this, [scene, handler])!;
    }

    private ContextEventSubscription SubscribeToSceneUpdateEventCore<T>(Scene scene, SceneUpdateHandler handler)
        where T : struct, SceneEvents.ISceneUpdateEvent
    {
        return SubscribeToEvent<T>(scene, (ref T eventData) => handler(eventData.TimeStep));
    }

    private void HandleSceneUpdate(float timeStep)
    {
        // Execute user-defined delayed start function before first update
        if (!_delayedStartCalled)
        {
            DelayedStart();
            _delayedStartCalled = true;

            // If did not need actual update events, unsubscribe now
            if ((_updateEventMask & UpdateEvent.UseUpdate) == 0)
            {
                _updateSubscription?.Dispose();
                _updateSubscription = null;
                _currentEventMask &= ~UpdateEvent.UseUpdate;
                return;
            }
        }

        // Then execute user-defined update function
        Update(timeStep);
    }

    private void HandleScenePostUpdate(float timeStep)
    {
        // Execute user-defined post-update function
        PostUpdate(timeStep);
    }

    private void HandleWorldOriginUpdate(ref SceneEvents.WorldOriginUpdateEvent eventData)
    {
        UpdateWorldOrigin(eventData.OldOrigin, eventData.NewOrigin, eventData.Delta);
    }

    private void HandleWorldOriginPostUpdate(ref SceneEvents.WorldOriginPostUpdateEvent eventData)
    {
        PostUpdateWorldOrigin(eventData.OldOrigin, eventData.NewOrigin, eventData.Delta);
    }
}
