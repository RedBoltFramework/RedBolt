using System.Security.Cryptography;
using System.Text;

using SharpCpp.Metadata.Export;
using SharpCpp.Model.Ast;

namespace SharpCpp.Metadata.Import;

public sealed class BindingMetadataImportCatalog
{
    private readonly IReadOnlyList<BindingMetadataDocument> _documents;

    private BindingMetadataImportCatalog(IReadOnlyList<BindingMetadataDocument> documents, IReadOnlyList<BindingMetadataImport> imports)
    {
        _documents = documents;
        Imports = imports;
    }

    public static BindingMetadataImportCatalog Empty { get; } = new([], []);

    public IReadOnlyList<BindingMetadataImport> Imports { get; }

    public static BindingMetadataImportCatalog Load(IEnumerable<string> metadataFiles)
    {
        ArgumentNullException.ThrowIfNull(metadataFiles);

        var documents = new List<BindingMetadataDocument>();
        var imports = new List<BindingMetadataImport>();

        foreach (var metadataFile in metadataFiles)
        {
            var fullPath = Path.GetFullPath(metadataFile);
            var json = File.ReadAllText(fullPath);
            var document = BindingMetadataSerializer.Deserialize(json);
            if (document.SchemaVersion != BindingMetadataSchema.CurrentVersion)
            {
                throw new InvalidOperationException($"Imported metadata '{fullPath}' uses unsupported schema version '{document.SchemaVersion}'.");
            }

            documents.Add(document);
            imports.Add(new BindingMetadataImport(document.Source.Package, ComputeHash(json)));
        }

        return new BindingMetadataImportCatalog(documents, imports
            .OrderBy(static item => item.Package, StringComparer.Ordinal)
            .ThenBy(static item => item.Hash, StringComparer.Ordinal)
            .ToArray());
    }

    public void Seed(BindingCompilation compilation, IDictionary<string, RecordBinding> recordsByName, IDictionary<string, EnumBinding> enumsByName)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(recordsByName);
        ArgumentNullException.ThrowIfNull(enumsByName);

