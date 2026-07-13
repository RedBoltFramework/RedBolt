using System.Threading;

namespace RedBolt;

/// <summary>Holds subscription from one PipelineStateTracker to another. Neither checks nor affects objects lifetime.</summary>
public sealed class PipelineStateSubscription : IDisposable, ICloneable
{
    /// <summary>Owned pointer.</summary>
    private PipelineStateTracker? _sender;

    /// <summary>Owner.</summary>
    private PipelineStateTracker? _subscriber;

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

    // TODO: port: C# has no move construction, move assignment, or copy assignment operators; Clone represents copy construction.

    /// <summary>Destruct.</summary>
    public void Dispose()
    {
        Reset(null, null);
        GC.SuppressFinalize(this);
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
}

/// <summary>Helper class to track pipeline state changes caused by derived class.</summary>
public abstract class PipelineStateTracker : Object
{
    private sealed class SubscriberReference
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

    /// <summary>Return (partial) pipeline state hash. Save to call from multiple threads as long as the object is not changing.</summary>
    public uint GetPipelineStateHash()
    {
        var hash = Volatile.Read(ref _pipelineStateHash);
        if (hash != 0)
            return hash;

        var newHash = Math.Max(1u, RecalculatePipelineStateHash());
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
            ++_subscribers[index].Count;
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
}
