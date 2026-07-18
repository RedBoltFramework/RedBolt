using SharpCpp.Model.Ast;
using SharpCpp.Model.Utilities;

using System.Text;

namespace SharpCpp.Rendering;

public static partial class BindingOutputRenderer
{
    private const string ICloneableFullName = "global::System.ICloneable";
    private const string BorrowedNativeHandleFullName = "global::SharpCpp.Support.BorrowedNativeHandle";
    private const string OwnedNativeHandleFullName = "global::SharpCpp.Support.OwnedNativeHandle";
    private const string NativeHandleOwnershipConstraintFullName = "global::SharpCpp.Support.INativeHandleOwnership";

    private sealed record ManagedCallbackUsage(BindingType CallbackType, string ManagedTypeName, string MarshallerDeclarationName);

    private sealed record ManagedCallbackDelegateDeclaration(string DeclarationName, BindingType CallbackType);

    private static ManagedBindingProjection? GetManagedProjection(BindingCompilation compilation)
    {
        return compilation.GetMetadata<ManagedBindingProjection>(MetadataKeys.ManagedBindingProjection);
    }

    private static string GetManagedNamespace(BindingCompilation compilation, RecordBinding record)
    {
        return GetManagedProjection(compilation)?.GetManagedNamespace(record, compilation) ??
            record.ManagedNamespace ??
            compilation.ManagedNamespace;
    }

    private static string GetManagedNamespace(BindingCompilation compilation, EnumBinding enumBinding)
    {
        return GetManagedProjection(compilation)?.GetManagedNamespace(enumBinding, compilation) ??
            enumBinding.ManagedNamespace ??
            compilation.ManagedNamespace;
    }

    private static string GetManagedNamespace(BindingCompilation compilation, FunctionBinding function)
    {
        return string.IsNullOrWhiteSpace(function.ManagedNamespace)
            ? compilation.ManagedNamespace
            : function.ManagedNamespace!;
    }

    private static string GetManagedClassName(BindingCompilation compilation, FunctionBinding function)
    {
        return string.IsNullOrWhiteSpace(function.ManagedClassName)
            ? compilation.ManagedClassName
            : function.ManagedClassName!;
    }

    private static string GetBindingClassAccessor(BindingCompilation compilation)
        => $"global::{compilation.ManagedNamespace}.{compilation.ManagedClassName}";

