using SharpCpp.Model.Ast;
using SharpCpp.Model.Utilities;

namespace SharpCpp.Rendering;

public static partial class BindingOutputRenderer
{
    private static string RenderManagedGenericFreeFunction(BindingCompilation compilation, FunctionBinding function)
    {
        var writer = new CodeWriter();
        WriteDocumentationComment(writer, function);
        var methodTypeParams = string.Join(", ", function.GenericParameters.Select(static param => param.Name));
        var returnTypeName = GetGenericManagedReturnType(function.ReturnType);
        var parameterList = string.Join(", ", function.Parameters.Select(p => RenderManagedPublicParameter(p, compilation)));
        var instantiations = function.SupportedGenericInstantiations.OrderBy(
            static instantiation => string.Join("|", instantiation.TypeArguments.Select(static argument => argument.CppTypeName)),
            StringComparer.Ordinal).ToArray();

        writer.Write($$"""
            public static {{returnTypeName}} {{function.ManagedName}}<{{methodTypeParams}}>({{parameterList}})
            {
            """);

        var methodParams = (IReadOnlyList<BindingGenericParameter>)function.GenericParameters.ToList();
        foreach (var instantiation in instantiations)
        {
            var concreteParameters = function.Parameters.Select(parameter => SubstituteFunctionGenericType(parameter.Type, methodParams, instantiation)).ToArray();
            var concreteReturnType = SubstituteFunctionGenericType(function.ReturnType, methodParams, instantiation);
            var condition = BuildFunctionInstantiationCondition(compilation, function, instantiation);
            var importMethodName = GetGenericImportMethodName(function, instantiation);
            var invocationArguments = new List<string>();

            for (var index = 0; index < function.Parameters.Count; index++)
            {
                invocationArguments.Add(BuildGenericInvocationArgument(compilation, function.Parameters[index], concreteParameters[index]));
            }

            writer.WriteLine($"    if ({condition})");
            writer.WriteLine("    {");
            var keepAliveStatements = BuildGenericKeepAliveStatements(function.Parameters, concreteParameters);

            if (concreteReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
            {
                writer.WriteLine($"        {GetGenericImportAccessor(compilation, function)}.{importMethodName}({string.Join(", ", invocationArguments)});");
                foreach (var keepAlive in keepAliveStatements)
                {
                    writer.WriteLine($"        {keepAlive}");
                }
                writer.WriteLine("        return;");
            }
            else if (function.ReturnType.IsGenericParameter)
            {
                writer.WriteLine($"        var __result = {GetGenericImportAccessor(compilation, function)}.{importMethodName}({string.Join(", ", invocationArguments)});");
                foreach (var keepAlive in keepAliveStatements)
                {
                    writer.WriteLine($"        {keepAlive}");
                }
                if (concreteReturnType.Kind == BindingTypeKind.Handle)
                {
                    writer.WriteLine($"        return ({returnTypeName})(object){BuildManagedHandleReturnResultExpression(compilation, concreteReturnType, "__result", ReturnsOwnedHandle(function, concreteReturnType))}!;");
                }
                else
                {
                    writer.WriteLine($"        return ({returnTypeName})(object)__result!;");
                }
            }
            else if (concreteReturnType.Kind == BindingTypeKind.Handle)
            {
                writer.WriteLine($"        var __result = {GetGenericImportAccessor(compilation, function)}.{importMethodName}({string.Join(", ", invocationArguments)});");
                foreach (var keepAlive in keepAliveStatements)
                {
                    writer.WriteLine($"        {keepAlive}");
                }
                writer.WriteLine($"        return {BuildManagedHandleReturnResultExpression(compilation, concreteReturnType, "__result", ReturnsOwnedHandle(function, concreteReturnType))};");
            }
            else
            {
                writer.WriteLine($"        var __result = {GetGenericImportAccessor(compilation, function)}.{importMethodName}({string.Join(", ", invocationArguments)});");
                foreach (var keepAlive in keepAliveStatements)
                {
                    writer.WriteLine($"        {keepAlive}");
                }
                writer.WriteLine("        return __result;");
            }

            writer.WriteLine("    }");
        }

        writer.Write($$"""
            throw new NotSupportedException("SharpCpp does not support the requested generic argument combination for '{{function.ManagedName}}'.");
            }
            """);
        return writer.ToString();
    }

    private static IEnumerable<string> CollectGenericFreeFunctionImportDeclarations(BindingCompilation compilation, FunctionBinding function)
    {
        foreach (var instantiation in function.SupportedGenericInstantiations.OrderBy(
                     static item => string.Join("|", item.TypeArguments.Select(static argument => argument.CppTypeName)),
                     StringComparer.Ordinal))
        {
            yield return RenderGenericFreeFunctionImportDeclaration(compilation, function, instantiation);
        }
    }

    private static string RenderGenericRecord(BindingCompilation compilation, RecordBinding record)
    {
        if (IsGenericArrayRecord(record))
        {
            return RenderGenericArrayRecord(compilation, record);
        }

        ValidateGenericRecordSlice(record);

        var writer = new CodeWriter();
        var constructors = record.Members.OfType<FunctionBinding>().Where(static function => function.IsConstructor && !function.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static function => function.Parameters.Count).ToArray();
        var instanceMethods = record.Members.OfType<FunctionBinding>().Where(static function => !function.IsConstructor && !function.IsStatic && function.OperatorToken is null && !function.IsConversionOperator && !function.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static function => function.ManagedName, StringComparer.Ordinal).ToArray();
        var genericInstanceMethods = instanceMethods.Where(static function => function.IsGeneric).ToArray();
        var concreteInstanceMethods = instanceMethods.Where(static function => !function.IsGeneric).ToArray();
        var staticMethods = record.Members.OfType<FunctionBinding>().Where(static function => function.IsStatic && function.OperatorToken is null && !function.IsConversionOperator && !function.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static function => function.ManagedName, StringComparer.Ordinal).ToArray();
        var instantiations = GetCanonicalManagedGenericInstantiations(compilation, record);
        var baseTypeName = GetGenericManagedBaseTypeName(compilation, record);
        var genericConstraints = GetGenericRecordConstraints(record);
        var canEmitClone = CanEmitClone(record, isEffectivelyAbstract: false, canEmitDeleteThunk: true);
        var additionalInterfaces = GetConfiguredManagedStaticInterfaces(compilation, record);
        var additionalInterfaceClause = additionalInterfaces.Count == 0
            ? string.Empty
            : $", {string.Join(", ", additionalInterfaces)}";

        WriteDocumentationComment(writer, record);
        writer.Write($$"""
            public partial class {{GetGenericRecordDeclarationName(record)}} : {{baseTypeName}}, global::SharpCpp.Support.INativeObjectRegistration<{{GetGenericRecordDeclarationName(record)}}>{{(canEmitClone ? ", global::System.ICloneable" : string.Empty)}}{{additionalInterfaceClause}}{{genericConstraints}}
            {
            internal static readonly {{GetInstanceCacheFieldTypeName(record)}} s_instanceCache = {{GetInstanceCacheFactoryExpression(record)}};

            protected override global::SharpCpp.Support.INativeInstanceCache? InstanceCache => s_instanceCache;

            internal {{record.ManagedName}}(nint handle, bool ownsHandle = false)
                : base(handle, ownsHandle)
            {
            }

            public nint Handle => DangerousGetHandle();
            public bool OwnsHandle => OwnsNativeHandle;
            public bool IsNull => IsClosed || IsInvalid;

            internal static {{GetGenericRecordDeclarationName(record)}} CreateManagedWrapper(nint handle, bool ownsHandle)
            {
            if (handle == 0)
                return new {{GetGenericRecordDeclarationName(record)}}(0, ownsHandle);

            return ({{GetGenericRecordDeclarationName(record)}})global::SharpCpp.Support.NativeObjectRegistry.Create<{{GetGenericRecordDeclarationName(record)}}>(handle, ownsHandle);
            }

            static bool global::SharpCpp.Support.INativeObjectRegistration<{{GetGenericRecordDeclarationName(record)}}>.RegistrationIsPolymorphic => false;

            static nuint global::SharpCpp.Support.INativeObjectRegistration<{{GetGenericRecordDeclarationName(record)}}>.RegistrationDynamicTypeId => 0;

            static global::System.Type global::SharpCpp.Support.INativeObjectRegistration<{{GetGenericRecordDeclarationName(record)}}>.RegistrationExpectedType => typeof({{GetGenericRecordDeclarationName(record)}});

            static global::SharpCpp.Support.INativeInstanceCache global::SharpCpp.Support.INativeObjectRegistration<{{GetGenericRecordDeclarationName(record)}}>.RegistrationInstanceCache => s_instanceCache;

            static global::System.Func<nint, bool, global::SharpCpp.Support.NativeObject>? global::SharpCpp.Support.INativeObjectRegistration<{{GetGenericRecordDeclarationName(record)}}>.RegistrationCreateFactory => static (handle, ownsHandle) => new {{GetGenericRecordDeclarationName(record)}}(handle, ownsHandle);

            static global::SharpCpp.Support.NativeObjectMetadata global::SharpCpp.Support.INativeObjectRegistration<{{GetGenericRecordDeclarationName(record)}}>.RegistrationGetMetadata(nint handle)
            {
            return new global::SharpCpp.Support.NativeObjectMetadata(handle, 0);
            }
            """);

        foreach (var constructor in constructors)
        {
            writer.WriteLine();
            writer.Write(RenderGenericManagedConstructor(compilation, record, constructor, instantiations));
        }

        foreach (var method in concreteInstanceMethods)
        {
            writer.WriteLine();
            writer.Write(RenderGenericManagedInstanceMethod(compilation, record, method, instantiations));
        }

        foreach (var method in genericInstanceMethods)
        {
            writer.WriteLine();
            writer.Write(RenderGenericManagedGenericMethod(compilation, record, method));
        }

        foreach (var method in staticMethods)
        {
            writer.WriteLine();
            writer.Write(RenderGenericManagedStaticMethod(compilation, record, method, instantiations));
        }

        if (canEmitClone)
        {
            writer.WriteLine();
            writer.Write(RenderGenericManagedCloneMethod(compilation, record, instantiations));
        }

        if (record.EmitDefaultNativeReleaseOverride)
        {
            writer.WriteLine();
            writer.Write(RenderGenericDeleteHelper(compilation, record, instantiations));
        }

        writer.WriteLine();
        writer.Write(
            """
            }
            """);

        writer.WriteLine();
        writer.Write(RenderManagedHandleReturnMarshallerHelpers(compilation, record, GetGenericRecordDeclarationName(record)));
        return writer.ToString();
    }

    private static IEnumerable<string> CollectGenericImportDeclarations(BindingCompilation compilation, RecordBinding record)
    {
        if (IsGenericArrayRecord(record))
        {
            foreach (var declaration in CollectGenericArrayImportDeclarations(compilation, record))
            {
                yield return declaration;
            }

            yield break;
        }

        var constructors = record.Members.OfType<FunctionBinding>().Where(static function => function.IsConstructor && !function.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static function => function.Parameters.Count).ToArray();
        var instanceMethods = record.Members.OfType<FunctionBinding>().Where(static function => !function.IsConstructor && !function.IsStatic && function.OperatorToken is null && !function.IsConversionOperator && !function.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static function => function.ManagedName, StringComparer.Ordinal).ToArray();
        var concreteInstanceMethods = instanceMethods.Where(static function => !function.IsGeneric).ToArray();
        var genericInstanceMethods = instanceMethods.Where(static function => function.IsGeneric).ToArray();
        var staticMethods = record.Members.OfType<FunctionBinding>().Where(static function => function.IsStatic && function.OperatorToken is null && !function.IsConversionOperator && !function.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static function => function.ManagedName, StringComparer.Ordinal).ToArray();
        var instantiations = GetCanonicalManagedGenericInstantiations(compilation, record);

        foreach (var constructor in constructors)
        {
            foreach (var instantiation in instantiations)
            {
                yield return RenderGenericConstructorImportDeclaration(compilation, record, constructor, instantiation);
            }
        }

        foreach (var method in concreteInstanceMethods)
        {
            foreach (var instantiation in instantiations)
            {
                yield return RenderGenericInstanceImportDeclaration(compilation, record, method, instantiation);
            }
        }

        foreach (var method in staticMethods)
        {
            foreach (var instantiation in instantiations)
            {
                yield return RenderGenericStaticImportDeclaration(compilation, record, method, instantiation);
            }
        }

        if (CanEmitClone(record, isEffectivelyAbstract: false, canEmitDeleteThunk: true))
        {
            foreach (var instantiation in instantiations)
            {
                yield return RenderGenericCloneImportDeclaration(compilation, record, instantiation);
            }
        }

        foreach (var method in genericInstanceMethods)
        {
            var methodInstantiations = method.SupportedGenericInstantiations.ToArray();
            if (methodInstantiations.Length == 0)
            {
                continue;
            }

            foreach (var recordInstantiation in instantiations)
            {
                foreach (var declaration in CollectGenericMethodImportDeclarations(compilation, record, method, [recordInstantiation]))
                {
                    yield return declaration;
                }
            }
        }

        foreach (var instantiation in instantiations)
        {
            yield return RenderGenericDeleteImportDeclaration(compilation, record, instantiation);
        }
    }

    private static IReadOnlyList<RecordGenericInstantiationBinding> GetCanonicalManagedGenericInstantiations(
        BindingCompilation compilation,
        RecordBinding record)
    {
        return record.SupportedGenericInstantiations
            .GroupBy(instantiation => GetManagedGenericInstantiationKey(compilation, record, instantiation), StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(instantiation => CountPointerLikeGenericArguments(instantiation.TypeArguments))
                .ThenByDescending(instantiation => CountManagedNativeNamespaceMatches(instantiation.TypeArguments))
                .ThenByDescending(instantiation => CountRecordValueGenericArguments(instantiation.TypeArguments))
                .ThenBy(instantiation => string.Join("|", instantiation.TypeArguments.Select(static argument => argument.CppTypeName)), StringComparer.Ordinal)
                .First())
            .OrderBy(static instantiation => string.Join("|", instantiation.TypeArguments.Select(static argument => argument.CppTypeName)), StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetManagedGenericInstantiationKey(
        BindingCompilation compilation,
        RecordBinding record,
        RecordGenericInstantiationBinding instantiation)
    {
        var visibleIndices = GetManagedVisibleGenericParameterIndices(record);
        var indices = visibleIndices.Count == 0
            ? Enumerable.Range(0, instantiation.TypeArguments.Count)
            : visibleIndices.Where(index => index < instantiation.TypeArguments.Count);

        return string.Join("|", indices.Select(index => GetManagedWrapperTypeName(instantiation.TypeArguments[index], compilation)));
    }

    private static int CountPointerLikeGenericArguments(IReadOnlyList<BindingType> arguments)
        => arguments.Sum(static argument =>
            (argument.Kind == BindingTypeKind.Handle && !argument.Marshaller.CppIsRecordValue ? 1 : 0) +
            CountPointerLikeGenericArguments(argument.GenericArguments));

    private static int CountRecordValueGenericArguments(IReadOnlyList<BindingType> arguments)
        => arguments.Sum(static argument =>
            (argument.Marshaller.CppIsRecordValue ? 1 : 0) +
            CountRecordValueGenericArguments(argument.GenericArguments));

    private static int CountManagedNativeNamespaceMatches(IReadOnlyList<BindingType> arguments)
        => arguments.Sum(static argument =>
            (ManagedRootNamespaceMatchesCpp(argument) ? 1 : 0) +
            CountManagedNativeNamespaceMatches(argument.GenericArguments));

    private static bool ManagedRootNamespaceMatchesCpp(BindingType argument)
    {
        var managedName = argument.ManagedTypeName;
        const string GlobalPrefix = "global::";
        if (managedName.StartsWith(GlobalPrefix, StringComparison.Ordinal))
        {
            managedName = managedName[GlobalPrefix.Length..];
        }

        var separator = managedName.IndexOf('.');
        if (separator <= 0)
        {
            return false;
        }

        var managedRoot = managedName[..separator];
        return argument.CppTypeName.StartsWith(managedRoot + "::", StringComparison.Ordinal);
    }

    private static bool IsGenericArrayRecord(RecordBinding record)
    {
        return IsGenericFixedArrayRecord(record) || IsGenericDynamicArrayRecord(record);
    }

    private static bool IsSharedNativeArrayRecord(RecordBinding record)
    {
        return record.GetMetadata<bool>(MetadataKeys.NativeArrayRecord);
    }

    private static bool IsGenericFixedArrayRecord(RecordBinding record)
    {
        return record.GetMetadata<bool>(MetadataKeys.FixedArrayRecord) ||
            record.ManagedBaseTypeName?.Contains("SharpCpp.Support.ArrayBase<", StringComparison.Ordinal) == true;
    }

    private static bool IsGenericDynamicArrayRecord(RecordBinding record)
    {
        return record.GetMetadata<bool>(MetadataKeys.DynamicArrayRecord) ||
            record.ManagedBaseTypeName?.Contains("SharpCpp.Support.SharedArrayBase<", StringComparison.Ordinal) == true;
    }

    private static string RenderGenericArrayRecord(BindingCompilation compilation, RecordBinding record)
    {
        if (IsSharedNativeArrayRecord(record))
        {
            return string.Empty;
        }

        return IsGenericDynamicArrayRecord(record)
            ? RenderGenericDynamicArrayRecord(compilation, record)
            : RenderGenericFixedArrayRecord(compilation, record);
    }

    private static string RenderGenericFixedArrayRecord(BindingCompilation compilation, RecordBinding record)
    {
        var instantiations = GetFixedArrayInstantiations(compilation, record).ToArray();
        var writer = new CodeWriter();
        var baseTypeName = GetGenericManagedBaseTypeName(compilation, record);
        writer.Write($$"""
            public partial class {{GetGenericRecordDeclarationName(record)}} : {{baseTypeName}}
            {
            internal {{record.ManagedName}}(nint handle, int length, bool ownsHandle = false)
                : base(handle, length, ownsHandle)
            {
                if (!IsInvalid)
                {
                    s_instanceCache.Register(this);
                }
            }

            internal static readonly global::SharpCpp.Support.DefaultNativeInstanceCache s_instanceCache = new();

            protected override global::SharpCpp.Support.INativeInstanceCache? InstanceCache => s_instanceCache;

            public nint Handle => DangerousGetHandle();
            public bool OwnsHandle => OwnsNativeHandle;
            public bool IsNull => IsClosed || IsInvalid;

            public {{record.ManagedName}}(int length)
                : this(CreateNative(length), length, ownsHandle: true)
            {
            }

            public {{record.ManagedName}}(global::System.ReadOnlySpan<T> values)
                : this(values.Length)
            {
                Assign(values);
            }

            public {{record.ManagedName}}(global::System.Collections.Generic.IEnumerable<T> values)
                : this(Materialize(values))
            {
            }

            private static T[] Materialize(global::System.Collections.Generic.IEnumerable<T> values)
            {
                global::System.ArgumentNullException.ThrowIfNull(values);
                return global::System.Linq.Enumerable.ToArray(values);
            }

            private {{record.ManagedName}}(T[] values)
                : this(values.AsSpan())
            {
            }

            internal static {{GetGenericRecordDeclarationName(record)}} CreateManagedWrapper(global::SharpCpp.Support.NativeArrayAbi abi, bool ownsHandle)
            {
                if (abi.Handle == 0)
                    return new {{GetGenericRecordDeclarationName(record)}}(0, checked((int)abi.Length), ownsHandle);

                if (s_instanceCache.TryGetExisting(abi.Handle, ownsHandle, out var existing) && existing is {{GetGenericRecordDeclarationName(record)}} cached)
                    return cached;

                return new {{GetGenericRecordDeclarationName(record)}}(abi.Handle, checked((int)abi.Length), ownsHandle);
            }

            internal static {{GetGenericRecordDeclarationName(record)}} CreateManagedWrapper(nint handle, bool ownsHandle)
            {
                return CreateManagedWrapper(new global::SharpCpp.Support.NativeArrayAbi(handle, 0), ownsHandle);
            }

            protected override T GetAtCore(nuint index)
            {
            {{RenderGenericArrayGetDispatch(compilation, record, instantiations)}}
                throw new global::System.NotSupportedException($"Unsupported {{record.ManagedName}} instantiation: {typeof(T).FullName}[{Length}].");
            }

            protected override void SetAtCore(nuint index, T value)
            {
            {{RenderGenericArraySetDispatch(compilation, record, instantiations)}}
                throw new global::System.NotSupportedException($"Unsupported {{record.ManagedName}} instantiation: {typeof(T).FullName}[{Length}].");
            }

            private static nint CreateNative(int length)
            {
            {{RenderGenericArrayCreateDispatch(compilation, record, instantiations)}}
                throw new global::System.NotSupportedException($"Unsupported {{record.ManagedName}} instantiation: {typeof(T).FullName}[{length}].");
            }

            protected override bool ReleaseNativeHandle(nint handle)
            {
                if (!OwnsHandle)
                {
                    return true;
                }

            {{RenderGenericArrayDeleteDispatch(compilation, record, instantiations)}}
                return true;
            }
            }

            {{RenderManagedHandleReturnMarshallerHelpers(compilation, record, GetGenericRecordDeclarationName(record))}}
            """);
        return writer.ToString();
    }

    private static string RenderGenericDynamicArrayRecord(BindingCompilation compilation, RecordBinding record)
    {
        var instantiations = GetDynamicArrayInstantiations(compilation, record).ToArray();
        var writer = new CodeWriter();
        var baseTypeName = GetGenericManagedBaseTypeName(compilation, record);
        writer.Write($$"""
            public partial class {{GetGenericRecordDeclarationName(record)}} : {{baseTypeName}}
            {
            internal {{record.ManagedName}}(nint handle, int? length, bool ownsHandle = false)
                : base(handle, length, ownsHandle)
            {
                if (!IsInvalid)
                {
                    s_instanceCache.Register(this);
                }
            }

            internal static readonly global::SharpCpp.Support.DefaultNativeInstanceCache s_instanceCache = new();

            protected override global::SharpCpp.Support.INativeInstanceCache? InstanceCache => s_instanceCache;

            public nint Handle => DangerousGetHandle();
            public bool OwnsHandle => OwnsNativeHandle;
            public bool IsNull => IsClosed || IsInvalid;

            public {{record.ManagedName}}(int length)
                : this(CreateNative(length), length, ownsHandle: true)
            {
            }

            public {{record.ManagedName}}(global::System.ReadOnlySpan<T> values)
                : this(values.Length)
            {
                Assign(values);
            }

            public {{record.ManagedName}}(global::System.Collections.Generic.IEnumerable<T> values)
                : this(Materialize(values))
            {
            }

            private static T[] Materialize(global::System.Collections.Generic.IEnumerable<T> values)
            {
                global::System.ArgumentNullException.ThrowIfNull(values);
                return global::System.Linq.Enumerable.ToArray(values);
            }

            private {{record.ManagedName}}(T[] values)
                : this(values.AsSpan())
            {
            }

            internal static {{GetGenericRecordDeclarationName(record)}} CreateManagedWrapper(nint handle, bool ownsHandle)
            {
                return CreateManagedWrapper(handle, length: null, ownsHandle: ownsHandle);
            }

            internal static {{GetGenericRecordDeclarationName(record)}} CreateManagedWrapper(nint handle, int? length, bool ownsHandle)
            {
                if (handle == 0)
                    return new {{GetGenericRecordDeclarationName(record)}}(0, length, ownsHandle);

                if (s_instanceCache.TryGetExisting(handle, ownsHandle, out var existing) && existing is {{GetGenericRecordDeclarationName(record)}} cached)
                    return cached;

                return new {{GetGenericRecordDeclarationName(record)}}(handle, length, ownsHandle);
            }

            protected override T GetAtCore(nuint index)
            {
            {{RenderGenericDynamicArrayGetDispatch(compilation, record, instantiations)}}
                throw new global::System.NotSupportedException($"Unsupported {{record.ManagedName}} instantiation: {typeof(T).FullName}.");
            }

            protected override void SetAtCore(nuint index, T value)
            {
            {{RenderGenericDynamicArraySetDispatch(compilation, record, instantiations)}}
                throw new global::System.NotSupportedException($"Unsupported {{record.ManagedName}} instantiation: {typeof(T).FullName}.");
            }

            private static nint CreateNative(int length)
            {
            {{RenderGenericDynamicArrayCreateDispatch(compilation, record, instantiations)}}
                throw new global::System.NotSupportedException($"Unsupported {{record.ManagedName}} instantiation: {typeof(T).FullName}.");
            }

            protected override bool ReleaseNativeHandle(nint handle)
            {
                if (!OwnsHandle)
                {
                    return true;
                }

            {{RenderGenericDynamicArrayDeleteDispatch(compilation, record, instantiations)}}
                return true;
            }
            }

            {{RenderManagedHandleReturnMarshallerHelpers(compilation, record, GetGenericRecordDeclarationName(record))}}
            """);
        return writer.ToString();
    }

    private static IEnumerable<string> CollectGenericArrayImportDeclarations(BindingCompilation compilation, RecordBinding record)
    {
        var declarations = IsGenericDynamicArrayRecord(record)
            ? CollectGenericDynamicArrayImportDeclarations(compilation, record)
            : CollectGenericFixedArrayImportDeclarations(compilation, record);
        return IsSharedNativeArrayRecord(record)
            ? declarations.Concat(CollectDynamicNativeArrayImportDeclarations(compilation))
            : declarations;
    }

    private static string RenderSharedNativeArrayRegistryInitializer(BindingCompilation compilation, IReadOnlyList<RecordBinding> records)
    {
        var registrations = records
            .Where(IsSharedNativeArrayRecord)
            .SelectMany(record => GetFixedArrayInstantiations(compilation, record)
                .Select(instantiation => RenderSharedNativeArrayRegistration(compilation, record, instantiation)))
            .Distinct(StringComparer.Ordinal)
            .Concat(GetPointerCountNativeArrayElementTypes(compilation)
                .Select(elementType => RenderDynamicNativeArrayRegistration(compilation, elementType)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (registrations.Length == 0)
        {
            return string.Empty;
        }

        var writer = new CodeWriter();
        writer.Write(
            """
            internal static class __NativeArrayRegistryInitializer
            {
            [global::System.Runtime.CompilerServices.ModuleInitializer]
            internal static void RegisterAll()
            {
            """);

        foreach (var registration in registrations)
        {
            writer.Write(registration);
        }

        writer.Write(
            """
            }
            }
            """);
        return writer.ToString();
    }

    private static string RenderDynamicNativeArrayRegistration(BindingCompilation compilation, BindingType elementType)
    {
        var managedElementType = GetManagedPublicReturnTypeName(elementType, compilation);
        var token = GetDynamicNativeArrayToken(elementType);
        var importAccessor = GetGenericImportAccessor(compilation);
        return $$"""
            global::SharpCpp.NativeArray<{{managedElementType}}>.__RegisterDynamic(
                static length => {{importAccessor}}.__DynamicNativeArray_Create_{{token}}(length),
                static self => {{importAccessor}}.__DynamicNativeArray_Delete_{{token}}(self),
                static (self, index) => {{importAccessor}}.__DynamicNativeArray_Get_{{token}}(self, index),
                static (self, index, value) => {{importAccessor}}.__DynamicNativeArray_Set_{{token}}(self, index, value));
            """;
    }

    private static IEnumerable<string> CollectDynamicNativeArrayImportDeclarations(BindingCompilation compilation)
    {
        foreach (var elementType in GetPointerCountNativeArrayElementTypes(compilation))
        {
            var token = GetDynamicNativeArrayToken(elementType);
            var getReturnType = GetManagedImportReturnTypeName(compilation, elementType);
            var getReturnAttribute = GetManagedImportReturnAttribute(compilation, elementType, ownsHandle: false);
            var setParameter = RenderManagedImportParameter(
                new ParameterBinding($"dynamic-array:set:{token}", "value", "value", elementType,
                    BindingParameterDirection.In, BindingParameterAbiKind.Value, elementType.CppTypeName), compilation);
            yield return $$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetDynamicNativeArrayEntryPoint(compilation, token, "create")}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static partial global::SharpCpp.Support.NativeArrayAbi __DynamicNativeArray_Create_{{token}}(nuint length);
                """;
            yield return $$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetDynamicNativeArrayEntryPoint(compilation, token, "delete")}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static partial void __DynamicNativeArray_Delete_{{token}}(global::SharpCpp.Support.NativeArrayAbi self);
                """;
            yield return $$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetDynamicNativeArrayEntryPoint(compilation, token, "get")}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                {{getReturnAttribute}}
                internal static partial {{getReturnType}} __DynamicNativeArray_Get_{{token}}(global::SharpCpp.Support.NativeArrayAbi self, nuint index);
                """;
            yield return $$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetDynamicNativeArrayEntryPoint(compilation, token, "set")}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static partial void __DynamicNativeArray_Set_{{token}}(global::SharpCpp.Support.NativeArrayAbi self, nuint index, {{setParameter}});
                """;
        }
    }

    private static IReadOnlyList<BindingType> GetPointerCountNativeArrayElementTypes(BindingCompilation compilation)
    {
        var result = new Dictionary<string, BindingType>(StringComparer.Ordinal);
        foreach (var owner in compilation.Records.Where(static record => !record.IsIgnored()))
        {
            var fields = owner.Members.OfType<FieldBinding>().Where(static field => !field.IsIgnored()).ToArray();
            var fieldNames = fields.Select(static field => field.NativeName).ToHashSet(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                var elementType = field.Type;
                if (elementType.Kind != BindingTypeKind.Handle || elementType.RecordId is null ||
                    !elementType.CppTypeName.TrimEnd().EndsWith('*'))
                    continue;
                if (!HasMatchingCountField(field.NativeName, fieldNames))
                    continue;

                var elementRecord = compilation.FindRecordBySymbolId(elementType.RecordId);
                if (elementRecord is null || elementRecord.IsIgnored() || elementRecord.IsGeneric ||
                    elementRecord.IsAbstract || elementRecord.IsPolymorphic)
                    continue;
                var arrayElementType = BindingType.CreateHandle(
                    elementRecord.CppQualifiedName,
                    elementType.ManagedTypeName,
                    elementRecord.SymbolId,
                    elementRecord.HandleMarshallerTypeName,
                    cppIsRecordValue: true);
                result.TryAdd(elementType.RecordId, arrayElementType);
            }
        }
        return result.Values.OrderBy(static type => type.CppTypeName, StringComparer.Ordinal).ToArray();
    }

    private static bool HasMatchingCountField(string pointerFieldName, IReadOnlySet<string> fieldNames)
    {
        if (fieldNames.Contains($"Num{pointerFieldName}"))
            return true;
        for (var i = pointerFieldName.Length - 1; i > 0; --i)
        {
            if (!char.IsUpper(pointerFieldName[i]))
                continue;
            return fieldNames.Contains($"Num{pointerFieldName[i..]}");
        }
        return false;
    }

    private static string GetDynamicNativeArrayToken(BindingType elementType) =>
        SanitizeManagedToken(elementType.RecordId ?? elementType.ManagedTypeName);

    private static string GetDynamicNativeArrayEntryPoint(
        BindingCompilation compilation, string token, string operation) =>
        $"{compilation.EntryPointPrefix}dynamic_native_array_{token}_{operation}";

    private static string RenderSharedNativeArrayRegistration(BindingCompilation compilation, RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        _ = TryGetFixedArrayLength(instantiation, out var length);
        var elementType = instantiation.TypeArguments[0];
        var managedElementType = GetManagedPublicReturnTypeName(elementType, compilation);
        var importToken = GetFixedArrayImportMethodToken(record, instantiation);
        var importAccessor = GetGenericImportAccessor(compilation);
        return $$"""
            global::SharpCpp.NativeArray<{{managedElementType}}>.__Register(
                {{length}},
                static () => {{importAccessor}}.__Array_Create_{{importToken}}(),
                static self => {{importAccessor}}.__Array_Delete_{{importToken}}(self),
                static (self, index) => {{importAccessor}}.__Array_Get_{{importToken}}(self, index),
                static (self, index, value) => {{importAccessor}}.__Array_Set_{{importToken}}(self, index, value));
            """;
    }

    private static IEnumerable<string> CollectGenericFixedArrayImportDeclarations(BindingCompilation compilation, RecordBinding record)
    {
        foreach (var instantiation in GetFixedArrayInstantiations(compilation, record))
        {
            var importToken = GetFixedArrayImportMethodToken(record, instantiation);
            var elementType = instantiation.TypeArguments[0];
            var getReturnType = IsSharedNativeArrayRecord(record)
                ? GetManagedImportReturnTypeName(compilation, elementType)
                : elementType.Kind == BindingTypeKind.Handle
                    ? "nint"
                    : FormatGlobalTypeName(elementType, compilation);
            var getReturnAttribute = IsSharedNativeArrayRecord(record)
                ? GetManagedImportReturnAttribute(compilation, elementType, ownsHandle: false)
                : GetManagedReturnAttribute(elementType);
            var setParameter = IsSharedNativeArrayRecord(record)
                ? RenderManagedImportParameter(
                    new ParameterBinding(
                        $"array:set:{importToken}",
                        "value",
                        "value",
                        elementType,
                        BindingParameterDirection.In,
                        BindingParameterAbiKind.Value,
                        elementType.CppTypeName),
                    compilation)
                : $"{GetManagedParameterAttribute(elementType, compilation)}{(elementType.Kind == BindingTypeKind.Handle ? "nint" : FormatGlobalTypeName(elementType, compilation))} value";
            yield return $$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetFixedArrayCreateEntryPointName(compilation, record, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static partial global::SharpCpp.Support.NativeArrayAbi __Array_Create_{{importToken}}();
                """;
            yield return $$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetFixedArrayDeleteEntryPointName(compilation, record, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static partial void __Array_Delete_{{importToken}}(global::SharpCpp.Support.NativeArrayAbi self);
                """;
            yield return $$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetFixedArrayGetEntryPointName(compilation, record, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                {{getReturnAttribute}}
                internal static partial {{getReturnType}} __Array_Get_{{importToken}}(global::SharpCpp.Support.NativeArrayAbi self, nuint index);
                """;
            yield return $$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetFixedArraySetEntryPointName(compilation, record, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static partial void __Array_Set_{{importToken}}(global::SharpCpp.Support.NativeArrayAbi self, nuint index, {{setParameter}});
                """;
        }
    }

    private static IEnumerable<string> CollectGenericDynamicArrayImportDeclarations(BindingCompilation compilation, RecordBinding record)
    {
        foreach (var instantiation in GetDynamicArrayInstantiations(compilation, record))
        {
            var token = GetDynamicArrayInstantiationToken(instantiation);
            var elementType = instantiation.TypeArguments[0];
            var managedElementType = FormatGlobalTypeName(elementType, compilation);
            var getReturnType = elementType.Kind == BindingTypeKind.Handle ? "nint" : managedElementType;
            var setParameterType = elementType.Kind == BindingTypeKind.Handle ? "nint" : managedElementType;
            yield return $$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetDynamicArrayCreateEntryPointName(compilation, record, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static partial nint __Array_Create_{{token}}(nuint length);
                """;
            yield return $$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetDynamicArrayDeleteEntryPointName(compilation, record, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static partial void __Array_Delete_{{token}}(nint self);
                """;
            yield return $$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetDynamicArrayGetEntryPointName(compilation, record, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static partial {{getReturnType}} __Array_Get_{{token}}(nint self, nuint index);
                """;
            yield return $$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetDynamicArraySetEntryPointName(compilation, record, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static partial void __Array_Set_{{token}}(nint self, nuint index, {{setParameterType}} value);
                """;
        }
    }

    private static void ValidateGenericRecordSlice(RecordBinding record)
    {
        if (record.IsDirector || record.IsPolymorphic || record.Bases.Count > 0)
        {
            throw new NotSupportedException($"Generic record '{record.ManagedName}' is limited to non-polymorphic, non-director, non-inheriting surface in this slice.");
        }

        if (record.Members.OfType<FieldBinding>().Any())
        {
            throw new NotSupportedException($"Generic record '{record.ManagedName}' does not support field projection in this slice.");
        }

        if (record.Members.OfType<FunctionBinding>().Any(static function => function.OperatorToken is not null || function.IsConversionOperator))
        {
            throw new NotSupportedException($"Generic record '{record.ManagedName}' does not support operators in this slice.");
        }

        foreach (var instantiation in record.SupportedGenericInstantiations)
        {
            if (instantiation.TypeArguments.Any(argument => argument.Kind is not (BindingTypeKind.Primitive or BindingTypeKind.Enum or BindingTypeKind.Handle)))
            {
                throw new NotSupportedException($"Generic record '{record.ManagedName}' currently supports only primitive/enum/handle concrete type arguments.");
            }
        }
    }

    private static string GetGenericParameterList(RecordBinding record)
    {
        var visibleParameters = GetManagedVisibleGenericParameters(record);
        return visibleParameters.Count == 0
            ? string.Empty
            : $"<{string.Join(", ", visibleParameters.Select(static parameter => parameter.Name))}>";
    }

    private static string GetGenericRecordDeclarationName(RecordBinding record)
    {
        return $"{record.ManagedName}{GetGenericParameterList(record)}";
    }

    private static string GetGenericManagedBaseTypeName(BindingCompilation compilation, RecordBinding record)
    {
        if (string.IsNullOrWhiteSpace(record.ManagedBaseTypeName))
        {
            return "global::SharpCpp.Support.NativeObject";
        }

        if (!record.ManagedBaseTypeName.Contains('{', StringComparison.Ordinal))
        {
            return QualifyRecordTypeName(compilation, record.ManagedBaseTypeName);
        }

        var typeArguments = record.GenericParameters.Select(static parameter => parameter.Name).Cast<object>().ToArray();
        var formattedBaseTypeName = string.Format(System.Globalization.CultureInfo.InvariantCulture, record.ManagedBaseTypeName, typeArguments);
        return QualifyRecordTypeName(compilation, formattedBaseTypeName);
    }

    private static string GetGenericRecordConstraints(RecordBinding record)
    {
        if (string.IsNullOrWhiteSpace(record.ManagedBaseTypeConstraints) || record.GenericParameters.Count == 0)
        {
            return string.Empty;
        }

        var typeArguments = record.GenericParameters.Select(static parameter => parameter.Name).Cast<object>().ToArray();
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, record.ManagedBaseTypeConstraints, typeArguments);
    }

    private static IEnumerable<RecordGenericInstantiationBinding> GetFixedArrayInstantiations(BindingCompilation compilation, RecordBinding record)
    {
        var instantiations = record.SupportedGenericInstantiations
            .Where(instantiation => IsSupportedFixedArrayInstantiation(compilation, instantiation))
            .Concat(CollectFixedArrayInstantiations(compilation, record))
            .Where(instantiation => IsSupportedFixedArrayInstantiation(compilation, instantiation))
            .GroupBy(static instantiation => $"{instantiation.TypeArguments[0].CppTypeName}|{instantiation.TypeArguments[1].CppTypeName}", StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static instantiation => instantiation.TypeArguments[0].CppTypeName, StringComparer.Ordinal)
            .ThenBy(static instantiation => int.Parse(instantiation.TypeArguments[1].CppTypeName, System.Globalization.CultureInfo.InvariantCulture));

        return instantiations;
    }

    private static IEnumerable<RecordGenericInstantiationBinding> GetDynamicArrayInstantiations(BindingCompilation compilation, RecordBinding record)
    {
        var instantiations = record.SupportedGenericInstantiations
            .Where(instantiation => IsSupportedDynamicArrayInstantiation(compilation, instantiation))
            .Concat(CollectDynamicArrayInstantiations(compilation, record))
            .Where(instantiation => IsSupportedDynamicArrayInstantiation(compilation, instantiation))
            .GroupBy(static instantiation => instantiation.TypeArguments[0].CppTypeName, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static instantiation => instantiation.TypeArguments[0].CppTypeName, StringComparer.Ordinal);

        return instantiations;
    }

    private static bool IsSupportedFixedArrayInstantiation(BindingCompilation compilation, RecordGenericInstantiationBinding instantiation)
    {
        return instantiation.TypeArguments.Count >= 2 &&
            TryGetFixedArrayLength(instantiation, out _) &&
            IsSupportedFixedArrayElementType(compilation, instantiation.TypeArguments[0]);
    }

    private static bool IsSupportedDynamicArrayInstantiation(BindingCompilation compilation, RecordGenericInstantiationBinding instantiation)
    {
        return instantiation.TypeArguments.Count >= 1 &&
            IsSupportedFixedArrayElementType(compilation, instantiation.TypeArguments[0]);
    }

    private static bool IsSupportedFixedArrayElementType(BindingCompilation compilation, BindingType type)
    {
        if (type.ContainsGenericParameters)
        {
            return false;
        }

        if (type.HasUnresolvedManagedNamespaceSeparators)
        {
            return false;
        }

        return type.Kind switch
        {
            BindingTypeKind.Primitive => true,
            BindingTypeKind.Enum => IsSupportedFixedArrayEnumElementType(compilation, type),
            BindingTypeKind.Handle => IsSupportedFixedArrayHandleElementType(compilation, type),
            _ => false
        };
    }

    private static bool IsSupportedFixedArrayEnumElementType(BindingCompilation compilation, BindingType type)
    {
        var enumBinding = compilation.FindEnumBySymbolId(type.EnumId);
        return enumBinding is not null && !enumBinding.IsIgnored();
    }

    private static bool IsSupportedFixedArrayHandleElementType(BindingCompilation compilation, BindingType type)
    {
        var record = compilation.FindRecordBySymbolId(type.RecordId);
        if (record?.IsIgnored() == true)
        {
            return false;
        }

        if (record?.GenericParameters.Count > 0 && type.GenericArguments.Count == 0)
        {
            return false;
        }

        return type.GenericArguments.All(argument => IsSupportedFixedArrayElementType(compilation, argument));
    }

    private static IEnumerable<RecordGenericInstantiationBinding> CollectFixedArrayInstantiations(BindingCompilation compilation, RecordBinding record)
    {
        foreach (var function in compilation.Functions.Where(static function => !function.IsIgnored() && function.Parent?.IsIgnored() != true))
        {
            foreach (var instantiation in CollectFixedArrayInstantiations(record, function.ReturnType))
            {
                yield return instantiation;
            }

            foreach (var parameter in function.Parameters)
            {
                foreach (var instantiation in CollectFixedArrayInstantiations(record, parameter.Type))
                {
                    yield return instantiation;
                }
            }
        }

        foreach (var field in compilation.Descendants<FieldBinding>().Where(static field => !field.IsIgnored() && field.Parent?.IsIgnored() != true))
        {
            foreach (var instantiation in CollectFixedArrayInstantiations(record, field.Type))
            {
                yield return instantiation;
            }
        }

        foreach (var alias in compilation.TypeAliases)
        {
            foreach (var instantiation in CollectFixedArrayInstantiations(record, alias.TargetType))
            {
                yield return instantiation;
            }
        }
    }

    private static IEnumerable<RecordGenericInstantiationBinding> CollectDynamicArrayInstantiations(BindingCompilation compilation, RecordBinding record)
    {
        foreach (var function in compilation.Functions.Where(static function => !function.IsIgnored() && function.Parent?.IsIgnored() != true))
        {
            foreach (var instantiation in CollectDynamicArrayInstantiations(record, function.ReturnType))
            {
                yield return instantiation;
            }

            foreach (var parameter in function.Parameters)
            {
                foreach (var instantiation in CollectDynamicArrayInstantiations(record, parameter.Type))
                {
                    yield return instantiation;
                }
            }
        }

        foreach (var field in compilation.Descendants<FieldBinding>().Where(static field => !field.IsIgnored() && field.Parent?.IsIgnored() != true))
        {
            foreach (var instantiation in CollectDynamicArrayInstantiations(record, field.Type))
            {
                yield return instantiation;
            }
        }

        foreach (var alias in compilation.TypeAliases)
        {
            foreach (var instantiation in CollectDynamicArrayInstantiations(record, alias.TargetType))
            {
                yield return instantiation;
            }
        }
    }

    private static IEnumerable<RecordGenericInstantiationBinding> CollectFixedArrayInstantiations(RecordBinding record, BindingType type)
    {
        foreach (var argument in type.GenericArguments)
        {
            foreach (var instantiation in CollectFixedArrayInstantiations(record, argument))
            {
                yield return instantiation;
            }
        }

        if (type.IsCallback)
        {
            if (type.CallbackReturnType is not null)
            {
                foreach (var instantiation in CollectFixedArrayInstantiations(record, type.CallbackReturnType))
                {
                    yield return instantiation;
                }
            }

            foreach (var parameterType in type.CallbackParameterTypes)
            {
                foreach (var instantiation in CollectFixedArrayInstantiations(record, parameterType))
                {
                    yield return instantiation;
                }
            }
        }

        if (type.Kind != BindingTypeKind.Handle ||
            !string.Equals(type.RecordId, record.SymbolId, StringComparison.Ordinal) ||
            type.GenericArguments.Count == 0 ||
            !TryGetSecondTopLevelTemplateArgument(type.CppTypeName, out var lengthText) ||
            !int.TryParse(lengthText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var length) ||
            length < 0)
        {
            yield break;
        }

        var arguments = type.GenericArguments.Count >= 2
            ? type.GenericArguments
            : type.GenericArguments.Concat([BindingType.CreateConfigured(lengthText, lengthText)]).ToArray();
        yield return new RecordGenericInstantiationBinding(arguments);
    }

    private static IEnumerable<RecordGenericInstantiationBinding> CollectDynamicArrayInstantiations(RecordBinding record, BindingType type)
    {
        foreach (var argument in type.GenericArguments)
        {
            foreach (var instantiation in CollectDynamicArrayInstantiations(record, argument))
            {
                yield return instantiation;
            }
        }

        if (type.IsCallback)
        {
            if (type.CallbackReturnType is not null)
            {
                foreach (var instantiation in CollectDynamicArrayInstantiations(record, type.CallbackReturnType))
                {
                    yield return instantiation;
                }
            }

            foreach (var parameterType in type.CallbackParameterTypes)
            {
                foreach (var instantiation in CollectDynamicArrayInstantiations(record, parameterType))
                {
                    yield return instantiation;
                }
            }
        }

        if (type.Kind != BindingTypeKind.Handle ||
            !string.Equals(type.RecordId, record.SymbolId, StringComparison.Ordinal) ||
            type.GenericArguments.Count == 0)
        {
            yield break;
        }

        var elementType = type.DynamicArrayElementType ?? type.GenericArguments[0];
        yield return new RecordGenericInstantiationBinding([elementType]);
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

    private static bool TryGetFixedArrayLength(RecordGenericInstantiationBinding instantiation, out int length)
    {
        if (instantiation.TypeArguments.Count >= 2 &&
            int.TryParse(instantiation.TypeArguments[1].CppTypeName, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out length) &&
            length >= 0)
        {
            return true;
        }

        length = 0;
        return false;
    }

    private static string GetFixedArrayInstantiationToken(RecordGenericInstantiationBinding instantiation)
    {
        _ = TryGetFixedArrayLength(instantiation, out var length);
        var elementType = instantiation.TypeArguments[0];
        var elementToken = SanitizeManagedToken(elementType.ManagedTypeName);
        while (elementType.IsFixedArray)
        {
            elementToken = $"{elementToken}_{elementType.FixedArrayLength}";
            elementType = elementType.FixedArrayElementType!;
        }
        return $"{elementToken}_{length}";
    }

    private static string GetFixedArrayImportMethodToken(RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $"{SanitizeManagedToken(record.ManagedName)}_{GetFixedArrayInstantiationToken(instantiation)}";
    }

    private static string GetDynamicArrayInstantiationToken(RecordGenericInstantiationBinding instantiation)
    {
        return SanitizeManagedToken(instantiation.TypeArguments[0].ManagedTypeName);
    }

    private static string GetFixedArrayCreateEntryPointName(BindingCompilation compilation, RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $"{compilation.EntryPointPrefix}_{SanitizeNativeEntrypointName(record.CppQualifiedName)}_array_create_{GetFixedArrayInstantiationToken(instantiation)}";
    }

    private static string GetFixedArrayDeleteEntryPointName(BindingCompilation compilation, RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $"{compilation.EntryPointPrefix}_{SanitizeNativeEntrypointName(record.CppQualifiedName)}_array_delete_{GetFixedArrayInstantiationToken(instantiation)}";
    }

    private static string GetFixedArrayGetEntryPointName(BindingCompilation compilation, RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $"{compilation.EntryPointPrefix}_{SanitizeNativeEntrypointName(record.CppQualifiedName)}_array_get_{GetFixedArrayInstantiationToken(instantiation)}";
    }

    private static string GetFixedArraySetEntryPointName(BindingCompilation compilation, RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $"{compilation.EntryPointPrefix}_{SanitizeNativeEntrypointName(record.CppQualifiedName)}_array_set_{GetFixedArrayInstantiationToken(instantiation)}";
    }

    private static string GetDynamicArrayCreateEntryPointName(BindingCompilation compilation, RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $"{compilation.EntryPointPrefix}_{SanitizeNativeEntrypointName(record.CppQualifiedName)}_dynamic_array_create_{GetDynamicArrayInstantiationToken(instantiation)}";
    }

    private static string GetDynamicArrayDeleteEntryPointName(BindingCompilation compilation, RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $"{compilation.EntryPointPrefix}_{SanitizeNativeEntrypointName(record.CppQualifiedName)}_dynamic_array_delete_{GetDynamicArrayInstantiationToken(instantiation)}";
    }

    private static string GetDynamicArrayGetEntryPointName(BindingCompilation compilation, RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $"{compilation.EntryPointPrefix}_{SanitizeNativeEntrypointName(record.CppQualifiedName)}_dynamic_array_get_{GetDynamicArrayInstantiationToken(instantiation)}";
    }

    private static string GetDynamicArraySetEntryPointName(BindingCompilation compilation, RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $"{compilation.EntryPointPrefix}_{SanitizeNativeEntrypointName(record.CppQualifiedName)}_dynamic_array_set_{GetDynamicArrayInstantiationToken(instantiation)}";
    }

    private static string RenderGenericArrayCreateDispatch(BindingCompilation compilation, RecordBinding record, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        foreach (var instantiation in instantiations)
        {
            _ = TryGetFixedArrayLength(instantiation, out var length);
            var elementType = FormatGlobalTypeName(instantiation.TypeArguments[0], compilation);
            writer.Write($$"""
                if (typeof(T) == typeof({{elementType}}) && length == {{length}})
                {
                    return {{GetGenericImportAccessor(compilation)}}.__Array_Create_{{GetFixedArrayImportMethodToken(record, instantiation)}}().Handle;
                }
                """);
        }

        return writer.ToString();
    }

    private static string RenderNativeDynamicNativeArray(BindingCompilation compilation, BindingType elementType)
    {
        var elementCppType = GetNativeFixedArrayElementCppType(compilation, elementType);
        var token = GetDynamicNativeArrayToken(elementType);
        return $$"""
            SHARPCPP_EXPORT NativeArrayAbi {{GetDynamicNativeArrayEntryPoint(compilation, token, "create")}}(arg_t<std::size_t> length)
            {
                auto* value = length != 0 ? new {{elementCppType}}[length]{} : nullptr;
                return { value, length };
            }

            SHARPCPP_EXPORT ret_t<void> {{GetDynamicNativeArrayEntryPoint(compilation, token, "delete")}}(NativeArrayAbi self)
            {
                delete[] static_cast<{{elementCppType}}*>(self.handle);
            }

            SHARPCPP_EXPORT ret_t<{{elementCppType}}> {{GetDynamicNativeArrayEntryPoint(compilation, token, "get")}}(NativeArrayAbi self, arg_t<std::size_t> index)
            {
                if (self.handle == nullptr || index >= self.length)
                    throw std::runtime_error("SharpCpp native array access out of range.");
                ReturnMarshaller<{{elementCppType}}> marshaller;
                return marshaller.ConvertToAbi(static_cast<{{elementCppType}}*>(self.handle)[index]);
            }

            SHARPCPP_EXPORT ret_t<void> {{GetDynamicNativeArrayEntryPoint(compilation, token, "set")}}(NativeArrayAbi self, arg_t<std::size_t> index, arg_t<{{elementCppType}}> value)
            {
                if (self.handle == nullptr || index >= self.length)
                    throw std::runtime_error("SharpCpp native array access out of range.");
                ParameterMarshaller<{{elementCppType}}> marshaller;
                static_cast<{{elementCppType}}*>(self.handle)[index] = marshaller.ConvertToCpp(value);
            }

            """;
    }

    private static string RenderGenericArrayDeleteDispatch(BindingCompilation compilation, RecordBinding record, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        foreach (var instantiation in instantiations)
        {
            _ = TryGetFixedArrayLength(instantiation, out var length);
            var elementType = FormatGlobalTypeName(instantiation.TypeArguments[0], compilation);
            writer.Write($$"""
                if (typeof(T) == typeof({{elementType}}) && Length == {{length}})
                {
                    {{GetGenericImportAccessor(compilation)}}.__Array_Delete_{{GetFixedArrayImportMethodToken(record, instantiation)}}(new global::SharpCpp.Support.NativeArrayAbi(handle, (nuint)Length));
                    return true;
                }
                """);
        }

        return writer.ToString();
    }

    private static string RenderGenericArrayGetDispatch(BindingCompilation compilation, RecordBinding record, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        foreach (var instantiation in instantiations)
        {
            _ = TryGetFixedArrayLength(instantiation, out var length);
            var element = instantiation.TypeArguments[0];
            var elementType = FormatGlobalTypeName(element, compilation);
            var resultExpression = $"{GetGenericImportAccessor(compilation)}.__Array_Get_{GetFixedArrayImportMethodToken(record, instantiation)}(ToNativeArrayAbi(), index)";
            if (element.Kind == BindingTypeKind.Handle)
            {
                resultExpression = $"{elementType}.CreateManagedWrapper({resultExpression}, ownsHandle: false)";
            }
            else if (element.Marshaller.CppIsRecordValue)
            {
                resultExpression = $"{elementType}.CreateManagedWrapper({resultExpression}, ownsHandle: true)";
            }

            writer.Write($$"""
                if (typeof(T) == typeof({{elementType}}) && Length == {{length}})
                {
                    return (T)(object){{resultExpression}};
                }
                """);
        }

        return writer.ToString();
    }

    private static string RenderGenericArraySetDispatch(BindingCompilation compilation, RecordBinding record, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        foreach (var instantiation in instantiations)
        {
            _ = TryGetFixedArrayLength(instantiation, out var length);
            var element = instantiation.TypeArguments[0];
            var elementType = FormatGlobalTypeName(element, compilation);
            var valueExpression = element.Kind == BindingTypeKind.Handle
                ? "global::SharpCpp.Support.NativeObject.GetHandle(value)"
                : $"({elementType})(object)value!";
            var keepAlive = element.Kind == BindingTypeKind.Handle ? "\n            global::System.GC.KeepAlive(value);" : string.Empty;
            writer.Write($$"""
                if (typeof(T) == typeof({{elementType}}) && Length == {{length}})
                {
                    {{GetGenericImportAccessor(compilation)}}.__Array_Set_{{GetFixedArrayImportMethodToken(record, instantiation)}}(ToNativeArrayAbi(), index, {{valueExpression}});{{keepAlive}}
                    return;
                }
                """);
        }

        return writer.ToString();
    }

    private static string RenderGenericDynamicArrayCreateDispatch(BindingCompilation compilation, RecordBinding record, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        foreach (var instantiation in instantiations)
        {
            var elementType = FormatGlobalTypeName(instantiation.TypeArguments[0], compilation);
            writer.Write($$"""
                if (typeof(T) == typeof({{elementType}}))
                {
                    return {{GetGenericImportAccessor(compilation)}}.__Array_Create_{{GetDynamicArrayInstantiationToken(instantiation)}}(checked((nuint)length));
                }
                """);
        }

        return writer.ToString();
    }

    private static string RenderGenericDynamicArrayDeleteDispatch(BindingCompilation compilation, RecordBinding record, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        foreach (var instantiation in instantiations)
        {
            var elementType = FormatGlobalTypeName(instantiation.TypeArguments[0], compilation);
            writer.Write($$"""
                if (typeof(T) == typeof({{elementType}}))
                {
                    {{GetGenericImportAccessor(compilation)}}.__Array_Delete_{{GetDynamicArrayInstantiationToken(instantiation)}}(handle);
                    return true;
                }
                """);
        }

        return writer.ToString();
    }

    private static string RenderGenericDynamicArrayGetDispatch(BindingCompilation compilation, RecordBinding record, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        foreach (var instantiation in instantiations)
        {
            var element = instantiation.TypeArguments[0];
            var elementType = FormatGlobalTypeName(element, compilation);
            var resultExpression = $"{GetGenericImportAccessor(compilation)}.__Array_Get_{GetDynamicArrayInstantiationToken(instantiation)}(DangerousGetHandle(), index)";
            if (element.Kind == BindingTypeKind.Handle)
            {
                resultExpression = $"{elementType}.CreateManagedWrapper({resultExpression}, ownsHandle: false)";
            }
            else if (element.Marshaller.CppIsRecordValue)
            {
                resultExpression = $"{elementType}.CreateManagedWrapper({resultExpression}, ownsHandle: true)";
            }

            writer.Write($$"""
                if (typeof(T) == typeof({{elementType}}))
                {
                    return (T)(object){{resultExpression}};
                }
                """);
        }

        return writer.ToString();
    }

    private static string RenderGenericDynamicArraySetDispatch(BindingCompilation compilation, RecordBinding record, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        foreach (var instantiation in instantiations)
        {
            var element = instantiation.TypeArguments[0];
            var elementType = FormatGlobalTypeName(element, compilation);
            var valueExpression = element.Kind == BindingTypeKind.Handle
                ? "global::SharpCpp.Support.NativeObject.GetHandle(value)"
                : $"({elementType})(object)value!";
            var keepAlive = element.Kind == BindingTypeKind.Handle ? "\n            global::System.GC.KeepAlive(value);" : string.Empty;
            writer.Write($$"""
                if (typeof(T) == typeof({{elementType}}))
                {
                    {{GetGenericImportAccessor(compilation)}}.__Array_Set_{{GetDynamicArrayInstantiationToken(instantiation)}}(DangerousGetHandle(), index, {{valueExpression}});{{keepAlive}}
                    return;
                }
                """);
        }

        return writer.ToString();
    }

    private static string GetGenericImportAccessor(BindingCompilation compilation)
    {
        return $"global::{compilation.ManagedNamespace}.{compilation.ManagedClassName}.__PInvoke";
    }

    private static string GetGenericImportAccessor(BindingCompilation compilation, FunctionBinding function)
    {
        return $"global::{GetManagedNamespace(compilation, function)}.{GetManagedClassName(compilation, function)}.__PInvoke";
    }

    private static string RenderGenericManagedConstructor(BindingCompilation compilation, RecordBinding record, FunctionBinding function, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        WriteDocumentationComment(writer, function);
        var parameterList = string.Join(", ", function.Parameters.Select(p => RenderManagedPublicParameter(p, compilation)));

        writer.Write($$"""
            public {{record.ManagedName}}({{parameterList}})
                : this({{GetGenericConstructorFactoryName(function)}}({{string.Join(", ", function.Parameters.Select(static parameter => parameter.ManagedName))}}), true)
            {
            global::SharpCpp.Support.NativeObjectRegistry.RegisterInstance<{{GetGenericRecordDeclarationName(record)}}>(this);
            }

            private static nint {{GetGenericConstructorFactoryName(function)}}({{parameterList}})
            {
            {{RenderGenericManagedDispatchBranches(compilation, record, function, instantiations, includeSelf: false)}}
            throw new NotSupportedException("SharpCpp does not support the requested generic argument combination for '{{record.ManagedName}}'.");
            }
            """);
        return writer.ToString();
    }

    private static string RenderGenericManagedInstanceMethod(BindingCompilation compilation, RecordBinding record, FunctionBinding function, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        WriteDocumentationComment(writer, function);
        var parameterList = string.Join(", ", function.Parameters.Select(p => RenderManagedPublicParameter(p, compilation)));
        var returnTypeName = GetManagedPublicReturnTypeName(function.ReturnType, compilation);
        var visibility = GetManagedVisibilityNormalized(compilation, record, function);

        writer.Write($$"""
            {{visibility}} {{GetManagedInstanceMethodModifier(compilation, record, function)}}{{returnTypeName}} {{function.ManagedName}}({{parameterList}})
            {
            {{RenderGenericManagedDispatchBranches(compilation, record, function, instantiations, includeSelf: true)}}
            throw new NotSupportedException("SharpCpp does not support the requested generic argument combination for '{{function.ManagedName}}'.");
            }
            """);
        return writer.ToString();
    }

    private static string RenderGenericManagedStaticMethod(BindingCompilation compilation, RecordBinding record, FunctionBinding function, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        WriteDocumentationComment(writer, function);
        var parameterList = string.Join(", ", function.Parameters.Select(p => RenderManagedPublicParameter(p, compilation)));
        var returnTypeName = GetManagedPublicReturnTypeName(function.ReturnType, compilation);

        writer.Write($$"""
            public static {{returnTypeName}} {{function.ManagedName}}({{parameterList}})
            {
            {{RenderGenericManagedDispatchBranches(compilation, record, function, instantiations, includeSelf: false)}}
            throw new NotSupportedException("SharpCpp does not support the requested generic argument combination for '{{function.ManagedName}}'.");
            }
            """);
        return writer.ToString();
    }

    private static string RenderGenericManagedCloneMethod(BindingCompilation compilation, RecordBinding record, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        var declarationName = GetGenericRecordDeclarationName(record);

        if (!record.HasOwnManagedNonFieldMemberNamed("Clone"))
        {
            var hidesBaseClone = !string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) && !record.ManagedBaseTypeName.Contains('.', StringComparison.Ordinal);
            writer.Write($$"""
                public {{(hidesBaseClone ? "new " : string.Empty)}}{{declarationName}} Clone()
                {
                {{RenderGenericManagedCloneDispatchBranches(compilation, record, instantiations)}}
                throw new NotSupportedException("SharpCpp does not support the requested generic argument combination for clone on '{{record.ManagedName}}'.");
                }

                object global::System.ICloneable.Clone()
                {
                return Clone();
                }
                """);
        }
        else
        {
            writer.Write($$"""
                object global::System.ICloneable.Clone()
                {
                {{RenderGenericManagedCloneDispatchBranches(compilation, record, instantiations)}}
                throw new NotSupportedException("SharpCpp does not support the requested generic argument combination for clone on '{{record.ManagedName}}'.");
                }
                """);
        }

        return writer.ToString();
    }

    private static string RenderGenericManagedCloneDispatchBranches(BindingCompilation compilation, RecordBinding record, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        foreach (var instantiation in instantiations)
        {
            writer.WriteLine($"if ({BuildGenericInstantiationCondition(compilation, record, instantiation)})");
            writer.WriteLine("{");
            writer.WriteLine($"    var __result = {GetGenericImportAccessor(compilation)}.{GetGenericCloneImportMethodName(record, instantiation)}(global::SharpCpp.Support.NativeObject.GetHandle(this));");
            writer.WriteLine("    if (__result is null)");
            writer.WriteLine("        throw new NotSupportedException(\"SharpCpp could not clone this native object because the native type is not copy-constructible.\");");
            writer.WriteLine("    return __result;");
            writer.WriteLine("}");
        }

        return writer.ToString();
    }

    private static string RenderGenericManagedGenericMethod(BindingCompilation compilation, RecordBinding record, FunctionBinding function)
    {
        var writer = new CodeWriter();
        WriteDocumentationComment(writer, function);
        var methodTypeParams = string.Join(", ", function.GenericParameters.Select(static param => param.Name));
        var returnTypeName = GetGenericManagedReturnType(function.ReturnType, compilation);
        var parameterList = RenderManagedPublicParameterList(function.Parameters, compilation);
        var methodInstantiations = function.SupportedGenericInstantiations.ToArray();
        var recordInstantiations = GetMethodRecordInstantiations(compilation, record);

        if (methodInstantiations.Length == 0 || recordInstantiations.Count == 0)
        {
            writer.Write($$"""
                public {{returnTypeName}} {{function.ManagedName}}<{{methodTypeParams}}>({{parameterList}})
                {
                throw new NotSupportedException("SharpCpp does not yet support generic methods inside generic records for type argument combination <{{methodTypeParams}}>.");
                }
                """);
            return writer.ToString();
        }

        writer.Write($$"""
            public {{returnTypeName}} {{function.ManagedName}}<{{methodTypeParams}}>({{parameterList}})
            {
            """);

        var methodParams = (IReadOnlyList<BindingGenericParameter>)function.GenericParameters.ToList();
        foreach (var recordInstantiation in recordInstantiations)
        {
            foreach (var methodInstantiation in methodInstantiations)
            {
                var concreteParameters = function.Parameters.Select(parameter =>
                    SubstituteGenericType(record, parameter.Type, recordInstantiation, methodParams, methodInstantiation)).ToArray();
                var concreteReturnType = SubstituteGenericType(record, function.ReturnType, recordInstantiation, methodParams, methodInstantiation);
                var concreteRecordType = record.FormatCppTypeName(recordInstantiation.TypeArguments);

                var condition = BuildCombinedGenericInstantiationCondition(compilation, record, recordInstantiation, function, methodInstantiation);
                var importMethodName = GetGenericMethodImportName(function, recordInstantiation, methodInstantiation);

                var invocationArguments = new List<string> { "global::SharpCpp.Support.NativeObject.GetHandle(this)" };
                for (var index = 0; index < function.Parameters.Count; index++)
                {
                    invocationArguments.Add(BuildGenericInvocationArgument(compilation, function.Parameters[index], concreteParameters[index]));
                }

                WriteGenericManagedMethodDispatchBranch(
                    writer,
                    compilation,
                    function,
                    returnTypeName,
                    concreteReturnType,
                    concreteParameters,
                    condition,
                    importMethodName,
                    invocationArguments,
                    indent: "    ");
            }
        }

        writer.Write($$"""
            throw new NotSupportedException("SharpCpp does not support the requested generic argument combination for '{{function.ManagedName}}'.");
            }
            """);
        return writer.ToString();
    }

    private static void WriteGenericManagedMethodDispatchBranch(
        CodeWriter writer,
        BindingCompilation compilation,
        FunctionBinding function,
        string returnTypeName,
        BindingType concreteReturnType,
        IReadOnlyList<BindingType> concreteParameters,
        string condition,
        string importMethodName,
        IReadOnlyList<string> invocationArguments,
        string indent)
    {
        writer.WriteLine($"{indent}if ({condition})");
        writer.WriteLine($"{indent}{{");
        var bodyIndent = indent + "    ";
        if (HasNativeDefaultDispatchParams(function))
        {
            WriteGenericManagedMethodDefaultCascade(
                writer,
                compilation,
                function,
                returnTypeName,
                concreteReturnType,
                concreteParameters,
                importMethodName,
                bodyIndent);
        }
        else
        {
            WriteGenericManagedMethodInvocation(
                writer,
                compilation,
                function,
                returnTypeName,
                concreteReturnType,
                concreteParameters,
                importMethodName,
                invocationArguments,
                bodyIndent);
        }

        writer.WriteLine($"{indent}}}");
    }

    private static void WriteGenericManagedMethodDefaultCascade(
        CodeWriter writer,
        BindingCompilation compilation,
        FunctionBinding function,
        string returnTypeName,
        BindingType concreteReturnType,
        IReadOnlyList<BindingType> concreteParameters,
        string importMethodName,
        string indent)
    {
        var defaultIndices = GetNativeDefaultDispatchParameterIndices(function);
        WriteGenericManagedMethodDefaultBranch(
            writer,
            compilation,
            function,
            returnTypeName,
            concreteReturnType,
            concreteParameters,
            importMethodName,
            function.Parameters.Count,
            defaultIndices,
            defaultIndices.Count,
            indent);

        for (var providedDefaultCount = defaultIndices.Count - 1; providedDefaultCount >= 0; providedDefaultCount--)
        {
            var keepCount = defaultIndices[providedDefaultCount];
            WriteGenericManagedMethodDefaultBranch(
                writer,
                compilation,
                function,
                returnTypeName,
                concreteReturnType,
                concreteParameters,
                GetUnexpressibleVariantImportName(importMethodName, keepCount),
                keepCount,
                defaultIndices,
                providedDefaultCount,
                indent);
        }

        writer.WriteLine($"{indent}throw new global::System.ArgumentException(\"SharpCpp optional arguments that depend on native defaults must be supplied as a trailing contiguous suffix.\");");
    }

    private static void WriteGenericManagedMethodDefaultBranch(
        CodeWriter writer,
        BindingCompilation compilation,
        FunctionBinding function,
        string returnTypeName,
        BindingType concreteReturnType,
        IReadOnlyList<BindingType> concreteParameters,
        string importMethodName,
        int keepCount,
        IReadOnlyList<int> defaultIndices,
        int providedDefaultCount,
        string indent)
    {
        writer.WriteLine($"{indent}if ({BuildGenericMethodDefaultCondition(function, defaultIndices, providedDefaultCount)})");
        writer.WriteLine($"{indent}{{");
        var invocationArguments = new List<string> { "global::SharpCpp.Support.NativeObject.GetHandle(this)" };
        for (var index = 0; index < keepCount; index++)
        {
            invocationArguments.Add(BuildGenericInvocationArgument(compilation, function.Parameters[index], concreteParameters[index]));
        }

        WriteGenericManagedMethodInvocation(
            writer,
            compilation,
            function,
            returnTypeName,
            concreteReturnType,
            concreteParameters.Take(keepCount).ToArray(),
            importMethodName,
            invocationArguments,
            indent + "    ");
        writer.WriteLine($"{indent}}}");
        writer.WriteLine();
    }

    private static string BuildGenericMethodDefaultCondition(FunctionBinding function, IReadOnlyList<int> defaultIndices, int providedDefaultCount)
    {
        var parts = new List<string>(defaultIndices.Count);
        for (var index = 0; index < defaultIndices.Count; index++)
        {
            var parameter = function.Parameters[defaultIndices[index]];
            if (parameter.Type.IsGenericParameter)
            {
                parts.Add($"{parameter.ManagedName} is {(index < providedDefaultCount ? "not null" : "null")}");
            }
            else
            {
                parts.Add(BuildUnexpressibleDefaultCondition(parameter, provided: index < providedDefaultCount));
            }
        }

        return string.Join(" && ", parts);
    }

    private static void WriteGenericManagedMethodInvocation(
        CodeWriter writer,
        BindingCompilation compilation,
        FunctionBinding function,
        string returnTypeName,
        BindingType concreteReturnType,
        IReadOnlyList<BindingType> concreteParameters,
        string importMethodName,
        IReadOnlyList<string> invocationArguments,
        string indent)
    {
        var keepAliveStatements = BuildGenericKeepAliveStatements(function.Parameters, concreteParameters);

        if (concreteReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
        {
            writer.WriteLine($"{indent}{GetGenericImportAccessor(compilation)}.{importMethodName}({string.Join(", ", invocationArguments)});");
            foreach (var keepAlive in keepAliveStatements)
            {
                writer.WriteLine($"{indent}{keepAlive}");
            }
            writer.WriteLine($"{indent}return;");
        }
        else if (function.ReturnType.IsGenericParameter)
        {
            writer.WriteLine($"{indent}var __result = {GetGenericImportAccessor(compilation)}.{importMethodName}({string.Join(", ", invocationArguments)});");
            foreach (var keepAlive in keepAliveStatements)
            {
                writer.WriteLine($"{indent}{keepAlive}");
            }
            if (concreteReturnType.Kind == BindingTypeKind.Handle)
            {
                writer.WriteLine($"{indent}return ({returnTypeName})(object){BuildManagedHandleReturnResultExpression(compilation, concreteReturnType, "__result", ReturnsOwnedHandle(function, concreteReturnType))}!;");
            }
            else
            {
                writer.WriteLine($"{indent}return ({returnTypeName})(object)__result!;");
            }
        }
        else if (concreteReturnType.Kind == BindingTypeKind.Handle)
        {
            writer.WriteLine($"{indent}var __result = {GetGenericImportAccessor(compilation)}.{importMethodName}({string.Join(", ", invocationArguments)});");
            foreach (var keepAlive in keepAliveStatements)
            {
                writer.WriteLine($"{indent}{keepAlive}");
            }
            writer.WriteLine($"{indent}return {BuildManagedHandleReturnResultExpression(compilation, concreteReturnType, "__result", ReturnsOwnedHandle(function, concreteReturnType))};");
        }
        else
        {
            writer.WriteLine($"{indent}var __result = {GetGenericImportAccessor(compilation)}.{importMethodName}({string.Join(", ", invocationArguments)});");
            foreach (var keepAlive in keepAliveStatements)
            {
                writer.WriteLine($"{indent}{keepAlive}");
            }
            writer.WriteLine($"{indent}return __result;");
        }
    }

    private static string RenderConcreteGenericManagedMethodOverloads(BindingCompilation compilation, RecordBinding record, FunctionBinding function)
    {
        if (record.IsGeneric)
        {
            return string.Empty;
        }

        var writer = new CodeWriter();
        var methodParams = (IReadOnlyList<BindingGenericParameter>)function.GenericParameters.ToList();
        var recordInstantiation = EmptyRecordGenericInstantiation();
        var instantiations = function.SupportedGenericInstantiations.OrderBy(
            static instantiation => string.Join("|", instantiation.TypeArguments.Select(static argument => argument.CppTypeName)),
            StringComparer.Ordinal).ToArray();

        foreach (var methodInstantiation in instantiations)
        {
            var concreteParameters = function.Parameters.Select(parameter =>
                SubstituteGenericType(record, parameter.Type, recordInstantiation, methodParams, methodInstantiation)).ToArray();
            var concreteReturnType = SubstituteGenericType(record, function.ReturnType, recordInstantiation, methodParams, methodInstantiation);
            var parameterList = string.Join(", ", function.Parameters.Select((parameter, index) =>
                RenderConcreteManagedPublicParameter(compilation, parameter, concreteParameters[index])));
            var returnTypeName = GetManagedPublicReturnTypeName(concreteReturnType, compilation);
            var importMethodName = GetGenericMethodImportName(function, recordInstantiation, methodInstantiation);
            var invocationArguments = new List<string> { "global::SharpCpp.Support.NativeObject.GetHandle(this)" };
            for (var index = 0; index < function.Parameters.Count; index++)
            {
                invocationArguments.Add(BuildGenericInvocationArgument(compilation, function.Parameters[index], concreteParameters[index]));
            }

            writer.WriteLine();
            WriteDocumentationComment(writer, function);
            writer.WriteLine($"{GetManagedVisibilityNormalized(compilation, record, function)} {GetManagedInstanceMethodModifier(compilation, record, function)}{returnTypeName} {function.ManagedName}({parameterList})");
            writer.WriteLine("{");
            var keepAliveStatements = BuildGenericKeepAliveStatements(function.Parameters, concreteParameters);

            if (concreteReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
            {
                writer.WriteLine($"    {GetGenericImportAccessor(compilation)}.{importMethodName}({string.Join(", ", invocationArguments)});");
                foreach (var keepAlive in keepAliveStatements)
                {
                    writer.WriteLine($"    {keepAlive}");
                }
                writer.WriteLine("}");
                continue;
            }

            writer.WriteLine($"    var __result = {GetGenericImportAccessor(compilation)}.{importMethodName}({string.Join(", ", invocationArguments)});");
            foreach (var keepAlive in keepAliveStatements)
            {
                writer.WriteLine($"    {keepAlive}");
            }

            if (concreteReturnType.Kind == BindingTypeKind.Handle)
            {
                writer.WriteLine($"    return {BuildManagedHandleReturnResultExpression(compilation, concreteReturnType, "__result", ReturnsOwnedHandle(function, concreteReturnType))};");
            }
            else
            {
                writer.WriteLine("    return __result;");
            }

            writer.WriteLine("}");
        }

        return writer.ToString();
    }

    private static string RenderConcreteManagedPublicParameter(BindingCompilation compilation, ParameterBinding parameter, BindingType concreteType)
    {
        var concreteParameter = CreateConcreteParameter(parameter, concreteType);
        return RenderManagedPublicParameter(concreteParameter, compilation);
    }

    private static ParameterBinding CreateConcreteParameter(ParameterBinding parameter, BindingType concreteType)
    {
        return new ParameterBinding(
            parameter.SymbolId,
            parameter.NativeName,
            parameter.ManagedName,
            concreteType,
            parameter.Direction,
            parameter.AbiKind,
            concreteType.CppTypeName)
        {
            ManagedDefaultValueExpression = parameter.ManagedDefaultValueExpression,
            UseCallbackDelegate = parameter.UseCallbackDelegate,
            CallbackDelegateTypeName = parameter.CallbackDelegateTypeName
        };
    }

    private static string BuildCombinedGenericInstantiationCondition(
        BindingCompilation compilation,
        RecordBinding record,
        RecordGenericInstantiationBinding recordInstantiation,
        FunctionBinding function,
        RecordGenericInstantiationBinding methodInstantiation)
    {
        var parts = new List<string>();

        foreach (var index in GetManagedVisibleGenericParameterIndices(record))
        {
            parts.Add($"typeof({record.GenericParameters[index].Name}) == typeof({GetManagedWrapperTypeName(recordInstantiation.TypeArguments[index], compilation)})");
        }

        for (var index = 0; index < function.GenericParameters.Count; index++)
        {
            parts.Add($"typeof({function.GenericParameters[index].Name}) == typeof({GetManagedWrapperTypeName(methodInstantiation.TypeArguments[index], compilation)})");
        }

        return parts.Count == 0 ? "true" : string.Join(" && ", parts);
    }

    private static IEnumerable<string> CollectGenericMethodImportDeclarations(BindingCompilation compilation, RecordBinding record, FunctionBinding method)
    {
        return CollectGenericMethodImportDeclarations(compilation, record, method, GetMethodRecordInstantiations(compilation, record));
    }

    private static IEnumerable<string> CollectNonGenericRecordGenericMethodImportDeclarations(BindingCompilation compilation, RecordBinding record)
    {
        foreach (var method in record.Members.OfType<FunctionBinding>()
                     .Where(static function => !function.IsConstructor && !function.IsStatic && function.IsGeneric && function.OperatorToken is null && !function.IsConversionOperator && !function.HasMetadata(MetadataKeys.IgnoreReason))
                     .OrderBy(static function => function.ManagedName, StringComparer.Ordinal))
        {
            foreach (var declaration in CollectGenericMethodImportDeclarations(compilation, record, method))
            {
                yield return declaration;
            }
        }
    }

    private static IEnumerable<string> CollectGenericMethodImportDeclarations(
        BindingCompilation compilation,
        RecordBinding record,
        FunctionBinding method,
        IReadOnlyList<RecordGenericInstantiationBinding> recordInstantiations)
    {
        var methodInstantiations = method.SupportedGenericInstantiations.ToArray();
        if (methodInstantiations.Length == 0)
        {
            yield break;
        }

        var methodParams = (IReadOnlyList<BindingGenericParameter>)method.GenericParameters.ToList();
        foreach (var recordInstantiation in recordInstantiations)
        {
            foreach (var methodInstantiation in methodInstantiations)
            {
                yield return RenderGenericMethodImportDeclaration(compilation, record, method, methodParams, recordInstantiation, methodInstantiation);
                if (HasNativeDefaultDispatchParams(method))
                {
                    foreach (var keepCount in GetVariantParameterIndices(method))
                    {
                        yield return RenderGenericMethodImportDeclaration(compilation, record, method, methodParams, recordInstantiation, methodInstantiation, keepCount);
                    }
                }
            }
        }
    }

    private static IReadOnlyList<RecordGenericInstantiationBinding> GetMethodRecordInstantiations(BindingCompilation compilation, RecordBinding record)
    {
        return record.IsGeneric
            ? GetCanonicalManagedGenericInstantiations(compilation, record)
            : [EmptyRecordGenericInstantiation()];
    }

    private static RecordGenericInstantiationBinding EmptyRecordGenericInstantiation()
        => new([]);

    private static string GetGenericMethodEntryPointName(FunctionBinding function, RecordGenericInstantiationBinding recordInstantiation, RecordGenericInstantiationBinding methodInstantiation)
    {
        return $"{function.EntryPointName}{GetGenericInstantiationSuffix(recordInstantiation)}{GetGenericInstantiationSuffix(methodInstantiation)}";
    }

    private static string GetGenericMethodImportName(FunctionBinding function, RecordGenericInstantiationBinding recordInstantiation, RecordGenericInstantiationBinding methodInstantiation)
    {
        return $"{GetManagedImportMethodName(function)}{GetGenericInstantiationSuffix(recordInstantiation)}{GetGenericInstantiationSuffix(methodInstantiation)}";
    }

    private static string GetGenericManagedReturnType(BindingType type, BindingCompilation? compilation = null)
    {
        if (type.IsGenericParameter)
        {
            return type.GenericParameterName ?? "object";
        }

        return GetManagedPublicReturnTypeName(type, compilation);
    }

    private static string BuildFunctionInstantiationCondition(BindingCompilation compilation, FunctionBinding function, RecordGenericInstantiationBinding instantiation)
    {
        if (function.GenericParameters.Count == 0 || instantiation.TypeArguments.Count == 0)
        {
            return "true";
        }

        return string.Join(
            " && ",
            function.GenericParameters.Select((parameter, index) => $"typeof({parameter.Name}) == typeof({GetManagedWrapperTypeName(instantiation.TypeArguments[index], compilation)})"));
    }

    private static string RenderGenericManagedDispatchBranches(BindingCompilation compilation, RecordBinding record, FunctionBinding function, IReadOnlyList<RecordGenericInstantiationBinding> instantiations, bool includeSelf)
    {
        var writer = new CodeWriter();

        foreach (var instantiation in instantiations)
        {
            var concreteParameters = function.Parameters.Select(parameter => SubstituteGenericType(record, parameter.Type, instantiation)).ToArray();
            var concreteReturnType = SubstituteGenericType(record, function.ReturnType, instantiation);
            var invocationArguments = new List<string>();
            if (includeSelf)
            {
                invocationArguments.Add("global::SharpCpp.Support.NativeObject.GetHandle(this)");
            }

            for (var index = 0; index < function.Parameters.Count; index++)
            {
                invocationArguments.Add(BuildGenericInvocationArgument(compilation, function.Parameters[index], concreteParameters[index]));
            }

            var importMethodName = GetGenericImportMethodName(function, instantiation);
            writer.WriteLine($"if ({BuildGenericInstantiationCondition(compilation, record, instantiation)})");
            writer.WriteLine("{");
            var keepAliveStatements = BuildGenericKeepAliveStatements(function.Parameters, concreteParameters);

            if (concreteReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
            {
                writer.WriteLine($"    {GetGenericImportAccessor(compilation)}.{importMethodName}({string.Join(", ", invocationArguments)});");
                foreach (var keepAlive in keepAliveStatements)
                {
                    writer.WriteLine($"    {keepAlive}");
                }
                writer.WriteLine("    return;");
            }
            else if (function.IsConstructor)
            {
                writer.WriteLine($"    return {GetGenericImportAccessor(compilation)}.{importMethodName}({string.Join(", ", invocationArguments)});");
            }
            else if (function.ReturnType.IsGenericParameter)
            {
                writer.WriteLine($"    var __result = {GetGenericImportAccessor(compilation)}.{importMethodName}({string.Join(", ", invocationArguments)});");
                foreach (var keepAlive in keepAliveStatements)
                {
                    writer.WriteLine($"    {keepAlive}");
                }
                if (concreteReturnType.Kind == BindingTypeKind.Handle)
                {
                    writer.WriteLine($"    return ({GetManagedPublicReturnTypeName(function.ReturnType, compilation)})(object){BuildManagedHandleReturnResultExpression(compilation, concreteReturnType, "__result", ReturnsOwnedHandle(function, concreteReturnType))}!;");
                }
                else
                {
                    writer.WriteLine($"    return ({GetManagedPublicReturnTypeName(function.ReturnType, compilation)})(object)__result!;");
                }
            }
            else if (concreteReturnType.Kind == BindingTypeKind.Handle)
            {
                writer.WriteLine($"    var __result = {GetGenericImportAccessor(compilation)}.{importMethodName}({string.Join(", ", invocationArguments)});");
                var wrapExpression = BuildManagedHandleReturnResultExpression(compilation, concreteReturnType, "__result", ReturnsOwnedHandle(function, concreteReturnType));
                foreach (var keepAlive in keepAliveStatements)
                {
                    writer.WriteLine($"    {keepAlive}");
                }
                if (function.ReturnType.RecordId == record.SymbolId && function.ReturnType.ContainsGenericParameters)
                {
                    writer.WriteLine($"    return ({GetManagedPublicReturnTypeName(function.ReturnType, compilation)})(object){wrapExpression}!;");
                }
                else
                {
                    writer.WriteLine($"    return {wrapExpression};");
                }
            }
            else
            {
                writer.WriteLine($"    var __result = {GetGenericImportAccessor(compilation)}.{importMethodName}({string.Join(", ", invocationArguments)});");
                foreach (var keepAlive in keepAliveStatements)
                {
                    writer.WriteLine($"    {keepAlive}");
                }
                writer.WriteLine("    return __result;");
            }

            writer.WriteLine("}");
        }

        return writer.ToString();
    }

    private static string RenderGenericDeleteHelper(BindingCompilation compilation, RecordBinding record, IReadOnlyList<RecordGenericInstantiationBinding> instantiations)
    {
        var writer = new CodeWriter();
        writer.Write(
            """
            private static void DeleteHandle(nint handle)
            {
            """);

        foreach (var instantiation in instantiations)
        {
            writer.WriteLine($"if ({BuildGenericInstantiationCondition(compilation, record, instantiation)})");
            writer.WriteLine("{");
            writer.WriteLine($"    {GetGenericImportAccessor(compilation)}.{GetGenericDeleteImportMethodName(record, instantiation)}(handle);");
            writer.WriteLine("    return;");
            writer.WriteLine("}");
        }

        writer.Write($$"""
            throw new NotSupportedException("SharpCpp does not support the requested generic argument combination for delete on '{{record.ManagedName}}'.");
            }

            protected override bool ReleaseNativeHandle(nint handle)
            {
            if (OwnsNativeHandle)
            {
                DeleteHandle(handle);
            }
            return true;
            }
            """);
        return writer.ToString();
    }

    private static string RenderGenericConstructorImportDeclaration(BindingCompilation compilation, RecordBinding record, FunctionBinding function, RecordGenericInstantiationBinding instantiation)
    {
        var parameterList = string.Join(", ", function.Parameters.Select((parameter, index) => RenderConcreteManagedParameter(compilation, parameter, SubstituteGenericType(record, parameter.Type, instantiation))));
        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetGenericEntryPointName(function, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static partial nint {{GetGenericImportMethodName(function, instantiation)}}({{parameterList}});
            """;
    }

    private static string RenderGenericInstanceImportDeclaration(BindingCompilation compilation, RecordBinding record, FunctionBinding function, RecordGenericInstantiationBinding instantiation)
    {
        var parameterList = string.Join(", ", new[] { "nint self" }.Concat(function.Parameters.Select((parameter, index) => RenderConcreteManagedParameter(compilation, parameter, SubstituteGenericType(record, parameter.Type, instantiation)))));
        var concreteReturnType = SubstituteGenericType(record, function.ReturnType, instantiation);
        var returnTypeName = GetManagedImportReturnTypeName(compilation, concreteReturnType);
        var returnAttribute = GetManagedImportReturnAttribute(compilation, concreteReturnType, ReturnsOwnedHandle(function, concreteReturnType));
        var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static partial {returnTypeName} {GetGenericImportMethodName(function, instantiation)}({parameterList});"
            : $"{returnAttribute}\ninternal static partial {returnTypeName} {GetGenericImportMethodName(function, instantiation)}({parameterList});";

        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetGenericEntryPointName(function, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            {{importMethodLine}}
            """;
    }

    private static string RenderGenericMethodImportDeclaration(
        BindingCompilation compilation,
        RecordBinding record,
        FunctionBinding function,
        IReadOnlyList<BindingGenericParameter> methodParams,
        RecordGenericInstantiationBinding recordInstantiation,
        RecordGenericInstantiationBinding methodInstantiation,
        int? keepCount = null)
    {
        var parameters = keepCount.HasValue
            ? function.Parameters.Take(keepCount.Value)
            : function.Parameters;
        var concreteParameters = parameters.Select(parameter =>
            RenderConcreteManagedParameter(compilation, parameter,
                SubstituteGenericType(record, parameter.Type, recordInstantiation, methodParams, methodInstantiation))).ToArray();
        var concreteReturnType = SubstituteGenericType(record, function.ReturnType, recordInstantiation, methodParams, methodInstantiation);
        var parameterList = string.Join(", ", new[] { "nint self" }.Concat(concreteParameters));
        var returnTypeName = GetManagedImportReturnTypeName(compilation, concreteReturnType);
        var returnAttribute = GetManagedImportReturnAttribute(compilation, concreteReturnType, ReturnsOwnedHandle(function, concreteReturnType));
        var baseImportMethodName = GetGenericMethodImportName(function, recordInstantiation, methodInstantiation);
        var baseEntryPointName = GetGenericMethodEntryPointName(function, recordInstantiation, methodInstantiation);
        var importMethodName = keepCount.HasValue
            ? GetUnexpressibleVariantImportName(baseImportMethodName, keepCount.Value)
            : baseImportMethodName;
        var entryPointName = keepCount.HasValue
            ? GetUnexpressibleVariantEntrypointName(baseEntryPointName, keepCount.Value)
            : baseEntryPointName;
        var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static partial {returnTypeName} {importMethodName}({parameterList});"
            : $"{returnAttribute}\ninternal static partial {returnTypeName} {importMethodName}({parameterList});";

        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{entryPointName}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            {{importMethodLine}}
            """;
    }

    private static string RenderGenericStaticImportDeclaration(BindingCompilation compilation, RecordBinding record, FunctionBinding function, RecordGenericInstantiationBinding instantiation)
    {
        var parameterList = string.Join(", ", function.Parameters.Select((parameter, index) => RenderConcreteManagedParameter(compilation, parameter, SubstituteGenericType(record, parameter.Type, instantiation))));
        var concreteReturnType = SubstituteGenericType(record, function.ReturnType, instantiation);
        var returnTypeName = GetManagedImportReturnTypeName(compilation, concreteReturnType);
        var returnAttribute = GetManagedImportReturnAttribute(compilation, concreteReturnType, ReturnsOwnedHandle(function, concreteReturnType));
        var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static partial {returnTypeName} {GetGenericImportMethodName(function, instantiation)}({parameterList});"
            : $"{returnAttribute}\ninternal static partial {returnTypeName} {GetGenericImportMethodName(function, instantiation)}({parameterList});";

        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetGenericEntryPointName(function, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            {{importMethodLine}}
            """;
    }

    private static string RenderGenericFreeFunctionImportDeclaration(BindingCompilation compilation, FunctionBinding function, RecordGenericInstantiationBinding instantiation)
    {
        var methodParams = (IReadOnlyList<BindingGenericParameter>)function.GenericParameters.ToList();
        var parameterList = string.Join(", ", function.Parameters.Select((parameter, index) => RenderConcreteManagedParameter(compilation, parameter, SubstituteFunctionGenericType(parameter.Type, methodParams, instantiation))));
        var concreteReturnType = SubstituteFunctionGenericType(function.ReturnType, methodParams, instantiation);
        var returnTypeName = GetManagedImportReturnTypeName(compilation, concreteReturnType);
        var returnAttribute = GetManagedImportReturnAttribute(compilation, concreteReturnType, ReturnsOwnedHandle(function, concreteReturnType));
        var importMethodName = GetGenericImportMethodName(function, instantiation);
        var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static partial {returnTypeName} {importMethodName}({parameterList});"
            : $"{returnAttribute}\ninternal static partial {returnTypeName} {importMethodName}({parameterList});";

        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetGenericEntryPointName(function, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            {{importMethodLine}}
            """;
    }

    private static string RenderGenericDeleteImportDeclaration(BindingCompilation compilation, RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetGenericDeleteEntryPointName(record, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static partial void {{GetGenericDeleteImportMethodName(record, instantiation)}}(nint self);
            """;
    }

    private static string RenderGenericCloneImportDeclaration(BindingCompilation compilation, RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        var concreteHandleType = SubstituteGenericType(record, record.HandleType, instantiation);
        var returnTypeName = GetManagedImportReturnTypeName(compilation, concreteHandleType);
        var returnAttribute = GetManagedImportReturnAttribute(compilation, concreteHandleType, ownsHandle: true);
        var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static partial {returnTypeName} {GetGenericCloneImportMethodName(record, instantiation)}(nint self);"
            : $"{returnAttribute}\ninternal static partial {returnTypeName} {GetGenericCloneImportMethodName(record, instantiation)}(nint self);";

        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetGenericCloneEntryPointName(record, instantiation)}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            {{importMethodLine}}
            """;
    }

    private static string RenderConcreteManagedParameter(BindingCompilation compilation, ParameterBinding parameter, BindingType concreteType)
    {
        var concreteParameter = CreateConcreteParameter(parameter, concreteType);
        var callbackMarshallerParameter = parameter.Type.IsCallback && concreteType.IsCallback
            ? parameter
            : null;
        return RenderManagedImportParameter(concreteParameter, concreteType, compilation, callbackMarshallerParameter);
    }

    private static BindingType SubstituteGenericType(RecordBinding record, BindingType type, RecordGenericInstantiationBinding instantiation, IReadOnlyList<BindingGenericParameter>? methodParameters = null, RecordGenericInstantiationBinding? methodInstantiation = null)
    {
        if (type.IsGenericParameter)
        {
            if (methodParameters is not null && methodInstantiation is not null)
            {
                var methodIndex = methodParameters.ToList().FindIndex(parameter => string.Equals(parameter.Name, type.GenericParameterName, StringComparison.Ordinal));
                if (methodIndex >= 0 && methodIndex < methodInstantiation.TypeArguments.Count)
                {
                    return methodInstantiation.TypeArguments[methodIndex];
                }
            }

            var index = record.GenericParameters.ToList().FindIndex(parameter => string.Equals(parameter.Name, type.GenericParameterName, StringComparison.Ordinal));
            return index >= 0 && index < instantiation.TypeArguments.Count
                ? instantiation.TypeArguments[index]
                : type;
        }

        if (type.GenericArguments.Count == 0)
        {
            return type;
        }

        var substitutedArguments = type.GenericArguments.Select(argument => SubstituteGenericType(record, argument, instantiation, methodParameters, methodInstantiation)).ToArray();
        if (type.Kind == BindingTypeKind.Handle)
        {
            var resolvedRecord = record.SymbolId == type.RecordId ? record : null;
            var cppTypeName = resolvedRecord is not null
                ? $"{resolvedRecord.FormatCppTypeName(substitutedArguments)}*"
                : type.CppTypeName;
            return BindingType.CreateHandle(
                cppTypeName,
                resolvedRecord?.ManagedTypeFormat ?? resolvedRecord?.ManagedName ?? type.ManagedTypeFormatOverride ?? type.Marshaller.PublicTypeName,
                type.RecordId,
                type.MarshallerTypeName,
                substitutedArguments,
                type.Marshaller.CppIsRecordValue);
        }

        if (type.Kind == BindingTypeKind.Enum)
        {
            return BindingType.CreateEnum(type.CppTypeName, type.Marshaller.PublicTypeName, substitutedArguments, type.EnumId);
        }

        return type;
    }

    private static BindingType SubstituteFunctionGenericType(BindingType type, IReadOnlyList<BindingGenericParameter> functionParameters, RecordGenericInstantiationBinding instantiation)
    {
        if (type.IsGenericParameter)
        {
            var index = functionParameters.ToList().FindIndex(parameter => string.Equals(parameter.Name, type.GenericParameterName, StringComparison.Ordinal));
            return index >= 0 && index < instantiation.TypeArguments.Count
                ? instantiation.TypeArguments[index]
                : type;
        }

        if (type.GenericArguments.Count == 0)
        {
            return type;
        }

        var substitutedArguments = type.GenericArguments.Select(argument => SubstituteFunctionGenericType(argument, functionParameters, instantiation)).ToArray();
        if (type.Kind == BindingTypeKind.Handle)
        {
            return BindingType.CreateHandle(
                type.CppTypeName,
                type.ManagedTypeFormatOverride ?? type.Marshaller.PublicTypeName,
                type.RecordId,
                type.MarshallerTypeName,
                substitutedArguments,
                type.Marshaller.CppIsRecordValue);
        }

        if (type.Kind == BindingTypeKind.Enum)
        {
            return BindingType.CreateEnum(type.CppTypeName, type.Marshaller.PublicTypeName, substitutedArguments, type.EnumId);
        }

        return type;
    }

    private static string BuildGenericInstantiationCondition(BindingCompilation compilation, RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        var visibleIndices = GetManagedVisibleGenericParameterIndices(record);
        if (visibleIndices.Count == 0 || instantiation.TypeArguments.Count == 0)
        {
            return "true";
        }

        return string.Join(
            " && ",
            visibleIndices.Select(index => $"typeof({record.GenericParameters[index].Name}) == typeof({GetManagedWrapperTypeName(instantiation.TypeArguments[index], compilation)})"));
    }

    private static IReadOnlyList<BindingGenericParameter> GetManagedVisibleGenericParameters(RecordBinding record)
    {
        return GetManagedVisibleGenericParameterIndices(record)
            .Select(index => record.GenericParameters[index])
            .ToArray();
    }

    private static IReadOnlyList<int> GetManagedVisibleGenericParameterIndices(RecordBinding record)
    {
        var usedIndices = new HashSet<int>();
        CollectManagedFormatPlaceholderIndices(record.ManagedTypeFormat, usedIndices);
        CollectManagedFormatPlaceholderIndices(record.ManagedBaseTypeName, usedIndices);
        CollectManagedFormatPlaceholderIndices(record.ManagedBaseTypeConstraints, usedIndices);

        if (usedIndices.Count == 0)
        {
            return Enumerable.Range(0, record.GenericParameters.Count).ToArray();
        }

        return usedIndices
            .Where(index => index >= 0 && index < record.GenericParameters.Count)
            .OrderBy(static index => index)
            .ToArray();
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

    private static string BuildGenericInvocationArgument(BindingCompilation compilation, ParameterBinding parameter, BindingType concreteType)
    {
        if (!parameter.Type.IsGenericParameter)
        {
            return RenderManagedInvocationArgument(compilation, parameter);
        }

        if (concreteType.IsPointerType)
        {
            return CanUseManagedHandleParameterImport(parameter, concreteType)
                ? $"({GetManagedWrapperTypeName(concreteType, compilation)})(object){parameter.ManagedName}!"
                : $"global::SharpCpp.Support.NativeObject.GetHandle({parameter.ManagedName})";
        }

        return $"({GetManagedWrapperTypeName(concreteType, compilation)})(object){parameter.ManagedName}!";
    }

    private static IReadOnlyList<string> BuildGenericKeepAliveStatements(IList<ParameterBinding> parameters, IReadOnlyList<BindingType> concreteTypes)
    {
        var results = new List<string>();
        var count = Math.Min(parameters.Count, concreteTypes.Count);
        for (var index = 0; index < count; index++)
        {
            if (concreteTypes[index].IsPointerType &&
                parameters[index].Direction is not (BindingParameterDirection.Ref or BindingParameterDirection.Out))
            {
                results.Add($"global::System.GC.KeepAlive({parameters[index].ManagedName});");
            }
        }

        return results;
    }

    private static string GetGenericConstructorFactoryName(FunctionBinding function)
    {
        return $"__CreateHandle_{GetManagedImportMethodName(function)}";
    }

    private static string GetGenericImportMethodName(FunctionBinding function, RecordGenericInstantiationBinding instantiation)
    {
        var ownerPrefix = function.FindOwningRecord() is { } ownerRecord
            ? $"{SanitizeManagedToken(ownerRecord.ManagedName)}_"
            : string.Empty;
        return $"{ownerPrefix}{GetManagedImportMethodName(function)}{GetGenericInstantiationSuffix(instantiation)}";
    }

    private static string GetGenericDeleteImportMethodName(RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $"Delete_{SanitizeManagedToken(record.ManagedName)}{GetGenericInstantiationSuffix(instantiation)}";
    }

    private static string GetGenericCloneImportMethodName(RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $"CloneCopyCtor_{SanitizeManagedToken(record.ManagedName)}{GetGenericInstantiationSuffix(instantiation)}";
    }

    private static string GetGenericEntryPointName(FunctionBinding function, RecordGenericInstantiationBinding instantiation)
    {
        return $"{function.EntryPointName}{GetGenericInstantiationSuffix(instantiation)}";
    }

    private static string GetGenericDeleteEntryPointName(RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $"sharpcpp_e2e_{SanitizeNativeEntrypointName(record.CppQualifiedName)}_delete{GetGenericInstantiationSuffix(instantiation)}";
    }

    private static string GetGenericCloneEntryPointName(RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        return $"sharpcpp_e2e_{SanitizeNativeEntrypointName(record.CppQualifiedName)}_clone{GetGenericInstantiationSuffix(instantiation)}";
    }

    private static string GetGenericInstantiationSuffix(RecordGenericInstantiationBinding instantiation)
    {
        return instantiation.Suffix;
    }

    private static string RenderNativeGenericConstructor(RecordBinding record, FunctionBinding constructor, RecordGenericInstantiationBinding instantiation)
    {
        var concreteParameters = constructor.Parameters.Select(parameter => SubstituteGenericType(record, parameter.Type, instantiation)).ToArray();
        var parameterList = string.Join(", ", constructor.Parameters.Select((parameter, index) => $"arg_t<{concreteParameters[index].CppTypeName}> {parameter.NativeName}"));
        var abiArguments = string.Join(", ", constructor.Parameters.Select(static parameter => parameter.NativeName));
        var constructorArgTypes = string.Join(", ", concreteParameters.Select(static parameter => parameter.CppTypeName));
        var concreteRecordType = record.FormatCppTypeName(instantiation.TypeArguments);

        return $$"""
            SHARPCPP_EXPORT ret_t<{{concreteRecordType}}*> {{GetGenericEntryPointName(constructor, instantiation)}}({{parameterList}})
            {
            return MarshallerInvoker::construct<{{concreteRecordType}}{{(constructorArgTypes.Length > 0 ? $", {constructorArgTypes}" : string.Empty)}}>({{abiArguments}});
            }
            """;
    }

    private static string RenderNativeGenericArrayRecord(BindingCompilation compilation, RecordBinding record)
    {
        return IsGenericDynamicArrayRecord(record)
            ? RenderNativeGenericDynamicArrayRecord(compilation, record)
            : RenderNativeGenericFixedArrayRecord(compilation, record);
    }

    private static string RenderNativeGenericFixedArrayRecord(BindingCompilation compilation, RecordBinding record)
    {
        var writer = new CodeWriter();
        foreach (var instantiation in GetFixedArrayInstantiations(compilation, record))
        {
            _ = TryGetFixedArrayLength(instantiation, out var length);
            var elementType = instantiation.TypeArguments[0];
            var elementCppType = GetNativeFixedArrayElementCppType(compilation, elementType);
            if (record.GetMetadata<bool>(MetadataKeys.NativeArrayRecord))
            {
                if (elementType.IsFixedArray)
                {
                    var nestedLength = elementType.FixedArrayLength ?? 0;
                    writer.Write($$"""
                        SHARPCPP_EXPORT NativeArrayAbi {{GetFixedArrayCreateEntryPointName(compilation, record, instantiation)}}()
                        {
                            auto* value = new {{elementCppType}}[{{length}}][{{nestedLength}}]{};
                            return { value, {{length}} };
                        }

                        SHARPCPP_EXPORT ret_t<void> {{GetFixedArrayDeleteEntryPointName(compilation, record, instantiation)}}(NativeArrayAbi self)
                        {
                            if (self.handle != nullptr)
                            {
                                delete[] static_cast<{{elementCppType}} (*)[{{nestedLength}}]>(self.handle);
                            }
                        }

                        SHARPCPP_EXPORT NativeArrayAbi {{GetFixedArrayGetEntryPointName(compilation, record, instantiation)}}(NativeArrayAbi self, arg_t<std::size_t> index)
                        {
                            if (self.handle == nullptr || self.length != {{length}} || index >= {{length}})
                            {
                                throw std::runtime_error("SharpCpp fixed array access out of range.");
                            }

                            return { const_cast<void*>(static_cast<const void*>(static_cast<{{elementCppType}} (*)[{{nestedLength}}]>(self.handle)[index])), {{nestedLength}} };
                        }

                        SHARPCPP_EXPORT ret_t<void> {{GetFixedArraySetEntryPointName(compilation, record, instantiation)}}(NativeArrayAbi self, arg_t<std::size_t> index, NativeArrayAbi value)
                        {
                            if (self.handle == nullptr || self.length != {{length}} || index >= {{length}})
                            {
                                throw std::runtime_error("SharpCpp fixed array access out of range.");
                            }

                            if (value.handle == nullptr || value.length != {{nestedLength}})
                            {
                                throw std::runtime_error("SharpCpp fixed array element assignment length mismatch.");
                            }

                            auto* target = static_cast<{{elementCppType}} (*)[{{nestedLength}}]>(self.handle);
                            auto* source = reinterpret_cast<{{elementCppType}} (*)[{{nestedLength}}]>(value.handle);
                            for (std::size_t nestedIndex = 0; nestedIndex < {{nestedLength}}; ++nestedIndex)
                            {
                                target[index][nestedIndex] = (*source)[nestedIndex];
                            }
                        }

                        """);
                    continue;
                }

                writer.Write($$"""
                    SHARPCPP_EXPORT NativeArrayAbi {{GetFixedArrayCreateEntryPointName(compilation, record, instantiation)}}()
                    {
                        auto* value = new {{elementCppType}}[{{length}}]{};
                        return { value, {{length}} };
                    }

                    SHARPCPP_EXPORT ret_t<void> {{GetFixedArrayDeleteEntryPointName(compilation, record, instantiation)}}(NativeArrayAbi self)
                    {
                        if (self.handle != nullptr)
                        {
                            delete[] static_cast<{{elementCppType}}*>(self.handle);
                        }
                    }

                    SHARPCPP_EXPORT ret_t<{{elementCppType}}> {{GetFixedArrayGetEntryPointName(compilation, record, instantiation)}}(NativeArrayAbi self, arg_t<std::size_t> index)
                    {
                        if (self.handle == nullptr || self.length != {{length}} || index >= {{length}})
                        {
                            throw std::runtime_error("SharpCpp fixed array access out of range.");
                        }

                        ReturnMarshaller<{{elementCppType}}> marshaller;
                        return marshaller.ConvertToAbi(static_cast<{{elementCppType}}*>(self.handle)[index]);
                    }

                    SHARPCPP_EXPORT ret_t<void> {{GetFixedArraySetEntryPointName(compilation, record, instantiation)}}(NativeArrayAbi self, arg_t<std::size_t> index, arg_t<{{elementCppType}}> value)
                    {
                        if (self.handle == nullptr || self.length != {{length}} || index >= {{length}})
                        {
                            throw std::runtime_error("SharpCpp fixed array access out of range.");
                        }

                        ParameterMarshaller<{{elementCppType}}> marshaller;
                        static_cast<{{elementCppType}}*>(self.handle)[index] = marshaller.ConvertToCpp(value);
                    }

                    """);
                continue;
            }

            var concreteArrayType = record.FormatCppTypeName(instantiation.TypeArguments);
            writer.Write($$"""
                SHARPCPP_EXPORT NativeArrayAbi {{GetFixedArrayCreateEntryPointName(compilation, record, instantiation)}}()
                {
                    auto* value = new {{concreteArrayType}}{};
                    return { value, {{length}} };
                }

                SHARPCPP_EXPORT ret_t<void> {{GetFixedArrayDeleteEntryPointName(compilation, record, instantiation)}}(NativeArrayAbi self)
                {
                    if (self.handle != nullptr)
                    {
                        delete static_cast<{{concreteArrayType}}*>(self.handle);
                    }
                }

                SHARPCPP_EXPORT ret_t<{{elementCppType}}> {{GetFixedArrayGetEntryPointName(compilation, record, instantiation)}}(NativeArrayAbi self, arg_t<std::size_t> index)
                {
                    if (self.handle == nullptr || self.length != {{length}} || index >= {{length}})
                    {
                        throw std::runtime_error("SharpCpp fixed array access out of range.");
                    }

                    ReturnMarshaller<{{elementCppType}}> marshaller;
                    return marshaller.ConvertToAbi((*static_cast<{{concreteArrayType}}*>(self.handle))[index]);
                }

                SHARPCPP_EXPORT ret_t<void> {{GetFixedArraySetEntryPointName(compilation, record, instantiation)}}(NativeArrayAbi self, arg_t<std::size_t> index, arg_t<{{elementCppType}}> value)
                {
                    if (self.handle == nullptr || self.length != {{length}} || index >= {{length}})
                    {
                        throw std::runtime_error("SharpCpp fixed array access out of range.");
                    }

                    ParameterMarshaller<{{elementCppType}}> marshaller;
                    (*static_cast<{{concreteArrayType}}*>(self.handle))[index] = marshaller.ConvertToCpp(value);
                }

                """);
        }

        if (IsSharedNativeArrayRecord(record))
        {
            foreach (var elementType in GetPointerCountNativeArrayElementTypes(compilation))
                writer.Write(RenderNativeDynamicNativeArray(compilation, elementType));
        }

        return writer.ToString();
    }

    private static string RenderNativeGenericDynamicArrayRecord(BindingCompilation compilation, RecordBinding record)
    {
        var writer = new CodeWriter();
        foreach (var instantiation in GetDynamicArrayInstantiations(compilation, record))
        {
            var concreteArrayType = record.FormatCppTypeName(instantiation.TypeArguments);
            var elementCppType = instantiation.TypeArguments[0].CppTypeName;
            writer.Write($$"""
                SHARPCPP_EXPORT ret_t<{{concreteArrayType}}*> {{GetDynamicArrayCreateEntryPointName(compilation, record, instantiation)}}(arg_t<std::size_t> length)
                {
                    return new {{concreteArrayType}}(new {{elementCppType}}[length]{});
                }

                SHARPCPP_EXPORT ret_t<void> {{GetDynamicArrayDeleteEntryPointName(compilation, record, instantiation)}}(arg_t<{{concreteArrayType}}*> self)
                {
                    delete self;
                }

                SHARPCPP_EXPORT ret_t<{{elementCppType}}> {{GetDynamicArrayGetEntryPointName(compilation, record, instantiation)}}(arg_t<{{concreteArrayType}}*> self, arg_t<std::size_t> index)
                {
                    if (self == nullptr)
                    {
                        throw std::runtime_error("SharpCpp dynamic array access on null array.");
                    }

                    ReturnMarshaller<{{elementCppType}}> marshaller;
                    return marshaller.ConvertToAbi((*self)[index]);
                }

                SHARPCPP_EXPORT ret_t<void> {{GetDynamicArraySetEntryPointName(compilation, record, instantiation)}}(arg_t<{{concreteArrayType}}*> self, arg_t<std::size_t> index, arg_t<{{elementCppType}}> value)
                {
                    if (self == nullptr)
                    {
                        throw std::runtime_error("SharpCpp dynamic array access on null array.");
                    }

                    ParameterMarshaller<{{elementCppType}}> marshaller;
                    (*self)[index] = marshaller.ConvertToCpp(value);
                }

                """);
        }

        return writer.ToString();
    }

    private static string RenderNativeGenericClone(RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        var concreteRecordType = record.FormatCppTypeName(instantiation.TypeArguments);
        return $$"""
            SHARPCPP_EXPORT ret_t<{{concreteRecordType}}*> {{GetGenericCloneEntryPointName(record, instantiation)}}(arg_t<{{concreteRecordType}}*> self)
            {
            Marshaller<{{concreteRecordType}}*> self_marshaller;
            auto* instance = static_cast<{{concreteRecordType}}*>(self_marshaller.ConvertToCpp(self));
            return detail::TryClone<{{concreteRecordType}}>(instance, 0);
            }
            """;
    }

    private static string RenderNativeGenericInstanceMethod(RecordBinding record, FunctionBinding method, RecordGenericInstantiationBinding instantiation)
    {
        var concreteRecordType = record.FormatCppTypeName(instantiation.TypeArguments);
        var concreteParameters = method.Parameters.Select(parameter => SubstituteGenericType(record, parameter.Type, instantiation)).ToArray();
        var concreteReturnType = SubstituteGenericType(record, method.ReturnType, instantiation);
        var parameterList = string.Join(", ", new[] { $"arg_t<{concreteRecordType}*> self" }.Concat(method.Parameters.Select((parameter, index) => $"arg_t<{concreteParameters[index].CppTypeName}> {parameter.NativeName}")));
        var abiArguments = string.Join(", ", method.Parameters.Select(static parameter => parameter.NativeName));
        var memberSignature = $"{concreteReturnType.CppTypeName}({concreteRecordType}::*)({string.Join(", ", concreteParameters.Select(static parameter => parameter.CppTypeName))}){(method.IsConstMethod ? " const" : string.Empty)}";
        var marshallerTemplateArguments = BuildMarshallerInvokerTemplateArguments("FT", concreteParameters.Select(static parameter => parameter.CppTypeName));

        if (method.TryGetMetadata(MetadataKeys.MarshallerExposedMethod, out string? _))
        {
            return $$"""
                SHARPCPP_EXPORT {{GetNativeEntrypointReturnType(concreteReturnType)}} {{GetGenericEntryPointName(method, instantiation)}}({{parameterList}})
                {
                {{RenderNativeGenericMarshallerExposedInvocation(method, concreteRecordType, concreteReturnType, concreteParameters, abiArguments)}}
                }
                """;
        }

        return $$"""
            SHARPCPP_EXPORT {{GetNativeEntrypointReturnType(concreteReturnType)}} {{GetGenericEntryPointName(method, instantiation)}}({{parameterList}})
            {
            using FT = {{memberSignature}};
            {{(concreteReturnType.PrimitiveKind == BindingPrimitiveKind.Void
                ? $"MarshallerInvoker::invoke<{marshallerTemplateArguments}>(&{concreteRecordType}::{method.NativeName}, self{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});"
                : RequiresOwnedRecordValueReturn(concreteReturnType)
                    ? $"return MarshallerInvoker::invoke_owned<{marshallerTemplateArguments}>(&{concreteRecordType}::{method.NativeName}, self{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});"
                    : $"return MarshallerInvoker::invoke<{marshallerTemplateArguments}>(&{concreteRecordType}::{method.NativeName}, self{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});")}}
            }
            """;
    }

    private static string RenderNativeGenericStaticMethod(RecordBinding record, FunctionBinding method, RecordGenericInstantiationBinding instantiation)
    {
        var concreteRecordType = record.FormatCppTypeName(instantiation.TypeArguments);
        var concreteParameters = method.Parameters.Select(parameter => SubstituteGenericType(record, parameter.Type, instantiation)).ToArray();
        var concreteReturnType = SubstituteGenericType(record, method.ReturnType, instantiation);
        var parameterList = string.Join(", ", method.Parameters.Select((parameter, index) => $"arg_t<{concreteParameters[index].CppTypeName}> {parameter.NativeName}"));
        var abiArguments = string.Join(", ", method.Parameters.Select(static parameter => parameter.NativeName));
        var signature = $"{concreteReturnType.CppTypeName}({string.Join(", ", concreteParameters.Select(static parameter => parameter.CppTypeName))})";
        var marshallerTemplateArguments = BuildMarshallerInvokerTemplateArguments("FT", concreteParameters.Select(static parameter => parameter.CppTypeName));

        return $$"""
            SHARPCPP_EXPORT {{GetNativeEntrypointReturnType(concreteReturnType)}} {{GetGenericEntryPointName(method, instantiation)}}({{parameterList}})
            {
            using FT = {{signature}};
            {{(concreteReturnType.PrimitiveKind == BindingPrimitiveKind.Void
                ? $"MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>(&{concreteRecordType}::{method.NativeName}{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});"
                : RequiresOwnedRecordValueReturn(concreteReturnType)
                    ? $"return MarshallerInvoker::invoke_owned_as<{marshallerTemplateArguments}>(&{concreteRecordType}::{method.NativeName}{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});"
                    : $"return MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>(&{concreteRecordType}::{method.NativeName}{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});")}}
            }
            """;
    }

    internal static string RenderNativeGenericFreeFunction(FunctionBinding function, RecordGenericInstantiationBinding instantiation)
    {
        var methodParams = (IReadOnlyList<BindingGenericParameter>)function.GenericParameters.ToList();
        var concreteParameters = function.Parameters.Select(parameter => SubstituteFunctionGenericType(parameter.Type, methodParams, instantiation)).ToArray();
        var concreteReturnType = SubstituteFunctionGenericType(function.ReturnType, methodParams, instantiation);
        var parameterList = string.Join(", ", function.Parameters.Select((parameter, index) => $"arg_t<{concreteParameters[index].CppTypeName}> {parameter.NativeName}"));
        var abiArguments = string.Join(", ", function.Parameters.Select(static parameter => parameter.NativeName));
        var signature = $"{concreteReturnType.CppTypeName}({string.Join(", ", concreteParameters.Select(static parameter => parameter.CppTypeName))})";
        var methodCppTypeArgs = string.Join(", ", instantiation.TypeArguments.Select(static argument => argument.CppTypeName));
        var marshallerTemplateArguments = BuildMarshallerInvokerTemplateArguments("FT", concreteParameters.Select(static parameter => parameter.CppTypeName));

        return $$"""
            SHARPCPP_EXPORT {{GetNativeEntrypointReturnType(concreteReturnType)}} {{GetGenericEntryPointName(function, instantiation)}}({{parameterList}})
            {
            using FT = {{signature}};
            {{(concreteReturnType.PrimitiveKind == BindingPrimitiveKind.Void
                ? $"MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>(&{function.NativeName}<{methodCppTypeArgs}>{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});"
                : RequiresOwnedRecordValueReturn(concreteReturnType)
                    ? $"return MarshallerInvoker::invoke_owned_as<{marshallerTemplateArguments}>(&{function.NativeName}<{methodCppTypeArgs}>{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});"
                    : $"return MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>(&{function.NativeName}<{methodCppTypeArgs}>{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});")}}
            }
            """;
    }

    private static string RenderNativeGenericDelete(RecordBinding record, RecordGenericInstantiationBinding instantiation)
    {
        var concreteRecordType = record.FormatCppTypeName(instantiation.TypeArguments);
        return $$"""
            SHARPCPP_EXPORT ret_t<void> {{GetGenericDeleteEntryPointName(record, instantiation)}}(arg_t<{{concreteRecordType}}*> self)
            {
            MarshallerInvoker::destroy<{{concreteRecordType}}>(self);
            }
            """;
    }

    internal static string RenderNativeGenericMethodInstanceMethod(
        RecordBinding record,
        FunctionBinding method,
        RecordGenericInstantiationBinding recordInstantiation,
        RecordGenericInstantiationBinding methodInstantiation)
    {
        var writer = new CodeWriter();
        var methodParams = (IReadOnlyList<BindingGenericParameter>)method.GenericParameters.ToList();
        var concreteRecordType = record.FormatCppTypeName(recordInstantiation.TypeArguments);
        var concreteParameters = method.Parameters.Select(parameter =>
            SubstituteGenericType(record, parameter.Type, recordInstantiation, methodParams, methodInstantiation)).ToArray();
        var concreteReturnType = SubstituteGenericType(record, method.ReturnType, recordInstantiation, methodParams, methodInstantiation);
        var parameterList = string.Join(", ", new[] { $"arg_t<{concreteRecordType}*> self" }.Concat(method.Parameters.Select((parameter, index) => $"arg_t<{concreteParameters[index].CppTypeName}> {parameter.NativeName}")));
        var abiArguments = string.Join(", ", method.Parameters.Select(static parameter => parameter.NativeName));
        var marshallerParameterTypes = method.Parameters.Select((parameter, index) => FormatConcreteGenericParameterCppType(parameter, concreteParameters[index])).ToArray();
        var memberSignature = $"{concreteReturnType.CppTypeName}({concreteRecordType}::*)({string.Join(", ", marshallerParameterTypes)}){(method.IsConstMethod ? " const" : string.Empty)}";
        var methodCppTypeArgs = string.Join(", ", methodInstantiation.TypeArguments.Select(static argument => argument.CppTypeName));
        var marshallerTemplateArguments = BuildMarshallerInvokerTemplateArguments("FT", marshallerParameterTypes);

        writer.Write($$"""
            SHARPCPP_EXPORT {{GetNativeEntrypointReturnType(concreteReturnType)}} {{GetGenericMethodEntryPointName(method, recordInstantiation, methodInstantiation)}}({{parameterList}})
            {
            using FT = {{memberSignature}};
            {{(concreteReturnType.PrimitiveKind == BindingPrimitiveKind.Void
                ? $"MarshallerInvoker::invoke<{marshallerTemplateArguments}>(&{concreteRecordType}::{method.NativeName}<{methodCppTypeArgs}>, self{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});"
                : RequiresOwnedRecordValueReturn(concreteReturnType)
                    ? $"return MarshallerInvoker::invoke_owned<{marshallerTemplateArguments}>(&{concreteRecordType}::{method.NativeName}<{methodCppTypeArgs}>, self{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});"
                    : $"return MarshallerInvoker::invoke<{marshallerTemplateArguments}>(&{concreteRecordType}::{method.NativeName}<{methodCppTypeArgs}>, self{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});")}}
            }
            """);

        if (HasNativeDefaultDispatchParams(method))
        {
            foreach (var keepCount in GetVariantParameterIndices(method))
            {
                writer.WriteLine();
                writer.Write(RenderNativeGenericMethodInstanceMethodDefaultVariant(
                    record,
                    method,
                    recordInstantiation,
                    methodInstantiation,
                    concreteRecordType,
                    concreteReturnType,
                    concreteParameters,
                    methodCppTypeArgs,
                    keepCount));
            }
        }

        return writer.ToString();
    }

    private static string RenderNativeGenericMethodInstanceMethodDefaultVariant(
        RecordBinding record,
        FunctionBinding method,
        RecordGenericInstantiationBinding recordInstantiation,
        RecordGenericInstantiationBinding methodInstantiation,
        string concreteRecordType,
        BindingType concreteReturnType,
        IReadOnlyList<BindingType> concreteParameters,
        string methodCppTypeArgs,
        int keepCount)
    {
        var variantParams = method.Parameters.Take(keepCount).ToArray();
        var parameterList = string.Join(", ", new[] { $"arg_t<{concreteRecordType}*> self" }
            .Concat(variantParams.Select((parameter, index) => $"arg_t<{concreteParameters[index].CppTypeName}> {parameter.NativeName}")));
        var signatureParameterTypes = new[] { $"{concreteRecordType}*" }
            .Concat(variantParams.Select((parameter, index) => FormatConcreteGenericParameterCppType(parameter, concreteParameters[index])))
            .ToArray();
        var signatureParameterList = string.Join(", ", signatureParameterTypes);
        var lambdaParameterList = string.Join(", ", new[] { "auto&& self" }
            .Concat(variantParams.Select(static parameter => $"auto&& {parameter.NativeName}")));
        var forwardedArgs = ForwardVariantArgs(variantParams);
        var invokeArgs = string.Join(", ", new[] { "fn", "self" }.Concat(variantParams.Select(static parameter => parameter.NativeName)));
        var entryPointName = GetUnexpressibleVariantEntrypointName(GetGenericMethodEntryPointName(method, recordInstantiation, methodInstantiation), keepCount);
        var invocation = forwardedArgs.Length == 0
            ? $"std::forward<decltype(self)>(self)->template {method.NativeName}<{methodCppTypeArgs}>()"
            : $"std::forward<decltype(self)>(self)->template {method.NativeName}<{methodCppTypeArgs}>({forwardedArgs})";
        var marshallerTemplateArguments = BuildMarshallerInvokerTemplateArguments("FT", signatureParameterTypes);
        var dispatch = concreteReturnType.PrimitiveKind == BindingPrimitiveKind.Void
            ? $"MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>({invokeArgs});"
            : RequiresOwnedRecordValueReturn(concreteReturnType)
                ? $"return MarshallerInvoker::invoke_owned_as<{marshallerTemplateArguments}>({invokeArgs});"
                : $"return MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>({invokeArgs});";

        return $$"""
            SHARPCPP_EXPORT {{GetNativeEntrypointReturnType(concreteReturnType)}} {{entryPointName}}({{parameterList}})
            {
            using FT = {{concreteReturnType.CppTypeName}}({{signatureParameterList}});
            auto fn = []({{lambdaParameterList}}) -> decltype(auto)
            {
                {{(concreteReturnType.PrimitiveKind == BindingPrimitiveKind.Void ? invocation + ";" : "return " + invocation + ";")}}
            };
            {{dispatch}}
            }
            """;
    }

    private static string FormatConcreteGenericParameterCppType(ParameterBinding parameter, BindingType concreteType)
    {
        if (!parameter.Type.IsGenericParameter || string.IsNullOrWhiteSpace(parameter.Type.GenericParameterName))
        {
            return concreteType.CppTypeName;
        }

        var genericName = parameter.Type.GenericParameterName;
        if (string.Equals(parameter.CppTypeName, genericName + "&&", StringComparison.Ordinal) &&
            concreteType.Marshaller.CppIsReference)
        {
            return concreteType.CppTypeName;
        }

        return parameter.CppTypeName.Replace(genericName, concreteType.CppTypeName, StringComparison.Ordinal);
    }

    private static string RenderNativeGenericMarshallerExposedInvocation(
        FunctionBinding method,
        string concreteRecordType,
        BindingType concreteReturnType,
        IReadOnlyList<BindingType> concreteParameters,
        string abiArguments)
    {
        var signatureParameters = string.Join(", ", new[] { $"{concreteRecordType}*" }.Concat(concreteParameters.Select(static parameter => parameter.CppTypeName)));
        var lambdaParameters = string.Join(", ", new[] { $"{concreteRecordType}* __self" }
            .Concat(concreteParameters.Select((parameter, index) => $"{parameter.CppTypeName} __arg{index}")));
        var invokeArguments = string.Join(", ", new[] { "*__self" }
            .Concat(concreteParameters.Select((_, index) => $"__arg{index}")));
        var callArguments = string.IsNullOrWhiteSpace(abiArguments) ? "self" : $"self, {abiArguments}";
        var returnStatement = concreteReturnType.PrimitiveKind == BindingPrimitiveKind.Void
            ? $"Marshaller<{concreteRecordType}>::{method.NativeName}({invokeArguments});"
            : $"return Marshaller<{concreteRecordType}>::{method.NativeName}({invokeArguments});";
        var marshallerTemplateArguments = BuildMarshallerInvokerTemplateArguments(
            "FT",
            new[] { $"{concreteRecordType}*" }.Concat(concreteParameters.Select(static parameter => parameter.CppTypeName)));
        var dispatch = concreteReturnType.PrimitiveKind == BindingPrimitiveKind.Void
            ? $"MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>(__invoke, {callArguments});"
            : RequiresOwnedRecordValueReturn(concreteReturnType)
                ? $"return MarshallerInvoker::invoke_owned_as<{marshallerTemplateArguments}>(__invoke, {callArguments});"
                : $"return MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>(__invoke, {callArguments});";

        return $$"""
            using FT = {{concreteReturnType.CppTypeName}}({{signatureParameters}});
            auto __invoke = []({{lambdaParameters}}) -> {{concreteReturnType.CppTypeName}}
            {
            {{returnStatement}}
            };
            {{dispatch}}
            """;
    }
}