        foreach (var document in _documents)
        {
            foreach (var importedType in document.Types.OrderBy(static item => item.SymbolId, StringComparer.Ordinal))
            {
                switch (importedType.Kind)
                {
                    case MetadataTypeKind.Handle:
                    case MetadataTypeKind.Record:
                        SeedRecord(compilation, recordsByName, document, importedType);
                        break;

                    case MetadataTypeKind.Enum:
                        SeedEnum(compilation, enumsByName, document, importedType);
                        break;
                }
            }

            foreach (var alias in (document.Aliases ?? Array.Empty<BindingMetadataAlias>())
                .OrderBy(static item => item.ManagedName, StringComparer.Ordinal)
                .ThenBy(static item => item.CppQualifiedName, StringComparer.Ordinal))
            {
                SeedAlias(compilation, recordsByName, enumsByName, document, alias);
            }
        }
    }

    private static void SeedRecord(BindingCompilation compilation, IDictionary<string, RecordBinding> recordsByName, BindingMetadataDocument document, BindingMetadataType importedType)
    {
        var cppQualifiedName = GetQualifiedCppTypeName(importedType.Projection.CppType);
        if (recordsByName.ContainsKey(importedType.NativeName) || recordsByName.ContainsKey(cppQualifiedName))
        {
            return;
        }

        var managedTypeName = GetQualifiedManagedTypeName(importedType.ManagedNamespace ?? document.Source.ManagedNamespace, importedType.ManagedName);
        var managedBaseTypeName = string.IsNullOrWhiteSpace(importedType.ManagedBaseTypeName)
            ? null
            : ResolveQualifiedManagedBaseTypeName(document, importedType.ManagedBaseTypeName!);

        var record = new RecordBinding(
            $"import:{document.Source.Package}:{importedType.SymbolId}",
            importedType.NativeName,
            managedTypeName,
            cppQualifiedName)
        {
            ManagedNamespace = importedType.ManagedNamespace ?? document.Source.ManagedNamespace,
            ManagedBaseTypeName = managedBaseTypeName,
            ManagedTypeFormat = importedType.Projection.GenericArguments is { Count: > 0 }
                ? BuildQualifiedManagedTypeFormat(managedTypeName, importedType.IsFixedArray || importedType.IsDynamicArray ? 1 : importedType.Projection.GenericArguments.Count)
                : null,
            ManagedBaseTypeConstraints = importedType.ManagedBaseTypeConstraints,
            ManagedInstanceCacheFactoryExpression = importedType.ManagedInstanceCacheFactoryExpression,
            ManagedInstanceCacheTypeName = importedType.ManagedInstanceCacheTypeName,
            EmitDefaultNativeReleaseOverride = importedType.EmitDefaultNativeReleaseOverride
        };

        if (importedType.IsFixedArray)
        {
            record.SetMetadata(MetadataKeys.FixedArrayRecord, true);
        }

        if (importedType.IsDynamicArray)
        {
            record.SetMetadata(MetadataKeys.DynamicArrayRecord, true);
        }

        if (importedType.Projection.GenericArguments is { Count: > 0 })
        {
            foreach (var (argument, index) in importedType.Projection.GenericArguments.Select((argument, index) => (argument, index)))
            {
                var name = argument.Kind == "genericparameter" && !string.IsNullOrWhiteSpace(argument.PublicType)
                    ? argument.PublicType
                    : $"T{index}";
                record.GenericParameters.Add(new BindingGenericParameter(name, index));
            }
        }

        record.SetMetadata(MetadataKeys.SuppressEmission, true);

        SeedRecordKeys(recordsByName, record);
        compilation.Members.Add(record);
    }

    private static void SeedEnum(BindingCompilation compilation, IDictionary<string, EnumBinding> enumsByName, BindingMetadataDocument document, BindingMetadataType importedType)
    {
        if (enumsByName.ContainsKey(importedType.NativeName) || enumsByName.ContainsKey(importedType.Projection.CppType))
        {
            return;
        }

        var enumBinding = new EnumBinding(
            $"import:{document.Source.Package}:{importedType.SymbolId}",
            importedType.NativeName,
            GetQualifiedManagedTypeName(importedType.ManagedNamespace ?? document.Source.ManagedNamespace, importedType.ManagedName),
            importedType.Projection.CppType,
            underlyingPrimitiveKind: BindingType.PrimitiveKindFromCppName(importedType.Projection.CppType))
        {
            ManagedNamespace = importedType.ManagedNamespace ?? document.Source.ManagedNamespace,
            IsFlags = importedType.IsFlags
        };
        enumBinding.SetMetadata(MetadataKeys.SuppressEmission, true);

        SeedEnumKeys(enumsByName, enumBinding);
        compilation.Members.Add(enumBinding);
    }

    private static void SeedAlias(
        BindingCompilation compilation,
        IDictionary<string, RecordBinding> recordsByName,
        IDictionary<string, EnumBinding> enumsByName,
        BindingMetadataDocument document,
        BindingMetadataAlias alias)
    {
        if (compilation.TypeAliases.Any(existing =>
                string.Equals(existing.CppQualifiedName, alias.CppQualifiedName, StringComparison.Ordinal) ||
                string.Equals(existing.ManagedName, alias.ManagedName, StringComparison.Ordinal)))
        {
            return;
        }

        var targetType = CreateAliasTargetType(recordsByName, enumsByName, document, alias.TargetType);
        var bindingAlias = new BindingTypeAlias(alias.CppQualifiedName, alias.NativeName, alias.ManagedName, targetType)
        {
            ManagedNamespace = alias.ManagedNamespace ?? document.Source.ManagedNamespace
        };
        compilation.TypeAliases.Add(bindingAlias);
    }

    private static void SeedRecordKeys(IDictionary<string, RecordBinding> recordsByName, RecordBinding record)
    {
        TryAdd(recordsByName, record.NativeName, record);
        TryAdd(recordsByName, record.CppQualifiedName, record);
    }

    private static void SeedEnumKeys(IDictionary<string, EnumBinding> enumsByName, EnumBinding enumBinding)
    {
        TryAdd(enumsByName, enumBinding.NativeName, enumBinding);
        TryAdd(enumsByName, enumBinding.CppQualifiedName, enumBinding);
    }

    private static void TryAdd<T>(IDictionary<string, T> dictionary, string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key) || dictionary.ContainsKey(key))
        {
            return;
        }

        dictionary.Add(key, value);
    }

    private static BindingType CreateAliasTargetType(
        IDictionary<string, RecordBinding> recordsByName,
        IDictionary<string, EnumBinding> enumsByName,
        BindingMetadataDocument document,
        BindingMetadataTypeReference typeReference)
    {
        var genericArguments = typeReference.GenericArguments?
            .Select(argument => CreateAliasTargetType(recordsByName, enumsByName, document, argument))
            .ToArray();

        if (typeReference.IsCallback)
        {
            return CreateCallbackTargetType(recordsByName, enumsByName, document, typeReference);
        }

        var result = typeReference.Kind switch
        {
            MetadataTypeKind.Handle => CreateAliasHandleTargetType(recordsByName, typeReference, genericArguments),
            MetadataTypeKind.Enum => CreateAliasEnumTargetType(enumsByName, document, typeReference, genericArguments),
            _ => BindingType.CreateConfigured(
                typeReference.CppType,
                GetConcreteManagedTypeHead(typeReference),
                typeReference.MarshallerType,
                typeReference.ManagedMarshaller,
                ParsePrimitiveKind(typeReference.Primitive),
                genericArguments)
        };

        if (typeReference.FixedArrayElementType is not null && typeReference.FixedArrayLength is not null)
        {
            result = result.WithFixedArray(
                CreateAliasTargetType(recordsByName, enumsByName, document, typeReference.FixedArrayElementType),
                typeReference.FixedArrayLength.Value);
        }

        if (typeReference.DynamicArrayElementType is not null)
        {
            result = result.WithDynamicArray(
                CreateAliasTargetType(recordsByName, enumsByName, document, typeReference.DynamicArrayElementType));
        }

        return result;
    }

    private static BindingType CreateCallbackTargetType(
        IDictionary<string, RecordBinding> recordsByName,
        IDictionary<string, EnumBinding> enumsByName,
        BindingMetadataDocument document,
        BindingMetadataTypeReference typeReference)
    {
        var returnType = typeReference.CallbackReturnType is null
            ? BindingType.Void
            : CreateAliasTargetType(recordsByName, enumsByName, document, typeReference.CallbackReturnType);
        var parameterTypes = typeReference.CallbackParameterTypes?
            .Select(argument => CreateAliasTargetType(recordsByName, enumsByName, document, argument))
            .ToArray() ?? Array.Empty<BindingType>();

        return BindingType.CreateCallback(
            typeReference.CppType,
            BuildManagedCallbackTypeFormat(returnType, parameterTypes),
            typeReference.ManagedMarshaller,
            returnType,
            parameterTypes);
    }

    private static string BuildManagedCallbackTypeFormat(BindingType returnType, IReadOnlyList<BindingType> parameterTypes)
    {
        if (returnType.PrimitiveKind == BindingPrimitiveKind.Void)
        {
            return parameterTypes.Count == 0
                ? "global::System.Action"
                : $"global::System.Action<{BuildGenericPlaceholders(parameterTypes.Count)}>";
        }

        return $"global::System.Func<{BuildGenericPlaceholders(parameterTypes.Count + 1)}>";
    }

    private static string BuildGenericPlaceholders(int count)
    {
        return string.Join(", ", Enumerable.Range(0, count).Select(static index => $"{{{index}}}"));
    }

    private static BindingType CreateAliasHandleTargetType(
        IDictionary<string, RecordBinding> recordsByName,
        BindingMetadataTypeReference typeReference,
        IReadOnlyList<BindingType>? genericArguments)
    {
        var qualifiedCppTypeName = GetQualifiedCppTypeName(typeReference.CppType);
        RecordBinding? record = null;
        if (!string.IsNullOrWhiteSpace(qualifiedCppTypeName) && recordsByName.TryGetValue(qualifiedCppTypeName, out var byCppName))
        {
            record = byCppName;
        }
        else if (!string.IsNullOrWhiteSpace(typeReference.PublicType))
        {
            record = recordsByName.Values.FirstOrDefault(candidate =>
                string.Equals(candidate.ManagedName, typeReference.PublicType, StringComparison.Ordinal));
        }

        return record is not null
            ? BindingType.CreateHandle(typeReference.CppType, record.ManagedName, record.SymbolId, typeReference.MarshallerType, genericArguments)
            : BindingType.CreateHandle(typeReference.CppType, GetConcreteManagedTypeHead(typeReference), typeReference.RecordId, typeReference.MarshallerType, genericArguments);
    }

    private static BindingType CreateAliasEnumTargetType(
        IDictionary<string, EnumBinding> enumsByName,
        BindingMetadataDocument document,
        BindingMetadataTypeReference typeReference,
        IReadOnlyList<BindingType>? genericArguments)
    {
        if (!string.IsNullOrWhiteSpace(typeReference.EnumId))
        {
            var importedEnumId = $"import:{document.Source.Package}:{typeReference.EnumId}";
            var enumById = enumsByName.Values.FirstOrDefault(candidate => string.Equals(candidate.SymbolId, importedEnumId, StringComparison.Ordinal));
            if (enumById is not null)
            {
                return BindingType.CreateEnum(typeReference.CppType, enumById.ManagedName, genericArguments, enumById.SymbolId);
            }
        }

        var qualifiedCppTypeName = GetQualifiedCppTypeName(typeReference.CppType);
        if (!string.IsNullOrWhiteSpace(qualifiedCppTypeName) && enumsByName.TryGetValue(qualifiedCppTypeName, out var enumBinding))
        {
            return BindingType.CreateEnum(typeReference.CppType, enumBinding.ManagedName, genericArguments, enumBinding.SymbolId);
        }

        return BindingType.CreateEnum(typeReference.CppType, typeReference.PublicType, genericArguments, typeReference.EnumId);
    }

    private static BindingPrimitiveKind ParsePrimitiveKind(string? primitive)
    {
        return primitive switch
        {
            "void" => BindingPrimitiveKind.Void,
            "boolean" => BindingPrimitiveKind.Boolean,
            "int8" => BindingPrimitiveKind.Int8,
            "uint8" => BindingPrimitiveKind.UInt8,
            "int16" => BindingPrimitiveKind.Int16,
            "uint16" => BindingPrimitiveKind.UInt16,
            "int32" => BindingPrimitiveKind.Int32,
            "uint32" => BindingPrimitiveKind.UInt32,
            "int64" => BindingPrimitiveKind.Int64,
            "uint64" => BindingPrimitiveKind.UInt64,
            "single" => BindingPrimitiveKind.Single,
            "double" => BindingPrimitiveKind.Double,
            "nativeint" => BindingPrimitiveKind.NativeInt,
            "nativeuint" => BindingPrimitiveKind.NativeUInt,
            "intptr" => BindingPrimitiveKind.IntPtr,
            _ => BindingPrimitiveKind.Unsupported
        };
    }

    private static string BuildQualifiedManagedTypeFormat(string managedTypeName, int genericArity)
    {
        if (genericArity <= 0)
        {
            return managedTypeName;
        }

        var placeholders = string.Join(", ", Enumerable.Range(0, genericArity).Select(static index => $"{{{index}}}"));
        return $"{managedTypeName}<{placeholders}>";
    }

    private static string GetConcreteManagedTypeHead(BindingMetadataTypeReference typeReference)
    {
        if (typeReference.GenericArguments is not { Count: > 0 })
        {
            return typeReference.PublicType;
        }

        var genericStart = typeReference.PublicType.IndexOf('<');
        return genericStart >= 0 ? typeReference.PublicType[..genericStart] : typeReference.PublicType;
    }

    private static string ResolveQualifiedManagedBaseTypeName(BindingMetadataDocument document, string managedBaseTypeName)
    {
        if (managedBaseTypeName.StartsWith("global::", StringComparison.Ordinal))
        {
            return managedBaseTypeName;
        }

        var baseType = document.Types.FirstOrDefault(candidate => string.Equals(candidate.ManagedName, managedBaseTypeName, StringComparison.Ordinal));
        if (baseType is not null)
        {
            return GetQualifiedManagedTypeName(baseType.ManagedNamespace ?? document.Source.ManagedNamespace, baseType.ManagedName);
        }

        return GetQualifiedManagedTypeName(document.Source.ManagedNamespace, managedBaseTypeName);
    }

    private static string GetQualifiedManagedTypeName(string? managedNamespace, string managedName)
    {
        if (managedName.StartsWith("global::", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(managedNamespace))
        {
            return managedName;
        }

        return $"global::{managedNamespace}.{managedName}";
    }

    private static string GetQualifiedCppTypeName(string cppType)
    {
        var normalized = cppType
            .Replace("const ", string.Empty, StringComparison.Ordinal)
            .Replace("&", string.Empty, StringComparison.Ordinal)
            .Replace("*", string.Empty, StringComparison.Ordinal)
            .Trim();

        return normalized;
    }

    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
