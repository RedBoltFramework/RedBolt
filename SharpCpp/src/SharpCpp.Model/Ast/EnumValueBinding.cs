namespace SharpCpp.Model.Ast;

public sealed class EnumValueBinding
{
    public EnumValueBinding(string nativeName, string managedName, string valueExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedName);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueExpression);

        NativeName = nativeName;
        ManagedName = managedName;
        ValueExpression = valueExpression;
    }

    public string NativeName { get; }

    public string ManagedName { get; set; }

    public string ValueExpression { get; set; }
}
