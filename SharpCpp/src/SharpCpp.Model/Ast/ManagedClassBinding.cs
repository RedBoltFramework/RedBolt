namespace SharpCpp.Model.Ast;

public sealed class ManagedClassBinding : BindingNamedContainerNode
{
    private readonly List<string> _bodyCodeBlocks = new();

    public ManagedClassBinding(string symbolId, string nativeName, string managedName)
        : base(symbolId, nativeName, managedName)
    {
    }

    public override BindingNodeKind Kind => BindingNodeKind.ManagedClass;

    public string? ManagedNamespace { get; set; }

    public BindingAccessModifier AccessModifier { get; set; } = BindingAccessModifier.Public;

    public bool IsStatic { get; set; }

    public bool IsPartial { get; set; } = true;

    public bool IsSealed { get; set; }

    public string? BaseTypeName { get; set; }

    public IList<string> BodyCodeBlocks => _bodyCodeBlocks;
}
