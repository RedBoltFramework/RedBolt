namespace SharpCpp.Model.Ast;

public sealed class FunctionBinding : BindingNamedNode
{
    private readonly BindingNodeCollection<ParameterBinding> _parameters;
    private readonly List<BindingGenericParameter> _genericParameters = new();

    public FunctionBinding(
        string symbolId,
        string nativeName,
        string managedName,
        BindingType returnType,
        string? entryPointName = null,
        string? cppReturnTypeName = null,
        string? cppQualifiedName = null)
        : base(symbolId, nativeName, managedName)
    {
        ArgumentNullException.ThrowIfNull(returnType);

        ReturnType = returnType;
        EntryPointName = string.IsNullOrWhiteSpace(entryPointName) ? nativeName : entryPointName;
        CppReturnTypeName = string.IsNullOrWhiteSpace(cppReturnTypeName) ? returnType.CppTypeName : cppReturnTypeName;
        CppQualifiedName = string.IsNullOrWhiteSpace(cppQualifiedName) ? nativeName : cppQualifiedName;
        _parameters = new BindingNodeCollection<ParameterBinding>(this);
        ImportMethodName = ManagedName;
    }

    public override BindingNodeKind Kind => BindingNodeKind.Function;

    public override IEnumerable<BindingNode> Children => _parameters;

    public BindingType ReturnType { get; set; }

    public string EntryPointName { get; set; }

    public string CppReturnTypeName { get; set; }

    public string CppQualifiedName { get; set; }

    public string ImportMethodName { get; set; }

    public string? ManagedNamespace { get; set; }

    public string? ManagedClassName { get; set; }

    public bool IsStatic { get; set; } = true;

    public BindingAccessModifier AccessModifier { get; set; } = BindingAccessModifier.Public;

    public bool IsVirtual { get; set; }

    public bool IsPureVirtual { get; set; }

    public bool IsFinal { get; set; }

    public bool IsOverride { get; set; }

    public string? NativeDispatchRecordCppTypeName { get; set; }

    public string? OwningRecordCppTypeName { get; set; }

    public bool IsConstructor { get; set; }

    public bool IsConstMethod { get; set; }

    public string? OperatorToken { get; set; }

    public bool IsConversionOperator { get; set; }

    public bool IsExplicitConversionOperator { get; set; }

    public bool ReturnsOwnedHandle { get; set; }

    public IList<BindingGenericParameter> GenericParameters => _genericParameters;

    public bool IsGeneric => _genericParameters.Count > 0;

    public IList<RecordGenericInstantiationBinding> SupportedGenericInstantiations { get; } = new List<RecordGenericInstantiationBinding>();

    public IList<ParameterBinding> Parameters => _parameters;
}
