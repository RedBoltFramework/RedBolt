namespace SharpCpp.Model.Ast;

public sealed class RecordBinding : BindingNamedContainerNode
{
    private readonly List<RecordBaseBinding> _bases = new();
    private readonly List<BindingGenericParameter> _genericParameters = new();
    private readonly List<RecordGenericInstantiationBinding> _supportedGenericInstantiations = new();

    public RecordBinding(
        string symbolId,
        string nativeName,
        string managedName,
        string? cppQualifiedName = null,
        bool isHandleType = true,
        string? marshallerTypeName = null)
        : base(symbolId, nativeName, managedName)
    {
        CppQualifiedName = string.IsNullOrWhiteSpace(cppQualifiedName) ? nativeName : cppQualifiedName;
        IsHandleType = isHandleType;
        HandleMarshallerTypeName = marshallerTypeName;
    }

    public override BindingNodeKind Kind => BindingNodeKind.Record;

    public bool IsHandleType { get; set; }

    public string? ManagedBaseTypeName { get; set; }

    /// <summary>
    /// Format string for generic constraints on the managed base class declaration.
    /// Uses {0}, {1} placeholders referencing <see cref="GenericParameters"/> by position.
    /// Example: "where {0} : notnull"
    /// Set by the marshaller from the <c>managed_base_type_constraints</c> annotation field.
    /// </summary>
    public string? ManagedBaseTypeConstraints { get; set; }

    public bool IsDirector { get; set; }

    public bool HasCopyConstructor { get; set; }

    public bool IsAbstract { get; set; }

    public bool HasPureVirtualMembers => Members.OfType<FunctionBinding>().Any(static f => f.IsPureVirtual);
    public bool IsEffectivelyAbstract => IsAbstract || HasPureVirtualMembers;

    public bool IsPolymorphic { get; set; }

    public IList<RecordBaseBinding> Bases => _bases;

    public IList<BindingGenericParameter> GenericParameters => _genericParameters;

    public IList<RecordGenericInstantiationBinding> SupportedGenericInstantiations => _supportedGenericInstantiations;

    public bool IsGeneric => GenericParameters.Count > 0;

    public string? ManagedTypeFormat { get; set; }

    public string? ManagedNamespace { get; set; }

    public string CppQualifiedName { get; set; }

    public string? HandleMarshallerTypeName { get; set; }

    public string? ManagedInstanceCacheFactoryExpression { get; set; }

    public string? ManagedInstanceCacheTypeName { get; set; }

    public bool EmitDefaultNativeReleaseOverride { get; set; } = true;

    public BindingType HandleType => BindingType.CreateHandle(
        $"{FormatCppTypeName(GenericParameters.Select(static parameter => BindingType.CreateGenericParameter(parameter.Name)))}*",
        ManagedName,
        SymbolId,
        marshallerTypeName: HandleMarshallerTypeName,
        genericArguments: GenericParameters.Select(static parameter => BindingType.CreateGenericParameter(parameter.Name)));

    public string FormatCppTypeName(IEnumerable<BindingType>? typeArguments = null)
    {
        var arguments = (typeArguments ?? Array.Empty<BindingType>()).ToArray();
        return arguments.Length == 0
            ? CppQualifiedName
            : $"{CppQualifiedName}<{string.Join(", ", arguments.Select(static argument => argument.CppTypeName))}>";
    }

    public string FormatManagedTypeName(IEnumerable<BindingType>? typeArguments = null)
    {
        var arguments = (typeArguments ?? Array.Empty<BindingType>()).ToArray();
        var format = string.IsNullOrWhiteSpace(ManagedTypeFormat) ? ManagedName : ManagedTypeFormat!;
        if (arguments.Length == 0)
        {
            return format;
        }

        var renderedArguments = arguments.Select(static argument => argument.ManagedTypeName).Cast<object>().ToArray();
        return format.Contains('{', StringComparison.Ordinal)
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, format, renderedArguments)
            : $"{format}<{string.Join(", ", arguments.Select(static argument => argument.ManagedTypeName))}>";
    }
}
