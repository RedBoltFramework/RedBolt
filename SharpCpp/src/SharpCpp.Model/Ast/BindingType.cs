namespace SharpCpp.Model.Ast;

public enum BindingTypeKind
{
    Unsupported = 0,
    Primitive = 1,
    Handle = 2,
    Enum = 3,
    GenericParameter = 4
}

public enum BindingPrimitiveKind
{
    Unsupported = 0,
    Void = 1,
    Boolean = 2,
    Int8 = 3,
    UInt8 = 4,
    Int16 = 5,
    UInt16 = 6,
    Int32 = 7,
    UInt32 = 8,
    Int64 = 9,
    UInt64 = 10,
    Single = 11,
    Double = 12,
    NativeInt = 13,
    NativeUInt = 14,
    IntPtr = 15
}

public sealed record MarshallerDescriptor(
    string CppTypeName,
    string PublicTypeName,
    string? MarshallerTypeName = null,
    string? ManagedMarshaller = null)
{
    public bool CppIsRecordValue { get; init; }

    public bool CppIsReference { get; init; }

    public bool IsRefStruct => PublicTypeName is "global::System.ReadOnlySpan<byte>";
}

public sealed class BindingType
{
    private readonly IReadOnlyList<BindingType> _genericArguments;
    private readonly IReadOnlyList<BindingType> _callbackParameterTypes;
    private string? _managedTypeFormatOverride;

    private BindingType(
        BindingTypeKind kind,
        BindingPrimitiveKind primitiveKind,
        MarshallerDescriptor marshaller,
        string? recordId = null,
        string? enumId = null,
        string? aliasCppQualifiedName = null,
        string? aliasNativeName = null,
        string? genericParameterName = null,
        IEnumerable<BindingType>? genericArguments = null,
        bool isCallback = false,
        BindingType? callbackReturnType = null,
        IEnumerable<BindingType>? callbackParameterTypes = null,
        BindingType? fixedArrayElementType = null,
        int? fixedArrayLength = null,
        BindingType? dynamicArrayElementType = null)
    {
        Kind = kind;
        PrimitiveKind = primitiveKind;
        Marshaller = marshaller;
        RecordId = recordId;
        EnumId = enumId;
        AliasCppQualifiedName = aliasCppQualifiedName;
        AliasNativeName = aliasNativeName;
        GenericParameterName = genericParameterName;
        _genericArguments = genericArguments?.ToArray() ?? Array.Empty<BindingType>();
        IsCallback = isCallback;
        CallbackReturnType = callbackReturnType;
        _callbackParameterTypes = callbackParameterTypes?.ToArray() ?? Array.Empty<BindingType>();
        FixedArrayElementType = fixedArrayElementType;
        FixedArrayLength = fixedArrayLength;
        DynamicArrayElementType = dynamicArrayElementType;
    }

    public static BindingType Void { get; } = Primitive(BindingPrimitiveKind.Void, "void", "void");

    public static BindingType Boolean { get; } = Primitive(BindingPrimitiveKind.Boolean, "bool", "bool");

    public static BindingType Int8 { get; } = Primitive(BindingPrimitiveKind.Int8, "signed char", "sbyte");

    public static BindingType UInt8 { get; } = Primitive(BindingPrimitiveKind.UInt8, "unsigned char", "byte");

    public static BindingType Int16 { get; } = Primitive(BindingPrimitiveKind.Int16, "short", "short");

    public static BindingType UInt16 { get; } = Primitive(BindingPrimitiveKind.UInt16, "unsigned short", "ushort");

    public static BindingType Int32 { get; } = Primitive(BindingPrimitiveKind.Int32, "int", "int");

    public static BindingType UInt32 { get; } = Primitive(BindingPrimitiveKind.UInt32, "unsigned int", "uint");

    public static BindingType Int64 { get; } = Primitive(BindingPrimitiveKind.Int64, "long long", "long");

    public static BindingType UInt64 { get; } = Primitive(BindingPrimitiveKind.UInt64, "unsigned long long", "ulong");

    public static BindingType Single { get; } = Primitive(BindingPrimitiveKind.Single, "float", "float");

    public static BindingType Double { get; } = Primitive(BindingPrimitiveKind.Double, "double", "double");

    public static BindingType NativeInt { get; } = Primitive(BindingPrimitiveKind.NativeInt, "long", "nint");

