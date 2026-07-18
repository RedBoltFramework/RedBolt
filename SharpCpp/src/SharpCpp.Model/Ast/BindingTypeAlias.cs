namespace SharpCpp.Model.Ast;

public sealed record BindingTypeAlias(
    string CppQualifiedName,
    string NativeName,
    string ManagedName,
    BindingType TargetType)
{
    public string? ManagedNamespace { get; set; }
}
