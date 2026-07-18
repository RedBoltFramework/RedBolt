namespace SharpCpp.Model.Ast;

public sealed class RecordBaseBinding
{
    public RecordBaseBinding(
        string recordId,
        string nativeName,
        string managedName,
        string cppQualifiedName,
        bool isVirtual)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedName);
        ArgumentException.ThrowIfNullOrWhiteSpace(cppQualifiedName);

        RecordId = recordId;
        NativeName = nativeName;
        ManagedName = managedName;
        CppQualifiedName = cppQualifiedName;
        IsVirtual = isVirtual;
    }

    public string RecordId { get; }

    public string NativeName { get; }

    public string ManagedName { get; }

    public string CppQualifiedName { get; }

    public bool IsVirtual { get; }
}
