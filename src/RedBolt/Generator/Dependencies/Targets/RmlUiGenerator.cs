namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCpp.Model.Ast;
using SharpCpp.Rendering;

internal sealed class RmlUiGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "rmlui",
        "RedBolt.Bindings.RmlUi",
        "RedBolt.Bindings.RmlUi",
        "RmlUi",
        nativeLibraryName,
        "redbolt_rmlui",
        "-xc++",
        "-std=c++20",
        true)
{
    private static readonly HashSet<string> RmlUiDirectorRecordCppTypeNames = new(StringComparer.Ordinal)
    {
        "Rml::ContextInstancer",
        "Rml::EventListener",
        "Rml::EventListenerInstancer",
        "Rml::FileInterface",
        "Rml::FontEngineInterface",
        "Rml::Plugin",
        "Rml::RenderInterfaceCompatibility",
        "Rml::SystemInterface",
        "Rml::VariableDefinition"
    };

    private static readonly HashSet<string> RetainedFreeFunctions = new(StringComparer.Ordinal)
    {
        "Rml::CreateContext",
        "Rml::Debugger::Initialise",
        "Rml::Debugger::IsVisible",
        "Rml::Debugger::SetVisible",
        "Rml::GetFileInterface",
        "Rml::GetFontEngineInterface",
        "Rml::GetRenderInterface",
        "Rml::GetSystemInterface",
        "Rml::Initialise",
        "Rml::LoadFontFace",
        "Rml::RegisterPlugin",
        "Rml::ReleaseTextures",
        "Rml::RemoveContext",
        "Rml::SetFileInterface",
        "Rml::SetFontEngineInterface",
        "Rml::SetRenderInterface",
        "Rml::SetSystemInterface",
        "Rml::Shutdown",
        "Rml::UnregisterPlugin"
    };

    protected override IReadOnlyDictionary<string, string> NamespaceMappings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Rml"] = "RedBolt.Bindings.RmlUi",
            ["RedBoltNative"] = "RedBoltNative"
        };

    protected override IReadOnlySet<string>? DirectorRecordCppTypeNames =>
        RmlUiDirectorRecordCppTypeNames;

    // RmlUi uses SharpCpp directors, so its wrapper must own the callback
    // release registration entry point.  This used to be supplied by the
    // monolithic native library through the SDL-generated unit.
    protected override bool IncludeCallbackReleaseRegistration => true;

    protected override IEnumerable<string> GetAdditionalMarshallerHeaders(GeneratorOptions options)
    {
        yield return Path.Combine(
            options.RedBoltRoot,
            "src",
            "RedBolt",
            "Native",
            "RmlUi",
            "RmlUiValueMarshallers.hpp");
    }

    protected override bool RetainStdlibWrapper(RecordBinding record) =>
        record.ManagedNamespace == "Std" && record.ManagedName is "String" or "UnorderedMap" or "Vector";

    protected override void ConfigureBeforeValidation(BindingCompilation compilation)
    {
        ConfigureSurface(compilation);
    }

    protected override int PruneCompilation(BindingCompilation compilation) => PruneSurface(compilation);

    protected override GeneratedBindingSet TransformBindings(GeneratedBindingSet bindings) =>
        AddLegacyAbi(bindings);

    private void ConfigureSurface(BindingCompilation compilation)
    {
        var systemInterface = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::SystemInterface");
        systemInterface.Bases.Clear();
        systemInterface.ManagedBaseTypeName = null;
        var fileInterface = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::FileInterface");
        fileInterface.Bases.Clear();
        fileInterface.ManagedBaseTypeName = null;

        var fontEngineInterface = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::FontEngineInterface");
        fontEngineInterface.Bases.Clear();
        fontEngineInterface.ManagedBaseTypeName = null;
        foreach (var method in fontEngineInterface.Members.OfType<FunctionBinding>())
        {
            if (!method.IsConstructor && method.NativeName is not ("Initialize" or "Shutdown"))
                method.SetSilentExclusionReason("RmlUi font engine member is outside the lifecycle binding slice");
        }

        var harfBuzzFontEngine = compilation.Records.First(static record =>
            record.CppQualifiedName == "RedBoltNative::RmlHarfBuzzFontEngine");
        foreach (var method in harfBuzzFontEngine.Members.OfType<FunctionBinding>())
        {
            if (!method.IsConstructor)
                method.SetSilentExclusionReason("RmlUi HarfBuzz font engine is accessed through FontEngineInterface");
        }

        var eventListener = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::EventListener");

        var factory = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Factory");
        foreach (var method in factory.Members.OfType<FunctionBinding>())
        {
            if (method.NativeName is not (
                    "ClearStyleSheetCache" or "ClearTemplateCache" or
                    "InstanceEventListener" or "RegisterContextInstancer" or "RegisterEventListenerInstancer"))
                method.SetSilentExclusionReason("RmlUi factory member is outside the current binding slice");
        }
        var registerEventListenerInstancer = factory.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "RegisterEventListenerInstancer");
        registerEventListenerInstancer.Parameters[0].Type =
            registerEventListenerInstancer.Parameters[0].Type.WithManagedTypeFormatOverride(
                $"{registerEventListenerInstancer.Parameters[0].Type.ManagedTypeName}?");

        var context = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Context");
        context.Bases.Clear();
        context.ManagedBaseTypeName = null;
        foreach (var method in context.Members.OfType<FunctionBinding>())
        {
            if (method.IsConstructor || method.NativeName is
                "GetDensityIndependentPixelRatio" or "GetDimensions" or "GetDocument" or "GetFocusElement" or
                "GetHoverElement" or "GetName" or "GetNumDocuments" or "GetRootElement" or "Render" or
                "LoadDocumentFromMemory" or "SetDensityIndependentPixelRatio" or "SetDimensions" or
                "CreateDataModel" or "GetDataModel" or "RemoveDataModel" or
                "ProcessKeyDown" or "ProcessKeyUp" or "ProcessMouseButtonDown" or "ProcessMouseButtonUp" or
                "ProcessMouseMove" or "ProcessMouseWheel" or "ProcessTextInput" or
                "UnloadAllDocuments" or "UnloadDocument" or "Update")
            {
                if (method.NativeName == "ProcessMouseWheel" &&
                    method.Parameters[0].CppTypeName != "float")
                {
                    method.SetSilentExclusionReason("RmlUi vector mouse wheel overload is outside the current binding slice");
                    continue;
                }
                if (method.NativeName == "ProcessTextInput" &&
                    method.Parameters[0].CppTypeName == "Rml::Character")
                {
                    method.SetSilentExclusionReason("RmlUi Character text input overload is outside the current binding slice");
                    continue;
                }
            }
            else
            {
                method.SetSilentExclusionReason("RmlUi context member is outside the current binding slice");
            }
        }
        foreach (var parameter in context.Members.OfType<FunctionBinding>()
                     .Where(static method => method.IsConstructor)
                     .SelectMany(static method => method.Parameters)
                     .Where(static parameter => parameter.NativeName is "render_manager" or "text_input_handler"))
        {
            parameter.Type = BindingType.IntPtr;
        }

        var vector2iType = BindingType.CreateConfigured(
            "Rml::Vector2<int>", "Vector2i",
            marshallerTypeName: null,
            managedMarshaller: "global::RedBolt.Math.Vector2iMarshaller");
        var getDimensions = context.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "GetDimensions");
        getDimensions.ReturnType = vector2iType;
        getDimensions.CppReturnTypeName = "Rml::Vector2<int>";
        var setDimensions = context.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "SetDimensions");
        var dimensionsParameter = setDimensions.Parameters.First(static parameter =>
            parameter.NativeName == "dimensions");
        dimensionsParameter.Type = vector2iType;
        dimensionsParameter.CppTypeName = "Rml::Vector2<int>";

        ConfigureDataModelSurface(compilation, context);

        var element = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Element");
        element.Bases.Clear();
        element.ManagedBaseTypeName = null;
        foreach (var method in element.Members.OfType<FunctionBinding>())
        {
            var retained = (!method.IsGeneric && method.NativeName is
                    "Blur" or "Click" or "Focus" or "GetAbsoluteOffset" or "GetBox" or "GetComputedValues" or
                    "GetContext" or "GetElementById" or "GetFocusLeafNode" or "GetOwnerDocument" or
                    "GetParentNode" or "GetProperty" or "GetTagName" or "IsVisible" or "ResolveLength" or
                    "ScrollIntoView" or "SetProperty") ||
                (method.IsGeneric && method.NativeName is "GetAttribute" or "SetAttribute");
            if (method.NativeName == "DispatchEvent" && method.Parameters.Count == 2 &&
                method.Parameters[0].CppTypeName == "const Rml::String&")
            {
                retained = true;
            }
            if (method.NativeName == "GetBox" && method.Parameters.Count != 0)
                retained = false;
            if (method.NativeName == "ScrollIntoView" &&
                method.Parameters.FirstOrDefault()?.CppTypeName != "Rml::ScrollIntoViewOptions")
                retained = false;
            if (!retained)
                method.SetSilentExclusionReason("RmlUi element member is outside the current binding slice");
        }
        foreach (var method in element.Members.OfType<FunctionBinding>()
                     .Where(static method => method.IsGeneric && method.NativeName is "GetAttribute" or "SetAttribute"))
        {
            method.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([BindingType.Boolean]));
            method.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([BindingType.IntPtr]));
        }

        var elementDocument = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::ElementDocument");
        foreach (var method in elementDocument.Members.OfType<FunctionBinding>())
        {
            if (method.NativeName is not (
                    "Close" or "FindNextTabElement" or "GetContext" or "GetSourceURL" or
                    "Hide" or "IsModal" or "Show" or "UpdateDocument"))
                method.SetSilentExclusionReason("RmlUi document member is outside the current binding slice");
        }

        var property = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Property");
        property.Bases.Clear();
        property.ManagedBaseTypeName = null;
        foreach (var member in property.Members)
        {
            if (member is not FunctionBinding { NativeName: "GetNumericValue" })
                member.SetSilentExclusionReason("RmlUi property member is outside the component binding slice");
        }
        var unit = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::Unit");
        unit.IsFlags = true;
        if (unit.Values.Count == 0)
        {
            foreach (var value in new[]
                     {
                         new EnumValueBinding("UNKNOWN", "Unknown", "0"),
                         new EnumValueBinding("KEYWORD", "Keyword", "1"),
                         new EnumValueBinding("STRING", "String", "2"),
                         new EnumValueBinding("COLOUR", "Colour", "4"),
                         new EnumValueBinding("RATIO", "Ratio", "8"),
                         new EnumValueBinding("NUMBER", "Number", "16"),
                         new EnumValueBinding("PERCENT", "Percent", "32"),
                         new EnumValueBinding("PX", "Px", "64"),
                         new EnumValueBinding("DP", "Dp", "128"),
                         new EnumValueBinding("VW", "Vw", "256"),
                         new EnumValueBinding("VH", "Vh", "512"),
                         new EnumValueBinding("X", "X", "1024"),
                         new EnumValueBinding("EM", "Em", "2048"),
                         new EnumValueBinding("REM", "Rem", "4096"),
                         new EnumValueBinding("INCH", "Inch", "8192"),
                         new EnumValueBinding("CM", "Cm", "16384"),
                         new EnumValueBinding("MM", "Mm", "32768"),
                         new EnumValueBinding("PT", "Pt", "65536"),
                         new EnumValueBinding("PC", "Pc", "131072"),
                         new EnumValueBinding("PPI_UNIT", "PpiUnit", "253952"),
                         new EnumValueBinding("DEG", "Deg", "262144"),
                         new EnumValueBinding("RAD", "Rad", "524288"),
                         new EnumValueBinding("TRANSFORM", "Transform", "1048576"),
                         new EnumValueBinding("TRANSITION", "Transition", "2097152"),
                         new EnumValueBinding("ANIMATION", "Animation", "4194304"),
                         new EnumValueBinding("DECORATOR", "Decorator", "8388608"),
                         new EnumValueBinding("FILTER", "Filter", "16777216"),
                         new EnumValueBinding("FONTEFFECT", "FontEffect", "33554432"),
                         new EnumValueBinding("COLORSTOPLIST", "ColorStopList", "67108864"),
                         new EnumValueBinding("BOXSHADOWLIST", "BoxShadowList", "134217728"),
                         new EnumValueBinding("LENGTH", "Length", "261056"),
                         new EnumValueBinding("LENGTH_PERCENT", "LengthPercent", "261088"),
                         new EnumValueBinding("NUMBER_PERCENT", "NumberPercent", "48"),
                         new EnumValueBinding("NUMBER_LENGTH_PERCENT", "NumberLengthPercent", "261104"),
                         new EnumValueBinding("DP_SCALABLE_LENGTH", "DpScalableLength", "254080"),
                         new EnumValueBinding("ANGLE", "Angle", "786432"),
                         new EnumValueBinding("NUMERIC", "Numeric", "1048560")
                     })
            {
                unit.Values.Add(value);
            }
        }
        var unitType = BindingType.CreateEnum(
            unit.CppQualifiedName, unit.ManagedName, enumId: unit.SymbolId);
        var propertyConstructor = new FunctionBinding(
            "constructor:Rml::Property:float:Rml::Unit",
            "Property",
            property.ManagedName,
            property.HandleType,
            "redbolt_rmlui_Rml_Property_Property_float_Unit",
            cppQualifiedName: "Rml::Property::Property")
        {
            HeaderIncludePath = property.HeaderIncludePath,
            IsConstructor = true,
            IsStatic = false,
            OwningRecordCppTypeName = property.CppQualifiedName
        };
        propertyConstructor.Parameters.Add(new ParameterBinding(
            "parameter:Rml::Property::Property:float:value",
            "value",
            "value",
            BindingType.Single,
            cppTypeName: "float"));
        propertyConstructor.Parameters.Add(new ParameterBinding(
            "parameter:Rml::Property::Property:Rml::Unit:unit",
            "unit",
            "unit",
            unitType,
            cppTypeName: "Rml::Unit"));
        property.Members.Add(propertyConstructor);

        var box = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Box");
        box.Bases.Clear();
        box.ManagedBaseTypeName = null;
        foreach (var method in box.Members.OfType<FunctionBinding>())
        {
            if (method.IsConstructor || method.NativeName != "GetSize")
                method.SetSilentExclusionReason("RmlUi box member is outside the component binding slice");
        }

        var computedValues = compilation.Records.First(static record =>
            record.CppQualifiedName is "Rml::ComputedValues" or "Rml::Style::ComputedValues");
        computedValues.Bases.Clear();
        computedValues.ManagedNamespace = ManagedNamespace;
        computedValues.ManagedBaseTypeName = null;
        foreach (var method in computedValues.Members.OfType<FunctionBinding>())
        {
            if (method.IsConstructor || !method.IsConstMethod || method.Parameters.Count != 0 ||
                method.NativeName is not ("focus" or "tab_index"))
                method.SetSilentExclusionReason("RmlUi computed-value member is outside the component binding slice");
        }

        var numericValue = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::NumericValue");
        numericValue.Bases.Clear();
        numericValue.ManagedBaseTypeName = null;
        foreach (var member in numericValue.Members)
            member.SetSilentExclusionReason("RmlUi numeric value is passed opaquely");

        var scrollIntoViewOptions = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::ScrollIntoViewOptions");
        scrollIntoViewOptions.Bases.Clear();
        scrollIntoViewOptions.ManagedBaseTypeName = null;
        foreach (var member in scrollIntoViewOptions.Members)
        {
            if (member is not FunctionBinding { IsConstructor: true })
                member.SetSilentExclusionReason("RmlUi scroll options fields are outside the component binding slice");
        }

        foreach (var method in context.Members.OfType<FunctionBinding>()
                     .Where(static method => method.NativeName == "LoadDocumentFromMemory"))
        {
            method.ReturnType = method.ReturnType.WithManagedTypeFormatOverride(
                $"{method.ReturnType.ManagedTypeName}?");
        }
        foreach (var method in context.Members.OfType<FunctionBinding>()
                     .Where(static method => method.NativeName is "GetDocument" or "GetFocusElement" or "GetHoverElement"))
        {
            method.ReturnType = method.ReturnType.WithManagedTypeFormatOverride(
                $"{method.ReturnType.ManagedTypeName}?");
        }
        foreach (var method in element.Members.OfType<FunctionBinding>()
                     .Where(static method => method.NativeName is "GetElementById" or "GetOwnerDocument"))
        {
            method.ReturnType = method.ReturnType.WithManagedTypeFormatOverride(
                $"{method.ReturnType.ManagedTypeName}?");
        }
        foreach (var method in element.Members.OfType<FunctionBinding>()
                     .Where(static method => method.NativeName is "GetFocusLeafNode" or "GetParentNode"))
        {
            method.ReturnType = method.ReturnType.WithManagedTypeFormatOverride(
                $"{method.ReturnType.ManagedTypeName}?");
        }
        foreach (var method in elementDocument.Members.OfType<FunctionBinding>()
                     .Where(static method => method.NativeName == "FindNextTabElement"))
        {
            method.ReturnType = method.ReturnType.WithManagedTypeFormatOverride(
                $"{method.ReturnType.ManagedTypeName}?");
        }
        foreach (var method in element.Members.OfType<FunctionBinding>()
                     .Where(static method => method.NativeName == "GetProperty"))
        {
            method.ReturnType = method.ReturnType.WithManagedTypeFormatOverride(
                $"{method.ReturnType.ManagedTypeName}?");
        }

        var vector2fType = BindingType.CreateConfigured(
            "Rml::Vector2<float>", "Vector2f",
            marshallerTypeName: null,
            managedMarshaller: "global::RedBolt.Math.Vector2fMarshaller");
        var getAbsoluteOffset = element.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "GetAbsoluteOffset");
        getAbsoluteOffset.ReturnType = vector2fType;
        getAbsoluteOffset.CppReturnTypeName = "Rml::Vector2<float>";
        foreach (var getSize in box.Members.OfType<FunctionBinding>()
                     .Where(static method => method.NativeName == "GetSize"))
        {
            getSize.ReturnType = vector2fType;
            getSize.CppReturnTypeName = "Rml::Vector2<float>";
        }

        var @event = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Event");
        @event.Bases.Clear();
        @event.ManagedBaseTypeName = null;
        foreach (var method in @event.Members.OfType<FunctionBinding>())
        {
            if (method.NativeName is not ("GetCurrentElement" or "GetPhase" or "GetTargetElement" or "IsPropagating"))
                method.SetSilentExclusionReason("RmlUi event member is outside the event listener binding slice");
        }

        eventListener.Bases.Clear();
        eventListener.ManagedBaseTypeName = null;

        var eventListenerInstancer = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::EventListenerInstancer");
        eventListenerInstancer.Bases.Clear();
        eventListenerInstancer.ManagedBaseTypeName = null;

        var contextInstancer = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::ContextInstancer");
        contextInstancer.Bases.Clear();
        contextInstancer.ManagedBaseTypeName = null;
        foreach (var parameter in contextInstancer.Members.OfType<FunctionBinding>()
                     .Where(static method => method.NativeName == "InstanceContext")
                     .SelectMany(static method => method.Parameters)
                     .Where(static parameter => parameter.NativeName is "render_manager" or "text_input_handler"))
        {
            parameter.Type = BindingType.IntPtr;
        }
        var releaseContext = contextInstancer.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "ReleaseContext");
        releaseContext.Parameters.First(static parameter => parameter.NativeName == "context").CallbackTakesOwnership = true;
        var instanceContext = contextInstancer.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "InstanceContext");
        instanceContext.ReturnType = context.HandleType;
        instanceContext.ReturnsOwnedHandle = true;
        contextInstancer.Members.Add(new FunctionBinding(
            "method:Rml::ContextInstancer::Release",
            "Release",
            "Release",
            BindingType.Void,
            "redbolt_rmlui_Rml_ContextInstancer_Release",
            cppQualifiedName: "Rml::Releasable::Release")
        {
            AccessModifier = BindingAccessModifier.Protected,
            IsStatic = false,
            IsPureVirtual = true,
            IsVirtual = true,
            OwningRecordCppTypeName = contextInstancer.CppQualifiedName
        });

        var plugin = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Plugin");
        plugin.Bases.Clear();
        plugin.ManagedBaseTypeName = null;

        var boxArea = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::BoxArea");
        var boxAreaType = BindingType.CreateEnum(
            boxArea.CppQualifiedName, boxArea.ManagedName, enumId: boxArea.SymbolId);
        foreach (var parameter in element.Members.OfType<FunctionBinding>()
                     .Where(static method => method.NativeName == "GetAbsoluteOffset")
                     .SelectMany(static method => method.Parameters)
                     .Where(static parameter => parameter.NativeName == "area"))
        {
            parameter.Type = boxAreaType;
            parameter.CppTypeName = boxArea.CppQualifiedName;
        }
        var propertyId = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::PropertyId");
        var propertyIdType = BindingType.CreateEnum(
            propertyId.CppQualifiedName, propertyId.ManagedName, enumId: propertyId.SymbolId);
        foreach (var parameter in element.Members.OfType<FunctionBinding>()
                     .Where(static method => method.NativeName is "GetProperty" or "SetProperty")
                     .SelectMany(static method => method.Parameters)
                     .Where(static parameter => parameter.NativeName == "id"))
        {
            parameter.Type = propertyIdType;
            parameter.CppTypeName = propertyId.CppQualifiedName;
        }
        foreach (var cppQualifiedName in new[]
                 {
                     "Rml::Unit",
                     "Rml::ScrollAlignment",
                     "Rml::ScrollBehavior",
                     "Rml::ScrollParentage",
                     "Rml::Style::Focus",
                     "Rml::Style::TabIndex"
                 })
        {
            var @enum = compilation.Enums.First(@enum => @enum.CppQualifiedName == cppQualifiedName);
            if (cppQualifiedName is "Rml::Style::Focus" or "Rml::Style::TabIndex")
                @enum.ManagedNamespace = ManagedNamespace;
        }
        var keyIdentifier = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::Input::KeyIdentifier");
        var keyIdentifierType = BindingType.CreateEnum(
            keyIdentifier.CppQualifiedName, keyIdentifier.ManagedName, enumId: keyIdentifier.SymbolId);
        foreach (var parameter in context.Members.OfType<FunctionBinding>()
                     .Where(static method => method.NativeName is "ProcessKeyDown" or "ProcessKeyUp")
                     .SelectMany(static method => method.Parameters)
                     .Where(static parameter => parameter.NativeName == "key_identifier"))
        {
            parameter.Type = keyIdentifierType;
            parameter.CppTypeName = keyIdentifier.CppQualifiedName;
        }
        var fontWeight = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::Style::FontWeight");
        var fontWeightType = BindingType.CreateEnum(
            fontWeight.CppQualifiedName, fontWeight.ManagedName, enumId: fontWeight.SymbolId);
        foreach (var parameter in compilation.Functions
                     .Where(static function => function.CppQualifiedName == "Rml::LoadFontFace")
                     .SelectMany(static function => function.Parameters)
                     .Where(static parameter => parameter.NativeName == "weight"))
        {
            parameter.Type = fontWeightType;
            parameter.CppTypeName = fontWeight.CppQualifiedName;
        }

        var fileHandleType = BindingType.CreateConfigured(
            "Rml::FileHandle", "nuint", BindingPrimitiveKind.NativeUInt);
        var open = fileInterface.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "Open");
        open.ReturnType = fileHandleType;
        open.CppReturnTypeName = "Rml::FileHandle";
        foreach (var parameter in fileInterface.Members.OfType<FunctionBinding>()
                     .SelectMany(static method => method.Parameters)
                     .Where(static parameter => parameter.NativeName == "file"))
        {
            parameter.Type = fileHandleType;
            parameter.CppTypeName = "Rml::FileHandle";
        }

        var renderInterfaceCompatibility = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::RenderInterfaceCompatibility");
        renderInterfaceCompatibility.Bases.Clear();
        renderInterfaceCompatibility.ManagedBaseTypeName = null;

        var nativeUIntType = BindingType.CreateConfigured(
            "uintptr_t", "nuint", BindingPrimitiveKind.NativeUInt);
        foreach (var method in renderInterfaceCompatibility.Members.OfType<FunctionBinding>())
        {
            if (method.CppReturnTypeName is "Rml::TextureHandle" or "Rml::CompiledGeometryHandle")
                method.ReturnType = nativeUIntType;
            else if (method.NativeName == "GetAdaptedInterface")
                method.ReturnType = BindingType.IntPtr;

            foreach (var parameter in method.Parameters)
            {
                if (parameter.CppTypeName is "Rml::TextureHandle" or "Rml::CompiledGeometryHandle")
                {
                    parameter.Type = nativeUIntType;
                    continue;
                }

                if (parameter.CppTypeName.Contains('*', StringComparison.Ordinal) ||
                    parameter.CppTypeName.Contains("Rml::TextureHandle&", StringComparison.Ordinal) ||
                    parameter.CppTypeName.Contains("Rml::Vector2", StringComparison.Ordinal) ||
                    parameter.CppTypeName.Contains("Rml::Matrix4", StringComparison.Ordinal))
                {
                    parameter.Type = BindingType.IntPtr;
                    parameter.Direction = BindingParameterDirection.In;
                    parameter.AbiKind = BindingParameterAbiKind.Value;
                }
            }
        }

        var log = compilation.Records.First(static record => record.CppQualifiedName == "Rml::Log");
        foreach (var method in log.Members.OfType<FunctionBinding>())
            method.SetSilentExclusionReason("RmlUi logging helpers are outside the interface binding slice");

        var logMessage = systemInterface.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "LogMessage");
        var logMessageType = logMessage.Parameters.First(static parameter => parameter.NativeName == "type");
        var logType = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::Log::Type");
        logType.CppQualifiedName = "Rml::Log::Type";
        logType.ManagedName = "LogType";
        logType.ManagedNamespace = ManagedNamespace;

        logMessageType.Type = BindingType.CreateEnum(
            logType.CppQualifiedName, logType.ManagedName, enumId: logType.SymbolId);
        logMessageType.CppTypeName = logType.CppQualifiedName;

        var vector2 = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Vector2");
        vector2.Members.Clear();

        foreach (var function in compilation.Functions.Where(static function =>
                     function.Parent is not RecordBinding))
        {
            if (!RetainedFreeFunctions.Contains(function.CppQualifiedName))
            {
                function.SetSilentExclusionReason("RmlUi free function is outside the current binding slice");
                continue;
            }

            if (function.CppQualifiedName == "Rml::LoadFontFace" &&
                function.Parameters.FirstOrDefault()?.CppTypeName != "const Rml::String&")
            {
                function.SetSilentExclusionReason("RmlUi memory font loading is outside the current binding slice");
                continue;
            }

            if (function.CppQualifiedName.StartsWith("Rml::Debugger::", StringComparison.Ordinal))
                continue;
            if (function.CppQualifiedName == "Rml::ReleaseTextures")
            {
                function.Parameters[0].Type = BindingType.IntPtr;
                continue;
            }
            if (function.CppQualifiedName == "Rml::CreateContext")
            {
                function.ReturnType = function.ReturnType.WithManagedTypeFormatOverride(
                    $"{function.ReturnType.ManagedTypeName}?");
                foreach (var parameter in function.Parameters)
                {
                    if (parameter.NativeName == "dimensions")
                    {
                        parameter.Type = vector2iType;
                        parameter.CppTypeName = "Rml::Vector2<int>";
                    }
                    else if (parameter.NativeName is "render_interface" or "text_input_handler")
                    {
                        parameter.Type = BindingType.IntPtr;
                    }
                }
                continue;
            }

            if (function.CppQualifiedName is "Rml::Initialise" or "Rml::RemoveContext" or "Rml::Shutdown")
                continue;

            if (function.CppQualifiedName is "Rml::GetRenderInterface" or "Rml::SetRenderInterface")
            {
                if (function.NativeName.StartsWith("Get", StringComparison.Ordinal))
                    function.ReturnType = BindingType.IntPtr;
                else
                    function.Parameters[0].Type = BindingType.IntPtr;
                continue;
            }
            if (function.NativeName.StartsWith("Get", StringComparison.Ordinal))
            {
                function.ReturnType = function.ReturnType.WithManagedTypeFormatOverride(
                    $"{function.ReturnType.ManagedTypeName}?");
            }
            else
            {
                var parameter = function.Parameters[0];
                parameter.Type = parameter.Type.WithManagedTypeFormatOverride(
                    $"{parameter.Type.ManagedTypeName}?");
            }
        }
    }

    private static void ConfigureDataModelSurface(BindingCompilation compilation, RecordBinding context)
    {
        var variant = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Variant");
        variant.Bases.Clear();
        variant.ManagedBaseTypeName = null;
        foreach (var method in variant.Members.OfType<FunctionBinding>())
        {
            if (!(method.IsConstructor && !method.IsGeneric ||
                  method.NativeName is "Clear" or "GetType" ||
                  method.IsGeneric && method.NativeName == "Get"))
                method.SetSilentExclusionReason("RmlUi variant member is outside the data-model binding slice");
        }
        var variantBooleanConstructor = new FunctionBinding(
            "constructor:Rml::Variant:bool",
            "Variant",
            variant.ManagedName,
            variant.HandleType,
            "redbolt_rmlui_Rml_Variant_Variant_bool",
            cppQualifiedName: "Rml::Variant::Variant")
        {
            HeaderIncludePath = variant.HeaderIncludePath,
            IsConstructor = true,
            IsStatic = false,
            OwningRecordCppTypeName = variant.CppQualifiedName
        };
        variantBooleanConstructor.Parameters.Add(new ParameterBinding(
            "parameter:Rml::Variant::Variant:bool:value",
            "value",
            "value",
            BindingType.Boolean,
            cppTypeName: "bool"));
        variant.Members.Add(variantBooleanConstructor);

        var stringType = BindingType.CreateConfigured(
            "Rml::String",
            "global::Std.String",
            marshallerTypeName: null,
            managedMarshaller: "global::Std.__StringMarshaller<global::SharpCpp.Support.BorrowedNativeHandle>");
        var int64Type = BindingType.CreateConfigured(
            "int64_t", "long", BindingPrimitiveKind.Int64);
        var uint64Type = BindingType.CreateConfigured(
            "uint64_t", "ulong", BindingPrimitiveKind.UInt64);
        var vector2fType = BindingType.CreateConfigured(
            "Rml::Vector2f", "Vector2f",
            marshallerTypeName: null,
            managedMarshaller: "global::RedBolt.Math.Vector2fMarshaller")
            .WithCppIsRecordValue(true);
        var vector3fType = BindingType.CreateConfigured(
            "Rml::Vector3f", "Vector3f",
            marshallerTypeName: null,
            managedMarshaller: "global::RedBolt.Math.Vector3fMarshaller")
            .WithCppIsRecordValue(true);
        var vector4fType = BindingType.CreateConfigured(
            "Rml::Vector4f", "Vector4f",
            marshallerTypeName: null,
            managedMarshaller: "global::RedBolt.Math.Vector4fMarshaller")
            .WithCppIsRecordValue(true);
        var colourfType = BindingType.CreateConfigured(
            "Rml::Colourf", "global::RedBolt.Math.Color",
            marshallerTypeName: null,
            managedMarshaller: "global::RedBolt.Math.ColorMarshaller")
            .WithCppIsRecordValue(true);
        var colourbType = BindingType.CreateConfigured(
            "Rml::Colourb", "global::RedBolt.Bindings.RmlUi.RmlUiLegacyColourb")
            .WithCppIsRecordValue(true);
        var get = variant.Members.OfType<FunctionBinding>().First(static method =>
            method.IsGeneric && method.NativeName == "Get");
        foreach (var type in new[]
                 {
                     BindingType.Boolean,
                     BindingType.Int8,
                     BindingType.UInt8,
                     BindingType.Int32,
                     BindingType.UInt32,
                     int64Type,
                     uint64Type,
                     BindingType.Single,
                     BindingType.Double,
                     BindingType.IntPtr,
                     stringType,
                     vector2fType,
                     vector3fType,
                     vector4fType,
                     colourfType,
                     colourbType
                 })
        {
            get.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding([type]));
        }

        var assign = new FunctionBinding(
            "method:Rml::Variant::operator_assign:T",
            "operator=",
            "Assign",
            BindingType.CreateHandle("Rml::Variant&", "Variant", variant.SymbolId),
            entryPointName: "redbolt_rmlui_Rml_Variant_operator_assign_generic",
            cppReturnTypeName: "Rml::Variant&",
            cppQualifiedName: "Rml::Variant::operator=")
        {
            HeaderIncludePath = variant.HeaderIncludePath,
            IsStatic = false,
            IsConstMethod = false,
            OwningRecordCppTypeName = variant.CppQualifiedName
        };
        assign.GenericParameters.Add(new BindingGenericParameter("T", 0));
        assign.Parameters.Add(new ParameterBinding(
            "parameter:Rml::Variant::operator_assign:T:value",
            "value",
            "value",
            BindingType.CreateGenericParameter("T"),
            cppTypeName: "T&&"));
        foreach (var type in new[]
                 {
                     BindingType.Boolean,
                     BindingType.Int8,
                     BindingType.UInt8,
                     BindingType.Int32,
                     BindingType.UInt32,
                     int64Type,
                     uint64Type,
                     BindingType.Single,
                     BindingType.Double,
                     BindingType.IntPtr,
                     stringType,
                     vector2fType,
                     vector3fType,
                     vector4fType,
                     colourfType,
                     colourbType
                 })
        {
            assign.SupportedGenericInstantiations.Add(new RecordGenericInstantiationBinding(
                [type.WithCppTypeName($"const {type.CppTypeName}&").WithCppIsReference(true)]));
        }
        assign.SetMetadata(MetadataKeys.ExplicitManagedRename, true);
        variant.Members.Add(assign);

        var variantType = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::Variant::Type");
        variantType.ManagedName = "VariantType";
        variantType.ManagedNamespace = variant.ManagedNamespace;
        var getVariantType = variant.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "GetType");
        getVariantType.ManagedName = "GetVariantType";
        getVariantType.ReturnType = BindingType.CreateEnum(
            variantType.CppQualifiedName, variantType.ManagedName, enumId: variantType.SymbolId);

        foreach (var vector in compilation.Records.Where(static record =>
                     record.ManagedNamespace == "Std" && record.ManagedName == "Vector"))
        {
            for (var index = vector.SupportedGenericInstantiations.Count - 1; index >= 0; --index)
            {
                if (vector.SupportedGenericInstantiations[index].TypeArguments.Any(static type =>
                        type.CppTypeName.Contains("Rml::Variant", StringComparison.Ordinal)))
                    continue;
                vector.SupportedGenericInstantiations.RemoveAt(index);
            }
        }

        var constructor = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::DataModelConstructor");
        constructor.Bases.Clear();
        constructor.ManagedBaseTypeName = null;
        foreach (var method in constructor.Members.OfType<FunctionBinding>())
        {
            if (method.IsGeneric ||
                !(method.IsConstructor && method.Parameters.Count == 0 ||
                  method.NativeName is ("BindCustomDataVariable" or "BindEventCallback" or "BindFunc" or "GetDataTypeRegister" or
                      "GetModelHandle" or "RegisterTransformFunc")))
                method.SetSilentExclusionReason("RmlUi data-model constructor member is outside the functional binding slice");
        }
        var bindEventCallback = constructor.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "BindEventCallback");
        bindEventCallback.Parameters.First(static parameter =>
            parameter.NativeName == "event_func").Type = compilation.TypeAliases.First(static alias =>
            alias.CppQualifiedName == "Rml::DataEventFunc").TargetType;
        var bindFunc = constructor.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "BindFunc");
        bindFunc.Parameters.First(static parameter =>
            parameter.NativeName == "get_func").Type = compilation.TypeAliases.First(static alias =>
            alias.CppQualifiedName == "Rml::DataGetFunc").TargetType;
        bindFunc.Parameters.First(static parameter =>
            parameter.NativeName == "set_func").Type = compilation.TypeAliases.First(static alias =>
            alias.CppQualifiedName == "Rml::DataSetFunc").TargetType;
        var registerTransformFunc = constructor.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "RegisterTransformFunc");
        var transformFuncParameter = registerTransformFunc.Parameters.First(static parameter =>
            parameter.NativeName == "transform_func");
        var transformFuncAlias = compilation.TypeAliases.First(static alias =>
            alias.CppQualifiedName == "Rml::DataTransformFunc");
        var transformFuncType = transformFuncAlias.TargetType;
        var transformReturnType = transformFuncType.CallbackReturnType!
            .WithCppTypeName("Rml::Variant")
            .WithCppIsRecordValue(true);
        var projectedTransformFuncType = BindingType.CreateCallback(
            transformFuncType.CppTypeName,
            transformFuncType.Marshaller.PublicTypeName,
            transformFuncType.ManagedMarshaller,
            transformReturnType,
            transformFuncType.CallbackParameterTypes);
        if (transformFuncType.AliasCppQualifiedName is not null && transformFuncType.AliasNativeName is not null)
        {
            projectedTransformFuncType = projectedTransformFuncType.WithAliasSurface(
                transformFuncType.AliasCppQualifiedName, transformFuncType.AliasNativeName);
        }
        transformFuncParameter.Type = projectedTransformFuncType;
        for (var index = 0; index < compilation.TypeAliases.Count; ++index)
        {
            var alias = compilation.TypeAliases[index];
            if (alias.CppQualifiedName != "Rml::DataTransformFunc")
                continue;
            compilation.TypeAliases[index] = alias with
            {
                TargetType = BindingType.CreateCallback(
                    alias.TargetType.CppTypeName,
                    alias.TargetType.Marshaller.PublicTypeName,
                    alias.TargetType.ManagedMarshaller,
                    transformReturnType,
                    alias.TargetType.CallbackParameterTypes)
            };
            break;
        }

        var typeRegister = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::DataTypeRegister");
        typeRegister.Bases.Clear();
        typeRegister.ManagedBaseTypeName = null;
        foreach (var method in typeRegister.Members.OfType<FunctionBinding>())
        {
            if (!method.IsConstructor)
                method.SetSilentExclusionReason("RmlUi type register member is outside the component binding slice");
        }

        var variableDefinition = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::VariableDefinition");
        variableDefinition.Bases.Clear();
        variableDefinition.ManagedBaseTypeName = null;
        foreach (var method in variableDefinition.Members.OfType<FunctionBinding>())
        {
            if (!method.IsConstructor && method.NativeName is not ("Child" or "Get" or "Set" or "Size" or "Type"))
                method.SetSilentExclusionReason("RmlUi variable definition member is outside the data-model binding slice");

            foreach (var parameter in method.Parameters.Where(static parameter =>
                         parameter.CppTypeName is "void *" or "void*"))
            {
                parameter.Type = BindingType.IntPtr;
            }
        }
        var variableDefinitionChild = variableDefinition.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "Child");
        variableDefinitionChild.ReturnType = variableDefinitionChild.ReturnType
            .WithCppTypeName("Rml::DataVariable")
            .WithCppIsRecordValue(true);

        var dataVariable = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::DataVariable");
        dataVariable.Bases.Clear();
        dataVariable.ManagedBaseTypeName = null;
        foreach (var method in dataVariable.Members.OfType<FunctionBinding>())
        {
            if (!method.IsConstructor && method.NativeName is not ("Child" or "Get" or "Set" or "Size" or "Type"))
                method.SetSilentExclusionReason("RmlUi data variable member is outside the data-model binding slice");

            foreach (var parameter in method.Parameters.Where(static parameter =>
                         parameter.CppTypeName is "void *" or "void*"))
            {
                parameter.Type = BindingType.IntPtr;
            }
        }

        var dataAddressEntry = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::DataAddressEntry");
        dataAddressEntry.Bases.Clear();
        dataAddressEntry.ManagedBaseTypeName = null;
        foreach (var member in dataAddressEntry.Members)
        {
            if (member is not FieldBinding { NativeName: "name" or "index" })
                member.SetSilentExclusionReason("RmlUi data address member is outside the data-model binding slice");
        }

        var handle = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::DataModelHandle");
        handle.Bases.Clear();
        handle.ManagedBaseTypeName = null;
        foreach (var method in handle.Members.OfType<FunctionBinding>())
        {
            if (method.IsConstructor ||
                method.NativeName is not ("DirtyAllVariables" or "DirtyVariable" or "IsVariableDirty"))
                method.SetSilentExclusionReason("RmlUi data-model handle member is outside the synchronization binding slice");
        }

        foreach (var method in context.Members.OfType<FunctionBinding>())
        {
            if (method.NativeName is not ("CreateDataModel" or "GetDataModel" or "RemoveDataModel"))
                continue;
            foreach (var parameter in method.Parameters.Where(static parameter =>
                         parameter.NativeName == "data_type_register"))
            {
                parameter.Type = BindingType.CreateHandle(
                    "Rml::DataTypeRegister*", "DataTypeRegister", typeRegister.SymbolId);
            }
        }
    }

    private static int PruneSurface(BindingCompilation compilation)
    {
        {
            var rmlUiPrunedCount = 0;
            for (var index = compilation.Members.Count - 1; index >= 0; --index)
            {
                var member = compilation.Members[index];
                if (member is FunctionBinding function &&
                    RetainedFreeFunctions.Contains(function.CppQualifiedName))
                {
                    continue;
                }

                if (member is RecordBinding record &&
                    (record.CppQualifiedName is
                         "Rml::Box" or "Rml::Context" or "Rml::DataModelConstructor" or
                         "Rml::DataModelHandle" or "Rml::DataTypeRegister" or "Rml::DataVariable" or
                         "Rml::DataAddressEntry" or "Rml::VariableDefinition" or "Rml::Element" or
                         "Rml::Event" or "Rml::NumericValue" or "Rml::ScrollIntoViewOptions" or "Rml::Variant" or
                         "Rml::ContextInstancer" or "Rml::ElementDocument" or "Rml::EventListener" or
                         "Rml::EventListenerInstancer" or "Rml::FileInterface" or
                         "Rml::Factory" or "Rml::FontEngineInterface" or "Rml::Log" or "Rml::Property" or
                         "Rml::Plugin" or "Rml::RenderInterfaceCompatibility" or "Rml::SystemInterface" or
                         "Rml::ComputedValues" or "Rml::Style::ComputedValues" or
                         "RedBoltNative::RmlHarfBuzzFontEngine" ||
                     record.CppQualifiedName.StartsWith("Rml::Vector2", StringComparison.Ordinal) ||
                     record.ManagedNamespace == "Std" && record.ManagedName is "String" or "UnorderedMap" or "Vector"))
                {
                    continue;
                }

                if (member is EnumBinding @enum &&
                    @enum.CppQualifiedName is
                        "Rml::BoxArea" or "Rml::EventPhase" or "Rml::FocusFlag" or "Rml::Log::Type" or
                        "Rml::DataVariableType" or "Rml::ModalFlag" or "Rml::PropertyId" or "Rml::Variant::Type" or
                        "Rml::Input::KeyIdentifier" or "Rml::Input::KeyModifier" or
                        "Rml::ScrollAlignment" or "Rml::ScrollBehavior" or "Rml::ScrollParentage" or
                        "Rml::Style::Focus" or "Rml::Style::FontWeight" or "Rml::Style::TabIndex" or "Rml::Unit")
                    continue;

                compilation.Members.RemoveAt(index);
                rmlUiPrunedCount++;
            }

            for (var index = compilation.TypeAliases.Count - 1; index >= 0; --index)
            {
                if (compilation.TypeAliases[index].CppQualifiedName is
                    "Rml::DataEventFunc" or "Rml::DataGetFunc" or "Rml::DataSetFunc" or "Rml::DataTransformFunc" or
                    "Rml::VariantList" or "Rml::Vector2f")
                    continue;

                compilation.TypeAliases.RemoveAt(index);
                rmlUiPrunedCount++;
            }

            compilation.InvalidateCaches();
            return rmlUiPrunedCount;
        }
    }

    private static GeneratedBindingSet AddLegacyAbi(GeneratedBindingSet bindings)
    {
        var files = bindings.Files.Select(static file =>
            file.RelativePath.EndsWith(".g.cpp", StringComparison.Ordinal)
                ? file with
                {
                    Content = file.Content +
                        """

                        #include <cstddef>
                        #include <type_traits>

                        static_assert(std::is_standard_layout_v<Rml::Vector2f>);
                        static_assert(sizeof(Rml::Vector2f) == sizeof(float) * 2);
                        static_assert(offsetof(Rml::Vector2f, x) == 0);
                        static_assert(offsetof(Rml::Vector2f, y) == sizeof(float));
                        static_assert(std::is_standard_layout_v<Rml::Vector3f>);
                        static_assert(sizeof(Rml::Vector3f) == sizeof(float) * 3);
                        static_assert(offsetof(Rml::Vector3f, x) == 0);
                        static_assert(offsetof(Rml::Vector3f, y) == sizeof(float));
                        static_assert(offsetof(Rml::Vector3f, z) == sizeof(float) * 2);
                        static_assert(std::is_standard_layout_v<Rml::Vector4f>);
                        static_assert(sizeof(Rml::Vector4f) == sizeof(float) * 4);
                        static_assert(offsetof(Rml::Vector4f, x) == 0);
                        static_assert(offsetof(Rml::Vector4f, y) == sizeof(float));
                        static_assert(offsetof(Rml::Vector4f, z) == sizeof(float) * 2);
                        static_assert(offsetof(Rml::Vector4f, w) == sizeof(float) * 3);
                        static_assert(std::is_standard_layout_v<Rml::Vector2i>);
                        static_assert(sizeof(Rml::Vector2i) == sizeof(int) * 2);
                        static_assert(offsetof(Rml::Vector2i, x) == 0);
                        static_assert(offsetof(Rml::Vector2i, y) == sizeof(int));
                        static_assert(std::is_standard_layout_v<Rml::Colourf>);
                        static_assert(sizeof(Rml::Colourf) == sizeof(float) * 4);
                        static_assert(offsetof(Rml::Colourf, red) == 0);
                        static_assert(offsetof(Rml::Colourf, green) == sizeof(float));
                        static_assert(offsetof(Rml::Colourf, blue) == sizeof(float) * 2);
                        static_assert(offsetof(Rml::Colourf, alpha) == sizeof(float) * 3);
                        static_assert(std::is_standard_layout_v<Rml::Colourb>);
                        static_assert(sizeof(Rml::Colourb) == sizeof(unsigned char) * 4);
                        static_assert(offsetof(Rml::Colourb, red) == 0);
                        static_assert(offsetof(Rml::Colourb, green) == sizeof(unsigned char));
                        static_assert(offsetof(Rml::Colourb, blue) == sizeof(unsigned char) * 2);
                        static_assert(offsetof(Rml::Colourb, alpha) == sizeof(unsigned char) * 3);
                        static_assert(std::is_standard_layout_v<Rml::Vertex>);
                        static_assert(sizeof(Rml::Vertex) == 20);
                        static_assert(offsetof(Rml::Vertex, position) == 0);
                        static_assert(offsetof(Rml::Vertex, colour) == 8);
                        static_assert(offsetof(Rml::Vertex, tex_coord) == 12);
                        static_assert(std::is_standard_layout_v<Rml::Matrix4f>);
                        static_assert(sizeof(Rml::Matrix4f) == sizeof(float) * 16);
                        """
                }
                : file).ToList();
        files.Add(new GeneratedBindingFile(
            "RedBolt.Bindings.RmlUi.LegacyAbi.g.cs",
            """
            // <auto-generated />
            #nullable enable
            namespace RedBolt.Bindings.RmlUi;

            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;

            [StructLayout(LayoutKind.Sequential)]
            public struct RmlUiLegacyVector2f
            {
                public float X;
                public float Y;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RmlUiLegacyVector2i
            {
                public int X;
                public int Y;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RmlUiLegacyColourb
            {
                public byte Red;
                public byte Green;
                public byte Blue;
                public byte Alpha;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RmlUiLegacyVertex
            {
                public RmlUiLegacyVector2f Position;
                public uint Colour;
                public RmlUiLegacyVector2f TexCoord;
            }

            [InlineArray(16)]
            public struct RmlUiLegacyMatrix4f
            {
                private float _element0;
            }
            """));
        return new GeneratedBindingSet(files);
    }
}
