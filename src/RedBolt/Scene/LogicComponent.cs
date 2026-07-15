namespace RedBolt;

public abstract class LogicComponent : Component, ISceneUpdateListener
{
    private UpdateEvent _updateEventMask = UpdateEvent.UseUpdate | UpdateEvent.UsePostUpdate | UpdateEvent.UseFixedUpdate | UpdateEvent.UseFixedPostUpdate;
    // TODO: port - C++ code:
    // UpdateEventFlags currentEventMask_; // Current event subscription mask.
    private bool _delayedStartCalled;

    public virtual void Start()
    {
    }

    public virtual void DelayedStart()
    {
    }

    public virtual void Stop()
    {
    }

    public virtual void Update(float timeStep)
    {
    }

    public virtual void PostUpdate(float timeStep)
    {
    }

    public virtual void FixedUpdate(float timeStep)
    {
    }

    public virtual void FixedPostUpdate(float timeStep)
    {
    }

    // TODO: port - C++ code:
    // virtual void UpdateWorldOrigin(const IntVector3& oldOrigin, const IntVector3& newOrigin, const IntVector3& delta);
    // virtual void PostUpdateWorldOrigin(const IntVector3& oldOrigin, const IntVector3& newOrigin, const IntVector3& delta);
    // virtual StringHash GetUpdateEvent() const;
    // virtual StringHash GetPostUpdateEvent() const;
    // void OnSetEnabled() override; // Changes update event subscription.
    // void OnSceneSet(Scene* previousScene, Scene* scene) override; // Subscribes/unsubscribes to update events.
    //
    // void UpdateEventSubscription(); // Subscribe/unsubscribe to update events.
    // void HandleSceneUpdate(StringHash eventType, VariantMap& eventData);
    // void HandleScenePostUpdate(StringHash eventType, VariantMap& eventData);
    // void HandlePhysicsPreStep(StringHash eventType, VariantMap& eventData);
    // void HandlePhysicsPostStep(StringHash eventType, VariantMap& eventData);
    // void HandleWorldOriginUpdate(StringHash eventType, VariantMap& eventData);
    // void HandleWorldOriginPostUpdate(StringHash eventType, VariantMap& eventData);

    public void SetUpdateEventMask(UpdateEvent mask)
    {
        _updateEventMask = mask;
    }

    public UpdateEvent GetUpdateEventMask() => _updateEventMask;

    public bool IsDelayedStartCalled() => _delayedStartCalled;

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (currentNode is not null)
            Start();
        else if (previousNode is not null)
            Stop();
    }

    void ISceneUpdateListener.OnSceneUpdate(float timeStep)
    {
        if (!IsEnabledEffective)
            return;

        if (!_delayedStartCalled)
        {
            DelayedStart();
            _delayedStartCalled = true;
        }

        if ((_updateEventMask & UpdateEvent.UseUpdate) != 0)
            Update(timeStep);
    }

    void ISceneUpdateListener.OnScenePostUpdate(float timeStep)
    {
        if (IsEnabledEffective && (_updateEventMask & UpdateEvent.UsePostUpdate) != 0)
            PostUpdate(timeStep);
    }
}
