using System.Collections.ObjectModel;

using SharpCpp.Model.Diagnostics;

namespace SharpCpp.Model.Ast;

public abstract class BindingNode
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyMetadata = new Dictionary<string, object?>(StringComparer.Ordinal);
    private IBindingNodeCollection? _parentCollection;
    private Dictionary<string, object?>? _metadata;

    public BindingNode? Parent { get; private set; }

    public BindingSourceReference? Source { get; init; }

    public string? HeaderIncludePath { get; set; }

    public abstract BindingNodeKind Kind { get; }

    public abstract IEnumerable<BindingNode> Children { get; }

    public IReadOnlyDictionary<string, object?> Metadata => _metadata ?? EmptyMetadata;

    public IEnumerable<BindingNode> Ancestors()
    {
        for (var current = Parent; current is not null; current = current.Parent)
        {
            yield return current;
        }
    }

    public IEnumerable<TNode> Descendants<TNode>()
        where TNode : BindingNode
    {
        foreach (var child in Children)
        {
            if (child is TNode typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in child.Descendants<TNode>())
            {
                yield return descendant;
            }
        }
    }

    public IEnumerable<TNode> DescendantsAndSelf<TNode>()
        where TNode : BindingNode
    {
        if (this is TNode typedSelf)
        {
            yield return typedSelf;
        }

        foreach (var descendant in Descendants<TNode>())
        {
            yield return descendant;
        }
    }

    public void Detach()
    {
        _parentCollection?.Remove(this);
    }

    public void SetMetadata<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _metadata ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        _metadata[key] = value;
    }

    public T? GetMetadata<T>(string key)
    {
        return TryGetMetadata<T>(key, out var value) ? value : default;
    }

    public bool TryGetMetadata<T>(string key, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_metadata is not null && _metadata.TryGetValue(key, out var rawValue) && rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool HasMetadata(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _metadata is not null && _metadata.ContainsKey(key);
    }

    public bool RemoveMetadata(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _metadata?.Remove(key) == true;
    }

    public void CopyMetadataFrom(BindingNode other, bool overwriteExisting = false)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var pair in other.Metadata)
        {
            if (!overwriteExisting && HasMetadata(pair.Key))
            {
                continue;
            }

            SetMetadata(pair.Key, pair.Value);
        }
    }

    internal virtual void NotifyStructureChanged()
    {
        Parent?.NotifyStructureChanged();
    }

    internal void AttachTo(BindingNode parent, IBindingNodeCollection parentCollection)
    {
        Parent = parent;
        _parentCollection = parentCollection;
    }

    internal void ClearParent()
    {
        Parent = null;
        _parentCollection = null;
    }
}

public abstract class BindingContainerNode : BindingNode
{
    private readonly BindingNodeCollection<BindingNode> _members;

    protected BindingContainerNode()
    {
        _members = new BindingNodeCollection<BindingNode>(this);
    }

    public IList<BindingNode> Members => _members;

    public override IEnumerable<BindingNode> Children => _members;
}

public abstract class BindingNamedNode : BindingNode
{
    protected BindingNamedNode(string symbolId, string nativeName, string managedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedName);

        SymbolId = symbolId;
        NativeName = nativeName;
        ManagedName = managedName;
    }

    public string SymbolId { get; }

    public string NativeName { get; }

    public string ManagedName { get; set; }
}

public abstract class BindingNamedContainerNode : BindingContainerNode
{
    protected BindingNamedContainerNode(string symbolId, string nativeName, string managedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedName);

        SymbolId = symbolId;
        NativeName = nativeName;
        ManagedName = managedName;
    }

    public string SymbolId { get; }

    public string NativeName { get; }

    public string ManagedName { get; set; }
}

internal interface IBindingNodeCollection
{
    BindingNode Owner { get; }

    void Remove(BindingNode node);
}

internal sealed class BindingNodeCollection<TNode> : Collection<TNode>, IBindingNodeCollection
    where TNode : BindingNode
{
    public BindingNodeCollection(BindingNode owner)
    {
        Owner = owner;
    }

    public BindingNode Owner { get; }

    public void Remove(BindingNode node)
    {
        if (node is TNode typedNode)
        {
            base.Remove(typedNode);
        }
    }

    protected override void InsertItem(int index, TNode item)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.Detach();
        base.InsertItem(index, item);
        item.AttachTo(Owner, this);
        Owner.NotifyStructureChanged();
    }

    protected override void SetItem(int index, TNode item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var existingItem = this[index];
        if (ReferenceEquals(existingItem, item))
        {
            return;
        }

        item.Detach();
        existingItem.ClearParent();
        base.SetItem(index, item);
        item.AttachTo(Owner, this);
        Owner.NotifyStructureChanged();
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];
        base.RemoveItem(index);
        item.ClearParent();
        Owner.NotifyStructureChanged();
    }

    protected override void ClearItems()
    {
        foreach (var item in this)
        {
            item.ClearParent();
        }

        base.ClearItems();
        Owner.NotifyStructureChanged();
    }
}
