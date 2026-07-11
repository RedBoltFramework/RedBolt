namespace RedBolt;

internal sealed class BackgroundLoadItem
{
    public required Type Type { get; init; }

    public required string Name { get; init; }

    public required bool SendEventOnFailure { get; init; }

    public Task<Resource?> LoadTask { get; set; } = Task.FromResult<Resource?>(null);

    public HashSet<(Type Type, string Name)> Dependencies { get; } = [];

    public HashSet<(Type Type, string Name)> Dependents { get; } = [];
}

public sealed class BackgroundLoader
{
    private readonly ResourceCache _owner;
    private readonly object _sync = new();
    private readonly Dictionary<(Type Type, string Name), BackgroundLoadItem> _queue = new();

    internal BackgroundLoader(ResourceCache owner)
    {
        this._owner = owner;
    }

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
                    return _owner.LoadBackgroundResource(type, sanitizedName);
                }
                finally
                {
                    NotifyLoadComplete(key);
                }
            });
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

        item.LoadTask.Wait();
        List<(Type Type, string Name)> dependencies;
        lock (_sync)
            dependencies = item.Dependencies.ToList();
        foreach (var dependency in dependencies)
            WaitForResource(dependency.Type, dependency.Name);
        FinishResource(key, item);
    }

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
        if (success || _owner.GetReturnFailedResources())
            _owner.StoreBackgroundResource(key.Type, key.Name, resource);
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
