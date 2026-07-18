using SharpCpp.Model.Ast;

namespace SharpCpp.Metadata.Export;

public static class BindingMetadataExporter
{
    private static ManagedBindingProjection? GetManagedProjection(BindingCompilation compilation)
    {
        return compilation.GetMetadata<ManagedBindingProjection>(MetadataKeys.ManagedBindingProjection);
    }

    private static string? GetManagedNamespace(BindingCompilation compilation, RecordBinding record)
    {
        return GetManagedProjection(compilation)?.GetManagedNamespace(record, compilation) ?? record.ManagedNamespace;
    }

    private static string? GetManagedNamespace(BindingCompilation compilation, EnumBinding enumBinding)
    {
        return GetManagedProjection(compilation)?.GetManagedNamespace(enumBinding, compilation) ?? enumBinding.ManagedNamespace;
    }

    private static string? GetManagedNamespace(BindingCompilation compilation, BindingTypeAlias alias)
    {
        return alias.ManagedNamespace;
    }

    public static BindingMetadataDocument Export(BindingCompilation compilation, BindingMetadataExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        options ??= new BindingMetadataExportOptions(compilation.ProjectName);

        var types = compilation.Records
            .Where(static record => !record.IsIgnored() && !record.GetMetadata<bool>(MetadataKeys.SuppressEmission))
            .OrderBy(static record => record.ManagedName, StringComparer.Ordinal)
            .ThenBy(static record => record.SymbolId, StringComparer.Ordinal)
            .Select(record => CreateRecordType(compilation, record))
            .Concat(
                compilation.Enums
                    .Where(static @enum => !@enum.IsIgnored() && !@enum.GetMetadata<bool>(MetadataKeys.SuppressEmission))
                    .OrderBy(static @enum => @enum.ManagedName, StringComparer.Ordinal)
                    .ThenBy(static @enum => @enum.SymbolId, StringComparer.Ordinal)
                    .Select(@enum => CreateEnumType(compilation, @enum)))
            .ToArray();

        var functions = compilation.Functions
            .Where(static function => function.Parent is not RecordBinding && !function.IsIgnored())
            .OrderBy(static function => function.ManagedName, StringComparer.Ordinal)
            .ThenBy(static function => function.SymbolId, StringComparer.Ordinal)
            .Select(function => CreateFunction(compilation, function))
            .ToArray();

        var aliases = compilation.TypeAliases
            .OrderBy(static alias => alias.ManagedName, StringComparer.Ordinal)
            .ThenBy(static alias => alias.CppQualifiedName, StringComparer.Ordinal)
            .Select(alias => new BindingMetadataAlias(
                alias.CppQualifiedName,
                alias.NativeName,
                alias.ManagedName,
                GetManagedNamespace(compilation, alias),
                CreateTypeReference(compilation, alias.TargetType, fullyQualifyConcrete: true)))
            .ToArray();

        var imports = (options.Imports ?? Array.Empty<BindingMetadataImport>())
            .OrderBy(static importItem => importItem.Package, StringComparer.Ordinal)
            .ThenBy(static importItem => importItem.Hash, StringComparer.Ordinal)
            .ToArray();

        var fingerprint = new BindingMetadataFingerprint(
            options.Phase2ClosureHash,
            options.ClangArgsHash,
            options.AttributeVocabularyHash,
            compilation.HeaderFiles.OrderBy(static header => header, StringComparer.Ordinal).ToArray());

        return new BindingMetadataDocument(
            BindingMetadataSchema.CurrentVersion,
            options.GeneratorVersion,
            options.SupportRuntimeVersion,
            new BindingMetadataSource(options.PackageId, compilation.ManagedNamespace, compilation.NativeLibraryName),
            imports,
            fingerprint,
            types,
            functions,
            aliases);
    }

