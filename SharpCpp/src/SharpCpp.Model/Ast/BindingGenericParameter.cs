namespace SharpCpp.Model.Ast;

public sealed class BindingGenericParameter
{
    public BindingGenericParameter(string name, int position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Position = position;
    }

    public string Name { get; }

    public int Position { get; }
}
