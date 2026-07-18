namespace SharpCpp.Model.Ast;

public enum BindingParameterDirection
{
    In = 0,
    Ref = 1,
    Out = 2
}

public enum BindingParameterAbiKind
{
    Value = 0,
    Reference = 1,
    Pointer = 2
}

public sealed class ParameterBinding : BindingNamedNode
{
    public ParameterBinding(
        string symbolId,
        string nativeName,
        string managedName,
        BindingType type,
        BindingParameterDirection direction = BindingParameterDirection.In,
        BindingParameterAbiKind abiKind = BindingParameterAbiKind.Value,
        string? cppTypeName = null)
        : base(symbolId, nativeName, managedName)
    {
        ArgumentNullException.ThrowIfNull(type);

        Type = type;
        Direction = direction;
        AbiKind = abiKind;
        CppTypeName = string.IsNullOrWhiteSpace(cppTypeName) ? type.CppTypeName : cppTypeName;
    }

    public override BindingNodeKind Kind => BindingNodeKind.Parameter;

    public override IEnumerable<BindingNode> Children => Array.Empty<BindingNode>();

    public BindingType Type { get; set; }

    public BindingParameterDirection Direction { get; set; }

    public BindingParameterAbiKind AbiKind { get; set; }

    public string CppTypeName { get; set; }

    public string? ManagedDefaultValueExpression { get; set; }

    public bool UseCallbackDelegate { get; set; }

    public string? CallbackDelegateTypeName { get; set; }
}