    public static BindingType NativeUInt { get; } = Primitive(BindingPrimitiveKind.NativeUInt, "unsigned long", "nuint");

    public static BindingType IntPtr { get; } = Primitive(BindingPrimitiveKind.IntPtr, "void*", "global::System.IntPtr");

    public static BindingPrimitiveKind PrimitiveKindFromCppName(string cppTypeName) => cppTypeName switch
    {
        "void" => BindingPrimitiveKind.Void,
        "bool" => BindingPrimitiveKind.Boolean,
        "signed char" => BindingPrimitiveKind.Int8,
        "unsigned char" => BindingPrimitiveKind.UInt8,
        "short" => BindingPrimitiveKind.Int16,
        "unsigned short" => BindingPrimitiveKind.UInt16,
        "int" => BindingPrimitiveKind.Int32,
        "unsigned int" => BindingPrimitiveKind.UInt32,
        "long long" => BindingPrimitiveKind.Int64,
        "unsigned long long" => BindingPrimitiveKind.UInt64,
        "float" => BindingPrimitiveKind.Single,
        "double" => BindingPrimitiveKind.Double,
        "long" => BindingPrimitiveKind.NativeInt,
        "unsigned long" => BindingPrimitiveKind.NativeUInt,
        "void*" => BindingPrimitiveKind.IntPtr,
        _ => BindingPrimitiveKind.Int32
    };

    public BindingTypeKind Kind { get; }

    public BindingPrimitiveKind PrimitiveKind { get; }

    public MarshallerDescriptor Marshaller { get; }

    public string? RecordId { get; }

    public string? EnumId { get; }

    public string? AliasCppQualifiedName { get; }

    public string? AliasNativeName { get; }

    public string? GenericParameterName { get; }

    public IReadOnlyList<BindingType> GenericArguments => _genericArguments;

    public bool IsCallback { get; }

    public BindingType? CallbackReturnType { get; }

    public IReadOnlyList<BindingType> CallbackParameterTypes => _callbackParameterTypes;

    public BindingType? FixedArrayElementType { get; }

    public int? FixedArrayLength { get; }

    public bool IsFixedArray => FixedArrayElementType is not null && FixedArrayLength is not null;

    public BindingType? DynamicArrayElementType { get; }

    public bool IsDynamicArray => DynamicArrayElementType is not null;

    public string CppTypeName => Marshaller.CppTypeName;

    public string ManagedTypeName => Kind == BindingTypeKind.GenericParameter
        ? GenericParameterName ?? Marshaller.PublicTypeName
        : FormatManagedTypeName();

    public string UnaliasedManagedTypeName => Kind == BindingTypeKind.GenericParameter
        ? GenericParameterName ?? Marshaller.PublicTypeName
        : FormatManagedTypeName(ignoreManagedTypeFormatOverride: true, unaliasGenericArguments: true);

    public string? MarshallerTypeName => Marshaller.MarshallerTypeName;

    public string? ManagedMarshaller => Marshaller.ManagedMarshaller;

    public bool IsGenericParameter => Kind == BindingTypeKind.GenericParameter;

    public bool ContainsGenericParameters => IsGenericParameter || GenericArguments.Any(static argument => argument.ContainsGenericParameters);

    public bool IsPointerType => Kind == BindingTypeKind.Handle && string.IsNullOrEmpty(Marshaller.MarshallerTypeName);

    public bool HasUnresolvedManagedNamespaceSeparators => ContainsUnresolvedManagedNamespaceSeparators(this);

    public string? ManagedTypeFormatOverride => _managedTypeFormatOverride;

    public void SetManagedTypeFormatOverride(string? managedTypeFormatOverride)
    {
        _managedTypeFormatOverride = string.IsNullOrWhiteSpace(managedTypeFormatOverride) ? null : managedTypeFormatOverride;
    }

