using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SharpCpp.Model.Ast;
using SharpCpp.Model.Logging;
using SharpCpp.Parsing;

namespace SharpCpp.Pipeline;

public static class BindingCompilationBootstrapPipeline
{
    private static readonly ILogger Logger = SharpCppLog.For(nameof(BindingCompilationBootstrapPipeline));

    /// <summary>
    /// Apply the bootstrap pipeline using ClangSharp-backed Phase 2 metadata extraction.
    /// The C++ compiler resolves template specializations; the AST walk replaces
    /// all fragile regex patterns for finding Marshaller/ReturnMarsharker declarations
    /// and extracting method signatures from struct body text.
    /// </summary>
    /// <param name="compilation">The binding compilation to process.</param>
    /// <param name="marshallerConfigFiles">Paths to marshaller configuration headers only
    /// (e.g., Marshaller.hpp), not all API header files.</param>
    /// <param name="clangArgs">Clang compiler arguments (same as used in Phase 1 parse).</param>
    /// <param name="marshallerClangArgs">Optional Clang compiler arguments for marshaller configuration parsing.
    /// Use this when the API surface and marshaller headers need different language modes.</param>
    public static void Apply(
        BindingCompilation compilation,
        IEnumerable<string> marshallerConfigFiles,
        IReadOnlyList<string> clangArgs,
        IReadOnlyList<string>? marshallerClangArgs = null)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(marshallerConfigFiles);

        RunTimed("NormalizeManagedNamespaces", () => NormalizeManagedNamespacesPass.Apply(compilation));
        RunTimed("MarshallerMetadataPhase2", () => MarshallerMetadataPhase2.ApplyWithClang(compilation, marshallerConfigFiles, marshallerClangArgs ?? clangArgs));
        RunTimed("SilentlyExcludeExternalDeclarations", () => SilentlyExcludeExternalDeclarationsPass.Apply(compilation));
        RunTimed("GenericInstantiation", () => BindingCompilationGenericInstantiationPass.Apply(compilation));
        RunTimed("DegradeUnsupportedSurface", () => DegradeUnsupportedSurfacePass.Apply(compilation));
        RunTimed("Inheritance", () => BindingCompilationInheritancePass.Apply(compilation));
        RunTimed("InheritManagedRuntimePolicy", () => InheritManagedRuntimePolicyPass.Apply(compilation));
        RunTimed("Polymorphism", () => BindingCompilationPolymorphismPass.Apply(compilation));
        RunTimed("ValidateBootstrap", () => ValidateBootstrapSurfacePass.Apply(compilation));
        RunTimed("SilentlyExcludeConstOverloadMethods", () => SilentlyExcludeConstOverloadMethodsPass.Apply(compilation));
        RunTimed("DegradeValidatedSurface", () => DegradeUnsupportedSurfacePass.Apply(compilation));

        // Prune aliases whose target records were degraded or excluded by
        // any of the pipeline passes (e.g. records outside configured
        // namespace mappings, unsupported surface, etc.).
        RunTimed("PruneDegradedAliases", () => PruneAliasesTargetingDegradedRecords(compilation));

