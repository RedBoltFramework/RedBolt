using SharpCpp.Model.Ast;
using SharpCpp.Rbfx.Generator;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class RbfxFlagSetLoweringPassTests
{
    [Fact]
    public void Apply_lowers_flagset_aliases_and_surface_types_to_flags_enum()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/All.h"]);
        var flagSetRecord = new RecordBinding("record:FlagSet", "FlagSet", "FlagSet", "Urho3D::FlagSet")
        {
            ManagedNamespace = "Urho3D"
        };
        var unsupportedGenericRecord = new RecordBinding("record:ValueWithDerivative", "ValueWithDerivative", "ValueWithDerivative", "Urho3D::ValueWithDerivative")
        {
            ManagedNamespace = "Urho3D"
        };
        unsupportedGenericRecord.GenericParameters.Add(new BindingGenericParameter("T", 0));
        unsupportedGenericRecord.SetIgnoreReason("generic record 'ValueWithDerivative' does not support field projection in this slice");
        var enumBinding = new EnumBinding("enum:RenderFlag", "RenderFlag", "RenderFlag", cppQualifiedName: "Urho3D::RenderFlag")
        {
            ManagedNamespace = "Urho3D"
        };
        compilation.Members.Add(flagSetRecord);
        compilation.Members.Add(unsupportedGenericRecord);
        compilation.Members.Add(enumBinding);

        var enumArgument = BindingType.CreateEnum("RenderFlag", "global::Urho3D.RenderFlag", enumId: enumBinding.SymbolId);
        var flagSetType = BindingType.CreateHandle(
            "Urho3D::FlagSet<RenderFlag>*",
            "global::Urho3D.FlagSet<global::Urho3D.RenderFlag>",
            flagSetRecord.SymbolId,
            genericArguments: [enumArgument]);
        compilation.TypeAliases.Add(new BindingTypeAlias("Urho3D::RenderFlags", "RenderFlags", "RenderFlags", flagSetType)
        {
            ManagedNamespace = "Urho3D"
        });
        compilation.TypeAliases.Add(new BindingTypeAlias(
            "Urho3D::PositionAndVelocity",
            "PositionAndVelocity",
            "PositionAndVelocity",
            BindingType.CreateHandle(
                "ValueWithDerivative<DoubleVector3>*",
                "global::Urho3D.ValueWithDerivative<global::Urho3D.Math.DoubleVector3>",
                unsupportedGenericRecord.SymbolId,
                genericArguments: [BindingType.CreateConfigured("Urho3D::DoubleVector3", "global::Urho3D.Math.DoubleVector3", "global::Urho3D.Math.DoubleVector3")])));

        var function = new FunctionBinding("function:SetRenderFlags", "SetRenderFlags", "SetRenderFlags", flagSetType, cppReturnTypeName: "Urho3D::FlagSet<RenderFlag>");
        function.Parameters.Add(new ParameterBinding("parameter:flags", "flags", "flags", flagSetType, cppTypeName: "Urho3D::FlagSet<RenderFlag>"));
        compilation.Members.Add(function);

        var owner = new RecordBinding("record:Owner", "Owner", "Owner", "Urho3D::Owner");
        owner.Members.Add(new FieldBinding("field:flags", "flags_", "Flags", flagSetType, "Urho3D::FlagSet<RenderFlag>", owner.CppQualifiedName));
        compilation.Members.Add(owner);

        RbfxFlagSetLoweringPass.Apply(compilation);

        Assert.True(enumBinding.IsFlags);
        Assert.True(flagSetRecord.GetMetadata<bool>(MetadataKeys.SuppressEmission));
        var alias = Assert.Single(compilation.TypeAliases);
        Assert.Equal("RenderFlags", alias.ManagedName);
        Assert.Equal("global::Urho3D.RenderFlag", alias.TargetType.ManagedTypeName);
        Assert.Equal("global::Urho3D.RenderFlag", function.ReturnType.ManagedTypeName);
        Assert.Equal("global::Urho3D.RenderFlag", function.Parameters.Single().Type.ManagedTypeName);
        Assert.Equal("global::Urho3D.RenderFlag", owner.Members.OfType<FieldBinding>().Single().Type.ManagedTypeName);
    }

    [Fact]
    public void Apply_does_not_rescue_abstract_record_constructors_after_flagset_lowering()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/All.h"]);
        var flagSetRecord = new RecordBinding("record:FlagSet", "FlagSet", "FlagSet", "Urho3D::FlagSet")
        {
            ManagedNamespace = "Urho3D"
        };
        var enumBinding = new EnumBinding("enum:DrawableFlag", "DrawableFlag", "DrawableFlag", cppQualifiedName: "Urho3D::DrawableFlag")
        {
            ManagedNamespace = "Urho3D"
        };
        var abstractRecord = new RecordBinding("record:Drawable", "Drawable", "Drawable", "Urho3D::Drawable")
        {
            IsAbstract = true,
            ManagedNamespace = "Urho3D"
        };

        compilation.Members.Add(flagSetRecord);
        compilation.Members.Add(enumBinding);
        compilation.Members.Add(abstractRecord);

        var enumArgument = BindingType.CreateEnum("DrawableFlag", "global::Urho3D.DrawableFlag", enumId: enumBinding.SymbolId);
        var flagSetType = BindingType.CreateHandle(
            "Urho3D::FlagSet<DrawableFlag>*",
            "global::Urho3D.FlagSet<global::Urho3D.DrawableFlag>",
            flagSetRecord.SymbolId,
            genericArguments: [enumArgument]);
        var constructor = new FunctionBinding(
            "function:Drawable_ctor",
            "Drawable",
            ".ctor",
            abstractRecord.HandleType,
            cppReturnTypeName: "Urho3D::Drawable*")
        {
            IsConstructor = true
        };
        constructor.Parameters.Add(new ParameterBinding("parameter:flags", "flags", "flags", flagSetType, cppTypeName: "Urho3D::FlagSet<DrawableFlag>"));
        constructor.SetIgnoreReason("abstract record constructors cannot be emitted");
        abstractRecord.Members.Add(constructor);

        RbfxFlagSetLoweringPass.Apply(compilation);

        Assert.True(constructor.IsIgnored());
        Assert.Equal("global::Urho3D.DrawableFlag", constructor.Parameters.Single().Type.ManagedTypeName);
    }
}