    public BindingType WithManagedTypeFormatOverride(string? managedTypeFormatOverride)
    {
        var normalizedOverride = string.IsNullOrWhiteSpace(managedTypeFormatOverride)
            ? null
            : managedTypeFormatOverride;
        if (string.Equals(_managedTypeFormatOverride, normalizedOverride, StringComparison.Ordinal))
        {
            return this;
        }

        var copy = new BindingType(
            Kind,
            PrimitiveKind,
            Marshaller,
            RecordId,
            EnumId,
            AliasCppQualifiedName,
            AliasNativeName,
            GenericParameterName,
            GenericArguments,
            IsCallback,
            CallbackReturnType,
            CallbackParameterTypes,
            FixedArrayElementType,
            FixedArrayLength,
            DynamicArrayElementType)
        {
            _managedTypeFormatOverride = normalizedOverride
        };
        return copy;
    }

    public BindingType WithGenericArguments(IEnumerable<BindingType> genericArguments)
    {
        ArgumentNullException.ThrowIfNull(genericArguments);

        var arguments = genericArguments.ToArray();
        if (GenericArguments.SequenceEqual(arguments))
        {
            return this;
        }

        var copy = new BindingType(
            Kind,
            PrimitiveKind,
            Marshaller,
            RecordId,
            EnumId,
            AliasCppQualifiedName,
            AliasNativeName,
            GenericParameterName,
            arguments,
            IsCallback,
            CallbackReturnType,
            CallbackParameterTypes,
            FixedArrayElementType,
            FixedArrayLength,
            DynamicArrayElementType)
        {
            _managedTypeFormatOverride = _managedTypeFormatOverride
        };
        return copy;
    }

    public BindingType WithCallbackSignature(BindingType callbackReturnType, IReadOnlyList<BindingType> callbackParameterTypes)
    {
        ArgumentNullException.ThrowIfNull(callbackReturnType);
        ArgumentNullException.ThrowIfNull(callbackParameterTypes);

        if (!IsCallback ||
            (Equals(CallbackReturnType, callbackReturnType) && CallbackParameterTypes.SequenceEqual(callbackParameterTypes)))
        {
            return this;
        }

        var copy = CreateCallback(
            CppTypeName,
            Marshaller.PublicTypeName,
            ManagedMarshaller,
            callbackReturnType,
            callbackParameterTypes)
            .WithManagedTypeFormatOverride(_managedTypeFormatOverride);
        return copy;
    }

    public BindingType WithFixedArray(BindingType elementType, int length)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        if (FixedArrayElementType == elementType && FixedArrayLength == length)
        {
            return this;
        }

