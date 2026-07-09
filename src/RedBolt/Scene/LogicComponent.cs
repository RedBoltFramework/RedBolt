namespace RedBolt;

public abstract class LogicComponent : Component, ISceneUpdateListener
{
    private UpdateEvent _updateEventMask = UpdateEvent.UseUpdate | UpdateEvent.UsePostUpdate | UpdateEvent.UseFixedUpdate | UpdateEvent.UseFixedPostUpdate;
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

    public void SetUpdateEventMask(UpdateEvent mask)
    {
        _updateEventMask = mask;
    }

    public UpdateEvent GetUpdateEventMask() => _updateEventMask;

    public bool IsDelayedStartCalled() => _delayedStartCalled;

    protected internal override void OnNodeSet(Node? previousNode, Node? currentNode)
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