        // Invalidate cached member lookups so rendering picks up any members
        // added by pipeline passes (MI projections, generic instantiations, etc.)
        compilation.InvalidateCaches();
    }

    private static void RunTimed(string name, Action action)
    {
        var sw = Stopwatch.StartNew();
        Logger.LogTrace("Pass '{Name}' starting", name);
        try
        {
            action();
        }
        catch
        {
            Logger.LogWarning("Pass '{Name}' FAILED after {Duration}", name, FormatDuration(sw.Elapsed));
            throw;
        }
        Logger.LogTrace("Pass '{Name}' completed in {Duration}", name, FormatDuration(sw.Elapsed));
    }

    private static string FormatDuration(TimeSpan elapsed) => elapsed.TotalSeconds < 1 ? $"{elapsed.TotalMilliseconds:F0}ms" : $"{elapsed.TotalSeconds:F1}s";

    private static void PruneAliasesTargetingDegradedRecords(BindingCompilation compilation)
    {
        if (compilation.TypeAliases.Count == 0)
        {
            return;
        }

        for (var i = compilation.TypeAliases.Count - 1; i >= 0; i--)
        {
            var alias = compilation.TypeAliases[i];
            if (ShouldPruneAliasTarget(alias, compilation))
            {
                compilation.TypeAliases.RemoveAt(i);
            }
        }
    }

    private static bool ShouldPruneAliasTarget(BindingTypeAlias alias, BindingCompilation compilation)
    {
        return ShouldPruneAliasType(alias.TargetType, compilation);
    }

    private static bool ShouldPruneAliasType(BindingType type, BindingCompilation compilation)
    {
        if (type.Kind == BindingTypeKind.Handle && type.RecordId is not null)
        {
            var records = compilation.Records
                .Where(r => string.Equals(r.SymbolId, type.RecordId, StringComparison.Ordinal))
                .ToArray();
            if (records.Length == 0 || records.All(ShouldPruneHandleAliasTarget))
            {
                return true;
            }
        }
        else if (type.Kind == BindingTypeKind.Enum)
        {
            var enumTarget = compilation.FindEnumBySymbolId(type.EnumId);
            if (enumTarget is null || enumTarget.IsIgnored())
            {
                return true;
            }
        }
        else if (type.Kind == BindingTypeKind.Primitive)
        {
            if (IsUnresolvedConfiguredPrimitive(type))
            {
                return true;
            }

            var cppName = type.CppTypeName;
            if (!string.IsNullOrWhiteSpace(cppName))
            {
                var backingRecord = compilation.Records.FirstOrDefault(r =>
                    string.Equals(r.CppQualifiedName, cppName, StringComparison.Ordinal) ||
                    string.Equals(r.NativeName, cppName, StringComparison.Ordinal));
                if (backingRecord is not null && backingRecord.IsIgnored())
                {
                    return true;
                }

                var backingEnum = compilation.Enums.FirstOrDefault(e =>
                    string.Equals(e.CppQualifiedName, cppName, StringComparison.Ordinal) ||
                    string.Equals(e.NativeName, cppName, StringComparison.Ordinal));
                if (backingEnum is not null && backingEnum.IsIgnored())
                {
                    return true;
                }
            }
        }

        foreach (var argument in type.GenericArguments)
        {
            if (ShouldPruneAliasType(argument, compilation))
                return true;
        }

        return false;
    }

    private static bool IsUnresolvedConfiguredPrimitive(BindingType type)
    {
        return type.PrimitiveKind == BindingPrimitiveKind.Unsupported &&
            string.IsNullOrWhiteSpace(type.MarshallerTypeName) &&
            string.Equals(type.Marshaller.PublicTypeName, type.CppTypeName, StringComparison.Ordinal);
    }

    private static bool ShouldPruneHandleAliasTarget(RecordBinding record)
    {
        if (!string.IsNullOrWhiteSpace(record.ManagedTypeFormat) ||
            !string.IsNullOrWhiteSpace(record.HandleMarshallerTypeName))
        {
            return false;
        }

        if (record.IsIgnored() && ShouldPruneIgnoredHandleAliasTarget(record))
        {
            return true;
        }

        if (record.GetMetadata<bool>(MetadataKeys.PlaceholderRecord))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(record.ManagedNamespace))
        {
            return false;
        }

        return false;
    }

    private static bool ShouldPruneIgnoredHandleAliasTarget(RecordBinding record)
    {
        if (record.GetMetadata<bool>(MetadataKeys.SuppressEmission))
        {
            return false;
        }

        if (record.IsGeneric)
        {
            if (!string.IsNullOrWhiteSpace(record.ManagedBaseTypeName))
            {
                return false;
            }

            return record.SupportedGenericInstantiations.Count == 0 &&
                !record.HasMetadata(MetadataKeys.ExplicitGenericInstantiations);
        }

        return string.IsNullOrWhiteSpace(record.HandleMarshallerTypeName);
    }
}
