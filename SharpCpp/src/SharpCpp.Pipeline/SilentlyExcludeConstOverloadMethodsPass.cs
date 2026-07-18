using SharpCpp.Model.Ast;

namespace SharpCpp.Pipeline;

public static class SilentlyExcludeConstOverloadMethodsPass
{
    public static void Apply(BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        foreach (var record in compilation.Records)
        {
            foreach (var duplicateGroup in record.Members.OfType<FunctionBinding>()
                         .Where(IsCandidate)
                         .GroupBy(GetManagedPublicCallableSignatureKey, StringComparer.Ordinal)
                         .Where(static group => group.Any(static function => !function.IsConstMethod) &&
                             group.Any(static function => function.IsConstMethod)))
            {
                var hasActiveNonConstMethod = duplicateGroup.Any(static function => !function.IsConstMethod && !function.IsIgnored());
                foreach (var constMethod in duplicateGroup.Where(static function => function.IsConstMethod))
                {
                    if (!hasActiveNonConstMethod && !HasManagedCallableDuplicateReason(constMethod))
                    {
                        continue;
                    }

                    constMethod.SetSilentExclusionReason($"const overload '{constMethod.CppQualifiedName}' duplicates a non-const managed callable signature", overwriteExisting: true);
                }
            }
        }
    }

    private static bool IsCandidate(FunctionBinding function)
    {
        if (!function.IsConstMethod)
        {
            return true;
        }

        if (!function.IsIgnored())
        {
            return true;
        }

        return HasManagedCallableDuplicateReason(function);
    }

    private static bool HasManagedCallableDuplicateReason(FunctionBinding function)
    {
        var reason = function.GetIgnoreReason();
        return reason?.Contains("managed callable signature", StringComparison.Ordinal) == true &&
            reason.Contains("declared multiple times", StringComparison.Ordinal);
    }

    private static string GetManagedPublicCallableSignatureKey(FunctionBinding function)
    {
        return string.Join(
            "|",
            function.ManagedName,
            function.NativeName,
            function.IsStatic,
            function.IsConstructor,
            GetGenericParameterSignature(function),
            function.CppReturnTypeName,
            function.ReturnType.Kind,
            function.ReturnType.UnaliasedManagedTypeName,
            string.Join(
                ";",
                function.Parameters.Select(static parameter =>
                    string.Join(
                        ":",
                        parameter.Direction,
                        parameter.AbiKind,
                        parameter.CppTypeName,
                        parameter.Type.Kind,
                        parameter.Type.UnaliasedManagedTypeName))));
    }

    private static string GetGenericParameterSignature(FunctionBinding function)
    {
        return string.Join(
            ",",
            function.GenericParameters.Select(static parameter => $"{parameter.Position}:{parameter.Name}"));
    }
}