    private static BindingMetadataType CreateRecordType(BindingCompilation compilation, RecordBinding record)
    {
        var kind = record.IsHandleType ? MetadataTypeKind.Handle : MetadataTypeKind.Record;
        return new BindingMetadataType(
            record.SymbolId,
            kind,
            record.NativeName,
            record.ManagedName,
            GetManagedNamespace(compilation, record),
            record.ManagedBaseTypeName,
            CreateTypeReference(record.HandleType),
            ManagedBaseTypeConstraints: record.ManagedBaseTypeConstraints,
            ManagedInstanceCacheFactoryExpression: record.ManagedInstanceCacheFactoryExpression,
            ManagedInstanceCacheTypeName: record.ManagedInstanceCacheTypeName,
            EmitDefaultNativeReleaseOverride: record.EmitDefaultNativeReleaseOverride,
            IsFixedArray: record.GetMetadata<bool>(MetadataKeys.FixedArrayRecord),
            IsDynamicArray: record.GetMetadata<bool>(MetadataKeys.DynamicArrayRecord),
            Members: record.Members
                .Where(static member => !member.IsIgnored())
                .Select(member => CreateRecordMember(compilation, member))
                .OrderBy(static member => member.ManagedName, StringComparer.Ordinal)
                .ToArray());
    }

    private static BindingMetadataType CreateEnumType(BindingCompilation compilation, EnumBinding @enum)
    {
        return new BindingMetadataType(
            @enum.SymbolId,
            MetadataTypeKind.Enum,
            @enum.NativeName,
            @enum.ManagedName,
            GetManagedNamespace(compilation, @enum),
            ManagedBaseTypeName: null,
            CreateTypeReference(BindingType.CreateEnum(@enum.NativeName, @enum.ManagedName, enumId: @enum.SymbolId)),
            IsFlags: @enum.IsFlags,
            Values: @enum.Values
                .OrderBy(static value => value.ManagedName, StringComparer.Ordinal)
                .Select(static value => new BindingMetadataEnumValue(value.NativeName, value.ManagedName, value.ValueExpression))
                .ToArray());
    }

    private static BindingMetadataFunction CreateFunction(BindingCompilation compilation, FunctionBinding function)
    {
        var parameters = function.Parameters
            .OrderBy(static parameter => parameter.ManagedName, StringComparer.Ordinal)
            .ThenBy(static parameter => parameter.SymbolId, StringComparer.Ordinal)
            .Select(parameter => new BindingMetadataParameter(
                parameter.ManagedName,
                parameter.Direction.ToString().ToLowerInvariant(),
                CreateTypeReference(compilation, parameter.Type, fullyQualifyConcrete: true),
                parameter.ManagedDefaultValueExpression,
                parameter.UseCallbackDelegate,
                parameter.CallbackDelegateTypeName))
            .ToArray();

        return new BindingMetadataFunction(
            function.SymbolId,
            function.NativeName,
            function.ManagedName,
            function.EntryPointName,
            CreateTypeReference(compilation, function.ReturnType, fullyQualifyConcrete: true),
            parameters,
            function.ReturnsOwnedHandle);
    }

