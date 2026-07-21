namespace RedBolt;

using Microsoft.Extensions.Logging;

public sealed class BackgroundLoadItem
{
    public Resource? Resource { get; set; }

    public required bool SendEventOnFailure { get; init; }

    public Task LoadTask { get; set; } = Task.CompletedTask;

    public HashSet<(Type Type, string Name)> Dependencies { get; } = [];

    public HashSet<(Type Type, string Name)> Dependents { get; } = [];
}

public sealed class BackgroundLoader : IDisposable
{
    private readonly ResourceCache _owner;
    private readonly object _sync = new();
    private readonly Dictionary<(Type Type, string Name), BackgroundLoadItem> _queue = new();

    public BackgroundLoader(ResourceCache owner)
    {
        this._owner = owner;
    }

    public void Dispose()
    {
        BackgroundLoadItem[] items;
        lock (_sync)
        {
            items = _queue.Values.ToArray();
            _queue.Clear();
        }

        foreach (var item in items)
            item.LoadTask.GetAwaiter().GetResult();
    }

    public bool QueueResource(Type type, string name, bool sendEventOnFailure, Resource? caller)
    {
        var key = (type, name);

        lock (_sync)
        {
            if (_queue.ContainsKey(key))
                return false;

            var item = new BackgroundLoadItem
            {
                SendEventOnFailure = sendEventOnFailure
            };
            _queue.Add(key, item);

            var resource = Context.Instance.CreateObject(type.GetClassName()) as Resource;
            if (resource is null)
            {
                Log.GetLogger().LogError("Could not load unknown resource type {ResourceType}", type.GetClassName());
                if (sendEventOnFailure && Environment.CurrentManagedThreadId == WorkQueue.GetMainThreadId())
                {
                    var eventData = new ResourceEvents.UnknownResourceType { ResourceType = type.GetClassName() };
                    _owner.SendEvent(ref eventData);
                }
                _queue.Remove(key);
                return false;
            }

            item.Resource = resource;
            resource.OwnerCache = _owner;
            resource.SetName(name);
            resource.SetAsyncLoadState(AsyncLoadState.Queued);

            if (caller is not null)
            {
                var callerKey = (caller.GetType(), caller.Name);
                if (_queue.TryGetValue(callerKey, out var callerItem))
                {
                    item.Dependents.Add(callerKey);
                    callerItem.Dependencies.Add(key);
                }
                else
                    Log.GetLogger().LogWarning(
                        "Resource {ResourceName} requested for a background loaded resource but was not in the background load queue",
                        caller.Name);
            }

            item.LoadTask = Task.Run(() => ThreadFunction(key, item));
        }

        return true;
    }

    public void WaitForResource(Type type, string name)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var key = (type, _owner.SanitateResourceName(name));
        BackgroundLoadItem? item;
        lock (_sync)
            _queue.TryGetValue(key, out item);

        if (item is null)
            return;

        for (;;)
        {
            int numDependencies;
            AsyncLoadState state;
            lock (_sync)
            {
                numDependencies = item.Dependencies.Count;
                state = item.Resource!.AsyncLoadState;
            }

            if (numDependencies > 0 || state == AsyncLoadState.Queued || state == AsyncLoadState.Loading)
                Thread.Sleep(1);
            else
                break;
        }

        FinishBackgroundLoading(item);
        lock (_sync)
            _queue.Remove(key);
    }

    public void FinishResources(int maxMs)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            (Type Type, string Name) key = default;
            BackgroundLoadItem? item = null;
            var numDependencies = 0;
            var state = AsyncLoadState.Done;
            lock (_sync)
            {
                foreach (var candidate in _queue)
                {
                    key = candidate.Key;
                    item = candidate.Value;
                    numDependencies = item.Dependencies.Count;
                    state = item.Resource!.AsyncLoadState;
                    break;
                }
            }

            if (item is null)
                break;

            if (numDependencies == 0 && state != AsyncLoadState.Queued && state != AsyncLoadState.Loading)
            {
                FinishBackgroundLoading(item);
                lock (_sync)
                    _queue.Remove(key);
            }

            if (timer.ElapsedMilliseconds >= maxMs)
                break;
        }
    }

    public int GetNumQueuedResources()
    {
        lock (_sync)
            return _queue.Count;
    }

    private void ThreadFunction((Type Type, string Name) key, BackgroundLoadItem item)
    {
        var resource = item.Resource!;
        var success = false;
        using var file = _owner.GetFile(resource.Name, item.SendEventOnFailure);
        if (file is not null)
        {
            resource.SetAsyncLoadState(AsyncLoadState.Loading);
            success = resource.BeginLoad(file);
        }

        lock (_sync)
        {
            foreach (var dependent in item.Dependents)
            {
                if (_queue.TryGetValue(dependent, out var dependentItem))
                    dependentItem.Dependencies.Remove(key);
            }
            item.Dependents.Clear();
            resource.SetAsyncLoadState(success ? AsyncLoadState.Success : AsyncLoadState.Fail);
        }
    }

    private void FinishBackgroundLoading(BackgroundLoadItem item)
    {
        var resource = item.Resource!;
        var success = resource.AsyncLoadState == AsyncLoadState.Success;
        if (success)
            success = resource.EndLoad();
        resource.SetAsyncLoadState(AsyncLoadState.Done);

        if (!success && item.SendEventOnFailure)
        {
            var loadFailedEventData = new ResourceEvents.LoadFailed { ResourceName = resource.Name };
            _owner.SendEvent(ref loadFailedEventData);
        }

        if (success || _owner.GetReturnFailedResources())
            _owner.AddManualResource(resource);

        var backgroundLoadedEventData = new ResourceEvents.ResourceBackgroundLoaded
        {
            ResourceName = resource.Name,
            Success = success,
            Resource = resource
        };
        _owner.SendEvent(ref backgroundLoadedEventData);
    }
}
