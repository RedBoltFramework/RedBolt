using SharpCpp.Model.Ast;

namespace SharpCpp.Pipeline;

public static class BindingCompilationGenericInstantiationPass
{
    public static void Apply(BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        foreach (var record in compilation.Records)
        {
            record.SupportedGenericInstantiations.Clear();

            if (record.IsIgnored())
            {
                continue;
            }

            if (record.TryGetMetadata(MetadataKeys.ExplicitGenericInstantiations, out GenericInstantiationMetadata? explicitInstantiations))
            {
                foreach (var instantiation in explicitInstantiations!.Instantiations)
                {
                    record.SupportedGenericInstantiations.Add(instantiation);
                }
            }
        }

        foreach (var function in compilation.Functions)
        {
            function.SupportedGenericInstantiations.Clear();

            if (function.IsIgnored())
            {
                continue;
            }

            if (function.TryGetMetadata(MetadataKeys.ExplicitGenericInstantiations, out GenericInstantiationMetadata? explicitInstantiations))
            {
                foreach (var instantiation in explicitInstantiations!.Instantiations)
                {
                    function.SupportedGenericInstantiations.Add(instantiation);
                }
            }
        }

        NormalizeArrayTypeReferences(compilation);

        foreach (var function in compilation.Functions)
        {
            if (function.IsIgnored() || function.IsGeneric || (function.Parent as RecordBinding)?.IsGeneric == true)
            {
                continue;
            }

            CollectRecordInstantiations(compilation, function.ReturnType);
            foreach (var parameter in function.Parameters)
            {
                CollectRecordInstantiations(compilation, parameter.Type);
            }
        }

        foreach (var field in compilation.Descendants<FieldBinding>())
        {
            if (field.IsIgnored())
            {
                continue;
            }

            CollectRecordInstantiations(compilation, field.Type);
        }

        foreach (var alias in compilation.TypeAliases)
        {
            CollectRecordInstantiations(compilation, alias.TargetType);
        }
    }

    private static void NormalizeArrayTypeReferences(BindingCompilation compilation)
    {
        var recordsById = compilation.Records
            .GroupBy(static record => record.SymbolId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Last(), StringComparer.Ordinal);

        foreach (var function in compilation.Functions)
        {
            function.ReturnType = NormalizeArrayType(function.ReturnType, recordsById);
            foreach (var parameter in function.Parameters)
            {
                parameter.Type = NormalizeArrayType(parameter.Type, recordsById);
            }
        }

        foreach (var field in compilation.Descendants<FieldBinding>())
        {
            field.Type = NormalizeArrayType(field.Type, recordsById);
        }

        for (var i = 0; i < compilation.TypeAliases.Count; i++)
        {
            var alias = compilation.TypeAliases[i];
            compilation.TypeAliases[i] = alias with { TargetType = NormalizeArrayType(alias.TargetType, recordsById) };
        }
    }

    private static BindingType NormalizeArrayType(BindingType type, IReadOnlyDictionary<string, RecordBinding> recordsById)
    {
        if (type.IsCallback)
        {
            var returnType = type.CallbackReturnType is null
                ? null
                : NormalizeArrayType(type.CallbackReturnType, recordsById);
            var parameterTypes = type.CallbackParameterTypes.Select(parameterType => NormalizeArrayType(parameterType, recordsById)).ToArray();
            return returnType is null ? type : type.WithCallbackSignature(returnType, parameterTypes);
        }

        var genericArguments = type.GenericArguments.Select(argument => NormalizeArrayType(argument, recordsById)).ToArray();
        var result = genericArguments.SequenceEqual(type.GenericArguments)
            ? type
            : type.WithGenericArguments(genericArguments);

        if (!result.IsDynamicArray &&
            result.Kind == BindingTypeKind.Handle &&
            !string.IsNullOrWhiteSpace(result.RecordId) &&
            recordsById.TryGetValue(result.RecordId, out var dynamicRecord) &&
            dynamicRecord.GetMetadata<bool>(MetadataKeys.DynamicArrayRecord) &&
            result.GenericArguments.Count > 0)
        {
            result = result.WithDynamicArray(result.GenericArguments[0]);
        }

        if (result.IsFixedArray ||
            result.Kind != BindingTypeKind.Handle ||
            string.IsNullOrWhiteSpace(result.RecordId) ||
            !recordsById.TryGetValue(result.RecordId, out var record) ||
            !record.GetMetadata<bool>(MetadataKeys.FixedArrayRecord) ||
            result.GenericArguments.Count == 0 ||
            !TryGetSecondTopLevelTemplateArgument(result.CppTypeName, out var lengthText) ||
            !int.TryParse(lengthText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var length) ||
            length < 0)
        {
            return result;
        }

        var elementType = result.GenericArguments[0];
        var fixedArrayArguments = result.GenericArguments.Count >= 2
            ? result.GenericArguments
            : result.GenericArguments.Concat([BindingType.CreateConfigured(lengthText, lengthText)]).ToArray();
        return result
            .WithGenericArguments(fixedArrayArguments)
            .WithFixedArray(elementType, length);
    }

    private static bool TryGetSecondTopLevelTemplateArgument(string cppTypeName, out string argument)
    {
        argument = string.Empty;
        var genericStart = cppTypeName.IndexOf('<');
        var genericEnd = cppTypeName.LastIndexOf('>');
        if (genericStart < 0 || genericEnd <= genericStart)
        {
            return false;
        }

        var arguments = SplitTopLevelArguments(cppTypeName[(genericStart + 1)..genericEnd]);
        if (arguments.Count < 2)
        {
            return false;
        }

        argument = arguments[1];
        return true;
    }

