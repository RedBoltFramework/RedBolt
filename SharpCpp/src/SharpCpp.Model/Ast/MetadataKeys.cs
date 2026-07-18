namespace SharpCpp.Model.Ast;

public static class MetadataKeys
{
    public const string CanonicalCppIdentity = "canonical_cpp_identity";
    public const string ManagedTypeIdentity = "managed_type_identity";
    public const string FixedArrayRecord = "fixed_array_record";
    public const string NativeArrayRecord = "native_array_record";
    public const string DynamicArrayRecord = "dynamic_array_record";
    public const string Marshaller = "marshaller";
    public const string AccessorProperty = "accessor_property";
    public const string IgnoreReason = "ignore_reason";
    public const string SilentExclusion = "silent_exclusion";
    public const string Warnings = "warnings";
    public const string DocumentationComment = "documentation_comment";
    public const string DeclarationDisplayText = "declaration_display_text";
    public const string ExplicitGenericInstantiations = "explicit_generic_instantiations";
    public const string ForwardDeclaration = "forward_declaration";
    public const string OpaqueNativeHandle = "opaque_native_handle";
    public const string NativeInterfaceWrapper = "native_interface_wrapper";
    public const string NativeInterfaceQuery = "native_interface_query";
    public const string NativeInterfaceReleaseMethod = "native_interface_release_method";
    public const string PlaceholderRecord = "placeholder_record";
    public const string SuppressEmission = "suppress_emission";
    public const string HasNativeDefinition = "has_native_definition";
    public const string NonPublicDestructor = "non_public_destructor";
    public const string UnexpressibleDefault = "unexpressible_default";
    public const string MarshallerExposedMethod = "marshaller_exposed_method";
    public const string MarshallerConfiguredRecord = "marshaller_configured_record";
    public const string UndefaultableParam = "undefaultable_param";
    public const string ManagedBindingProjection = "managed_binding_projection";
    public const string ExplicitManagedRename = "explicit_managed_rename";
    public const string ManagedStaticInterfaces = "managed_static_interfaces";
    public const string ManagedStaticInterfacesAbstract = "managed_static_interfaces_abstract";
    public const string NativeFieldAccessExpression = "native_field_access_expression";
    public const string NativeDefaultValueExpression = "native_default_value_expression";
    public const string InputPointerArray = "input_pointer_array";
}

public sealed record NativeInterfaceQueryAnnotation(string SourceRecordCppQualifiedName, string InterfaceIdExpression);

public enum AccessorPropertyKind
{
    Getter = 0,
    Setter = 1
}

public sealed record AccessorPropertyAnnotation(string PropertyName, AccessorPropertyKind Kind);