        var copy = new BindingType(
            Kind,
            PrimitiveKind,
            Marshaller,
            RecordId,
            EnumId,
            AliasCppQualifiedName,
            AliasNativeName,
            GenericParameterName,
            GenericArguments,
            IsCallback,
            CallbackReturnType,
            CallbackParameterTypes,
            elementType,
            length,
            DynamicArrayElementType)
        {
            _managedTypeFormatOverride = _managedTypeFormatOverride
        };
        return copy;
    }

    public BindingType WithDynamicArray(BindingType elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        if (DynamicArrayElementType == elementType)
        {
            return this;
        }

        var copy = new BindingType(
            Kind,
            PrimitiveKind,
            Marshaller,
            RecordId,
            EnumId,
            AliasCppQualifiedName,
            AliasNativeName,
            GenericParameterName,
            GenericArguments,
            IsCallback,
            CallbackReturnType,
            CallbackParameterTypes,
            FixedArrayElementType,
            FixedArrayLength,
            elementType)
        {
            _managedTypeFormatOverride = _managedTypeFormatOverride
        };
        return copy;
    }

    public static BindingType CreateHandle(
        string cppTypeName,
        string publicTypeName,
        string? recordId = null,
        string? marshallerTypeName = null,
        IEnumerable<BindingType>? genericArguments = null,
        bool cppIsRecordValue = false)
    {
        return new BindingType(
            BindingTypeKind.Handle,
            BindingPrimitiveKind.Unsupported,
            new MarshallerDescriptor(cppTypeName, publicTypeName, marshallerTypeName)
            {
                CppIsRecordValue = cppIsRecordValue
            },
            recordId: recordId,
            genericArguments: genericArguments);
    }

    public static BindingType CreateEnum(string cppTypeName, string publicTypeName, IEnumerable<BindingType>? genericArguments = null, string? enumId = null)
    {
        return new BindingType(
            BindingTypeKind.Enum,
            BindingPrimitiveKind.Unsupported,
            new MarshallerDescriptor(cppTypeName, publicTypeName),
            enumId: enumId,
            genericArguments: genericArguments);
    }

    public static BindingType CreateConfigured(
        string cppTypeName,
        string publicTypeName,
        BindingPrimitiveKind primitiveKind = BindingPrimitiveKind.Unsupported,
        IEnumerable<BindingType>? genericArguments = null)
    {
        return CreateConfigured(cppTypeName, publicTypeName, null, null, primitiveKind, genericArguments);
    }

    public static BindingType CreateConfigured(
        string cppTypeName,
        string publicTypeName,
        string? marshallerTypeName,
        BindingPrimitiveKind primitiveKind = BindingPrimitiveKind.Unsupported,
        IEnumerable<BindingType>? genericArguments = null)
    {
        return CreateConfigured(cppTypeName, publicTypeName, marshallerTypeName, null, primitiveKind, genericArguments);
    }

    public static BindingType CreateConfigured(
        string cppTypeName,
        string publicTypeName,
        string? marshallerTypeName,
        string? managedMarshaller,
        BindingPrimitiveKind primitiveKind = BindingPrimitiveKind.Unsupported,
        IEnumerable<BindingType>? genericArguments = null,
        bool cppIsReference = false)
    {
        return new BindingType(
            BindingTypeKind.Primitive,
            primitiveKind,
            new MarshallerDescriptor(cppTypeName, publicTypeName, marshallerTypeName, managedMarshaller)
            {
                CppIsReference = cppIsReference
            },
            genericArguments: genericArguments);
    }

    public static BindingType CreateCallback(
        string cppTypeName,
        string publicTypeFormat,
        string? managedMarshaller,
        BindingType callbackReturnType,
        IReadOnlyList<BindingType> callbackParameterTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cppTypeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicTypeFormat);
        ArgumentNullException.ThrowIfNull(callbackReturnType);
        ArgumentNullException.ThrowIfNull(callbackParameterTypes);

        var genericArguments = callbackReturnType.PrimitiveKind == BindingPrimitiveKind.Void
            ? callbackParameterTypes.ToArray()
            : callbackParameterTypes.Concat([callbackReturnType]).ToArray();

        return new BindingType(
            BindingTypeKind.Primitive,
            BindingPrimitiveKind.Unsupported,
            new MarshallerDescriptor(cppTypeName, publicTypeFormat, ManagedMarshaller: managedMarshaller),
            genericArguments: genericArguments,
            isCallback: true,
            callbackReturnType: callbackReturnType,
            callbackParameterTypes: callbackParameterTypes);
    }

    public override string ToString()
    {
        return ManagedTypeName;
    }

    private static BindingType Primitive(
        BindingPrimitiveKind primitiveKind,
        string cppTypeName,
        string publicTypeName)
    {
        return new BindingType(
            BindingTypeKind.Primitive,
            primitiveKind,
            new MarshallerDescriptor(cppTypeName, publicTypeName));
    }

    public static BindingType CreateGenericParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new BindingType(
            BindingTypeKind.GenericParameter,
            BindingPrimitiveKind.Unsupported,
            new MarshallerDescriptor(name, name),
            genericParameterName: name);
    }

    public BindingType WithCppIsRecordValue(bool value)
    {
        if (Marshaller.CppIsRecordValue == value)
            return this;
        return new BindingType(
            Kind,
            PrimitiveKind,
            Marshaller with { CppIsRecordValue = value },
            RecordId,
            EnumId,
            AliasCppQualifiedName,
            AliasNativeName,
            GenericParameterName,
            GenericArguments,
            IsCallback,
            CallbackReturnType,
            CallbackParameterTypes,
            FixedArrayElementType,
            FixedArrayLength,
            DynamicArrayElementType);
    }

    public BindingType WithCppTypeName(string cppTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cppTypeName);
        if (string.Equals(Marshaller.CppTypeName, cppTypeName, StringComparison.Ordinal))
            return this;
        var copy = new BindingType(
            Kind,
            PrimitiveKind,
            Marshaller with { CppTypeName = cppTypeName },
            RecordId,
            EnumId,
            AliasCppQualifiedName,
            AliasNativeName,
            GenericParameterName,
            GenericArguments,
            IsCallback,
            CallbackReturnType,
            CallbackParameterTypes,
            FixedArrayElementType,
            FixedArrayLength,
            DynamicArrayElementType)
        {
            _managedTypeFormatOverride = _managedTypeFormatOverride
        };
        return copy;
    }

    public BindingType WithCppIsReference(bool value)
    {
        if (Marshaller.CppIsReference == value)
            return this;
        return new BindingType(
            Kind,
            PrimitiveKind,
            Marshaller with { CppIsReference = value },
            RecordId,
            EnumId,
            AliasCppQualifiedName,
            AliasNativeName,
            GenericParameterName,
            GenericArguments,
            IsCallback,
            CallbackReturnType,
            CallbackParameterTypes,
            FixedArrayElementType,
            FixedArrayLength,
            DynamicArrayElementType);
    }

    public BindingType WithAliasSurface(string cppQualifiedName, string nativeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cppQualifiedName);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeName);
        if (string.Equals(AliasCppQualifiedName, cppQualifiedName, StringComparison.Ordinal) &&
            string.Equals(AliasNativeName, nativeName, StringComparison.Ordinal))
        {
            return this;
        }

        var copy = new BindingType(
            Kind,
            PrimitiveKind,
            Marshaller,
            RecordId,
            EnumId,
            cppQualifiedName,
            nativeName,
            GenericParameterName,
            GenericArguments,
            IsCallback,
            CallbackReturnType,
            CallbackParameterTypes,
            FixedArrayElementType,
            FixedArrayLength,
            DynamicArrayElementType)
        {
            _managedTypeFormatOverride = _managedTypeFormatOverride
        };
        return copy;
    }

    private static bool ContainsUnresolvedManagedNamespaceSeparators(BindingType type)
    {
        if (type.Kind == BindingTypeKind.GenericParameter)
        {
            return false;
        }

        var publicName = type.Marshaller.PublicTypeName;
        if (publicName.Contains("::", StringComparison.Ordinal) &&
            !publicName.StartsWith("global::", StringComparison.Ordinal))
        {
            return true;
        }

        return type.GenericArguments.Any(ContainsUnresolvedManagedNamespaceSeparators);
    }

    private string FormatManagedTypeName(
        string? managedTypeFormatOverride = null,
        bool ignoreManagedTypeFormatOverride = false,
        bool unaliasGenericArguments = false)
    {
        // When an explicit managed type format override is set (via
        // Marshaller<T> attributes) and contains no format placeholders,
        // use it as-is — C++ template arguments don't apply to the
        // configured managed type.
        var effectiveFormatOverride = ignoreManagedTypeFormatOverride
            ? managedTypeFormatOverride
            : managedTypeFormatOverride ?? _managedTypeFormatOverride;
        if (effectiveFormatOverride is not null &&
            !effectiveFormatOverride.Contains('{', StringComparison.Ordinal))
        {
            return effectiveFormatOverride;
        }

        var managedTypeFormat = effectiveFormatOverride ?? Marshaller.PublicTypeName;
        if (GenericArguments.Count == 0)
        {
            return managedTypeFormat;
        }

        var renderedArguments = GenericArguments
            .Select(argument => unaliasGenericArguments ? argument.UnaliasedManagedTypeName : argument.ManagedTypeName)
            .ToArray();
        var argumentNames = renderedArguments.Cast<object>().ToArray();
        return managedTypeFormat.Contains('{', StringComparison.Ordinal)
            ? FormatPlaceholderManagedTypeName(managedTypeFormat, argumentNames, renderedArguments)
            : $"{managedTypeFormat}<{string.Join(", ", renderedArguments)}>";
    }

    private static string FormatPlaceholderManagedTypeName(string managedTypeFormat, object[] argumentNames, IReadOnlyList<string> renderedArguments)
    {
        if (AllPlaceholdersInRange(managedTypeFormat, argumentNames.Length))
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, managedTypeFormat, argumentNames);
        }

        var genericStart = managedTypeFormat.IndexOf('<');
        var managedTypeHead = genericStart >= 0 ? managedTypeFormat[..genericStart] : managedTypeFormat;
        return $"{managedTypeHead}<{string.Join(", ", renderedArguments)}>";
    }

    private static bool AllPlaceholdersInRange(string format, int argumentCount)
    {
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

            if (int.TryParse(format[(index + 1)..closeIndex], out var placeholderIndex) &&
                placeholderIndex >= argumentCount)
            {
                return false;
            }

            index = closeIndex;
        }

        return true;
    }
}
