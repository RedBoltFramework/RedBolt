using Microsoft.Extensions.Logging;
using SharpCpp.Model.Ast;
using SharpCpp.Model.Logging;

namespace SharpCpp.Pipeline;

public static class InheritManagedRuntimePolicyPass
{
    private static readonly ILogger Logger = SharpCppLog.For(nameof(InheritManagedRuntimePolicyPass));

    public static void Apply(BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        foreach (var record in compilation.Records)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { record.SymbolId };
            for (var currentBase = record.GetPrimaryBaseRecord(compilation); currentBase is not null; currentBase = currentBase.GetPrimaryBaseRecord(compilation))
            {
                if (!visited.Add(currentBase.SymbolId))
                {
                    Logger.LogWarning(
                        "Detected primary base cycle while inheriting runtime policy for '{Record}'. Revisited '{Base}'.",
                        record.CppQualifiedName,
                        currentBase.CppQualifiedName);
                    break;
                }

                if (string.IsNullOrWhiteSpace(record.ManagedInstanceCacheFactoryExpression) &&
                    !string.IsNullOrWhiteSpace(currentBase.ManagedInstanceCacheFactoryExpression))
                {
                    record.ManagedInstanceCacheFactoryExpression = currentBase.ManagedInstanceCacheFactoryExpression;
                }

                if (string.IsNullOrWhiteSpace(record.ManagedInstanceCacheTypeName) &&
                    !string.IsNullOrWhiteSpace(currentBase.ManagedInstanceCacheTypeName))
                {
                    record.ManagedInstanceCacheTypeName = currentBase.ManagedInstanceCacheTypeName;
                }

                if (!currentBase.EmitDefaultNativeReleaseOverride)
                {
                    record.EmitDefaultNativeReleaseOverride = false;
                }
            }
        }
    }
}
