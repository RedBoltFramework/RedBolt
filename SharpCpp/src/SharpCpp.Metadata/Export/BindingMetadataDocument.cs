namespace SharpCpp.Metadata.Export;

public sealed record BindingMetadataDocument(
    int SchemaVersion,
    string GeneratorVersion,
    string SupportRuntimeVersion,
    BindingMetadataSource Source,
    IReadOnlyList<BindingMetadataImport> Imports,
    BindingMetadataFingerprint Fingerprint,
    IReadOnlyList<BindingMetadataType> Types,
    IReadOnlyList<BindingMetadataFunction> Functions,
    IReadOnlyList<BindingMetadataAlias>? Aliases = null);

public sealed record BindingMetadataSource(string Package, string ManagedNamespace, string NativeLibrary);

public sealed record BindingMetadataImport(string Package, string Hash);

public sealed record BindingMetadataFingerprint(
    string Phase2ClosureHash,
    string ClangArgsHash,
    string AttributeVocabularyHash,
    IReadOnlyList<string> HeaderFiles);

public sealed record BindingMetadataType(
    string SymbolId,
    string Kind,
    string NativeName,
    string ManagedName,
    string? ManagedNamespace,
    string? ManagedBaseTypeName,
    BindingMetadataTypeReference Projection,
    string? ManagedBaseTypeConstraints = null,
    string? ManagedInstanceCacheFactoryExpression = null,
    string? ManagedInstanceCacheTypeName = null,
    bool EmitDefaultNativeReleaseOverride = true,
    bool IsFixedArray = false,
    bool IsDynamicArray = false,
    bool IsFlags = false,
    IReadOnlyList<BindingMetadataEnumValue>? Values = null,
    IReadOnlyList<BindingMetadataRecordMember>? Members = null);

public sealed record BindingMetadataEnumValue(string NativeName, string ManagedName, string ValueExpression);

public sealed record BindingMetadataRecordMember(
    string Kind,
    string NativeName,
    string ManagedName,
    BindingMetadataTypeReference? Type,
    IReadOnlyList<BindingMetadataParameter>? Parameters,
    bool IsReadOnly,
    bool IsStatic,
    bool IsConstructor,
    string? OperatorToken = null,
    bool IsConversionOperator = false,
    bool IsExplicitConversionOperator = false,
    bool ReturnsOwnedHandle = false,
    bool GetterReturnsOwnedHandle = false);

public sealed record BindingMetadataFunction(
    string SymbolId,
    string NativeName,
    string ManagedName,
    string EntryPoint,
    BindingMetadataTypeReference ReturnType,
    IReadOnlyList<BindingMetadataParameter> Parameters,
    bool ReturnsOwnedHandle = false);

public sealed record BindingMetadataAlias(
    string CppQualifiedName,
    string NativeName,
    string ManagedName,
    string? ManagedNamespace,
    BindingMetadataTypeReference TargetType);

public sealed record BindingMetadataParameter(
    string Name,
    string Direction,
    BindingMetadataTypeReference Type,
    string? DefaultValueExpression = null,
    bool UseCallbackDelegate = false,
    string? CallbackDelegateTypeName = null);

public sealed record BindingMetadataTypeReference(
    string Kind,
    string CppType,
    string PublicType,
    string? Primitive,
    string? RecordId,
    string? MarshallerType,
    IReadOnlyList<BindingMetadataTypeReference>? GenericArguments = null,
    string? EnumId = null,
    string? ManagedMarshaller = null,
    bool IsCallback = false,
    BindingMetadataTypeReference? CallbackReturnType = null,
    IReadOnlyList<BindingMetadataTypeReference>? CallbackParameterTypes = null,
    BindingMetadataTypeReference? FixedArrayElementType = null,
    int? FixedArrayLength = null,
    BindingMetadataTypeReference? DynamicArrayElementType = null);

public sealed record BindingMetadataExportOptions(
    string PackageId,
    string GeneratorVersion = BindingMetadataSchema.PreviewGeneratorVersion,
    string SupportRuntimeVersion = BindingMetadataSchema.PreviewSupportRuntimeVersion,
    string Phase2ClosureHash = "phase2-bootstrap",
    string ClangArgsHash = "clang-bootstrap",
    string AttributeVocabularyHash = BindingMetadataSchema.AttributeVocabularyVersion,
    IReadOnlyList<BindingMetadataImport>? Imports = null);
