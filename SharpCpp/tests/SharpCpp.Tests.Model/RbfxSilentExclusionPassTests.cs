using SharpCpp.Model.Ast;
using SharpCpp.Rbfx.Generator;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class RbfxSilentExclusionPassTests
{
    [Fact]
    public void Apply_silently_excludes_non_urho3d_declarations()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var external = new RecordBinding("record:ea::vector", "vector", "Vector", "ea::vector");
        compilation.Members.Add(external);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(1, count);
        Assert.True(external.IsIgnored());
        Assert.True(external.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_keeps_configured_non_urho3d_records()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var stringView = new RecordBinding("record:ea::string_view", "string_view", "ReadOnlySpan", "ea::string_view")
        {
            ManagedTypeFormat = "global::System.ReadOnlySpan<byte>"
        };
        var member = new FunctionBinding("method:ea::string_view:size", "size", "Size", BindingType.UInt64, cppQualifiedName: "ea::string_view::size");
        stringView.Members.Add(member);
        compilation.Members.Add(stringView);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(0, count);
        Assert.False(stringView.IsIgnored());
        Assert.False(stringView.IsSilentlyExcluded());
        Assert.False(member.IsIgnored());
    }

    [Fact]
    public void Apply_silently_excludes_detail_parent_namespaces()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var record = new RecordBinding("record:Urho3D::detail::Hidden", "Hidden", "Hidden", "Urho3D::detail::Hidden");
        var enumBinding = new EnumBinding("enum:Urho3D::Detail::Mode", "Mode", "Mode", cppQualifiedName: "Urho3D::Detail::Mode");
        var function = new FunctionBinding("function:Urho3D::detail::MakeHidden", "MakeHidden", "MakeHidden", BindingType.Int32, cppQualifiedName: "Urho3D::detail::MakeHidden");
        compilation.Members.Add(record);
        compilation.Members.Add(enumBinding);
        compilation.Members.Add(function);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(3, count);
        Assert.True(record.IsSilentlyExcluded());
        Assert.True(enumBinding.IsSilentlyExcluded());
        Assert.True(function.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_silently_excludes_configured_detail_records()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var record = new RecordBinding("record:Urho3D::detail::Configured", "Configured", "Configured", "Urho3D::detail::Configured")
        {
            ManagedTypeFormat = "Urho3D.Configured"
        };
        compilation.Members.Add(record);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(1, count);
        Assert.True(record.IsIgnored());
        Assert.True(record.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_silently_excludes_custom_smart_pointer_wrapper_records()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var sharedPtr = new RecordBinding("record:Urho3D::SharedPtr", "SharedPtr", "SharedPtr", "Urho3D::SharedPtr")
        {
            ManagedTypeFormat = "Urho3D.SharedPtr<{0}>"
        };
        var weakPtr = new RecordBinding("record:Urho3D::WeakPtr", "WeakPtr", "WeakPtr", "Urho3D::WeakPtr")
        {
            ManagedTypeFormat = "Urho3D.WeakPtr<{0}>"
        };
        compilation.Members.Add(sharedPtr);
        compilation.Members.Add(weakPtr);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(2, count);
        Assert.True(sharedPtr.IsSilentlyExcluded());
        Assert.True(weakPtr.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_silently_excludes_custom_smart_pointer_deduction_guides()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var sharedPtr = new FunctionBinding("function:Urho3D::SharedPtr", "deduced", "SharedPtr", BindingType.Void, cppQualifiedName: "Urho3D::PtrGuide");
        sharedPtr.SetMetadata(MetadataKeys.DeclarationDisplayText, "template <class InterfaceType> SharedPtr() -> SharedPtr<InterfaceType>");
        var weakPtr = new FunctionBinding("function:Urho3D::WeakPtr", "deduced", "WeakPtr", BindingType.Void, cppQualifiedName: "Urho3D::PtrGuide");
        weakPtr.SetMetadata(MetadataKeys.DeclarationDisplayText, "template <class InterfaceType> WeakPtr() -> WeakPtr<InterfaceType>");
        compilation.Members.Add(sharedPtr);
        compilation.Members.Add(weakPtr);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(2, count);
        Assert.True(sharedPtr.IsSilentlyExcluded());
        Assert.True(weakPtr.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_silently_excludes_custom_smart_pointer_record_members()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var record = new RecordBinding("record:Urho3D::SharedPtr", "SharedPtr", "SharedPtr", "Urho3D::SharedPtr");
        var constructor = new FunctionBinding("method:Urho3D::SharedPtr::SharedPtr", "SharedPtr", "SharedPtr", BindingType.Void, cppQualifiedName: "Urho3D::SharedPtr<InterfaceType, RefCountedType>");
        record.Members.Add(constructor);
        compilation.Members.Add(record);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(2, count);
        Assert.True(record.IsSilentlyExcluded());
        Assert.True(constructor.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_silently_excludes_templated_smart_pointer_record_names()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var record = new RecordBinding(
            "record:Urho3D::SharedPtr<InterfaceType, RefCountedType>",
            "SharedPtr",
            "SharedPtr",
            "Urho3D::SharedPtr<InterfaceType, RefCountedType>");
        compilation.Members.Add(record);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(1, count);
        Assert.True(record.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_silently_excludes_flagset_only_operators()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var flagSetRecord = new RecordBinding("record:Urho3D::FlagSet", "FlagSet", "FlagSet", "Urho3D::FlagSet")
        {
            HeaderIncludePath = "Container/FlagSet.h"
        };
        var flagSetType = BindingType.CreateHandle("Urho3D::FlagSet<AttributeMode>", "FlagSet", recordId: flagSetRecord.SymbolId);
        var enumType = BindingType.CreateEnum("AttributeMode", "AttributeMode");
        var bitwiseAnd = new FunctionBinding("function:operator&", "operator&", "OperatorAnd", flagSetType, cppQualifiedName: "Urho3D::operator&")
        {
            OperatorToken = "&"
        };
        bitwiseAnd.SetMetadata(MetadataKeys.DeclarationDisplayText, "inline constexpr Urho3D::FlagSet<AttributeMode> operator&(const AttributeMode lhs, const AttributeMode rhs)");
        bitwiseAnd.Parameters.Add(new ParameterBinding("parameter:operator&:lhs", "lhs", "lhs", enumType, cppTypeName: "const AttributeMode"));
        bitwiseAnd.Parameters.Add(new ParameterBinding("parameter:operator&:rhs", "rhs", "rhs", enumType, cppTypeName: "const AttributeMode"));
        var bitwiseXor = new FunctionBinding("function:operator^", "operator^", "OperatorXor", flagSetType, cppQualifiedName: "Urho3D::operator^")
        {
            OperatorToken = "^"
        };
        bitwiseXor.SetMetadata(MetadataKeys.DeclarationDisplayText, "inline constexpr Urho3D::FlagSet<AttributeMode> operator^(const AttributeMode lhs, const AttributeMode rhs)");
        bitwiseXor.Parameters.Add(new ParameterBinding("parameter:operator^:lhs", "lhs", "lhs", enumType, cppTypeName: "const AttributeMode"));
        bitwiseXor.Parameters.Add(new ParameterBinding("parameter:operator^:rhs", "rhs", "rhs", enumType, cppTypeName: "const AttributeMode"));
        var bitwiseOr = new FunctionBinding("function:operator|", "operator|", "OperatorOr", flagSetType, cppQualifiedName: "Urho3D::operator|")
        {
            OperatorToken = "|"
        };
        bitwiseOr.SetMetadata(MetadataKeys.DeclarationDisplayText, "inline constexpr Urho3D::FlagSet<AttributeMode> operator|(const AttributeMode lhs, const AttributeMode rhs)");
        bitwiseOr.Parameters.Add(new ParameterBinding("parameter:operator|:lhs", "lhs", "lhs", enumType, cppTypeName: "const AttributeMode"));
        bitwiseOr.Parameters.Add(new ParameterBinding("parameter:operator|:rhs", "rhs", "rhs", enumType, cppTypeName: "const AttributeMode"));
        var complement = new FunctionBinding("function:operator~", "operator~", "OperatorNot", flagSetType, cppQualifiedName: "Urho3D::operator~")
        {
            OperatorToken = "~"
        };
        complement.SetMetadata(MetadataKeys.DeclarationDisplayText, "inline constexpr Urho3D::FlagSet<AttributeMode> operator~(const AttributeMode rhs)");
        complement.Parameters.Add(new ParameterBinding("parameter:operator~:rhs", "rhs", "rhs", enumType, cppTypeName: "const AttributeMode"));
        compilation.Members.Add(flagSetRecord);
        compilation.Members.Add(bitwiseAnd);
        compilation.Members.Add(bitwiseXor);
        compilation.Members.Add(bitwiseOr);
        compilation.Members.Add(complement);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(5, count);
        Assert.True(flagSetRecord.IsSilentlyExcluded());
        Assert.True(bitwiseAnd.IsSilentlyExcluded());
        Assert.True(bitwiseXor.IsSilentlyExcluded());
        Assert.True(bitwiseOr.IsSilentlyExcluded());
        Assert.True(complement.IsSilentlyExcluded());
        Assert.Contains("FlagSet operator", bitwiseAnd.GetIgnoreReason(), StringComparison.Ordinal);
        Assert.Contains("FlagSet operator", bitwiseXor.GetIgnoreReason(), StringComparison.Ordinal);
        Assert.Contains("FlagSet operator", bitwiseOr.GetIgnoreReason(), StringComparison.Ordinal);
        Assert.Contains("FlagSet operator", complement.GetIgnoreReason(), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_keeps_non_flagset_bitwise_operators()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var bitwiseOr = new FunctionBinding("function:operator|", "operator|", "OperatorOr", BindingType.UInt32, cppQualifiedName: "Urho3D::operator|")
        {
            OperatorToken = "|"
        };
        bitwiseOr.Parameters.Add(new ParameterBinding("parameter:operator|:lhs", "lhs", "lhs", BindingType.UInt32));
        bitwiseOr.Parameters.Add(new ParameterBinding("parameter:operator|:rhs", "rhs", "rhs", BindingType.UInt32));
        compilation.Members.Add(bitwiseOr);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(0, count);
        Assert.False(bitwiseOr.IsIgnored());
    }

    [Fact]
    public void Apply_silently_excludes_blocked_header_path_variants()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var relative = new RecordBinding("record:relative", "Relative", "Relative", "Urho3D::Relative")
        {
            HeaderIncludePath = "Container/Ptr.h"
        };
        var prefixed = new RecordBinding("record:prefixed", "Prefixed", "Prefixed", "Urho3D::Prefixed")
        {
            HeaderIncludePath = "Urho3D/Container/Ptr.h"
        };
        var absolute = new RecordBinding("record:absolute", "Absolute", "Absolute", "Urho3D::Absolute")
        {
            HeaderIncludePath = "/opt/rbfx/include/Urho3D/Container/Ptr.h"
        };
        compilation.Members.Add(relative);
        compilation.Members.Add(prefixed);
        compilation.Members.Add(absolute);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(3, count);
        Assert.True(relative.IsSilentlyExcluded());
        Assert.True(prefixed.IsSilentlyExcluded());
        Assert.True(absolute.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_silently_excludes_members_by_parent_blocked_header()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var record = new RecordBinding("record:Urho3D::PtrOwner", "PtrOwner", "PtrOwner", "Urho3D::PtrOwner")
        {
            HeaderIncludePath = "Container/Ptr.h"
        };
        var method = new FunctionBinding("method:Urho3D::PtrOwner::Get", "Get", "Get", BindingType.Int32, cppQualifiedName: "Urho3D::PtrOwner::Get");
        var field = new FieldBinding("field:Urho3D::PtrOwner::value", "value", "Value", BindingType.Int32, "int", "Urho3D::PtrOwner");
        record.Members.Add(method);
        record.Members.Add(field);
        compilation.Members.Add(record);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(3, count);
        Assert.Null(method.HeaderIncludePath);
        Assert.Null(field.HeaderIncludePath);
        Assert.True(record.IsSilentlyExcluded());
        Assert.True(method.IsSilentlyExcluded());
        Assert.True(field.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_keeps_same_names_in_non_blocked_header()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var record = new RecordBinding("record:Urho3D::RefCounted", "RefCounted", "RefCounted", "Urho3D::RefCounted")
        {
            HeaderIncludePath = "Core/Object.h"
        };
        var function = new FunctionBinding("function:Urho3D::CreateRefCounted", "CreateRefCounted", "CreateRefCounted", BindingType.Int32, cppQualifiedName: "Urho3D::CreateRefCounted")
        {
            HeaderIncludePath = "Core/Object.h"
        };
        compilation.Members.Add(record);
        compilation.Members.Add(function);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(0, count);
        Assert.False(record.IsIgnored());
        Assert.False(function.IsIgnored());
    }

    [Fact]
    public void Apply_keeps_ref_counted_header_definitions()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var record = new RecordBinding("record:Urho3D::RefCounted", "RefCounted", "RefCounted", "Urho3D::RefCounted")
        {
            HeaderIncludePath = "Urho3D/Container/RefCounted.h"
        };
        compilation.Members.Add(record);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(0, count);
        Assert.False(record.IsIgnored());
    }

    [Fact]
    public void Apply_prunes_alias_targeting_blocked_record()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var record = new RecordBinding("record:Urho3D::Mutex", "Mutex", "Mutex", "Urho3D::Mutex")
        {
            HeaderIncludePath = "Core/Assert.h"
        };
        compilation.Members.Add(record);
        compilation.TypeAliases.Add(new BindingTypeAlias(
            "Urho3D::MutexAlias",
            "MutexAlias",
            "MutexAlias",
            BindingType.CreateHandle("Urho3D::Mutex*", "Mutex", record.SymbolId)));

        _ = RbfxSilentExclusionPass.Apply(compilation);

        Assert.True(record.IsSilentlyExcluded());
        Assert.Empty(compilation.TypeAliases);
    }

    [Fact]
    public void Apply_drops_ignored_base_from_retained_derived_record()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var baseRecord = new RecordBinding("record:Urho3D::Mutex", "Mutex", "Mutex", "Urho3D::Mutex")
        {
            HeaderIncludePath = "Core/Assert.h",
            ManagedBaseTypeName = "global::SharpCpp.Support.NativeObject"
        };
        var derived = new RecordBinding("record:Urho3D::Object", "Object", "Object", "Urho3D::Object")
        {
            HeaderIncludePath = "Core/Object.h",
            ManagedBaseTypeName = "Mutex"
        };
        var secondaryBaseRecord = new RecordBinding("record:Urho3D::Thread", "Thread", "Thread", "Urho3D::Thread")
        {
            HeaderIncludePath = "Core/Thread.h"
        };
        var overrideMethod = new FunctionBinding("method:Urho3D::Object::GetName", "GetName", "GetName", BindingType.Int32, cppQualifiedName: "Urho3D::Object::GetName")
        {
            IsOverride = true,
            IsVirtual = true
        };
        derived.Bases.Add(new RecordBaseBinding(baseRecord.SymbolId, baseRecord.NativeName, baseRecord.ManagedName, baseRecord.CppQualifiedName, isVirtual: false));
        derived.Bases.Add(new RecordBaseBinding(secondaryBaseRecord.SymbolId, secondaryBaseRecord.NativeName, secondaryBaseRecord.ManagedName, secondaryBaseRecord.CppQualifiedName, isVirtual: false));
        derived.Members.Add(overrideMethod);
        compilation.Members.Add(baseRecord);
        compilation.Members.Add(secondaryBaseRecord);
        compilation.Members.Add(derived);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(1, count);
        Assert.True(baseRecord.IsSilentlyExcluded());
        Assert.False(derived.IsIgnored());
        Assert.Single(derived.Bases);
        Assert.Equal(secondaryBaseRecord.SymbolId, derived.Bases[0].RecordId);
        Assert.Equal("Thread", derived.ManagedBaseTypeName);
        Assert.True(overrideMethod.IsOverride);
        Assert.True(overrideMethod.IsVirtual);
    }

    [Fact]
    public void Apply_keeps_non_ignored_suppress_emission_base()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var baseRecord = new RecordBinding("record:Urho3D::Handwritten", "Handwritten", "Handwritten", "Urho3D::Handwritten")
        {
            HeaderIncludePath = "Core/Object.h"
        };
        baseRecord.SetMetadata(MetadataKeys.SuppressEmission, true);
        var derived = new RecordBinding("record:Urho3D::Derived", "Derived", "Derived", "Urho3D::Derived")
        {
            HeaderIncludePath = "Core/Object.h",
            ManagedBaseTypeName = "Handwritten"
        };
        derived.Bases.Add(new RecordBaseBinding(baseRecord.SymbolId, baseRecord.NativeName, baseRecord.ManagedName, baseRecord.CppQualifiedName, isVirtual: false));
        compilation.Members.Add(baseRecord);
        compilation.Members.Add(derived);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(0, count);
        Assert.False(baseRecord.IsIgnored());
        Assert.Single(derived.Bases);
        Assert.Equal("Handwritten", derived.ManagedBaseTypeName);
    }

    [Fact]
    public void Apply_returns_transition_only_count_on_repeated_apply()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var record = new RecordBinding("record:Urho3D::Mutex", "Mutex", "Mutex", "Urho3D::Mutex")
        {
            HeaderIncludePath = "Core/Assert.h"
        };
        compilation.Members.Add(record);

        var firstCount = RbfxSilentExclusionPass.Apply(compilation);
        var secondCount = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(1, firstCount);
        Assert.Equal(0, secondCount);
        Assert.True(record.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_non_silently_ignores_functions_referencing_header_blocked_types()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var mutex = new RecordBinding("record:Urho3D::Mutex", "Mutex", "Mutex", "Urho3D::Mutex")
        {
            HeaderIncludePath = "Core/Assert.h"
        };
        var returnMutex = new FunctionBinding(
            "function:Urho3D::GetMutex",
            "GetMutex",
            "GetMutex",
            BindingType.CreateHandle("Urho3D::Mutex*", "Mutex", mutex.SymbolId),
            cppQualifiedName: "Urho3D::GetMutex")
        {
            HeaderIncludePath = "Core/Object.h"
        };
        var takeMutex = new FunctionBinding("function:Urho3D::SetMutex", "SetMutex", "SetMutex", BindingType.Void, cppQualifiedName: "Urho3D::SetMutex")
        {
            HeaderIncludePath = "Core/Object.h"
        };
        takeMutex.Parameters.Add(new ParameterBinding("parameter:Urho3D::SetMutex:mutex", "mutex", "mutex", BindingType.CreateHandle("Urho3D::Mutex*", "Mutex", mutex.SymbolId)));
        compilation.Members.Add(mutex);
        compilation.Members.Add(returnMutex);
        compilation.Members.Add(takeMutex);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(1, count);
        Assert.True(mutex.IsSilentlyExcluded());
        Assert.True(returnMutex.IsIgnored());
        Assert.False(returnMutex.IsSilentlyExcluded());
        Assert.True(takeMutex.IsIgnored());
        Assert.False(takeMutex.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_non_silently_ignores_function_referencing_blocked_type_by_cpp_name_without_id()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var mutex = new RecordBinding("record:Urho3D::Mutex", "Mutex", "Mutex", "Urho3D::Mutex")
        {
            HeaderIncludePath = "Core/Assert.h"
        };
        var returnMutex = new FunctionBinding(
            "function:Urho3D::GetMutexNoId",
            "GetMutexNoId",
            "GetMutexNoId",
            BindingType.CreateHandle("const class Urho3D::Mutex*", "Mutex"),
            cppQualifiedName: "Urho3D::GetMutexNoId")
        {
            HeaderIncludePath = "Core/Object.h"
        };
        compilation.Members.Add(mutex);
        compilation.Members.Add(returnMutex);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(1, count);
        Assert.True(mutex.IsSilentlyExcluded());
        Assert.True(returnMutex.IsIgnored());
        Assert.False(returnMutex.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_non_silently_ignores_blocked_enum_dependencies()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var enumBinding = new EnumBinding("enum:Urho3D::BlockedMode", "BlockedMode", "BlockedMode", cppQualifiedName: "Urho3D::BlockedMode")
        {
            HeaderIncludePath = "Core/Assert.h"
        };
        var byId = new FunctionBinding(
            "function:Urho3D::UseBlockedModeById",
            "UseBlockedModeById",
            "UseBlockedModeById",
            BindingType.CreateEnum("Urho3D::BlockedMode", "BlockedMode", enumId: enumBinding.SymbolId),
            cppQualifiedName: "Urho3D::UseBlockedModeById")
        {
            HeaderIncludePath = "Core/Object.h"
        };
        var byCppName = new FunctionBinding(
            "function:Urho3D::UseBlockedModeByCppName",
            "UseBlockedModeByCppName",
            "UseBlockedModeByCppName",
            BindingType.CreateEnum("enum Urho3D::BlockedMode", "BlockedMode"),
            cppQualifiedName: "Urho3D::UseBlockedModeByCppName")
        {
            HeaderIncludePath = "Core/Object.h"
        };
        compilation.Members.Add(enumBinding);
        compilation.Members.Add(byId);
        compilation.Members.Add(byCppName);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(1, count);
        Assert.True(enumBinding.IsSilentlyExcluded());
        Assert.True(byId.IsIgnored());
        Assert.False(byId.IsSilentlyExcluded());
        Assert.True(byCppName.IsIgnored());
        Assert.False(byCppName.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_preserves_existing_dependent_ignore_reason()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var mutex = new RecordBinding("record:Urho3D::Mutex", "Mutex", "Mutex", "Urho3D::Mutex")
        {
            HeaderIncludePath = "Core/Assert.h"
        };
        var function = new FunctionBinding(
            "function:Urho3D::GetMutexWithExistingReason",
            "GetMutexWithExistingReason",
            "GetMutexWithExistingReason",
            BindingType.CreateHandle("Urho3D::Mutex*", "Mutex", mutex.SymbolId),
            cppQualifiedName: "Urho3D::GetMutexWithExistingReason")
        {
            HeaderIncludePath = "Core/Object.h"
        };
        function.SetIgnoreReason("existing reason");
        compilation.Members.Add(mutex);
        compilation.Members.Add(function);

        _ = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal("existing reason", function.GetIgnoreReason());
        Assert.False(function.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_non_silently_ignores_fields_referencing_header_blocked_types()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var mutex = new RecordBinding("record:Urho3D::Mutex", "Mutex", "Mutex", "Urho3D::Mutex")
        {
            HeaderIncludePath = "Core/Assert.h"
        };
        var owner = new RecordBinding("record:Urho3D::Owner", "Owner", "Owner", "Urho3D::Owner")
        {
            HeaderIncludePath = "Core/Object.h"
        };
        var field = new FieldBinding(
            "field:Urho3D::Owner::refCounted",
            "refCounted",
            "RefCounted",
            BindingType.CreateHandle("Urho3D::Mutex*", "Mutex", mutex.SymbolId),
            "Urho3D::Mutex*",
            "Urho3D::Owner");
        owner.Members.Add(field);
        compilation.Members.Add(mutex);
        compilation.Members.Add(owner);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(1, count);
        Assert.True(mutex.IsSilentlyExcluded());
        Assert.False(owner.IsIgnored());
        Assert.True(field.IsIgnored());
        Assert.False(field.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_prunes_recursive_generic_alias_targeting_blocked_type_but_keeps_other_ignored_aliases()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var mutex = new RecordBinding("record:Urho3D::Mutex", "Mutex", "Mutex", "Urho3D::Mutex")
        {
            HeaderIncludePath = "Core/Assert.h"
        };
        var detail = new RecordBinding("record:Urho3D::detail::Hidden", "Hidden", "Hidden", "Urho3D::detail::Hidden");
        compilation.Members.Add(mutex);
        compilation.Members.Add(detail);
        var prunedAlias = new BindingTypeAlias(
            "Urho3D::VectorMutex",
            "VectorMutex",
            "VectorMutex",
            BindingType.CreateHandle(
                "Urho3D::Vector<Urho3D::Mutex>",
                "Vector",
                genericArguments: [BindingType.CreateHandle("Urho3D::Mutex*", "Mutex", mutex.SymbolId)]));
        var retainedAlias = new BindingTypeAlias(
            "Urho3D::HiddenAlias",
            "HiddenAlias",
            "HiddenAlias",
            BindingType.CreateHandle("Urho3D::detail::Hidden*", "Hidden", detail.SymbolId));
        compilation.TypeAliases.Add(prunedAlias);
        compilation.TypeAliases.Add(retainedAlias);

        _ = RbfxSilentExclusionPass.Apply(compilation);

        Assert.True(mutex.IsSilentlyExcluded());
        Assert.True(detail.IsSilentlyExcluded());
        Assert.DoesNotContain(prunedAlias, compilation.TypeAliases);
        Assert.Contains(retainedAlias, compilation.TypeAliases);
    }

    [Fact]
    public void Apply_prunes_alias_targeting_blocked_type_by_cpp_name_without_id()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var mutex = new RecordBinding("record:Urho3D::Mutex", "Mutex", "Mutex", "Urho3D::Mutex")
        {
            HeaderIncludePath = "Core/Assert.h"
        };
        var alias = new BindingTypeAlias(
            "Urho3D::MutexNoIdAlias",
            "MutexNoIdAlias",
            "MutexNoIdAlias",
            BindingType.CreateHandle("struct Urho3D::Mutex&", "Mutex"));
        compilation.Members.Add(mutex);
        compilation.TypeAliases.Add(alias);

        _ = RbfxSilentExclusionPass.Apply(compilation);

        Assert.True(mutex.IsSilentlyExcluded());
        Assert.DoesNotContain(alias, compilation.TypeAliases);
    }

    [Fact]
    public void Apply_ignores_projected_methods_from_dropped_header_blocked_base()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var baseRecord = new RecordBinding("record:Urho3D::Mutex", "Mutex", "Mutex", "Urho3D::Mutex")
        {
            HeaderIncludePath = "Core/Assert.h"
        };
        var derived = new RecordBinding("record:Urho3D::Object", "Object", "Object", "Urho3D::Object")
        {
            HeaderIncludePath = "Core/Object.h"
        };
        derived.Bases.Add(new RecordBaseBinding(baseRecord.SymbolId, baseRecord.NativeName, baseRecord.ManagedName, baseRecord.CppQualifiedName, isVirtual: true));
        var projected = new FunctionBinding(
            "projected-secondary:Urho3D::Object:Urho3D::Mutex:Lock",
            "AddRef",
            "AddRef",
            BindingType.Void,
            cppQualifiedName: "Urho3D::Object::AddRef")
        {
            NativeDispatchRecordCppTypeName = "Urho3D::Mutex"
        };
        derived.Members.Add(projected);
        compilation.Members.Add(baseRecord);
        compilation.Members.Add(derived);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(1, count);
        Assert.True(baseRecord.IsSilentlyExcluded());
        Assert.Empty(derived.Bases);
        Assert.True(projected.IsIgnored());
        Assert.False(projected.IsSilentlyExcluded());
    }

    [Fact]
    public void Apply_does_not_ignore_configured_external_record_by_ancestor_header_fallback()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var owner = new RecordBinding("record:Urho3D::PtrOwner", "PtrOwner", "PtrOwner", "Urho3D::PtrOwner")
        {
            HeaderIncludePath = "Container/Ptr.h"
        };
        var external = new RecordBinding("record:ea::string_view", "string_view", "ReadOnlySpan", "ea::string_view")
        {
            ManagedTypeFormat = "global::System.ReadOnlySpan<byte>"
        };
        owner.Members.Add(external);
        compilation.Members.Add(owner);

        var count = RbfxSilentExclusionPass.Apply(compilation);

        Assert.Equal(1, count);
        Assert.True(owner.IsSilentlyExcluded());
        Assert.False(external.IsIgnored());
    }
}
