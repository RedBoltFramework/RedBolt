namespace SharpCpp.Model.Diagnostics;

public sealed record BindingSourceReference(string FilePath, int Line, int Column)
{
    public override string ToString()
    {
        if (Line <= 0)
        {
            return FilePath;
        }

        return $"{FilePath}({Line},{Column})";
    }
}
