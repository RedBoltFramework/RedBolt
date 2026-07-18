namespace SharpCpp.Model.Ast;

public sealed class RecordGenericInstantiationBinding
{
    private static int s_instantiationCounter;
    private string? _suffix;
    private readonly BindingType[] _typeArguments;

    public RecordGenericInstantiationBinding(IEnumerable<BindingType> typeArguments)
    {
        ArgumentNullException.ThrowIfNull(typeArguments);

        _typeArguments = typeArguments.ToArray();
    }

    public IReadOnlyList<BindingType> TypeArguments => _typeArguments;

    public void UpdateTypeArgument(int index, BindingType updatedType)
    {
        _typeArguments[index] = updatedType;
    }

    public string Suffix
    {
        get
        {
            if (_suffix is null)
            {
                _suffix = TypeArguments.Count == 0
                    ? string.Empty
                    : $"__{++s_instantiationCounter}";
            }

            return _suffix;
        }
    }
}
