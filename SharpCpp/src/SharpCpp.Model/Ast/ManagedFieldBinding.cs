namespace SharpCpp.Model.Ast;

public sealed class ManagedFieldBinding : BindingNamedNode
{
    public ManagedFieldBinding(
        string symbolId,
        string nativeName,
        string managedName,
        string managedTypeName,
        string? initializerExpression = null)
        : base(symbolId, nativeName, managedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedTypeName);

        ManagedTypeName = managedTypeName;
        InitializerExpression = initializerExpression;
    }

    public override BindingNodeKind Kind => BindingNodeKind.ManagedField;

    public override IEnumerable<BindingNode> Children => Array.Empty<BindingNode>();

    public string ManagedTypeName { get; set; }

    public string? InitializerExpression { get; set; }

    public BindingAccessModifier AccessModifier { get; set; } = BindingAccessModifier.Public;

    public bool IsStatic { get; set; } = true;

    public bool IsReadOnly { get; set; } = true;

    public bool IsConst { get; set; }
}
