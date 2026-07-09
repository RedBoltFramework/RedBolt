namespace RedBolt;

public abstract class LogicComponent : Component, ISceneUpdateListener
{
    private UpdateEvent updateEventMask = UpdateEvent.UseUpdate | UpdateEvent.UsePostUpdate | UpdateEvent.UseFixedUpdate | UpdateEvent.UseFixedPostUpdate;
    private bool delayedStartCalled;

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
        updateEventMask = mask;
    }

    public UpdateEvent GetUpdateEventMask() => updateEventMask;

    public bool IsDelayedStartCalled() => delayedStartCalled;

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

        if (!delayedStartCalled)
        {
            DelayedStart();
            delayedStartCalled = true;
        }

        if ((updateEventMask & UpdateEvent.UseUpdate) != 0)
            Update(timeStep);
    }

    void ISceneUpdateListener.OnScenePostUpdate(float timeStep)
    {
        if (IsEnabledEffective && (updateEventMask & UpdateEvent.UsePostUpdate) != 0)
            PostUpdate(timeStep);
    }
}
