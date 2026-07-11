namespace RedBolt;

internal sealed class BackgroundLoadItem
{
    public required Type Type { get; init; }

    public required string Name { get; init; }

    public required bool SendEventOnFailure { get; init; }

    public required Task<Resource?> LoadTask { get; init; }
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

            _queue.Add(key, new BackgroundLoadItem
            {
                Type = type,
                Name = sanitizedName,
                SendEventOnFailure = sendEventOnFailure,
                LoadTask = Task.Run(() => _owner.LoadBackgroundResource(type, sanitizedName))
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
                    .Where(static item => item.Value.LoadTask.IsCompleted)
                    .ToList();
            }

            if (completed.Count == 0)
                break;

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
}
