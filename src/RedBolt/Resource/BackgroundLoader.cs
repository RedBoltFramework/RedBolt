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
    private readonly ResourceCache owner;
    private readonly object sync = new();
    private readonly Dictionary<(Type Type, string Name), BackgroundLoadItem> queue = new();

    internal BackgroundLoader(ResourceCache owner)
    {
        this.owner = owner;
    }

    public bool QueueResource(Type type, string name, bool sendEventOnFailure = true, Resource? caller = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!owner.CanBackgroundLoad(type))
            return false;

        var sanitizedName = owner.SanitateResourceName(name);
        var key = (type, sanitizedName);

        lock (sync)
        {
            if (queue.ContainsKey(key) || owner.HasResource(type, sanitizedName))
                return false;

            queue.Add(key, new BackgroundLoadItem
            {
                Type = type,
                Name = sanitizedName,
                SendEventOnFailure = sendEventOnFailure,
                LoadTask = Task.Run(() => owner.LoadBackgroundResource(type, sanitizedName))
            });
        }

        return true;
    }

    public void WaitForResource(Type type, string name)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var key = (type, owner.SanitateResourceName(name));
        BackgroundLoadItem? item;
        lock (sync)
            queue.TryGetValue(key, out item);

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
            lock (sync)
            {
                completed = queue
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
        lock (sync)
            return queue.Count;
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

        lock (sync)
        {
            if (!queue.Remove(key))
                return;
        }

        if (resource is not null)
            owner.StoreBackgroundResource(key.Type, key.Name, resource);
    }
}