    private static IReadOnlyList<string> SplitTopLevelArguments(string text)
    {
        var result = new List<string>();
        var segmentStart = 0;
        var angleDepth = 0;
        var parenDepth = 0;

        for (var index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '<':
                    angleDepth++;
                    break;
                case '>':
                    angleDepth--;
                    break;
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    parenDepth--;
                    break;
                case ',' when angleDepth == 0 && parenDepth == 0:
                    result.Add(text[segmentStart..index].Trim());
                    segmentStart = index + 1;
                    break;
            }
        }

        result.Add(text[segmentStart..].Trim());
        return result;
    }

    private static void CollectRecordInstantiations(BindingCompilation compilation, BindingType type)
    {
        if (type.IsCallback)
        {
            if (type.CallbackReturnType is not null)
            {
                CollectRecordInstantiations(compilation, type.CallbackReturnType);
            }

            foreach (var parameterType in type.CallbackParameterTypes)
            {
                CollectRecordInstantiations(compilation, parameterType);
            }

            return;
        }

        foreach (var genericArgument in type.GenericArguments)
        {
            CollectRecordInstantiations(compilation, genericArgument);
        }

        if (type.Kind != BindingTypeKind.Handle ||
            string.IsNullOrWhiteSpace(type.RecordId) ||
            type.GenericArguments.Count == 0 ||
            type.ContainsGenericParameters ||
            ContainsOpenNativeTemplateParameter(type))
        {
            return;
        }

        var record = compilation.Records.FirstOrDefault(candidate => string.Equals(candidate.SymbolId, type.RecordId, StringComparison.Ordinal));
        if (record is null || record.IsIgnored() || !record.IsGeneric)
        {
            return;
        }

        var visibleGenericParameterCount = GetVisibleGenericParameterCount(record);
        if (type.GenericArguments.Count != record.GenericParameters.Count &&
            type.GenericArguments.Count != visibleGenericParameterCount &&
            type.GenericArguments.Count >= record.GenericParameters.Count)
        {
            return;
        }

        if (type.GenericArguments.Any(argument => ContainsRecordGenericParameterReference(record, argument)))
        {
            return;
        }

        if (record.SupportedGenericInstantiations.Any(existing => InstantiationsEqual(existing.TypeArguments, type.GenericArguments)))
        {
            return;
        }

        record.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding(type.GenericArguments));
    }

    private static bool ContainsOpenNativeTemplateParameter(BindingType type)
    {
        if (type.CppTypeName.Contains("type-parameter-", StringComparison.Ordinal) ||
            type.ManagedTypeName.Contains("type-parameter-", StringComparison.Ordinal))
        {
            return true;
        }

        if (type.IsCallback)
        {
            return (type.CallbackReturnType is not null && ContainsOpenNativeTemplateParameter(type.CallbackReturnType)) ||
                type.CallbackParameterTypes.Any(ContainsOpenNativeTemplateParameter);
        }

        return type.GenericArguments.Any(ContainsOpenNativeTemplateParameter);
    }

    private static bool InstantiationsEqual(IReadOnlyList<BindingType> left, IReadOnlyList<BindingType> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].CppTypeName, right[index].CppTypeName, StringComparison.Ordinal) ||
                left[index].Kind != right[index].Kind)
            {
                return false;
            }
        }

        return true;
    }

    private static int GetVisibleGenericParameterCount(RecordBinding record)
    {
        var usedIndices = new HashSet<int>();
        CollectManagedFormatPlaceholderIndices(record.ManagedTypeFormat, usedIndices);
        CollectManagedFormatPlaceholderIndices(record.ManagedBaseTypeName, usedIndices);
        CollectManagedFormatPlaceholderIndices(record.ManagedBaseTypeConstraints, usedIndices);

        return usedIndices.Count == 0
            ? record.GenericParameters.Count
            : usedIndices.Count(index => index >= 0 && index < record.GenericParameters.Count);
    }

    private static bool ContainsRecordGenericParameterReference(RecordBinding record, BindingType type)
    {
        if (type.IsGenericParameter &&
            record.GenericParameters.Any(parameter => string.Equals(parameter.Name, type.GenericParameterName, StringComparison.Ordinal)))
        {
            return true;
        }

        var trimmedTypeName = type.CppTypeName.Trim().TrimEnd('*', '&').Trim();
        if (record.GenericParameters.Any(parameter => string.Equals(parameter.Name, trimmedTypeName, StringComparison.Ordinal)))
        {
            return true;
        }

        return type.GenericArguments.Any(argument => ContainsRecordGenericParameterReference(record, argument));
    }

    private static void CollectManagedFormatPlaceholderIndices(string? format, ISet<int> target)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return;
        }

        for (var index = 0; index < format.Length; index++)
        {
            if (format[index] != '{')
            {
                continue;
            }

            var closeIndex = format.IndexOf('}', index + 1);
            if (closeIndex <= index + 1)
            {
                continue;
            }

            if (int.TryParse(format[(index + 1)..closeIndex], out var placeholderIndex))
            {
                target.Add(placeholderIndex);
            }

            index = closeIndex;
        }
    }
}