    public static GeneratedBindingSet Render(
        BindingCompilation compilation,
        string fileStem,
        Func<string?, string>? deriveGroupStem = null,
        bool includeCallbackReleaseRegistration = true)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileStem);

        AssignImportMethodNames(compilation);

        if (deriveGroupStem is null)
        {
            var files = new[]
            {
                new GeneratedBindingFile($"{fileStem}.Bindings.g.cs", RenderManagedBindings(
                    compilation,
                    includeCallbackHelpers: false)),
                new GeneratedBindingFile($"{fileStem}.Bindings.Aliases.g.cs", RenderAliasBindings(compilation, includeSharedCallbackHelpers: true)),
                new GeneratedBindingFile($"{fileStem}.Bindings.g.cpp", RenderNativeBindings(compilation, includeCallbackReleaseRegistration: includeCallbackReleaseRegistration))
            };
            return new GeneratedBindingSet(files);
        }

        var allFiles = new List<GeneratedBindingFile>();

        var groups = compilation.Members.OfType<BindingNode>()
            .Where(m => m is FunctionBinding ||
                        m is RecordBinding record && !record.GetMetadata<bool>(MetadataKeys.SuppressEmission) ||
                        m is EnumBinding @enum && !@enum.GetMetadata<bool>(MetadataKeys.SuppressEmission) ||
                        m is ManagedClassBinding managedClass && managedClass.Parent is not ManagedClassBinding)
            .GroupBy(m => deriveGroupStem(string.IsNullOrEmpty(m.HeaderIncludePath) ? null : m.HeaderIncludePath), StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToList();

        if (groups.Count == 0)
        {
            allFiles.Add(new GeneratedBindingFile($"{fileStem}.Bindings.g.cs", RenderManagedBindings(
                compilation,
                includeCallbackHelpers: false)));
            allFiles.Add(new GeneratedBindingFile($"{fileStem}.Bindings.g.cpp", RenderNativeBindings(compilation, includeCallbackReleaseRegistration: includeCallbackReleaseRegistration)));
        }
        else
        {
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                var stem = group.Key;
                var memberSet = new HashSet<BindingNode>(group);

                allFiles.Add(new GeneratedBindingFile($"{stem}.g.cs",
                    RenderManagedBindings(
                        compilation,
                        node => memberSet.Contains(node) || (node is RecordBinding r && memberSet.Contains(r)),
                        includeCallbackHelpers: false)));
                allFiles.Add(new GeneratedBindingFile($"{stem}.g.cpp",
                    RenderNativeBindings(
                        compilation,
                        node => memberSet.Contains(node) || (node is RecordBinding r && memberSet.Contains(r)),
                        includeCallbackReleaseRegistration: includeCallbackReleaseRegistration && groupIndex == 0)));
            }
        }

        allFiles.Add(new GeneratedBindingFile($"{fileStem}.Bindings.Aliases.g.cs",
            RenderAliasBindings(compilation, deriveGroupStem is not null, includeSharedCallbackHelpers: true)));
        return new GeneratedBindingSet(allFiles);
    }

    private static string RenderManagedBindings(
        BindingCompilation compilation,
        Func<BindingNode, bool>? memberFilter = null,
        string? groupHeaderPath = null,
        bool includeCallbackHelpers = true)
    {
        var writer = new CodeWriter();
        var allRecords = compilation.Records.Where(static record => !record.GetMetadata<bool>(MetadataKeys.SuppressEmission) && !record.IsSilentlyExcluded()).AsEnumerable();
        var allEnums = compilation.Enums.Where(static @enum => !@enum.GetMetadata<bool>(MetadataKeys.SuppressEmission) && !@enum.IsSilentlyExcluded()).AsEnumerable();
        var allFunctions = compilation.Functions.Where(static function => !function.IsSilentlyExcluded() &&
            (function.Parent is not RecordBinding parentRecord || !parentRecord.IsSilentlyExcluded())).AsEnumerable();
        var allManagedClasses = compilation.ManagedClasses
            .Where(static managedClass => managedClass.Parent is not ManagedClassBinding)
            .AsEnumerable();

        if (memberFilter is not null)
        {
            allRecords = allRecords.Where(r => memberFilter(r));
            allEnums = allEnums.Where(e => memberFilter(e));
            allFunctions = allFunctions.Where(f => memberFilter(f));
            allManagedClasses = allManagedClasses.Where(c => memberFilter(c));
        }

        var records = allRecords.OrderBy(static record => record.ManagedName, StringComparer.Ordinal).ToArray();
        var enums = allEnums.OrderBy(static item => item.ManagedName, StringComparer.Ordinal).ToArray();
        var functions = allFunctions.OrderBy(static function => function.ManagedName, StringComparer.Ordinal).ToArray();
        var managedClasses = allManagedClasses.OrderBy(static item => item.ManagedName, StringComparer.Ordinal).ToArray();
        var interfaceRecordIds = compilation.Records
            .SelectMany(record => record.GetSecondaryBaseRecords(compilation))
            .Select(static record => record.SymbolId)
            .ToHashSet(StringComparer.Ordinal);
        var interfaceRecords = records
            .Where(record => interfaceRecordIds.Contains(record.SymbolId))
            .GroupBy(static record => record.SymbolId, StringComparer.Ordinal)
            .Select(static group => group.First())
            // Exclude concrete generic instantiations from interface rendering.
            // Each closed instantiation (e.g. IIDFamily<Geometry>, IIDFamily<Material>)
            // has a ManagedName containing angle brackets. Emitting them as separate
            // partial interfaces with different type parameter names causes CS0264.
            .Where(static record => !record.ManagedName.Contains('<'))
            .OrderBy(static record => record.ManagedName, StringComparer.Ordinal)
            .ToArray();
        var polymorphicConcreteRecords = records
            .Where(record => record.IsPolymorphic && CanCreateManagedWrapperFactory(compilation, record) && !record.HasMetadata(MetadataKeys.IgnoreReason))
            .ToArray();
        var genericImportDeclarations = new List<string>();

        writer.Write(
            $$"""
            // <auto-generated />
            #nullable enable
            using System;
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            using System.Runtime.InteropServices.Marshalling;
            """);

        var recordGroups = records.GroupBy(record => GetManagedNamespace(compilation, record)).ToArray();
        var enumGroups = enums.GroupBy(@enum => GetManagedNamespace(compilation, @enum)).ToArray();
        var managedClassGroups = managedClasses.GroupBy(managedClass => GetManagedNamespace(compilation, managedClass)).ToArray();
        var allNamespaces = recordGroups.Select(group => group.Key)
            .Concat(enumGroups.Select(group => group.Key))
            .Concat(managedClassGroups.Select(group => group.Key))
            .Distinct()
            .OrderBy(ns => ns, StringComparer.Ordinal);

        foreach (var ns in allNamespaces)
        {
            var nsRecords = records.Where(record => GetManagedNamespace(compilation, record) == ns).ToArray();
            var nsInterfaces = interfaceRecords.Where(record => GetManagedNamespace(compilation, record) == ns).ToArray();
            var nsEnums = enums.Where(@enum => GetManagedNamespace(compilation, @enum) == ns).ToArray();
            var nsManagedClasses = managedClasses.Where(managedClass => GetManagedNamespace(compilation, managedClass) == ns).ToArray();

            writer.WriteLine();
            writer.Write(
                $$"""
                namespace {{ns}}
                {
                """);

            foreach (var interfaceRecord in nsInterfaces)
            {
                writer.WriteLine();
                writer.Write(RenderInterface(compilation, interfaceRecord));
            }

            foreach (var record in nsRecords)
            {
                writer.WriteLine();
                if (record.HasMetadata(MetadataKeys.IgnoreReason))
                {
                    writer.Write(RenderIgnoredStub(record));
                }
                else
                {
                    genericImportDeclarations.AddRange(record.IsGeneric
                        ? CollectGenericImportDeclarations(compilation, record)
                        : CollectNonGenericRecordGenericMethodImportDeclarations(compilation, record));

                    writer.Write(RenderRecord(compilation, record));
                }
            }

            foreach (var @enum in nsEnums)
            {
                writer.WriteLine();
                if (@enum.HasMetadata(MetadataKeys.IgnoreReason))
                {
                    writer.Write(RenderIgnoredStub(@enum));
                }
                else
                {
                    writer.Write(RenderEnum(@enum));
                }
            }

            foreach (var managedClass in nsManagedClasses)
            {
                writer.WriteLine();
                writer.Write(RenderManagedClass(managedClass));
            }

            writer.Write(
                """
                }
                """);
        }

        var callbackParameters = includeCallbackHelpers
            ? CollectCallbackParameters(records, functions).ToArray()
            : Array.Empty<ParameterBinding>();
        var defaultContainer = (Namespace: compilation.ManagedNamespace, Class: compilation.ManagedClassName);
        var freeFunctionGroups = functions
            .Where(static function => function.Parent is not RecordBinding && !function.IsConstructor)
            .GroupBy(function => (Namespace: GetManagedNamespace(compilation, function), Class: GetManagedClassName(compilation, function)))
            .ToDictionary(static group => group.Key, static group => group.OrderBy(static function => function.ManagedName, StringComparer.Ordinal).ToArray());

        if ((memberFilter is null && polymorphicConcreteRecords.Length > 0) || callbackParameters.Length > 0 || genericImportDeclarations.Count > 0)
        {
            freeFunctionGroups.TryAdd(defaultContainer, Array.Empty<FunctionBinding>());
        }

        foreach (var (container, containerFunctions) in freeFunctionGroups
            .OrderBy(static pair => pair.Key.Namespace, StringComparer.Ordinal)
            .ThenBy(static pair => pair.Key.Class, StringComparer.Ordinal))
        {
            writer.WriteLine();
            writer.Write(
                $$"""
                namespace {{container.Namespace}}
                {
                public static partial class {{container.Class}}
                {
                """);

            var freeFunctionDeclarations = new List<string>();
            var lastFreeFunctionWasIgnoredStub = false;

            foreach (var function in containerFunctions)
            {
                var isIgnoredStub = function.HasMetadata(MetadataKeys.IgnoreReason);
                writer.WriteLine();
                if (lastFreeFunctionWasIgnoredStub && !isIgnoredStub)
                {
                    writer.WriteLine();
                }

                if (isIgnoredStub)
                {
                    writer.Write(RenderIgnoredStub(function));
                }
                else if (function.IsGeneric)
                {
                    writer.Write(RenderManagedGenericFreeFunction(compilation, function));
                    freeFunctionDeclarations.AddRange(CollectGenericFreeFunctionImportDeclarations(compilation, function));
                }
                else
                {
                    writer.Write(RenderManagedWrapperMethod(compilation, function));
                    freeFunctionDeclarations.Add(RenderManagedImportDeclaration(compilation, function));

                    if (HasNativeDefaultDispatchParams(function) || HasUndefaultableParams(function))
                        freeFunctionDeclarations.Add(RenderUnexpressibleDefaultFreeFunctionImport(compilation, function));
                }

                lastFreeFunctionWasIgnoredStub = isIgnoredStub;
            }

            if (lastFreeFunctionWasIgnoredStub)
            {
                writer.WriteLine();
            }

            var isDefaultContainer = container == defaultContainer;
            if (isDefaultContainer && memberFilter is null && polymorphicConcreteRecords.Length > 0)
            {
                writer.WriteLine();
                writer.Write(RenderNativeObjectRegistryInitializer(compilation, polymorphicConcreteRecords));
            }

            if (isDefaultContainer && callbackParameters.Length > 0)
            {
                writer.WriteLine();
                writer.Write(RenderManagedCallbackSupport(compilation, callbackParameters));
            }

            if (isDefaultContainer)
            {
                var nativeArrayRegistryInitializer = RenderSharedNativeArrayRegistryInitializer(compilation, records);
                if (!string.IsNullOrEmpty(nativeArrayRegistryInitializer))
                {
                    writer.WriteLine();
                    writer.Write(nativeArrayRegistryInitializer);
                }
            }

            if (freeFunctionDeclarations.Count > 0 || isDefaultContainer && genericImportDeclarations.Count > 0)
            {
                writer.WriteLine();
                writer.Write(
                    """
                    internal static partial class __PInvoke
                    {
                    """);

                foreach (var declaration in freeFunctionDeclarations)
                {
                    writer.WriteLine();
                    writer.Write(declaration);
                }

                if (isDefaultContainer)
                {
                    foreach (var declaration in genericImportDeclarations)
                    {
                        writer.WriteLine();
                        writer.Write(declaration);
                    }
                }

                writer.WriteLine();
                writer.Write(
                    """
                    }
                    """);
            }

            writer.WriteLine();
            writer.Write(
                """
                }
                }
                """);
        }

        return writer.ToString();
    }

    private static string GetManagedNamespace(BindingCompilation compilation, ManagedClassBinding managedClass)
    {
        return string.IsNullOrWhiteSpace(managedClass.ManagedNamespace)
            ? compilation.ManagedNamespace
            : managedClass.ManagedNamespace;
    }

    private static string RenderManagedClass(ManagedClassBinding managedClass)
    {
        var writer = new CodeWriter();
        var modifiers = new List<string>
        {
            GetManagedVisibility(managedClass.AccessModifier)
        };

        if (managedClass.IsStatic)
        {
            modifiers.Add("static");
        }
        else if (managedClass.IsSealed)
        {
            modifiers.Add("sealed");
        }

        if (managedClass.IsPartial)
        {
            modifiers.Add("partial");
        }

        var baseClause = !managedClass.IsStatic && !string.IsNullOrWhiteSpace(managedClass.BaseTypeName)
            ? $" : {managedClass.BaseTypeName}"
            : string.Empty;

        writer.Write(
            $$"""
            {{string.Join(' ', modifiers)}} class {{managedClass.ManagedName}}{{baseClause}}
            {
            """);

        foreach (var field in managedClass.Members.OfType<ManagedFieldBinding>().OrderBy(static field => field.ManagedName, StringComparer.Ordinal))
        {
            writer.WriteLine();
            writer.Write(RenderManagedField(field));
        }

        foreach (var codeBlock in managedClass.BodyCodeBlocks)
        {
            writer.WriteLine();
            writer.Write(codeBlock);
        }

        foreach (var nestedClass in managedClass.Members.OfType<ManagedClassBinding>().OrderBy(static nestedClass => nestedClass.ManagedName, StringComparer.Ordinal))
        {
            writer.WriteLine();
            writer.Write(RenderManagedClass(nestedClass));
        }

        writer.WriteLine();
        writer.Write(
            """
            }
            """);
        return writer.ToString();
    }

    private static string RenderManagedField(ManagedFieldBinding field)
    {
        var modifiers = new List<string>
        {
            GetManagedVisibility(field.AccessModifier)
        };

        if (field.IsConst)
        {
            modifiers.Add("const");
        }
        else
        {
            if (field.IsStatic)
            {
                modifiers.Add("static");
            }

            if (field.IsReadOnly)
            {
                modifiers.Add("readonly");
            }
        }

        var initializer = string.IsNullOrWhiteSpace(field.InitializerExpression)
            ? string.Empty
            : $" = {field.InitializerExpression}";
        return $"{string.Join(' ', modifiers)} {field.ManagedTypeName} {field.ManagedName}{initializer};";
    }


    private static string RenderNativeDirectorClass(BindingCompilation compilation, RecordBinding record)
    {
        var writer = new CodeWriter();
        var directorName = GetNativeDirectorClassName(record);
        var constructors = record.Members.OfType<FunctionBinding>().Where(static function => function.IsConstructor).OrderBy(static function => function.Parameters.Count).ToArray();
        var slots = record.GetEffectiveVirtualMethodSlots(compilation);

        writer.Write($$"""
            class {{directorName}} : public {{record.CppQualifiedName}}
            {
            public:
            """);

        foreach (var (slot, index) in slots.Select((slot, index) => (slot, index)))
        {
            writer.WriteLine($"using {GetNativeDirectorCallbackTypeName(index)} = {BuildNativeDirectorCallbackSignature(slot.Method)};");
        }

        if (slots.Count > 0)
        {
            writer.WriteLine();
        }

        foreach (var constructor in constructors)
        {
            var parameterList = string.Join(", ", constructor.Parameters.Select(parameter => $"{parameter.CppTypeName} {parameter.NativeName}"));
            var arguments = string.Join(", ", constructor.Parameters.Select(static parameter => parameter.NativeName));
            writer.Write($$"""
                {{directorName}}({{parameterList}})
                    : {{record.CppQualifiedName}}({{arguments}})
                {
                }

                """);
        }

        foreach (var method in GetAccessibleProtectedMethods(compilation, record))
        {
            writer.WriteLine();
            writer.Write(RenderNativeProtectedMethodHelper(record, method));
        }

        foreach (var field in GetAccessibleProtectedFields(compilation, record))
        {
            writer.WriteLine();
            writer.Write(RenderNativeProtectedFieldHelpers(compilation, record, field));
        }

        var callbaseSlots = record.GetEffectiveVirtualMethodSlots(compilation)
            .Where(static slot => !slot.Method.IsPureVirtual)
            .ToArray();
        if (callbaseSlots.Length > 0)
        {
            writer.WriteLine();
            foreach (var slot in callbaseSlots)
            {
                writer.Write(RenderNativeCallbaseHelper(record, slot));
            }
        }

        writer.Write($$"""
            void connect_director({{string.Join(", ", slots.Select((_, index) => $"void* slot{index}"))}})
            {
            """);

        foreach (var (slot, index) in slots.Select((slot, index) => (slot, index)))
        {
            writer.WriteLine($"{GetNativeDirectorCallbackFieldName(index)} = reinterpret_cast<{GetNativeDirectorCallbackTypeName(index)}>(slot{index});");
        }

        writer.WriteLine("}");

        foreach (var (slot, index) in slots.Select((slot, index) => (slot, index)))
        {
            writer.WriteLine();
            writer.Write(RenderNativeDirectorMethod(slot, index));
        }

        writer.WriteLine();
        writer.WriteLine("private:");
        foreach (var (slot, index) in slots.Select((slot, index) => (slot, index)))
        {
            writer.WriteLine($"{GetNativeDirectorCallbackTypeName(index)} {GetNativeDirectorCallbackFieldName(index)} = nullptr;");
        }

        writer.Write(
            """
            };
            """);
        return writer.ToString();
    }

    private static string RenderNativeDirectorMethod(VirtualMethodSlotBinding slot, int index)
    {
        var method = slot.Method;
        var parameterList = string.Join(", ", method.Parameters.Select(parameter => $"{parameter.CppTypeName} {parameter.NativeName}"));
        var arguments = string.Join(", ", method.Parameters.Select(static parameter => parameter.NativeName));
        var callbackArguments = string.Join(", ", new[] { "const_cast<void*>(static_cast<const void*>(this))" }.Concat(method.Parameters.Select((_, parameterIndex) => $"__abi_arg{parameterIndex}")));
        var writer = new CodeWriter();

        writer.Write($$"""
            {{method.CppReturnTypeName}} {{method.NativeName}}({{parameterList}}){{(method.IsConstMethod ? " const" : string.Empty)}} override
            {
            if ({{GetNativeDirectorCallbackFieldName(index)}} != nullptr)
            {
            """);

        foreach (var (parameter, parameterIndex) in method.Parameters.Select((parameter, parameterIndex) => (parameter, parameterIndex)))
        {
            writer.WriteLine($"    ParameterMarshaller<{parameter.CppTypeName}> __arg{parameterIndex}_marshaller;");
            writer.WriteLine($"    auto __abi_arg{parameterIndex} = __arg{parameterIndex}_marshaller.ConvertToAbi({parameter.NativeName});");
        }

        if (method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
        {
            writer.WriteLine($"    {GetNativeDirectorCallbackFieldName(index)}({callbackArguments});");
            writer.WriteLine("    return;");
        }
        else
        {
            writer.WriteLine($"    ReturnMarshaller<{method.CppReturnTypeName}> __return_marshaller;");
            writer.WriteLine($"    auto __abi_result = {GetNativeDirectorCallbackFieldName(index)}({callbackArguments});");
            writer.WriteLine("    return __return_marshaller.ConvertToCpp(__abi_result);");
        }

        writer.WriteLine("}");
        if (method.IsPureVirtual)
        {
            writer.WriteLine($"throw std::runtime_error(\"SharpCpp callback for pure virtual '{slot.NativeImplementationRecord.NativeName}::{method.NativeName}' was not connected.\");");
        }
        else if (method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
        {
            writer.WriteLine($"{slot.NativeImplementationRecord.CppQualifiedName}::{method.NativeName}({arguments});");
        }
        else
        {
            writer.WriteLine($"return {slot.NativeImplementationRecord.CppQualifiedName}::{method.NativeName}({arguments});");
        }

        writer.WriteLine("}");
        return writer.ToString();
    }

    private static string RenderNativeConnectDirector(RecordBinding record, IReadOnlyList<VirtualMethodSlotBinding> slots)
    {
        var writer = new CodeWriter();
        var directorName = GetNativeDirectorClassName(record);
        var parameterList = string.Join(", ", new[] { $"arg_t<{record.CppQualifiedName}*> self" }.Concat(slots.Select((_, index) => $"void* slot{index}")));
        var arguments = string.Join(", ", slots.Select((_, index) => $"slot{index}"));

        writer.Write($$"""
            SHARPCPP_EXPORT ret_t<void> sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_connect_director({{parameterList}})
            {
            auto* director = dynamic_cast<{{directorName}}*>(self);
            if (director != nullptr)
            {
                director->connect_director({{arguments}});
            }
            }
            """);
        return writer.ToString();
    }

    private static string RenderNativeDelete(RecordBinding record, bool requiresDirector)
    {
        if (!requiresDirector)
        {
            return $$"""
                SHARPCPP_EXPORT ret_t<void> sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_delete(arg_t<{{record.CppQualifiedName}}*> self)
                {
                MarshallerInvoker::destroy<{{record.CppQualifiedName}}>(self);
                }
                """;
        }

        var directorName = GetNativeDirectorClassName(record);
        return $$"""
            SHARPCPP_EXPORT ret_t<void> sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_delete(arg_t<{{record.CppQualifiedName}}*> self)
            {
            if (auto* director = dynamic_cast<{{directorName}}*>(self))
            {
                delete director;
                return;
            }

            delete self;
            }
            """;
    }

    private static string RenderNativeArrayElement(RecordBinding record)
    {
        return $$"""
            SHARPCPP_EXPORT ret_t<{{record.CppQualifiedName}}*> sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_array_element(arg_t<{{record.CppQualifiedName}}*> self, arg_t<std::size_t> index)
            {
            return self != nullptr ? self + index : nullptr;
            }
            """;
    }

    private static string RenderNativeClone(RecordBinding record)
    {
        return $$"""
            SHARPCPP_EXPORT ret_t<{{record.CppQualifiedName}}*> sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_clone(arg_t<{{record.CppQualifiedName}}*> self)
            {
            Marshaller<{{record.CppQualifiedName}}*> self_marshaller;
            auto* instance = static_cast<{{record.CppQualifiedName}}*>(self_marshaller.ConvertToCpp(self));
            return detail::TryClone<{{record.CppQualifiedName}}>(instance, 0);
            }
            """;
    }

    private static string RenderNativeMethodInvocation(FunctionBinding method, string abiArguments, string memberSignature)
    {
        var argumentsSuffix = abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty;
        var marshallerTemplateArguments = BuildMarshallerInvokerTemplateArguments("FT", method.Parameters.Select(static parameter => parameter.CppTypeName));

        if (string.IsNullOrWhiteSpace(method.NativeDispatchRecordCppTypeName))
        {
            return method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void
                ? $$"""
                    MarshallerInvoker::invoke<{{marshallerTemplateArguments}}>(&{{method.OwningRecordCppTypeName}}::{{method.NativeName}}, self{{argumentsSuffix}});
                    """
                : RequiresOwnedRecordValueReturn(method)
                    ? $$"""
                        return MarshallerInvoker::invoke_owned<{{marshallerTemplateArguments}}>(&{{method.OwningRecordCppTypeName}}::{{method.NativeName}}, self{{argumentsSuffix}});
                        """
                : $$"""
                    return MarshallerInvoker::invoke<{{marshallerTemplateArguments}}>(&{{method.OwningRecordCppTypeName}}::{{method.NativeName}}, self{{argumentsSuffix}});
                    """;
        }

        return $$"""
                Marshaller<{{method.OwningRecordCppTypeName}}*> self_marshaller;
                auto* instance = static_cast<{{method.OwningRecordCppTypeName}}*>(self_marshaller.ConvertToCpp(self));
                auto* dispatch = dynamic_cast<{{method.NativeDispatchRecordCppTypeName}}*>(instance);
                if (dispatch == nullptr)
                {
                throw std::runtime_error("SharpCpp failed to adjust the native handle for secondary-base dispatch.");
                }

                using DispatchFT = {{memberSignature.Replace(method.OwningRecordCppTypeName + "::*", method.NativeDispatchRecordCppTypeName + "::*", StringComparison.Ordinal)}};
                {{(method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void
                    ? $"MarshallerInvoker::invoke_instance<{BuildMarshallerInvokerTemplateArguments("DispatchFT", method.Parameters.Select(static parameter => parameter.CppTypeName))}>(static_cast<DispatchFT>(&{method.NativeDispatchRecordCppTypeName}::{method.NativeName}), dispatch{argumentsSuffix});"
                    : RequiresOwnedRecordValueReturn(method)
                        ? $"return MarshallerInvoker::invoke_owned_instance<{BuildMarshallerInvokerTemplateArguments("DispatchFT", method.Parameters.Select(static parameter => parameter.CppTypeName))}>(static_cast<DispatchFT>(&{method.NativeDispatchRecordCppTypeName}::{method.NativeName}), dispatch{argumentsSuffix});"
                        : $"return MarshallerInvoker::invoke_instance<{BuildMarshallerInvokerTemplateArguments("DispatchFT", method.Parameters.Select(static parameter => parameter.CppTypeName))}>(static_cast<DispatchFT>(&{method.NativeDispatchRecordCppTypeName}::{method.NativeName}), dispatch{argumentsSuffix});")}}
                """;
    }

    private static bool RequiresOwnedRecordValueReturn(FunctionBinding function)
    {
        return RequiresOwnedRecordValueReturn(function.ReturnType, function.CppReturnTypeName);
    }

    private static bool RequiresOwnedRecordValueReturn(BindingType returnType)
    {
        return RequiresOwnedRecordValueReturn(returnType, returnType.CppTypeName);
    }

    private static bool RequiresOwnedRecordValueReturn(BindingType returnType, string cppReturnTypeName)
    {
        return returnType.Kind == BindingTypeKind.Handle &&
            returnType.Marshaller.CppIsRecordValue &&
            !returnType.Marshaller.CppIsReference &&
            !IsPointerOrReferenceCppType(cppReturnTypeName);
    }

    private static bool IsPointerOrReferenceCppType(string cppTypeName)
    {
        var normalized = cppTypeName.Trim();
        return normalized.EndsWith('*') || normalized.EndsWith('&');
    }

    private static bool ReturnsOwnedHandle(FunctionBinding function)
    {
        return function.ReturnsOwnedHandle || RequiresOwnedRecordValueReturn(function);
    }

    private static bool ReturnsOwnedHandle(FunctionBinding function, BindingType returnType)
    {
        return function.ReturnsOwnedHandle || RequiresOwnedRecordValueReturn(returnType);
    }

    private static string GetNativeEntrypointReturnType(FunctionBinding function)
    {
        return GetNativeEntrypointReturnType(function.ReturnType, function.CppReturnTypeName);
    }

    private static string GetNativeEntrypointReturnType(BindingType returnType)
    {
        return GetNativeEntrypointReturnType(returnType, returnType.CppTypeName);
    }

    private static string GetNativeEntrypointReturnType(BindingType returnType, string cppReturnTypeName)
    {
        return RequiresOwnedRecordValueReturn(returnType, cppReturnTypeName)
            ? $"arg_t<{cppReturnTypeName}*>"
            : $"ret_t<{cppReturnTypeName}>";
    }

    private static string GetNativeDirectorClassName(RecordBinding record)
    {
        return $"__{SanitizeNativeEntrypointName(record.CppQualifiedName)}_director";
    }

    private static string BuildNativeDirectorCallbackSignature(FunctionBinding method)
    {
        var parameterList = string.Join(", ", new[] { "void* self" }.Concat(method.Parameters.Select(parameter => $"arg_t<{GetNativeParameterMarshallerCppTypeName(parameter)}>")));
        return $"ret_t<{method.CppReturnTypeName}> (*)({parameterList})";
    }

    private static string GetNativeDirectorCallbackTypeName(int index)
    {
        return $"callback_{index}_t";
    }

    private static string GetNativeDirectorCallbackFieldName(int index)
    {
        return $"callback_{index}_";
    }

    private static string RenderNativeProtectedMethodHelper(RecordBinding record, FunctionBinding method)
    {
        ValidateProtectedMemberSupport(method);

        var parameterList = string.Join(", ", method.Parameters.Select(RenderNativeParameter));
        var abiArguments = string.Join(", ", method.Parameters.Select(static parameter => parameter.NativeName));
        var argumentsSuffix = abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty;
        var writer = new CodeWriter();
        var helperName = GetNativeProtectedMethodHelperName(method);
        var directorName = GetNativeDirectorClassName(record);
        var signature = method.IsStatic
            ? BuildNativeFunctionSignature(method)
            : BuildNativeMethodSignature(method).Replace(method.OwningRecordCppTypeName + "::*", directorName + "::*", StringComparison.Ordinal);
        var callable = method.IsStatic
            ? $"static_cast<std::add_pointer_t<FT>>(&{directorName}::{method.NativeName})"
            : $"static_cast<FT>(&{directorName}::{method.NativeName})";
        var marshallerTemplateArguments = BuildMarshallerInvokerTemplateArguments("FT", method.Parameters.Select(static parameter => parameter.CppTypeName));
        var invocation = method.IsStatic
            ? $"MarshallerInvoker::{(RequiresOwnedRecordValueReturn(method) ? "invoke_owned_as" : "invoke_as")}<{marshallerTemplateArguments}>({callable}{argumentsSuffix})"
            : $"MarshallerInvoker::{(RequiresOwnedRecordValueReturn(method) ? "invoke_owned_instance" : "invoke_instance")}<{marshallerTemplateArguments}>({callable}, this{argumentsSuffix})";

        writer.Write($$"""
            {{(method.IsStatic ? "static " : string.Empty)}}{{GetNativeEntrypointReturnType(method)}} {{helperName}}({{parameterList}})
            {
            using FT = {{signature}};
            """);

        if (method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
        {
            writer.WriteLine($"{invocation};");
        }
        else
        {
            writer.WriteLine($"return {invocation};");
        }

        writer.Write(
            """
            }
            """);
        return writer.ToString();
    }

    private static string RenderNativeProtectedFieldHelpers(BindingCompilation compilation, RecordBinding accessorRecord, FieldBinding field)
    {
        var writer = new CodeWriter();
        if (NeedsManagedFixedArrayProxy(field))
        {
            writer.Write($$"""
                {{GetNativeFixedArrayItemGetterReturnType(compilation, field)}} {{GetNativeProtectedFieldItemGetterHelperName(field)}}(std::size_t index)
                {
                {{RenderNativeFixedArrayItemGetInvocation(compilation, field, "this")}}
                }
                """);

            if (!field.IsReadOnly)
            {
                writer.WriteLine();
                writer.Write($$"""
                    void {{GetNativeProtectedFieldItemSetterHelperName(field)}}(std::size_t index, {{GetNativeFixedArrayItemSetterParameterType(compilation, field)}} value)
                    {
                    {{RenderNativeFixedArrayItemSetInvocation(compilation, field, "this")}}
                    }
                    """);
            }
        }
        else
        {
            var plan = CreateNativeFieldInteropPlan(compilation, field);
            var isNativeFixedArrayField = IsNativeFixedArrayType(compilation, field.Type);
            var getterReturnType = isNativeFixedArrayField ? "NativeArrayAbi" : $"ret_t<{plan.StorageCppType}>";
            var directorName = GetNativeDirectorClassName(accessorRecord);
            var getterBody = RenderNativeFieldGetInvocation(compilation, field, plan, "this", directorName);

            writer.Write($$"""
                {{getterReturnType}} {{GetNativeProtectedFieldGetterHelperName(field)}}()
                {
                {{getterBody}}
                }
                """);

            if (!field.IsReadOnly)
            {
                var setterParameterType = isNativeFixedArrayField ? "NativeArrayAbi" : plan.SetterParameterCppType;
                var setterBody = RenderNativeFieldSetInvocation(compilation, field, plan, "this", directorName);
                writer.WriteLine();
                writer.Write($$"""
                    void {{GetNativeProtectedFieldSetterHelperName(field)}}({{setterParameterType}} value)
                    {
                    {{setterBody}}
                    }
                    """);
            }
        }

        return writer.ToString();
    }

    private static string RenderNativeProtectedMethodInvocation(BindingCompilation compilation, FunctionBinding method, string abiArguments, string memberSignature)
    {
        var helperName = GetNativeProtectedMethodHelperName(method);
        var record = compilation.Records.First(candidate => string.Equals(candidate.CppQualifiedName, method.OwningRecordCppTypeName, StringComparison.Ordinal));
        return RenderNativeProtectedAccessInvocation(record, method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void
            ? directorExpression => $"{directorExpression}->{helperName}({abiArguments}); return;"
            : directorExpression => $"return {directorExpression}->{helperName}({abiArguments});", "SharpCpp protected member access requires a director-backed instance.");
    }

    private static string RenderNativeProtectedFieldGetInvocation(RecordBinding accessorRecord, FieldBinding field)
    {
        var helperName = GetNativeProtectedFieldGetterHelperName(field);
        return RenderNativeProtectedAccessInvocation(accessorRecord, directorExpression => $"return {directorExpression}->{helperName}();", "SharpCpp protected field access requires a director-backed instance.");
    }

    private static string RenderNativeProtectedFieldSetInvocation(RecordBinding accessorRecord, FieldBinding field)
    {
        var helperName = GetNativeProtectedFieldSetterHelperName(field);
        return RenderNativeProtectedAccessInvocation(accessorRecord, directorExpression => $"{directorExpression}->{helperName}(value); return;", "SharpCpp protected field access requires a director-backed instance.");
    }

    private static string RenderNativeProtectedFieldItemGetInvocation(RecordBinding accessorRecord, FieldBinding field)
    {
        var helperName = GetNativeProtectedFieldItemGetterHelperName(field);
        return RenderNativeProtectedAccessInvocation(accessorRecord, directorExpression => $"return {directorExpression}->{helperName}(index);", "SharpCpp protected field access requires a director-backed instance.");
    }

    private static string RenderNativeProtectedFieldItemSetInvocation(RecordBinding accessorRecord, FieldBinding field)
    {
        var helperName = GetNativeProtectedFieldItemSetterHelperName(field);
        return RenderNativeProtectedAccessInvocation(accessorRecord, directorExpression => $"{directorExpression}->{helperName}(index, value); return;", "SharpCpp protected field access requires a director-backed instance.");
    }

    private static string RenderNativeProtectedAccessInvocation(
        RecordBinding accessorRecord,
        Func<string, string> renderDirectorBody,
        string errorMessage)
    {
        var writer = new CodeWriter();
        var directorName = GetNativeDirectorClassName(accessorRecord);
        var variableName = $"director_{SanitizeNativeEntrypointName(accessorRecord.CppQualifiedName)}";
        writer.Write($$"""
            if (auto* {{variableName}} = dynamic_cast<{{directorName}}*>(self))
            {
            {{renderDirectorBody(variableName)}}
            }

            """);

        writer.Write($"throw std::runtime_error(\"{errorMessage}\");");
        return writer.ToString();
    }

    private static IReadOnlyList<FunctionBinding> GetAccessibleProtectedMethods(BindingCompilation compilation, RecordBinding record)
    {
        return record.EnumerateAllBaseRecords(compilation)
            .Reverse()
            .Append(record)
            .SelectMany(static candidate => candidate.Members.OfType<FunctionBinding>())
            .Where(static function => !function.IsConstructor && function.AccessModifier == BindingAccessModifier.Protected && !function.HasMetadata(MetadataKeys.IgnoreReason))
            .GroupBy(GetNativeProtectedMethodHelperName, StringComparer.Ordinal)
            .Select(static group => group.Last())
            .ToArray();
    }

    private static IReadOnlyList<FieldBinding> GetAccessibleProtectedFields(BindingCompilation compilation, RecordBinding record)
    {
        return record.EnumerateAllBaseRecords(compilation)
            .Reverse()
            .Append(record)
            .SelectMany(static candidate => candidate.Members.OfType<FieldBinding>())
            .Where(static field => field.AccessModifier == BindingAccessModifier.Protected && !field.HasMetadata(MetadataKeys.IgnoreReason))
            .GroupBy(GetNativeProtectedFieldGetterHelperName, StringComparer.Ordinal)
            .Select(static group => group.Last())
            .ToArray();
    }

    private static IReadOnlyList<FieldBinding> GetInheritedProtectedFields(BindingCompilation compilation, RecordBinding record)
    {
        if (!record.RequiresDirector(compilation))
        {
            return Array.Empty<FieldBinding>();
        }

        return record.EnumerateAllBaseRecords(compilation)
            .Reverse()
            .SelectMany(static candidate => candidate.Members.OfType<FieldBinding>())
            .Where(static field => field.AccessModifier == BindingAccessModifier.Protected && !field.HasMetadata(MetadataKeys.IgnoreReason))
            .GroupBy(GetNativeProtectedFieldGetterHelperName, StringComparer.Ordinal)
            .Select(static group => group.Last())
            .ToArray();
    }

    private static IEnumerable<(RecordBinding AccessorRecord, FieldBinding Field)> GetNativeFieldEntries(BindingCompilation compilation, RecordBinding record)
    {
        foreach (var field in record.Members.OfType<FieldBinding>())
        {
            yield return (record, field);
        }

        foreach (var field in GetInheritedProtectedFields(compilation, record))
        {
            if (record.Members.OfType<FieldBinding>().Any(ownField => string.Equals(GetNativeProtectedFieldGetterHelperName(ownField), GetNativeProtectedFieldGetterHelperName(field), StringComparison.Ordinal)))
            {
                continue;
            }

            yield return (record, field);
        }
    }

    private static string GetNativeFieldGetterEntrypointName(string recordCppTypeName, FieldBinding field)
    {
        return $"sharpcpp_e2e_{SanitizeNativeEntrypointName(recordCppTypeName)}_{field.NativeName}_get";
    }

    private static string GetNativeFieldItemGetterEntrypointName(string recordCppTypeName, FieldBinding field)
    {
        return $"sharpcpp_e2e_{SanitizeNativeEntrypointName(recordCppTypeName)}_{field.NativeName}_item_get";
    }

    private static string GetNativeFieldSetterEntrypointName(string recordCppTypeName, FieldBinding field)
    {
        return $"sharpcpp_e2e_{SanitizeNativeEntrypointName(recordCppTypeName)}_{field.NativeName}_set";
    }

    private static string GetNativeFieldItemSetterEntrypointName(string recordCppTypeName, FieldBinding field)
    {
        return $"sharpcpp_e2e_{SanitizeNativeEntrypointName(recordCppTypeName)}_{field.NativeName}_item_set";
    }

    private enum FieldSetterMode
    {
        ValuePointer,
        HandleAbi,
        BorrowedStorage
    }

    private sealed record NativeFieldInteropPlan(
        string StorageCppType,
        bool BorrowsStorage,
        FieldSetterMode SetterMode)
    {
        public string SetterParameterCppType => SetterMode == FieldSetterMode.HandleAbi
            ? $"arg_t<{StorageCppType}>"
            : $"field_t<{StorageCppType}>";
    }

    private static NativeFieldInteropPlan CreateNativeFieldInteropPlan(BindingCompilation compilation, FieldBinding field)
    {
        var storageCppType = GetNativeFieldCppType(compilation, field);
        var borrowsStorage = ShouldBorrowFieldStorage(compilation, field);
        var setterMode = borrowsStorage
            ? FieldSetterMode.BorrowedStorage
            : field.Type.Kind == BindingTypeKind.Handle
                ? FieldSetterMode.HandleAbi
                : FieldSetterMode.ValuePointer;

        return new NativeFieldInteropPlan(storageCppType, borrowsStorage, setterMode);
    }

    private static string RenderNativeFieldGetInvocation(BindingCompilation compilation, FieldBinding field, NativeFieldInteropPlan plan, string selfExpression, string? fieldOwnerCppTypeName = null)
    {
        if (IsNativeFixedArrayType(compilation, field.Type))
        {
            var arrayAccessExpression = GetNativeFixedArrayFieldAccessExpression(field, selfExpression);
            if (NeedsNativeFixedArrayHandleView(field))
            {
                var handleCppType = GetNativeFixedArrayElementCppType(compilation, field.Type);
                var length = field.Type.FixedArrayLength ?? 0;
                return $$"""
                    auto* view = new {{handleCppType}}[{{length}}]{};
                    for (std::size_t index = 0; index < {{length}}; ++index)
                    {
                        view[index] = &({{arrayAccessExpression}}[index]);
                    }
                    return { view, {{length}} };
                    """;
            }

            return $"return {{ const_cast<void*>(static_cast<const void*>({arrayAccessExpression})), {field.Type.FixedArrayLength ?? 0} }};";
        }

        if (TryGetNativeFieldAccessExpression(field, selfExpression, out var accessExpression))
        {
            if (plan.BorrowsStorage)
            {
                return $"return &({accessExpression});";
            }

            return $$"""
                ReturnMarshaller<{{plan.StorageCppType}}> marshaller;
                return marshaller.ConvertToAbi({{accessExpression}});
                """;
        }

        var fieldOwner = fieldOwnerCppTypeName ?? field.OwningRecordCppTypeName;
        var borrowedStorageTag = plan.BorrowsStorage ? ", std::true_type{}" : string.Empty;
        return $"return MarshallerInvoker::get_field<{plan.StorageCppType}>(&{fieldOwner}::{field.NativeName}, {selfExpression}{borrowedStorageTag});";
    }

    private static string RenderNativeFieldSetInvocation(BindingCompilation compilation, FieldBinding field, NativeFieldInteropPlan plan, string selfExpression, string? fieldOwnerCppTypeName = null)
    {
        if (IsNativeFixedArrayType(compilation, field.Type))
        {
            var arrayAccessExpression = GetNativeFixedArrayFieldAccessExpression(field, selfExpression);
            var elementCppType = GetNativeFixedArrayElementCppType(compilation, field.Type);
            var length = field.Type.FixedArrayLength ?? 0;
            if (NeedsNativeFixedArrayHandleView(field))
            {
                return $$"""
                    if (value.handle == nullptr || value.length != {{length}})
                    {
                        throw std::runtime_error("SharpCpp fixed array field assignment length mismatch.");
                    }

                    auto* source = static_cast<{{elementCppType}}*>(value.handle);
                    for (std::size_t index = 0; index < {{length}}; ++index)
                    {
                        if (source[index] == nullptr)
                        {
                            throw std::runtime_error("SharpCpp fixed array field assignment encountered a null handle.");
                        }

                        {{arrayAccessExpression}}[index] = *source[index];
                    }
                    """;
            }

            return $$"""
                if (value.handle == nullptr || value.length != {{length}})
                {
                    throw std::runtime_error("SharpCpp fixed array field assignment length mismatch.");
                }

                auto* source = static_cast<{{elementCppType}}*>(value.handle);
                ParameterMarshaller<{{elementCppType}}> marshaller;
                for (std::size_t index = 0; index < {{length}}; ++index)
                {
                    {{arrayAccessExpression}}[index] = marshaller.ConvertToCpp(source[index]);
                }
                """;
        }

        if (TryGetNativeFieldAccessExpression(field, selfExpression, out var accessExpression))
        {
            if (plan.SetterMode == FieldSetterMode.BorrowedStorage)
            {
                return $$"""
                    if (value == nullptr)
                    {
                        if constexpr (detail::has_clear_v<{{plan.StorageCppType}}>)
                        {
                            {{accessExpression}}.clear();
                        }
                        else
                        {
                            throw std::runtime_error("SharpCpp borrowed field setter cannot clear this field type.");
                        }
                    }
                    else
                    {
                        {{accessExpression}} = std::move(*value);
                    }
                    """;
            }

            var valueExpression = plan.SetterMode == FieldSetterMode.ValuePointer ? "*value" : "value";
            if (ShouldCastDirectNativeFieldSetValue(field, plan))
            {
                valueExpression = $"static_cast<decltype({accessExpression})>({valueExpression})";
            }

            return $"{accessExpression} = {valueExpression};";
        }

        var fieldOwner = fieldOwnerCppTypeName ?? field.OwningRecordCppTypeName;
        var borrowedStorageTag = plan.SetterMode == FieldSetterMode.BorrowedStorage ? ", std::true_type{}" : string.Empty;
        return $"MarshallerInvoker::set_field<{plan.StorageCppType}>(&{fieldOwner}::{field.NativeName}, {selfExpression}, value{borrowedStorageTag});";
    }

    private static string RenderNativeFixedArrayItemGetInvocation(BindingCompilation compilation, FieldBinding field, string selfExpression)
    {
        var arrayAccessExpression = GetNativeFixedArrayFieldAccessExpression(field, selfExpression);
        var elementType = field.Type.FixedArrayElementType
            ?? throw new InvalidOperationException($"Fixed array field '{field.ManagedName}' is missing element type metadata.");

        if (elementType.IsFixedArray)
        {
            return $"return {{ const_cast<void*>(static_cast<const void*>({arrayAccessExpression}[index])), {elementType.FixedArrayLength ?? 0} }};";
        }

        var elementCppType = GetNativeFixedArrayElementCppType(compilation, elementType);
        var valueExpression = field.CppTypeName.Contains('*', StringComparison.Ordinal)
            ? $"{arrayAccessExpression}[index]"
            : $"&({arrayAccessExpression}[index])";
        return $$"""
            ReturnMarshaller<{{elementCppType}}> marshaller;
            return marshaller.ConvertToAbi({{valueExpression}});
            """;
    }

    private static string RenderNativeFixedArrayItemSetInvocation(BindingCompilation compilation, FieldBinding field, string selfExpression)
    {
        var arrayAccessExpression = GetNativeFixedArrayFieldAccessExpression(field, selfExpression);
        var elementType = field.Type.FixedArrayElementType
            ?? throw new InvalidOperationException($"Fixed array field '{field.ManagedName}' is missing element type metadata.");

        if (elementType.IsFixedArray)
        {
            var nestedElementCppType = GetNativeFixedArrayElementCppType(compilation, elementType);
            var nestedLength = elementType.FixedArrayLength ?? 0;
            return $$"""
                if (value.handle == nullptr || value.length != {{nestedLength}})
                {
                    throw std::runtime_error("SharpCpp fixed array element assignment length mismatch.");
                }

                auto& target = {{arrayAccessExpression}}[index];
                auto* source = reinterpret_cast<{{nestedElementCppType}} (*)[{{nestedLength}}]>(value.handle);
                for (std::size_t nestedIndex = 0; nestedIndex < {{nestedLength}}; ++nestedIndex)
                {
                    target[nestedIndex] = (*source)[nestedIndex];
                }
                """;
        }

        var elementCppType = GetNativeFixedArrayElementCppType(compilation, elementType);
        if (field.CppTypeName.Contains('*', StringComparison.Ordinal))
        {
            return $$"""
                ParameterMarshaller<{{elementCppType}}> marshaller;
                {{arrayAccessExpression}}[index] = marshaller.ConvertToCpp(value);
                """;
        }

        return $$"""
            ParameterMarshaller<{{elementCppType}}> marshaller;
            auto* source = marshaller.ConvertToCpp(value);
            if (source == nullptr)
            {
                throw std::runtime_error("SharpCpp fixed array element assignment encountered a null handle.");
            }

            {{arrayAccessExpression}}[index] = *source;
            """;
    }

    private static string GetNativeFixedArrayItemGetterReturnType(BindingCompilation compilation, FieldBinding field)
    {
        var elementType = field.Type.FixedArrayElementType
            ?? throw new InvalidOperationException($"Fixed array field '{field.ManagedName}' is missing element type metadata.");
        return elementType.IsFixedArray
            ? "NativeArrayAbi"
            : $"ret_t<{GetNativeFixedArrayElementCppType(compilation, elementType)}>";
    }

    private static string GetNativeFixedArrayItemSetterParameterType(BindingCompilation compilation, FieldBinding field)
    {
        var elementType = field.Type.FixedArrayElementType
            ?? throw new InvalidOperationException($"Fixed array field '{field.ManagedName}' is missing element type metadata.");
        return elementType.IsFixedArray
            ? "NativeArrayAbi"
            : $"arg_t<{GetNativeFixedArrayElementCppType(compilation, elementType)}>";
    }

    private static bool NeedsNativeFixedArrayHandleView(FieldBinding field)
    {
        var elementType = field.Type.FixedArrayElementType;
        return elementType?.Kind == BindingTypeKind.Handle && !field.CppTypeName.Contains('*', StringComparison.Ordinal);
    }

    private static string GetNativeFixedArrayFieldAccessExpression(FieldBinding field, string selfExpression)
    {
        return TryGetNativeFieldAccessExpression(field, selfExpression, out var accessExpression)
            ? accessExpression
            : $"{selfExpression}->{field.NativeName}";
    }

    private static bool TryGetNativeFieldAccessExpression(FieldBinding field, string selfExpression, out string accessExpression)
    {
        if (field.TryGetMetadata<string>(MetadataKeys.NativeFieldAccessExpression, out var fieldAccessPath) &&
            !string.IsNullOrWhiteSpace(fieldAccessPath))
        {
            accessExpression = $"{selfExpression}->{fieldAccessPath}";
            return true;
        }

        accessExpression = string.Empty;
        return false;
    }

    private static bool ShouldCastDirectNativeFieldSetValue(FieldBinding field, NativeFieldInteropPlan plan)
    {
        return plan.SetterMode == FieldSetterMode.ValuePointer &&
            field.IsPointer &&
            field.Type.Kind == BindingTypeKind.Primitive &&
            field.Type.PrimitiveKind == BindingPrimitiveKind.IntPtr &&
            string.Equals(plan.StorageCppType, BindingType.IntPtr.CppTypeName, StringComparison.Ordinal);
    }

    private static string GetNativeProtectedMethodHelperName(FunctionBinding method)
    {
        return $"__call_{SanitizeManagedToken(method.ImportMethodName)}";
    }

    private static string GetNativeProtectedFieldGetterHelperName(FieldBinding field)
    {
        return $"__get_{SanitizeManagedToken(field.ManagedName)}";
    }

    private static string GetNativeProtectedFieldItemGetterHelperName(FieldBinding field)
    {
        return $"__get_{SanitizeManagedToken(field.ManagedName)}_item";
    }

    private static string GetNativeProtectedFieldSetterHelperName(FieldBinding field)
    {
        return $"__set_{SanitizeManagedToken(field.ManagedName)}";
    }

    private static string GetNativeProtectedFieldItemSetterHelperName(FieldBinding field)
    {
        return $"__set_{SanitizeManagedToken(field.ManagedName)}_item";
    }

    private static void ValidateProtectedMemberSupport(FunctionBinding method)
    {
        if (method.Parameters.Any(parameter => parameter.Direction is not BindingParameterDirection.In || parameter.AbiKind is not BindingParameterAbiKind.Value))
        {
            throw new NotSupportedException($"Protected member '{method.ManagedName}' only supports in-value parameters in this slice.");
        }
    }

    private static string RenderNativeCallbaseHelper(RecordBinding record, VirtualMethodSlotBinding slot)
    {
        var method = slot.Method;
        var parameterList = string.Join(", ", method.Parameters.Select(parameter => $"{parameter.CppTypeName} {parameter.NativeName}"));
        var arguments = string.Join(", ", method.Parameters.Select(static parameter => parameter.NativeName));
        var directorName = GetNativeDirectorClassName(record);
        var writer = new CodeWriter();

        writer.Write($$"""
            static {{method.CppReturnTypeName}} {{GetNativeCallbaseHelperName(method)}}({{record.CppQualifiedName}}* self{{(method.Parameters.Count > 0 ? $", {parameterList}" : string.Empty)}})
            {
            auto* director = static_cast<{{directorName}}*>(self);
            """);

        if (method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
        {
            writer.WriteLine($"    director->{slot.NativeImplementationRecord.CppQualifiedName}::{method.NativeName}({arguments});");
        }
        else
        {
            writer.WriteLine($"    return director->{slot.NativeImplementationRecord.CppQualifiedName}::{method.NativeName}({arguments});");
        }

        writer.Write(
            """
            }
            """);
        return writer.ToString();
    }

    private static string GetNativeCallbaseHelperName(FunctionBinding method)
    {
        return $"__callbase_{SanitizeManagedToken(method.ImportMethodName)}";
    }

    private static string GetNativeCallbaseEntrypointName(RecordBinding record, FunctionBinding method)
    {
        return $"sharpcpp_e2e_{record.NativeName}_callbase_{method.ImportMethodName}";
    }

    private static string GetManagedCallbaseImportMethodName(FunctionBinding function)
    {
        return $"__CallBase_{function.ImportMethodName}";
    }


    private static string RenderManagedWrapperMethod(BindingCompilation compilation, FunctionBinding function)
    {
        var writer = new CodeWriter();
        WriteDocumentationComment(writer, function);
        var returnTypeName = GetManagedPublicReturnTypeName(function.ReturnType, compilation);
        var importMethodName = GetManagedImportMethodName(function);

        if (HasUndefaultableParams(function))
        {
            WriteUndefaultableOverloads(compilation, writer, function, importMethodName, []);
            return writer.ToString();
        }

        if (HasUnexpressibleDefaultParams(function))
        {
            WriteManagedUnexpressibleDefaultConvenienceOverloads(
                writer,
                compilation,
                function,
                signaturePrefix: $"public static {returnTypeName} {function.ManagedName}",
                invocationPrefix: string.Empty,
                invocationTarget: function.ManagedName,
                returnTypeName: returnTypeName,
                isConstructor: false);
        }

        var parameterList = RenderManagedPublicParameterList(function.Parameters, compilation);
        var invocationPlan = BuildManagedInvocationPlan(compilation, function.Parameters);
        var invokeArguments = string.Join(", ", invocationPlan.Arguments);

        writer.Write($$"""
            public static {{returnTypeName}} {{function.ManagedName}}({{parameterList}})
            {
            """);

        foreach (var statement in invocationPlan.PreCallStatements)
        {
            writer.Write(statement);
        }

        if (HasNativeDefaultDispatchParams(function))
        {
            WriteUnexpressibleDefaultCascade(compilation, writer, function, importMethodName, []);
        }
        else
        {

        if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
        {
            writer.Write($$"""
                __PInvoke.{{importMethodName}}({{invokeArguments}});
                """);

            foreach (var statement in invocationPlan.PostCallStatements)
            {
                writer.Write(statement);
            }
        }
        else if (function.ReturnType.Kind == BindingTypeKind.Handle)
        {
            writer.Write($$"""
                var __result = __PInvoke.{{importMethodName}}({{invokeArguments}});
                """);

            foreach (var statement in invocationPlan.PostCallStatements)
            {
                writer.Write(statement);
            }

            writer.Write($$"""
                return {{BuildManagedHandleReturnResultExpression(compilation, function.ReturnType, "__result", ReturnsOwnedHandle(function))}};
                """);
        }
        else if (function.ReturnType.MarshallerTypeName is not null)
        {
            WriteManagedReturnWithMarshallerConversion(compilation, writer, function, importMethodName, invokeArguments, invocationPlan);
        }
        else
        {
            writer.Write($$"""
                var __result = __PInvoke.{{importMethodName}}({{invokeArguments}});
                """);

            foreach (var statement in invocationPlan.PostCallStatements)
            {
                writer.Write(statement);
            }

            writer.Write(
                """
                return __result;
                """);
        }

        }

        writer.Write(
            """
            }
            """);
        return writer.ToString();
    }

    private static string RenderManagedImportDeclaration(BindingCompilation compilation, FunctionBinding function)
    {
        var writer = new CodeWriter();
        var parameterList = string.Join(
            ", ",
            function.Parameters.Select(parameter => RenderManagedImportParameter(parameter, compilation)));
        var returnTypeName = GetManagedImportReturnTypeName(compilation, function.ReturnType);
        var returnAttribute = GetManagedImportReturnAttribute(compilation, function.ReturnType, ReturnsOwnedHandle(function));
        var importMethodName = GetManagedImportMethodName(function);
        var unsafeModifier = parameterList.Contains('*', StringComparison.Ordinal) ? "unsafe " : string.Empty;
        var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static {unsafeModifier}partial {returnTypeName} {importMethodName}({parameterList});"
            : $"{returnAttribute}\ninternal static {unsafeModifier}partial {returnTypeName} {importMethodName}({parameterList});";

        writer.Write(
            $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{function.EntryPointName}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            {{importMethodLine}}
            """);
        return writer.ToString();
    }

    private static IEnumerable<ParameterBinding> CollectCallbackParameters(
        IEnumerable<RecordBinding> records,
        IEnumerable<FunctionBinding> functions)
    {
        return functions
            .Where(static function => !function.IsIgnored())
            .SelectMany(static function => function.Parameters.Where(static parameter => parameter.Type.IsCallback))
            .Concat(records.SelectMany(static record => record.Members
                .OfType<FunctionBinding>()
                .Where(static function => !function.IsIgnored())
                .SelectMany(static function => function.Parameters.Where(static parameter => parameter.Type.IsCallback))))
            .Concat(records.SelectMany(static record => record.Members
                .OfType<FunctionBinding>()
                .Where(static function => !function.IsIgnored() && function.IsGeneric)
                .SelectMany(function => CollectGenericMethodCallbackParameters(record, function))));
    }

    private static IEnumerable<ParameterBinding> CollectGenericMethodCallbackParameters(RecordBinding record, FunctionBinding function)
    {
        var methodParams = (IReadOnlyList<BindingGenericParameter>)function.GenericParameters.ToList();
        var recordInstantiations = record.IsGeneric
            ? record.SupportedGenericInstantiations
            : [EmptyRecordGenericInstantiation()];

        foreach (var recordInstantiation in recordInstantiations)
        {
            foreach (var methodInstantiation in function.SupportedGenericInstantiations)
            {
                foreach (var parameter in function.Parameters)
                {
                    var concreteType = SubstituteGenericType(record, parameter.Type, recordInstantiation, methodParams, methodInstantiation);
                    if (concreteType.IsCallback)
                    {
                        yield return CreateConcreteParameter(parameter, concreteType);
                    }
                }
            }
        }
    }

    private static string RenderManagedCallbackSupport(BindingCompilation compilation, IReadOnlyList<ParameterBinding> callbackParameters)
    {
        var writer = new CodeWriter();
        var delegateDeclarations = CollectManagedCallbackDelegateDeclarations(callbackParameters).ToArray();
        var usages = CollectManagedCallbackUsages(compilation, callbackParameters).ToArray();

        foreach (var declaration in delegateDeclarations.OrderBy(static declaration => declaration.DeclarationName, StringComparer.Ordinal))
        {
            writer.Write(RenderManagedCallbackDelegateDeclaration(compilation, declaration));
        }

        if (usages.Length > 0)
        {
            if (delegateDeclarations.Length > 0)
            {
                writer.WriteLine();
            }

            writer.Write(
                $$"""
                [StructLayout(LayoutKind.Sequential)]
                internal readonly unsafe struct __SharpCppManagedCallbackHandleAbi
                {
                    public __SharpCppManagedCallbackHandleAbi(nint state, nint callback)
                    {
                        State = state;
                        Callback = callback;
                    }

                    public nint State { get; }

                    public nint Callback { get; }
                }

                private static unsafe void __RegisterManagedCallbackRelease()
                {
                    __PInvoke.__SetManagedCallbackRelease(&__ReleaseManagedCallbackState);
                }

                [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
                private static void __ReleaseManagedCallbackState(nint state)
                {
                    if (state != 0)
                    {
                        global::System.Runtime.InteropServices.GCHandle.FromIntPtr(state).Free();
                    }
                }

                internal static partial class __PInvoke
                {
                    [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetManagedCallbackReleaseRegistrationEntryPointName()}}", StringMarshalling = StringMarshalling.Utf8)]
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    internal static unsafe partial void __SetManagedCallbackRelease(delegate* unmanaged[Cdecl]<nint, void> release);
                }
                """);

            foreach (var usage in usages.OrderBy(static usage => usage.MarshallerDeclarationName, StringComparer.Ordinal))
            {
                writer.WriteLine();
                writer.Write(RenderManagedCallbackMarshallerHelper(compilation, usage));
            }
        }

        return writer.ToString();
    }

    private static IEnumerable<ManagedCallbackDelegateDeclaration> CollectManagedCallbackDelegateDeclarations(IReadOnlyList<ParameterBinding> callbackParameters)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in callbackParameters.Where(static parameter => parameter.UseCallbackDelegate))
        {
            var declarationName = GetGeneratedCallbackDelegateDeclarationName(parameter);
            if (declarationName is null)
            {
                continue;
            }

            var signatureKey = GetCallbackSignatureKey(parameter.Type);
            if (seen.TryGetValue(declarationName, out var existingSignatureKey))
            {
                if (!string.Equals(existingSignatureKey, signatureKey, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Callback delegate '{declarationName}' is requested with incompatible callback signatures.");
                }

                continue;
            }

            seen.Add(declarationName, signatureKey);
            yield return new ManagedCallbackDelegateDeclaration(declarationName, parameter.Type);
        }
    }

    private static IEnumerable<ManagedCallbackUsage> CollectManagedCallbackUsages(BindingCompilation compilation, IReadOnlyList<ParameterBinding> callbackParameters)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in callbackParameters.Where(static parameter => parameter.Type.ManagedMarshaller is null))
        {
            var usage = GetManagedCallbackUsage(compilation, parameter);
            if (seen.Add($"{usage.MarshallerDeclarationName}|{usage.ManagedTypeName}|{GetCallbackSignatureKey(usage.CallbackType)}"))
            {
                yield return usage;
            }
        }
    }

    private static string RenderManagedCallbackDelegateDeclaration(BindingCompilation compilation, ManagedCallbackDelegateDeclaration declaration)
    {
        var callbackReturnType = declaration.CallbackType.CallbackReturnType ?? BindingType.Void;
        var returnType = callbackReturnType.PrimitiveKind == BindingPrimitiveKind.Void
            ? "void"
            : GetManagedCallbackDelegateValueTypeName(compilation, callbackReturnType);
        var parameters = declaration.CallbackType.CallbackParameterTypes
            .Select((type, index) => $"{GetManagedCallbackDelegateValueTypeName(compilation, type)} arg{index}");
        return $$"""
            public delegate {{returnType}} {{declaration.DeclarationName}}({{string.Join(", ", parameters)}});
            """;
    }

    private static string RenderManagedCallbackMarshallerHelper(BindingCompilation compilation, ManagedCallbackUsage usage)
    {
        var callbackType = usage.CallbackType;
        var declarationName = usage.MarshallerDeclarationName;
        var callbackManagedType = usage.ManagedTypeName;
        var callbackReturnType = callbackType.CallbackReturnType ?? BindingType.Void;
        var callbackReturnAbiType = callbackReturnType.PrimitiveKind == BindingPrimitiveKind.Void
            ? "void"
            : GetManagedImportTypeName(callbackReturnType, compilation);
        var parameters = callbackType.CallbackParameterTypes
            .Select((type, index) => (type, index, abiType: GetManagedImportTypeName(type, compilation)))
            .ToArray();
        var abiParameterTypes = string.Join(", ", parameters.Select(static item => item.abiType));
        var invokeSignature = callbackReturnType.PrimitiveKind == BindingPrimitiveKind.Void
            ? $"delegate* unmanaged[Cdecl]<nint{(abiParameterTypes.Length > 0 ? $", {abiParameterTypes}" : string.Empty)}, void>"
            : $"delegate* unmanaged[Cdecl]<nint{(abiParameterTypes.Length > 0 ? $", {abiParameterTypes}" : string.Empty)}, {callbackReturnAbiType}>";
        var invokeArguments = string.Join(", ", parameters.Select(item => BuildManagedCallbackInvocationArgument(compilation, item.type, $"arg{item.index}")));
        var invokeParameterList = string.Join(", ", new[] { "nint state" }.Concat(parameters.Select(static item => $"{item.abiType} arg{item.index}")));
        var invokeBody = callbackReturnType.PrimitiveKind == BindingPrimitiveKind.Void
            ? $$"""
                      callback({{invokeArguments}});
                      return;
              """
            : $$"""
                      var result = callback({{invokeArguments}});
                      return {{BuildManagedCallbackReturnExpression(compilation, callbackReturnType, "result")}};
              """;

        return $$"""
            [CustomMarshaller(typeof({{callbackManagedType}}), MarshalMode.ManagedToUnmanagedIn, typeof({{declarationName}}))]
            internal static unsafe class {{declarationName}}
            {
                static {{declarationName}}()
                {
                    __RegisterManagedCallbackRelease();
                }

                public static __SharpCppManagedCallbackHandleAbi ConvertToUnmanaged({{callbackManagedType}}? managed)
                {
                    if (managed is null)
                    {
                        return default;
                    }

                    var handle = global::System.Runtime.InteropServices.GCHandle.Alloc(managed);
                    return new __SharpCppManagedCallbackHandleAbi(
                        global::System.Runtime.InteropServices.GCHandle.ToIntPtr(handle),
                        (nint)({{invokeSignature}})&Invoke);
                }

                [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
                private static {{callbackReturnAbiType}} Invoke({{invokeParameterList}})
                {
                    var callback = ({{callbackManagedType}})global::System.Runtime.InteropServices.GCHandle.FromIntPtr(state).Target!;
                    try
                    {
              {{invokeBody}}
                    }
                    catch (global::System.Exception exception)
                    {
                        global::System.Environment.FailFast("SharpCpp managed callback threw an exception.", exception);
              {{(callbackReturnType.PrimitiveKind == BindingPrimitiveKind.Void ? "return;" : "return default;")}}
                    }
                }
            }
            """;
    }

    private static string GetManagedCallbackReleaseRegistrationEntryPointName()
    {
        return "sharpcpp_set_managed_callback_release";
    }

    private static string BuildManagedCallbackInvocationArgument(BindingCompilation compilation, BindingType type, string valueExpression)
    {
        return type.Kind == BindingTypeKind.Handle
            ? BuildManagedHandleWrap(compilation, type, valueExpression, ownsHandle: false)
            : valueExpression;
    }

    private static string BuildManagedCallbackReturnExpression(BindingCompilation compilation, BindingType type, string valueExpression)
    {
        return type.Kind == BindingTypeKind.Handle
            ? $"global::SharpCpp.Support.NativeObject.GetHandle({valueExpression})"
            : valueExpression;
    }

    private static string GetManagedCallbackMarshallerTypeName(BindingCompilation compilation, ParameterBinding parameter)
    {
        return $"global::{compilation.ManagedNamespace}.{compilation.ManagedClassName}.{GetManagedCallbackUsage(compilation, parameter).MarshallerDeclarationName}";
    }

    private static ManagedCallbackUsage GetManagedCallbackUsage(BindingCompilation compilation, ParameterBinding parameter)
    {
        var managedTypeName = GetManagedCallbackParameterTypeName(compilation, parameter);
        var defaultTypeName = GetManagedImportTypeName(parameter.Type, compilation);
        var declarationName = string.Equals(managedTypeName, defaultTypeName, StringComparison.Ordinal)
            ? GetManagedCallbackMarshallerDeclarationName(compilation, parameter.Type)
            : $"{GetManagedCallbackMarshallerDeclarationName(compilation, parameter.Type)}_{SanitizeManagedToken(managedTypeName)}";
        return new ManagedCallbackUsage(parameter.Type, managedTypeName, declarationName);
    }

    private static string GetManagedCallbackParameterTypeName(BindingCompilation compilation, ParameterBinding parameter)
    {
        if (!parameter.Type.IsCallback)
        {
            return GetManagedPublicReturnTypeName(parameter.Type, compilation);
        }

        if (!parameter.UseCallbackDelegate)
        {
            return GetManagedPublicReturnTypeName(parameter.Type, compilation);
        }

        if (!string.IsNullOrWhiteSpace(parameter.CallbackDelegateTypeName))
        {
            return IsExternalCallbackDelegateTypeName(parameter.CallbackDelegateTypeName!)
                ? NormalizeExternalCallbackDelegateTypeName(parameter.CallbackDelegateTypeName!)
                : GetQualifiedGeneratedCallbackDelegateTypeName(compilation, parameter.CallbackDelegateTypeName!);
        }

        return GetQualifiedGeneratedCallbackDelegateTypeName(compilation, GetGeneratedCallbackDelegateName(parameter));
    }

    private static string? GetGeneratedCallbackDelegateDeclarationName(ParameterBinding parameter)
    {
        if (!parameter.UseCallbackDelegate)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(parameter.CallbackDelegateTypeName) && IsExternalCallbackDelegateTypeName(parameter.CallbackDelegateTypeName!))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(parameter.CallbackDelegateTypeName)
            ? GetGeneratedCallbackDelegateName(parameter)
            : parameter.CallbackDelegateTypeName!;
    }

    private static string GetGeneratedCallbackDelegateName(ParameterBinding parameter)
    {
        var function = parameter.Parent as FunctionBinding;
        var ownerName = function?.FindOwningRecord()?.ManagedName ?? "Global";
        var functionName = function?.ImportMethodName ?? function?.ManagedName ?? "Callback";
        return $"{SanitizeManagedToken(ownerName)}_{SanitizeManagedToken(functionName)}_{SanitizeManagedToken(parameter.ManagedName)}Callback";
    }

    private static string GetQualifiedGeneratedCallbackDelegateTypeName(BindingCompilation compilation, string declarationName)
    {
        return $"global::{compilation.ManagedNamespace}.{compilation.ManagedClassName}.{declarationName}";
    }

    private static bool IsExternalCallbackDelegateTypeName(string value)
    {
        return value.StartsWith("global::", StringComparison.Ordinal) ||
            value.Contains('.', StringComparison.Ordinal);
    }

    private static string NormalizeExternalCallbackDelegateTypeName(string value)
    {
        return value.StartsWith("global::", StringComparison.Ordinal)
            ? value
            : $"global::{value}";
    }

    private static string GetManagedCallbackDelegateValueTypeName(BindingCompilation compilation, BindingType type)
    {
        return type.Kind == BindingTypeKind.Handle
            ? GetManagedWrapperTypeName(type, compilation)
            : GetManagedImportTypeName(type, compilation);
    }

    private static string GetCallbackSignatureKey(BindingType type)
    {
        var returnType = type.CallbackReturnType?.UnaliasedManagedTypeName ?? "void";
        return string.Join("|", returnType, string.Join(";", type.CallbackParameterTypes.Select(static parameter => parameter.UnaliasedManagedTypeName)));
    }

    private static string GetManagedCallbackMarshallerDeclarationName(BindingType type)
    {
        var returnToken = type.CallbackReturnType is null ? "void" : type.CallbackReturnType.ManagedTypeName;
        var parameterToken = type.CallbackParameterTypes.Count == 0
            ? "none"
            : string.Join("_", type.CallbackParameterTypes.Select(static parameter => parameter.ManagedTypeName));
        return $"__CallbackMarshaller_{SanitizeManagedToken(returnToken)}_{SanitizeManagedToken(parameterToken)}";
    }

    private static string GetManagedCallbackMarshallerDeclarationName(BindingCompilation compilation, BindingType type)
    {
        var returnToken = type.CallbackReturnType is null ? "void" : GetManagedPublicReturnTypeName(type.CallbackReturnType, compilation);
        var parameterToken = type.CallbackParameterTypes.Count == 0
            ? "none"
            : string.Join("_", type.CallbackParameterTypes.Select(parameter => GetManagedPublicReturnTypeName(parameter, compilation)));
        return $"__CallbackMarshaller_{SanitizeManagedToken(returnToken)}_{SanitizeManagedToken(parameterToken)}";
    }

    private static string RenderManagedHandleReturnMarshallerHelpers(BindingCompilation compilation, RecordBinding record, string recordDeclarationName)
    {
        if (IsSharedNativeArrayRecord(record))
        {
            return string.Empty;
        }

        if (IsGenericArrayRecord(record))
        {
            return RenderManagedGenericArrayMarshallerHelpers(compilation, record, recordDeclarationName);
        }

        if (!CanUseUnifiedHandleMarshaller(record))
        {
            return RenderManagedGenericHandleMarshallerHelpers(compilation, record, recordDeclarationName);
        }

        var qualifiedRecordTypeName = GetQualifiedManagedRecordDeclarationTypeName(compilation, record, recordDeclarationName);
        var qualifiedOpenRecordTypeName = GetQualifiedManagedOpenRecordTypeName(compilation, record);
        var marshallerDeclarationName = GetHandleMarshallerDeclarationName(record);
        var borrowedMarshallerTypeName = GetQualifiedUnifiedHandleMarshallerTypeName(compilation, record, BorrowedNativeHandleFullName);
        var ownedMarshallerTypeName = GetQualifiedUnifiedHandleMarshallerTypeName(compilation, record, OwnedNativeHandleFullName);
        var genericConstraints = GetHandleMarshallerGenericConstraints();

        return $$"""
            [CustomMarshaller(typeof({{qualifiedOpenRecordTypeName}}), MarshalMode.ManagedToUnmanagedOut, typeof({{borrowedMarshallerTypeName}}))]
            [CustomMarshaller(typeof({{qualifiedOpenRecordTypeName}}), MarshalMode.ManagedToUnmanagedOut, typeof({{ownedMarshallerTypeName}}))]
            [CustomMarshaller(typeof({{qualifiedOpenRecordTypeName}}), MarshalMode.ManagedToUnmanagedIn, typeof({{borrowedMarshallerTypeName}}))]
            internal static class {{marshallerDeclarationName}}{{genericConstraints}}
            {
                public static nint ConvertToUnmanaged({{qualifiedRecordTypeName}} managed)
                {
                    return global::SharpCpp.Support.NativeObject.GetHandle(managed);
                }

                public static {{qualifiedRecordTypeName}} ConvertToManaged(nint unmanaged)
                {
                    return {{qualifiedRecordTypeName}}.CreateManagedWrapper(unmanaged, ownsHandle: TOwnership.OwnsHandle);
                }
            }
            """;
    }

    private static string RenderManagedGenericArrayMarshallerHelpers(BindingCompilation compilation, RecordBinding record, string recordDeclarationName)
    {
        return IsGenericDynamicArrayRecord(record)
            ? RenderManagedGenericDynamicArrayMarshallerHelpers(compilation, record, recordDeclarationName)
            : RenderManagedGenericFixedArrayMarshallerHelpers(compilation, record, recordDeclarationName);
    }

    private static string RenderManagedGenericFixedArrayMarshallerHelpers(BindingCompilation compilation, RecordBinding record, string recordDeclarationName)
    {
        var qualifiedRecordTypeName = GetQualifiedManagedRecordDeclarationTypeName(compilation, record, recordDeclarationName);
        var qualifiedOpenRecordTypeName = GetQualifiedManagedOpenRecordTypeName(compilation, record);
        var inMarshallerDeclarationName = GetHandleInMarshallerDeclarationName(record);
        var inMarshallerTypeName = GetQualifiedHandleInMarshallerTypeName(compilation, record);
        var borrowedMarshallerDeclarationName = GetHandleReturnMarshallerDeclarationName(record, ownsHandle: false);
        var borrowedMarshallerTypeName = GetQualifiedHandleReturnMarshallerTypeName(compilation, record, ownsHandle: false);
        var ownedMarshallerDeclarationName = GetHandleReturnMarshallerDeclarationName(record, ownsHandle: true);
        var ownedMarshallerTypeName = GetQualifiedHandleReturnMarshallerTypeName(compilation, record, ownsHandle: true);
        var genericConstraints = GetGenericRecordConstraints(record);

        return $$"""
            [CustomMarshaller(typeof({{qualifiedOpenRecordTypeName}}), MarshalMode.ManagedToUnmanagedIn, typeof({{inMarshallerTypeName}}))]
            internal static class {{inMarshallerDeclarationName}}{{genericConstraints}}
            {
                public static global::SharpCpp.Support.NativeArrayAbi ConvertToUnmanaged({{qualifiedRecordTypeName}} managed)
                {
                    global::System.ArgumentNullException.ThrowIfNull(managed);
                    return managed.ToNativeArrayAbi();
                }
            }

            [CustomMarshaller(typeof({{qualifiedOpenRecordTypeName}}), MarshalMode.ManagedToUnmanagedOut, typeof({{borrowedMarshallerTypeName}}))]
            internal static class {{borrowedMarshallerDeclarationName}}{{genericConstraints}}
            {
                public static {{qualifiedRecordTypeName}} ConvertToManaged(global::SharpCpp.Support.NativeArrayAbi unmanaged)
                {
                    return {{qualifiedRecordTypeName}}.CreateManagedWrapper(unmanaged, ownsHandle: false);
                }
            }

            [CustomMarshaller(typeof({{qualifiedOpenRecordTypeName}}), MarshalMode.ManagedToUnmanagedOut, typeof({{ownedMarshallerTypeName}}))]
            internal static class {{ownedMarshallerDeclarationName}}{{genericConstraints}}
            {
                public static {{qualifiedRecordTypeName}} ConvertToManaged(global::SharpCpp.Support.NativeArrayAbi unmanaged)
                {
                    return {{qualifiedRecordTypeName}}.CreateManagedWrapper(unmanaged, ownsHandle: true);
                }
            }
            """;
    }

    private static string RenderManagedGenericDynamicArrayMarshallerHelpers(BindingCompilation compilation, RecordBinding record, string recordDeclarationName)
    {
        var qualifiedRecordTypeName = GetQualifiedManagedRecordDeclarationTypeName(compilation, record, recordDeclarationName);
        var qualifiedOpenRecordTypeName = GetQualifiedManagedOpenRecordTypeName(compilation, record);
        var inMarshallerDeclarationName = GetHandleInMarshallerDeclarationName(record);
        var inMarshallerTypeName = GetQualifiedHandleInMarshallerTypeName(compilation, record);
        var borrowedMarshallerDeclarationName = GetHandleReturnMarshallerDeclarationName(record, ownsHandle: false);
        var borrowedMarshallerTypeName = GetQualifiedHandleReturnMarshallerTypeName(compilation, record, ownsHandle: false);
        var ownedMarshallerDeclarationName = GetHandleReturnMarshallerDeclarationName(record, ownsHandle: true);
        var ownedMarshallerTypeName = GetQualifiedHandleReturnMarshallerTypeName(compilation, record, ownsHandle: true);
        var genericConstraints = GetGenericRecordConstraints(record);

        return $$"""
            [CustomMarshaller(typeof({{qualifiedOpenRecordTypeName}}), MarshalMode.ManagedToUnmanagedIn, typeof({{inMarshallerTypeName}}))]
            internal static class {{inMarshallerDeclarationName}}{{genericConstraints}}
            {
                public static nint ConvertToUnmanaged({{qualifiedRecordTypeName}} managed)
                {
                    return global::SharpCpp.Support.NativeObject.GetHandle(managed);
                }
            }

            [CustomMarshaller(typeof({{qualifiedOpenRecordTypeName}}), MarshalMode.ManagedToUnmanagedOut, typeof({{borrowedMarshallerTypeName}}))]
            internal static class {{borrowedMarshallerDeclarationName}}{{genericConstraints}}
            {
                public static {{qualifiedRecordTypeName}} ConvertToManaged(nint unmanaged)
                {
                    return {{qualifiedRecordTypeName}}.CreateManagedWrapper(unmanaged, ownsHandle: false);
                }
            }

            [CustomMarshaller(typeof({{qualifiedOpenRecordTypeName}}), MarshalMode.ManagedToUnmanagedOut, typeof({{ownedMarshallerTypeName}}))]
            internal static class {{ownedMarshallerDeclarationName}}{{genericConstraints}}
            {
                public static {{qualifiedRecordTypeName}} ConvertToManaged(nint unmanaged)
                {
                    return {{qualifiedRecordTypeName}}.CreateManagedWrapper(unmanaged, ownsHandle: true);
                }
            }
            """;
    }

    private static string RenderManagedGenericHandleMarshallerHelpers(BindingCompilation compilation, RecordBinding record, string recordDeclarationName)
    {
        var qualifiedRecordTypeName = GetQualifiedManagedRecordDeclarationTypeName(compilation, record, recordDeclarationName);
        var qualifiedOpenRecordTypeName = GetQualifiedManagedOpenRecordTypeName(compilation, record);
        var inMarshallerDeclarationName = GetHandleInMarshallerDeclarationName(record);
        var inMarshallerTypeName = GetQualifiedHandleInMarshallerTypeName(compilation, record);
        var borrowedMarshallerDeclarationName = GetHandleReturnMarshallerDeclarationName(record, ownsHandle: false);
        var borrowedMarshallerTypeName = GetQualifiedHandleReturnMarshallerTypeName(compilation, record, ownsHandle: false);
        var ownedMarshallerDeclarationName = GetHandleReturnMarshallerDeclarationName(record, ownsHandle: true);
        var ownedMarshallerTypeName = GetQualifiedHandleReturnMarshallerTypeName(compilation, record, ownsHandle: true);
        var genericConstraints = GetGenericRecordConstraints(record);

        return $$"""
            [CustomMarshaller(typeof({{qualifiedOpenRecordTypeName}}), MarshalMode.ManagedToUnmanagedIn, typeof({{inMarshallerTypeName}}))]
            internal static class {{inMarshallerDeclarationName}}{{genericConstraints}}
            {
                public static nint ConvertToUnmanaged({{qualifiedRecordTypeName}} managed)
                {
                    return global::SharpCpp.Support.NativeObject.GetHandle(managed);
                }
            }

            [CustomMarshaller(typeof({{qualifiedOpenRecordTypeName}}), MarshalMode.ManagedToUnmanagedOut, typeof({{borrowedMarshallerTypeName}}))]
            internal static class {{borrowedMarshallerDeclarationName}}{{genericConstraints}}
            {
                public static {{qualifiedRecordTypeName}} ConvertToManaged(nint unmanaged)
                {
                    return {{qualifiedRecordTypeName}}.CreateManagedWrapper(unmanaged, ownsHandle: false);
                }
            }

            [CustomMarshaller(typeof({{qualifiedOpenRecordTypeName}}), MarshalMode.ManagedToUnmanagedOut, typeof({{ownedMarshallerTypeName}}))]
            internal static class {{ownedMarshallerDeclarationName}}{{genericConstraints}}
            {
                public static {{qualifiedRecordTypeName}} ConvertToManaged(nint unmanaged)
                {
                    return {{qualifiedRecordTypeName}}.CreateManagedWrapper(unmanaged, ownsHandle: true);
                }
            }
            """;
    }

    private static string RenderAliasBindings(
        BindingCompilation? compilation = null,
        bool includeRegistry = false,
        bool includeSharedCallbackHelpers = false)
    {
        var aliases = compilation?.TypeAliases.ToArray() ?? Array.Empty<BindingTypeAlias>();
        var needsRegistry = compilation is not null && includeRegistry;
        var callbackParameters = compilation is not null && includeSharedCallbackHelpers
            ? CollectCallbackParameters(
                compilation.Records.Where(static record => !record.GetMetadata<bool>(MetadataKeys.SuppressEmission) && !record.IsSilentlyExcluded()),
                compilation.Functions.Where(static function => !function.IsSilentlyExcluded() &&
                    (function.Parent is not RecordBinding parentRecord || !parentRecord.IsSilentlyExcluded())))
                .ToArray()
            : Array.Empty<ParameterBinding>();
        if (compilation is null || (aliases.Length == 0 && !needsRegistry && callbackParameters.Length == 0))
        {
            return "// <auto-generated />\n// No alias glue is required for this bootstrap pipeline.\n";
        }

        var polymorphicConcreteRecords = needsRegistry
            ? compilation.Records
                .Where(record => record.IsPolymorphic && CanCreateManagedWrapperFactory(compilation, record) && !record.HasMetadata(MetadataKeys.IgnoreReason))
                .ToArray()
            : Array.Empty<RecordBinding>();
        if (aliases.Length == 0 && needsRegistry && polymorphicConcreteRecords.Length == 0 && callbackParameters.Length == 0)
        {
            return "// <auto-generated />\n// No registry contents to generate.\n";
        }

        var writer = new CodeWriter();
        writer.Write(
            """
            // <auto-generated />
            #nullable enable
            """);

        if (aliases.Length > 0)
        {
            writer.WriteLine();
            foreach (var alias in aliases
                .GroupBy(static a => a.ManagedName, StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static a => a.ManagedName, StringComparer.Ordinal))
            {
                var qualifiedTarget = FormatGlobalTypeName(alias.TargetType, compilation);
                writer.Write($"global using {alias.ManagedName} = {qualifiedTarget};");
            }
        }

        if (callbackParameters.Length > 0)
        {
            writer.Write(
                """

                using System;
                using System.Runtime.CompilerServices;
                using System.Runtime.InteropServices;
                using System.Runtime.InteropServices.Marshalling;
                """);
        }

        if ((!needsRegistry || polymorphicConcreteRecords.Length == 0) && callbackParameters.Length == 0)
        {
            return writer.ToString();
        }

        writer.Write($$"""

            namespace {{compilation.ManagedNamespace}}
            {
            public static partial class {{compilation.ManagedClassName}}
            {
            """);
        if (needsRegistry && polymorphicConcreteRecords.Length > 0)
        {
            writer.Write(RenderNativeObjectRegistryInitializer(compilation, polymorphicConcreteRecords));
        }

        if (callbackParameters.Length > 0)
        {
            writer.WriteLine();
            writer.Write(RenderManagedCallbackSupport(compilation, callbackParameters));
        }

        writer.Write(
            """
            }
            }
            """);
        return writer.ToString();
    }

    /// <summary>
    /// Builds a fully-qualified concrete managed type name without applying
    /// public alias shorthand. This is the representation used for global
    /// using targets, internal signatures, and other non-public surfaces.
    /// </summary>
    private static string FormatGlobalTypeName(BindingType type, BindingCompilation compilation)
    {
        if (type.Kind == BindingTypeKind.GenericParameter)
        {
            return type.GenericParameterName ?? "T";
        }

        if (type.Kind == BindingTypeKind.Primitive)
        {
            if (type.PrimitiveKind != BindingPrimitiveKind.Unsupported)
            {
                return GetConcretePrimitiveManagedTypeName(type);
            }

            return FormatConfiguredManagedTypeName(type, compilation, publicSurface: false);
        }

        if (type.Kind == BindingTypeKind.Enum)
        {
            return GetQualifiedEnumTypeName(compilation, type);
        }

        if (type.Kind == BindingTypeKind.Handle)
        {
            return FormatQualifiedRecordTypeName(compilation, type, publicSurface: false);
        }

        return QualifyManagedTypeText(type.UnaliasedManagedTypeName);
    }

    private static string QualifyRecordTypeName(BindingCompilation compilation, string typeName)
    {
        // Already qualified?
        if (typeName.Contains("global::", StringComparison.Ordinal) || typeName.Contains('.', StringComparison.Ordinal))
            return typeName;

        var genericStart = typeName.IndexOf('<');
        if (genericStart >= 0)
        {
            var head = typeName[..genericStart];
            var suffix = typeName[genericStart..];
            return QualifyRecordTypeName(compilation, head) + suffix;
        }

        // Look up the record by its managed name to find its namespace
        var baseRecord = compilation.Records.FirstOrDefault(r =>
            string.Equals(r.ManagedName, typeName, StringComparison.Ordinal));
        var ns = baseRecord is null ? compilation.ManagedNamespace : GetManagedNamespace(compilation, baseRecord);
        return $"global::{ns}.{typeName}";
    }

    private static string FormatQualifiedRecordTypeName(BindingCompilation compilation, BindingType type, bool publicSurface)
    {
        var record = ResolveRecord(compilation, type);
        if (record is null)
        {
            return FormatConfiguredManagedTypeName(type, compilation, publicSurface);
        }

        var managedName = record.ManagedTypeFormat ?? record.ManagedName;
        var head = GetManagedTypeHead(managedName);
        var qualifiedHead = head.StartsWith("global::", StringComparison.Ordinal)
            ? head
            : head.Contains('.', StringComparison.Ordinal)
                ? $"global::{head}"
                : $"global::{GetManagedNamespace(compilation, record)}.{head}";
        if (type.GenericArguments.Count == 0 || (!record.IsGeneric && managedName.Contains('{', StringComparison.Ordinal) != true))
        {
            return qualifiedHead;
        }

        var renderedArguments = type.GenericArguments.Select(argument => publicSurface
            ? GetManagedPublicReturnTypeName(argument, compilation)
            : FormatGlobalTypeName(argument, compilation)).ToArray();
        if (managedName.Contains('{', StringComparison.Ordinal))
        {
            var formattedName = FormatManagedTypeNameWithPlaceholders(managedName, renderedArguments);
            var formattedHead = GetManagedTypeHead(formattedName);
            var qualifiedFormattedHead = formattedHead.StartsWith("global::", StringComparison.Ordinal)
                ? formattedHead
                : formattedHead.Contains('.', StringComparison.Ordinal)
                    ? $"global::{formattedHead}"
                    : $"global::{GetManagedNamespace(compilation, record)}.{formattedHead}";
            return qualifiedFormattedHead + formattedName[formattedHead.Length..];
        }

        return $"{qualifiedHead}<{string.Join(", ", renderedArguments)}>";
    }

    private static string FormatManagedTypeNameWithPlaceholders(string managedTypeFormat, IReadOnlyList<string> renderedArguments)
    {
        var argumentNames = renderedArguments.Cast<object>().ToArray();
        return AllPlaceholdersInRange(managedTypeFormat, argumentNames.Length)
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, managedTypeFormat, argumentNames)
            : $"{GetManagedTypeHead(managedTypeFormat)}<{string.Join(", ", renderedArguments)}>";
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

    private static string GetManagedRecordTypeName(BindingCompilation compilation, RecordBinding record)
    {
        return FormatQualifiedRecordTypeName(compilation, record.HandleType, publicSurface: false);
    }

    private static string GetQualifiedEnumTypeName(BindingCompilation compilation, BindingType type)
    {
        var normalizedName = NormalizeEnumManagedTypeName(type.UnaliasedManagedTypeName);
        var enumBinding = compilation.Enums.FirstOrDefault(candidate =>
            string.Equals(candidate.CppQualifiedName, type.CppTypeName, StringComparison.Ordinal) ||
            string.Equals(candidate.ManagedName, normalizedName, StringComparison.Ordinal) ||
            string.Equals(candidate.NativeName, type.CppTypeName, StringComparison.Ordinal));
        if (enumBinding is null)
        {
            return QualifyManagedTypeText(compilation, normalizedName);
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

        return $"global::{GetManagedNamespace(compilation, enumBinding)}.{managedName}";
    }

    private static string FormatConfiguredManagedTypeName(BindingType type, BindingCompilation compilation, bool publicSurface)
    {
        var managedTypeName = type.UnaliasedManagedTypeName;
        if (type.GenericArguments.Count == 0)
        {
            return QualifyManagedTypeText(compilation, managedTypeName, type.CppTypeName);
        }

        var qualifiedHead = QualifyManagedTypeText(compilation, GetManagedTypeHead(managedTypeName), type.CppTypeName);
        var renderedArguments = string.Join(", ", type.GenericArguments.Select(argument => publicSurface
            ? GetManagedPublicReturnTypeName(argument, compilation)
            : FormatGlobalTypeName(argument, compilation)));
        return $"{qualifiedHead}<{renderedArguments}>";
    }

    private static string GetManagedTypeHead(string managedTypeName)
    {
        var genericStart = managedTypeName.IndexOf('<');
        return genericStart >= 0 ? managedTypeName[..genericStart] : managedTypeName;
    }

    private static string QualifyManagedTypeText(string managedTypeName)
    {
        if (string.IsNullOrWhiteSpace(managedTypeName))
        {
            return managedTypeName;
        }

        var suffix = string.Empty;
        if (managedTypeName.EndsWith("?", StringComparison.Ordinal))
        {
            suffix = "?";
            managedTypeName = managedTypeName[..^1];
        }

        if (managedTypeName.StartsWith("global::", StringComparison.Ordinal) || IsManagedKeywordTypeName(managedTypeName))
        {
            return managedTypeName + suffix;
        }

        return managedTypeName.Contains('.', StringComparison.Ordinal)
            ? $"global::{managedTypeName}{suffix}"
            : managedTypeName + suffix;
    }

    private static string QualifyManagedTypeText(BindingCompilation compilation, string managedTypeName, string? cppTypeName = null)
    {
        if (string.IsNullOrWhiteSpace(managedTypeName) ||
            managedTypeName.StartsWith("global::", StringComparison.Ordinal) ||
            IsManagedKeywordTypeName(managedTypeName) ||
            managedTypeName.Contains('.', StringComparison.Ordinal))
        {
            return QualifyManagedTypeText(managedTypeName);
        }

        var nullableSuffix = string.Empty;
        if (managedTypeName.EndsWith("?", StringComparison.Ordinal))
        {
            nullableSuffix = "?";
            managedTypeName = managedTypeName[..^1];
        }

        var normalizedCppTypeName = NormalizeCppTypeName(cppTypeName);
        var records = compilation.Records.Where(static record => !record.IsIgnored() && !record.GetMetadata<bool>(MetadataKeys.SuppressEmission));
        var record = !string.IsNullOrWhiteSpace(normalizedCppTypeName)
            ? records.FirstOrDefault(record => string.Equals(record.CppQualifiedName, normalizedCppTypeName, StringComparison.Ordinal))
            : null;

        if (record is null)
        {
            var matches = records
                .Where(record =>
                    string.Equals(record.ManagedName, managedTypeName, StringComparison.Ordinal) ||
                    string.Equals(GetManagedTypeHead(record.ManagedTypeFormat ?? record.ManagedName), managedTypeName, StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            record = matches.Length == 1 ? matches[0] : null;
        }

        if (record is not null)
        {
            return $"global::{GetManagedNamespace(compilation, record)}.{managedTypeName}{nullableSuffix}";
        }

        var normalizedEnumName = NormalizeEnumManagedTypeName(managedTypeName);
        var enums = compilation.Enums.Where(static @enum => !@enum.IsIgnored() && !@enum.GetMetadata<bool>(MetadataKeys.SuppressEmission));
        var enumBinding = !string.IsNullOrWhiteSpace(normalizedCppTypeName)
            ? enums.FirstOrDefault(candidate => string.Equals(candidate.CppQualifiedName, normalizedCppTypeName, StringComparison.Ordinal))
            : null;

        if (enumBinding is null)
        {
            var matches = enums
                .Where(candidate =>
                    string.Equals(candidate.ManagedName, managedTypeName, StringComparison.Ordinal) ||
                    string.Equals(candidate.ManagedName, normalizedEnumName, StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            enumBinding = matches.Length == 1 ? matches[0] : null;
        }

        if (enumBinding is not null)
        {
            return $"global::{GetManagedNamespace(compilation, enumBinding)}.{enumBinding.ManagedName}{nullableSuffix}";
        }

        return QualifyManagedTypeText(managedTypeName + nullableSuffix);
    }

    private static string? NormalizeCppTypeName(string? cppTypeName)
    {
        if (string.IsNullOrWhiteSpace(cppTypeName))
        {
            return null;
        }

        var normalized = cppTypeName.Trim();
        if (normalized.StartsWith("const ", StringComparison.Ordinal))
        {
            normalized = normalized["const ".Length..].TrimStart();
        }

        while (normalized.EndsWith("*", StringComparison.Ordinal) || normalized.EndsWith("&", StringComparison.Ordinal))
        {
            normalized = normalized[..^1].TrimEnd();
        }

        return normalized;
    }

    private static bool IsManagedKeywordTypeName(string managedTypeName) => managedTypeName switch
    {
        "void" or "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or
        "float" or "double" or "nint" or "nuint" or "char" or "string" or "object" or "IntPtr" or "UIntPtr" => true,
        _ => false
    };

    private static string GetConcretePrimitiveManagedTypeName(BindingType type) => type.PrimitiveKind switch
    {
        BindingPrimitiveKind.Void => "void",
        BindingPrimitiveKind.Boolean => "bool",
        BindingPrimitiveKind.Int8 => "sbyte",
        BindingPrimitiveKind.UInt8 => "byte",
        BindingPrimitiveKind.Int16 => "short",
        BindingPrimitiveKind.UInt16 => "ushort",
        BindingPrimitiveKind.Int32 => "int",
        BindingPrimitiveKind.UInt32 => "uint",
        BindingPrimitiveKind.Int64 => "long",
        BindingPrimitiveKind.UInt64 => "ulong",
        BindingPrimitiveKind.Single => "float",
        BindingPrimitiveKind.Double => "double",
        BindingPrimitiveKind.NativeInt => "nint",
        BindingPrimitiveKind.NativeUInt => "nuint",
        BindingPrimitiveKind.IntPtr => "global::System.IntPtr",
        _ => type.UnaliasedManagedTypeName
    };

    private static string RenderRecord(BindingCompilation compilation, RecordBinding record)
    {
        if (record.IsGeneric)
        {
            return RenderGenericRecord(compilation, record);
        }

        var writer = new CodeWriter();
        var canEmitDeleteThunk = CanEmitDeleteThunk(record);
        var pinvokeDeclarations = new List<string>();
        if (canEmitDeleteThunk)
        {
            pinvokeDeclarations.Add(RenderManagedDeleteImportDeclaration(compilation, record));
        }
        if (RequiresArrayElementAccess(compilation, record))
        {
            pinvokeDeclarations.Add(RenderManagedArrayElementImportDeclaration(compilation, record));
        }
        if (TryGetNativeInterfaceQuery(record, out var nativeInterfaceQuery))
        {
            pinvokeDeclarations.Add(RenderManagedNativeInterfaceQueryImportDeclaration(compilation, record, nativeInterfaceQuery));
        }
        if (record.HasMetadata(MetadataKeys.NativeInterfaceReleaseMethod))
        {
            pinvokeDeclarations.Add(RenderManagedNativeInterfaceReleaseImportDeclaration(compilation, record));
        }
        var requiresDirector = record.RequiresDirector(compilation);
        var virtualSlots = record.GetEffectiveVirtualMethodSlots(compilation);
        var isEffectivelyAbstract = IsManagedWrapperAbstract(compilation, record, virtualSlots);
        var isRegistrationPolymorphic = record.IsPolymorphic && !IsNativeInterfaceWrapper(record);
        if (isRegistrationPolymorphic)
        {
            pinvokeDeclarations.Add(RenderManagedStaticTypeIdImportDeclaration(compilation, record));
            pinvokeDeclarations.Add(RenderManagedNativeObjectMetadataImportDeclaration(compilation, record));
        }
        if (requiresDirector)
        {
            pinvokeDeclarations.Add(RenderManagedConnectDirectorImportDeclaration(compilation, record, virtualSlots));
        }

        WriteDocumentationComment(writer, record);
        var classModifier = isEffectivelyAbstract ? "abstract " : string.Empty;
        var hidesStaticHelpers = !string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) && 
            !record.ManagedBaseTypeName.Contains('.', StringComparison.Ordinal);
        var staticHelperModifier = hidesStaticHelpers ? "new " : string.Empty;
        var cacheFieldModifier = hidesStaticHelpers ? "new " : string.Empty;
        var interfaceNames = record.GetSecondaryBaseRecords(compilation)
            .Where(baseRecord => !baseRecord.IsIgnored() && baseRecord.GetRenderableInterfaceMethods(compilation).Count > 0)
            .Select(baseRecord => $"global::{GetManagedNamespace(compilation, baseRecord)}.{baseRecord.GetManagedInterfaceName()}")
            .Cast<string>()
            .ToArray();
        var canEmitClone = CanEmitClone(record, isEffectivelyAbstract, canEmitDeleteThunk);
        var baseTypes = new List<string>
        {
            string.IsNullOrWhiteSpace(record.ManagedBaseTypeName)
                ? "global::SharpCpp.Support.NativeObject"
                : QualifyRecordTypeName(compilation, record.ManagedBaseTypeName)
        };
        baseTypes.AddRange(interfaceNames);
        if (canEmitClone)
        {
            baseTypes.Add(ICloneableFullName);
        }
        baseTypes.AddRange(GetConfiguredManagedStaticInterfaces(compilation, record));
        var baseClause = $" : {string.Join(", ", baseTypes)}";
        writer.Write(
            $$"""
            public {{classModifier}}partial class {{record.ManagedName}}{{baseClause}}, global::SharpCpp.Support.INativeObjectRegistration<{{record.ManagedName}}>
            {
            {{cacheFieldModifier}}internal static readonly {{GetInstanceCacheFieldTypeName(record)}} s_instanceCache = {{GetInstanceCacheFactoryExpression(record)}};

            protected override global::SharpCpp.Support.INativeInstanceCache? InstanceCache => s_instanceCache;

            protected internal {{record.ManagedName}}(nint handle, bool ownsHandle = false)
                : base(handle, ownsHandle)
            {
            }

            {{(string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) && !record.HasOwnManagedMemberNamed("Handle") ? "public nint Handle => DangerousGetHandle();" : string.Empty)}}
            {{(string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) && !record.HasOwnManagedMemberNamed("OwnsHandle") ? "public bool OwnsHandle => OwnsNativeHandle;" : string.Empty)}}
            {{(string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) && !record.HasOwnManagedMemberNamed("IsNull") ? "public bool IsNull => IsClosed || IsInvalid;" : string.Empty)}}

            {{staticHelperModifier}}public static {{record.ManagedName}} CreateManagedWrapper(nint handle, bool ownsHandle)
            {
            if (handle == 0)
                {{(isEffectivelyAbstract ? "return null!;" : $"return new {record.ManagedName}(0, ownsHandle);")}}

            return ({{record.ManagedName}})global::SharpCpp.Support.NativeObjectRegistry.Create<{{record.ManagedName}}>(handle, ownsHandle);
            }
            """);

        if (TryGetNativeInterfaceQuery(record, out nativeInterfaceQuery))
        {
            var sourceRecord = compilation.Records.Single(candidate =>
                string.Equals(candidate.CppQualifiedName, nativeInterfaceQuery.SourceRecordCppQualifiedName, StringComparison.Ordinal));
            writer.WriteLine();
            writer.Write($$"""
                public static {{record.ManagedName}}? QueryManagedWrapper({{QualifyRecordTypeName(compilation, sourceRecord.ManagedName)}} source)
                {
                global::System.ArgumentNullException.ThrowIfNull(source);
                var handle = __PInvoke.QueryInterface(source);
                return handle == 0 ? null : CreateManagedWrapper(handle, ownsHandle: true);
                }
                """);
        }

        if (RequiresArrayElementAccess(compilation, record))
        {
            writer.WriteLine();
            writer.Write($$"""
                {{(!string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) ? "new " : string.Empty)}}public {{record.ManagedName}} GetArrayElement(nuint index)
                {
                return CreateManagedWrapper(__PInvoke.GetArrayElement(this, index), ownsHandle: false);
                }
                """);
        }

        if (record.Members.OfType<FunctionBinding>().Any(static function => function.OperatorToken is "==" or "!="))
        {
            writer.WriteLine();
            writer.Write($$"""
                public override bool Equals(object? obj)
                {
                return obj is {{record.ManagedName}} other && this == other;
                }

                public override int GetHashCode()
                {
                return Handle.GetHashCode();
                }
                """);
        }

        writer.WriteLine();
        writer.Write(RenderManagedDispose(record, canEmitDeleteThunk));

        if (canEmitClone)
        {
            writer.WriteLine();
            writer.Write(RenderManagedCloneMethod(compilation, record));
            pinvokeDeclarations.Add(RenderManagedCloneImportDeclaration(compilation, record));
        }

        var accessorProperties = CollectAccessorProperties(record);
        var accessorMethodIds = accessorProperties
            .SelectMany(static property => new[] { property.Getter?.SymbolId, property.Setter?.SymbolId })
            .Where(static symbolId => !string.IsNullOrWhiteSpace(symbolId))
            .ToHashSet(StringComparer.Ordinal);
        var lastRecordMemberWasIgnoredStub = false;

        void WriteRecordMemberSeparator(bool isIgnoredStub)
        {
            writer.WriteLine();
            if (lastRecordMemberWasIgnoredStub && !isIgnoredStub)
            {
                writer.WriteLine();
            }

            lastRecordMemberWasIgnoredStub = isIgnoredStub;
        }

        void CloseIgnoredStubCluster()
        {
            if (lastRecordMemberWasIgnoredStub)
            {
                writer.WriteLine();
                lastRecordMemberWasIgnoredStub = false;
            }
        }

        foreach (var constructor in record.Members.OfType<FunctionBinding>().Where(static function => function.IsConstructor && !function.IsSilentlyExcluded()).OrderBy(static function => function.Parameters.Count))
        {
            var isIgnoredStub = constructor.HasMetadata(MetadataKeys.IgnoreReason);
            WriteRecordMemberSeparator(isIgnoredStub);
            if (isIgnoredStub)
            {
                writer.Write(RenderIgnoredStub(constructor));
            }
            else
            {
                writer.Write(RenderManagedConstructor(compilation, record, constructor, requiresDirector));
                pinvokeDeclarations.Add(RenderManagedConstructorImportDeclaration(compilation, constructor));
                if (HasNativeDefaultDispatchParams(constructor) || HasUndefaultableParams(constructor))
                {
                    pinvokeDeclarations.Add(RenderManagedUnexpressibleDefaultConstructorImports(compilation, constructor));
                }
            }
        }

        foreach (var property in accessorProperties.OrderBy(static property => property.PropertyName, StringComparer.Ordinal))
        {
            WriteRecordMemberSeparator(isIgnoredStub: false);
            writer.Write(RenderManagedAccessorProperty(compilation, property));

            if (property.Getter is not null)
            {
                pinvokeDeclarations.Add(RenderManagedInstanceImportDeclaration(compilation, property.Getter, record.ManagedName));
            }

            if (property.Setter is not null)
            {
                pinvokeDeclarations.Add(RenderManagedInstanceImportDeclaration(compilation, property.Setter, record.ManagedName));
            }
        }

        foreach (var method in record.Members.OfType<FunctionBinding>().Where(function => !function.IsConstructor && !function.IsStatic && !function.IsGeneric && !function.IsConversionOperator && function.OperatorToken is null && !function.IsSilentlyExcluded() && !accessorMethodIds.Contains(function.SymbolId)).OrderBy(static method => method.ManagedName, StringComparer.Ordinal))
        {
            var isIgnoredStub = method.HasMetadata(MetadataKeys.IgnoreReason);
            WriteRecordMemberSeparator(isIgnoredStub);
            if (isIgnoredStub)
            {
                writer.Write(RenderIgnoredStub(method));
            }
            else
            {
                var isDirectorVirtual = requiresDirector && method.IsVirtual;
                writer.Write(RenderManagedInstanceMethod(compilation, record, method, isDirectorVirtual));
                if (isDirectorVirtual)
                {
                    pinvokeDeclarations.Add(RenderManagedCallbaseImportDeclaration(compilation, method, record.ManagedName));
                    if (HasNativeDefaultDispatchParams(method) || HasUndefaultableParams(method))
                    {
                        pinvokeDeclarations.Add(RenderManagedUnexpressibleDefaultInstanceImports(compilation, method, record.ManagedName, useCallbase: true));
                    }
                }
                else
                {
                    if (!ShouldRenderPureVirtualManagedStub(method))
                    {
                        pinvokeDeclarations.Add(RenderManagedInstanceImportDeclaration(compilation, method, record.ManagedName));
                        if (HasNativeDefaultDispatchParams(method) || HasUndefaultableParams(method))
                        {
                            pinvokeDeclarations.Add(RenderManagedUnexpressibleDefaultInstanceImports(compilation, method, record.ManagedName));
                        }
                    }
                }
            }
        }

        foreach (var method in record.Members.OfType<FunctionBinding>().Where(function => !function.IsConstructor && !function.IsStatic && function.IsGeneric && function.OperatorToken is null && !function.IsConversionOperator && !function.IsSilentlyExcluded() && !accessorMethodIds.Contains(function.SymbolId)).OrderBy(static method => method.ManagedName, StringComparer.Ordinal))
        {
            var isIgnoredStub = method.HasMetadata(MetadataKeys.IgnoreReason);
            WriteRecordMemberSeparator(isIgnoredStub);
            if (isIgnoredStub)
            {
                writer.Write(RenderIgnoredStub(method));
            }
            else
            {
                writer.Write(RenderConcreteGenericManagedMethodOverloads(compilation, record, method));
                writer.Write(RenderGenericManagedGenericMethod(compilation, record, method));
            }
        }

        foreach (var method in record.Members.OfType<FunctionBinding>().Where(static function => function.IsStatic && !function.IsConversionOperator && function.OperatorToken is null && !function.IsSilentlyExcluded()).OrderBy(static method => method.ManagedName, StringComparer.Ordinal))
        {
            var isIgnoredStub = method.HasMetadata(MetadataKeys.IgnoreReason);
            WriteRecordMemberSeparator(isIgnoredStub);
            if (isIgnoredStub)
            {
                writer.Write(RenderIgnoredStub(method));
            }
            else
            {
                writer.Write(RenderManagedStaticMethod(compilation, method));
                pinvokeDeclarations.Add(RenderManagedStaticImportDeclaration(compilation, method));
                if (HasNativeDefaultDispatchParams(method) || HasUndefaultableParams(method))
                {
                    pinvokeDeclarations.Add(RenderManagedUnexpressibleDefaultStaticImports(compilation, method));
                }
            }
        }

        foreach (var method in record.Members.OfType<FunctionBinding>().Where(static function => (function.IsConversionOperator || function.OperatorToken is not null) && !function.IsSilentlyExcluded()).OrderBy(static method => method.ManagedName, StringComparer.Ordinal))
        {
            var isIgnoredStub = method.HasMetadata(MetadataKeys.IgnoreReason);
            WriteRecordMemberSeparator(isIgnoredStub);
            if (isIgnoredStub)
            {
                writer.Write(RenderIgnoredStub(method));
            }
            else
            {
                writer.Write(RenderManagedOperator(compilation, record, method));
                pinvokeDeclarations.Add(RenderManagedOperatorImportDeclaration(compilation, method, record.ManagedName));
            }
        }

        foreach (var field in record.Members.OfType<FieldBinding>().Where(static field => !field.IsSilentlyExcluded()).OrderBy(static field => field.ManagedName, StringComparer.Ordinal))
        {
            if (record.HasOwnManagedNonFieldMemberNamed(field.ManagedName))
            {
                continue;
            }

            var isIgnoredStub = field.HasMetadata(MetadataKeys.IgnoreReason);
            WriteRecordMemberSeparator(isIgnoredStub);
            if (isIgnoredStub)
            {
                writer.Write(RenderIgnoredStub(field));
            }
            else
            {
                if (NeedsManagedFixedArrayProxy(field))
                {
                    writer.Write(RenderManagedFixedArrayProxy(compilation, field, record.ManagedName));
                }

                writer.Write(RenderManagedProperty(compilation, field));
                if (NeedsManagedFixedArrayProxy(field))
                {
                    pinvokeDeclarations.Add(RenderManagedFieldItemGetterImportDeclaration(compilation, field, record.ManagedName, record.CppQualifiedName));

                    if (!field.IsReadOnly)
                    {
                        pinvokeDeclarations.Add(RenderManagedFieldItemSetterImportDeclaration(compilation, field, record.ManagedName, record.CppQualifiedName));
                    }
                }
                else
                {
                    pinvokeDeclarations.Add(RenderManagedFieldGetterImportDeclaration(compilation, field, record.ManagedName, record.CppQualifiedName));

                    if (!field.IsReadOnly)
                    {
                        pinvokeDeclarations.Add(RenderManagedFieldSetterImportDeclaration(compilation, field, record.ManagedName, record.CppQualifiedName));
                    }
                }
            }
        }

        foreach (var field in GetInheritedProtectedFields(compilation, record).OrderBy(static field => field.ManagedName, StringComparer.Ordinal))
        {
            if (record.HasOwnManagedMemberNamed(field.ManagedName))
            {
                continue;
            }

            WriteRecordMemberSeparator(false);
            writer.Write(RenderManagedProperty(compilation, field, hidesInheritedMember: true));
            if (NeedsManagedFixedArrayProxy(field))
            {
                pinvokeDeclarations.Add(RenderManagedFieldItemGetterImportDeclaration(compilation, field, record.ManagedName, record.CppQualifiedName));

                if (!field.IsReadOnly)
                {
                    pinvokeDeclarations.Add(RenderManagedFieldItemSetterImportDeclaration(compilation, field, record.ManagedName, record.CppQualifiedName));
                }
            }
            else
            {
                pinvokeDeclarations.Add(RenderManagedFieldGetterImportDeclaration(compilation, field, record.ManagedName, record.CppQualifiedName));

                if (!field.IsReadOnly)
                {
                    pinvokeDeclarations.Add(RenderManagedFieldSetterImportDeclaration(compilation, field, record.ManagedName, record.CppQualifiedName));
                }
            }
        }

        CloseIgnoredStubCluster();

        writer.WriteLine();
        writer.Write(
            $$"""
            {{(isRegistrationPolymorphic ? $$"""
            {{staticHelperModifier}}internal static nuint GetManagedTypeId()
            {
            return __PInvoke.GetStaticTypeId();
            }

            """ : string.Empty)}}static bool global::SharpCpp.Support.INativeObjectRegistration<{{record.ManagedName}}>.RegistrationIsPolymorphic => {{isRegistrationPolymorphic.ToString().ToLowerInvariant()}};

            static nuint global::SharpCpp.Support.INativeObjectRegistration<{{record.ManagedName}}>.RegistrationDynamicTypeId => {{(isRegistrationPolymorphic ? "GetManagedTypeId()" : "0")}};

            static global::System.Type global::SharpCpp.Support.INativeObjectRegistration<{{record.ManagedName}}>.RegistrationExpectedType => typeof({{record.ManagedName}});

            static global::SharpCpp.Support.INativeInstanceCache global::SharpCpp.Support.INativeObjectRegistration<{{record.ManagedName}}>.RegistrationInstanceCache => s_instanceCache;

            static global::System.Func<nint, bool, global::SharpCpp.Support.NativeObject>? global::SharpCpp.Support.INativeObjectRegistration<{{record.ManagedName}}>.RegistrationCreateFactory => {{(isEffectivelyAbstract ? "null" : $"static (handle, ownsHandle) => new {record.ManagedName}(handle, ownsHandle)")}};

            static global::SharpCpp.Support.NativeObjectMetadata global::SharpCpp.Support.INativeObjectRegistration<{{record.ManagedName}}>.RegistrationGetMetadata(nint handle)
            {
            {{(isRegistrationPolymorphic ? "__PInvoke.GetNativeObjectMetadata(handle, out var resolvedHandle, out var dynamicTypeId);\nreturn new global::SharpCpp.Support.NativeObjectMetadata(resolvedHandle, dynamicTypeId);" : "return new global::SharpCpp.Support.NativeObjectMetadata(handle, 0);")}}
            }
            """);
        writer.WriteLine();

        if (requiresDirector)
        {
            foreach (var slot in virtualSlots)
            {
                writer.WriteLine();
                writer.Write(RenderManagedVirtualCallbackMembers(compilation, record, slot));
            }

            writer.WriteLine();
            writer.Write(RenderManagedConnectDirector(compilation, record, virtualSlots));
            writer.WriteLine();
        }

        var hasGeneratedBase = !string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) && 
            !record.ManagedBaseTypeName.Contains('.', StringComparison.Ordinal);
        writer.Write(
            hasGeneratedBase
            ? $$"""
            new internal static partial class __PInvoke
            {
            """
            : $$"""
            internal static partial class __PInvoke
            {
            """);

        foreach (var declaration in pinvokeDeclarations)
        {
            writer.WriteLine();
            writer.Write(declaration);
        }

        writer.WriteLine();
        writer.Write(
            """
            }
            """);

        writer.WriteLine();
        writer.Write(
            """
            }
            """);

        writer.WriteLine();
        writer.Write(RenderManagedHandleReturnMarshallerHelpers(compilation, record, record.ManagedName));
        return writer.ToString();
    }

    private static IReadOnlyList<string> GetConfiguredManagedStaticInterfaces(BindingCompilation compilation, RecordBinding record)
    {
        var interfaces = new List<string>();
        foreach (var source in record.EnumerateAllBaseRecords(compilation).Append(record))
        {
            var isDirectSource = ReferenceEquals(source, record);
            if (isDirectSource && source.GetMetadata<bool>(MetadataKeys.ManagedStaticInterfacesAbstract))
            {
                continue;
            }

            if (!isDirectSource && !HasPublicStaticMethod(record, "GetTypeStatic"))
            {
                continue;
            }

            var configured = source.GetMetadata<IReadOnlyList<string>>(MetadataKeys.ManagedStaticInterfaces);
            if (configured is null)
            {
                continue;
            }

            interfaces.AddRange(configured
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Select(item => FormatManagedStaticInterface(compilation, record, item)));
        }

        return interfaces.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string FormatManagedStaticInterface(BindingCompilation compilation, RecordBinding record, string interfaceTemplate)
    {
        return interfaceTemplate.Replace(
            "{managed_type}",
            GetQualifiedManagedSelfTypeName(compilation, record),
            StringComparison.Ordinal);
    }

    private static string GetQualifiedManagedSelfTypeName(BindingCompilation compilation, RecordBinding record)
    {
        var declarationName = record.IsGeneric
            ? GetGenericRecordDeclarationName(record)
            : record.ManagedName;
        return $"global::{GetManagedNamespace(compilation, record)}.{declarationName}";
    }

    private static bool HasPublicStaticMethod(RecordBinding record, string managedName)
    {
        return record.Members.OfType<FunctionBinding>().Any(function =>
            string.Equals(function.ManagedName, managedName, StringComparison.Ordinal) &&
            function is { IsStatic: true, AccessModifier: BindingAccessModifier.Public } &&
            !function.IsIgnored() &&
            !function.IsSilentlyExcluded());
    }

    private static string RenderManagedInstanceMethod(BindingCompilation compilation, RecordBinding record, FunctionBinding function, bool useCallbase = false)
    {
        var writer = new CodeWriter();
        WriteDocumentationComment(writer, function);
        var returnTypeName = GetManagedPublicReturnTypeName(function.ReturnType, compilation);
        var importMethodName = useCallbase
            ? GetManagedCallbaseImportMethodName(function)
            : GetManagedImportMethodName(function);
        var memberModifier = GetManagedInstanceMethodModifier(compilation, record, function);

        if (memberModifier.Contains("abstract ", StringComparison.Ordinal))
        {
            var parameterList = RenderManagedPublicParameterList(function.Parameters, compilation);
            writer.Write($$"""
                {{GetManagedVisibilityNormalized(compilation, record, function)}} {{memberModifier}}{{returnTypeName}} {{function.ManagedName}}({{parameterList}});
                """);
            return writer.ToString();
        }

        if (ShouldRenderPureVirtualManagedStub(function))
        {
            var parameterList = RenderManagedPublicParameterList(function.Parameters, compilation);
            writer.Write($$"""
                {{GetManagedVisibilityNormalized(compilation, record, function)}} {{memberModifier}}{{returnTypeName}} {{function.ManagedName}}({{parameterList}})
                {
                throw new global::System.InvalidOperationException("Native pure virtual method '{{function.ManagedName}}' is not callable from managed code.");
                }
                """);
            return writer.ToString();
        }

        if (function.Parameters.Any(IsInputPointerArrayParameter))
        {
            return RenderManagedPointerArrayInstanceMethod(compilation, record, function, returnTypeName, memberModifier, importMethodName);
        }

        if (HasUndefaultableParams(function))
        {
            WriteUndefaultableOverloads(compilation, writer, function, importMethodName, ["this"]);
            return writer.ToString();
        }

        if (HasUnexpressibleDefaultParams(function))
        {
            WriteManagedUnexpressibleDefaultConvenienceOverloads(
                writer,
                compilation,
                function,
                signaturePrefix: $"{GetManagedVisibilityNormalized(compilation, record, function)} {memberModifier}{returnTypeName} {function.ManagedName}",
                invocationPrefix: string.Empty,
                invocationTarget: function.ManagedName,
                returnTypeName: returnTypeName,
                isConstructor: false);
        }

        var parameterListFull = RenderManagedPublicParameterList(function.Parameters, compilation);

        writer.Write($$"""
            {{GetManagedVisibilityNormalized(compilation, record, function)}} {{memberModifier}}{{returnTypeName}} {{function.ManagedName}}({{parameterListFull}})
            {
            """);

        if (HasNativeDefaultDispatchParams(function))
        {
            WriteUnexpressibleDefaultCascade(compilation, writer, function, importMethodName, ["this"]);
        }
        else
        {
            var invocationPlan = BuildManagedInvocationPlan(compilation, function.Parameters);
            var invokeArguments = string.Join(", ", new[] { "this" }.Concat(invocationPlan.Arguments));

            foreach (var statement in invocationPlan.PreCallStatements)
            {
                writer.Write(statement);
            }

            if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
            {
                writer.Write($$"""
                    __PInvoke.{{importMethodName}}({{invokeArguments}});
                    """);

                foreach (var statement in invocationPlan.PostCallStatements)
                {
                    writer.Write(statement);
                }
            }
        else if (function.ReturnType.Kind == BindingTypeKind.Handle)
        {
            writer.Write($$"""
                var __result = __PInvoke.{{importMethodName}}({{invokeArguments}});
                """);

                foreach (var statement in invocationPlan.PostCallStatements)
                {
                    writer.Write(statement);
                }

            writer.Write($$"""
                return {{BuildManagedHandleReturnResultExpression(compilation, function.ReturnType, "__result", ReturnsOwnedHandle(function))}};
                """);
        }
            else if (function.ReturnType.MarshallerTypeName is not null)
            {
                WriteManagedReturnWithMarshallerConversion(compilation, writer, function, importMethodName, invokeArguments, invocationPlan);
            }
            else
            {
                writer.Write($$"""
                    var __result = __PInvoke.{{importMethodName}}({{invokeArguments}});
                    """);

                foreach (var statement in invocationPlan.PostCallStatements)
                {
                    writer.Write(statement);
                }

                writer.Write(
                    """
                    return __result;
                    """);
            }
        }

        writer.Write(
            """
            }
            """);
        return writer.ToString();
    }

    private static string RenderManagedPointerArrayInstanceMethod(
        BindingCompilation compilation,
        RecordBinding record,
        FunctionBinding function,
        string returnTypeName,
        string memberModifier,
        string importMethodName)
    {
        if (function.ReturnType.MarshallerTypeName is not null)
        {
            throw new NotSupportedException($"Pointer-array method '{function.ManagedName}' does not support custom return marshalling yet.");
        }

        var writer = new CodeWriter();
        var parameterList = RenderManagedPublicParameterList(function.Parameters, compilation);
        writer.Write($$"""
            {{GetManagedVisibilityNormalized(compilation, record, function)}} {{memberModifier}}{{returnTypeName}} {{function.ManagedName}}({{parameterList}})
            {
            """);

        var pointerArrayParameters = function.Parameters.Where(IsInputPointerArrayParameter).ToArray();
        foreach (var parameter in pointerArrayParameters.Where(IsInputHandlePointerArrayParameter))
        {
            var abiSpanName = GetPointerArrayAbiSpanName(parameter);
            var indexName = $"{abiSpanName}_i";
            writer.Write($$"""
                global::System.Span<nint> {{abiSpanName}} = {{parameter.ManagedName}}.Length <= 64 ? stackalloc nint[{{parameter.ManagedName}}.Length] : new nint[{{parameter.ManagedName}}.Length];
                for (var {{indexName}} = 0; {{indexName}} < {{parameter.ManagedName}}.Length; {{indexName}}++)
                {
                    {{abiSpanName}}[{{indexName}}] = global::SharpCpp.Support.NativeObject.GetHandle({{parameter.ManagedName}}[{{indexName}}]);
                }
                """);
        }

        writer.WriteLine("unsafe");
        writer.WriteLine("{");
        foreach (var parameter in pointerArrayParameters)
        {
            var pointerTypeName = GetManagedImportTypeName(parameter.Type, compilation);
            var spanName = IsInputHandlePointerArrayParameter(parameter)
                ? GetPointerArrayAbiSpanName(parameter)
                : parameter.ManagedName;
            writer.WriteLine($"fixed ({pointerTypeName}* {GetPointerArrayPointerName(parameter)} = {spanName})");
        }
        writer.WriteLine("{");

        var invokeArguments = string.Join(", ", new[] { "this" }.Concat(function.Parameters.Select(parameter =>
            IsInputPointerArrayParameter(parameter)
                ? GetPointerArrayPointerName(parameter)
                : RenderManagedInvocationArgument(compilation, parameter))));

        if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
        {
            writer.WriteLine($"__PInvoke.{importMethodName}({invokeArguments});");
        }
        else if (function.ReturnType.Kind == BindingTypeKind.Handle)
        {
            writer.WriteLine($"var __result = __PInvoke.{importMethodName}({invokeArguments});");
            writer.WriteLine($"return {BuildManagedHandleReturnResultExpression(compilation, function.ReturnType, "__result", ReturnsOwnedHandle(function))};");
        }
        else
        {
            writer.WriteLine($"return __PInvoke.{importMethodName}({invokeArguments});");
        }

        writer.WriteLine("}");
        writer.WriteLine("}");
        writer.WriteLine("}");
        return writer.ToString();
    }

    private static bool ShouldUseNewModifier(BindingCompilation compilation, RecordBinding record, string managedName)
    {
        if (string.IsNullOrWhiteSpace(record.ManagedBaseTypeName))
        {
            return false;
        }

        var baseRecord = compilation.Records.FirstOrDefault(candidate => string.Equals(candidate.ManagedName, record.ManagedBaseTypeName, StringComparison.Ordinal));
        return baseRecord is not null &&
               baseRecord.Members.OfType<FunctionBinding>().Any(candidate => !candidate.IsIgnored() && string.Equals(candidate.ManagedName, managedName, StringComparison.Ordinal));
    }

    private static string RenderManagedDispose(RecordBinding record, bool canEmitDeleteThunk)
    {
        if (!record.EmitDefaultNativeReleaseOverride)
        {
            return string.Empty;
        }

        if (record.HasMetadata(MetadataKeys.NativeInterfaceReleaseMethod))
        {
            return """
                protected override bool ReleaseNativeHandle(nint handle)
                {
                if (OwnsNativeHandle)
                {
                    __PInvoke.ReleaseOwnedHandle(handle);
                }
                return true;
                }
                """;
        }

        if (!canEmitDeleteThunk)
        {
            return """
                protected override bool ReleaseNativeHandle(nint handle)
                {
                return true;
                }
                """;
        }

        return """
            protected override bool ReleaseNativeHandle(nint handle)
            {
            if (OwnsNativeHandle)
            {
                __PInvoke.Delete(handle);
            }
            return true;
            }
            """;
    }

    private static string RenderManagedDeleteImportDeclaration(BindingCompilation compilation, RecordBinding record)
    {
        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_delete", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static partial void Delete(nint self);
            """;
    }

    private static string RenderManagedArrayElementImportDeclaration(BindingCompilation compilation, RecordBinding record)
    {
        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_array_element", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static partial nint GetArrayElement({{record.ManagedName}} self, nuint index);
            """;
    }

    private static string RenderManagedNativeInterfaceQueryImportDeclaration(
        BindingCompilation compilation,
        RecordBinding record,
        NativeInterfaceQueryAnnotation query)
    {
        var sourceRecord = compilation.Records.Single(candidate =>
            string.Equals(candidate.CppQualifiedName, query.SourceRecordCppQualifiedName, StringComparison.Ordinal));
        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_query_interface", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static partial nint QueryInterface({{sourceRecord.ManagedName}} source);
            """;
    }

    private static string RenderManagedNativeInterfaceReleaseImportDeclaration(BindingCompilation compilation, RecordBinding record)
    {
        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_release_interface", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static partial void ReleaseOwnedHandle(nint self);
            """;
    }

    private static string RenderManagedCloneImportDeclaration(BindingCompilation compilation, RecordBinding record)
    {
        var returnTypeName = GetManagedImportReturnTypeName(compilation, record.HandleType);
        var returnAttribute = GetManagedImportReturnAttribute(compilation, record.HandleType, ownsHandle: true);
        var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static partial {returnTypeName} CloneCopyCtor({record.ManagedName} self);"
            : $"{returnAttribute}\ninternal static partial {returnTypeName} CloneCopyCtor({record.ManagedName} self);";

        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_clone", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            {{importMethodLine}}
            """;
    }

    private static string RenderManagedCloneMethod(BindingCompilation compilation, RecordBinding record)
    {
        var writer = new CodeWriter();

        if (!record.HasOwnManagedNonFieldMemberNamed("Clone"))
        {
            var hidesBaseClone = InheritsCloneInterface(compilation, record);
            writer.Write($$"""
                public {{(hidesBaseClone ? "new " : string.Empty)}}{{record.ManagedName}} Clone()
                {
                var __result = __PInvoke.CloneCopyCtor(this);
                if (__result is null)
                {
                throw new NotSupportedException("SharpCpp could not clone this native object because the native type is not copy-constructible.");
                }
                return __result;
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
                var __result = __PInvoke.CloneCopyCtor(this);
                if (__result is null)
                {
                throw new NotSupportedException("SharpCpp could not clone this native object because the native type is not copy-constructible.");
                }
                return __result;
                }
                """);
        }

        return writer.ToString();
    }

    private static string RenderManagedAccessorProperty(BindingCompilation compilation, AccessorProperty property)
    {
        var writer = new CodeWriter();
        var type = property.Getter?.ReturnType ?? property.Setter!.Parameters[0].Type;
        var propertyTypeName = GetManagedPublicReturnTypeName(type, compilation);

        writer.Write($$"""
            {{GetManagedVisibility(GetAccessorPropertyAccessModifier(property))}} {{propertyTypeName}} {{property.PropertyName}}
            {
            """);

        if (property.Getter is not null)
        {
            if (property.Getter.ReturnType.Kind == BindingTypeKind.Handle)
            {
                writer.Write($$"""
                    get
                    {
                    return {{BuildManagedHandleReturnResultExpression(compilation, property.Getter.ReturnType, $"__PInvoke.{GetManagedImportMethodName(property.Getter)}(this)", ownsHandle: false)}};
                    }
                    """);
            }
            else
            {
                writer.Write($$"""
                    get
                    {
                    return {{BuildManagedImportToPublicExpression(compilation, property.Getter.ReturnType, $"__PInvoke.{GetManagedImportMethodName(property.Getter)}(this)")}};
                    }
                    """);
            }
        }

        if (property.Setter is not null)
        {
            var requiresWrapping = RequiresHandleWrapping(property.Setter.Parameters[0].Type);
            var useManagedHandleImport = CanUseManagedHandleParameterImport(property.Setter.Parameters[0], property.Setter.Parameters[0].Type);
            var setterArgument = useManagedHandleImport
                ? "value"
                : requiresWrapping
                ? "global::SharpCpp.Support.NativeObject.GetHandle(value)"
                : BuildManagedPublicToImportExpression(compilation, property.Setter.Parameters[0].Type, "value");
            var setterKeepAlive = requiresWrapping
                ? "\n            global::System.GC.KeepAlive(value);"
                : string.Empty;
            writer.Write($$"""
                set
                {
                __PInvoke.{{GetManagedImportMethodName(property.Setter)}}(this, {{setterArgument}});{{setterKeepAlive}}
                }
                """);
        }

        writer.Write(
            """
            }
            """);
        return writer.ToString();
    }

    private static IReadOnlyList<AccessorProperty> CollectAccessorProperties(RecordBinding record)
    {
        var getters = new Dictionary<string, FunctionBinding>(StringComparer.Ordinal);
        var setters = new Dictionary<string, FunctionBinding>(StringComparer.Ordinal);

        foreach (var method in record.Members.OfType<FunctionBinding>())
        {
            if (method.HasMetadata(MetadataKeys.IgnoreReason))
            {
                continue;
            }

            if (!method.TryGetMetadata<AccessorPropertyAnnotation>(MetadataKeys.AccessorProperty, out var accessor) || accessor is null)
            {
                continue;
            }

            if (accessor.Kind == AccessorPropertyKind.Getter)
            {
                getters[accessor.PropertyName] = method;
            }
            else
            {
                setters[accessor.PropertyName] = method;
            }
        }

        return getters.Keys
            .Concat(setters.Keys)
            .Distinct(StringComparer.Ordinal)
            .Select(propertyName => new AccessorProperty(
                propertyName,
                getters.TryGetValue(propertyName, out var getter) ? getter : null,
                setters.TryGetValue(propertyName, out var setter) ? setter : null))
            .ToArray();
    }

    private static string RenderManagedConstructor(BindingCompilation compilation, RecordBinding record, FunctionBinding function, bool requiresDirector)
    {
        var writer = new CodeWriter();
        WriteDocumentationComment(writer, function);
        var constructorName = function.Parent is RecordBinding owningRecord
            ? owningRecord.ManagedName
            : NormalizeEnumManagedTypeName(function.ManagedName);
        var importMethodName = GetManagedImportMethodName(function);

        if (HasUndefaultableParams(function))
        {
            WriteUndefaultableConstructorOverloads(writer, compilation, record, function, constructorName, importMethodName, requiresDirector);
            return writer.ToString();
        }

        if (HasUnexpressibleDefaultParams(function))
        {
            WriteManagedUnexpressibleDefaultConvenienceOverloads(
                writer,
                compilation,
                function,
                signaturePrefix: $"{GetManagedVisibility(function.AccessModifier)} {constructorName}",
                invocationPrefix: ": this",
                invocationTarget: string.Empty,
                returnTypeName: null,
                isConstructor: true);
        }

        var parameterList = RenderManagedPublicParameterList(function.Parameters, compilation);
        var invokeArguments = string.Join(", ", function.Parameters.Select(parameter => RenderManagedInvocationArgument(compilation, parameter)));
        var hasHandleParams = function.Parameters.Any(static parameter => parameter.Type.Kind == BindingTypeKind.Handle);
        var hasNativeDefaultDispatch = HasNativeDefaultDispatchParams(function);
        var postConstructStatements = RenderManagedConstructorPostConstruct(record, requiresDirector);

        if (hasHandleParams || hasNativeDefaultDispatch)
        {
            var factoryName = $"__CreateHandle_{importMethodName}";

            writer.Write($$"""
                {{GetManagedVisibility(function.AccessModifier)}} {{constructorName}}({{parameterList}})
                    : this({{factoryName}}({{string.Join(", ", function.Parameters.Select(RenderManagedForwardingArgument))}}), true)
                {
                {{postConstructStatements}}
                }

                private static nint {{factoryName}}({{parameterList}})
                {
                """);

            if (hasNativeDefaultDispatch)
            {
                WriteUnexpressibleDefaultConstructorCascade(writer, function, importMethodName);
            }
            else
            {
                var invocationPlan = BuildManagedInvocationPlan(null!, function.Parameters);
                writer.Write($$"""
                    var __handle = __PInvoke.{{importMethodName}}({{invokeArguments}});
                    """);

                foreach (var statement in invocationPlan.PostCallStatements)
                {
                    writer.Write(statement);
                }

                writer.Write(
                    """
                    return __handle;
                    """);
            }

            writer.Write(
                """
                }
                """);
        }
        else
        {
            writer.Write($$"""
                {{GetManagedVisibility(function.AccessModifier)}} {{constructorName}}({{parameterList}})
                    : this(__PInvoke.{{importMethodName}}({{invokeArguments}}), true)
                {
                {{postConstructStatements}}
                }
                """);
        }

        return writer.ToString();
    }

    private static string RenderManagedConstructorPostConstruct(RecordBinding record, bool requiresDirector)
    {
        var registerInstance = $"global::SharpCpp.Support.NativeObjectRegistry.RegisterInstance<{record.ManagedName}>(this);";
        return requiresDirector
            ? $"ConnectDirector();\n    {registerInstance}"
            : registerInstance;
    }

    private static void WriteUndefaultableConstructorOverloads(
        CodeWriter writer,
        BindingCompilation compilation,
        RecordBinding record,
        FunctionBinding function,
        string constructorName,
        string importMethodName,
        bool requiresDirector)
    {
        var firstU = GetFirstUndefaultableParamIndex(function);
        if (firstU < 0)
        {
            return;
        }

        var postConstructStatements = RenderManagedConstructorPostConstruct(record, requiresDirector);

        // =========================================================
        // Overload A: params before first undefaultable index
        // =========================================================
        {
            var beforeParams = function.Parameters.Take(firstU).ToArray();
            var aParamList = string.Join(", ", beforeParams.Select(p => RenderManagedPublicParameter(p, compilation)));
            var aArgNames = string.Join(", ", beforeParams.Select(RenderManagedForwardingArgument));
            var aHasUnexpressible = beforeParams.Any(p => p.HasMetadata(MetadataKeys.UnexpressibleDefault));

            var aFactoryName = $"__CreateHandle_{importMethodName}_OverloadA";

            writer.Write($$"""
                public {{constructorName}}({{aParamList}})
                    : this({{aFactoryName}}({{aArgNames}}), true)
                {
                {{postConstructStatements}}
                }

                private static nint {{aFactoryName}}({{aParamList}})
                {
                """);

            if (firstU == 0)
            {
                writer.Write($$"""
                    var __handle = __PInvoke.{{GetUnexpressibleVariantImportName(importMethodName, 0)}}();
                    return __handle;
                    """);
            }
            else if (aHasUnexpressible)
            {
                WriteUnexpressibleDefaultConstructorCascade(writer, function, importMethodName, firstU);
            }
            else
            {
                var aPlan = BuildManagedInvocationPlan(null!, beforeParams);
                var aInvokeArgs = string.Join(", ", beforeParams.Select(parameter => RenderManagedInvocationArgument(compilation, parameter)));
                writer.Write($$"""
                    var __handle = __PInvoke.{{GetUnexpressibleVariantImportName(importMethodName, firstU)}}({{aInvokeArgs}});
                    """);
                foreach (var statement in aPlan.PostCallStatements)
                {
                    writer.Write(statement);
                }
                writer.Write("""
                    return __handle;
                    """);
            }

            writer.Write("""
                }
                """);
            writer.WriteLine();
        }

        // =========================================================
        // Overload B: all params, before/first undefaultable are required
        // =========================================================
        {
            var bParamParts = new List<string>();
            for (var i = 0; i < function.Parameters.Count; i++)
            {
                var param = function.Parameters[i];
                if (i < firstU || param.HasMetadata(MetadataKeys.UndefaultableParam))
                {
                    bParamParts.Add(RenderManagedPublicParameterRequired(param, compilation));
                }
                else
                {
                    bParamParts.Add(RenderManagedPublicParameter(param, compilation));
                }
            }
            var bParamList = string.Join(", ", bParamParts);
            var bArgNames = string.Join(", ", function.Parameters.Select(RenderManagedForwardingArgument));
            var bFactoryName = $"__CreateHandle_{importMethodName}_OverloadB";
            var bHasAfterUnexpressible = function.Parameters.Skip(firstU).Any(p => p.HasMetadata(MetadataKeys.UnexpressibleDefault));

            writer.Write($$"""
                public {{constructorName}}({{bParamList}})
                    : this({{bFactoryName}}({{bArgNames}}), true)
                {
                {{postConstructStatements}}
                }

                private static nint {{bFactoryName}}({{bParamList}})
                {
                """);

            if (bHasAfterUnexpressible)
            {
                // Cascade only for after-firstU unexpressible defaults.
                // We use a filtered approach: create a projection of the function
                // that only has the after-firstU params, so the standard cascade
                // only generates conditions/variants for those positions.
                // Reuse the cascade but with a builder that adjusts param names.
                var uFirst = GetFirstUndefaultableParamIndex(function);
                var afterDefaultIndices = new List<int>();
                for (var i = uFirst; i < function.Parameters.Count; i++)
                {
                    if (function.Parameters[i].HasMetadata(MetadataKeys.UnexpressibleDefault))
                    {
                        afterDefaultIndices.Add(i);
                    }
                }

                // Write a targeted cascade for the after-params only.
                // Filter defaultIndices to only after-firstU positions, since
                // before-firstU params are non-nullable in Overload B.
                WriteUndefaultableConstructorCascade_Filtered(writer, function, importMethodName, uFirst, afterDefaultIndices);
            }
            else
            {
                var bInvokeArgs = string.Join(", ", function.Parameters.Select(parameter => RenderManagedInvocationArgument(compilation, parameter)));
                writer.Write($$"""
                    var __handle = __PInvoke.{{importMethodName}}({{bInvokeArgs}});
                    return __handle;
                    """);
            }

            writer.Write("""
                }
                """);
        }
    }

    private static string RenderManagedConstructorImportDeclaration(BindingCompilation compilation, FunctionBinding function)
    {
        var writer = new CodeWriter();
        var parameterList = string.Join(
            ", ",
            function.Parameters.Select(parameter => RenderManagedImportParameter(parameter, compilation)));
        var importMethodName = GetManagedImportMethodName(function);

        writer.Write($$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{function.EntryPointName}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static partial nint {{importMethodName}}({{parameterList}});
            """);
        return writer.ToString();
    }

    private static string RenderManagedConnectDirectorImportDeclaration(
        BindingCompilation compilation,
        RecordBinding record,
        IReadOnlyList<VirtualMethodSlotBinding> slots)
    {
        var parameterList = string.Join(
            ", ",
            new[] { "nint self" }.Concat(slots.Select((_, index) => $"nint slot{index}")));

        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_connect_director", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static partial void ConnectDirector({{parameterList}});
            """;
    }

    private static string RenderManagedConnectDirector(BindingCompilation compilation, RecordBinding record, IReadOnlyList<VirtualMethodSlotBinding> slots)
    {
        var callbackArguments = slots.Count == 0
            ? string.Empty
            : ", " + string.Join(
                ", ",
                slots.Select(slot => $"{GetManagedRecordTypeName(compilation, record)}.{GetVirtualCallbackPointerAccessorName(slot.ManagedCallbackMethod)}(runtimeType)"));

        return $$"""
            private void ConnectDirector()
            {
            var runtimeType = ((object)this).GetType();
            __PInvoke.ConnectDirector(DangerousGetHandle(){{callbackArguments}});
            }
            """;
    }

    private static string RenderManagedVirtualCallbackMembers(BindingCompilation compilation, RecordBinding record, VirtualMethodSlotBinding slot)
    {
        var method = slot.ManagedCallbackMethod;
        ValidateVirtualCallbackSupport(method);

        var ownerRecord = slot.ManagedCallbackOwnerRecord;
        var dispatchRecord = method.AccessModifier == BindingAccessModifier.Protected ? record : ownerRecord;
        var ownerTypeName = GetManagedRecordTypeName(compilation, ownerRecord);
        var dispatchTypeName = GetManagedRecordTypeName(compilation, dispatchRecord);
        var methodNameExpression = method.AccessModifier == BindingAccessModifier.Protected
            ? $"\"{method.ManagedName}\""
            : $"nameof({ownerTypeName}.{method.ManagedName})";
        var writer = new CodeWriter();
        var callbackReturnType = GetManagedImportTypeName(method.ReturnType, compilation);
        var returnAttribute = GetManagedReturnAttribute(method.ReturnType);
        var callbackParameterList = string.Join(
            ", ",
            new[] { "nint self" }.Concat(method.Parameters.Select(parameter => RenderManagedCallbackParameter(compilation, parameter))));
        var invocationArguments = string.Join(
            ", ",
            method.Parameters.Select(parameter => BuildManagedCallbackInvocationArgument(compilation, parameter)));

        if (!string.IsNullOrWhiteSpace(returnAttribute))
        {
            writer.WriteLine(returnAttribute);
        }

        writer.Write($$"""
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate {{callbackReturnType}} {{GetVirtualCallbackDelegateName(method)}}({{callbackParameterList}});

            private static readonly {{GetVirtualCallbackDelegateName(method)}} {{GetVirtualCallbackFieldName(method)}} = {{GetVirtualCallbackDispatchName(method)}};
            private static readonly nint {{GetVirtualCallbackPointerFieldName(method)}} = Marshal.GetFunctionPointerForDelegate({{GetVirtualCallbackFieldName(method)}});

            internal static nint {{GetVirtualCallbackPointerAccessorName(method)}}(Type runtimeType)
            {
            return HasManagedOverride(runtimeType, typeof({{ownerTypeName}}), {{methodNameExpression}}, {{FormatReflectionParameterTypes(compilation, method)}})
                ? {{GetVirtualCallbackPointerFieldName(method)}}
                : 0;
            }

            """);

        if (!string.IsNullOrWhiteSpace(returnAttribute))
        {
            writer.WriteLine(returnAttribute);
        }

        writer.Write($$"""
            private static {{callbackReturnType}} {{GetVirtualCallbackDispatchName(method)}}({{callbackParameterList}})
            {
            var owner = {{dispatchTypeName}}.CreateManagedWrapper(self, ownsHandle: false);
            """);

        var invocation = $"owner.{method.ManagedName}({invocationArguments})";
        if (method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
        {
            writer.Write($$"""
                {{invocation}};
            }
            """);
            return writer.ToString();
        }

        if (method.ReturnType.Kind == BindingTypeKind.Handle)
        {
            writer.Write($$"""
                var __result = {{invocation}};
                return global::SharpCpp.Support.NativeObject.GetHandle(__result);
            }
            """);
            return writer.ToString();
        }

        writer.Write($$"""
            return {{invocation}};
            }
            """);
        return writer.ToString();
    }

    private static string RenderManagedStaticMethod(BindingCompilation compilation, FunctionBinding function)
    {
        var writer = new CodeWriter();
        WriteDocumentationComment(writer, function);
        var returnTypeName = GetManagedPublicReturnTypeName(function.ReturnType, compilation);
        var importMethodName = GetManagedImportMethodName(function);

        if (HasUndefaultableParams(function))
        {
            WriteUndefaultableOverloads(compilation, writer, function, importMethodName, []);
            return writer.ToString();
        }

        if (HasUnexpressibleDefaultParams(function))
        {
            WriteManagedUnexpressibleDefaultConvenienceOverloads(
                writer,
                compilation,
                function,
                signaturePrefix: $"{GetManagedVisibility(function.AccessModifier)} static {returnTypeName} {function.ManagedName}",
                invocationPrefix: string.Empty,
                invocationTarget: function.ManagedName,
                returnTypeName: returnTypeName,
                isConstructor: false);
        }

        var parameterList = RenderManagedPublicParameterList(function.Parameters, compilation);

        writer.Write($$"""
            {{GetManagedVisibility(function.AccessModifier)}} static {{returnTypeName}} {{function.ManagedName}}({{parameterList}})
            {
            """);

        if (HasNativeDefaultDispatchParams(function))
        {
            WriteUnexpressibleDefaultCascade(compilation, writer, function, importMethodName, []);
        }
        else
        {
            var invocationPlan = BuildManagedInvocationPlan(compilation, function.Parameters);
            var invokeArguments = string.Join(", ", invocationPlan.Arguments);

            foreach (var statement in invocationPlan.PreCallStatements)
            {
                writer.Write(statement);
            }

            if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
            {
                writer.Write($$"""
                    __PInvoke.{{importMethodName}}({{invokeArguments}});
                    """);

                foreach (var statement in invocationPlan.PostCallStatements)
                {
                    writer.Write(statement);
                }
            }
            else if (function.ReturnType.Kind == BindingTypeKind.Handle)
            {
                writer.Write($$"""
                    var __result = __PInvoke.{{importMethodName}}({{invokeArguments}});
                    """);

                foreach (var statement in invocationPlan.PostCallStatements)
                {
                    writer.Write(statement);
                }

                writer.Write($$"""
                    return {{BuildManagedHandleReturnResultExpression(compilation, function.ReturnType, "__result", ReturnsOwnedHandle(function))}};
                    """);
            }
            else if (function.ReturnType.MarshallerTypeName is not null)
            {
                WriteManagedReturnWithMarshallerConversion(compilation, writer, function, importMethodName, invokeArguments, invocationPlan);
            }
            else
            {
                writer.Write($$"""
                    var __result = __PInvoke.{{importMethodName}}({{invokeArguments}});
                    """);

                foreach (var statement in invocationPlan.PostCallStatements)
                {
                    writer.Write(statement);
                }

                writer.Write(
                    """
                    return __result;
                    """);
            }
        }

        writer.Write(
            """
            }
            """);
        return writer.ToString();
    }

    private static string RenderManagedStaticImportDeclaration(BindingCompilation compilation, FunctionBinding function)
    {
        var writer = new CodeWriter();
        var parameterList = string.Join(
            ", ",
            function.Parameters.Select(parameter => RenderManagedImportParameter(parameter, compilation)));
        var returnTypeName = GetManagedImportReturnTypeName(compilation, function.ReturnType);
        var returnAttribute = GetManagedImportReturnAttribute(compilation, function.ReturnType, ReturnsOwnedHandle(function));
        var importMethodName = GetManagedImportMethodName(function);
        var unsafeModifier = parameterList.Contains('*', StringComparison.Ordinal) ? "unsafe " : string.Empty;
        var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static {unsafeModifier}partial {returnTypeName} {importMethodName}({parameterList});"
            : $"{returnAttribute}\ninternal static {unsafeModifier}partial {returnTypeName} {importMethodName}({parameterList});";

        writer.Write(
            $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{function.EntryPointName}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            {{importMethodLine}}
            """);
        return writer.ToString();
    }

    private static string RenderManagedOperator(BindingCompilation compilation, RecordBinding record, FunctionBinding function)
    {
        var writer = new CodeWriter();
        WriteDocumentationComment(writer, function);

        if (function.Parameters.Count == 1 && function.OperatorToken == "==" && !HasOperator(record, "!="))
        {
            writer.Write($$"""
                public bool Equals({{record.ManagedName}} rhs)
                {
                return __PInvoke.{{function.ManagedName}}(this, rhs);
                }
            """);
            return writer.ToString();
        }

        if (function.Parameters.Count == 1 && function.OperatorToken == "!=" && !HasOperator(record, "=="))
        {
            writer.Write($$"""
                public bool Equals({{record.ManagedName}} rhs)
                {
                return !__PInvoke.{{function.ManagedName}}(this, rhs);
                }

                public static bool operator ==({{record.ManagedName}} left, {{record.ManagedName}} right)
                {
                return !__PInvoke.{{function.ManagedName}}(left, right);
                }

                public static bool operator !=({{record.ManagedName}} left, {{record.ManagedName}} right)
                {
                return __PInvoke.{{function.ManagedName}}(left, right);
                }
            """);
            return writer.ToString();
        }

        if (function.Parameters.Count == 1 && function.OperatorToken == "<" && !HasOperator(record, ">"))
        {
            writer.Write($$"""
                public bool LessThan({{record.ManagedName}} rhs)
                {
                return __PInvoke.{{function.ManagedName}}(this, rhs);
                }
            """);
            return writer.ToString();
        }

        if (function.OperatorToken == "!" && function.Parameters.Count == 0)
        {
            var returnTypeName = GetManagedWrapperTypeName(function.ReturnType, compilation);
            writer.Write($$"""
                public static {{returnTypeName}} operator !({{record.ManagedName}} value)
                {
                return __PInvoke.{{function.ManagedName}}(value);
                }
                """);
            return writer.ToString();
        }

        if (function.OperatorToken == "!" && function.Parameters.Count == 0)
        {
            var returnTypeName = GetManagedWrapperTypeName(function.ReturnType, compilation);
            writer.Write($$"""
                public static {{returnTypeName}} operator !({{record.ManagedName}} value)
                {
                return __PInvoke.{{function.ManagedName}}(value);
                }
                """);
            return writer.ToString();
        }

        if (function.IsConversionOperator)
        {
            var targetTypeName = GetManagedWrapperTypeName(function.ReturnType, compilation);
            var conversionKeyword = function.IsExplicitConversionOperator ? "explicit" : "implicit";
            writer.Write($$"""
                public static {{conversionKeyword}} operator {{targetTypeName}}({{record.ManagedName}} value)
                {
                return __PInvoke.{{function.ManagedName}}(value);
                }
                """);
            return writer.ToString();
        }

        if (function.OperatorToken == "+" && function.Parameters.Count == 1)
        {
            var returnTypeName = GetManagedWrapperTypeName(function.ReturnType, compilation);
            var parameter = RenderManagedPublicParameter(function.Parameters[0], compilation);
            if (function.ReturnType.Kind == BindingTypeKind.Handle)
            {
                writer.Write($$"""
                    public static {{returnTypeName}} operator +({{record.ManagedName}} left, {{parameter}})
                    {
                    return {{BuildManagedHandleReturnResultExpression(compilation, function.ReturnType, $"__PInvoke.{function.ManagedName}(left, {RenderManagedSafeHandleInvocationArgument(function.Parameters[0])})", ownsHandle: false)}};
                    }
                    """);
            }
            else
            {
                writer.Write($$"""
                    public static {{returnTypeName}} operator +({{record.ManagedName}} left, {{parameter}})
                    {
                    return __PInvoke.{{function.ManagedName}}(left, {{RenderManagedSafeHandleInvocationArgument(function.Parameters[0])}});
                    }
                    """);
            }
            return writer.ToString();
        }

        if (function.Parameters.Count == 1 && function.OperatorToken is "==" or "!=" or "<" or ">")
        {
            var returnTypeName = GetManagedWrapperTypeName(function.ReturnType, compilation);
            var parameterTypeName = GetManagedWrapperTypeName(function.Parameters[0].Type, compilation);
            writer.Write($$"""
                public static {{returnTypeName}} operator {{function.OperatorToken}}({{record.ManagedName}} left, {{parameterTypeName}} right)
                {
                return __PInvoke.{{function.ManagedName}}(left, right);
                }
                """);
            return writer.ToString();
        }

        if (function.OperatorToken == "-" && function.Parameters.Count == 0)
        {
            var returnTypeName = GetManagedWrapperTypeName(function.ReturnType, compilation);
            if (function.ReturnType.Kind == BindingTypeKind.Handle)
            {
                writer.Write($$"""
                    public static {{returnTypeName}} operator -({{record.ManagedName}} value)
                    {
                    return {{BuildManagedHandleReturnResultExpression(compilation, function.ReturnType, $"__PInvoke.{function.ManagedName}(value)", ownsHandle: false)}};
                    }
                    """);
            }
            else
            {
                writer.Write($$"""
                    public static {{returnTypeName}} operator -({{record.ManagedName}} value)
                    {
                    return __PInvoke.{{function.ManagedName}}(value);
                    }
                    """);
            }
            return writer.ToString();
        }

        throw new NotSupportedException($"Managed operator emission does not support '{function.ManagedName}'.");
    }

    private static bool HasOperator(RecordBinding record, string operatorToken)
    {
        return record.Members
            .OfType<FunctionBinding>()
            .Any(candidate => string.Equals(candidate.OperatorToken, operatorToken, StringComparison.Ordinal));
    }

    private static string RenderManagedOperatorImportDeclaration(BindingCompilation compilation, FunctionBinding function, string recordManagedName)
    {
        var writer = new CodeWriter();
        var parameterList = function.Parameters.Count == 0
            ? $"{recordManagedName} self"
            : string.Join(", ", new[] { $"{recordManagedName} self" }.Concat(function.Parameters.Select(p => RenderManagedImportParameter(p, compilation))));
        var returnTypeName = GetManagedImportReturnTypeName(compilation, function.ReturnType);
        var returnAttribute = GetManagedImportReturnAttribute(compilation, function.ReturnType, ReturnsOwnedHandle(function));
        var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static partial {returnTypeName} {function.ManagedName}({parameterList});"
            : $"{returnAttribute}\ninternal static partial {returnTypeName} {function.ManagedName}({parameterList});";

        writer.Write(
            $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{function.EntryPointName}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            {{importMethodLine}}
            """);
        return writer.ToString();
    }

    private static string RenderManagedInstanceImportDeclaration(BindingCompilation compilation, FunctionBinding function, string recordManagedName, bool useSafeHandleParams = false)
    {
        var writer = new CodeWriter();
        Func<ParameterBinding, string> parameterRenderer = useSafeHandleParams
            ? (Func<ParameterBinding, string>)(p => RenderManagedSafeHandleParameter(p))
            : (Func<ParameterBinding, string>)(p => RenderManagedImportParameter(p, compilation));
        var parameterList = string.Join(
            ", ",
            new[] { $"{recordManagedName} self" }.Concat(function.Parameters.Select(parameterRenderer)));
        var returnTypeName = GetManagedImportReturnTypeName(compilation, function.ReturnType);
        var returnAttribute = GetManagedImportReturnAttribute(compilation, function.ReturnType, ReturnsOwnedHandle(function));
        var importMethodName = GetManagedImportMethodName(function);
        var unsafeModifier = parameterList.Contains('*', StringComparison.Ordinal) ? "unsafe " : string.Empty;
        var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static {unsafeModifier}partial {returnTypeName} {importMethodName}({parameterList});"
            : $"{returnAttribute}\ninternal static {unsafeModifier}partial {returnTypeName} {importMethodName}({parameterList});";

        writer.Write(
            $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{function.EntryPointName}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            {{importMethodLine}}
            """);
        return writer.ToString();
    }

    private static string RenderManagedCallbaseImportDeclaration(BindingCompilation compilation, FunctionBinding function, string recordManagedName)
    {
        var writer = new CodeWriter();
        var parameterList = string.Join(
            ", ",
            new[] { $"{recordManagedName} self" }.Concat(function.Parameters.Select(parameter => RenderManagedImportParameter(parameter, compilation))));
        var returnTypeName = GetManagedImportReturnTypeName(compilation, function.ReturnType);
        var returnAttribute = GetManagedImportReturnAttribute(compilation, function.ReturnType, ReturnsOwnedHandle(function));
        var importMethodName = GetManagedCallbaseImportMethodName(function);
        var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static partial {returnTypeName} {importMethodName}({parameterList});"
            : $"{returnAttribute}\ninternal static partial {returnTypeName} {importMethodName}({parameterList});";
        var owningRecord = function.FindOwningRecord();
        var entryPointName = owningRecord is not null
            ? GetNativeCallbaseEntrypointName(owningRecord, function)
            : function.EntryPointName;
        var unsafeModifier = function.Parameters.Any(IsInputHandlePointerArrayParameter) ? "unsafe " : string.Empty;
        importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static {unsafeModifier}partial {returnTypeName} {importMethodName}({parameterList});"
            : $"{returnAttribute}\ninternal static {unsafeModifier}partial {returnTypeName} {importMethodName}({parameterList});";

        writer.Write(
            $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{entryPointName}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            {{importMethodLine}}
            """);
        return writer.ToString();
    }

    private static string RenderInterface(BindingCompilation compilation, RecordBinding record)
    {
        var writer = new CodeWriter();
        var interfaceBaseNames = record.GetManagedInterfaceBaseRecords(compilation)
            .Select(baseRecord => $"global::{GetManagedNamespace(compilation, baseRecord)}.{baseRecord.GetManagedInterfaceName()}")
            .ToArray();
        var baseClause = interfaceBaseNames.Length == 0
            ? string.Empty
            : $" : {string.Join(", ", interfaceBaseNames)}";

        writer.Write($$"""
            public partial interface {{record.GetManagedInterfaceName()}}{{baseClause}}
            {
            """);

        foreach (var method in record.GetRenderableInterfaceMethods(compilation).OrderBy(static method => method.ManagedName, StringComparer.Ordinal))
        {
            writer.WriteLine();
            var parameterList = RenderManagedPublicParameterList(method.Parameters, compilation);
            var returnTypeName = GetManagedPublicReturnTypeName(method.ReturnType, compilation);
            WriteDocumentationComment(writer, method);
            writer.Write($$"""
                {{returnTypeName}} {{method.ManagedName}}({{parameterList}});
                """);
        }

        writer.Write(
            """
            }
            """);
        return writer.ToString();
    }

    private static string RenderEnum(EnumBinding @enum)
    {
        var writer = new CodeWriter();
        WriteDocumentationComment(writer, @enum);

        if (@enum.IsFlags)
        {
            writer.Write("[Flags]");
        }

        writer.Write($$"""
            public enum {{@enum.ManagedName}} : {{GetManagedEnumUnderlyingTypeName(@enum)}}
            {
            """);

        foreach (var value in @enum.Values)
        {
            writer.Write($$"""
                {{value.ManagedName}} = {{value.ValueExpression}},
                """);
        }

        writer.Write(
            """
            }
            """);
        return writer.ToString();
    }

    private static string RenderManagedProperty(BindingCompilation compilation, FieldBinding field, bool hidesInheritedMember = false)
    {
        var writer = new CodeWriter();
        WriteDocumentationComment(writer, field);
        var propertyTypeName = GetManagedFieldPropertyTypeName(compilation, field);
        writer.Write($$"""
            {{GetManagedVisibility(field.AccessModifier)}} {{(hidesInheritedMember ? "new " : string.Empty)}}{{propertyTypeName}} {{field.ManagedName}}
            {
            get
            {
            """);

        if (NeedsManagedFixedArrayProxy(field))
        {
            writer.Write($$"""
                return {{GetManagedFixedArrayProxyFieldName(field)}} ??= new {{GetManagedFixedArrayProxyClassName(field)}}(this);
                """);
        }
        else if (field.Type.Kind == BindingTypeKind.Handle)
        {
            writer.Write($$"""
                return {{BuildManagedHandleReturnResultExpression(compilation, field.Type, $"__PInvoke.Get_{field.ManagedName}(this)", field.ReturnsOwnedHandle)}};
                """);
        }
        else
        {
            writer.Write($$"""
                return {{BuildManagedImportToPublicExpression(compilation, field.Type, $"__PInvoke.Get_{field.ManagedName}(this)")}};
                """);
        }

        writer.Write(
            """
            }
            """);

        if (!field.IsReadOnly)
        {
            if (NeedsManagedFixedArrayProxy(field))
            {
                writer.Write($$"""
                    set
                    {
                    global::System.ArgumentNullException.ThrowIfNull(value);
                    if (value.Count != {{field.Type.FixedArrayLength ?? 0}})
                    {
                        throw new global::System.ArgumentException("Expected array length {{field.Type.FixedArrayLength ?? 0}}.", nameof(value));
                    }

                    var proxy = {{GetManagedFixedArrayProxyFieldName(field)}} ??= new {{GetManagedFixedArrayProxyClassName(field)}}(this);
                    for (var index = 0; index < value.Count; ++index)
                    {
                        proxy[index] = value[index];
                    }
                    global::System.GC.KeepAlive(value);
                    }
                    """);
            }
            else
            {
                var setterParameter = new ParameterBinding(
                    $"field:set:{field.NativeName}",
                    "value",
                    "value",
                    field.Type,
                    BindingParameterDirection.In,
                    BindingParameterAbiKind.Pointer,
                    field.CppTypeName);
                var setterArgument = field.Type.IsFixedArray
                    ? "value"
                    : CanUseManagedHandleParameterImport(setterParameter, field.Type)
                    ? "value"
                    : RequiresHandleWrapping(field.Type)
                    ? "global::SharpCpp.Support.NativeObject.GetHandle(value)"
                    : BuildManagedPublicToImportExpression(compilation, field.Type, "value");
                var setterPrecondition = field.Type.IsFixedArray
                ? $$"""
                    global::System.ArgumentNullException.ThrowIfNull(value);
                    if (value.Length != {{field.Type.FixedArrayLength ?? 0}})
                    {
                        throw new global::System.ArgumentException("Expected array length {{field.Type.FixedArrayLength ?? 0}}.", nameof(value));
                    }
            """
                    : string.Empty;
                var setterKeepAlive = field.Type.IsFixedArray || RequiresHandleWrapping(field.Type)
                    ? "\n            global::System.GC.KeepAlive(value);"
                    : string.Empty;
                writer.Write($$"""
                    set
                    {
                    {{setterPrecondition}}
                    __PInvoke.Set_{{field.ManagedName}}(this, {{setterArgument}});{{setterKeepAlive}}
                    }
                    """);
            }
        }

        writer.Write(
            """
            }
            """);
        return writer.ToString();
    }

    private static string GetManagedFieldPropertyTypeName(BindingCompilation compilation, FieldBinding field)
    {
        if (!NeedsManagedFixedArrayProxy(field))
        {
            return GetManagedPublicReturnTypeName(field.Type, compilation);
        }

        var elementType = field.Type.FixedArrayElementType
            ?? throw new InvalidOperationException($"Fixed array field '{field.ManagedName}' is missing element type metadata.");
        return $"global::SharpCpp.Support.IFixedArray<{GetManagedPublicReturnTypeName(elementType, compilation)}>";
    }

    private static bool NeedsManagedFixedArrayProxy(FieldBinding field)
    {
        return field.Type.IsFixedArray && field.Type.FixedArrayElementType?.Kind == BindingTypeKind.Handle;
    }

    private static string GetManagedFixedArrayProxyClassName(FieldBinding field)
    {
        return $"__{field.ManagedName}FixedArrayProxy";
    }

    private static string GetManagedFixedArrayProxyFieldName(FieldBinding field)
    {
        return $"__{char.ToLowerInvariant(field.ManagedName[0])}{field.ManagedName[1..]}FixedArrayProxy";
    }

    private static string RenderManagedFixedArrayProxy(BindingCompilation compilation, FieldBinding field, string recordManagedName)
    {
        if (!NeedsManagedFixedArrayProxy(field))
        {
            return string.Empty;
        }

        var writer = new CodeWriter();
        var elementType = field.Type.FixedArrayElementType
            ?? throw new InvalidOperationException($"Fixed array field '{field.ManagedName}' is missing element type metadata.");
        var elementTypeName = GetManagedPublicReturnTypeName(elementType, compilation);
        var setterKeepAlive = elementType.Kind == BindingTypeKind.Handle
            ? "\n            global::System.GC.KeepAlive(value);"
            : string.Empty;

        writer.Write($$"""
            private {{GetManagedFixedArrayProxyClassName(field)}}? {{GetManagedFixedArrayProxyFieldName(field)}};

            private sealed class {{GetManagedFixedArrayProxyClassName(field)}}({{recordManagedName}} owner)
                : global::SharpCpp.Support.FixedArrayProxy<{{elementTypeName}}>({{field.Type.FixedArrayLength ?? 0}})
            {
                private readonly {{recordManagedName}} owner = owner;

                protected override {{elementTypeName}} GetAtCore(nuint index)
                {
                    var result = __PInvoke.Get_{{field.ManagedName}}_Item(owner, index);
                    global::System.GC.KeepAlive(owner);
                    return result;
                }

                protected override void SetAtCore(nuint index, {{elementTypeName}} value)
                {
                    __PInvoke.Set_{{field.ManagedName}}_Item(owner, index, value);
                    global::System.GC.KeepAlive(owner);{{setterKeepAlive}}
                }
            }
            """);
        return writer.ToString();
    }

    private static string RenderManagedFieldGetterImportDeclaration(BindingCompilation compilation, FieldBinding field, string recordManagedName, string entryPointRecordCppTypeName)
    {
        var writer = new CodeWriter();
        var returnTypeName = GetManagedImportReturnTypeName(compilation, field.Type);
        var returnAttribute = GetManagedImportReturnAttribute(compilation, field.Type, field.ReturnsOwnedHandle);
        var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static partial {returnTypeName} Get_{field.ManagedName}({recordManagedName} self);"
            : $"{returnAttribute}\ninternal static partial {returnTypeName} Get_{field.ManagedName}({recordManagedName} self);";

        writer.Write($$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetNativeFieldGetterEntrypointName(entryPointRecordCppTypeName, field)}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            {{importMethodLine}}
            """);
        return writer.ToString();
    }

    private static string RenderManagedFieldSetterImportDeclaration(BindingCompilation compilation, FieldBinding field, string recordManagedName, string entryPointRecordCppTypeName)
    {
        var writer = new CodeWriter();
        var parameter = RenderManagedImportParameter(
            new ParameterBinding(
                $"field:set:{field.NativeName}",
                "value",
                "value",
                field.Type,
                BindingParameterDirection.In,
                field.Type.Kind == BindingTypeKind.Handle ? BindingParameterAbiKind.Value : BindingParameterAbiKind.Pointer,
                field.CppTypeName),
            compilation);

        writer.Write($$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetNativeFieldSetterEntrypointName(entryPointRecordCppTypeName, field)}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static partial void Set_{{field.ManagedName}}({{recordManagedName}} self, {{parameter}});
            """);
        return writer.ToString();
    }

    private static string RenderManagedFieldItemGetterImportDeclaration(BindingCompilation compilation, FieldBinding field, string recordManagedName, string entryPointRecordCppTypeName)
    {
        var writer = new CodeWriter();
        var elementType = field.Type.FixedArrayElementType
            ?? throw new InvalidOperationException($"Fixed array field '{field.ManagedName}' is missing element type metadata.");
        var returnTypeName = GetManagedImportReturnTypeName(compilation, elementType);
        var returnAttribute = GetManagedImportReturnAttribute(compilation, elementType, ownsHandle: false);
        var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
            ? $"internal static partial {returnTypeName} Get_{field.ManagedName}_Item({recordManagedName} self, nuint index);"
            : $"{returnAttribute}\ninternal static partial {returnTypeName} Get_{field.ManagedName}_Item({recordManagedName} self, nuint index);";

        writer.Write($$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetNativeFieldItemGetterEntrypointName(entryPointRecordCppTypeName, field)}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            {{importMethodLine}}
            """);
        return writer.ToString();
    }

    private static string RenderManagedFieldItemSetterImportDeclaration(BindingCompilation compilation, FieldBinding field, string recordManagedName, string entryPointRecordCppTypeName)
    {
        var writer = new CodeWriter();
        var elementType = field.Type.FixedArrayElementType
            ?? throw new InvalidOperationException($"Fixed array field '{field.ManagedName}' is missing element type metadata.");
        var parameter = RenderManagedImportParameter(
            new ParameterBinding(
                $"field:set:item:{field.NativeName}",
                "value",
                "value",
                elementType,
                BindingParameterDirection.In,
                elementType.Kind == BindingTypeKind.Handle ? BindingParameterAbiKind.Value : BindingParameterAbiKind.Pointer,
                elementType.CppTypeName),
            compilation);

        writer.Write($$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{GetNativeFieldItemSetterEntrypointName(entryPointRecordCppTypeName, field)}}", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static partial void Set_{{field.ManagedName}}_Item({{recordManagedName}} self, nuint index, {{parameter}});
            """);
        return writer.ToString();
    }

    private static string RenderNativeBindings(
        BindingCompilation compilation,
        Func<BindingNode, bool>? memberFilter = null,
        string? groupHeaderPath = null,
        bool includeCallbackReleaseRegistration = true)
    {
        var writer = new CodeWriter();
        var allFunctions = compilation.Functions.AsEnumerable();
        var allRecords = compilation.Records.Where(static record => !record.GetMetadata<bool>(MetadataKeys.SuppressEmission)).AsEnumerable();

        if (memberFilter is not null)
        {
            allFunctions = allFunctions.Where(f => memberFilter(f));
            allRecords = allRecords.Where(r => memberFilter(r));
        }

        var functions = allFunctions.OrderBy(static function => function.ManagedName, StringComparer.Ordinal).ToArray();
        var records = allRecords.OrderBy(static record => record.ManagedName, StringComparer.Ordinal).ToArray();

        writer.Write(
            """
            // <auto-generated />
            #include "SharpCpp/Export.hpp"
            #include "SharpCpp/Marshaller.hpp"
            #include "SharpCpp/detail/MarshallerSupport.hpp"
            #include <stdexcept>
            #include <typeinfo>

            using namespace SharpCpp;
            """);

        foreach (var headerFile in compilation.HeaderFiles.OrderBy(static header => header, StringComparer.Ordinal))
        {
            writer.Write($$"""

                #include "{{headerFile}}"
                """);
        }

        foreach (var headerFile in compilation.NativeSupportHeaders.OrderBy(static header => header, StringComparer.Ordinal))
        {
            writer.Write($$"""

                #include "{{headerFile}}"
                """);
        }

        if (includeCallbackReleaseRegistration)
        {
            writer.Write($$"""

                SHARPCPP_EXPORT void {{GetManagedCallbackReleaseRegistrationEntryPointName()}}(void (*release)(void*))
                {
                    SharpCpp::detail::SetManagedCallbackRelease(release);
                }
                """);
        }

        foreach (var record in records.Where(record => record.RequiresDirector(compilation)).OrderBy(static record => record.ManagedName, StringComparer.Ordinal))
        {
            writer.WriteLine();
            writer.Write(RenderNativeDirectorClass(compilation, record));
        }

        var directorRecords = records.Where(record => record.RequiresDirector(compilation)).ToArray();
        foreach (var record in directorRecords)
        {
            var callbaseMethods = record.GetEffectiveVirtualMethodSlots(compilation)
                .Select(static slot => slot.Method)
                .Where(static method => !method.IsPureVirtual)
                .ToArray();
            foreach (var method in callbaseMethods)
            {
                var parameterList = string.Join(", ", new[] { $"arg_t<{record.CppQualifiedName}*> self" }.Concat(method.Parameters.Select(RenderNativeParameter)));
                var abiArguments = string.Join(", ", method.Parameters.Select(static p => p.NativeName));
                var parameterCppList = string.Join(", ", method.Parameters.Select(static p => p.CppTypeName));
                var callbaseArguments = string.Join(", ", method.Parameters.Select(static p => p.NativeName));
                var directorName = GetNativeDirectorClassName(record);
                var helperName = GetNativeCallbaseHelperName(method);
                var marshallerTemplateArguments = BuildMarshallerInvokerTemplateArguments(
                    "FT",
                    new[] { $"{record.CppQualifiedName}*" }.Concat(method.Parameters.Select(static p => p.CppTypeName)));

                writer.WriteLine();
                if (method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
                {
                    writer.Write($$"""
                        SHARPCPP_EXPORT ret_t<void> {{GetNativeCallbaseEntrypointName(record, method)}}({{parameterList}})
                        {
                        using FT = void({{record.CppQualifiedName}}*{{(parameterCppList.Length > 0 ? $", {parameterCppList}" : string.Empty)}});
                        MarshallerInvoker::invoke_as<{{marshallerTemplateArguments}}>(&{{directorName}}::{{helperName}}, self{{(callbaseArguments.Length > 0 ? $", {callbaseArguments}" : string.Empty)}});
                        }
                        """);
                }
                else
                {
                    writer.Write($$"""
                        SHARPCPP_EXPORT {{GetNativeEntrypointReturnType(method)}} {{GetNativeCallbaseEntrypointName(record, method)}}({{parameterList}})
                        {
                        using FT = {{method.CppReturnTypeName}}({{record.CppQualifiedName}}*{{(parameterCppList.Length > 0 ? $", {parameterCppList}" : string.Empty)}});
                        {{(RequiresOwnedRecordValueReturn(method)
                            ? $"return MarshallerInvoker::invoke_owned_as<{marshallerTemplateArguments}>(&{directorName}::{helperName}, self{(callbaseArguments.Length > 0 ? $", {callbaseArguments}" : string.Empty)});"
                            : $"return MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>(&{directorName}::{helperName}, self{(callbaseArguments.Length > 0 ? $", {callbaseArguments}" : string.Empty)});")}}
                        }
                        """);
                }

                if (HasNativeDefaultDispatchParams(method) || HasUndefaultableParams(method))
                {
                    writer.WriteLine();
                    writer.Write(RenderNativeUnexpressibleDefaultCallbase(record, method));
                }
            }
        }

        foreach (var record in records.Where(static record => record.IsGeneric && !record.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static record => record.ManagedName, StringComparer.Ordinal))
        {
            if (IsGenericArrayRecord(record))
            {
                writer.Write(RenderNativeGenericArrayRecord(compilation, record));
                continue;
            }

            foreach (var instantiation in record.SupportedGenericInstantiations)
            {
                foreach (var constructor in record.Members.OfType<FunctionBinding>().Where(static function => function.IsConstructor && !function.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static function => function.Parameters.Count))
                {
                    writer.WriteLine();
                    writer.Write(RenderNativeGenericConstructor(record, constructor, instantiation));
                }

                foreach (var method in record.Members.OfType<FunctionBinding>().Where(static function => !function.IsConstructor && !function.IsStatic && !function.IsGeneric && function.OperatorToken is null && !function.IsConversionOperator && !function.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static function => function.ManagedName, StringComparer.Ordinal))
                {
                    writer.WriteLine();
                    writer.Write(RenderNativeGenericInstanceMethod(record, method, instantiation));
                }

                foreach (var method in record.Members.OfType<FunctionBinding>().Where(static function => function.IsStatic && !function.IsGeneric && function.OperatorToken is null && !function.IsConversionOperator && !function.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static function => function.ManagedName, StringComparer.Ordinal))
                {
                    writer.WriteLine();
                    writer.Write(RenderNativeGenericStaticMethod(record, method, instantiation));
                }

                if (CanEmitClone(record, isEffectivelyAbstract: false, canEmitDeleteThunk: true))
                {
                    writer.WriteLine();
                    writer.Write(RenderNativeGenericClone(record, instantiation));
                }

                writer.WriteLine();
                writer.Write(RenderNativeGenericDelete(record, instantiation));
            }

            var genericInstanceMethods = record.Members.OfType<FunctionBinding>()
                .Where(static function => !function.IsConstructor && !function.IsStatic && function.IsGeneric && function.OperatorToken is null && !function.IsConversionOperator && !function.HasMetadata(MetadataKeys.IgnoreReason))
                .OrderBy(static function => function.ManagedName, StringComparer.Ordinal).ToArray();
            foreach (var method in genericInstanceMethods)
            {
                var methodInstantiations = method.SupportedGenericInstantiations.ToArray();
                if (methodInstantiations.Length == 0)
                {
                    continue;
                }

                foreach (var instantiation in record.SupportedGenericInstantiations)
                {
                    foreach (var methodInstantiation in methodInstantiations)
                    {
                        writer.WriteLine();
                        writer.Write(RenderNativeGenericMethodInstanceMethod(record, method, instantiation, methodInstantiation));
                    }
                }
            }
        }

        foreach (var function in functions)
        {
            if (function.Parent is RecordBinding || function.IsConstructor || function.HasMetadata(MetadataKeys.IgnoreReason))
            {
                continue;
            }

            if (function.IsGeneric)
            {
                foreach (var instantiation in function.SupportedGenericInstantiations)
                {
                    writer.WriteLine();
                    writer.Write(RenderNativeGenericFreeFunction(function, instantiation));
                }

                continue;
            }

            var parameterList = string.Join(", ", function.Parameters.Select(RenderNativeParameter));
            var abiArguments = string.Join(", ", function.Parameters.Select(static p => p.NativeName));
            var signature = BuildNativeFunctionSignature(function);
            var marshallerTemplateArguments = BuildMarshallerInvokerTemplateArguments("FT", function.Parameters.Select(static p => p.CppTypeName));

            writer.WriteLine();
            writer.Write($$"""
                SHARPCPP_EXPORT {{GetNativeEntrypointReturnType(function)}} {{function.EntryPointName}}({{parameterList}})
                {
                using FT = {{signature}};
                """);

            if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
            {
                writer.Write($$"""
                    MarshallerInvoker::invoke_as<{{marshallerTemplateArguments}}>(static_cast<std::add_pointer_t<FT>>(&{{function.CppQualifiedName}}){{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)}});
                    """);
            }
            else
            {
                writer.Write($$"""
                    {{(RequiresOwnedRecordValueReturn(function)
                        ? $"return MarshallerInvoker::invoke_owned_as<{marshallerTemplateArguments}>(static_cast<std::add_pointer_t<FT>>(&{function.CppQualifiedName}){(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});"
                        : $"return MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>(static_cast<std::add_pointer_t<FT>>(&{function.CppQualifiedName}){(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});")}}
                    """);
            }

            writer.Write("""
                }
                """);

            if (HasNativeDefaultDispatchParams(function) || HasUndefaultableParams(function))
            {
                writer.WriteLine();
                writer.Write(RenderNativeUnexpressibleDefaultFreeFunction(function));
            }
        }

        foreach (var method in records.Where(static record => !record.IsGeneric && !record.HasMetadata(MetadataKeys.IgnoreReason)).SelectMany(static record => record.Members.OfType<FunctionBinding>()).Where(static function => !function.IsConstructor && !function.IsStatic && !function.IsGeneric && !function.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static method => method.ManagedName, StringComparer.Ordinal))
        {
            var parameterList = string.Join(", ", new[] { $"arg_t<{method.OwningRecordCppTypeName}*> self" }.Concat(method.Parameters.Select(RenderNativeParameter)));
            var abiArguments = string.Join(", ", method.Parameters.Select(static p => p.NativeName));
            var memberSignature = BuildNativeMethodSignature(method);

            writer.WriteLine();
            if (method.TryGetMetadata(MetadataKeys.MarshallerExposedMethod, out string? marshallerCppType))
            {
                writer.Write($$"""
                    SHARPCPP_EXPORT {{GetNativeEntrypointReturnType(method)}} {{method.EntryPointName}}({{parameterList}})
                    {
                    {{RenderNativeMarshallerExposedInvocation(method, marshallerCppType!, abiArguments)}}
                    }
                    """);
            }
            else
            {
            writer.Write($$"""
                SHARPCPP_EXPORT {{GetNativeEntrypointReturnType(method)}} {{method.EntryPointName}}({{parameterList}})
                {
                using FT = {{memberSignature}};
                """);

            var owningRecord = method.Parent as RecordBinding;
            writer.Write(method.AccessModifier == BindingAccessModifier.Protected && owningRecord is not null && owningRecord.RequiresDirector(compilation)
                ? RenderNativeProtectedMethodInvocation(compilation, method, abiArguments, memberSignature)
                : RenderNativeMethodInvocation(method, abiArguments, memberSignature));

            writer.Write("""
                }
                """);
            }

            if (HasNativeDefaultDispatchParams(method) || HasUndefaultableParams(method))
            {
                writer.WriteLine();
                writer.Write(RenderNativeUnexpressibleDefaultInstanceMethod(method));
            }
        }

        foreach (var record in records.Where(static record => !record.IsGeneric && !record.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static record => record.ManagedName, StringComparer.Ordinal))
        {
            foreach (var method in record.Members.OfType<FunctionBinding>().Where(static function => !function.IsConstructor && !function.IsStatic && function.IsGeneric && function.OperatorToken is null && !function.IsConversionOperator && !function.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static method => method.ManagedName, StringComparer.Ordinal))
            {
                foreach (var methodInstantiation in method.SupportedGenericInstantiations)
                {
                    writer.WriteLine();
                    writer.Write(RenderNativeGenericMethodInstanceMethod(record, method, EmptyRecordGenericInstantiation(), methodInstantiation));
                }
            }
        }

        foreach (var method in records.Where(static record => !record.IsGeneric && !record.HasMetadata(MetadataKeys.IgnoreReason)).SelectMany(static record => record.Members.OfType<FunctionBinding>()).Where(static function => function.IsStatic && !function.HasMetadata(MetadataKeys.IgnoreReason) && function.AccessModifier != BindingAccessModifier.Protected).OrderBy(static method => method.ManagedName, StringComparer.Ordinal))
        {
            var parameterList = string.Join(", ", method.Parameters.Select(RenderNativeParameter));
            var abiArguments = string.Join(", ", method.Parameters.Select(static p => p.NativeName));
            var signature = BuildNativeFunctionSignature(method);
            var marshallerTemplateArguments = BuildMarshallerInvokerTemplateArguments("FT", method.Parameters.Select(static p => p.CppTypeName));

            writer.WriteLine();
            writer.Write($$"""
                SHARPCPP_EXPORT {{GetNativeEntrypointReturnType(method)}} {{method.EntryPointName}}({{parameterList}})
                {
                using FT = {{signature}};
                """);

            if (method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
            {
                writer.Write($$"""
                    MarshallerInvoker::invoke_as<{{marshallerTemplateArguments}}>(&{{method.OwningRecordCppTypeName}}::{{method.NativeName}}{{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)}});
                    """);
            }
            else
            {
                writer.Write($$"""
                    {{(RequiresOwnedRecordValueReturn(method)
                        ? $"return MarshallerInvoker::invoke_owned_as<{marshallerTemplateArguments}>(&{method.OwningRecordCppTypeName}::{method.NativeName}{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});"
                        : $"return MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>(&{method.OwningRecordCppTypeName}::{method.NativeName}{(abiArguments.Length > 0 ? $", {abiArguments}" : string.Empty)});")}}
                    """);
            }

            writer.Write("""
                }
                """);

            if (HasNativeDefaultDispatchParams(method) || HasUndefaultableParams(method))
            {
                writer.WriteLine();
                writer.Write(RenderNativeUnexpressibleDefaultStaticMethod(method));
            }
        }

        foreach (var constructor in records.Where(static record => !record.IsGeneric && !record.HasMetadata(MetadataKeys.IgnoreReason)).SelectMany(static record => record.Members.OfType<FunctionBinding>()).Where(static function => function.IsConstructor && !function.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static function => function.ManagedName, StringComparer.Ordinal))
        {
            var parameterList = string.Join(", ", constructor.Parameters.Select(RenderNativeParameter));
            var abiArguments = string.Join(", ", constructor.Parameters.Select(static p => p.NativeName));
            var constructorArgTypes = string.Join(", ", constructor.Parameters.Select(static p => p.CppTypeName));
            var owningRecord = constructor.Parent as RecordBinding;

            writer.WriteLine();
            writer.Write($$"""
                SHARPCPP_EXPORT ret_t<{{constructor.CppReturnTypeName}}> {{constructor.EntryPointName}}({{parameterList}})
                {
                {{(owningRecord is not null && owningRecord.RequiresDirector(compilation)
                    ? $"return new {GetNativeDirectorClassName(owningRecord)}({abiArguments});"
                    : $"return MarshallerInvoker::construct<{constructor.OwningRecordCppTypeName}{(constructorArgTypes.Length > 0 ? $", {constructorArgTypes}" : string.Empty)}>({abiArguments});")}}
                }
                """);

            if (HasNativeDefaultDispatchParams(constructor) || HasUndefaultableParams(constructor))
            {
                writer.WriteLine();
                writer.Write(RenderNativeUnexpressibleDefaultConstructor(constructor, owningRecord is not null && owningRecord.RequiresDirector(compilation)));
            }
        }

        foreach (var (accessorRecord, field) in records
                     .Where(static record => !record.HasMetadata(MetadataKeys.IgnoreReason))
                     .SelectMany(record => GetNativeFieldEntries(compilation, record))
                     .Where(static entry => !entry.Field.HasMetadata(MetadataKeys.IgnoreReason))
                     .OrderBy(static entry => entry.Field.ManagedName, StringComparer.Ordinal))
        {
            if (NeedsManagedFixedArrayProxy(field))
            {
                writer.WriteLine();
                writer.Write($$"""
                    SHARPCPP_EXPORT {{GetNativeFixedArrayItemGetterReturnType(compilation, field)}} {{GetNativeFieldItemGetterEntrypointName(accessorRecord.CppQualifiedName, field)}}(arg_t<{{accessorRecord.CppQualifiedName}}*> self, std::size_t index)
                    {
                    {{(field.AccessModifier == BindingAccessModifier.Protected
                        ? RenderNativeProtectedFieldItemGetInvocation(accessorRecord, field)
                        : RenderNativeFixedArrayItemGetInvocation(compilation, field, "self"))}}
                    }
                    """);

                if (!field.IsReadOnly)
                {
                    writer.WriteLine();
                    writer.Write($$"""
                        SHARPCPP_EXPORT ret_t<void> {{GetNativeFieldItemSetterEntrypointName(accessorRecord.CppQualifiedName, field)}}(arg_t<{{accessorRecord.CppQualifiedName}}*> self, std::size_t index, {{GetNativeFixedArrayItemSetterParameterType(compilation, field)}} value)
                        {
                        {{(field.AccessModifier == BindingAccessModifier.Protected
                            ? RenderNativeProtectedFieldItemSetInvocation(accessorRecord, field)
                            : RenderNativeFixedArrayItemSetInvocation(compilation, field, "self"))}}
                        }
                        """);
                }
            }
            else
            {
                var plan = CreateNativeFieldInteropPlan(compilation, field);
                var nativeFieldCppType = plan.StorageCppType;
                var isNativeFixedArrayField = IsNativeFixedArrayType(compilation, field.Type);
                var getterReturnType = isNativeFixedArrayField ? "NativeArrayAbi" : $"ret_t<{nativeFieldCppType}>";
                writer.WriteLine();
                writer.Write($$"""
                    SHARPCPP_EXPORT {{getterReturnType}} {{GetNativeFieldGetterEntrypointName(accessorRecord.CppQualifiedName, field)}}(arg_t<{{accessorRecord.CppQualifiedName}}*> self)
                    {
                    {{(field.AccessModifier == BindingAccessModifier.Protected
                        ? RenderNativeProtectedFieldGetInvocation(accessorRecord, field)
                        : RenderNativeFieldGetInvocation(compilation, field, plan, "self"))}}
                    }
                    """);

                if (!field.IsReadOnly)
                {
                    var setterParameterType = isNativeFixedArrayField ? "NativeArrayAbi" : plan.SetterParameterCppType;
                    writer.WriteLine();
                    writer.Write($$"""
                        SHARPCPP_EXPORT ret_t<void> {{GetNativeFieldSetterEntrypointName(accessorRecord.CppQualifiedName, field)}}(arg_t<{{accessorRecord.CppQualifiedName}}*> self, {{setterParameterType}} value)
                        {
                        {{(field.AccessModifier == BindingAccessModifier.Protected
                            ? RenderNativeProtectedFieldSetInvocation(accessorRecord, field)
                            : RenderNativeFieldSetInvocation(compilation, field, plan, "self"))}}
                        }
                        """);
                }
            }
        }

        foreach (var record in records.Where(static record => !record.IsGeneric && !record.HasMetadata(MetadataKeys.IgnoreReason)).OrderBy(static record => record.ManagedName, StringComparer.Ordinal))
        {
            writer.WriteLine();
            if (CanEmitDeleteThunk(record))
            {
                writer.Write(RenderNativeDelete(record, record.RequiresDirector(compilation)));
            }

            if (RequiresArrayElementAccess(compilation, record))
            {
                writer.WriteLine();
                writer.Write(RenderNativeArrayElement(record));
            }


            if (TryGetNativeInterfaceQuery(record, out var nativeInterfaceQuery))
            {
                writer.WriteLine();
                writer.Write(RenderNativeInterfaceQuery(record, nativeInterfaceQuery));
            }

            if (record.TryGetMetadata(MetadataKeys.NativeInterfaceReleaseMethod, out string? nativeInterfaceReleaseMethod))
            {
                writer.WriteLine();
                writer.Write(RenderNativeInterfaceRelease(record, nativeInterfaceReleaseMethod!));
            }

            if (CanEmitClone(record, record.IsEffectivelyAbstract || record.HasUnimplementedAbstractBaseMethods(compilation), CanEmitDeleteThunk(record)))
            {
                writer.WriteLine();
                writer.Write(RenderNativeClone(record));
            }

            if (record.RequiresDirector(compilation))
            {
                writer.WriteLine();
                writer.Write(RenderNativeConnectDirector(record, record.GetEffectiveVirtualMethodSlots(compilation)));
            }

            if (!record.IsPolymorphic || IsNativeInterfaceWrapper(record))
            {
                continue;
            }

            writer.WriteLine();
            writer.Write($$"""
                SHARPCPP_EXPORT ret_t<std::size_t> sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_type_id()
                {
                return typeid({{record.CppQualifiedName}}).hash_code();
                }
                """);

            writer.WriteLine();
            writer.Write($$"""
                SHARPCPP_EXPORT ret_t<void> sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_metadata(arg_t<{{record.CppQualifiedName}}*> self, void** handle, std::size_t* dynamicTypeId)
                {
                if (handle == nullptr || dynamicTypeId == nullptr)
                {
                return;
                }

                if (self == nullptr)
                {
                *handle = nullptr;
                *dynamicTypeId = 0;
                return;
                }

                *handle = dynamic_cast<void*>(self);
                *dynamicTypeId = typeid(*self).hash_code();
                }
                """);
        }

        return writer.ToString();
    }

    private static string GetManagedWrapperTypeName(BindingType type, BindingCompilation? compilation = null)
    {
        if (compilation is not null)
        {
            return FormatGlobalTypeName(type, compilation);
        }

        return type.Kind switch
        {
            BindingTypeKind.Primitive => type.PrimitiveKind != BindingPrimitiveKind.Unsupported
                ? GetConcretePrimitiveManagedTypeName(type)
                : type.ManagedTypeName,
            BindingTypeKind.Handle => type.ManagedTypeName,
            BindingTypeKind.Enum => NormalizeEnumManagedTypeName(type.ManagedTypeName),
            BindingTypeKind.GenericParameter => type.ManagedTypeName,
            _ => throw new NotSupportedException($"Managed wrapper emission does not support '{type.Kind}'.")
        };
    }

    private static bool TryGetPublicAliasName(BindingType type, BindingCompilation compilation, out string aliasName)
    {
        var unaliasedType = type.WithManagedTypeFormatOverride(null);
        var targetKey = BindingAliasTargetKey.From(unaliasedType);
        if (!string.IsNullOrWhiteSpace(type.ManagedTypeFormatOverride))
        {
            var explicitAlias = compilation.TypeAliases.FirstOrDefault(alias =>
                string.Equals(alias.ManagedName, type.ManagedTypeFormatOverride, StringComparison.Ordinal) &&
                BindingAliasTargetKey.From(alias.TargetType) == targetKey);
            if (explicitAlias is not null)
            {
                aliasName = explicitAlias.ManagedName;
                return true;
            }
        }

        var matches = compilation.TypeAliases
            .Where(alias => BindingAliasTargetKey.From(alias.TargetType) == targetKey)
            .Select(static alias => alias.ManagedName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (matches.Length == 1)
        {
            aliasName = matches[0];
            return true;
        }

        aliasName = string.Empty;
        return false;
    }

    private static string GetManagedPublicReturnTypeName(BindingType type, BindingCompilation? compilation = null)
    {
        if (compilation is not null)
        {
            if (TryGetPublicAliasName(type, compilation, out var aliasName))
            {
                return aliasName;
            }

            if (type.Kind == BindingTypeKind.Handle)
            {
                return FormatQualifiedRecordTypeName(compilation, type, publicSurface: true);
            }

            if (type.Kind == BindingTypeKind.Enum)
            {
                return GetQualifiedEnumTypeName(compilation, type);
            }

            if (type.Kind == BindingTypeKind.Primitive && type.PrimitiveKind == BindingPrimitiveKind.Unsupported)
            {
                return FormatConfiguredManagedTypeName(type, compilation, publicSurface: true);
            }
        }

        return GetManagedWrapperTypeName(type, compilation);
    }

    /// <summary>
    /// Writes the P/Invoke result variable, post-call statements, and conversion
    /// for types where import type differs from public type (e.g., IntPtr → string).
    /// This path is used when a marshaller_type is set but NO managed_marshaller.
    /// New types should use managed_marshaller (C# custom marshaller) instead.
    /// </summary>
    private static void WriteManagedReturnWithMarshallerConversion(
        BindingCompilation compilation,
        CodeWriter writer,
        FunctionBinding function,
        string importMethodName,
        string invokeArguments,
        ManagedInvocationPlan invocationPlan)
    {
        var importTypeName = GetManagedImportTypeName(function.ReturnType, compilation);
        var publicTypeName = function.ReturnType.ManagedTypeName;
        writer.Write($$"""
            var __result_raw = __PInvoke.{{importMethodName}}({{invokeArguments}});
            """);

        foreach (var statement in invocationPlan.PostCallStatements)
        {
            writer.Write(statement);
        }

        var conversionExpression = importTypeName == BindingType.IntPtr.ManagedTypeName &&
                                   string.Equals(function.ReturnType.Marshaller.PublicTypeName, "string", StringComparison.Ordinal)
            ? "global::System.Runtime.InteropServices.Marshal.PtrToStringUTF8(__result_raw)!"
            : "__result_raw";
        writer.Write($$"""
            return {{conversionExpression}};
            """);
    }

    private static string GetManagedImportTypeName(BindingType type, BindingCompilation? compilation = null)
    {
        if (type.MarshallerTypeName is not null)
        {
            return type.MarshallerTypeName;
        }

        if (compilation is not null)
        {
            return type.Kind switch
            {
                BindingTypeKind.Primitive when type.PrimitiveKind != BindingPrimitiveKind.Unsupported => GetConcretePrimitiveManagedTypeName(type),
                BindingTypeKind.Primitive => FormatGlobalTypeName(type, compilation),
                BindingTypeKind.Handle => BindingType.NativeInt.ManagedTypeName,
                BindingTypeKind.Enum => FormatGlobalTypeName(type, compilation),
                BindingTypeKind.GenericParameter => type.UnaliasedManagedTypeName,
                _ => throw new NotSupportedException($"Managed import emission does not support '{type.Kind}'.")
            };
        }

        return type.Kind switch
        {
            BindingTypeKind.Primitive => type.PrimitiveKind != BindingPrimitiveKind.Unsupported
                ? GetConcretePrimitiveManagedTypeName(type)
                : type.UnaliasedManagedTypeName,
            BindingTypeKind.Handle => BindingType.NativeInt.ManagedTypeName,
            BindingTypeKind.Enum => NormalizeEnumManagedTypeName(type.UnaliasedManagedTypeName),
            BindingTypeKind.GenericParameter => type.UnaliasedManagedTypeName,
            _ => throw new NotSupportedException($"Managed import emission does not support '{type.Kind}'.")
        };
    }

    private static string GetManagedImportReturnTypeName(BindingCompilation compilation, BindingType type)
    {
        if (type.IsFixedArray)
        {
            return GetManagedWrapperTypeName(type, compilation);
        }

        return CanUseManagedHandleReturnMarshaller(compilation, type)
            ? GetManagedWrapperTypeName(type, compilation)
            : GetManagedImportTypeName(type, compilation);
    }

    private static string GetManagedImportReturnAttribute(BindingCompilation compilation, BindingType type, bool ownsHandle)
    {
        if (type.IsFixedArray)
        {
            return $"[return: MarshalUsing(typeof({GetManagedFixedArrayReturnMarshallerTypeName(compilation, type, ownsHandle)}))]";
        }

        if (CanUseManagedHandleReturnMarshaller(compilation, type))
        {
            return $"[return: MarshalUsing(typeof({GetManagedHandleReturnMarshallerTypeName(compilation, type, ownsHandle)}))]";
        }

        return GetManagedReturnAttribute(type);
    }

    private static bool CanUseManagedHandleReturnMarshaller(BindingCompilation compilation, BindingType type)
    {
        return !type.IsFixedArray && type.Kind == BindingTypeKind.Handle && ResolveRecord(compilation, type) is not null;
    }

    private static string GetManagedHandleReturnMarshallerTypeName(BindingCompilation compilation, BindingType type, bool ownsHandle)
    {
        var record = ResolveRecord(compilation, type)
            ?? throw new InvalidOperationException($"Unable to resolve handle return marshaller record for '{type.ManagedTypeName}'.");
        return CanUseUnifiedHandleMarshaller(record)
            ? GetQualifiedUnifiedHandleMarshallerTypeName(
                compilation,
                record,
                ownsHandle ? OwnedNativeHandleFullName : BorrowedNativeHandleFullName)
            : GetQualifiedHandleReturnMarshallerTypeName(compilation, record, ownsHandle);
    }

    private static string BuildManagedHandleReturnResultExpression(BindingCompilation compilation, BindingType type, string resultExpression, bool ownsHandle)
    {
        if (type.IsFixedArray)
        {
            return resultExpression;
        }

        return CanUseManagedHandleReturnMarshaller(compilation, type)
            ? resultExpression
            : BuildManagedHandleWrap(compilation, type, resultExpression, ownsHandle);
    }

    private static string GetHandleMarshallerDeclarationName(RecordBinding record)
    {
        return $"__{record.ManagedName}Marshaller<TOwnership>";
    }

    private static bool CanUseUnifiedHandleMarshaller(RecordBinding record)
    {
        return GetManagedVisibleGenericParameters(record).Count == 0;
    }

    private static string GetHandleMarshallerGenericConstraints()
    {
        var ownershipConstraint = $"where TOwnership : {NativeHandleOwnershipConstraintFullName}";
        return $"\n{ownershipConstraint}";
    }

    private static string GetHandleReturnMarshallerDeclarationName(RecordBinding record, bool ownsHandle)
    {
        var visibleParameters = GetManagedVisibleGenericParameters(record);
        var genericSuffix = visibleParameters.Count == 0
            ? string.Empty
            : $"<{string.Join(", ", visibleParameters.Select(static parameter => parameter.Name))}>";
        return $"__{record.ManagedName}{(ownsHandle ? "Owned" : string.Empty)}ReturnMarshaller{genericSuffix}";
    }

    private static string GetHandleInMarshallerDeclarationName(RecordBinding record)
    {
        var visibleParameters = GetManagedVisibleGenericParameters(record);
        var genericSuffix = visibleParameters.Count == 0
            ? string.Empty
            : $"<{string.Join(", ", visibleParameters.Select(static parameter => parameter.Name))}>";
        return $"__{record.ManagedName}InMarshaller{genericSuffix}";
    }

    private static string GetQualifiedUnifiedHandleMarshallerTypeName(BindingCompilation compilation, RecordBinding record, string ownershipTypeName)
    {
        var ns = GetManagedNamespace(compilation, record);
        return $"global::{ns}.__{record.ManagedName}Marshaller<{ownershipTypeName}>";
    }

    private static string GetQualifiedHandleReturnMarshallerTypeName(BindingCompilation compilation, RecordBinding record, bool ownsHandle)
    {
        var ns = GetManagedNamespace(compilation, record);
        var arity = GetManagedVisibleGenericParameters(record).Count;
        var openGenericSuffix = arity == 0 ? string.Empty : $"<{new string(',', arity - 1)}>";
        return $"global::{ns}.__{record.ManagedName}{(ownsHandle ? "Owned" : string.Empty)}ReturnMarshaller{openGenericSuffix}";
    }

    private static string GetQualifiedHandleInMarshallerTypeName(BindingCompilation compilation, RecordBinding record)
    {
        var ns = GetManagedNamespace(compilation, record);
        var arity = GetManagedVisibleGenericParameters(record).Count;
        var openGenericSuffix = arity == 0 ? string.Empty : $"<{new string(',', arity - 1)}>";
        return $"global::{ns}.__{record.ManagedName}InMarshaller{openGenericSuffix}";
    }

    private static string GetManagedFixedArrayReturnMarshallerTypeName(BindingCompilation compilation, BindingType type, bool ownsHandle)
    {
        var record = ResolveRecord(compilation, type)
            ?? throw new InvalidOperationException($"Unable to resolve fixed-array return marshaller record for '{type.ManagedTypeName}'.");
        return GetQualifiedHandleReturnMarshallerTypeName(compilation, record, ownsHandle);
    }

    private static string GetManagedFixedArrayInMarshallerAttribute(BindingCompilation compilation, BindingType type)
    {
        var record = ResolveRecord(compilation, type)
            ?? throw new InvalidOperationException($"Unable to resolve fixed-array parameter marshaller record for '{type.ManagedTypeName}'.");
        return $"[MarshalUsing(typeof({GetQualifiedHandleInMarshallerTypeName(compilation, record)}))] ";
    }

    private static string GetQualifiedManagedRecordDeclarationTypeName(BindingCompilation compilation, RecordBinding record, string recordDeclarationName)
    {
        var ns = GetManagedNamespace(compilation, record);
        return $"global::{ns}.{recordDeclarationName}";
    }

    private static string GetQualifiedManagedOpenRecordTypeName(BindingCompilation compilation, RecordBinding record)
    {
        var ns = GetManagedNamespace(compilation, record);
        var arity = GetManagedVisibleGenericParameters(record).Count;
        var openGenericSuffix = arity == 0 ? string.Empty : $"<{new string(',', arity - 1)}>";
        return $"global::{ns}.{record.ManagedName}{openGenericSuffix}";
    }

    private static string GetManagedHandleInMarshallerAttribute(BindingCompilation compilation, BindingType type)
    {
        var record = ResolveRecord(compilation, type);
        if (record is null)
        {
            return string.Empty;
        }

        return CanUseUnifiedHandleMarshaller(record)
            ? $"[MarshalUsing(typeof({GetQualifiedUnifiedHandleMarshallerTypeName(compilation, record, BorrowedNativeHandleFullName)}))] "
            : $"[MarshalUsing(typeof({GetQualifiedHandleInMarshallerTypeName(compilation, record)}))] ";
    }

    private static string RenderManagedSafeHandleParameter(ParameterBinding parameter, BindingCompilation? compilation = null)
    {
        var modifier = parameter.Direction switch
        {
            BindingParameterDirection.In when parameter.AbiKind != BindingParameterAbiKind.Value => "in ",
            BindingParameterDirection.Ref => "ref ",
            BindingParameterDirection.Out => "out ",
            _ => string.Empty
        };
        var attribute = GetManagedParameterAttribute(parameter, compilation);
        var typeName = parameter.Type.Kind == BindingTypeKind.Handle
            ? $"{GetManagedWrapperTypeName(parameter.Type, compilation)}?"
            : GetManagedWrapperTypeName(parameter.Type, compilation);

        return $"{attribute}{modifier}{typeName} {parameter.ManagedName}";
    }

    private static bool CanUseManagedHandleParameterImport(ParameterBinding parameter, BindingType type)
    {
        return type.Kind == BindingTypeKind.Handle &&
            parameter.Direction is not (BindingParameterDirection.Ref or BindingParameterDirection.Out);
    }

    private static string RenderManagedImportParameter(ParameterBinding parameter, BindingCompilation? compilation = null)
    {
        return RenderManagedImportParameter(parameter, parameter.Type, compilation);
    }

    private static string RenderManagedImportParameter(ParameterBinding parameter, BindingType type, BindingCompilation? compilation = null)
    {
        return RenderManagedImportParameter(parameter, type, compilation, callbackMarshallerParameter: null);
    }

    private static string RenderManagedImportParameter(
        ParameterBinding parameter,
        BindingType type,
        BindingCompilation? compilation,
        ParameterBinding? callbackMarshallerParameter)
    {
        if (IsInputHandlePointerArrayParameter(parameter))
        {
            return $"nint* {parameter.ManagedName}";
        }

        if (IsInputPrimitivePointerArrayParameter(parameter))
        {
            return $"{GetManagedImportTypeName(type, compilation)}* {parameter.ManagedName}";
        }

        if (type.IsFixedArray)
        {
            var attribute = compilation is null ? string.Empty : GetManagedFixedArrayInMarshallerAttribute(compilation, type);
            return $"{attribute}{GetManagedWrapperTypeName(type, compilation)} {parameter.ManagedName}";
        }

        if (CanUseManagedHandleParameterImport(parameter, type))
        {
            var modifier = parameter.Direction switch
            {
                BindingParameterDirection.In when parameter.AbiKind != BindingParameterAbiKind.Value => "in ",
                _ => string.Empty
            };
            var attribute = GetManagedParameterAttribute(parameter, type, compilation);
            var marshallerAttribute = compilation is null ? string.Empty : GetManagedHandleInMarshallerAttribute(compilation, type);
            var typeName = $"{GetManagedWrapperTypeName(type, compilation)}?";
            return $"{marshallerAttribute}{attribute}{modifier}{typeName} {parameter.ManagedName}";
        }

        var modifierFallback = parameter.Direction switch
        {
            BindingParameterDirection.In when parameter.AbiKind != BindingParameterAbiKind.Value => "in ",
            BindingParameterDirection.Ref => "ref ",
            BindingParameterDirection.Out => "out ",
            _ => string.Empty
        };
        var attributeFallback = GetManagedParameterAttribute(callbackMarshallerParameter ?? parameter, type, compilation);
        var importTypeName = GetManagedImportTypeName(type, compilation);
        var typeNameFallback = type.IsCallback && parameter.Direction == BindingParameterDirection.In && parameter.AbiKind == BindingParameterAbiKind.Value
            ? $"{GetManagedCallbackParameterTypeName(compilation!, parameter)}?"
            : IsNullableManagedReferenceDefaultParameter(parameter) && IsManagedReferenceTypeName(type.ManagedTypeName)
                ? $"{importTypeName}?"
                : importTypeName;
        return $"{attributeFallback}{modifierFallback}{typeNameFallback} {parameter.ManagedName}";
    }

    private static string RenderManagedParameter(ParameterBinding parameter)
    {
        var modifier = parameter.Direction switch
        {
            BindingParameterDirection.In when parameter.AbiKind != BindingParameterAbiKind.Value => "in ",
            BindingParameterDirection.Ref => "ref ",
            BindingParameterDirection.Out => "out ",
            _ => string.Empty
        };
        var attribute = GetManagedParameterAttribute(parameter.Type);

        return $"{attribute}{modifier}{GetManagedImportTypeName(parameter.Type)} {parameter.ManagedName}";
    }

    private static string RenderManagedPublicParameter(ParameterBinding parameter, BindingCompilation? compilation = null)
    {
        return RenderManagedPublicParameterCore(parameter, forceRequired: false, suppressDefault: false, compilation, renderUnexpressibleDefault: false);
    }

    private static string RenderManagedPublicParameterWithUnexpressibleDefault(ParameterBinding parameter, BindingCompilation? compilation = null)
    {
        return RenderManagedPublicParameterCore(parameter, forceRequired: false, suppressDefault: false, compilation, renderUnexpressibleDefault: true);
    }

    private static string RenderManagedPublicParameterList(IEnumerable<ParameterBinding> parameters, BindingCompilation? compilation = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var parameterArray = parameters as ParameterBinding[] ?? parameters.ToArray();

        var firstUnexpressibleDefault = parameterArray
            .Select((parameter, index) => (parameter, index))
            .FirstOrDefault(static entry => entry.parameter.HasMetadata(MetadataKeys.UnexpressibleDefault));
        var lastRequiredParameterIndex = Array.FindLastIndex(parameterArray, static parameter => !CanRenderManagedPublicDefault(parameter));

        var parts = new List<string>(parameterArray.Length);
        for (var i = 0; i < parameterArray.Length; i++)
        {
            var parameter = parameterArray[i];
            if (firstUnexpressibleDefault.parameter is not null)
            {
                if (i < firstUnexpressibleDefault.index)
                {
                    parts.Add(RenderManagedPublicParameterRequired(parameter, compilation));
                    continue;
                }

                if (parameter.HasMetadata(MetadataKeys.UnexpressibleDefault))
                {
                    parts.Add(i < lastRequiredParameterIndex
                        ? RenderManagedPublicParameterNoDefault(parameter, compilation)
                        : RenderManagedPublicParameterWithUnexpressibleDefault(parameter, compilation));
                    continue;
                }
            }

            if (i < lastRequiredParameterIndex && CanRenderManagedPublicDefault(parameter))
            {
                parts.Add(RenderManagedPublicParameterNoDefault(parameter, compilation));
                continue;
            }

            parts.Add(RenderManagedPublicParameter(parameter, compilation));
        }

        return string.Join(", ", parts);
    }

    private static void WriteManagedUnexpressibleDefaultConvenienceOverloads(
        CodeWriter writer,
        BindingCompilation compilation,
        FunctionBinding function,
        string signaturePrefix,
        string invocationPrefix,
        string invocationTarget,
        string? returnTypeName,
        bool isConstructor)
    {
        foreach (var keepCount in GetUnexpressibleDefaultConvenienceOverloadParameterIndices(function))
        {
            if (isConstructor && ShouldSkipConflictingConstructorConvenienceOverload(compilation, function, keepCount))
            {
                continue;
            }

            var variantParams = function.Parameters.Take(keepCount).ToArray();
            var parameterList = RenderManagedUnexpressibleDefaultOverloadParameterList(function, keepCount, compilation);
            var forwardedArgs = string.Join(
                ", ",
                variantParams.Select(RenderManagedForwardingArgument)
                    .Concat(function.Parameters.Skip(keepCount).Select(p => GetManagedOmittedDefaultArgument(p, compilation))));
            var overloadSignaturePrefix = signaturePrefix;

            if (!isConstructor &&
                string.Equals(invocationTarget, nameof(ToString), StringComparison.Ordinal) &&
                variantParams.Length == 0 &&
                returnTypeName is not null &&
                !overloadSignaturePrefix.Contains(" new ", StringComparison.Ordinal))
            {
                overloadSignaturePrefix = overloadSignaturePrefix.Replace(
                    $"{returnTypeName} {invocationTarget}",
                    $"new {returnTypeName} {invocationTarget}",
                    StringComparison.Ordinal);
            }

            if (isConstructor)
            {
                writer.Write($$"""
                    {{signaturePrefix}}({{parameterList}}) {{invocationPrefix}}({{forwardedArgs}})
                    {
                    }

                    """);
                continue;
            }

            writer.Write($$"""
                {{overloadSignaturePrefix}}({{parameterList}})
                {
                """);

            if (string.Equals(returnTypeName, "void", StringComparison.Ordinal))
            {
                writer.Write($$"""
                    {{invocationTarget}}({{forwardedArgs}});
                    }

                    """);
            }
            else
            {
                writer.Write($$"""
                    return {{invocationTarget}}({{forwardedArgs}});
                    }

                    """);
            }
        }
    }

    private static bool ShouldSkipConflictingConstructorConvenienceOverload(BindingCompilation compilation, FunctionBinding function, int keepCount)
    {
        if (function.Parent is not RecordBinding record)
            return false;

        foreach (var other in record.Members.OfType<FunctionBinding>().Where(static member => member.IsConstructor && !member.HasMetadata(MetadataKeys.IgnoreReason)))
        {
            if (ReferenceEquals(other, function))
                continue;

            if (!ConstructorExposesManagedSignatureWithArity(other, keepCount))
                continue;

            if (!ManagedParameterPrefixSignaturesMatch(compilation, function, other, keepCount))
                continue;

            if (string.CompareOrdinal(function.SymbolId, other.SymbolId) > 0)
                return true;
        }

        return false;
    }

    private static bool ConstructorExposesManagedSignatureWithArity(FunctionBinding constructor, int parameterCount)
    {
        if (constructor.Parameters.Count == parameterCount)
            return true;

        return GetUnexpressibleDefaultConvenienceOverloadParameterIndices(constructor).Contains(parameterCount);
    }

    private static bool ManagedParameterPrefixSignaturesMatch(BindingCompilation compilation, FunctionBinding left, FunctionBinding right, int parameterCount)
    {
        if (left.Parameters.Count < parameterCount || right.Parameters.Count < parameterCount)
            return false;

        for (var i = 0; i < parameterCount; ++i)
        {
            var leftParameter = left.Parameters[i];
            var rightParameter = right.Parameters[i];
            if (leftParameter.Direction != rightParameter.Direction)
                return false;

            var leftTypeName = GetManagedPublicParameterTypeName(leftParameter, compilation);
            var rightTypeName = GetManagedPublicParameterTypeName(rightParameter, compilation);
            if (!string.Equals(leftTypeName, rightTypeName, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string RenderManagedUnexpressibleDefaultOverloadParameterList(
        FunctionBinding function,
        int keepCount,
        BindingCompilation? compilation = null)
    {
        var parameterArray = function.Parameters;
        var keepParameters = parameterArray.Take(keepCount).ToArray();
        if (keepParameters.Length == 0)
        {
            return string.Empty;
        }

        var localDefaultFlags = GetManagedPublicParameterDefaultFlags(keepParameters);
        var parts = new List<string>(keepCount);
        for (var i = 0; i < keepParameters.Length; i++)
        {
            var parameter = keepParameters[i];
            if (parameter.HasMetadata(MetadataKeys.UnexpressibleDefault))
            {
                parts.Add(RenderManagedPublicParameterNoDefault(parameter, compilation));
                continue;
            }

            parts.Add(localDefaultFlags[i]
                ? RenderManagedPublicParameter(parameter, compilation)
                : RenderManagedPublicParameterNoDefault(parameter, compilation));
        }

        return string.Join(", ", parts);
    }

    private static string GetManagedOmittedDefaultArgument(ParameterBinding parameter, BindingCompilation? compilation = null)
    {
        if (parameter.HasMetadata(MetadataKeys.UnexpressibleDefault) || parameter.ManagedDefaultValueExpression == "null")
        {
            return $"default({GetManagedOmittedDefaultTypeName(parameter, compilation)})";
        }

        if (parameter.ManagedDefaultValueExpression is { Length: > 0 } defaultValueExpression)
        {
            return RenderManagedDefaultValueExpression(parameter, defaultValueExpression, compilation);
        }

        return "default";
    }

    private static string GetManagedOmittedDefaultTypeName(ParameterBinding parameter, BindingCompilation? compilation = null)
    {
        var baseTypeName = GetManagedPublicParameterTypeName(parameter, compilation);
        if (IsManagedReferenceDefaultParameter(parameter))
        {
            return $"{baseTypeName}?";
        }

        return parameter.HasMetadata(MetadataKeys.UnexpressibleDefault)
            ? $"{baseTypeName}?"
            : baseTypeName;
    }

    private static string GetManagedPublicParameterTypeName(ParameterBinding parameter, BindingCompilation? compilation = null)
    {
        return parameter.Type.IsCallback && compilation is not null
            ? GetManagedCallbackParameterTypeName(compilation, parameter)
            : GetManagedPublicReturnTypeName(parameter.Type, compilation);
    }

    private static string RenderManagedPublicParameterRequired(ParameterBinding parameter, BindingCompilation? compilation = null)
    {
        return RenderManagedPublicParameterCore(parameter, forceRequired: true, suppressDefault: true, compilation, renderUnexpressibleDefault: false);
    }

    private static string RenderManagedPublicParameterNoDefault(ParameterBinding parameter, BindingCompilation? compilation = null)
    {
        return RenderManagedPublicParameterCore(parameter, forceRequired: false, suppressDefault: true, compilation, renderUnexpressibleDefault: false);
    }

    private static bool CanRenderManagedPublicDefault(ParameterBinding parameter)
    {
        return !parameter.HasMetadata(MetadataKeys.UnexpressibleDefault) &&
            parameter.Direction == BindingParameterDirection.In &&
            parameter.AbiKind == BindingParameterAbiKind.Value &&
            parameter.ManagedDefaultValueExpression is { Length: > 0 };
    }

    private static string RenderManagedPublicParameterCore(
        ParameterBinding parameter,
        bool forceRequired,
        bool suppressDefault,
        BindingCompilation? compilation = null,
        bool renderUnexpressibleDefault = false)
    {
        if (IsInputHandlePointerArrayParameter(parameter))
        {
            return $"global::System.ReadOnlySpan<{GetManagedPublicReturnTypeName(parameter.Type, compilation)}?> {parameter.ManagedName}";
        }

        if (IsInputPrimitivePointerArrayParameter(parameter))
        {
            return $"global::System.ReadOnlySpan<{GetManagedPublicReturnTypeName(parameter.Type, compilation)}> {parameter.ManagedName}";
        }

        var modifier = parameter.Direction switch
        {
            BindingParameterDirection.In when parameter.AbiKind != BindingParameterAbiKind.Value => "in ",
            BindingParameterDirection.Ref => "ref ",
            BindingParameterDirection.Out => "out ",
            _ => string.Empty
        };

        var isNullableManagedReferenceDefault = !forceRequired &&
            IsNullableManagedReferenceDefaultParameter(parameter);

        var typeName = (!forceRequired && parameter.HasMetadata(MetadataKeys.UnexpressibleDefault)) || isNullableManagedReferenceDefault
            ? $"{GetManagedPublicParameterTypeName(parameter, compilation)}?"
            : GetManagedPublicParameterTypeName(parameter, compilation);

        var signature = $"{modifier}{typeName} {parameter.ManagedName}";

        if (!forceRequired && !suppressDefault)
        {
            if (parameter.HasMetadata(MetadataKeys.UnexpressibleDefault))
            {
                if (renderUnexpressibleDefault)
                {
                    signature += " = default";
                }
            }
            else if (isNullableManagedReferenceDefault)
            {
                signature += " = default";
            }
            else if (parameter.Direction == BindingParameterDirection.In &&
                parameter.AbiKind == BindingParameterAbiKind.Value &&
                parameter.ManagedDefaultValueExpression is { Length: > 0 } defaultValueExpression)
            {
                var fallbackDefault = defaultValueExpression == "null"
                    ? "default"
                    : RenderManagedDefaultValueExpression(parameter, defaultValueExpression, compilation);
                signature += $" = {fallbackDefault}";
            }
        }
        return signature;
    }

    private static string RenderManagedDefaultValueExpression(ParameterBinding parameter, string defaultValueExpression, BindingCompilation? compilation)
    {
        if (defaultValueExpression.Length >= 3 &&
            defaultValueExpression[0] == '\'' &&
            defaultValueExpression[^1] == '\'')
        {
            return parameter.Type.PrimitiveKind switch
            {
                BindingPrimitiveKind.Int8 => $"(sbyte){defaultValueExpression}",
                BindingPrimitiveKind.UInt8 => $"(byte){defaultValueExpression}",
                _ => defaultValueExpression
            };
        }

        if (compilation is not null && parameter.Type.Kind == BindingTypeKind.Enum)
        {
            var separator = defaultValueExpression.IndexOf('.');
            if (separator > 0)
            {
                var enumTypeName = defaultValueExpression[..separator];
                var enumMemberName = defaultValueExpression[(separator + 1)..];
                var qualifiedEnumTypeName = GetQualifiedEnumTypeName(compilation, parameter.Type);
                if (string.Equals(enumTypeName, parameter.Type.UnaliasedManagedTypeName, StringComparison.Ordinal) ||
                    string.Equals(enumTypeName, NormalizeEnumManagedTypeName(parameter.Type.UnaliasedManagedTypeName), StringComparison.Ordinal))
                {
                    return $"{qualifiedEnumTypeName}.{enumMemberName}";
                }
            }
        }

        return defaultValueExpression;
    }

    private static string RenderNativeParameter(ParameterBinding parameter)
    {
        return $"arg_t<{GetNativeParameterMarshallerCppTypeName(parameter)}> {parameter.NativeName}";
    }

    private static string GetNativeParameterMarshallerCppTypeName(ParameterBinding parameter)
    {
        return parameter.Type.Marshaller.IsRefStruct
            ? parameter.Type.CppTypeName
            : parameter.CppTypeName;
    }

    private static string BuildMarshallerInvokerTemplateArguments(string functionTypeName, IEnumerable<string> cppParameterTypes)
    {
        var tupleTypes = string.Join(", ", cppParameterTypes);
        return $"{functionTypeName}, std::tuple<{tupleTypes}>";
    }

    private static string BuildNativeFunctionSignature(FunctionBinding function)
    {
        var returnTypeName = function.CppReturnTypeName;
        var parameterTypes = string.Join(", ", function.Parameters.Select(static p => p.CppTypeName));
        return $"{returnTypeName}({parameterTypes})";
    }

    private static string BuildNativeMethodSignature(FunctionBinding method)
    {
        var returnTypeName = method.CppReturnTypeName;
        var parameterTypes = string.Join(", ", method.Parameters.Select(static p => p.CppTypeName));
        var constQualifier = method.IsConstMethod ? " const" : string.Empty;
        return $"{returnTypeName}({method.OwningRecordCppTypeName}::*)({parameterTypes}){constQualifier}";
    }

    private static string GetNativeFieldCppType(BindingCompilation compilation, FieldBinding field)
    {
        return field.CppTypeName;
    }

    private static string GetNativeFixedArrayElementCppType(BindingCompilation compilation, BindingType type)
    {
        var elementType = type.IsFixedArray ? type.FixedArrayElementType ?? type : type;
        return elementType.Kind switch
        {
            BindingTypeKind.Enum => compilation.FindEnumBySymbolId(elementType.EnumId)?.CppQualifiedName ?? elementType.CppTypeName,
            BindingTypeKind.Handle => GetNativeFixedArrayHandleElementCppType(compilation, elementType),
            _ => elementType.CppTypeName
        };
    }

    private static string GetNativeFixedArrayHandleElementCppType(BindingCompilation compilation, BindingType type)
    {
        var record = compilation.FindRecordBySymbolId(type.RecordId);
        if (record is null)
        {
            return type.CppTypeName;
        }

        var pointerSuffix = string.Empty;
        var cppTypeName = type.CppTypeName.TrimEnd();
        while (cppTypeName.EndsWith('*'))
        {
            pointerSuffix += "*";
            cppTypeName = cppTypeName[..^1].TrimEnd();
        }

        var qualifiedTypeName = record.GenericParameters.Count > 0 && type.GenericArguments.Count > 0
            ? record.FormatCppTypeName(type.GenericArguments)
            : record.CppQualifiedName;
        return qualifiedTypeName + pointerSuffix;
    }


    private static bool RequiresHandleWrapping(BindingType type)
    {
        return type.IsPointerType;
    }

    private static string RenderManagedInvocationArgument(BindingCompilation? compilation, ParameterBinding parameter)
    {
        if (parameter.Type.IsFixedArray)
        {
            return parameter.ManagedName;
        }

        var requiresWrapping = RequiresHandleWrapping(parameter.Type);

        if (requiresWrapping && CanUseManagedHandleParameterImport(parameter, parameter.Type))
        {
            return RenderManagedSafeHandleInvocationArgument(parameter);
        }

        var valueExpression = requiresWrapping
            ? $"global::SharpCpp.Support.NativeObject.GetHandle({parameter.ManagedName})"
            : parameter.ManagedName;

        if (!requiresWrapping)
        {
            var importExpression = BuildManagedPublicToImportExpression(compilation, parameter.Type, valueExpression);
            return parameter.Direction switch
            {
                BindingParameterDirection.Ref => $"ref {valueExpression}",
                BindingParameterDirection.Out => $"out {valueExpression}",
                _ => importExpression
            };
        }

        return parameter.Direction switch
        {
            BindingParameterDirection.In when parameter.AbiKind != BindingParameterAbiKind.Value => $"in {valueExpression}",
            BindingParameterDirection.Ref => $"ref {valueExpression}",
            BindingParameterDirection.Out => $"out {valueExpression}",
            _ => valueExpression
        };
    }

    private static string RenderManagedSafeHandleInvocationArgument(ParameterBinding parameter)
    {
        return parameter.Direction switch
        {
            BindingParameterDirection.In when parameter.AbiKind != BindingParameterAbiKind.Value => $"in {parameter.ManagedName}",
            BindingParameterDirection.Ref => $"ref {parameter.ManagedName}",
            BindingParameterDirection.Out => $"out {parameter.ManagedName}",
            _ => parameter.ManagedName
        };
    }

    private static ManagedInvocationPlan BuildManagedInvocationPlan(BindingCompilation compilation, IEnumerable<ParameterBinding> parameters)
    {
        var arguments = new List<string>();
        var preCallStatements = new List<string>();
        var postCallStatements = new List<string>();

        foreach (var parameter in parameters)
        {
            if (parameter.Type.IsFixedArray)
            {
                var expectedLength = parameter.Type.FixedArrayLength ?? 0;
                preCallStatements.Add($$"""
                    global::System.ArgumentNullException.ThrowIfNull({{parameter.ManagedName}});
                    if ({{parameter.ManagedName}}.Length != {{expectedLength}})
                    {
                        throw new global::System.ArgumentException("Expected array length {{expectedLength}}.", nameof({{parameter.ManagedName}}));
                    }
                    """);
                arguments.Add(RenderManagedInvocationArgument(compilation, parameter));
                postCallStatements.Add($"global::System.GC.KeepAlive({parameter.ManagedName});");
                continue;
            }

            var requiresWrapping = RequiresHandleWrapping(parameter.Type);

            if (requiresWrapping && parameter.Direction is BindingParameterDirection.Ref or BindingParameterDirection.Out)
            {
                var abiVariableName = $"__abi_{parameter.ManagedName}";

                if (parameter.Direction == BindingParameterDirection.Ref)
                {
                    preCallStatements.Add($$"""
                        var {{abiVariableName}} = global::SharpCpp.Support.NativeObject.GetHandle({{parameter.ManagedName}});
                        """);
                    arguments.Add($"ref {abiVariableName}");
                }
                else
                {
                    preCallStatements.Add($$"""
                        nint {{abiVariableName}};
                        """);
                    arguments.Add($"out {abiVariableName}");
                }

                postCallStatements.Add($$"""
                    {{parameter.ManagedName}} = {{BuildManagedHandleWrap(compilation, parameter.Type, abiVariableName, ownsHandle: false)}};
                    """);
                continue;
            }

            if (requiresWrapping)
            {
                postCallStatements.Add($"global::System.GC.KeepAlive({parameter.ManagedName});");
            }

            arguments.Add(RenderManagedInvocationArgument(compilation, parameter));
        }

        return new ManagedInvocationPlan(arguments, preCallStatements, postCallStatements);
    }

    private sealed record ManagedInvocationPlan(
        IReadOnlyList<string> Arguments,
        IReadOnlyList<string> PreCallStatements,
        IReadOnlyList<string> PostCallStatements);

    private static string BuildManagedPublicToImportExpression(BindingCompilation? compilation, BindingType type, string expression)
    {
        if (type.Kind == BindingTypeKind.Handle || type.IsFixedArray)
        {
            return expression;
        }

        var importTypeName = GetManagedImportTypeName(type, compilation);
        var publicTypeName = GetManagedPublicReturnTypeName(type, compilation);
        return string.Equals(importTypeName, publicTypeName, StringComparison.Ordinal)
            ? expression
            : $"({importTypeName}){expression}";
    }

    private static string BuildManagedImportToPublicExpression(BindingCompilation? compilation, BindingType type, string expression)
    {
        if (type.Kind == BindingTypeKind.Handle || type.IsFixedArray)
        {
            return expression;
        }

        var importTypeName = GetManagedImportTypeName(type, compilation);
        var publicTypeName = GetManagedPublicReturnTypeName(type, compilation);
        return string.Equals(importTypeName, publicTypeName, StringComparison.Ordinal)
            ? expression
            : $"({publicTypeName})({expression})";
    }

    private sealed record AccessorProperty(string PropertyName, FunctionBinding? Getter, FunctionBinding? Setter);

    private static string GetManagedInstanceMethodModifier(BindingCompilation compilation, RecordBinding record, FunctionBinding function)
    {
        var hasManagedBase = !string.IsNullOrWhiteSpace(record.ManagedBaseTypeName);
        var hidingObjectToString = string.Equals(function.ManagedName, nameof(ToString), StringComparison.Ordinal) &&
            function.Parameters.Count == 0 &&
            !function.IsOverride;
        var hidingModifier = hidingObjectToString ? "new " : string.Empty;
        var primaryBaseVirtualMatch = hasManagedBase ? FindPrimaryBaseVirtualMatch(compilation, record, function) : null;

        if (IsNativeInterfaceWrapper(record) && IsManagedAbstractMethod(function))
        {
            return hidingModifier + "virtual ";
        }

        if (IsManagedAbstractMethod(function) && primaryBaseVirtualMatch is null)
        {
            return hidingModifier + "abstract ";
        }

        if (function.IsVirtual)
        {
            string modifier;
            if (primaryBaseVirtualMatch is null)
            {
                modifier = ShouldUseNewModifier(compilation, record, function.ManagedName)
                    ? "new virtual "
                    : (function.IsOverride && !string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) &&
                        record.ManagedBaseTypeName.Contains('.', StringComparison.Ordinal) ? "override " : "virtual ");
            }
            else
            {
                modifier = "override ";
            }

            if (function.IsFinal && modifier == "override ")
            {
                modifier += "sealed ";
            }

            return hidingModifier + modifier;
        }

        if (function.IsOverride && !string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) &&
            record.ManagedBaseTypeName.Contains('.', StringComparison.Ordinal))
        {
            return "override ";
        }

        return hidingModifier.Length > 0 || ShouldUseNewModifier(compilation, record, function.ManagedName) ? "new " : string.Empty;
    }

    private static string GetManagedVisibility(BindingAccessModifier accessModifier)
    {
        return accessModifier switch
        {
            BindingAccessModifier.Protected => "protected",
            BindingAccessModifier.Private => "private",
            _ => "public"
        };
    }

    private static string GetManagedVisibilityNormalized(BindingCompilation compilation, RecordBinding record, FunctionBinding function)
    {
        if (!function.IsVirtual)
        {
            return GetManagedVisibility(function.AccessModifier);
        }

        if (function.AccessModifier == BindingAccessModifier.Protected && FindPrimaryBaseVirtualMatch(compilation, record, function) is null)
        {
            return "protected";
        }

        var mostRestrictiveBaseAccess = FindMostRestrictiveBaseAccess(compilation, record, function);
        if (mostRestrictiveBaseAccess == BindingAccessModifier.Protected)
        {
            return "protected";
        }

        return "public";
    }

    private static BindingAccessModifier FindMostRestrictiveBaseAccess(BindingCompilation compilation, RecordBinding record, FunctionBinding function)
    {
        var foundBaseMatch = false;
        var mostRestrictive = BindingAccessModifier.Public;

        foreach (var @base in record.Bases)
        {
            var baseRecord = compilation.Records.FirstOrDefault(candidate => string.Equals(candidate.SymbolId, @base.RecordId, StringComparison.Ordinal));
            if (baseRecord is null)
            {
                continue;
            }

            var match = baseRecord.Members
                .OfType<FunctionBinding>()
                .FirstOrDefault(bm => !bm.IsIgnored() && bm.IsVirtual && bm.ManagedName == function.ManagedName && bm.Parameters.Count == function.Parameters.Count);

            if (match is not null)
            {
                foundBaseMatch = true;
                if (match.AccessModifier == BindingAccessModifier.Protected)
                {
                    mostRestrictive = BindingAccessModifier.Protected;
                }
                else
                {
                    var inheritedRestrictive = FindMostRestrictiveBaseAccess(compilation, baseRecord, match);
                    if (inheritedRestrictive == BindingAccessModifier.Protected)
                    {
                        mostRestrictive = BindingAccessModifier.Protected;
                    }
                }
            }
        }

        return foundBaseMatch ? mostRestrictive : function.AccessModifier;
    }

    private static BindingAccessModifier GetAccessorPropertyAccessModifier(AccessorProperty property)
    {
        if (property.Getter?.AccessModifier == BindingAccessModifier.Protected ||
            property.Setter?.AccessModifier == BindingAccessModifier.Protected)
        {
            return BindingAccessModifier.Protected;
        }

        return BindingAccessModifier.Public;
    }

    private static FunctionBinding? FindPrimaryBaseVirtualMatch(BindingCompilation compilation, RecordBinding record, FunctionBinding function)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { record.SymbolId };
        for (var current = record.GetPrimaryBaseRecord(compilation); current is not null; current = current.GetPrimaryBaseRecord(compilation))
        {
            if (!visited.Add(current.SymbolId))
            {
                break;
            }

            var match = current.Members
                .OfType<FunctionBinding>()
                .FirstOrDefault(candidate =>
                    !candidate.IsIgnored() &&
                    candidate.IsVirtual &&
                    RecordBindingExtensions.SignaturesMatch(candidate, function) &&
                    string.Equals(candidate.ReturnType.ManagedTypeName, function.ReturnType.ManagedTypeName, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static string RenderManagedCallbackParameter(BindingCompilation compilation, ParameterBinding parameter)
    {
        ValidateVirtualCallbackSupport(parameter);
        return $"{GetManagedParameterAttribute(parameter.Type, compilation)}{GetManagedImportTypeName(parameter.Type, compilation)} {parameter.ManagedName}";
    }

    private static string BuildManagedCallbackInvocationArgument(BindingCompilation compilation, ParameterBinding parameter)
    {
        ValidateVirtualCallbackSupport(parameter);

        return parameter.Type.IsPointerType
            ? BuildManagedHandleWrap(compilation, parameter.Type, parameter.ManagedName, ownsHandle: false)
            : parameter.ManagedName;
    }

    private static string FormatReflectionParameterTypes(BindingCompilation compilation, FunctionBinding method)
    {
        return method.Parameters.Count == 0
            ? "System.Array.Empty<global::System.Type>()"
            : $"new global::System.Type[] {{ {string.Join(", ", method.Parameters.Select(parameter => $"typeof({GetManagedWrapperTypeName(parameter.Type, compilation)})"))} }}";
    }

    private static string GetVirtualCallbackDelegateName(FunctionBinding method)
    {
        return $"__{GetVirtualCallbackTokenPrefix(method)}_Delegate";
    }

    private static string GetVirtualCallbackFieldName(FunctionBinding method)
    {
        return $"s_{GetVirtualCallbackTokenPrefix(method)}_Callback";
    }

    private static string GetVirtualCallbackPointerFieldName(FunctionBinding method)
    {
        return $"s_{GetVirtualCallbackTokenPrefix(method)}_Pointer";
    }

    private static string GetVirtualCallbackPointerAccessorName(FunctionBinding method)
    {
        return $"__Get{GetVirtualCallbackTokenPrefix(method)}Pointer";
    }

    private static string GetVirtualCallbackDispatchName(FunctionBinding method)
    {
        return $"__Dispatch{GetVirtualCallbackTokenPrefix(method)}";
    }

    private static string GetVirtualCallbackTokenPrefix(FunctionBinding method)
    {
        var ownerName = method.FindOwningRecord()?.ManagedName ?? "Global";
        return $"{SanitizeManagedToken(ownerName)}_{SanitizeManagedToken(method.ImportMethodName)}";
    }

    private static string SanitizeManagedToken(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        var token = builder.ToString().Trim('_');
        return token.Length == 0 ? "Callback" : token;
    }

    private static void ValidateVirtualCallbackSupport(FunctionBinding method)
    {
        foreach (var parameter in method.Parameters)
        {
            ValidateVirtualCallbackSupport(parameter);
        }

        if (method.ReturnType.Kind == BindingTypeKind.Handle && ReturnsOwnedHandle(method))
        {
            throw new NotSupportedException($"Director callback '{method.ManagedName}' does not support owned-handle returns yet.");
        }
    }

    private static void ValidateVirtualCallbackSupport(ParameterBinding parameter)
    {
        if (parameter.Direction is not BindingParameterDirection.In || parameter.AbiKind is not BindingParameterAbiKind.Value)
        {
            throw new NotSupportedException($"Director callback parameter '{parameter.ManagedName}' must be an in-value parameter.");
        }
    }

    private static string BuildManagedHandleWrap(BindingCompilation compilation, BindingType type, string handleExpression, bool ownsHandle)
    {
        var record = ResolveRecord(compilation, type);
        if (record is not null)
        {
            return $"{GetManagedWrapperTypeName(type, compilation)}.CreateManagedWrapper({handleExpression}, ownsHandle: {ownsHandle.ToString().ToLowerInvariant()})";
        }

        var wrapperTypeName = GetManagedWrapperTypeName(type, compilation);
        return ownsHandle
            ? $"new {wrapperTypeName}({handleExpression}, true)"
            : $"new {wrapperTypeName}({handleExpression})";
    }

    private static RecordBinding? ResolveRecord(BindingCompilation compilation, BindingType type)
    {
        return string.IsNullOrWhiteSpace(type.RecordId)
            ? null
            : compilation.Records.FirstOrDefault(candidate => string.Equals(candidate.SymbolId, type.RecordId, StringComparison.Ordinal));
    }

    private static string RenderManagedForwardingArgument(ParameterBinding parameter)
    {
        return parameter.Direction switch
        {
            BindingParameterDirection.In when parameter.AbiKind != BindingParameterAbiKind.Value => $"in {parameter.ManagedName}",
            BindingParameterDirection.Ref => $"ref {parameter.ManagedName}",
            BindingParameterDirection.Out => $"out {parameter.ManagedName}",
            _ => parameter.ManagedName
        };
    }

    private static string RenderManagedStaticTypeIdImportDeclaration(BindingCompilation compilation, RecordBinding record)
    {
        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_type_id", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static partial nuint GetStaticTypeId();
            """;
    }

    private static string RenderManagedNativeObjectMetadataImportDeclaration(BindingCompilation compilation, RecordBinding record)
    {
        return $$"""
            [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_metadata", StringMarshalling = StringMarshalling.Utf8)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static partial void GetNativeObjectMetadata(nint self, out nint handle, out nuint dynamicTypeId);
            """;
    }

    private static string RenderNativeObjectRegistryInitializer(BindingCompilation compilation, IReadOnlyList<RecordBinding> records)
    {
        var writer = new CodeWriter();
        writer.Write(
            """
            internal static class __NativeObjectRegistryInitializer
            {
            [global::System.Runtime.CompilerServices.ModuleInitializer]
            internal static void RegisterAll()
            {
            """);

        foreach (var record in records.OrderBy(static record => record.ManagedName, StringComparer.Ordinal))
        {
            var qualifiedRecordTypeName = FormatQualifiedRecordTypeName(compilation, record.HandleType, publicSurface: false);
            writer.Write($$"""
                global::SharpCpp.Support.NativeObjectRegistry.RegisterType<{{qualifiedRecordTypeName}}>();
                """);
        }

        writer.Write(
            """
            }
            }
            """);
        return writer.ToString();
    }


    private static string GetManagedImportMethodName(FunctionBinding function)
    {
        return function.ImportMethodName;
    }

    private static string SanitizeNativeEntrypointName(string value)
    {
        return value.Replace("::", "_", StringComparison.Ordinal)
                    .Replace("<", "_", StringComparison.Ordinal)
                    .Replace(">", "_", StringComparison.Ordinal);
    }

    private static string GetInstanceCacheFactoryExpression(RecordBinding record)
    {
        return string.IsNullOrWhiteSpace(record.ManagedInstanceCacheFactoryExpression)
            ? "new global::SharpCpp.Support.DefaultNativeInstanceCache()"
            : record.ManagedInstanceCacheFactoryExpression!;
    }

    private static string GetInstanceCacheFieldTypeName(RecordBinding record)
    {
        if (string.IsNullOrWhiteSpace(record.ManagedInstanceCacheFactoryExpression))
        {
            return "global::SharpCpp.Support.DefaultNativeInstanceCache";
        }

        return string.IsNullOrWhiteSpace(record.ManagedInstanceCacheTypeName)
            ? "global::SharpCpp.Support.INativeInstanceCache"
            : record.ManagedInstanceCacheTypeName!;
    }

    private static string GetManagedParameterAttribute(ParameterBinding parameter, BindingCompilation? compilation = null)
    {
        return GetManagedParameterAttribute(parameter, parameter.Type, compilation);
    }

    private static string GetManagedParameterAttribute(ParameterBinding parameter, BindingType type, BindingCompilation? compilation = null)
    {
        if (type.IsCallback && compilation is not null && type.ManagedMarshaller is null)
        {
            return $"[MarshalUsing(typeof({GetManagedCallbackMarshallerTypeName(compilation, parameter)}))] ";
        }

        return GetManagedParameterAttribute(type, compilation);
    }

    private static string GetManagedParameterAttribute(BindingType type, BindingCompilation? compilation = null)
    {
        if (type.ManagedMarshaller is not null)
        {
            return $"[MarshalUsing(typeof({type.ManagedMarshaller}))] ";
        }

        if (type.IsCallback && compilation is not null)
        {
            return $"[MarshalUsing(typeof(global::{compilation.ManagedNamespace}.{compilation.ManagedClassName}.{GetManagedCallbackMarshallerDeclarationName(compilation, type)}))] ";
        }

        return type.PrimitiveKind == BindingPrimitiveKind.Boolean
            ? "[MarshalAs(UnmanagedType.I1)] "
            : string.Empty;
    }

    private static string GetManagedReturnAttribute(BindingType type)
    {
        if (type.ManagedMarshaller is not null)
        {
            return $"[return: MarshalUsing(typeof({type.ManagedMarshaller}))]";
        }

        return type.PrimitiveKind == BindingPrimitiveKind.Boolean
            ? "[return: MarshalAs(UnmanagedType.I1)]"
            : string.Empty;
    }

    private static string GetManagedEnumUnderlyingTypeName(EnumBinding @enum)
    {
        return GetManagedPrimitiveTypeName(@enum.UnderlyingPrimitiveKind);
    }

    private static string GetManagedPrimitiveTypeName(BindingPrimitiveKind kind) => kind switch
    {
        BindingPrimitiveKind.Int32 => "int",
        BindingPrimitiveKind.UInt32 => "uint",
        BindingPrimitiveKind.UInt16 => "ushort",
        BindingPrimitiveKind.Int16 => "short",
        BindingPrimitiveKind.UInt64 => "ulong",
        BindingPrimitiveKind.Int64 => "long",
        _ => "int"
    };

    private static string NormalizeEnumManagedTypeName(string managedTypeName)
    {
        var normalized = managedTypeName
            .Replace("Const ", string.Empty, StringComparison.Ordinal)
            .Replace("const ", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (!normalized.Contains(' ', StringComparison.Ordinal) && !normalized.Contains('_', StringComparison.Ordinal))
        {
            return normalized;
        }

        return string.Concat(
            normalized
                .Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries)
                .Select(static part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static string RenderIgnoredStub(BindingNode node)
    {
        var reason = node.TryGetMetadata(MetadataKeys.IgnoreReason, out string? metadataReason)
            ? metadataReason!
            : "excluded";
        var declarationText = node.TryGetMetadata(MetadataKeys.DeclarationDisplayText, out string? metadataDeclaration)
            ? metadataDeclaration
            : string.Empty;

        return node switch
        {
            RecordBinding record => RenderIgnoredDeclarationStub(declarationText, $"{record.NativeName};", reason),
            EnumBinding @enum => RenderIgnoredDeclarationStub(declarationText, $"enum {@enum.NativeName};", reason),
            FunctionBinding function => RenderIgnoredFunctionStub(function, declarationText, reason),
            FieldBinding field => RenderIgnoredDeclarationStub(declarationText, $"{field.Type.CppTypeName} {field.NativeName};", reason),
            _ => $"// {node.Kind}; // excluded: {reason}"
        };
    }

    private static string RenderIgnoredDeclarationStub(string? declarationText, string fallback, string reason)
    {
        var declaration = string.IsNullOrWhiteSpace(declarationText) ? fallback : declarationText;
        return $"// {declaration} // excluded: {reason}";
    }

    private static string RenderIgnoredFunctionStub(FunctionBinding function, string? declarationText, string reason)
    {
        if (!string.IsNullOrWhiteSpace(declarationText))
        {
            return $"// {declarationText} // excluded: {reason}";
        }

        var parameterList = string.Join(", ", function.Parameters.Select(RenderIgnoredNativeParameter));
        var returnTypeName = function.CppReturnTypeName ?? function.ReturnType.CppTypeName;

        if (function.IsConstructor)
        {
            var constructorName = function.CppQualifiedName[(function.CppQualifiedName.LastIndexOf("::", StringComparison.Ordinal) + 2)..];
            return $"// {constructorName}({parameterList}); // excluded: {reason}";
        }

        var staticModifier = function.IsStatic || function.Parent is null ? "static " : string.Empty;
        var qualifier = function.Parent is RecordBinding record ? $"{record.CppQualifiedName}::" : string.Empty;
        return $"// {staticModifier}{returnTypeName} {qualifier}{function.NativeName}({parameterList}); // excluded: {reason}";
    }

    private static bool CanEmitClone(RecordBinding record, bool isEffectivelyAbstract, bool canEmitDeleteThunk)
    {
        ArgumentNullException.ThrowIfNull(record);

        return record.HasCopyConstructor &&
            !isEffectivelyAbstract &&
            (!record.EmitDefaultNativeReleaseOverride || canEmitDeleteThunk);
    }

    private static bool CanEmitDeleteThunk(RecordBinding record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return !record.GetMetadata<bool>(MetadataKeys.NonPublicDestructor) &&
            !record.GetMetadata<bool>(MetadataKeys.OpaqueNativeHandle) &&
            !IsNativeInterfaceWrapper(record);
    }

    private static bool CanCreateManagedWrapperFactory(BindingCompilation compilation, RecordBinding record)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(record);

        return !IsManagedWrapperAbstract(compilation, record, record.GetEffectiveVirtualMethodSlots(compilation));
    }

    private static bool IsManagedWrapperAbstract(
        BindingCompilation compilation,
        RecordBinding record,
        IReadOnlyCollection<VirtualMethodSlotBinding> virtualSlots)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(virtualSlots);

        if (IsNativeInterfaceWrapper(record))
        {
            return false;
        }

        return record.IsAbstract ||
            virtualSlots.Any(static slot => IsManagedAbstractMethod(slot.Method)) ||
            HasUnimplementedManagedAbstractBaseMethods(compilation, record);
    }

    private static bool IsNativeInterfaceWrapper(RecordBinding record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return record.GetMetadata<bool>(MetadataKeys.NativeInterfaceWrapper);
    }

    private static bool TryGetNativeInterfaceQuery(RecordBinding record, out NativeInterfaceQueryAnnotation query)
    {
        return record.TryGetMetadata(MetadataKeys.NativeInterfaceQuery, out query!);
    }

    private static string RenderNativeInterfaceQuery(RecordBinding record, NativeInterfaceQueryAnnotation query)
    {
        return $$"""
            SHARPCPP_EXPORT ret_t<{{record.CppQualifiedName}}*> sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_query_interface(arg_t<{{query.SourceRecordCppQualifiedName}}*> self)
            {
            if (self == nullptr)
            {
            return nullptr;
            }

            {{record.CppQualifiedName}}* result = nullptr;
            self->QueryInterface({{query.InterfaceIdExpression}}, reinterpret_cast<{{query.SourceRecordCppQualifiedName}}**>(&result));
            return result;
            }
            """;
    }

    private static string RenderNativeInterfaceRelease(RecordBinding record, string releaseMethod)
    {
        return $$"""
            SHARPCPP_EXPORT ret_t<void> sharpcpp_e2e_{{SanitizeNativeEntrypointName(record.CppQualifiedName)}}_release_interface(arg_t<{{record.CppQualifiedName}}*> self)
            {
            if (self != nullptr)
            {
            self->{{releaseMethod}}();
            }
            }
            """;
    }

    private static bool IsManagedAbstractMethod(FunctionBinding method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return method.IsPureVirtual && method.AccessModifier == BindingAccessModifier.Public;
    }

    private static bool ShouldRenderPureVirtualManagedStub(FunctionBinding method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return method.IsPureVirtual && method.AccessModifier != BindingAccessModifier.Public;
    }

    private static bool HasUnimplementedManagedAbstractBaseMethods(BindingCompilation compilation, RecordBinding record)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(record);

        var chain = new List<RecordBinding>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { record.SymbolId };
        for (var current = record.GetPrimaryBaseRecord(compilation); current is not null; current = current.GetPrimaryBaseRecord(compilation))
        {
            if (!visited.Add(current.SymbolId))
            {
                break;
            }

            chain.Insert(0, current);
        }

        chain.Add(record);

        var concreteImplementations = chain
            .SelectMany(static candidate => candidate.Members.OfType<FunctionBinding>())
            .Where(static method => !method.IsIgnored() && !method.IsPureVirtual)
            .ToArray();

        foreach (var baseRecord in chain.Take(chain.Count - 1))
        {
            foreach (var abstractMethod in baseRecord.Members.OfType<FunctionBinding>().Where(static method => !method.IsIgnored() && IsManagedAbstractMethod(method)))
            {
                if (!concreteImplementations.Any(candidate => ManagedSignaturesMatch(candidate, abstractMethod)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ManagedSignaturesMatch(FunctionBinding candidate, FunctionBinding abstractMethod)
    {
        if (!string.Equals(candidate.ManagedName, abstractMethod.ManagedName, StringComparison.Ordinal) ||
            candidate.Parameters.Count != abstractMethod.Parameters.Count)
        {
            return false;
        }

        for (var i = 0; i < candidate.Parameters.Count; i++)
        {
            var left = candidate.Parameters[i].Type;
            var right = abstractMethod.Parameters[i].Type;
            if (!string.Equals(left.ManagedTypeName, right.ManagedTypeName, StringComparison.Ordinal) ||
                left.Kind != right.Kind)
            {
                return false;
            }
        }

        return true;
    }

    private static bool InheritsCloneInterface(BindingCompilation compilation, RecordBinding record)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { record.SymbolId };
        for (var current = record.GetPrimaryBaseRecord(compilation); current is not null; current = current.GetPrimaryBaseRecord(compilation))
        {
            if (!visited.Add(current.SymbolId))
            {
                break;
            }

            if (current.HasCopyConstructor)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldBorrowFieldStorage(BindingCompilation compilation, FieldBinding field)
    {
        if (field.Type.Kind != BindingTypeKind.Handle || field.IsPointer)
        {
            return false;
        }

        return IsMarshallerBackedStorageWrapper(compilation, field.Type);
    }

    private static bool IsMarshallerBackedStorageWrapper(BindingCompilation compilation, BindingType type)
    {
        var record = ResolveRecord(compilation, type);
        return record is not null &&
            record.Members.OfType<FunctionBinding>().Any(static method => method.HasMetadata(MetadataKeys.MarshallerExposedMethod));
    }

    private static bool IsNativeFixedArrayType(BindingCompilation compilation, BindingType type)
    {
        return type.IsFixedArray &&
            ResolveRecord(compilation, type)?.GetMetadata<bool>(MetadataKeys.NativeArrayRecord) == true;
    }

    private static string RenderNativeMarshallerExposedInvocation(FunctionBinding method, string marshallerCppType, string abiArguments)
    {
        var signatureParameters = string.Join(", ", new[] { $"{method.OwningRecordCppTypeName}*" }.Concat(method.Parameters.Select(static parameter => parameter.CppTypeName)));
        var lambdaParameters = string.Join(", ", new[] { $"{method.OwningRecordCppTypeName}* __self" }
            .Concat(method.Parameters.Select((parameter, index) => $"{parameter.CppTypeName} __arg{index}")));
        var invokeArguments = string.Join(", ", new[] { "*__self" }
            .Concat(method.Parameters.Select((_, index) => $"__arg{index}")));
        var callArguments = string.IsNullOrWhiteSpace(abiArguments) ? "self" : $"self, {abiArguments}";
        var returnStatement = method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void
            ? $"Marshaller<{marshallerCppType}>::{method.NativeName}({invokeArguments});"
            : $"return Marshaller<{marshallerCppType}>::{method.NativeName}({invokeArguments});";
        var marshallerTemplateArguments = BuildMarshallerInvokerTemplateArguments(
            "FT",
            new[] { $"{method.OwningRecordCppTypeName}*" }.Concat(method.Parameters.Select(static parameter => parameter.CppTypeName)));
        var dispatch = method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void
            ? $"MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>(__invoke, {callArguments});"
            : $"return MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>(__invoke, {callArguments});";

        return $$"""
            using FT = {{method.CppReturnTypeName}}({{signatureParameters}});
            auto __invoke = []({{lambdaParameters}}) -> {{method.CppReturnTypeName}}
            {
            {{returnStatement}}
            };
            {{dispatch}}
            """;
    }

    private static string RenderIgnoredNativeParameter(ParameterBinding parameter)
        => $"{parameter.CppTypeName} {parameter.NativeName}";

    private static void WriteDocumentationComment(CodeWriter writer, BindingNode node)
    {
        if (!node.TryGetMetadata(MetadataKeys.DocumentationComment, out string? comment) || string.IsNullOrWhiteSpace(comment))
        {
            return;
        }

        var escaped = System.Security.SecurityElement.Escape(comment!);
        writer.Write($"/// <summary>{escaped}</summary>");
    }

    private static void AssignImportMethodNames(BindingCompilation compilation)
    {
        foreach (var group in compilation.Members.OfType<FunctionBinding>()
            .Where(static function => function.Parent is not RecordBinding)
            .GroupBy(function => $"{GetManagedNamespace(compilation, function)}|{GetManagedClassName(compilation, function)}|{function.ManagedName}", StringComparer.Ordinal))
        {
            var counter = 0;
            foreach (var function in group.OrderBy(static f => f.SymbolId, StringComparer.Ordinal))
            {
                counter++;
                function.ImportMethodName = counter == 1 ? function.ManagedName : $"{function.ManagedName}{counter}";
            }
        }

        foreach (var record in compilation.Records)
        {
            foreach (var group in record.Members.OfType<FunctionBinding>()
                .GroupBy(static function => function.ManagedName, StringComparer.Ordinal))
            {
                var counter = 0;
                foreach (var function in group.OrderBy(static f => f.SymbolId, StringComparer.Ordinal))
                {
                    counter++;
                    function.ImportMethodName = counter == 1 ? function.ManagedName : $"{function.ManagedName}{counter}";
                }
            }
        }
    }

    private static bool HasUnexpressibleDefaultParams(FunctionBinding function)
    {
        return function.Parameters.Any(static parameter => parameter.HasMetadata(MetadataKeys.UnexpressibleDefault));
    }

    private static bool HasNativeDefaultDispatchParams(FunctionBinding function)
    {
        return function.Parameters.Any(IsNativeDefaultDispatchParameter);
    }

    private static bool IsNativeDefaultDispatchParameter(ParameterBinding parameter)
    {
        return parameter.HasMetadata(MetadataKeys.UnexpressibleDefault) ||
            IsNullableManagedReferenceDefaultParameter(parameter);
    }

    private static bool HasUndefaultableParams(FunctionBinding function)
    {
        return function.Parameters.Any(static parameter => parameter.HasMetadata(MetadataKeys.UndefaultableParam));
    }

    private static int GetFirstUndefaultableParamIndex(FunctionBinding function)
    {
        for (var i = 0; i < function.Parameters.Count; i++)
        {
            if (function.Parameters[i].HasMetadata(MetadataKeys.UndefaultableParam))
            {
                return i;
            }
        }

        return -1;
    }

    private static IReadOnlyList<int> GetUnexpressibleDefaultParameterIndices(FunctionBinding function)
    {
        var indices = new List<int>();
        for (var i = 0; i < function.Parameters.Count; i++)
        {
            if (function.Parameters[i].HasMetadata(MetadataKeys.UnexpressibleDefault))
            {
                indices.Add(i);
            }
        }

        return indices;
    }

    private static IReadOnlyList<int> GetUnexpressibleDefaultConvenienceOverloadParameterIndices(FunctionBinding function)
    {
        var unexpressibleDefaultParameters = GetUnexpressibleDefaultParameterIndices(function);
        if (unexpressibleDefaultParameters.Count == 0)
        {
            return Array.Empty<int>();
        }

        var mainDefaultFlags = GetManagedPublicParameterDefaultFlags(function);
        return unexpressibleDefaultParameters
            .Where(index => !IsRedundantUnexpressibleDefaultConvenienceOverload(function, index, mainDefaultFlags))
            .ToArray();
    }

    private static bool IsRedundantUnexpressibleDefaultConvenienceOverload(
        FunctionBinding function,
        int keepCount,
        IReadOnlyList<bool> mainDefaultFlags)
    {
        for (var i = 0; i < keepCount; i++)
        {
            if (HasManagedUnexpressibleDefaultOverloadParameterDefault(function, keepCount, i) && !mainDefaultFlags[i])
            {
                return false;
            }
        }

        for (var i = keepCount; i < function.Parameters.Count; i++)
        {
            if (!mainDefaultFlags[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasManagedUnexpressibleDefaultOverloadParameterDefault(FunctionBinding function, int keepCount, int parameterIndex)
    {
        if (function.Parameters[parameterIndex].HasMetadata(MetadataKeys.UnexpressibleDefault))
        {
            return false;
        }

        var prefixFlags = GetManagedPublicParameterDefaultFlags(function.Parameters.Take(keepCount).ToArray());
        return parameterIndex < prefixFlags.Count && prefixFlags[parameterIndex];
    }

    private static IReadOnlyList<bool> GetManagedPublicParameterDefaultFlags(FunctionBinding function)
    {
        return GetManagedPublicParameterDefaultFlags(function.Parameters.ToArray());
    }

    private static IReadOnlyList<bool> GetManagedPublicParameterDefaultFlags(IReadOnlyList<ParameterBinding> parameterArray)
    {
        var firstUnexpressibleDefault = parameterArray
            .Select((parameter, index) => (parameter, index))
            .FirstOrDefault(static entry => entry.parameter.HasMetadata(MetadataKeys.UnexpressibleDefault));
        var lastRequiredParameterIndex = -1;
        for (var i = parameterArray.Count - 1; i >= 0; i--)
        {
            if (!CanRenderManagedPublicDefault(parameterArray[i]))
            {
                lastRequiredParameterIndex = i;
                break;
            }
        }

        var flags = new bool[parameterArray.Count];

        for (var i = 0; i < parameterArray.Count; i++)
        {
            var parameter = parameterArray[i];
            if (firstUnexpressibleDefault.parameter is not null)
            {
                if (i < firstUnexpressibleDefault.index)
                {
                    continue;
                }

                if (parameter.HasMetadata(MetadataKeys.UnexpressibleDefault))
                {
                    flags[i] = i >= lastRequiredParameterIndex;
                    continue;
                }
            }

            flags[i] = i >= lastRequiredParameterIndex && CanRenderManagedPublicDefault(parameter);
        }

        return flags;
    }

    private static IReadOnlyList<int> GetNativeDefaultDispatchParameterIndices(FunctionBinding function)
    {
        var indices = new List<int>();
        for (var i = 0; i < function.Parameters.Count; i++)
        {
            if (IsNativeDefaultDispatchParameter(function.Parameters[i]))
            {
                indices.Add(i);
            }
        }

        return indices;
    }

    private static IReadOnlyList<int> GetVariantParameterIndices(FunctionBinding function)
    {
        var indices = new HashSet<int>(GetNativeDefaultDispatchParameterIndices(function));
        var firstUndefaultable = GetFirstUndefaultableParamIndex(function);
        if (firstUndefaultable >= 0)
        {
            indices.Add(firstUndefaultable);
        }

        return indices.OrderBy(static i => i).ToArray();
    }

    private static IReadOnlyList<ParameterBinding> GetUnexpressibleVariantParams(FunctionBinding function, int keepCount)
    {
        return function.Parameters.Take(keepCount).ToArray();
    }

    private static string GetUnexpressibleVariantImportName(string importMethodName, int keepCount)
    {
        return $"{importMethodName}__Args{keepCount}";
    }

    private static string GetUnexpressibleVariantEntrypointName(string entryPointName, int keepCount)
    {
        return $"{entryPointName}__Args{keepCount}";
    }

    private static string RenderUnexpressibleDefaultFreeFunctionImport(BindingCompilation compilation, FunctionBinding function)
    {
        var writer = new CodeWriter();
        var importMethodName = GetManagedImportMethodName(function);
        foreach (var keepCount in GetVariantParameterIndices(function))
        {
            var variantParams = GetUnexpressibleVariantParams(function, keepCount);
            var parameterList = string.Join(", ", variantParams.Select(parameter => RenderManagedImportParameter(parameter, compilation)));
            var returnTypeName = GetManagedImportReturnTypeName(compilation, function.ReturnType);
            var returnAttribute = GetManagedImportReturnAttribute(compilation, function.ReturnType, ReturnsOwnedHandle(function));
            var importName = GetUnexpressibleVariantImportName(importMethodName, keepCount);
            var entrypointName = GetUnexpressibleVariantEntrypointName(function.EntryPointName, keepCount);
            var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
                ? $"internal static partial {returnTypeName} {importName}({parameterList});"
                : $"{returnAttribute}\ninternal static partial {returnTypeName} {importName}({parameterList});";

            writer.Write($$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{entrypointName}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                {{importMethodLine}}
                """);
        }
        return writer.ToString();
    }

    private static string RenderManagedUnexpressibleDefaultStaticImports(BindingCompilation compilation, FunctionBinding function)
    {
        var writer = new CodeWriter();
        var importMethodName = GetManagedImportMethodName(function);
        foreach (var keepCount in GetVariantParameterIndices(function))
        {
            var variantParams = GetUnexpressibleVariantParams(function, keepCount);
            var parameterList = string.Join(", ", variantParams.Select(parameter => RenderManagedImportParameter(parameter, compilation)));
            var returnTypeName = GetManagedImportReturnTypeName(compilation, function.ReturnType);
            var returnAttribute = GetManagedImportReturnAttribute(compilation, function.ReturnType, ReturnsOwnedHandle(function));
            var variantImportName = GetUnexpressibleVariantImportName(importMethodName, keepCount);
            var variantEntrypointName = GetUnexpressibleVariantEntrypointName(function.EntryPointName, keepCount);
            var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
                ? $"internal static partial {returnTypeName} {variantImportName}({parameterList});"
                : $"{returnAttribute}\ninternal static partial {returnTypeName} {variantImportName}({parameterList});";

            writer.Write($$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{variantEntrypointName}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                {{importMethodLine}}
                """);
        }

        return writer.ToString();
    }

    private static string RenderManagedUnexpressibleDefaultInstanceImports(BindingCompilation compilation, FunctionBinding function, string recordManagedName, bool useCallbase = false)
    {
        var writer = new CodeWriter();
        var importMethodName = useCallbase
            ? GetManagedCallbaseImportMethodName(function)
            : GetManagedImportMethodName(function);
        var owningRecord = function.FindOwningRecord();
        var entryPointName = useCallbase && owningRecord is not null
            ? GetNativeCallbaseEntrypointName(owningRecord, function)
            : function.EntryPointName;

        foreach (var keepCount in GetVariantParameterIndices(function))
        {
            var variantParams = GetUnexpressibleVariantParams(function, keepCount);
            var parameterList = string.Join(", ", new[] { $"{recordManagedName} self" }.Concat(variantParams.Select(parameter => RenderManagedImportParameter(parameter, compilation))));
            var returnTypeName = GetManagedImportReturnTypeName(compilation, function.ReturnType);
            var returnAttribute = GetManagedImportReturnAttribute(compilation, function.ReturnType, ReturnsOwnedHandle(function));
            var variantImportName = GetUnexpressibleVariantImportName(importMethodName, keepCount);
            var variantEntrypointName = GetUnexpressibleVariantEntrypointName(entryPointName, keepCount);
            var importMethodLine = string.IsNullOrWhiteSpace(returnAttribute)
                ? $"internal static partial {returnTypeName} {variantImportName}({parameterList});"
                : $"{returnAttribute}\ninternal static partial {returnTypeName} {variantImportName}({parameterList});";

            writer.Write($$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{variantEntrypointName}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                {{importMethodLine}}
                """);
        }

        return writer.ToString();
    }

    private static string RenderManagedUnexpressibleDefaultConstructorImports(BindingCompilation compilation, FunctionBinding function)
    {
        var writer = new CodeWriter();
        var importMethodName = GetManagedImportMethodName(function);
        foreach (var keepCount in GetVariantParameterIndices(function))
        {
            var variantParams = GetUnexpressibleVariantParams(function, keepCount);
            var parameterList = string.Join(", ", variantParams.Select(parameter => RenderManagedImportParameter(parameter, compilation)));
            var variantImportName = GetUnexpressibleVariantImportName(importMethodName, keepCount);
            var variantEntrypointName = GetUnexpressibleVariantEntrypointName(function.EntryPointName, keepCount);

            writer.Write($$"""
                [LibraryImport("{{compilation.NativeLibraryName}}", EntryPoint = "{{variantEntrypointName}}", StringMarshalling = StringMarshalling.Utf8)]
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static partial nint {{variantImportName}}({{parameterList}});
                """);
        }

        return writer.ToString();
    }

    private static void EmitUnexpressibleDefaultVariant(
        CodeWriter writer,
        FunctionBinding function,
        IReadOnlyList<ParameterBinding> variantParams,
        int keepCount,
        string entryPointBaseName,
        string invocation,
        string? selfCppTypeName = null)
    {
        var isSelf = selfCppTypeName is not null;

        var parameterParts = isSelf
            ? new[] { $"arg_t<{selfCppTypeName}*> self" }.Concat(variantParams.Select(RenderNativeParameter))
            : variantParams.Select(RenderNativeParameter);
        var parameterList = string.Join(", ", parameterParts);

        var abiArgs = string.Join(", ", variantParams.Select(static p => p.NativeName));

        var entrypointName = GetUnexpressibleVariantEntrypointName(entryPointBaseName, keepCount);

        var cppTypeParts = isSelf
            ? new[] { $"{selfCppTypeName}*" }.Concat(variantParams.Select(static p => p.CppTypeName))
            : variantParams.Select(static p => p.CppTypeName);
        var cppTypeNames = string.Join(", ", cppTypeParts);

        var paramDeclParts = isSelf
            ? new[] { "auto&& self" }.Concat(variantParams.Select(static p => $"auto&& {p.NativeName}"))
            : variantParams.Select(static p => $"auto&& {p.NativeName}");
        var cppParamDecls = string.Join(", ", paramDeclParts);

        var signature = $"{function.CppReturnTypeName}({cppTypeNames})";
        var marshallerTemplateArguments = BuildMarshallerInvokerTemplateArguments("FT", cppTypeParts);

        var invokeAsArgs = isSelf
            ? $"fn, self{(abiArgs.Length > 0 ? $", {abiArgs}" : string.Empty)}"
            : $"fn{(abiArgs.Length > 0 ? $", {abiArgs}" : string.Empty)}";

        writer.Write($$"""
            SHARPCPP_EXPORT {{GetNativeEntrypointReturnType(function)}} {{entrypointName}}({{parameterList}})
            {
                using FT = {{signature}};
                auto fn = []({{cppParamDecls}}) -> decltype(auto)
                {
                    {{invocation}}
                };
                {{(RequiresOwnedRecordValueReturn(function)
                    ? $"return MarshallerInvoker::invoke_owned_as<{marshallerTemplateArguments}>({invokeAsArgs});"
                    : $"return MarshallerInvoker::invoke_as<{marshallerTemplateArguments}>({invokeAsArgs});")}}
            }

            """);
    }

    private static string ForwardVariantArgs(IReadOnlyList<ParameterBinding> variantParams)
    {
        return string.Join(", ", variantParams.Select(static p => $"std::forward<decltype({p.NativeName})>({p.NativeName})"));
    }

    private static string BuildNativeVariantCallArguments(FunctionBinding function, IReadOnlyList<ParameterBinding> variantParams)
    {
        var providedParameterNames = new HashSet<string>(variantParams.Select(static parameter => parameter.NativeName), StringComparer.Ordinal);
        var arguments = new List<string>(function.Parameters.Count);
        foreach (var parameter in function.Parameters)
        {
            if (providedParameterNames.Contains(parameter.NativeName))
            {
                arguments.Add($"std::forward<decltype({parameter.NativeName})>({parameter.NativeName})");
                continue;
            }

            if (!parameter.TryGetMetadata<string>(MetadataKeys.NativeDefaultValueExpression, out var defaultValue) ||
                string.IsNullOrWhiteSpace(defaultValue))
            {
                defaultValue = GetFallbackNativeDefaultValueExpression(parameter);
            }

            if (string.IsNullOrWhiteSpace(defaultValue))
            {
                throw new InvalidOperationException($"Parameter '{parameter.ManagedName}' on '{function.ManagedName}' is missing native default metadata.");
            }

            arguments.Add(defaultValue);
        }

        return string.Join(", ", arguments);
    }

    private static string? GetFallbackNativeDefaultValueExpression(ParameterBinding parameter)
    {
        if (parameter.CppTypeName.Contains('*', StringComparison.Ordinal) ||
            parameter.AbiKind == BindingParameterAbiKind.Pointer ||
            parameter.Type.IsPointerType ||
            parameter.Type.PrimitiveKind == BindingPrimitiveKind.IntPtr)
        {
            return "nullptr";
        }

        if (string.Equals(parameter.ManagedDefaultValueExpression, "null", StringComparison.Ordinal) &&
            IsManagedReferenceDefaultParameter(parameter))
        {
            return "nullptr";
        }

        if (string.Equals(parameter.ManagedDefaultValueExpression, "default", StringComparison.Ordinal) &&
            IsManagedReferenceDefaultParameter(parameter))
        {
            return "nullptr";
        }

        return null;
    }

    private static string BuildConstructorDefaultNamespacePreamble(FunctionBinding constructor)
    {
        var cppTypeName = constructor.OwningRecordCppTypeName;
        if (string.IsNullOrWhiteSpace(cppTypeName))
            return string.Empty;

        var lastSeparatorIndex = cppTypeName.LastIndexOf("::", StringComparison.Ordinal);
        if (lastSeparatorIndex <= 0)
            return string.Empty;

        var cppNamespace = cppTypeName[..lastSeparatorIndex];
        return $"using namespace {cppNamespace};";
    }

    private static string RenderNativeUnexpressibleDefaultFreeFunction(FunctionBinding function)
    {
        var writer = new CodeWriter();
        foreach (var keepCount in GetVariantParameterIndices(function))
        {
            var variantParams = GetUnexpressibleVariantParams(function, keepCount);
            var invocation = $"return {function.CppQualifiedName}({ForwardVariantArgs(variantParams)});";
            EmitUnexpressibleDefaultVariant(writer, function, variantParams, keepCount, function.EntryPointName, invocation);
        }
        return writer.ToString();
    }

    private static string RenderNativeUnexpressibleDefaultStaticMethod(FunctionBinding method)
    {
        var writer = new CodeWriter();
        foreach (var keepCount in GetVariantParameterIndices(method))
        {
            var variantParams = GetUnexpressibleVariantParams(method, keepCount);
            var invocation = $"return {method.OwningRecordCppTypeName}::{method.NativeName}({ForwardVariantArgs(variantParams)});";
            EmitUnexpressibleDefaultVariant(writer, method, variantParams, keepCount, method.EntryPointName, invocation);
        }
        return writer.ToString();
    }

    private static string RenderNativeUnexpressibleDefaultInstanceMethod(FunctionBinding method)
    {
        if (method.AccessModifier == BindingAccessModifier.Protected)
        {
            throw new NotSupportedException($"Protected member '{method.ManagedName}' does not support unexpressible defaults yet.");
        }

        var writer = new CodeWriter();
        foreach (var keepCount in GetVariantParameterIndices(method))
        {
            var variantParams = GetUnexpressibleVariantParams(method, keepCount);
            var forwardedArgs = ForwardVariantArgs(variantParams);
            var invocation = method.NativeDispatchRecordCppTypeName is null
                ? $"return std::forward<decltype(self)>(self)->{method.NativeName}({forwardedArgs});"
                : $$"""
                    auto* dispatch = dynamic_cast<{{method.NativeDispatchRecordCppTypeName}}*>(std::forward<decltype(self)>(self));
                    if (dispatch == nullptr)
                    {
                        throw std::runtime_error("SharpCpp failed to adjust the native handle for secondary-base dispatch.");
                    }
                    return dispatch->{{method.NativeName}}({{forwardedArgs}});
                    """;
            EmitUnexpressibleDefaultVariant(writer, method, variantParams, keepCount, method.EntryPointName, invocation, method.OwningRecordCppTypeName);
        }
        return writer.ToString();
    }

    private static string RenderNativeUnexpressibleDefaultCallbase(RecordBinding record, FunctionBinding method)
    {
        var writer = new CodeWriter();
        var helperName = GetNativeCallbaseHelperName(method);
        var baseEntrypointName = GetNativeCallbaseEntrypointName(record, method);

        foreach (var keepCount in GetVariantParameterIndices(method))
        {
            var variantParams = GetUnexpressibleVariantParams(method, keepCount);
            var forwardedArgs = ForwardVariantArgs(variantParams);
            var invocation = $"return {helperName}(std::forward<decltype(self)>(self){(forwardedArgs.Length > 0 ? $", {forwardedArgs}" : string.Empty)});";
            EmitUnexpressibleDefaultVariant(writer, method, variantParams, keepCount, baseEntrypointName, invocation, record.CppQualifiedName);
        }
        return writer.ToString();
    }

    private static string RenderNativeUnexpressibleDefaultConstructor(FunctionBinding constructor, bool requiresDirector)
    {
        var writer = new CodeWriter();
        var constructedTypeName = requiresDirector
            ? GetNativeDirectorClassName((RecordBinding)constructor.Parent!)
            : constructor.OwningRecordCppTypeName;
        var namespacePreamble = BuildConstructorDefaultNamespacePreamble(constructor);

        foreach (var keepCount in GetVariantParameterIndices(constructor))
        {
            var variantParams = GetUnexpressibleVariantParams(constructor, keepCount);
            var invocation = string.IsNullOrWhiteSpace(namespacePreamble)
                ? $"return new {constructedTypeName}({BuildNativeVariantCallArguments(constructor, variantParams)});"
                : $"{namespacePreamble}\n        return new {constructedTypeName}({BuildNativeVariantCallArguments(constructor, variantParams)});";
            EmitUnexpressibleDefaultVariant(writer, constructor, variantParams, keepCount, constructor.EntryPointName, invocation);
        }
        return writer.ToString();
    }

    private static string BuildManagedDispatchArgument(ParameterBinding parameter, string plannedArgument)
    {
        if (!parameter.HasMetadata(MetadataKeys.UnexpressibleDefault) || IsManagedReferenceDefaultParameter(parameter))
        {
            return plannedArgument;
        }

        var valueExpression = $"{parameter.ManagedName}.Value";
        if (!parameter.Type.IsPointerType)
        {
            return parameter.Direction switch
            {
                BindingParameterDirection.Ref => $"ref {valueExpression}",
                BindingParameterDirection.Out => $"out {valueExpression}",
                _ => valueExpression
            };
        }
        return parameter.Direction switch
        {
            BindingParameterDirection.In when parameter.AbiKind != BindingParameterAbiKind.Value => $"in {valueExpression}",
            BindingParameterDirection.Ref => $"ref {valueExpression}",
            BindingParameterDirection.Out => $"out {valueExpression}",
            _ => valueExpression
        };
    }

    private static string BuildManagedDispatchArgumentRequired(ParameterBinding parameter, string plannedArgument)
    {
        // For required (non-nullable) params, just use the planned argument directly
        return plannedArgument;
    }

    private static string BuildManagedDispatchArguments(FunctionBinding function, ManagedInvocationPlan plan, IReadOnlyList<string> leadingArguments, int keepCount)
    {
        var arguments = new List<string>(leadingArguments);
        for (var i = 0; i < keepCount; i++)
        {
            var parameter = function.Parameters[i];
            arguments.Add(BuildManagedDispatchArgument(parameter, plan.Arguments[i]));
        }

        return string.Join(", ", arguments);
    }

    private static string BuildUnexpressibleDefaultCondition(ParameterBinding parameter, bool provided)
    {
        if (IsManagedReferenceDefaultParameter(parameter))
        {
            return $"{parameter.ManagedName} is {(provided ? "not null" : "null")}";
        }

        return provided
            ? $"{parameter.ManagedName}.HasValue"
            : $"!{parameter.ManagedName}.HasValue";
    }

    private static bool IsManagedReferenceDefaultParameter(ParameterBinding parameter)
    {
        return parameter.Type.IsPointerType ||
            parameter.Type.IsCallback ||
            IsManagedReferenceTypeName(parameter.Type.ManagedTypeName);
    }

    private static bool IsNullableManagedReferenceDefaultParameter(ParameterBinding parameter)
    {
        if (parameter.Direction != BindingParameterDirection.In ||
            parameter.AbiKind != BindingParameterAbiKind.Value)
        {
            return false;
        }

        if (parameter.ManagedDefaultValueExpression == "null")
        {
            return IsManagedReferenceDefaultParameter(parameter);
        }

        return parameter.ManagedDefaultValueExpression == "default" &&
            IsManagedReferenceTypeName(parameter.Type.ManagedTypeName);
    }

    private static bool IsManagedReferenceTypeName(string managedTypeName)
    {
        return string.Equals(managedTypeName, "string", StringComparison.Ordinal) ||
            string.Equals(managedTypeName, "global::System.String", StringComparison.Ordinal);
    }

    private static bool IsInputHandlePointerArrayParameter(ParameterBinding parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        return parameter.Direction == BindingParameterDirection.In &&
            parameter.AbiKind == BindingParameterAbiKind.Pointer &&
            parameter.Type.Kind == BindingTypeKind.Handle &&
            IsPointerToPointerType(parameter.CppTypeName);
    }

    private static bool IsInputPrimitivePointerArrayParameter(ParameterBinding parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        return parameter.HasMetadata(MetadataKeys.InputPointerArray) &&
            parameter.Direction == BindingParameterDirection.In &&
            parameter.AbiKind == BindingParameterAbiKind.Pointer &&
            parameter.Type.Kind == BindingTypeKind.Primitive;
    }

    private static bool IsInputPointerArrayParameter(ParameterBinding parameter)
    {
        return IsInputHandlePointerArrayParameter(parameter) ||
            IsInputPrimitivePointerArrayParameter(parameter);
    }

    private static bool RequiresArrayElementAccess(BindingCompilation compilation, RecordBinding record)
    {
        if (record.HasMetadata(MetadataKeys.ForwardDeclaration))
            return false;

        return compilation.Members.OfType<FunctionBinding>()
            .Concat(compilation.Records.SelectMany(static candidate => candidate.Members.OfType<FunctionBinding>()))
            .SelectMany(static function => function.Parameters)
            .Any(parameter => parameter.Direction == BindingParameterDirection.Out
                && parameter.AbiKind == BindingParameterAbiKind.Pointer
                && parameter.Type.Kind == BindingTypeKind.Handle
                && IsPointerToPointerType(parameter.CppTypeName)
                && string.Equals(parameter.Type.RecordId, record.SymbolId, StringComparison.Ordinal));
    }

    private static bool IsPointerToPointerType(string cppTypeName)
    {
        var normalized = cppTypeName
            .Replace("const", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();
        return normalized.EndsWith("**", StringComparison.Ordinal);
    }

    private static string GetPointerArrayAbiSpanName(ParameterBinding parameter)
    {
        return $"__abi_{parameter.ManagedName}";
    }

    private static string GetPointerArrayPointerName(ParameterBinding parameter)
    {
        return $"{GetPointerArrayAbiSpanName(parameter)}_ptr";
    }

    private static string BuildUnexpressibleDefaultCondition(FunctionBinding function, IReadOnlyList<int> defaultIndices, int providedDefaultCount)
    {
        var parts = new List<string>(defaultIndices.Count);
        for (var i = 0; i < defaultIndices.Count; i++)
        {
            parts.Add(BuildUnexpressibleDefaultCondition(function.Parameters[defaultIndices[i]], provided: i < providedDefaultCount));
        }

        return string.Join(" && ", parts);
    }

    private static void WriteUndefaultableOverloads(
        BindingCompilation compilation,
        CodeWriter writer,
        FunctionBinding function,
        string importMethodName,
        IReadOnlyList<string> leadingArguments,
        bool isConstructor = false)
    {
        var firstU = GetFirstUndefaultableParamIndex(function);
        if (firstU < 0)
        {
            // No undefaultable params — normal path, shouldn't reach here
            return;
        }

        // Build common invocation plan for the full parameter set
        var fullPlan = BuildManagedInvocationPlan(compilation, function.Parameters);

        // =========================================================
        // Overload A: params before first undefaultable index
        // =========================================================
        {
            var beforeParams = function.Parameters.Take(firstU).ToArray();

            // Check if before-params have any unexpressible defaults
            var beforeHasUnexpressible = beforeParams.Any(p => p.HasMetadata(MetadataKeys.UnexpressibleDefault));

            // Render public signature for overload A
            var aParamList = string.Join(", ", beforeParams.Select(p => RenderManagedPublicParameter(p, compilation)));

            if (isConstructor)
            {
                writer.Write($$"""
                    public {{function.ManagedName}}({{aParamList}})
                    {
                    """);
            }
            else if (function.IsStatic)
            {
                writer.Write($$"""
                    {{GetManagedVisibility(function.AccessModifier)}} static {{GetManagedPublicReturnTypeName(function.ReturnType, compilation)}} {{function.ManagedName}}({{aParamList}})
                    {
                    """);
            }
            else
            {
                writer.Write($$"""
                    {{GetManagedVisibility(function.AccessModifier)}} {{GetManagedPublicReturnTypeName(function.ReturnType, compilation)}} {{function.ManagedName}}({{aParamList}})
                    {
                    """);
            }

            if (firstU == 0)
            {
                // Overload A has no params — call __Args0 variant
                var variantImportName = GetUnexpressibleVariantImportName(importMethodName, 0);
                if (isConstructor)
                {
                    writer.Write($$"""
                        var __handle = __PInvoke.{{variantImportName}}();
                        """);
                }
                else if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
                {
                    writer.Write($$"""
                        __PInvoke.{{variantImportName}}({{string.Join(", ", leadingArguments)}});
                        """);
                }
                else
                {
                    var leading = leadingArguments.Count > 0 ? string.Join(", ", leadingArguments) + ", " : string.Empty;
                    writer.Write($$"""
                        var __result = __PInvoke.{{variantImportName}}({{leading.TrimEnd(',', ' ')}});
                        """);
                }
            }
            else if (beforeHasUnexpressible)
            {
                // Cascade for before-params with unexpressible defaults
                WriteUnexpressibleDefaultCascade(compilation, writer, function, importMethodName, leadingArguments, firstU);
                // Need to get past the closing brace
            }
            else
            {
                // Direct call with firstU params — use __Args{firstU} variant
                var variantImportName = GetUnexpressibleVariantImportName(importMethodName, firstU);
                var beforePlan = BuildManagedInvocationPlan(compilation, beforeParams);
                var beforeArgs = string.Join(", ", leadingArguments.Concat(beforePlan.Arguments));

                if (isConstructor)
                {
                    writer.Write($$"""
                        var __handle = __PInvoke.{{variantImportName}}({{beforeArgs}});
                        """);
                }
                else if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
                {
                    writer.Write($$"""
                        __PInvoke.{{variantImportName}}({{beforeArgs}});
                        """);
                }
                else
                {
                    writer.Write($$"""
                        var __result = __PInvoke.{{variantImportName}}({{beforeArgs}});
                        """);
                }
            }

            // Handle return and constructor post-call
            if (isConstructor)
            {
                writer.Write("""
                    return __handle;
                    }
                    """);
            }
            else if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
            {
                writer.Write("""
                    }
                    """);
            }
            else
            {
                writer.Write($$"""
                    return {{BuildManagedHandleWrapIfNeeded(compilation, function.ReturnType, "__result", ReturnsOwnedHandle(function))}};
                    }
                    """);
            }

            writer.WriteLine();
        }

        // =========================================================
        // Overload B: all params, undefaultable ones required
        // =========================================================
        {
            // Render public parameter list: params before firstU are required,
            // firstU is required, params after firstU get normal defaults
            var bParamParts = new List<string>();
            for (var i = 0; i < function.Parameters.Count; i++)
            {
                var param = function.Parameters[i];
                if (i < firstU || param.HasMetadata(MetadataKeys.UndefaultableParam))
                {
                    // Required: no default, non-nullable
                    bParamParts.Add(RenderManagedPublicParameterRequired(param, compilation));
                }
                else
                {
                    // Normal default rules
                    bParamParts.Add(RenderManagedPublicParameter(param, compilation));
                }
            }
            var bParamList = string.Join(", ", bParamParts);

            if (isConstructor)
            {
                writer.Write($$"""
                    public {{function.ManagedName}}({{bParamList}})
                    {
                    """);
            }
            else if (function.IsStatic)
            {
                writer.Write($$"""
                    {{GetManagedVisibility(function.AccessModifier)}} static {{GetManagedPublicReturnTypeName(function.ReturnType, compilation)}} {{function.ManagedName}}({{bParamList}})
                    {
                    """);
            }
            else
            {
                writer.Write($$"""
                    {{GetManagedVisibility(function.AccessModifier)}} {{GetManagedPublicReturnTypeName(function.ReturnType, compilation)}} {{function.ManagedName}}({{bParamList}})
                    {
                    """);
            }

            // Emit pre-call statements
            foreach (var statement in fullPlan.PreCallStatements)
            {
                writer.Write(statement);
            }

            // Check if after-firstU params have unexpressible defaults
            var afterParams = function.Parameters.Skip(firstU).ToArray();
            var afterHasUnexpressible = afterParams.Any(static p =>
                p.HasMetadata(MetadataKeys.UnexpressibleDefault) &&
                !p.HasMetadata(MetadataKeys.UndefaultableParam));

            if (afterHasUnexpressible)
            {
                // Build a cascade for the after-params portion (indices >= firstU)
                // This mirrors the standard cascade but only for after-firstU params.
                // Before-firstU params are always provided (required, non-nullable).
                var afterDefaultIndices = new List<int>();
                for (var i = firstU; i < function.Parameters.Count; i++)
                {
                    if (function.Parameters[i].HasMetadata(MetadataKeys.UnexpressibleDefault) &&
                        !function.Parameters[i].HasMetadata(MetadataKeys.UndefaultableParam))
                    {
                        afterDefaultIndices.Add(i);
                    }
                }

                // Each branch handles its own dispatch + post-call + return
                for (var providedDefaultCount = afterDefaultIndices.Count; providedDefaultCount >= 0; providedDefaultCount--)
                {
                    var keepCount = providedDefaultCount < afterDefaultIndices.Count
                        ? afterDefaultIndices[providedDefaultCount]
                        : function.Parameters.Count;
                    WriteUndefaultableBranchB(compilation, writer, function, importMethodName, fullPlan,
                        leadingArguments, keepCount, afterDefaultIndices, providedDefaultCount);
                }

                // Fallback: should never reach here if cascade conditions are complete
                writer.Write(
                    """
                        throw new global::System.ArgumentException("SharpCpp optional arguments that depend on native defaults must be supplied as a trailing contiguous suffix.");
                        }
                    """);
            }
            else
            {
                // Direct call with all params
                var allArgs = string.Join(", ", leadingArguments.Concat(fullPlan.Arguments));

                if (isConstructor)
                {
                    writer.Write($$"""
                        var __handle = __PInvoke.{{importMethodName}}({{allArgs}});
                        """);
                }
                else if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
                {
                    writer.Write($$"""
                        __PInvoke.{{importMethodName}}({{allArgs}});
                        """);
                }
                else
                {
                    writer.Write($$"""
                        var __result = __PInvoke.{{importMethodName}}({{allArgs}});
                        """);
                }

                // Post-call statements (only on non-cascade path)
                foreach (var statement in fullPlan.PostCallStatements)
                {
                    writer.Write(statement);
                }

                // Return / constructor finalize (only on non-cascade path)
                if (isConstructor)
                {
                    writer.Write("""
                        return __handle;
                        }
                        """);
                }
                else if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
                {
                    writer.Write("""
                        }
                        """);
                }
                else
                {
                    writer.Write($$"""
                        return {{BuildManagedHandleWrapIfNeeded(compilation, function.ReturnType, "__result", ReturnsOwnedHandle(function))}};
                        }
                        """);
                }
            }
        }
    }

    private static void WriteUndefaultableBranchB(
        BindingCompilation compilation,
        CodeWriter writer,
        FunctionBinding function,
        string importMethodName,
        ManagedInvocationPlan plan,
        IReadOnlyList<string> leadingArguments,
        int keepCount,
        IReadOnlyList<int> defaultIndices,
        int? providedDefaultCount = null)
    {
        // For Overload B cascade branches, the before-firstU params are always present (non-nullable).
        // The cascade condition only tests the after-firstU params that have unexpressible defaults.
        if (providedDefaultCount.HasValue)
        {
            // Build condition for the unexpressible after-params
            var relevantIndices = defaultIndices.Where(i => i >= GetFirstUndefaultableParamIndex(function)).ToList();
            if (relevantIndices.Count == 0)
            {
                // No cascade needed
                return;
            }

            var condition = BuildUndefaultableBCondition(function, relevantIndices, providedDefaultCount.Value);
            writer.WriteLine($"if ({condition})");
            writer.WriteLine("{");
        }

        // Build dispatch arguments — for required params, use the plan argument directly;
        // for unexpressible default after-params, unwrap .Value
        var args = new List<string>(leadingArguments);
        for (var i = 0; i < keepCount; i++)
        {
            var param = function.Parameters[i];
            if (param.HasMetadata(MetadataKeys.UndefaultableParam) ||
                (i < GetFirstUndefaultableParamIndex(function) && !param.HasMetadata(MetadataKeys.UnexpressibleDefault)))
            {
                // Required param — use plan argument directly
                args.Add(plan.Arguments[i]);
            }
            else
            {
                // May have unexpressible default — use .Value unwrapping if needed
                args.Add(BuildManagedDispatchArgument(param, plan.Arguments[i]));
            }
        }
        var invokeArgs = string.Join(", ", args);
        var branchImportName = keepCount == function.Parameters.Count
            ? importMethodName
            : GetUnexpressibleVariantImportName(importMethodName, keepCount);

        if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
        {
            writer.Write($$"""
                __PInvoke.{{branchImportName}}({{invokeArgs}});
                """);

            foreach (var statement in plan.PostCallStatements)
            {
                writer.Write(statement);
            }

            // Prevent fallthrough to next cascade branch or throw
            if (providedDefaultCount.HasValue)
            {
                writer.WriteLine("return;");
            }
        }
        else
        {
            writer.Write($$"""
                var __result = __PInvoke.{{branchImportName}}({{invokeArgs}});
                """);

            foreach (var statement in plan.PostCallStatements)
            {
                writer.Write(statement);
            }

            writer.Write($$"""
                return {{BuildManagedHandleWrapIfNeeded(compilation, function.ReturnType, "__result", ReturnsOwnedHandle(function))}};
                """);
        }

        if (providedDefaultCount.HasValue)
        {
            writer.WriteLine("}");
        }
    }

    private static string BuildUndefaultableBCondition(FunctionBinding function, IReadOnlyList<int> defaultIndices, int providedDefaultCount)
    {
        var firstU = GetFirstUndefaultableParamIndex(function);
        var relevantIndices = defaultIndices.Where(i => i >= firstU).ToList();
        var parts = new List<string>(relevantIndices.Count);
        for (var i = 0; i < relevantIndices.Count; i++)
        {
            var param = function.Parameters[relevantIndices[i]];
            parts.Add(param.HasMetadata(MetadataKeys.UndefaultableParam)
                ? $"{param.ManagedName} is not null"
                : BuildUnexpressibleDefaultCondition(param, provided: i < providedDefaultCount));
        }

        return string.Join(" && ", parts);
    }

    private static string BuildManagedHandleWrapIfNeeded(BindingCompilation compilation, BindingType type, string variableName, bool returnsOwnedHandle)
    {
        if (type.Kind != BindingTypeKind.Handle)
        {
            return variableName;
        }

        return BuildManagedHandleReturnResultExpression(compilation, type, variableName, returnsOwnedHandle);
    }

    private static void WriteUnexpressibleDefaultCascade(BindingCompilation compilation, CodeWriter writer, FunctionBinding function, string importMethodName, IReadOnlyList<string> leadingArguments, int? maxParameterCount = null)
    {
        var parameterCount = maxParameterCount ?? function.Parameters.Count;
        var defaultIndices = GetNativeDefaultDispatchParameterIndices(function)
            .Where(index => index < parameterCount)
            .ToArray();
        var invocationPlan = BuildManagedInvocationPlan(compilation, GetUnexpressibleVariantParams(function, parameterCount));
        var fullImportName = parameterCount == function.Parameters.Count
            ? importMethodName
            : GetUnexpressibleVariantImportName(importMethodName, parameterCount);

        foreach (var statement in invocationPlan.PreCallStatements)
        {
            writer.Write(statement);
        }

        WriteUnexpressibleDefaultBranch(compilation, writer, function, fullImportName, invocationPlan, leadingArguments, parameterCount, defaultIndices, defaultIndices.Length);

        for (var providedDefaultCount = defaultIndices.Length - 1; providedDefaultCount >= 0; providedDefaultCount--)
        {
            var keepCount = defaultIndices[providedDefaultCount];
            var variantPlan = BuildManagedInvocationPlan(compilation, GetUnexpressibleVariantParams(function, keepCount));
            var variantImportName = GetUnexpressibleVariantImportName(importMethodName, keepCount);
            WriteUnexpressibleDefaultBranch(compilation, writer, function, variantImportName, variantPlan, leadingArguments, keepCount, defaultIndices, providedDefaultCount);
        }

        writer.Write(
            """
                throw new global::System.ArgumentException("SharpCpp optional arguments that depend on native defaults must be supplied as a trailing contiguous suffix.");
            """);
    }

    private static void WriteUnexpressibleDefaultBranch(BindingCompilation compilation, CodeWriter writer, FunctionBinding function, string importName,
        ManagedInvocationPlan plan, IReadOnlyList<string> leadingArguments, int keepCount, IReadOnlyList<int> defaultIndices, int providedDefaultCount)
    {
        writer.WriteLine($"if ({BuildUnexpressibleDefaultCondition(function, defaultIndices, providedDefaultCount)})");
        writer.WriteLine("{");
        WriteDispatchCall(writer, compilation, function, importName, plan, leadingArguments, keepCount);
        if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
        {
            writer.WriteLine("return;");
        }

        writer.WriteLine("}");
        writer.WriteLine();
    }

    private static void WriteDispatchCall(CodeWriter writer, BindingCompilation compilation, FunctionBinding function, string importName,
        ManagedInvocationPlan plan, IReadOnlyList<string> leadingArguments, int keepCount)
    {
        var resultVar = "__result";
        var args = BuildManagedDispatchArguments(function, plan, leadingArguments, keepCount);

        if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void)
        {
            writer.Write($$"""
                __PInvoke.{{importName}}({{args}});
                """);

            foreach (var statement in plan.PostCallStatements)
                writer.Write(statement);
        }
        else if (function.ReturnType.Kind == BindingTypeKind.Handle)
        {
            writer.Write($$"""
                var {{resultVar}} = __PInvoke.{{importName}}({{args}});
                """);

            foreach (var statement in plan.PostCallStatements)
                writer.Write(statement);

            writer.Write($$"""
                return {{BuildManagedHandleReturnResultExpression(compilation, function.ReturnType, resultVar, ReturnsOwnedHandle(function))}};
                """);
        }
        else
        {
            writer.Write($$"""
                var {{resultVar}} = __PInvoke.{{importName}}({{args}});
                """);

            foreach (var statement in plan.PostCallStatements)
                writer.Write(statement);

            writer.Write($$"""
                return {{resultVar}};
                """);
        }
    }

    private static void WriteUnexpressibleDefaultConstructorCascade(CodeWriter writer, FunctionBinding function, string importMethodName, int? maxParameterCount = null)
    {
        var parameterCount = maxParameterCount ?? function.Parameters.Count;
        var defaultIndices = GetNativeDefaultDispatchParameterIndices(function)
            .Where(index => index < parameterCount)
            .ToArray();
        var invocationPlan = BuildManagedInvocationPlan(null!, GetUnexpressibleVariantParams(function, parameterCount));
        var fullImportName = parameterCount == function.Parameters.Count
            ? importMethodName
            : GetUnexpressibleVariantImportName(importMethodName, parameterCount);

        foreach (var statement in invocationPlan.PreCallStatements)
        {
            writer.Write(statement);
        }

        WriteUnexpressibleDefaultConstructorBranch(writer, function, fullImportName, invocationPlan, parameterCount, defaultIndices, defaultIndices.Length);

        for (var providedDefaultCount = defaultIndices.Length - 1; providedDefaultCount >= 0; providedDefaultCount--)
        {
            var keepCount = defaultIndices[providedDefaultCount];
            var variantPlan = BuildManagedInvocationPlan(null!, GetUnexpressibleVariantParams(function, keepCount));
            var variantImportName = GetUnexpressibleVariantImportName(importMethodName, keepCount);
            WriteUnexpressibleDefaultConstructorBranch(writer, function, variantImportName, variantPlan, keepCount, defaultIndices, providedDefaultCount);
        }

        writer.Write(
            """
                throw new global::System.ArgumentException("SharpCpp optional arguments that depend on native defaults must be supplied as a trailing contiguous suffix.");
            """);
    }

    private static void WriteUndefaultableConstructorCascade_Filtered(
        CodeWriter writer,
        FunctionBinding function,
        string importMethodName,
        int firstU,
        IReadOnlyList<int> afterDefaultIndices)
    {
        if (afterDefaultIndices.Count == 0)
        {
            return;
        }

        // Use only the full-params import variant for Overload B (before-firstU params
        // are always present, so we never call __ArgsN for before-firstU positions).
        var invocationPlan = BuildManagedInvocationPlan(null!, function.Parameters);

        foreach (var statement in invocationPlan.PreCallStatements)
        {
            writer.Write(statement);
        }

        // Branch: all after-defaults provided → full dispatch
        var conditionAll = BuildUndefaultableBCondition(function, afterDefaultIndices, afterDefaultIndices.Count);
        writer.WriteLine($"if ({conditionAll})");
        writer.WriteLine("{");
        WriteConstructorDispatchCall(writer, function, importMethodName, invocationPlan, function.Parameters.Count);
        writer.WriteLine("}");
        writer.WriteLine();

        // Cascade branches: progressively fewer after-defaults provided
        // Each uses a __ArgsN variant that keeps only up to the relevant after-default index
        for (var providedDefaultCount = afterDefaultIndices.Count - 1; providedDefaultCount >= 0; providedDefaultCount--)
        {
            var keepCount = afterDefaultIndices[providedDefaultCount];
            var condition = BuildUndefaultableBCondition(function, afterDefaultIndices, providedDefaultCount);
            writer.WriteLine($"if ({condition})");
            writer.WriteLine("{");
            var variantImportName = GetUnexpressibleVariantImportName(importMethodName, keepCount);
            var variantPlan = BuildManagedInvocationPlan(null!, GetUnexpressibleVariantParams(function, keepCount));
            WriteConstructorDispatchCall(writer, function, variantImportName, variantPlan, keepCount);
            writer.WriteLine("}");
            writer.WriteLine();
        }

        writer.Write(
            """
                throw new global::System.ArgumentException("SharpCpp optional arguments that depend on native defaults must be supplied as a trailing contiguous suffix.");
            """);
    }

    private static void WriteUnexpressibleDefaultConstructorBranch(CodeWriter writer, FunctionBinding function, string importName,
        ManagedInvocationPlan plan, int keepCount, IReadOnlyList<int> defaultIndices, int providedDefaultCount)
    {
        writer.WriteLine($"if ({BuildUnexpressibleDefaultCondition(function, defaultIndices, providedDefaultCount)})");
        writer.WriteLine("{");
        WriteConstructorDispatchCall(writer, function, importName, plan, keepCount);
        writer.WriteLine("}");
        writer.WriteLine();
    }

    private static void WriteConstructorDispatchCall(CodeWriter writer, FunctionBinding function, string importName,
        ManagedInvocationPlan plan, int keepCount)
    {
        var args = BuildManagedDispatchArguments(function, plan, [], keepCount);

        writer.Write($$"""
            var __handle = __PInvoke.{{importName}}({{args}});
            """);

        foreach (var statement in plan.PostCallStatements)
        {
            writer.Write(statement);
        }

        writer.Write(
            """
            return __handle;
            """);
    }

}
