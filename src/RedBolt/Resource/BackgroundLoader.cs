namespace RedBolt;

// C++: BackgroundLoadItem is a struct in BackgroundLoader.h with URHO3D_API.
// C++: stores SharedPtr<Resource> resource_ (the actual resource object, not a Task).
// C++: stores dependencies as ea::hash_set<ea::pair<StringHash, StringHash>>
// C++: stores dependents as ea::hash_set<ea::pair<StringHash, StringHash>>
// C++: has bool sendEventOnFailure_ field.
internal sealed class BackgroundLoadItem
{
    public required Type Type { get; init; }

    public required string Name { get; init; }

    public required bool SendEventOnFailure { get; init; }

    public Task<Resource?> LoadTask { get; set; } = Task.FromResult<Resource?>(null);

    public HashSet<(Type Type, string Name)> Dependencies { get; } = [];

    public HashSet<(Type Type, string Name)> Dependents { get; } = [];
}

// C++: BackgroundLoader inherits from RefCounted and Thread.
// C++: has private ThreadFunction() override — background loading loop running on explicit thread with mutex lock/unlock/sleep.
// C++: uses Mutex backgroundLoadMutex_ for thread-safe queue access.
public sealed class BackgroundLoader : IDisposable
{
    private readonly ResourceCache _owner;
    private readonly object _sync = new();
    private readonly Dictionary<(Type Type, string Name), BackgroundLoadItem> _queue = new();

    internal BackgroundLoader(ResourceCache owner)
    {
        this._owner = owner;
    }

    public void Dispose()
    {
        BackgroundLoadItem[] items;
        lock (_sync)
            items = _queue.Values.ToArray();

        foreach (var item in items)
        {
            try
            {
                item.LoadTask.GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        lock (_sync)
            _queue.Clear();
    }

    // C++: creates resource via DynamicCast<Resource>(owner_->GetContext()->CreateObject(type))
    // C++: sets resource name and async state to ASYNC_QUEUED eagerly.
    // C++: starts thread via if (!IsStarted()) Run();
    public bool QueueResource(Type type, string name, bool sendEventOnFailure = true, Resource? caller = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_owner.CanBackgroundLoad(type))
            return false;

        var sanitizedName = _owner.SanitateResourceName(name);
        var key = (type, sanitizedName);

        lock (_sync)
        {
            if (_queue.ContainsKey(key) || _owner.HasResource(type, sanitizedName))
                return false;

            var item = new BackgroundLoadItem
            {
                Type = type,
                Name = sanitizedName,
                SendEventOnFailure = sendEventOnFailure
            };
            _queue.Add(key, item);

            if (caller is not null)
            {
                var callerKey = (caller.GetType(), _owner.SanitateResourceName(caller.Name));
                if (_queue.TryGetValue(callerKey, out var callerItem))
                {
                    item.Dependents.Add(callerKey);
                    callerItem.Dependencies.Add(key);
                }
            }

            item.LoadTask = Task.Run(() =>
            {
                try
                {
                    return _owner.LoadBackgroundResource(type, sanitizedName, item.SendEventOnFailure);
                }
                finally
                {
                    NotifyLoadComplete(key);
                }
            });
        }

        return true;
    }

    // C++: acquires mutex, finds item, releases mutex, polls resource state in loop with Time::Sleep(1),
    // then calls FinishBackgroundLoading(item). Erases by key via mutex.
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

        item.LoadTask.Wait();
        List<(Type Type, string Name)> dependencies;
        lock (_sync)
            dependencies = item.Dependencies.ToList();
        foreach (var dependency in dependencies)
            WaitForResource(dependency.Type, dependency.Name);
        FinishResource(key, item);
    }

    // C++: iterates queue with mutex, calls FinishBackgroundLoading for each ready item, breaks on time limit.
    public void FinishResources(int maxMs)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            List<KeyValuePair<(Type Type, string Name), BackgroundLoadItem>> completed;
            lock (_sync)
            {
                completed = _queue
                    .Where(static item => item.Value.LoadTask.IsCompleted && item.Value.Dependencies.Count == 0)
                    .ToList();
            }

            if (completed.Count == 0)
            {
                lock (_sync)
                {
                    if (_queue.Count == 0 || stopwatch.ElapsedMilliseconds >= Math.Max(maxMs, 1))
                        break;
                }
                Thread.Yield();
                continue;
            }

            foreach (var item in completed)
            {
                FinishResource(item.Key, item.Value);
                if (stopwatch.ElapsedMilliseconds >= Math.Max(maxMs, 1))
                    return;
            }
        }
    }

    public int GetNumQueuedResources()
    {
        lock (_sync)
            return _queue.Count;
    }

    // TODO: port - C++ code:
    // void ThreadFunction() override — background loading loop.

    private void FinishResource((Type Type, string Name) key, BackgroundLoadItem item)
    {
        Resource? resource;
        try
        {
            resource = item.LoadTask.GetAwaiter().GetResult();
        }
        catch
        {
            resource = null;
        }

        lock (_sync)
        {
            if (!_queue.Remove(key))
                return;
        }

        if (resource is null)
            return;

        var success = resource.AsyncLoadState == AsyncLoadState.Success && resource.EndLoad();
        resource.SetAsyncLoadState(AsyncLoadState.Done);

        if (!success && item.SendEventOnFailure)
        {
            var loadFailedEventData = new ResourceEvents.LoadFailed { ResourceName = resource.Name };
            _owner.SendEvent(ref loadFailedEventData);
        }

        if (success || _owner.GetReturnFailedResources())
            _owner.StoreBackgroundResource(key.Type, key.Name, resource);

        var backgroundLoadedEventData = new ResourceEvents.ResourceBackgroundLoaded
        {
            ResourceName = resource.Name,
            Success = success,
            Resource = resource
        };
        _owner.SendEvent(ref backgroundLoadedEventData);
    }

    private void NotifyLoadComplete((Type Type, string Name) key)
    {
        lock (_sync)
        {
            if (!_queue.TryGetValue(key, out var item))
                return;
            foreach (var dependent in item.Dependents)
            {
                if (_queue.TryGetValue(dependent, out var dependentItem))
                    dependentItem.Dependencies.Remove(key);
            }
            item.Dependents.Clear();
        }
    }
}
