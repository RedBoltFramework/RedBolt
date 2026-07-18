using System.Reflection;
using System.Runtime.CompilerServices;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class MarshallerConfigExtractorTests
{
    [Fact]
    public void ExtractManagedTypeOverrides_reads_token_spaced_attribute_text_from_ast_extent()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "MarshallerConfigParsing");
        var headerPath = Path.Combine(fixtureDir, "MarshallerConfig.hpp");

        var extractorType = Type.GetType("SharpCpp.Parsing.MarshallerConfigExtractor, SharpCpp.Parsing", throwOnError: true)!;
        var method = extractorType.GetMethod("ExtractManagedTypeOverrides", BindingFlags.Public | BindingFlags.Static)!;

        var overrides = (System.Collections.IDictionary)method.Invoke(
            null,
            [new[] { headerPath }, new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" }])!;

        Assert.Equal(4, overrides.Count);
        Assert.True(overrides.Contains("int"));
        var overrideInfo = overrides["int"]!;

        var overrideType = overrideInfo.GetType();
        Assert.Equal("global::Test.Managed", (string)overrideType.GetProperty("PublicTypeName")!.GetValue(overrideInfo)!);
        Assert.True((bool)overrideType.GetProperty("Undefaultable")!.GetValue(overrideInfo)!);

        Assert.True(overrides.Contains("return:float"));
        var returnOverride = overrides["return:float"]!;
        var returnOverrideType = returnOverride.GetType();
        Assert.Equal("owning", (string)returnOverrideType.GetProperty("ReturnOwnership")!.GetValue(returnOverride)!);
        Assert.False((bool)returnOverrideType.GetProperty("HasPublicTypeMapping")!.GetValue(returnOverride)!);

        var callbackOverride = overrides.Values.Cast<object>().Single(candidate =>
            (bool)candidate.GetType().GetProperty("Callback")!.GetValue(candidate)!);
        var callbackOverrideType = callbackOverride.GetType();
        Assert.Contains("std::function", (string)callbackOverrideType.GetProperty("CppTypeName")!.GetValue(callbackOverride)!, StringComparison.Ordinal);
        Assert.True((bool)callbackOverrideType.GetProperty("HasPublicTypeMapping")!.GetValue(callbackOverride)!);
        Assert.Equal("global::Test.CallbackMarshaller", (string)callbackOverrideType.GetProperty("ManagedMarshaller")!.GetValue(callbackOverride)!);

        var dynamicArrayOverride = overrides.Values.Cast<object>().Single(candidate =>
            (bool)candidate.GetType().GetProperty("DynamicArray")!.GetValue(candidate)!);
        var dynamicArrayOverrideType = dynamicArrayOverride.GetType();
        Assert.Equal("Ea.SharedArray<{0}>", (string)dynamicArrayOverrideType.GetProperty("PublicTypeName")!.GetValue(dynamicArrayOverride)!);
    }

    [Fact]
    public void ExtractManagedTypeOverrides_reads_template_parameters_from_nested_type_patterns()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "CallbackMarshaller");
        var headerPath = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var extractorType = Type.GetType("SharpCpp.Parsing.MarshallerConfigExtractor, SharpCpp.Parsing", throwOnError: true)!;
        var method = extractorType.GetMethod("ExtractManagedTypeOverrides", BindingFlags.Public | BindingFlags.Static)!;

        var overrides = (System.Collections.IDictionary)method.Invoke(
            null,
            [new[] { headerPath }, new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" }])!;

        var hashMapOverride = overrides.Values.Cast<object>().Single(candidate =>
            ((string)candidate.GetType().GetProperty("CppTypeName")!.GetValue(candidate)!).Contains("hash_map", StringComparison.Ordinal));
        var templateParameterNames = ((System.Collections.IEnumerable)hashMapOverride.GetType().GetProperty("TemplateParameterNames")!.GetValue(hashMapOverride)!)
            .Cast<string>()
            .ToArray();

        Assert.Equal(new[] { "TKey", "TValue", "THash", "TPredicate", "TAllocator" }, templateParameterNames);
    }

    [Fact]
    public void ExtractManagedTypeOverrides_keeps_distinct_std_and_eastl_unordered_map_configs()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "HashMapAlias");
        var includeDir = Path.Combine(repoRoot, "include");
        var coreMarshallerHeader = Path.Combine(includeDir, "SharpCpp", "Marshaller.hpp");
        var eastlMarshallerHeader = Path.Combine(fixtureDir, "SampleMarshallers.h");

        var extractorType = Type.GetType("SharpCpp.Parsing.MarshallerConfigExtractor, SharpCpp.Parsing", throwOnError: true)!;
        var method = extractorType.GetMethod("ExtractManagedTypeOverrides", BindingFlags.Public | BindingFlags.Static)!;

        var clangArgs = CreateStandardLibraryClangArgs(fixtureDir, includeDir);
        var overrides = (System.Collections.IDictionary)method.Invoke(
            null,
            [new[] { coreMarshallerHeader, eastlMarshallerHeader }, clangArgs])!;
        var keys = overrides.Keys.Cast<string>().OrderBy(static key => key, StringComparer.Ordinal).ToArray();
        var mapKeys = string.Join("; ", keys.Where(static key => key.Contains("map", StringComparison.Ordinal)).Select(key =>
        {
            var value = overrides[key]!;
            var publicType = (string)value.GetType().GetProperty("PublicTypeName")!.GetValue(value)!;
            return $"{key}=>{publicType}";
        }));

        Assert.True(keys.Any(static key => key.StartsWith("std::unordered_map<", StringComparison.Ordinal)), mapKeys);
        Assert.True(keys.Any(static key => key.StartsWith("eastl::hash_map<", StringComparison.Ordinal)), mapKeys);

        var eastlMapOverride = overrides[keys.Single(static key => key.StartsWith("eastl::hash_map<", StringComparison.Ordinal))]!;
        var templateParameterNames = ((System.Collections.IEnumerable)eastlMapOverride.GetType().GetProperty("TemplateParameterNames")!.GetValue(eastlMapOverride)!)
            .Cast<string>()
            .ToArray();
        Assert.Equal(new[] { "TKey", "TValue", "THash", "TPredicate", "TAllocator", "CacheHashCode" }, templateParameterNames);
    }

    [Fact]
    public void ExtractManagedTypeOverrides_reads_non_type_template_parameter_for_std_array()
    {
        var repoRoot = FindRepositoryRoot();
        var includeDir = Path.Combine(repoRoot, "include");
        var coreMarshallerHeader = Path.Combine(includeDir, "SharpCpp", "Marshaller.hpp");

        var extractorType = Type.GetType("SharpCpp.Parsing.MarshallerConfigExtractor, SharpCpp.Parsing", throwOnError: true)!;
        var method = extractorType.GetMethod("ExtractManagedTypeOverrides", BindingFlags.Public | BindingFlags.Static)!;

        var overrides = (System.Collections.IDictionary)method.Invoke(
            null,
            [new[] { coreMarshallerHeader }, CreateStandardLibraryClangArgs(repoRoot, includeDir)])!;

        var arrayOverride = overrides.Values.Cast<object>().Single(candidate =>
            ((string)candidate.GetType().GetProperty("CppTypeName")!.GetValue(candidate)!).StartsWith("std::array<", StringComparison.Ordinal));
        var templateParameterNames = ((System.Collections.IEnumerable)arrayOverride.GetType().GetProperty("TemplateParameterNames")!.GetValue(arrayOverride)!)
            .Cast<string>()
            .ToArray();

        Assert.Equal(new[] { "T", "N" }, templateParameterNames);
        Assert.True((bool)arrayOverride.GetType().GetProperty("FixedArray")!.GetValue(arrayOverride)!);
    }

    [Fact]
    public void ApplyRecordMetadata_uses_expose_member_entries_from_marshaller_configs()
    {
        var compilation = new SharpCpp.Model.Ast.BindingCompilation("TestProject", "Test.Namespace", "Bindings", "Test.Native", new[] { "MarshallerConfig.hpp" });
        var record = new SharpCpp.Model.Ast.RecordBinding("record:widget", "Widget", "Widget", "Widget");
        compilation.Members.Add(record);

        var extractorType = Type.GetType("SharpCpp.Parsing.MarshallerConfigExtractor, SharpCpp.Parsing", throwOnError: true)!;
        var exposedMemberType = extractorType.GetNestedType("ExposedMemberInfo", BindingFlags.NonPublic)!;
        var configType = extractorType.GetNestedType("MarshallerBodyConfig", BindingFlags.NonPublic)!;

        var exposedMemberCtor = exposedMemberType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == 6);
        var exposedParameters = Array.CreateInstance(typeof(ValueTuple<string, string>), 0);
        var exposedMember = exposedMemberCtor.Invoke(["Value", "int", true, "property", false, exposedParameters])!;

        var configCtor = configType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == 6);
        var exposedMembers = Array.CreateInstance(exposedMemberType, 1);
        exposedMembers.SetValue(exposedMember, 0);
        var genericParameters = Array.CreateInstance(typeof(string), 0);
        var config = configCtor.Invoke(["[[sharpcpp::managed_type(public_type = \"global::Test.Managed\")]]", exposedMembers, genericParameters, null, null, true])!;

        var applyMethod = Type.GetType("SharpCpp.Parsing.MarshallerMetadataPhase2, SharpCpp.Parsing", throwOnError: true)!
            .GetMethod("ApplyRecordMetadata", BindingFlags.NonPublic | BindingFlags.Static)!;
        var configs = (System.Collections.IDictionary)Activator.CreateInstance(
            typeof(Dictionary<,>).MakeGenericType(typeof(string), configType),
            StringComparer.Ordinal)!;
        configs["Widget"] = config;

        var recordsByNativeName = (System.Collections.IDictionary)Activator.CreateInstance(
            typeof(Dictionary<,>).MakeGenericType(typeof(string), typeof(SharpCpp.Model.Ast.RecordBinding)),
            StringComparer.Ordinal)!;
        recordsByNativeName[record.NativeName] = record;

        applyMethod.Invoke(null, [compilation, configs, recordsByNativeName]);

        var function = Assert.Single(record.Members.OfType<SharpCpp.Model.Ast.FunctionBinding>());
        Assert.Equal("Value", function.ManagedName);
        Assert.Equal("int", function.ReturnType.ManagedTypeName);
        Assert.True(function.IsConstMethod);
        Assert.Empty(function.Parameters);
    }

    [Fact]
    public void ApplyRecordMetadata_reads_static_interface_contracts()
    {
        var compilation = new SharpCpp.Model.Ast.BindingCompilation("TestProject", "Test.Namespace", "Bindings", "Test.Native", new[] { "MarshallerConfig.hpp" });
        var record = new SharpCpp.Model.Ast.RecordBinding("record:object", "Object", "Object", "Object");
        compilation.Members.Add(record);

        var extractorType = Type.GetType("SharpCpp.Parsing.MarshallerConfigExtractor, SharpCpp.Parsing", throwOnError: true)!;
        var exposedMemberType = extractorType.GetNestedType("ExposedMemberInfo", BindingFlags.NonPublic)!;
        var configType = extractorType.GetNestedType("MarshallerBodyConfig", BindingFlags.NonPublic)!;

        var configCtor = configType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == 6);
        var exposedMembers = Array.CreateInstance(exposedMemberType, 0);
        var genericParameters = Array.CreateInstance(typeof(string), 0);
        var config = configCtor.Invoke(["[[sharpcpp::managed_type(managed_static_interfaces = \"global::Example.IStaticTypeInfo<{managed_type}>; global::Example.IOther<{managed_type}>\", managed_static_interfaces_abstract = true)]]", exposedMembers, genericParameters, null, null, true])!;

        var applyMethod = Type.GetType("SharpCpp.Parsing.MarshallerMetadataPhase2, SharpCpp.Parsing", throwOnError: true)!
            .GetMethod("ApplyRecordMetadata", BindingFlags.NonPublic | BindingFlags.Static)!;
        var configs = (System.Collections.IDictionary)Activator.CreateInstance(
            typeof(Dictionary<,>).MakeGenericType(typeof(string), configType),
            StringComparer.Ordinal)!;
        configs["Object"] = config;

        var recordsByNativeName = (System.Collections.IDictionary)Activator.CreateInstance(
            typeof(Dictionary<,>).MakeGenericType(typeof(string), typeof(SharpCpp.Model.Ast.RecordBinding)),
            StringComparer.Ordinal)!;
        recordsByNativeName[record.NativeName] = record;

        applyMethod.Invoke(null, [compilation, configs, recordsByNativeName]);

        var interfaces = record.GetMetadata<IReadOnlyList<string>>(SharpCpp.Model.Ast.MetadataKeys.ManagedStaticInterfaces);
        Assert.Equal(new[] { "global::Example.IStaticTypeInfo<{managed_type}>", "global::Example.IOther<{managed_type}>" }, interfaces);
        Assert.True(record.GetMetadata<bool>(SharpCpp.Model.Ast.MetadataKeys.ManagedStaticInterfacesAbstract));
    }

    [Fact]
    public void ApplyRecordMetadata_leaves_synthetic_record_header_path_null()
    {
        var compilation = new SharpCpp.Model.Ast.BindingCompilation("TestProject", "Test.Namespace", "Bindings", "Test.Native", new[] { "MarshallerConfig.hpp" });

        var extractorType = Type.GetType("SharpCpp.Parsing.MarshallerConfigExtractor, SharpCpp.Parsing", throwOnError: true)!;
        var exposedMemberType = extractorType.GetNestedType("ExposedMemberInfo", BindingFlags.NonPublic)!;
        var configType = extractorType.GetNestedType("MarshallerBodyConfig", BindingFlags.NonPublic)!;

        var configCtor = configType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == 6);
        var exposedMembers = Array.CreateInstance(exposedMemberType, 0);
        var genericParameters = Array.CreateInstance(typeof(string), 0);
        var config = configCtor.Invoke(["[[sharpcpp::managed_type(managed_base_type = \"global::SharpCpp.Support.StringBase\")]]", exposedMembers, genericParameters, null, null, true])!;

        var applyMethod = Type.GetType("SharpCpp.Parsing.MarshallerMetadataPhase2, SharpCpp.Parsing", throwOnError: true)!
            .GetMethod("ApplyRecordMetadata", BindingFlags.NonPublic | BindingFlags.Static)!;
        var configs = (System.Collections.IDictionary)Activator.CreateInstance(
            typeof(Dictionary<,>).MakeGenericType(typeof(string), configType),
            StringComparer.Ordinal)!;
        configs["std::basic_string<char>"] = config;

        var recordsByNativeName = (System.Collections.IDictionary)Activator.CreateInstance(
            typeof(Dictionary<,>).MakeGenericType(typeof(string), typeof(SharpCpp.Model.Ast.RecordBinding)),
            StringComparer.Ordinal)!;

        applyMethod.Invoke(null, [compilation, configs, recordsByNativeName]);

        var record = Assert.Single(compilation.Members.OfType<SharpCpp.Model.Ast.RecordBinding>());
        Assert.Equal("std::basic_string", record.NativeName);
        Assert.Null(record.HeaderIncludePath);
    }

    [Fact]
    public void ApplyManagedTypeFormats_rebases_qualified_record_ids_to_resolved_record_symbol_id()
    {
        var compilation = new SharpCpp.Model.Ast.BindingCompilation("TestProject", "Test.Namespace", "Bindings", "Test.Native", Array.Empty<string>());
        var record = new SharpCpp.Model.Ast.RecordBinding("record:hash_map", "hash_map", "UnorderedMap", "eastl::hash_map")
        {
            ManagedTypeFormat = "Ea.UnorderedMap<{0}, {1}>",
            ManagedNamespace = "Ea"
        };
        record.GenericParameters.Add(new SharpCpp.Model.Ast.BindingGenericParameter("TKey", 0));
        record.GenericParameters.Add(new SharpCpp.Model.Ast.BindingGenericParameter("TValue", 1));
        compilation.Members.Add(record);

        var mapType = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "eastl::hash_map<int, float>*",
            "HashMap",
            "record:eastl::hash_map",
            genericArguments: [SharpCpp.Model.Ast.BindingType.Int32, SharpCpp.Model.Ast.BindingType.Single]);
        var function = new SharpCpp.Model.Ast.FunctionBinding(
            "function:GetMap",
            "GetMap",
            "GetMap",
            mapType,
            "GetMap",
            "eastl::hash_map<int, float>*",
            "GetMap");
        compilation.Members.Add(function);

        var applyMethod = Type.GetType("SharpCpp.Parsing.MarshallerMetadataPhase2, SharpCpp.Parsing", throwOnError: true)!
            .GetMethod("ApplyManagedTypeFormats", BindingFlags.NonPublic | BindingFlags.Static)!;
        var recordsByNativeName = new Dictionary<string, SharpCpp.Model.Ast.RecordBinding>(StringComparer.Ordinal)
        {
            [record.NativeName] = record
        };

        applyMethod.Invoke(null, [compilation, recordsByNativeName]);

        Assert.Equal(record.SymbolId, function.ReturnType.RecordId);
        Assert.Equal("Ea.UnorderedMap<int, float>", function.ReturnType.ManagedTypeName);
    }

    [Fact]
    public void ApplyManagedTypeFormats_preserves_record_value_cpp_type_when_rebinding_generic_arguments()
    {
        var compilation = new SharpCpp.Model.Ast.BindingCompilation("TestProject", "Test.Namespace", "Bindings", "Test.Native", Array.Empty<string>());
        var stringRecord = new SharpCpp.Model.Ast.RecordBinding("record:string", "basic_string", "String", "eastl::basic_string<char>")
        {
            ManagedTypeFormat = "Ea.String",
            ManagedNamespace = "Ea",
            ManagedBaseTypeName = "SharpCpp.Support.StringBase"
        };
        var variantRecord = new SharpCpp.Model.Ast.RecordBinding("record:variant", "Variant", "Variant", "Urho3D::Variant")
        {
            ManagedTypeFormat = "Urho3D.Core.Variant",
            ManagedNamespace = "Urho3D.Core",
            HasCopyConstructor = true
        };
        var mapRecord = new SharpCpp.Model.Ast.RecordBinding("record:hash_map", "hash_map", "UnorderedMap", "eastl::hash_map")
        {
            ManagedTypeFormat = "Ea.UnorderedMap<{0}, {1}>",
            ManagedNamespace = "Ea"
        };
        mapRecord.GenericParameters.Add(new SharpCpp.Model.Ast.BindingGenericParameter("TKey", 0));
        mapRecord.GenericParameters.Add(new SharpCpp.Model.Ast.BindingGenericParameter("TValue", 1));
        compilation.Members.Add(stringRecord);
        compilation.Members.Add(variantRecord);
        compilation.Members.Add(mapRecord);

        var stringType = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "eastl::basic_string<char>*",
            "String",
            "record:eastl::basic_string<char>");
        var variantType = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "Urho3D::Variant*",
            "Variant",
            "record:Urho3D::Variant");
        var mapType = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "eastl::hash_map<eastl::basic_string<char>, Urho3D::Variant>",
            "UnorderedMap",
            mapRecord.SymbolId,
            genericArguments: [stringType, variantType]);
        var function = new SharpCpp.Model.Ast.FunctionBinding(
            "function:GetMap",
            "GetMap",
            "GetMap",
            mapType,
            "GetMap",
            mapType.CppTypeName,
            "GetMap");
        compilation.Members.Add(function);

        var applyMethod = Type.GetType("SharpCpp.Parsing.MarshallerMetadataPhase2, SharpCpp.Parsing", throwOnError: true)!
            .GetMethod("ApplyManagedTypeFormats", BindingFlags.NonPublic | BindingFlags.Static)!;
        var recordsByNativeName = new Dictionary<string, SharpCpp.Model.Ast.RecordBinding>(StringComparer.Ordinal)
        {
            [stringRecord.CppQualifiedName] = stringRecord,
            [variantRecord.CppQualifiedName] = variantRecord,
            [mapRecord.NativeName] = mapRecord
        };

        applyMethod.Invoke(null, [compilation, recordsByNativeName]);

        Assert.Equal("Ea.UnorderedMap<Ea.String, Urho3D.Core.Variant>", function.ReturnType.ManagedTypeName);
        Assert.Equal("eastl::basic_string<char>", function.ReturnType.GenericArguments[0].CppTypeName);
        Assert.Equal("Urho3D::Variant", function.ReturnType.GenericArguments[1].CppTypeName);
    }

    [Fact]
    public void ApplyManagedTypeFormats_recovers_record_identity_from_native_template_argument()
    {
        var compilation = new SharpCpp.Model.Ast.BindingCompilation("TestProject", "Test.Namespace", "Bindings", "Test.Native", Array.Empty<string>());
        var stringRecord = new SharpCpp.Model.Ast.RecordBinding("record:string", "basic_string", "String", "eastl::basic_string<char>")
        {
            ManagedTypeFormat = "Ea.String",
            ManagedNamespace = "Ea"
        };
        var urhoVariantRecord = new SharpCpp.Model.Ast.RecordBinding("record:Urho3D::Variant", "Variant", "Variant", "Urho3D::Variant")
        {
            ManagedTypeFormat = "Urho3D.Core.Variant",
            ManagedNamespace = "Urho3D.Core"
        };
        var rmlVariantRecord = new SharpCpp.Model.Ast.RecordBinding("record:Rml::Variant", "Variant", "Variant", "Rml::Variant")
        {
            ManagedTypeFormat = "Urho3D.Core.Variant",
            ManagedNamespace = "Urho3D.Core"
        };
        var mapRecord = new SharpCpp.Model.Ast.RecordBinding("record:hash_map", "hash_map", "UnorderedMap", "eastl::hash_map")
        {
            ManagedTypeFormat = "Ea.UnorderedMap<{0}, {1}>",
            ManagedNamespace = "Ea"
        };
        mapRecord.GenericParameters.Add(new SharpCpp.Model.Ast.BindingGenericParameter("TKey", 0));
        mapRecord.GenericParameters.Add(new SharpCpp.Model.Ast.BindingGenericParameter("TValue", 1));
        compilation.Members.Add(stringRecord);
        compilation.Members.Add(urhoVariantRecord);
        compilation.Members.Add(rmlVariantRecord);
        compilation.Members.Add(mapRecord);

        var stringType = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "eastl::basic_string<char>*",
            "String",
            stringRecord.SymbolId);
        var rmlVariantTypeWithWrongRecordId = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "Rml::Variant*",
            "Variant",
            urhoVariantRecord.SymbolId);
        var mapType = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "eastl::hash_map<eastl::basic_string<char>, Rml::Variant>",
            "UnorderedMap",
            mapRecord.SymbolId,
            genericArguments: [stringType, rmlVariantTypeWithWrongRecordId]);
        var function = new SharpCpp.Model.Ast.FunctionBinding(
            "function:GetRmlMap",
            "GetRmlMap",
            "GetRmlMap",
            mapType,
            "GetRmlMap",
            mapType.CppTypeName,
            "GetRmlMap");
        compilation.Members.Add(function);

        var applyMethod = Type.GetType("SharpCpp.Parsing.MarshallerMetadataPhase2, SharpCpp.Parsing", throwOnError: true)!
            .GetMethod("ApplyManagedTypeFormats", BindingFlags.NonPublic | BindingFlags.Static)!;
        var recordsByNativeName = new Dictionary<string, SharpCpp.Model.Ast.RecordBinding>(StringComparer.Ordinal)
        {
            [stringRecord.CppQualifiedName] = stringRecord,
            [urhoVariantRecord.CppQualifiedName] = urhoVariantRecord,
            [rmlVariantRecord.CppQualifiedName] = rmlVariantRecord,
            [mapRecord.NativeName] = mapRecord
        };

        applyMethod.Invoke(null, [compilation, recordsByNativeName]);

        Assert.Equal("Rml::Variant*", function.ReturnType.GenericArguments[1].CppTypeName);
        Assert.Equal(rmlVariantRecord.SymbolId, function.ReturnType.GenericArguments[1].RecordId);
    }

    [Fact]
    public void ApplyManagedTypeFormats_recovers_record_value_cpp_type_from_configured_marshaller_metadata()
    {
        var compilation = new SharpCpp.Model.Ast.BindingCompilation("TestProject", "Test.Namespace", "Bindings", "Test.Native", Array.Empty<string>());
        var valueRecord = new SharpCpp.Model.Ast.RecordBinding(
            "record:value",
            "Value",
            "Value",
            "Test::Value",
            marshallerTypeName: "global::Test.__ValueMarshaller<{0}>");
        var vectorRecord = new SharpCpp.Model.Ast.RecordBinding("record:vector", "vector", "Vector", "eastl::vector")
        {
            ManagedTypeFormat = "Ea.Vector<{0}>",
            ManagedNamespace = "Ea"
        };
        vectorRecord.GenericParameters.Add(new SharpCpp.Model.Ast.BindingGenericParameter("T", 0));
        compilation.Members.Add(valueRecord);
        compilation.Members.Add(vectorRecord);

        var valueType = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "Test::Value*",
            "Value",
            valueRecord.SymbolId,
            marshallerTypeName: valueRecord.HandleMarshallerTypeName);
        var vectorType = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "eastl::vector<Test::Value>",
            "Vector",
            vectorRecord.SymbolId,
            genericArguments: [valueType]);
        var function = new SharpCpp.Model.Ast.FunctionBinding(
            "function:GetVector",
            "GetVector",
            "GetVector",
            vectorType,
            "GetVector",
            vectorType.CppTypeName,
            "GetVector");
        compilation.Members.Add(function);

        var applyMethod = Type.GetType("SharpCpp.Parsing.MarshallerMetadataPhase2, SharpCpp.Parsing", throwOnError: true)!
            .GetMethod("ApplyManagedTypeFormats", BindingFlags.NonPublic | BindingFlags.Static)!;
        var recordsByNativeName = new Dictionary<string, SharpCpp.Model.Ast.RecordBinding>(StringComparer.Ordinal)
        {
            [valueRecord.CppQualifiedName] = valueRecord,
            [vectorRecord.NativeName] = vectorRecord
        };

        applyMethod.Invoke(null, [compilation, recordsByNativeName]);

        Assert.Equal("Test::Value", function.ReturnType.GenericArguments[0].CppTypeName);
    }

    [Fact]
    public void ApplyManagedTypeFormats_keeps_handle_cpp_type_for_unconfigured_record_generic_arguments()
    {
        var compilation = new SharpCpp.Model.Ast.BindingCompilation("TestProject", "Test.Namespace", "Bindings", "Test.Native", Array.Empty<string>());
        var itemRecord = new SharpCpp.Model.Ast.RecordBinding("record:item", "Item", "Item", "Test::Item");
        var vectorRecord = new SharpCpp.Model.Ast.RecordBinding("record:vector", "vector", "Vector", "eastl::vector")
        {
            ManagedTypeFormat = "Ea.Vector<{0}>",
            ManagedNamespace = "Ea"
        };
        vectorRecord.GenericParameters.Add(new SharpCpp.Model.Ast.BindingGenericParameter("T", 0));
        compilation.Members.Add(itemRecord);
        compilation.Members.Add(vectorRecord);

        var itemType = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "Test::Item*",
            "Item",
            itemRecord.SymbolId);
        var vectorType = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "eastl::vector<Test::Item>",
            "Vector",
            vectorRecord.SymbolId,
            genericArguments: [itemType]);
        var function = new SharpCpp.Model.Ast.FunctionBinding(
            "function:GetVector",
            "GetVector",
            "GetVector",
            vectorType,
            "GetVector",
            vectorType.CppTypeName,
            "GetVector");
        compilation.Members.Add(function);

        var applyMethod = Type.GetType("SharpCpp.Parsing.MarshallerMetadataPhase2, SharpCpp.Parsing", throwOnError: true)!
            .GetMethod("ApplyManagedTypeFormats", BindingFlags.NonPublic | BindingFlags.Static)!;
        var recordsByNativeName = new Dictionary<string, SharpCpp.Model.Ast.RecordBinding>(StringComparer.Ordinal)
        {
            [itemRecord.CppQualifiedName] = itemRecord,
            [vectorRecord.NativeName] = vectorRecord
        };

        applyMethod.Invoke(null, [compilation, recordsByNativeName]);

        Assert.Equal("Test::Item*", function.ReturnType.GenericArguments[0].CppTypeName);
    }

    [Fact]
    public void ApplyManagedTypeFormats_keeps_closed_generic_argument_when_recovering_map_values()
    {
        var compilation = new SharpCpp.Model.Ast.BindingCompilation("TestProject", "Test.Namespace", "Bindings", "Test.Native", Array.Empty<string>());
        var stringHashRecord = new SharpCpp.Model.Ast.RecordBinding("record:string_hash", "StringHash", "StringHash", "Urho3D::StringHash")
        {
            ManagedTypeFormat = "Urho3D.Math.StringHash",
            ManagedNamespace = "Urho3D.Math"
        };
        var vectorRecord = new SharpCpp.Model.Ast.RecordBinding("record:vector", "vector", "Vector", "eastl::vector")
        {
            ManagedTypeFormat = "Ea.Vector<{0}>",
            ManagedNamespace = "Ea"
        };
        vectorRecord.GenericParameters.Add(new SharpCpp.Model.Ast.BindingGenericParameter("T", 0));
        var mapRecord = new SharpCpp.Model.Ast.RecordBinding("record:hash_map", "hash_map", "UnorderedMap", "eastl::hash_map")
        {
            ManagedTypeFormat = "Ea.UnorderedMap<{0}, {1}>",
            ManagedNamespace = "Ea"
        };
        mapRecord.GenericParameters.Add(new SharpCpp.Model.Ast.BindingGenericParameter("TKey", 0));
        mapRecord.GenericParameters.Add(new SharpCpp.Model.Ast.BindingGenericParameter("TValue", 1));
        compilation.Members.Add(stringHashRecord);
        compilation.Members.Add(vectorRecord);
        compilation.Members.Add(mapRecord);

        var keyType = SharpCpp.Model.Ast.BindingType.UInt32;
        var stringHashType = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "Urho3D::StringHash",
            "StringHash",
            stringHashRecord.SymbolId,
            cppIsRecordValue: true);
        var vectorType = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "eastl::vector<Urho3D::StringHash>",
            "Vector",
            vectorRecord.SymbolId,
            genericArguments: [stringHashType]);
        var mapType = SharpCpp.Model.Ast.BindingType.CreateHandle(
            "eastl::hash_map<unsigned int, eastl::vector<Urho3D::StringHash>>",
            "UnorderedMap",
            mapRecord.SymbolId,
            genericArguments: [keyType, vectorType]);
        var function = new SharpCpp.Model.Ast.FunctionBinding(
            "function:GetMap",
            "GetMap",
            "GetMap",
            mapType,
            "GetMap",
            mapType.CppTypeName,
            "GetMap");
        compilation.Members.Add(function);

        var applyMethod = Type.GetType("SharpCpp.Parsing.MarshallerMetadataPhase2, SharpCpp.Parsing", throwOnError: true)!
            .GetMethod("ApplyManagedTypeFormats", BindingFlags.NonPublic | BindingFlags.Static)!;
        var recordsByNativeName = new Dictionary<string, SharpCpp.Model.Ast.RecordBinding>(StringComparer.Ordinal)
        {
            [stringHashRecord.CppQualifiedName] = stringHashRecord,
            [vectorRecord.CppQualifiedName] = vectorRecord,
            [mapRecord.NativeName] = mapRecord
        };

        applyMethod.Invoke(null, [compilation, recordsByNativeName]);

        Assert.Equal("Ea.UnorderedMap<uint, Ea.Vector<Urho3D.Math.StringHash>>", function.ReturnType.ManagedTypeName);
        Assert.Single(function.ReturnType.GenericArguments[1].GenericArguments);
    }

    [Fact]
    public void ExtractMarshallerBodies_includes_inherited_expose_members_and_leaf_overrides()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureDir = Path.Combine(repoRoot, "tests", "SharpCpp.Tests.Model", "Fixtures", "InheritedMarshallerConfig");
        var headerPath = Path.Combine(fixtureDir, "InheritedMarshallerConfig.hpp");

        var extractorType = Type.GetType("SharpCpp.Parsing.MarshallerConfigExtractor, SharpCpp.Parsing", throwOnError: true)!;
        var method = extractorType.GetMethod("ExtractMarshallerBodies", BindingFlags.Public | BindingFlags.Static)!;

        var bodies = (System.Collections.IDictionary)method.Invoke(
            null,
            [new[] { headerPath }, new[] { "-x", "c++", "-std=c++20", $"-I{fixtureDir}" }])!;

        var body = bodies["widget"]!;
        var bodyType = body.GetType();
        var members = ((System.Collections.IEnumerable)bodyType.GetProperty("ExposedMembers")!.GetValue(body)!)
            .Cast<object>()
            .ToArray();

        Assert.Equal(2, members.Length);
        Assert.Contains(members, member => (string)member.GetType().GetProperty("MethodName")!.GetValue(member)! == "GetCountCore");
        var addCore = Assert.Single(members.Where(member => (string)member.GetType().GetProperty("MethodName")!.GetValue(member)! == "AddCore"));
        Assert.Equal("method", (string)addCore.GetType().GetProperty("Shape")!.GetValue(addCore)!);
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        var startPath = string.IsNullOrWhiteSpace(sourceFilePath)
            ? AppContext.BaseDirectory
            : Path.GetDirectoryName(sourceFilePath)!;
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            var hasIncludeDir = Directory.Exists(Path.Combine(directory.FullName, "include"));
            var hasModelTestsDir = Directory.Exists(Path.Combine(directory.FullName, "tests", "SharpCpp.Tests.Model"));
            if (hasIncludeDir &&
                (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) || hasModelTestsDir))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private static string[] CreateStandardLibraryClangArgs(params string[] includeDirs)
    {
        var args = new List<string> { "-x", "c++", "-std=c++20" };
        args.AddRange(includeDirs.Select(static includeDir => $"-I{includeDir}"));

        args.AddRange(EnumerateSystemIncludeDirs().Select(static includeDir => $"-I{includeDir}"));
        return args.ToArray();
    }

    private static IEnumerable<string> EnumerateSystemIncludeDirs()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (Directory.Exists("/usr/include/c++"))
        {
            foreach (var versionDir in Directory.GetDirectories("/usr/include/c++").OrderByDescending(static path => path, StringComparer.Ordinal))
            {
                foreach (var candidate in new[]
                {
                    versionDir,
                    Path.Combine(versionDir, "x86_64-pc-linux-gnu"),
                    Path.Combine(versionDir, "backward")
                })
                {
                    if (Directory.Exists(candidate) && seen.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
        }

        if (Directory.Exists("/usr/lib/gcc/x86_64-pc-linux-gnu"))
        {
            foreach (var versionDir in Directory.GetDirectories("/usr/lib/gcc/x86_64-pc-linux-gnu").OrderByDescending(static path => path, StringComparer.Ordinal))
            {
                var includeDir = Path.Combine(versionDir, "include");
                if (Directory.Exists(includeDir) && seen.Add(includeDir))
                {
                    yield return includeDir;
                }
            }
        }

        if (Directory.Exists("/usr/lib/clang"))
        {
            foreach (var versionDir in Directory.GetDirectories("/usr/lib/clang").OrderByDescending(static path => path, StringComparer.Ordinal))
            {
                var includeDir = Path.Combine(versionDir, "include");
                if (Directory.Exists(includeDir) && seen.Add(includeDir))
                {
                    yield return includeDir;
                }
            }
        }

        if (Directory.Exists("/usr/include") && seen.Add("/usr/include"))
        {
            yield return "/usr/include";
        }
    }
}
