namespace RedBolt;

public abstract class LogicComponent : Component, ISceneUpdateListener
{
    private UpdateEvent _updateEventMask = UpdateEvent.UseUpdate | UpdateEvent.UsePostUpdate | UpdateEvent.UseFixedUpdate | UpdateEvent.UseFixedPostUpdate;
    private bool _delayedStartCalled;
    private ContextEventSubscription? _worldOriginUpdateSubscription;
    private ContextEventSubscription? _worldOriginPostUpdateSubscription;

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

    public virtual void UpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta)
    {
    }

    public virtual void PostUpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta)
    {
    }

    public void SetUpdateEventMask(UpdateEvent mask)
    {
        _updateEventMask = mask;
        UpdateWorldOriginSubscriptions(Scene);
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

    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        UpdateWorldOriginSubscriptions(scene);
    }

    private void UpdateWorldOriginSubscriptions(Scene? scene)
    {
        _worldOriginUpdateSubscription?.Dispose();
        _worldOriginUpdateSubscription = null;
        _worldOriginPostUpdateSubscription?.Dispose();
        _worldOriginPostUpdateSubscription = null;

        if (scene is null || !IsEnabledEffective)
            return;

        if ((_updateEventMask & UpdateEvent.UseWorldOriginUpdate) != 0)
            _worldOriginUpdateSubscription = SubscribeToEvent<SceneEvents.WorldOriginUpdateEvent>(scene, HandleWorldOriginUpdate);
        if ((_updateEventMask & UpdateEvent.UseWorldOriginPostUpdate) != 0)
            _worldOriginPostUpdateSubscription = SubscribeToEvent<SceneEvents.WorldOriginPostUpdateEvent>(scene, HandleWorldOriginPostUpdate);
    }

    private void HandleWorldOriginUpdate(ref SceneEvents.WorldOriginUpdateEvent eventData)
    {
        UpdateWorldOrigin(eventData.OldOrigin, eventData.NewOrigin, eventData.Delta);
    }

    private void HandleWorldOriginPostUpdate(ref SceneEvents.WorldOriginPostUpdateEvent eventData)
    {
        PostUpdateWorldOrigin(eventData.OldOrigin, eventData.NewOrigin, eventData.Delta);
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
