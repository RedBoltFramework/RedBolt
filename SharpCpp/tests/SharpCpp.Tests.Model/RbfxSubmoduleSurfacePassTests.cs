using SharpCpp.Model.Ast;
using SharpCpp.Rbfx.Generator;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class RbfxSubmoduleSurfacePassTests
{
    [Fact]
    public void Rml_pass_prunes_public_record_methods_with_non_public_rml_types()
    {
        var compilation = CreateCompilation();
        var element = new RecordBinding("record:Rml::Element", "Element", "Element", "Rml::Element")
        {
            HeaderIncludePath = "RmlUi/Core/Element.h"
        };
        var method = new FunctionBinding(
            "method:Rml::Element::GetDisplay",
            "GetDisplay",
            "GetDisplay",
            BindingType.CreateEnum("Style::Display", "Display"),
            cppReturnTypeName: "Style::Display",
            cppQualifiedName: "Rml::Element::GetDisplay")
        {
            IsStatic = false
        };
        element.Members.Add(method);
        compilation.Members.Add(element);

        RbfxRmlUiSurfacePass.Apply(compilation);

        Assert.True(method.IsIgnored());
        Assert.True(method.IsSilentlyExcluded());
    }

    [Fact]
    public void Rml_pass_removes_generic_instantiations_with_non_public_rml_types()
    {
        var compilation = CreateCompilation();
        var map = new RecordBinding("record:eastl::hash_map", "hash_map", "UnorderedMap", "eastl::hash_map");
        map.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding(
        [
            BindingType.CreateEnum("PropertyId", "PropertyId"),
            BindingType.CreateHandle("Rml::Property*", "Property", recordId: "record:Rml::Property")
        ]));
        compilation.Members.Add(map);

        RbfxRmlUiSurfacePass.Apply(compilation);

        Assert.Empty(map.SupportedGenericInstantiations);
    }

    [Fact]
    public void Rml_pass_removes_non_public_rml_bases_and_aliases()
    {
        var compilation = CreateCompilation();
        var element = new RecordBinding("record:Rml::Element", "Element", "Element", "Rml::Element")
        {
            HeaderIncludePath = "RmlUi/Core/Element.h",
            ManagedBaseTypeName = "ScriptInterface"
        };
        element.Bases.Add(new RecordBaseBinding("record:Rml::ScriptInterface", "ScriptInterface", "ScriptInterface", "Rml::ScriptInterface", isVirtual: false));
        compilation.Members.Add(element);
        compilation.TypeAliases.Add(new BindingTypeAlias(
            "Rml::Units",
            "Units",
            "Units",
            BindingType.CreateEnum("Rml::Unit", "Unit")));

        RbfxRmlUiSurfacePass.Apply(compilation);

        Assert.Empty(element.Bases);
        Assert.Null(element.ManagedBaseTypeName);
        Assert.Empty(compilation.TypeAliases);
    }

    [Fact]
    public void Rml_pass_keeps_data_model_records_for_application_sample()
    {
        var compilation = CreateCompilation();
        var records = new[]
        {
            "Rml::Context",
            "Rml::DataModelConstructor",
            "Rml::DataModelHandle",
            "Rml::Event",
            "Rml::Variant",
            "Rml::VariantList"
        }.Select(name => new RecordBinding($"record:{name}", name.Split("::")[1], name.Split("::")[1], name)
        {
            HeaderIncludePath = $"RmlUi/Core/{name.Split("::")[1]}.h"
        }).ToArray();

        foreach (var record in records)
        {
            record.SetSilentExclusionReason("outside default rbfx surface");
            compilation.Members.Add(record);
        }

        RbfxRmlUiSurfacePass.Apply(compilation);

        foreach (var record in records)
        {
            Assert.Equal("Rml", record.ManagedNamespace);
            Assert.False(record.IsIgnored());
            Assert.False(record.IsSilentlyExcluded());
        }
    }

    [Fact]
    public void Rml_pass_keeps_document_show_flags_for_generated_show_surface()
    {
        var compilation = CreateCompilation();
        var document = new RecordBinding("record:Rml::ElementDocument", "ElementDocument", "ElementDocument", "Rml::ElementDocument")
        {
            HeaderIncludePath = "RmlUi/Core/ElementDocument.h"
        };
        var modalFlag = new EnumBinding("enum:Rml::ModalFlag", "ModalFlag", "ModalFlag", cppQualifiedName: "Rml::ModalFlag")
        {
            HeaderIncludePath = "RmlUi/Core/ElementDocument.h"
        };
        var focusFlag = new EnumBinding("enum:Rml::FocusFlag", "FocusFlag", "FocusFlag", cppQualifiedName: "Rml::FocusFlag")
        {
            HeaderIncludePath = "RmlUi/Core/ElementDocument.h"
        };
        var show = new FunctionBinding(
            "method:Rml::ElementDocument::Show",
            "Show",
            "Show",
            BindingType.Void,
            cppQualifiedName: "Rml::ElementDocument::Show")
        {
            IsStatic = false
        };
        show.Parameters.Add(new ParameterBinding(
            "parameter:Rml::ElementDocument::Show:modal_flag",
            "modal_flag",
            "modalFlag",
            BindingType.CreateEnum("Rml::ModalFlag", "ModalFlag", enumId: modalFlag.SymbolId),
            cppTypeName: "Rml::ModalFlag"));
        show.Parameters.Add(new ParameterBinding(
            "parameter:Rml::ElementDocument::Show:focus_flag",
            "focus_flag",
            "focusFlag",
            BindingType.CreateEnum("Rml::FocusFlag", "FocusFlag", enumId: focusFlag.SymbolId),
            cppTypeName: "Rml::FocusFlag"));
        document.Members.Add(show);
        compilation.Members.Add(document);
        compilation.Members.Add(modalFlag);
        compilation.Members.Add(focusFlag);

        RbfxRmlUiSurfacePass.Apply(compilation);

        Assert.False(modalFlag.IsIgnored());
        Assert.False(focusFlag.IsIgnored());
        Assert.False(show.IsIgnored());
    }

    [Fact]
    public void Rml_pass_keeps_data_model_callback_aliases_for_generated_binding_surface()
    {
        var compilation = CreateCompilation();
        var constructor = new RecordBinding("record:Rml::DataModelConstructor", "DataModelConstructor", "DataModelConstructor", "Rml::DataModelConstructor") { HeaderIncludePath = "RmlUi/Core/DataModelHandle.h" };
        var handle = new RecordBinding("record:Rml::DataModelHandle", "DataModelHandle", "DataModelHandle", "Rml::DataModelHandle") { HeaderIncludePath = "RmlUi/Core/DataModelHandle.h" };
        var @event = new RecordBinding("record:Rml::Event", "Event", "Event", "Rml::Event") { HeaderIncludePath = "RmlUi/Core/Event.h" };
        var variant = new RecordBinding("record:Rml::Variant", "Variant", "Variant", "Rml::Variant") { HeaderIncludePath = "RmlUi/Core/Variant.h" };
        var variantList = new RecordBinding("record:Rml::VariantList", "VariantList", "VariantList", "Rml::VariantList") { HeaderIncludePath = "RmlUi/Core/Types.h" };
        var bindFunc = new FunctionBinding(
            "method:Rml::DataModelConstructor::BindFunc",
            "BindFunc",
            "BindFunc",
            BindingType.Boolean,
            cppReturnTypeName: "bool",
            cppQualifiedName: "Rml::DataModelConstructor::BindFunc")
        {
            IsStatic = false
        };
        bindFunc.Parameters.Add(new ParameterBinding(
            "parameter:Rml::DataModelConstructor::BindFunc:name",
            "name",
            "name",
            BindingType.CreateConfigured("Rml::String", "global::Ea.String"),
            cppTypeName: "Rml::String"));
        bindFunc.Parameters.Add(new ParameterBinding(
            "parameter:Rml::DataModelConstructor::BindFunc:get_func",
            "get_func",
            "getFunc",
            BindingType.CreateCallback(
                "Rml::DataGetFunc",
                "global::System.Action<{0}>",
                managedMarshaller: null,
                BindingType.Void,
                [BindingType.CreateHandle("Rml::Variant*", "Variant", variant.SymbolId)]),
            cppTypeName: "Rml::DataGetFunc"));
        bindFunc.Parameters.Add(new ParameterBinding(
            "parameter:Rml::DataModelConstructor::BindFunc:set_func",
            "set_func",
            "setFunc",
            BindingType.CreateCallback(
                "Rml::DataSetFunc",
                "global::System.Action<{0}>",
                managedMarshaller: null,
                BindingType.Void,
                [BindingType.CreateHandle("const Rml::Variant&", "Variant", variant.SymbolId)]),
            cppTypeName: "Rml::DataSetFunc"));
        var bindEventCallback = new FunctionBinding(
            "method:Rml::DataModelConstructor::BindEventCallback",
            "BindEventCallback",
            "BindEventCallback",
            BindingType.Boolean,
            cppReturnTypeName: "bool",
            cppQualifiedName: "Rml::DataModelConstructor::BindEventCallback")
        {
            IsStatic = false
        };
        bindEventCallback.Parameters.Add(new ParameterBinding(
            "parameter:Rml::DataModelConstructor::BindEventCallback:name",
            "name",
            "name",
            BindingType.CreateConfigured("Rml::String", "global::Ea.String"),
            cppTypeName: "Rml::String"));
        bindEventCallback.Parameters.Add(new ParameterBinding(
            "parameter:Rml::DataModelConstructor::BindEventCallback:event_func",
            "event_func",
            "eventFunc",
            BindingType.CreateCallback(
                "Rml::DataEventFunc",
                "global::System.Action<{0}, {1}, {2}>",
                managedMarshaller: null,
                BindingType.Void,
                [
                    BindingType.CreateHandle("Rml::DataModelHandle", "DataModelHandle", handle.SymbolId),
                    BindingType.CreateHandle("Rml::Event&", "Event", @event.SymbolId),
                    BindingType.CreateHandle("const Rml::VariantList&", "VariantList", variantList.SymbolId)
                ]),
            cppTypeName: "Rml::DataEventFunc"));
        constructor.Members.Add(bindFunc);
        constructor.Members.Add(bindEventCallback);
        compilation.Members.Add(constructor);
        compilation.Members.Add(handle);
        compilation.Members.Add(@event);
        compilation.Members.Add(variant);
        compilation.Members.Add(variantList);

        RbfxRmlUiSurfacePass.Apply(compilation);

        Assert.False(bindFunc.IsIgnored(), bindFunc.GetIgnoreReason());
        Assert.False(bindEventCallback.IsIgnored(), bindEventCallback.GetIgnoreReason());
    }

    [Fact]
    public void Rml_pass_uses_header_path_for_ownership()
    {
        var compilation = CreateCompilation();
        var variant = new RecordBinding("record:Rml::Variant", "Variant", "Variant", "Rml::Variant")
        {
            HeaderIncludePath = "NotRml/Variant.h"
        };
        variant.SetSilentExclusionReason("outside default rbfx surface");
        compilation.Members.Add(variant);

        RbfxRmlUiSurfacePass.Apply(compilation);

        Assert.Null(variant.ManagedNamespace);
        Assert.True(variant.IsSilentlyExcluded());
    }

    [Fact]
    public void Rml_pass_adds_variant_string_template_instantiations()
    {
        var compilation = CreateCompilation();
        var variant = new RecordBinding("record:Rml::Variant", "Variant", "Variant", "Rml::Variant")
        {
            HeaderIncludePath = "RmlUi/Core/Variant.h"
        };
        var get = new FunctionBinding(
            "method:Rml::Variant::Get",
            "Get",
            "Get",
            BindingType.CreateGenericParameter("T"),
            cppReturnTypeName: "T",
            cppQualifiedName: "Rml::Variant::Get")
        {
            HeaderIncludePath = "RmlUi/Core/Variant.h",
            IsStatic = false,
            IsConstMethod = true,
            OwningRecordCppTypeName = "Rml::Variant"
        };
        get.GenericParameters.Add(new BindingGenericParameter("T", 0));
        get.SetSilentExclusionReason("generic function has no known concrete instantiations");
        get.Parameters.Add(new ParameterBinding(
            "parameter:Rml::Variant::Get:default_value",
            "default_value",
            "defaultValue",
            BindingType.CreateGenericParameter("T"),
            cppTypeName: "T"));
        variant.Members.Add(get);
        compilation.Members.Add(variant);

        RbfxRmlUiSurfacePass.Apply(compilation);

        Assert.False(get.IsIgnored(), get.GetIgnoreReason());
        Assert.Contains(get.SupportedGenericInstantiations, instantiation =>
            instantiation.TypeArguments.Single().CppTypeName == "Rml::String");
        var assign = Assert.Single(variant.Members.OfType<FunctionBinding>().Where(static function => function.ManagedName == "Assign"));
        Assert.Contains(assign.SupportedGenericInstantiations, instantiation =>
            instantiation.TypeArguments.Single().CppTypeName == "const Rml::String&");
    }

    [Fact]
    public void ImGui_pass_prunes_texture_id_fields()
    {
        var compilation = CreateCompilation();
        var command = new RecordBinding("record:ImDrawCmd", "ImDrawCmd", "ImDrawCmd", "ImDrawCmd") { HeaderIncludePath = "ImGui/imgui.h" };
        var field = new FieldBinding(
            "field:ImDrawCmd::TextureId",
            "TextureId",
            "TextureId",
            BindingType.CreateHandle("ImTextureID*", "global::System.IntPtr"),
            "ImTextureID*",
            "ImDrawCmd");
        command.Members.Add(field);
        compilation.Members.Add(command);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.True(field.IsIgnored());
        Assert.True(field.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_keeps_non_imgui_methods_with_non_imgui_record_types()
    {
        var compilation = CreateCompilation();
        var objectRecord = new RecordBinding("record:Urho3D::Object", "Object", "Object", "Urho3D::Object");
        var context = new RecordBinding("record:Urho3D::Context", "Context", "Context", "Urho3D::Context");
        var getSubsystem = new FunctionBinding(
            "method:Urho3D::Context::GetSubsystem",
            "GetSubsystem",
            "GetSubsystem",
            BindingType.CreateHandle("Urho3D::Object*", "Object", objectRecord.SymbolId),
            cppReturnTypeName: "Urho3D::Object*",
            cppQualifiedName: "Urho3D::Context::GetSubsystem")
        {
            IsStatic = false,
            IsConstMethod = true
        };
        getSubsystem.Parameters.Add(new ParameterBinding(
            "parameter:Urho3D::Context::GetSubsystem:type",
            "type",
            "type",
            BindingType.CreateConfigured("Urho3D::StringHash", "global::Urho3D.Math.StringHash"),
            cppTypeName: "Urho3D::StringHash"));
        context.Members.Add(getSubsystem);
        compilation.Members.Add(objectRecord);
        compilation.Members.Add(context);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.False(getSubsystem.IsIgnored(), getSubsystem.GetIgnoreReason());
        Assert.False(getSubsystem.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_keeps_non_imgui_char_pointer_fields()
    {
        var compilation = CreateCompilation();
        var record = new RecordBinding("record:Urho3D::NamedThing", "NamedThing", "NamedThing", "Urho3D::NamedThing");
        var field = new FieldBinding(
            "field:Urho3D::NamedThing::Name",
            "Name",
            "Name",
            BindingType.CreateHandle("const char*", "global::System.ReadOnlySpan<byte>"),
            "const char*",
            "Urho3D::NamedThing");
        record.Members.Add(field);
        compilation.Members.Add(record);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.False(field.IsIgnored(), field.GetIgnoreReason());
        Assert.False(field.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_restores_public_records_after_prior_silent_exclusion()
    {
        var compilation = CreateCompilation();
        var drawList = new RecordBinding("record:ImDrawList", "ImDrawList", "ImDrawList", "ImDrawList") { HeaderIncludePath = "ImGui/imgui.h" };
        drawList.SetSilentExclusionReason("outside default rbfx surface");
        compilation.Members.Add(drawList);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.Equal("ImGuiNet", drawList.ManagedNamespace);
        Assert.False(drawList.IsIgnored());
        Assert.False(drawList.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_uses_header_path_for_record_ownership()
    {
        var compilation = CreateCompilation();
        var drawList = new RecordBinding("record:ImDrawList", "ImDrawList", "ImDrawList", "ImDrawList")
        {
            HeaderIncludePath = "Other/imgui.h"
        };
        drawList.SetSilentExclusionReason("outside default rbfx surface");
        compilation.Members.Add(drawList);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.Null(drawList.ManagedNamespace);
        Assert.True(drawList.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_restores_free_functions_after_prior_silent_exclusion()
    {
        var compilation = CreateCompilation();
        var begin = new FunctionBinding(
            "function:ImGui::Begin",
            "Begin",
            "Begin",
            BindingType.Boolean,
            cppQualifiedName: "ImGui::Begin")
        {
            HeaderIncludePath = "ImGui/imgui.h"
        };
        begin.SetSilentExclusionReason("outside default rbfx surface");
        compilation.Members.Add(begin);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.Equal("ImGuiNet", begin.ManagedNamespace);
        Assert.Equal("ImGui", begin.ManagedClassName);
        Assert.False(begin.IsIgnored());
        Assert.False(begin.IsSilentlyExcluded());
    }

    [Theory]
    [InlineData("SetNextWindowPos")]
    [InlineData("PushTextWrapPos")]
    [InlineData("PopTextWrapPos")]
    [InlineData("Button")]
    [InlineData("BeginItemTooltip")]
    [InlineData("EndTooltip")]
    public void ImGui_pass_restores_application_free_functions_after_prior_silent_exclusion(string nativeName)
    {
        var compilation = CreateCompilation();
        var function = new FunctionBinding(
            $"function:ImGui::{nativeName}",
            nativeName,
            nativeName,
            nativeName == "Button" || nativeName == "BeginItemTooltip" ? BindingType.Boolean : BindingType.Void,
            cppQualifiedName: $"ImGui::{nativeName}")
        {
            HeaderIncludePath = "ImGui/imgui.h"
        };
        function.SetSilentExclusionReason("outside default rbfx surface");
        compilation.Members.Add(function);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.Equal("ImGuiNet", function.ManagedNamespace);
        Assert.Equal("ImGui", function.ManagedClassName);
        Assert.False(function.IsIgnored());
        Assert.False(function.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_keeps_internal_free_functions_silently_excluded()
    {
        var compilation = CreateCompilation();
        var recover = new FunctionBinding(
            "function:ImGui::ErrorCheckEndFrameRecover",
            "ErrorCheckEndFrameRecover",
            "ErrorCheckEndFrameRecover",
            BindingType.Void,
            cppQualifiedName: "ImGui::ErrorCheckEndFrameRecover")
        {
            HeaderIncludePath = "ImGui/imgui_internal.h"
        };
        compilation.Members.Add(recover);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.Equal("ImGuiNet", recover.ManagedNamespace);
        Assert.Equal("ImGui", recover.ManagedClassName);
        Assert.True(recover.IsIgnored());
        Assert.True(recover.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_keeps_non_sample_public_free_functions_silently_excluded()
    {
        var compilation = CreateCompilation();
        var plotLines = new FunctionBinding(
            "function:ImGui::PlotLines",
            "PlotLines",
            "PlotLines",
            BindingType.Void,
            cppQualifiedName: "ImGui::PlotLines")
        {
            HeaderIncludePath = "ImGui/imgui.h"
        };
        compilation.Members.Add(plotLines);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.Equal("ImGuiNet", plotLines.ManagedNamespace);
        Assert.Equal("ImGui", plotLines.ManagedClassName);
        Assert.True(plotLines.IsIgnored());
        Assert.True(plotLines.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_prunes_char_pointer_fields()
    {
        var compilation = CreateCompilation();
        var io = new RecordBinding("record:ImGuiIO", "ImGuiIO", "ImGuiIO", "ImGuiIO") { HeaderIncludePath = "ImGui/imgui.h" };
        var field = new FieldBinding(
            "field:ImGuiIO::IniFilename",
            "IniFilename",
            "IniFilename",
            BindingType.CreateHandle("const char*", "global::System.ReadOnlySpan<byte>"),
            "const char*",
            "ImGuiIO");
        io.Members.Add(field);
        compilation.Members.Add(io);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.True(field.IsIgnored());
        Assert.True(field.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_prunes_const_imvec4_pointer_methods()
    {
        var compilation = CreateCompilation();
        var drawList = new RecordBinding("record:ImDrawList", "ImDrawList", "ImDrawList", "ImDrawList") { HeaderIncludePath = "ImGui/imgui.h" };
        var method = new FunctionBinding(
            "method:ImDrawList::AddText",
            "AddText",
            "AddText",
            BindingType.Void,
            cppQualifiedName: "ImDrawList::AddText")
        {
            IsStatic = false
        };
        method.Parameters.Add(new ParameterBinding(
            "parameter:ImDrawList::AddText:cpu_fine_clip_rect",
            "cpu_fine_clip_rect",
            "cpuFineClipRect",
            BindingType.CreateHandle("const ImVec4*", "global::Urho3D.Math.Vector4?"),
            cppTypeName: "const ImVec4*"));
        drawList.Members.Add(method);
        compilation.Members.Add(drawList);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.True(method.IsIgnored());
        Assert.True(method.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_prunes_vararg_free_functions()
    {
        var compilation = CreateCompilation();
        var text = new FunctionBinding(
            "function:ImGui::Text",
            "Text",
            "Text",
            BindingType.Void,
            cppQualifiedName: "ImGui::Text")
        {
            HeaderIncludePath = "ImGui/imgui.h"
        };
        text.SetMetadata(MetadataKeys.DeclarationDisplayText, "IMGUI_API void Text(const char* fmt, ...)");
        compilation.Members.Add(text);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.True(text.IsIgnored());
        Assert.True(text.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_prunes_callback_function_parameters()
    {
        var compilation = CreateCompilation();
        var inputText = new FunctionBinding(
            "function:ImGui::InputText",
            "InputText",
            "InputText",
            BindingType.Boolean,
            cppQualifiedName: "ImGui::InputText")
        {
            HeaderIncludePath = "ImGui/imgui.h"
        };
        inputText.Parameters.Add(new ParameterBinding(
            "parameter:ImGui::InputText:callback",
            "callback",
            "callback",
            BindingType.CreateHandle("ImGuiInputTextCallback", "global::System.IntPtr"),
            cppTypeName: "ImGuiInputTextCallback"));
        compilation.Members.Add(inputText);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.True(inputText.IsIgnored());
        Assert.True(inputText.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_prunes_raw_array_parameters()
    {
        var compilation = CreateCompilation();
        var combo = new FunctionBinding(
            "function:ImGui::Combo",
            "Combo",
            "Combo",
            BindingType.Boolean,
            cppQualifiedName: "ImGui::Combo")
        {
            HeaderIncludePath = "ImGui/imgui.h"
        };
        combo.Parameters.Add(new ParameterBinding(
            "parameter:ImGui::Combo:items",
            "items",
            "items",
            BindingType.CreateHandle("const char* const[]", "global::System.ReadOnlySpan<nint>"),
            cppTypeName: "const char*const[]"));
        compilation.Members.Add(combo);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.True(combo.IsIgnored());
        Assert.True(combo.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_prunes_user_defined_literal_operators()
    {
        var compilation = CreateCompilation();
        var literal = new FunctionBinding(
            "function:ImGui::Literals::operator\"\"_pt",
            "operator\"\"_pt",
            "operator\"\"_pt",
            BindingType.Single,
            cppQualifiedName: "ImGui::Literals::operator\"\"_pt")
        {
            HeaderIncludePath = "ImGui/imgui.h"
        };
        compilation.Members.Add(literal);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.True(literal.IsIgnored());
        Assert.True(literal.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_prunes_fixed_array_methods()
    {
        var compilation = CreateCompilation();
        var atlas = new RecordBinding("record:ImFontAtlas", "ImFontAtlas", "ImFontAtlas", "ImFontAtlas") { HeaderIncludePath = "ImGui/imgui.h" };
        var method = new FunctionBinding(
            "method:ImFontAtlas::GetMouseCursorTexData",
            "GetMouseCursorTexData",
            "GetMouseCursorTexData",
            BindingType.Boolean,
            cppQualifiedName: "ImFontAtlas::GetMouseCursorTexData")
        {
            IsStatic = false
        };
        method.Parameters.Add(new ParameterBinding(
            "parameter:ImFontAtlas::GetMouseCursorTexData:out_uv_border",
            "out_uv_border",
            "outUvBorder",
            BindingType.CreateHandle("ImVec2", "global::Urho3D.Math.Vector2").WithFixedArray(BindingType.CreateHandle("ImVec2", "global::Urho3D.Math.Vector2"), 2),
            cppTypeName: "ImVec2[2]"));
        atlas.Members.Add(method);
        compilation.Members.Add(atlas);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.True(method.IsIgnored());
        Assert.True(method.IsSilentlyExcluded());
    }

    [Fact]
    public void ImGui_pass_removes_fixed_array_instantiations_for_configured_value_types()
    {
        var compilation = CreateCompilation();
        var array = new RecordBinding("record:NativeArray", "NativeArray", "NativeArray", "NativeArray");
        array.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding(
        [
            BindingType.CreateHandle("ImVec2", "global::Urho3D.Math.Vector2"),
            BindingType.CreateConfigured("2", "2")
        ]));
        compilation.Members.Add(array);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.Empty(array.SupportedGenericInstantiations);
    }

    [Fact]
    public void ImGui_pass_removes_bool_fixed_array_instantiations()
    {
        var compilation = CreateCompilation();
        var array = new RecordBinding("record:NativeArray", "NativeArray", "NativeArray", "NativeArray");
        array.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding(
        [
            BindingType.Boolean,
            BindingType.CreateConfigured("5", "5")
        ]));
        compilation.Members.Add(array);

        RbfxImGuiSurfacePass.Apply(compilation);

        Assert.Empty(array.SupportedGenericInstantiations);
    }

    private static BindingCompilation CreateCompilation()
        => new("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
}