    private static BindingMetadataRecordMember CreateRecordMember(BindingCompilation compilation, BindingNode member)
    {
        return member switch
        {
            FunctionBinding function => new BindingMetadataRecordMember(
                function.IsConstructor ? "constructor" : "method",
                function.NativeName,
                function.ManagedName,
                function.IsConstructor ? null : CreateTypeReference(compilation, function.ReturnType, fullyQualifyConcrete: true),
                function.Parameters
                    .OrderBy(static parameter => parameter.ManagedName, StringComparer.Ordinal)
                    .ThenBy(static parameter => parameter.SymbolId, StringComparer.Ordinal)
                    .Select(parameter => new BindingMetadataParameter(
                        parameter.ManagedName,
                        parameter.Direction.ToString().ToLowerInvariant(),
                        CreateTypeReference(compilation, parameter.Type, fullyQualifyConcrete: true),
                        parameter.ManagedDefaultValueExpression,
                        parameter.UseCallbackDelegate,
                        parameter.CallbackDelegateTypeName))
                    .ToArray(),
                IsReadOnly: false,
                function.IsStatic,
                function.IsConstructor,
                function.OperatorToken,
                function.IsConversionOperator,
                function.IsExplicitConversionOperator,
                function.ReturnsOwnedHandle,
                GetterReturnsOwnedHandle: false),
            FieldBinding field => new BindingMetadataRecordMember(
                "field",
                field.NativeName,
                field.ManagedName,
                CreateTypeReference(compilation, field.Type, fullyQualifyConcrete: true),
                Parameters: null,
                field.IsReadOnly,
                IsStatic: false,
                IsConstructor: false,
                GetterReturnsOwnedHandle: field.ReturnsOwnedHandle),
            _ => throw new NotSupportedException($"Metadata export does not support member kind '{member.Kind}'.")
        };
    }

    private static BindingMetadataTypeReference CreateTypeReference(BindingType type)
    {
        return CreateTypeReference(null, type, fullyQualifyConcrete: false);
    }

    private static BindingMetadataTypeReference CreateTypeReference(BindingCompilation? compilation, BindingType type, bool fullyQualifyConcrete)
    {
        return new BindingMetadataTypeReference(
            type.Kind.ToString().ToLowerInvariant(),
            type.CppTypeName,
            fullyQualifyConcrete && compilation is not null
                ? FormatQualifiedConcreteManagedTypeName(compilation, type)
                : type.ManagedTypeName,
            type.Kind == BindingTypeKind.Primitive ? type.PrimitiveKind.ToString().ToLowerInvariant() : null,
            type.RecordId,
            type.MarshallerTypeName,
            type.GenericArguments.Count == 0
                ? null
                : type.GenericArguments.Select(argument => CreateTypeReference(compilation, argument, fullyQualifyConcrete)).ToArray(),
            type.EnumId,
            type.ManagedMarshaller,
            type.IsCallback,
            type.CallbackReturnType is null
                ? null
                : CreateTypeReference(compilation, type.CallbackReturnType, fullyQualifyConcrete),
            type.CallbackParameterTypes.Count == 0
                ? null
                : type.CallbackParameterTypes.Select(argument => CreateTypeReference(compilation, argument, fullyQualifyConcrete)).ToArray(),
            type.FixedArrayElementType is null
                ? null
                : CreateTypeReference(compilation, type.FixedArrayElementType, fullyQualifyConcrete),
            type.FixedArrayLength,
            type.DynamicArrayElementType is null
                ? null
                : CreateTypeReference(compilation, type.DynamicArrayElementType, fullyQualifyConcrete));
    }

    private static string FormatQualifiedConcreteManagedTypeName(BindingCompilation compilation, BindingType type)
    {
        return type.Kind switch
        {
            BindingTypeKind.GenericParameter => type.GenericParameterName ?? "T",
            BindingTypeKind.Primitive when type.PrimitiveKind != BindingPrimitiveKind.Unsupported => type.UnaliasedManagedTypeName,
            BindingTypeKind.Primitive => FormatConfiguredManagedTypeName(compilation, type),
            BindingTypeKind.Handle => FormatQualifiedRecordTypeName(compilation, type),
            BindingTypeKind.Enum => FormatQualifiedEnumTypeName(compilation, type),
            _ => type.UnaliasedManagedTypeName
        };
    }

