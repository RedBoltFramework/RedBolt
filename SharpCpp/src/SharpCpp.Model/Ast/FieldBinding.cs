namespace SharpCpp.Model.Ast;

public sealed class FieldBinding : BindingNamedNode
{
    public FieldBinding(
        string symbolId,
        string nativeName,
        string managedName,
        BindingType type,
        string cppTypeName,
        string owningRecordCppTypeName,
        bool isReadOnly)
        : this(symbolId, nativeName, managedName, type, cppTypeName, owningRecordCppTypeName, isReadOnly, isReference: false, isPointer: false)
    {
    }

    public FieldBinding(
        string symbolId,
        string nativeName,
        string managedName,
        BindingType type,
        string cppTypeName,
        string owningRecordCppTypeName,
        bool isReadOnly = false,
        bool isReference = false,
        bool isPointer = false)
        : base(symbolId, nativeName, managedName)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(cppTypeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(owningRecordCppTypeName);

        Type = type;
        CppTypeName = cppTypeName;
        OwningRecordCppTypeName = owningRecordCppTypeName;
        IsReadOnly = isReadOnly;
        IsReference = isReference;
        IsPointer = isPointer;
    }

    public override BindingNodeKind Kind => BindingNodeKind.Field;

    public override IEnumerable<BindingNode> Children => Array.Empty<BindingNode>();

    public BindingType Type { get; set; }

    public string CppTypeName { get; set; }

    public string OwningRecordCppTypeName { get; set; }

    public bool IsReadOnly { get; set; }

    public bool IsReference { get; set; }

    public bool IsPointer { get; set; }

    public BindingAccessModifier AccessModifier { get; set; } = BindingAccessModifier.Public;

    public bool ReturnsOwnedHandle { get; set; }
}
