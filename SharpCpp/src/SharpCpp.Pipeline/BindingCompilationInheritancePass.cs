using SharpCpp.Model.Ast;

namespace SharpCpp.Pipeline;

public static class BindingCompilationInheritancePass
{
    private static int s_projectedMethodCounter;

    public static void Apply(BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        foreach (var record in compilation.Records.ToArray())
        {
            if (record.IsIgnored())
            {
                continue;
            }

            ProjectSecondaryBaseMethods(compilation, record);
        }
    }

    private static void ProjectSecondaryBaseMethods(BindingCompilation compilation, RecordBinding record)
    {
        var primaryBase = record.Bases.FirstOrDefault(static candidate => !candidate.IsVirtual);
        foreach (var secondaryBaseBinding in record.Bases.Where(baseBinding => !baseBinding.IsVirtual && !ReferenceEquals(baseBinding, primaryBase)))
        {
            var secondaryBase = compilation.FindRecordBySymbolId(secondaryBaseBinding.RecordId);
            if (secondaryBase is null || secondaryBase.IsIgnored())
            {
                continue;
            }

            foreach (var method in EnumerateProjectableMethods(compilation, secondaryBase))
            {
                if (method.IsIgnored())
                {
                    continue;
                }

                if (record.Members.OfType<FunctionBinding>().Any(existing => RecordBindingExtensions.SignaturesMatch(existing, method)))
                {
                    continue;
                }

                record.Members.Add(CloneProjectedMethod(compilation, record, secondaryBase, secondaryBaseBinding.CppQualifiedName, method));
            }
        }
    }

    private static IEnumerable<FunctionBinding> EnumerateProjectableMethods(BindingCompilation compilation, RecordBinding record)
    {
        foreach (var baseRecord in record.EnumerateAllBaseRecords(compilation).Reverse().Append(record))
        {
            if (baseRecord.IsIgnored())
            {
                continue;
            }

            foreach (var method in baseRecord.Members.OfType<FunctionBinding>())
            {
                if (method.IsConstructor || method.IsStatic || method.OperatorToken is not null || method.IsConversionOperator)
                {
                    continue;
                }

                yield return method;
            }
        }
    }

    private static FunctionBinding CloneProjectedMethod(
        BindingCompilation compilation,
        RecordBinding targetRecord,
        RecordBinding dispatchRecord,
        string dispatchRecordCppTypeName,
        FunctionBinding sourceMethod)
    {
        var entrypointName = $"{compilation.EntryPointPrefix}_{targetRecord.NativeName}_{dispatchRecord.NativeName}_{sourceMethod.NativeName}_projected_{++s_projectedMethodCounter}";
        var projectedMethod = new FunctionBinding(
            $"projected-secondary:{targetRecord.SymbolId}:{dispatchRecord.SymbolId}:{sourceMethod.SymbolId}",
            sourceMethod.NativeName,
            sourceMethod.ManagedName,
            sourceMethod.ReturnType,
            entryPointName: entrypointName,
            cppReturnTypeName: sourceMethod.CppReturnTypeName,
            cppQualifiedName: $"{dispatchRecord.CppQualifiedName}::{sourceMethod.NativeName}")
        {
            ImportMethodName = sourceMethod.ImportMethodName,
            IsStatic = false,
            IsVirtual = sourceMethod.IsVirtual,
            IsPureVirtual = sourceMethod.IsPureVirtual,
            IsConstMethod = sourceMethod.IsConstMethod,
            NativeDispatchRecordCppTypeName = dispatchRecordCppTypeName,
            OwningRecordCppTypeName = targetRecord.CppQualifiedName,
            IsConversionOperator = sourceMethod.IsConversionOperator,
            IsExplicitConversionOperator = sourceMethod.IsExplicitConversionOperator,
            OperatorToken = sourceMethod.OperatorToken,
            ReturnsOwnedHandle = sourceMethod.ReturnsOwnedHandle
        };

        if (sourceMethod.TryGetMetadata(MetadataKeys.DocumentationComment, out string? documentation) && !string.IsNullOrWhiteSpace(documentation))
        {
            projectedMethod.SetMetadata(MetadataKeys.DocumentationComment, documentation);
        }

        foreach (var parameter in sourceMethod.Parameters)
        {
            projectedMethod.Parameters.Add(new ParameterBinding(
                $"{projectedMethod.SymbolId}:{parameter.ManagedName}",
                parameter.NativeName,
                parameter.ManagedName,
                parameter.Type,
                parameter.Direction,
                parameter.AbiKind,
                parameter.CppTypeName)
            {
                ManagedDefaultValueExpression = parameter.ManagedDefaultValueExpression,
                UseCallbackDelegate = parameter.UseCallbackDelegate,
                CallbackDelegateTypeName = parameter.CallbackDelegateTypeName
            });
        }

        return projectedMethod;
    }
}