    private static string FormatQualifiedRecordTypeName(BindingCompilation compilation, BindingType type)
    {
        var record = compilation.FindRecordBySymbolId(type.RecordId);
        if (record is null)
        {
            return FormatConfiguredManagedTypeName(compilation, type);
        }

        var managedName = record.ManagedTypeFormat ?? record.ManagedName;
        var head = GetManagedTypeHead(managedName);
        var qualifiedHead = head.StartsWith("global::", StringComparison.Ordinal)
            ? head
            : head.Contains('.', StringComparison.Ordinal)
                ? $"global::{head}"
                : $"global::{GetManagedNamespace(compilation, record) ?? compilation.ManagedNamespace}.{head}";
        if (type.GenericArguments.Count == 0)
        {
            return qualifiedHead;
        }

        var visibleArguments = SelectManagedVisibleGenericArguments(managedName, type.GenericArguments);
        return $"{qualifiedHead}<{string.Join(", ", visibleArguments.Select(argument => FormatQualifiedConcreteManagedTypeName(compilation, argument)))}>";
    }

    private static IReadOnlyList<BindingType> SelectManagedVisibleGenericArguments(string managedTypeFormat, IReadOnlyList<BindingType> genericArguments)
    {
        if (genericArguments.Count == 0 || !managedTypeFormat.Contains('{', StringComparison.Ordinal))
        {
            return genericArguments;
        }

        var indices = new SortedSet<int>();
        for (var index = 0; index < managedTypeFormat.Length; index++)
        {
            if (managedTypeFormat[index] != '{')
            {
                continue;
            }

            var closeIndex = managedTypeFormat.IndexOf('}', index + 1);
            if (closeIndex <= index + 1)
            {
                continue;
            }

            if (int.TryParse(managedTypeFormat[(index + 1)..closeIndex], out var argumentIndex) &&
                argumentIndex >= 0 &&
                argumentIndex < genericArguments.Count)
            {
                indices.Add(argumentIndex);
            }

            index = closeIndex;
        }

        return indices.Count == 0
            ? genericArguments
            : indices.Select(index => genericArguments[index]).ToArray();
    }

    private static string FormatQualifiedEnumTypeName(BindingCompilation compilation, BindingType type)
    {
        var enumBinding = compilation.FindEnumBySymbolId(type.EnumId);
        if (enumBinding is null)
        {
            return QualifyManagedTypeText(type.UnaliasedManagedTypeName);
        }

        var managedName = enumBinding.ManagedName;
        if (managedName.StartsWith("global::", StringComparison.Ordinal))
        {
            return managedName;
        }

        if (managedName.Contains('.', StringComparison.Ordinal))
        {
            return $"global::{managedName}";
        }

        return $"global::{GetManagedNamespace(compilation, enumBinding) ?? compilation.ManagedNamespace}.{managedName}";
    }

    private static string FormatConfiguredManagedTypeName(BindingCompilation compilation, BindingType type)
    {
        if (type.GenericArguments.Count == 0)
        {
            return QualifyManagedTypeText(type.UnaliasedManagedTypeName);
        }

        var head = QualifyManagedTypeText(GetManagedTypeHead(type.UnaliasedManagedTypeName));
        var arguments = string.Join(", ", type.GenericArguments.Select(argument => FormatQualifiedConcreteManagedTypeName(compilation, argument)));
        return $"{head}<{arguments}>";
    }

    private static string GetManagedTypeHead(string managedTypeName)
    {
        var genericStart = managedTypeName.IndexOf('<');
        return genericStart >= 0 ? managedTypeName[..genericStart] : managedTypeName;
    }

    private static string QualifyManagedTypeText(string managedTypeName)
    {
        if (string.IsNullOrWhiteSpace(managedTypeName) ||
            managedTypeName.StartsWith("global::", StringComparison.Ordinal) ||
            IsManagedKeywordTypeName(managedTypeName))
        {
            return managedTypeName;
        }

        return managedTypeName.Contains('.', StringComparison.Ordinal)
            ? $"global::{managedTypeName}"
            : managedTypeName;
    }

    private static bool IsManagedKeywordTypeName(string managedTypeName) => managedTypeName switch
    {
        "void" or "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or
        "float" or "double" or "nint" or "nuint" or "char" or "string" or "object" or "IntPtr" or "UIntPtr" => true,
        _ => false
    };
}
