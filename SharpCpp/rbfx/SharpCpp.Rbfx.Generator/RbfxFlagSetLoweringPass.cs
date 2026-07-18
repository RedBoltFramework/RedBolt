using SharpCpp.Model.Ast;

namespace SharpCpp.Rbfx.Generator;

public static class RbfxFlagSetLoweringPass
{
    public static void Apply(BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        for (var i = 0; i < compilation.TypeAliases.Count; i++)
        {
            var alias = compilation.TypeAliases[i];
            if (TryLowerFlagSetType(compilation, alias.TargetType, out var enumType))
            {
                compilation.TypeAliases[i] = alias with { TargetType = enumType };
            }
        }

        foreach (var function in compilation.Functions)
        {
            var loweredFunction = false;
            if (TryLowerFlagSetType(compilation, function.ReturnType, out var returnType))
            {
                function.ReturnType = returnType;
                loweredFunction = true;
            }

            foreach (var parameter in function.Parameters)
            {
                if (TryLowerFlagSetType(compilation, parameter.Type, out var parameterType))
                {
                    parameter.Type = parameterType;
                    loweredFunction = true;
                }
            }

            if (loweredFunction && CanRescueLoweredConstructor(function))
            {
                function.ClearIgnoreReason();
            }
        }

        foreach (var field in compilation.Records.SelectMany(static record => record.Members.OfType<FieldBinding>()))
        {
            if (TryLowerFlagSetType(compilation, field.Type, out var fieldType))
            {
                field.Type = fieldType;
                if (!field.IsSilentlyExcluded() &&
                    field.Parent?.IsSilentlyExcluded() != true &&
                    !ContainsOpenGenericParameter(field.Type))
                {
                    field.ClearIgnoreReason();
                }
            }
        }

        foreach (var record in compilation.Records.Where(static record => string.Equals(record.NativeName, "FlagSet", StringComparison.Ordinal)))
        {
            record.SetMetadata(MetadataKeys.SuppressEmission, true);
            record.SetIgnoreReason("rbfx FlagSet<TEnum> is lowered to the enum managed surface", overwriteExisting: true);
        }

        for (var i = compilation.TypeAliases.Count - 1; i >= 0; i--)
        {
            if (TargetsUnavailableIgnoredRecord(compilation, compilation.TypeAliases[i].TargetType))
            {
                compilation.TypeAliases.RemoveAt(i);
            }
        }

        compilation.InvalidateCaches();
    }

    private static bool TryLowerFlagSetType(BindingCompilation compilation, BindingType type, out BindingType enumType)
    {
        enumType = BindingType.Void;
        if (!IsFlagSetType(compilation, type) || type.GenericArguments.Count != 1)
        {
            return false;
        }

        var enumArgument = type.GenericArguments[0];
        if (enumArgument.Kind != BindingTypeKind.Enum || !TryFindEnum(compilation, enumArgument, out var enumBinding))
        {
            return false;
        }

        enumBinding.IsFlags = true;
        enumType = BindingType.CreateEnum(enumArgument.CppTypeName, GetQualifiedEnumManagedName(compilation, enumBinding), enumId: enumBinding.SymbolId);
        return true;
    }

    private static bool IsFlagSetType(BindingCompilation compilation, BindingType type)
    {
        if (type.Kind != BindingTypeKind.Handle || string.IsNullOrWhiteSpace(type.RecordId))
        {
            return false;
        }

        return compilation.Records.Any(record =>
            string.Equals(record.SymbolId, type.RecordId, StringComparison.Ordinal) &&
            string.Equals(record.NativeName, "FlagSet", StringComparison.Ordinal));
    }

    private static bool TryFindEnum(BindingCompilation compilation, BindingType enumType, out EnumBinding enumBinding)
    {
        enumBinding = compilation.FindEnumBySymbolId(enumType.EnumId)!;
        return enumBinding is not null;
    }

    private static bool CanRescueLoweredConstructor(FunctionBinding function)
    {
        if (!function.IsConstructor ||
            function.IsSilentlyExcluded() ||
            function.Parent?.IsSilentlyExcluded() == true ||
            ContainsOpenGenericParameter(function.ReturnType) ||
            function.Parameters.Any(static parameter => ContainsOpenGenericParameter(parameter.Type)))
        {
            return false;
        }

        return function.Parent is not RecordBinding record || !record.IsEffectivelyAbstract;
    }

    private static bool ContainsOpenGenericParameter(BindingType type)
    {
        if (type.Kind == BindingTypeKind.GenericParameter)
        {
            return true;
        }

        return type.GenericArguments.Any(ContainsOpenGenericParameter);
    }

    private static string GetQualifiedEnumManagedName(BindingCompilation compilation, EnumBinding enumBinding)
    {
        return $"global::{enumBinding.ManagedNamespace ?? compilation.ManagedNamespace}.{enumBinding.ManagedName}";
    }

    private static bool TargetsUnavailableIgnoredRecord(BindingCompilation compilation, BindingType type)
    {
        if (type.Kind == BindingTypeKind.Handle && !string.IsNullOrWhiteSpace(type.RecordId))
        {
            var records = compilation.Records
                .Where(record => string.Equals(record.SymbolId, type.RecordId, StringComparison.Ordinal))
                .ToArray();
            if (records.Length == 0 || records.All(IsUnavailableIgnoredRecord))
            {
                return true;
            }
        }

        return type.GenericArguments.Any(argument => TargetsUnavailableIgnoredRecord(compilation, argument));
    }

    private static bool IsUnavailableIgnoredRecord(RecordBinding record)
    {
        if (!record.IsIgnored() || record.GetMetadata<bool>(MetadataKeys.SuppressEmission))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) ||
            !string.IsNullOrWhiteSpace(record.HandleMarshallerTypeName))
        {
            return false;
        }

        return true;
    }
}
