using SharpCpp.Model.Ast;

namespace SharpCpp.Pipeline;

public static class SilentlyExcludeExternalDeclarationsPass
{
    public static void Apply(BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        foreach (var record in compilation.Records)
        {
            if (ShouldSilentlyExcludeRecord(record))
            {
                record.SetSilentExclusionReason($"record '{record.CppQualifiedName}' is defined outside headers owned by this compilation");
            }
        }

        foreach (var function in compilation.Functions)
        {
            if (ShouldSilentlyExcludeFunction(function, out var reason))
            {
                function.SetSilentExclusionReason(reason);
            }
        }
    }

    private static bool ShouldSilentlyExcludeRecord(RecordBinding record)
    {
        if (!record.GetMetadata<bool>(MetadataKeys.PlaceholderRecord))
        {
            return false;
        }

        if (record.GetMetadata<bool>(MetadataKeys.SuppressEmission))
        {
            return false;
        }

        return !IsExplicitlyConfiguredRecord(record);
    }

    private static bool IsExplicitlyConfiguredRecord(RecordBinding record)
    {
        return !string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) ||
            !string.IsNullOrWhiteSpace(record.ManagedTypeFormat) ||
            !string.IsNullOrWhiteSpace(record.HandleMarshallerTypeName) ||
            record.Members.OfType<FunctionBinding>().Any(static member => member.HasMetadata(MetadataKeys.MarshallerExposedMethod));
    }

    private static bool ShouldSilentlyExcludeFunction(FunctionBinding function, out string reason)
    {
        if (string.Equals(function.NativeName, "begin", StringComparison.Ordinal) ||
            string.Equals(function.NativeName, "end", StringComparison.Ordinal) ||
            string.Equals(function.NativeName, "size", StringComparison.Ordinal))
        {
            reason = $"callable '{function.CppQualifiedName}' is an iterator endpoint not projected into the managed surface";
            return true;
        }

        if (string.Equals(function.NativeName, "operator=", StringComparison.Ordinal))
        {
            reason = $"assignment operator '{function.CppQualifiedName}' is not projected into the managed surface";
            return true;
        }

        reason = string.Empty;
        return false;
    }
}
