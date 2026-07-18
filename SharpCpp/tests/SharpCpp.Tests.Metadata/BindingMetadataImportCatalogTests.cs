using SharpCpp.Metadata.Export;
using SharpCpp.Metadata.Import;
using SharpCpp.Model.Ast;
using Xunit;

namespace SharpCpp.Tests.Metadata;

public sealed class BindingMetadataImportCatalogTests
{
    [Fact]
    public void Load_seeds_suppressed_imported_records_with_qualified_managed_names()
    {
        var document = new BindingMetadataDocument(
            SchemaVersion: BindingMetadataSchema.CurrentVersion,
            GeneratorVersion: BindingMetadataSchema.PreviewGeneratorVersion,
            SupportRuntimeVersion: BindingMetadataSchema.PreviewSupportRuntimeVersion,
            Source: new BindingMetadataSource("SharpCpp.Rbfx.Bindings", "Urho3D", "SharpCpp.Rbfx.Native"),
            Imports: Array.Empty<BindingMetadataImport>(),
            Fingerprint: new BindingMetadataFingerprint("phase2", "clang", "attrs", ["Urho3D/LogicComponent.h"]),
            Types:
            [
                new BindingMetadataType(
                    SymbolId: "record:LogicComponent",
                    Kind: "handle",
                    NativeName: "LogicComponent",
                    ManagedName: "LogicComponent",
                    ManagedNamespace: "Urho3D",
                    ManagedBaseTypeName: "Component",
                    Projection: new BindingMetadataTypeReference("handle", "Urho3D::LogicComponent*", "LogicComponent", null, "record:LogicComponent", null),
                    ManagedInstanceCacheFactoryExpression: "global::Urho3D.Interop.RefCountedInstanceCache.Shared",
                    ManagedInstanceCacheTypeName: "global::SharpCpp.Support.INativeInstanceCache",
                    EmitDefaultNativeReleaseOverride: false)
            ],
            Functions: Array.Empty<BindingMetadataFunction>(),
            Aliases:
            [
                new BindingMetadataAlias(
                    CppQualifiedName: "Urho3D::LogicComponentRef",
                    NativeName: "LogicComponentRef",
                    ManagedName: "LogicComponentRef",
                    ManagedNamespace: "Urho3D",
                    TargetType: new BindingMetadataTypeReference(
                        "handle",
                        "Urho3D::LogicComponent*",
                        "global::Urho3D.LogicComponent",
                        null,
                        "record:LogicComponent",
                        null))
            ]);

        var tempFile = Path.Combine(Path.GetTempPath(), $"SharpCpp.Tests.Metadata.{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempFile, BindingMetadataSerializer.Serialize(document));
            var catalog = BindingMetadataImportCatalog.Load([tempFile]);

            var compilation = new BindingCompilation("Consumer", "RbfxUserCode", "Bindings", "Consumer.Native", ["RbfxUserCode/RotatingComponent.hpp"]);
            var recordsByName = new Dictionary<string, RecordBinding>(StringComparer.Ordinal);
            var enumsByName = new Dictionary<string, EnumBinding>(StringComparer.Ordinal);

            catalog.Seed(compilation, recordsByName, enumsByName);

            var importedRecord = Assert.Single(compilation.Records);
            Assert.Equal("LogicComponent", importedRecord.NativeName);
            Assert.Equal("global::Urho3D.LogicComponent", importedRecord.ManagedName);
            Assert.Equal("global::Urho3D.Component", importedRecord.ManagedBaseTypeName);
            Assert.Equal("global::Urho3D.Interop.RefCountedInstanceCache.Shared", importedRecord.ManagedInstanceCacheFactoryExpression);
            Assert.Equal("global::SharpCpp.Support.INativeInstanceCache", importedRecord.ManagedInstanceCacheTypeName);
            Assert.False(importedRecord.EmitDefaultNativeReleaseOverride);
            Assert.True(importedRecord.GetMetadata<bool>(MetadataKeys.SuppressEmission));
            Assert.Same(importedRecord, recordsByName["LogicComponent"]);
            Assert.Same(importedRecord, recordsByName["Urho3D::LogicComponent"]);
            var importedAlias = Assert.Single(compilation.TypeAliases);
            Assert.Equal("LogicComponentRef", importedAlias.ManagedName);
            Assert.Equal("Urho3D", importedAlias.ManagedNamespace);
            Assert.Equal(importedRecord.SymbolId, importedAlias.TargetType.RecordId);
            Assert.Equal("global::Urho3D.LogicComponent", importedAlias.TargetType.ManagedTypeName);
            Assert.Equal("SharpCpp.Rbfx.Bindings", Assert.Single(catalog.Imports).Package);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Load_preserves_generic_projection_shape_for_imported_alias_targets()
    {
        var document = new BindingMetadataDocument(
            SchemaVersion: BindingMetadataSchema.CurrentVersion,
            GeneratorVersion: BindingMetadataSchema.PreviewGeneratorVersion,
            SupportRuntimeVersion: BindingMetadataSchema.PreviewSupportRuntimeVersion,
            Source: new BindingMetadataSource("SharpCpp.Rbfx.Bindings", "Urho3D", "SharpCpp.Rbfx.Native"),
            Imports: Array.Empty<BindingMetadataImport>(),
            Fingerprint: new BindingMetadataFingerprint("phase2", "clang", "attrs", ["Urho3D/Math/NumericRange.h"]),
            Types:
            [
                new BindingMetadataType(
                    SymbolId: "record:NumericRange",
                    Kind: "handle",
                    NativeName: "NumericRange",
                    ManagedName: "NumericRange",
                    ManagedNamespace: "Urho3D",
                    ManagedBaseTypeName: null,
                    Projection: new BindingMetadataTypeReference(
                        "handle",
                        "Urho3D::NumericRange<T>*",
                        "NumericRange<T>",
                        null,
                        "record:NumericRange",
                        null,
                        [new BindingMetadataTypeReference("genericparameter", "T", "T", null, null, null)]))
            ],
            Functions: Array.Empty<BindingMetadataFunction>(),
            Aliases:
            [
                new BindingMetadataAlias(
                    CppQualifiedName: "Urho3D::FloatRange",
                    NativeName: "FloatRange",
                    ManagedName: "FloatRange",
                    ManagedNamespace: "Urho3D",
                    TargetType: new BindingMetadataTypeReference(
                        "handle",
                        "Urho3D::NumericRange<float>*",
                        "global::Urho3D.NumericRange<float>",
                        null,
                        "record:NumericRange",
                        null,
                        [new BindingMetadataTypeReference("primitive", "float", "float", "single", null, null)]))
            ]);

        var tempFile = Path.Combine(Path.GetTempPath(), $"SharpCpp.Tests.Metadata.{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempFile, BindingMetadataSerializer.Serialize(document));
            var catalog = BindingMetadataImportCatalog.Load([tempFile]);

            var compilation = new BindingCompilation("Consumer", "RbfxUserCode", "Bindings", "Consumer.Native", ["RbfxUserCode/RotatingComponent.hpp"]);
            var recordsByName = new Dictionary<string, RecordBinding>(StringComparer.Ordinal);
            var enumsByName = new Dictionary<string, EnumBinding>(StringComparer.Ordinal);

            catalog.Seed(compilation, recordsByName, enumsByName);

            var importedRecord = Assert.Single(compilation.Records);
            var importedAlias = Assert.Single(compilation.TypeAliases);
            Assert.Single(importedRecord.GenericParameters);
            Assert.Equal("global::Urho3D.NumericRange<{0}>", importedRecord.ManagedTypeFormat);
            Assert.Equal("global::Urho3D.NumericRange<float>", importedAlias.TargetType.ManagedTypeName);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Load_preserves_callback_alias_target_metadata()
    {
        var document = new BindingMetadataDocument(
            SchemaVersion: BindingMetadataSchema.CurrentVersion,
            GeneratorVersion: BindingMetadataSchema.PreviewGeneratorVersion,
            SupportRuntimeVersion: BindingMetadataSchema.PreviewSupportRuntimeVersion,
            Source: new BindingMetadataSource("SharpCpp.Rbfx.Bindings", "Urho3D", "SharpCpp.Rbfx.Native"),
            Imports: Array.Empty<BindingMetadataImport>(),
            Fingerprint: new BindingMetadataFingerprint("phase2", "clang", "attrs", ["Callbacks.h"]),
            Types: Array.Empty<BindingMetadataType>(),
            Functions: Array.Empty<BindingMetadataFunction>(),
            Aliases:
            [
                new BindingMetadataAlias(
                    CppQualifiedName: "Urho3D::CallbackAlias",
                    NativeName: "CallbackAlias",
                    ManagedName: "CallbackAlias",
                    ManagedNamespace: "Urho3D",
                    TargetType: new BindingMetadataTypeReference(
                        Kind: "primitive",
                        CppType: "std::function<void(int)>",
                        PublicType: "global::System.Action<int>",
                        Primitive: "unsupported",
                        RecordId: null,
                        MarshallerType: null,
                        ManagedMarshaller: "global::Urho3D.CallbackMarshaller",
                        IsCallback: true,
                        CallbackReturnType: new BindingMetadataTypeReference("primitive", "void", "void", "void", null, null),
                        CallbackParameterTypes: [new BindingMetadataTypeReference("primitive", "int", "int", "int32", null, null)]))
            ]);

        var tempFile = Path.Combine(Path.GetTempPath(), $"SharpCpp.Tests.Metadata.{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempFile, BindingMetadataSerializer.Serialize(document));
            var catalog = BindingMetadataImportCatalog.Load([tempFile]);

            var compilation = new BindingCompilation("Consumer", "RbfxUserCode", "Bindings", "Consumer.Native", ["Callbacks.h"]);
            var recordsByName = new Dictionary<string, RecordBinding>(StringComparer.Ordinal);
            var enumsByName = new Dictionary<string, EnumBinding>(StringComparer.Ordinal);

            catalog.Seed(compilation, recordsByName, enumsByName);

            var alias = Assert.Single(compilation.TypeAliases);
            Assert.True(alias.TargetType.IsCallback);
            Assert.Equal("global::System.Action<int>", alias.TargetType.ManagedTypeName);
            Assert.Equal("global::Urho3D.CallbackMarshaller", alias.TargetType.ManagedMarshaller);
            Assert.Equal("void", alias.TargetType.CallbackReturnType!.ManagedTypeName);
            Assert.Equal("int", Assert.Single(alias.TargetType.CallbackParameterTypes).ManagedTypeName);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
