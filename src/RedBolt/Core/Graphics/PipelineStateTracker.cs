namespace RedBolt.Graphics;

using SystemMath = global::System.Math;
using System.Threading;

using RedBolt.Core;

/// <summary>Holds subscription from one PipelineStateTracker to another. Neither checks nor affects objects lifetime.</summary>
public class PipelineStateSubscription : IDisposable, ICloneable
{
    /// <summary>Construct default.</summary>
    public PipelineStateSubscription()
    {
    }

    /// <summary>Construct valid.</summary>
    public PipelineStateSubscription(PipelineStateTracker? sender, PipelineStateTracker? subscriber)
    {
        Reset(sender, subscriber);
    }

    /// <summary>Copy-construct.</summary>
    public object Clone()
    {
        return new PipelineStateSubscription(_sender, _subscriber);
    }

    /// <summary>Destruct.</summary>
    public void Dispose()
    {
        Reset(null, null);
    }

    /// <summary>Assign.</summary>
    public PipelineStateSubscription CopyAssign(PipelineStateSubscription other)
    {
        Reset(other._sender, other._subscriber);
        return this;
    }

    /// <summary>Move-assign.</summary>
    public PipelineStateSubscription MoveAssign(PipelineStateSubscription other)
    {
        (_sender, other._sender) = (other._sender, _sender);
        (_subscriber, other._subscriber) = (other._subscriber, _subscriber);
        return this;
    }

    /// <summary>Reset and update dependency.</summary>
    private void Reset(PipelineStateTracker? sender, PipelineStateTracker? subscriber)
    {
        // Add new reference first
        if (subscriber is not null && sender is not null)
            sender.AddSubscriberReference(subscriber);

        // Remove old reference
        if (_subscriber is not null && _sender is not null)
            _sender.RemoveSubscriberReference(_subscriber);

        // Relink pointers
        _subscriber = subscriber;
        _sender = sender;
    }

    /// <summary>Owned pointer.</summary>
    private PipelineStateTracker? _sender;

    /// <summary>Owner.</summary>
    private PipelineStateTracker? _subscriber;
}

/// <summary>Helper class to track pipeline state changes caused by derived class.</summary>
public abstract class PipelineStateTracker : RedObject
{
    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _subscribers.Clear();
        base.Dispose();
    }

    /// <summary>Return (partial) pipeline state hash. Save to call from multiple threads as long as the object is not changing.</summary>
    public uint GetPipelineStateHash()
    {
        var hash = Volatile.Read(ref _pipelineStateHash);
        if (hash != 0)
            return hash;

        var newHash = SystemMath.Max(1u, RecalculatePipelineStateHash());
        Volatile.Write(ref _pipelineStateHash, newHash);
        return newHash;
    }

    /// <summary>Mark pipeline state hash as dirty.</summary>
    public void MarkPipelineStateHashDirty()
    {
        var oldHash = Interlocked.Exchange(ref _pipelineStateHash, 0);
        if (oldHash != 0)
        {
            foreach (var item in _subscribers)
                item.Subscriber.MarkPipelineStateHashDirty();
        }
    }

    /// <summary>Create dependency onto another pipeline state.</summary>
    protected PipelineStateSubscription CreateDependency(PipelineStateTracker? dependency)
    {
        return new PipelineStateSubscription(dependency, this);
    }

    /// <summary>Add reference to subscriber pipeline state tracker.</summary>
    internal void AddSubscriberReference(PipelineStateTracker? subscriber)
    {
        if (subscriber is null)
            return;

        var index = FindSubscriberIndex(subscriber);
        if (index >= 0)
        {
            var item = _subscribers[index];
            ++item.Count;
            _subscribers[index] = item;
        }
        else
            _subscribers.Add(new SubscriberReference(subscriber));

        subscriber.MarkPipelineStateHashDirty();
    }

    /// <summary>Remove reference to subscriber pipeline state tracker.</summary>
    internal void RemoveSubscriberReference(PipelineStateTracker? subscriber)
    {
        if (subscriber is null)
            return;

        var index = FindSubscriberIndex(subscriber);
        var item = _subscribers[index];
        --item.Count;
        if (item.Count == 0)
            _subscribers.RemoveAt(index);
        else
            _subscribers[index] = item;

        subscriber.MarkPipelineStateHashDirty();
    }

    /// <summary>Recalculate hash (must not be non zero). Shall be save to call from multiple threads as long as the object is not changing.</summary>
    protected abstract uint RecalculatePipelineStateHash();

    /// <summary>Find subscriber iterator by pointer.</summary>
    private int FindSubscriberIndex(PipelineStateTracker subscriber)
    {
        for (var i = 0; i < _subscribers.Count; ++i)
        {
            if (ReferenceEquals(_subscribers[i].Subscriber, subscriber))
                return i;
        }

        return -1;
    }

    private struct SubscriberReference
    {
        public SubscriberReference(PipelineStateTracker subscriber)
        {
            Subscriber = subscriber;
            Count = 1;
        }

        public PipelineStateTracker Subscriber;
        public uint Count;
    }

    /// <summary>Cached hash.</summary>
    private uint _pipelineStateHash;

    /// <summary>Other pipeline state trackers depending on this tracker.</summary>
    private readonly List<SubscriberReference> _subscribers = [];
}
