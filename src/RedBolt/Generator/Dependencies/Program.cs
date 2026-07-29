namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCpp.Metadata.Export;
using SharpCpp.Model.Ast;
using SharpCpp.Model.Logging;
using SharpCpp.Parsing;
using SharpCpp.Pipeline;
using SharpCpp.Rendering;

internal static class Program
{
    private static readonly string[] ParseCompatibilityDefines =
    [
        "SDL_DISABLE_MM3DNOW_H=1",
        "SDL_DISABLE_LSX_H=1",
        "SDL_DISABLE_LASX_H=1",
        "SDL_DISABLE_IMMINTRIN_H=1",
        "SDL_DISABLE_MMINTRIN_H=1",
        "SDL_DISABLE_XMMINTRIN_H=1",
        "SDL_DISABLE_EMMINTRIN_H=1",
        "SDL_DISABLE_PMMINTRIN_H=1"
    ];

    private static readonly string[] ParseSimdDisableArguments =
    [
        "-mno-sse",
        "-mno-sse2",
        "-mno-sse3",
        "-mno-ssse3",
        "-mno-sse4.1",
        "-mno-sse4.2",
        "-mno-avx",
        "-mno-avx2",
        "-mno-avx512f",
        "-mno-avx512bw",
        "-mno-avx512cd",
        "-mno-avx512dq",
        "-mno-avx512vl",
        "-U__SSE__",
        "-U__x86_64__"
    ];

    private static readonly string[] ParseWarningDisableArguments =
    [
        "-Wno-deprecated-literal-operator",
        "-Wno-pragma-once-outside-header",
        "-Wno-unknown-attributes"
    ];

    private static readonly HashSet<string> RetainedDependencyFunctions = new(StringComparer.Ordinal)
    {
        "SDL_GL_GetDrawableSize",
        "SDL_GetVersion",
        "SDL_Init",
        "SDL_InitSubSystem",
        "SDL_Quit",
        "SDL_QuitSubSystem",
        "SDL_WasInit"
    };

    private static readonly HashSet<string> RetainedDependencyRecords = new(StringComparer.Ordinal)
    {
        "SDL_version"
    };

    private static readonly HashSet<string> RetainedLz4Functions = new(StringComparer.Ordinal)
    {
        "LZ4_compressBound",
        "LZ4_compress_HC",
        "LZ4_decompress_fast",
        "LZ4_versionNumber"
    };

    private static readonly HashSet<string> RetainedHarfBuzzFunctions = new(StringComparer.Ordinal)
    {
        "hb_blob_create_from_file_or_fail",
        "hb_blob_destroy",
        "hb_face_create_or_fail",
        "hb_face_destroy",
        "hb_face_get_upem",
        "hb_font_create",
        "hb_font_destroy",
        "hb_font_get_glyph",
        "hb_font_get_glyph_extents",
        "hb_font_get_glyph_h_advance",
        "hb_font_set_scale",
        "hb_ot_font_set_funcs",
        "hb_raster_draw_create_or_fail",
        "hb_raster_draw_destroy",
        "hb_raster_draw_glyph_or_fail",
        "hb_raster_draw_render",
        "hb_raster_draw_reset",
        "hb_raster_draw_set_glyph_extents",
        "hb_raster_image_destroy",
        "hb_raster_image_get_buffer",
        "hb_raster_image_get_extents",
        "hb_raster_image_get_format"
    };

    private static readonly HashSet<string> RetainedHarfBuzzRecords = new(StringComparer.Ordinal)
    {
        "hb_blob_t",
        "hb_face_t",
        "hb_font_t",
        "hb_glyph_extents_t",
        "hb_raster_draw_t",
        "hb_raster_extents_t",
        "hb_raster_image_t"
    };

    private static readonly HashSet<string> RetainedHarfBuzzEnums = new(StringComparer.Ordinal)
    {
        "hb_memory_mode_t",
        "hb_raster_format_t"
    };

    private static readonly HashSet<string> RetainedHarfBuzzAliases = new(StringComparer.Ordinal)
    {
        "hb_bool_t",
        "hb_codepoint_t",
        "hb_position_t"
    };

    private static readonly HashSet<string> RmlUiDirectorRecordCppTypeNames = new(StringComparer.Ordinal)
    {
        "Rml::EventListener",
        "Rml::FileInterface",
        "Rml::FontEngineInterface",
        "Rml::RenderInterfaceCompatibility",
        "Rml::SystemInterface"
    };

    private static readonly HashSet<string> RetainedDiligentFunctions = new(StringComparer.Ordinal)
    {
        "GetEngineFactoryOpenGL",
        "GetEngineFactoryVk",
        "InstallDebugMessageCallback",
        "CreateTemporaryGLProgram",
        "CreateProxySwapChainGL",
        "CreateSecondaryProxySwapChainGL",
        "CreateProxySwapChainMS",
        "GetVulkanSurfaceExtent",
        "ReleaseTemporaryGLProgramHandle",
        "DeleteTemporaryGLProgram",
        "GetTemporaryGLProgramVertexAttributeCount",
        "GetTemporaryGLProgramVertexAttributeName",
        "GetTemporaryGLProgramVertexAttributeLocation",
        "BindTemporaryGLProgramVertexAttributes",
        "GetTemporaryGLProgramUniformBlockCount",
        "GetTemporaryGLProgramUniformBlockName",
        "GetTemporaryGLProgramUniformBlockDataSize",
        "GetTemporaryGLProgramUniformBlockIndex",
        "GetTemporaryGLProgramUniformCount",
        "GetTemporaryGLProgramUniformName",
        "GetTemporaryGLProgramUniformBlockOffset",
        "GetTemporaryGLProgramUniformSize",
        "GetShaderCodeBufferVariableAt",
        "GetTextureFormatAttribs"
    };

    private static readonly HashSet<string> RetainedWebPFunctions = new(StringComparer.Ordinal)
    {
        "WebPGetFeaturesInternal",
        "WebPDecodeRGBInto",
        "WebPDecodeRGBAInto",
        "WebPConfigInitInternal",
        "WebPPictureInitInternal",
        "WebPPictureImportRGB",
        "WebPPictureImportRGBA",
        "WebPPictureFree",
        "WebPMemoryWriterInit",
        "WebPMemoryWrite",
        "WebPMemoryWriterClear",
        "WebPEncode"
    };

    private static readonly Dictionary<string, HashSet<string>> RetainedWebPRecordFields = new(StringComparer.Ordinal)
    {
        ["WebPBitstreamFeatures"] = new(StringComparer.Ordinal) { "width", "height", "has_alpha" },
        ["WebPConfig"] = new(StringComparer.Ordinal) { "lossless", "exact" },
        ["WebPPicture"] = new(StringComparer.Ordinal) { "use_argb", "width", "height", "writer", "custom_ptr", "error_code" },
        ["WebPMemoryWriter"] = new(StringComparer.Ordinal) { "mem", "size" }
    };

    private static readonly HashSet<string> RetainedWebPEnums = new(StringComparer.Ordinal)
    {
        "VP8StatusCode",
        "WebPEncodingError",
        "WebPPreset"
    };

    private static readonly HashSet<string> RetainedXAtlasFunctions = new(StringComparer.Ordinal)
    {
        "Create",
        "Destroy",
        "AddMesh",
        "AddMeshJoin",
        "Generate"
    };

    private static readonly HashSet<string> RetainedXAtlasEnums = new(StringComparer.Ordinal)
    {
        "AddMeshError",
        "IndexFormat"
    };

    private static readonly Dictionary<string, HashSet<string>> RetainedXAtlasRecordFields = new(StringComparer.Ordinal)
    {
        ["Vertex"] = new(StringComparer.Ordinal) { "uv", "xref" },
        ["Mesh"] = new(StringComparer.Ordinal) { "indexArray", "indexCount", "Vertices", "NumVertices" },
        ["Atlas"] = new(StringComparer.Ordinal) { "width", "height", "NumMeshes", "Meshes" },
        ["MeshDecl"] = new(StringComparer.Ordinal)
        {
            "vertexCount",
            "vertexPositionData",
            "vertexPositionStride",
            "vertexNormalData",
            "vertexNormalStride",
            "indexCount",
            "indexData",
            "indexFormat"
        },
        ["ChartOptions"] = new(StringComparer.Ordinal),
        ["PackOptions"] = new(StringComparer.Ordinal) { "padding", "texelsPerUnit" }
    };

    private static readonly HashSet<string> UnsupportedGlslangFunctions = new(StringComparer.Ordinal)
    {
        "glslang_shader_set_resource_set_binding"
    };

    private static readonly HashSet<string> RetainedDiligentBaseInterfaceMethods = new(StringComparer.Ordinal)
    {
        "AddRef",
        "Release"
    };

    private static readonly IReadOnlyDictionary<string, string> DiligentQueryableInterfaces =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Diligent::IEngineFactoryVk"] = "Diligent::IID_EngineFactoryVk",
            ["Diligent::IDeviceContextGL"] = "Diligent::IID_DeviceContextGL",
            ["Diligent::ISwapChainVk"] = "Diligent::IID_SwapChainVk",
            ["Diligent::IRenderDeviceVk"] = "Diligent::IID_RenderDeviceVk",
            ["Diligent::IRenderDeviceD3D11"] = "Diligent::IID_RenderDeviceD3D11",
            ["Diligent::IRenderDeviceD3D12"] = "Diligent::IID_RenderDeviceD3D12",
            ["Diligent::IRenderDeviceGL"] = "Diligent::IID_RenderDeviceGL",
            ["Diligent::IRenderDeviceGLES"] = "Diligent::IID_RenderDeviceGLES"
        };

    private static int Main(string[] args)
    {
        StreamWriter? logWriter = null;

        try
        {
            var options = GeneratorOptions.Parse(args);
            var descriptor = BindingSurfaceDescriptor.Create(options.Surface, options.NativeLibraryName);
            Directory.CreateDirectory(options.OutputDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(options.LogPath)!);
            logWriter = new StreamWriter(options.LogPath, false);
            SharpCppLog.Configure(fileWriter: logWriter);

            var clangArguments = BuildClangArguments(options, descriptor);
            var marshallerClangArguments = BuildMarshallerClangArguments(clangArguments, descriptor);
            var marshallerHeaders = BuildMarshallerHeaders(options, descriptor);

            var parser = new ClangSharpHeaderParser();
            var compilation = parser.Parse(new ClangSharpParseRequest(
                ProjectName: descriptor.ProjectName,
                ManagedNamespace: descriptor.ManagedNamespace,
                ManagedClassName: descriptor.ManagedClassName,
                NativeLibraryName: descriptor.NativeLibraryName,
                    HeaderFilePath: options.HeaderPath,
                    HeaderIncludePath: options.HeaderIncludePath,
                    ClangArguments: clangArguments,
                    MarshallerConfigurationFiles: marshallerHeaders,
                    EntryPointPrefix: descriptor.EntryPointPrefix,
                    OwnedFileRoots: options.OwnedHeaders,
                    MarshallerClangArguments: marshallerClangArguments,
                    DirectorRecordCppTypeNames: descriptor.Surface == "rmlui"
                        ? RmlUiDirectorRecordCppTypeNames
                        : null));

            compilation.NamespaceMappings = descriptor.Surface switch
            {
                "diligent" => new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Diligent"] = descriptor.ManagedNamespace
                },
                "xatlas" => new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["xatlas"] = descriptor.ManagedNamespace
                },
                "rmlui" => new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Rml"] = descriptor.ManagedNamespace
                },
                _ => new Dictionary<string, string>(StringComparer.Ordinal)
            };

            foreach (var marshallerHeader in marshallerHeaders)
                compilation.NativeSupportHeaders.Add(marshallerHeader.Replace('\\', '/'));

            MarkNativeInterfaceWrappers(compilation, descriptor);
            BindingCompilationBootstrapPipeline.Apply(compilation, marshallerHeaders, clangArguments, marshallerClangArguments);
            FixImGuiFunctions(compilation, descriptor);
            FixCDependencyTypes(compilation, descriptor);
            FixDiligentBlendFactors(compilation, descriptor);
            FixDiligentTextureFormatFunction(compilation, descriptor);
            FixDiligentVulkanImageHandle(compilation, descriptor);
            FixSdlVoidPointerFunctions(compilation, descriptor);
            FixStbVorbisBufferFunctions(compilation, descriptor);
            FixHarfBuzzTypes(compilation, descriptor);
            FixRmlUiSurface(compilation, descriptor);
            FixXAtlasTypes(compilation, descriptor);
            if (descriptor.Surface == "webp")
            {
                compilation.Functions.First(static function => function.NativeName == "WebPMemoryWrite")
                    .SetMetadata(MetadataKeys.ExposeNativeFunctionPointer, true);
            }
            var prunedCount = IgnoreStdlibWrappers(compilation, descriptor);
            prunedCount += PruneDependencySurface(compilation, descriptor);
            prunedCount += PruneImGuiSurfaceIfApplicable(compilation, descriptor);
            ApplyImGuiEnumAliasSurfaceIfApplicable(compilation, descriptor);
            prunedCount += PruneDiligentAccessoryRecords(compilation, descriptor);
            prunedCount += PruneInvalidTypeAliases(compilation);

            var renderedBindings = BindingOutputRenderer.Render(
                compilation,
                options.FileStem,
                includeCallbackReleaseRegistration: descriptor.Surface == "sdl");
            renderedBindings = AddRmlUiLegacyAbi(renderedBindings, descriptor);
            renderedBindings.WriteToDirectory(options.OutputDirectory);

            var metadataDocument = BindingMetadataExporter.Export(
                compilation,
                new BindingMetadataExportOptions(
                    PackageId: descriptor.ProjectName,
                    Phase2ClosureHash: $"redbolt-{descriptor.Surface}-phase2",
                    ClangArgsHash: $"redbolt-{descriptor.Surface}-clang"));

            File.WriteAllText(options.MetadataPath, BindingMetadataSerializer.Serialize(metadataDocument));
            var summary =
                $"Generated {descriptor.Surface} bindings with {compilation.Functions.Count} functions after pruning {prunedCount} unsupported declarations: {options.OutputDirectory}";
            Console.WriteLine(summary);
            logWriter.WriteLine(summary);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            logWriter?.WriteLine(ex);
            PrintUsage();
            return 1;
        }
        finally
        {
            SharpCppLog.Configure();
            logWriter?.Dispose();
        }
    }

    private static List<string> BuildClangArguments(GeneratorOptions options, BindingSurfaceDescriptor descriptor)
    {
        var clangArguments = new List<string>
        {
            descriptor.ClangLanguageArgument,
            descriptor.LanguageStandardArgument
        };

        clangArguments.AddRange(ParseWarningDisableArguments);

        foreach (var define in options.Defines)
            clangArguments.Add($"-D{define}");

        foreach (var define in ParseCompatibilityDefines)
        {
            var argument = $"-D{define}";
            if (!clangArguments.Contains(argument, StringComparer.Ordinal))
                clangArguments.Add(argument);
        }

        foreach (var includeDirectory in options.IncludeDirectories)
            clangArguments.Add($"-I{includeDirectory}");

        // Match the upstream rbfx generator so marshaller config headers can resolve
        // project-relative includes such as <SharpCpp/Marshaller.hpp>.
        var sharpCppIncludeRoot = Path.Combine(options.SharpCppRoot, "include");
        if (!clangArguments.Contains($"-I{sharpCppIncludeRoot}", StringComparer.Ordinal))
            clangArguments.Add($"-I{sharpCppIncludeRoot}");

        var toolchainInfo = CompilerToolchainDiscovery.Discover(options.CompilerPath);
        if (toolchainInfo.TargetTriple?.StartsWith("wasm", StringComparison.OrdinalIgnoreCase) != true)
            clangArguments.AddRange(ParseSimdDisableArguments);

        if (!string.IsNullOrWhiteSpace(toolchainInfo.TargetTriple) &&
            !clangArguments.Any(static argument => argument.StartsWith("--target=", StringComparison.Ordinal) || argument == "-target"))
        {
            clangArguments.Add($"--target={toolchainInfo.TargetTriple}");
        }

        if (!string.IsNullOrWhiteSpace(toolchainInfo.ClangResourceDirectory) &&
            !clangArguments.Any(static argument => argument.StartsWith("-resource-dir=", StringComparison.Ordinal) || argument == "-resource-dir"))
        {
            clangArguments.Add($"-resource-dir={toolchainInfo.ClangResourceDirectory}");
        }

        foreach (var systemIncludeDirectory in toolchainInfo.IncludeDirectories)
            clangArguments.Add($"-isystem{systemIncludeDirectory}");

        return clangArguments.Distinct(StringComparer.Ordinal).ToList();
    }

    private static List<string> BuildMarshallerClangArguments(IReadOnlyList<string> clangArguments, BindingSurfaceDescriptor descriptor)
    {
        if (!descriptor.UseMarshallerConfiguration)
            return clangArguments.ToList();

        if (descriptor.Surface != "sdl" && descriptor.Surface != "dearimgui" && descriptor.Surface != "stb")
            return clangArguments.ToList();

        var marshallerArguments = new List<string>(clangArguments.Count);
        var replacedLanguageMode = false;
        var replacedLanguageStandard = false;

        foreach (var argument in clangArguments)
        {
            if (!replacedLanguageMode && string.Equals(argument, descriptor.ClangLanguageArgument, StringComparison.Ordinal))
            {
                marshallerArguments.Add("-xc++");
                replacedLanguageMode = true;
                continue;
            }

            if (!replacedLanguageStandard && string.Equals(argument, descriptor.LanguageStandardArgument, StringComparison.Ordinal))
            {
                marshallerArguments.Add("-std=c++20");
                replacedLanguageStandard = true;
                continue;
            }

            marshallerArguments.Add(argument);
        }

        if (!replacedLanguageMode)
            marshallerArguments.Insert(0, "-xc++");

        if (!replacedLanguageStandard)
            marshallerArguments.Insert(replacedLanguageMode ? 1 : 0, "-std=c++20");

        return marshallerArguments;
    }

    private static List<string> BuildMarshallerHeaders(GeneratorOptions options, BindingSurfaceDescriptor descriptor)
    {
        if (!descriptor.UseMarshallerConfiguration)
            return new List<string>();

        var marshallerHeaders = new List<string>
        {
            Path.Combine(options.SharpCppRoot, "include", "SharpCpp", "Marshaller.hpp")
        };

        if (descriptor.Surface is "dearimgui" or "rmlui")
        {
            marshallerHeaders.Add(Path.Combine(
                options.RedBoltRoot,
                "src",
                "RedBolt",
                "Native",
                "include",
                "RedBolt",
                descriptor.Surface == "dearimgui"
                    ? "ImGuiValueMarshallers.hpp"
                    : "RmlUiValueMarshallers.hpp"));
        }

        return marshallerHeaders
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void MarkNativeInterfaceWrappers(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "diligent")
            return;

        foreach (var record in compilation.Records)
        {
            if (IsDiligentInterfaceRecord(record))
            {
                record.SetMetadata(MetadataKeys.NativeInterfaceWrapper, true);
                if (record.CppQualifiedName is not ("Diligent::IMemoryAllocator" or "Diligent::IReferenceCounters"))
                    record.SetMetadata(MetadataKeys.NativeInterfaceReleaseMethod, "Release");
                if (DiligentQueryableInterfaces.TryGetValue(record.CppQualifiedName, out var interfaceIdExpression))
                {
                    record.SetMetadata(
                        MetadataKeys.NativeInterfaceQuery,
                        new NativeInterfaceQueryAnnotation(
                            "Diligent::IObject",
                            interfaceIdExpression));
                }
            }
        }
    }

    private static void FixDiligentBlendFactors(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "diligent")
            return;

        var deviceContext = compilation.Records.FirstOrDefault(static record =>
            string.Equals(record.CppQualifiedName, "Diligent::IDeviceContext", StringComparison.Ordinal));
        var setBlendFactors = deviceContext?.Members.OfType<FunctionBinding>().FirstOrDefault(static method =>
            string.Equals(method.NativeName, "SetBlendFactors", StringComparison.Ordinal));
        var blendFactors = setBlendFactors?.Parameters.FirstOrDefault(static parameter =>
            string.Equals(parameter.NativeName, "pBlendFactors", StringComparison.Ordinal));
        if (blendFactors is null)
            return;

        blendFactors.Type = BindingType.CreateConfigured(
            cppTypeName: "const float*",
            publicTypeName: "global::RedBolt.Bindings.Diligent.BlendFactors");
    }

    private static void FixCDependencyTypes(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface is not ("glslang" or "spirvcross" or "spirvtools"))
            return;

        for (var index = 0; index < compilation.TypeAliases.Count; ++index)
        {
            var alias = compilation.TypeAliases[index];
            if (alias.CppQualifiedName == "spvc_bool")
                compilation.TypeAliases[index] = alias with { TargetType = BindingType.Int8 };
        }

        foreach (var function in compilation.Functions)
        {
            if (function.CppReturnTypeName == "spvc_bool")
            {
                function.ReturnType = BindingType.CreateConfigured(
                    "spvc_bool",
                    BindingType.UInt8.ManagedTypeName,
                    primitiveKind: BindingPrimitiveKind.UInt8);
            }
            else if (function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Boolean)
            {
                function.ReturnType = BindingType.Boolean;
                function.CppReturnTypeName = "bool";
            }

            foreach (var parameter in function.Parameters)
            {
                if (descriptor.Surface == "spirvcross" &&
                    function.NativeName == "spvc_constant_set_scalar_i8" &&
                    parameter.NativeName == "value")
                {
                    parameter.Type = BindingType.Int8;
                    parameter.CppTypeName = "signed char";
                }
                else if (parameter.CppTypeName == "spvc_bool")
                {
                    parameter.Type = BindingType.CreateConfigured(
                        "spvc_bool",
                        BindingType.UInt8.ManagedTypeName,
                        primitiveKind: BindingPrimitiveKind.UInt8);
                }
                else if (parameter.Type.PrimitiveKind == BindingPrimitiveKind.Boolean)
                {
                    parameter.Type = BindingType.Boolean;
                    parameter.CppTypeName = "bool";
                }
            }
        }

        foreach (var record in compilation.Records)
        {
            foreach (var field in record.Members.OfType<FieldBinding>())
            {
                if (field.CppTypeName == "spvc_bool")
                {
                    field.Type = BindingType.CreateConfigured(
                        "spvc_bool",
                        BindingType.UInt8.ManagedTypeName,
                        primitiveKind: BindingPrimitiveKind.UInt8);
                }
                else if (field.Type.PrimitiveKind == BindingPrimitiveKind.Boolean)
                {
                    field.Type = BindingType.Boolean;
                    field.CppTypeName = "bool";
                }
            }

            if (record.Members.OfType<FieldBinding>().Any(static field => field.IsReadOnly))
            {
                for (var index = record.Members.Count - 1; index >= 0; --index)
                {
                    if (record.Members[index] is FunctionBinding { IsConstructor: true })
                        record.Members.RemoveAt(index);
                }
            }
        }
    }

    private static void FixDiligentTextureFormatFunction(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "diligent")
            return;

        var function = compilation.Functions.FirstOrDefault(static item =>
            string.Equals(item.NativeName, "GetTextureFormatAttribs", StringComparison.Ordinal) ||
            item.CppQualifiedName.EndsWith("::GetTextureFormatAttribs", StringComparison.Ordinal));
        if (function is null)
            return;

        function.ReturnType = BindingType.IntPtr;
    }

    private static void FixDiligentVulkanImageHandle(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "diligent")
            return;

        var renderDeviceVk = compilation.Records.FirstOrDefault(static record =>
            string.Equals(record.CppQualifiedName, "Diligent::IRenderDeviceVk", StringComparison.Ordinal));
        var createTexture = renderDeviceVk?.Members.OfType<FunctionBinding>().FirstOrDefault(static method =>
            string.Equals(method.NativeName, "CreateTextureFromVulkanImage", StringComparison.Ordinal));
        var vkImage = createTexture?.Parameters.FirstOrDefault(static parameter =>
            string.Equals(parameter.NativeName, "vkImage", StringComparison.Ordinal));
        if (vkImage is null)
            return;

        vkImage.Type = BindingType.CreateConfigured("VkImage", "nint", BindingPrimitiveKind.NativeInt);
        vkImage.CppTypeName = "VkImage";
        createTexture!.ClearIgnoreReason();
    }

    private static void FixXAtlasTypes(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "xatlas")
            return;

        var indexFormat = PromoteXAtlasEnum(compilation, "IndexFormat");
        var addMeshError = PromoteXAtlasEnum(compilation, "AddMeshError");
        compilation.InvalidateCaches();

        var meshDecl = compilation.Records.First(static record => record.NativeName == "MeshDecl");
        var indexFormatField = meshDecl.Members.OfType<FieldBinding>().First(static field => field.NativeName == "indexFormat");
        indexFormatField.Type = BindingType.CreateEnum(
            indexFormat.CppQualifiedName, indexFormat.ManagedName, enumId: indexFormat.SymbolId);
        indexFormatField.CppTypeName = indexFormat.CppQualifiedName;
        indexFormatField.ClearIgnoreReason();

        var mesh = compilation.Records.First(static record => record.NativeName == "Mesh");
        var vertex = compilation.Records.First(static record => record.NativeName == "Vertex");
        var atlas = compilation.Records.First(static record => record.NativeName == "Atlas");
        var meshesField = atlas.Members.OfType<FieldBinding>().First(static field => field.NativeName == "meshes");
        var meshPointerType = BindingType.CreateHandle(
            "xatlas::Mesh*", meshesField.Type.ManagedTypeName, mesh.SymbolId, mesh.HandleMarshallerTypeName);
        ReplaceXAtlasField(atlas, "meshes", "Meshes", meshPointerType);
        ReplaceXAtlasField(atlas, "meshCount", "NumMeshes");

        var vertexArrayField = mesh.Members.OfType<FieldBinding>().First(static field => field.NativeName == "vertexArray");
        var vertexPointerType = BindingType.CreateHandle(
            "xatlas::Vertex*", vertexArrayField.Type.ManagedTypeName, vertex.SymbolId, vertex.HandleMarshallerTypeName);
        ReplaceXAtlasField(mesh, "vertexArray", "Vertices", vertexPointerType);
        ReplaceXAtlasField(mesh, "vertexCount", "NumVertices");
        var indexArrayField = mesh.Members.OfType<FieldBinding>().First(static field => field.NativeName == "indexArray");
        indexArrayField.Type = BindingType.IntPtr;
        indexArrayField.IsPointer = true;
        indexArrayField.ClearIgnoreReason();

        var addMesh = compilation.Functions.First(static function => function.NativeName == "AddMesh");
        addMesh.ReturnType = BindingType.CreateEnum(
            addMeshError.CppQualifiedName, addMeshError.ManagedName, enumId: addMeshError.SymbolId);
        addMesh.CppReturnTypeName = addMeshError.CppQualifiedName;
        addMesh.ClearIgnoreReason();

        var generate = compilation.Functions.First(static function => function.NativeName == "Generate");
        var parameterizeFunc = generate.Parameters.First(static parameter => parameter.NativeName == "paramFunc");
        parameterizeFunc.Type = BindingType.IntPtr;
        parameterizeFunc.CppTypeName = "xatlas::ParameterizeFunc";
        parameterizeFunc.Direction = BindingParameterDirection.In;
        parameterizeFunc.AbiKind = BindingParameterAbiKind.Value;
        parameterizeFunc.ClearIgnoreReason();
        generate.ClearIgnoreReason();
    }

    private static EnumBinding PromoteXAtlasEnum(BindingCompilation compilation, string recordName)
    {
        var @enum = new EnumBinding(
            $"enum:xatlas::{recordName}::Enum", "Enum", recordName, cppQualifiedName: $"xatlas::{recordName}::Enum");
        var values = recordName switch
        {
            "IndexFormat" => new[]
            {
                new EnumValueBinding("UInt16", "UInt16", "0"),
                new EnumValueBinding("UInt32", "UInt32", "1")
            },
            "AddMeshError" => new[]
            {
                new EnumValueBinding("Success", "Success", "0"),
                new EnumValueBinding("Error", "Error", "1"),
                new EnumValueBinding("IndexOutOfRange", "IndexOutOfRange", "2"),
                new EnumValueBinding("InvalidIndexCount", "InvalidIndexCount", "3")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(recordName))
        };
        foreach (var value in values)
            @enum.Values.Add(value);
        compilation.Members.Add(@enum);
        return @enum;
    }

    private static FieldBinding ReplaceXAtlasField(
        RecordBinding record, string nativeName, string replacementNativeName, BindingType? type = null)
    {
        var index = record.Members
            .Select(static (member, index) => (Member: member, Index: index))
            .First(pair => pair.Member is FieldBinding field && field.NativeName == nativeName).Index;
        var source = (FieldBinding)record.Members[index];
        var replacement = new FieldBinding(
            source.SymbolId, replacementNativeName, source.ManagedName, type ?? source.Type, source.CppTypeName,
            source.OwningRecordCppTypeName, source.IsReadOnly, source.IsReference, source.IsPointer)
        {
            AccessModifier = source.AccessModifier,
            ReturnsOwnedHandle = source.ReturnsOwnedHandle
        };
        replacement.SetMetadata(MetadataKeys.NativeFieldAccessExpression, nativeName);
        record.Members[index] = replacement;
        return replacement;
    }

    private static int PruneDiligentAccessoryRecords(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "diligent")
            return 0;

        var accessoryRecordNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "HashMapStringKey",
            "RefCountersImpl",
            "Matrix2x2",
            "Matrix3x3",
            "Matrix4x4",
            "Quaternion",
            "Vector2",
            "Vector3",
            "Vector4"
        };
        var prunedCount = 0;
        for (var index = compilation.Members.Count - 1; index >= 0; --index)
        {
            if (compilation.Members[index] is not RecordBinding record ||
                !accessoryRecordNames.Contains(record.NativeName))
            {
                continue;
            }

            compilation.Members.RemoveAt(index);
            prunedCount++;
        }

        var remainingRecordIds = compilation.Records.Select(static record => record.SymbolId).ToHashSet(StringComparer.Ordinal);
        for (var index = compilation.TypeAliases.Count - 1; index >= 0; --index)
        {
            var alias = compilation.TypeAliases[index];
            if (alias.ManagedName == "String" ||
                alias.TargetType.RecordId is not null && !remainingRecordIds.Contains(alias.TargetType.RecordId))
            {
                compilation.TypeAliases.RemoveAt(index);
                prunedCount++;
            }
        }

        return prunedCount;
    }

    private static bool IsDiligentInterfaceRecord(RecordBinding record)
    {
        if (!record.CppQualifiedName.StartsWith("Diligent::", StringComparison.Ordinal) ||
            string.Equals(record.NativeName, "INTERFACE_ID", StringComparison.Ordinal) ||
            record.Members.OfType<FieldBinding>().Any())
        {
            return false;
        }

        return record.IsAbstract &&
            record.Members.OfType<FunctionBinding>().Any(static method => method is
            {
                IsVirtual: true,
                IsPureVirtual: true,
                AccessModifier: BindingAccessModifier.Public
            });
    }

    private static int IgnoreStdlibWrappers(
        BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        var ignoredCount = 0;
        foreach (var record in compilation.Records)
        {
            if (record.IsIgnored() ||
                !record.CppQualifiedName.StartsWith("std::", StringComparison.Ordinal) ||
                descriptor.Surface == "rmlui" && record.ManagedNamespace == "Std" && record.ManagedName == "String")
            {
                continue;
            }

            record.SetIgnoreReason("std library wrapper emission is disabled in RedBolt for now");
            ignoredCount++;
        }

        return ignoredCount;
    }

    private static void FixRmlUiSurface(
        BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "rmlui")
            return;

        var systemInterface = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::SystemInterface");
        systemInterface.ClearIgnoreReason();
        systemInterface.Bases.Clear();
        systemInterface.ManagedBaseTypeName = null;
        foreach (var method in systemInterface.Members.OfType<FunctionBinding>())
        {
            if (method.IsConstructor || method.IsVirtual)
                method.ClearIgnoreReason();
        }

        var fileInterface = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::FileInterface");
        fileInterface.ClearIgnoreReason();
        fileInterface.Bases.Clear();
        fileInterface.ManagedBaseTypeName = null;
        foreach (var method in fileInterface.Members.OfType<FunctionBinding>())
        {
            if (method.IsConstructor || method.IsVirtual)
                method.ClearIgnoreReason();
        }

        var fontEngineInterface = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::FontEngineInterface");
        fontEngineInterface.ClearIgnoreReason();
        fontEngineInterface.Bases.Clear();
        fontEngineInterface.ManagedBaseTypeName = null;
        foreach (var method in fontEngineInterface.Members.OfType<FunctionBinding>())
        {
            if (method.IsConstructor || method.NativeName is "Initialize" or "Shutdown")
                method.ClearIgnoreReason();
            else
                method.SetSilentExclusionReason("RmlUi font engine member is outside the lifecycle binding slice");
        }

        var harfBuzzFontEngine = compilation.Records.First(static record =>
            record.CppQualifiedName == "RedBoltNative::RmlHarfBuzzFontEngine");
        harfBuzzFontEngine.ClearIgnoreReason();
        foreach (var method in harfBuzzFontEngine.Members.OfType<FunctionBinding>())
        {
            if (method.IsConstructor)
                method.ClearIgnoreReason();
            else
                method.SetSilentExclusionReason("RmlUi HarfBuzz font engine is accessed through FontEngineInterface");
        }

        var eventListener = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::EventListener");

        var factory = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Factory");
        factory.ClearIgnoreReason();
        foreach (var method in factory.Members.OfType<FunctionBinding>())
        {
            if (method.NativeName is "ClearStyleSheetCache" or "ClearTemplateCache")
                method.ClearIgnoreReason();
            else
                method.SetSilentExclusionReason("RmlUi factory member is outside the current binding slice");
        }

        var context = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Context");
        context.ClearIgnoreReason();
        context.Bases.Clear();
        context.ManagedBaseTypeName = null;
        foreach (var method in context.Members.OfType<FunctionBinding>())
        {
            if (method.NativeName is
                "GetDensityIndependentPixelRatio" or "GetDimensions" or "GetDocument" or "GetFocusElement" or
                "GetHoverElement" or "GetName" or "GetNumDocuments" or "GetRootElement" or "Render" or
                "LoadDocumentFromMemory" or "SetDensityIndependentPixelRatio" or "SetDimensions" or
                "ProcessKeyDown" or "ProcessKeyUp" or "ProcessMouseButtonDown" or "ProcessMouseButtonUp" or
                "ProcessMouseMove" or "ProcessMouseWheel" or "ProcessTextInput" or
                "UnloadDocument" or "Update")
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
                method.ClearIgnoreReason();
            }
            else
            {
                method.SetSilentExclusionReason("RmlUi context member is outside the current binding slice");
            }
        }

        var vector2iType = BindingType.CreateConfigured(
            "Rml::Vector2<int>", "global::RedBolt.Bindings.RmlUi.RmlUiLegacyVector2i");
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

        var element = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Element");
        element.ClearIgnoreReason();
        element.Bases.Clear();
        element.ManagedBaseTypeName = null;
        foreach (var method in element.Members.OfType<FunctionBinding>())
        {
            if (!method.IsGeneric && method.NativeName is
                "Blur" or "GetAbsoluteOffset" or "GetContext" or "GetOwnerDocument" or "GetProperty" or
                "GetTagName" or "IsVisible" or "SetProperty")
                method.ClearIgnoreReason();
            else
                method.SetSilentExclusionReason("RmlUi element member is outside the current binding slice");
        }

        var elementDocument = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::ElementDocument");
        elementDocument.ClearIgnoreReason();
        foreach (var method in elementDocument.Members.OfType<FunctionBinding>())
        {
            if (method.NativeName is
                "Close" or "GetContext" or "GetSourceURL" or "Hide" or "IsModal" or "Show" or "UpdateDocument")
                method.ClearIgnoreReason();
            else
                method.SetSilentExclusionReason("RmlUi document member is outside the current binding slice");
        }

        var property = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Property");
        property.ClearIgnoreReason();
        property.Bases.Clear();
        property.ManagedBaseTypeName = null;
        foreach (var member in property.Members)
            member.SetSilentExclusionReason("RmlUi property internals are outside the document reload binding slice");

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
                     .Where(static method => method.NativeName == "GetOwnerDocument"))
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
            "Rml::Vector2<float>", "global::RedBolt.Bindings.RmlUi.RmlUiLegacyVector2f");
        var getAbsoluteOffset = element.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "GetAbsoluteOffset");
        getAbsoluteOffset.ReturnType = vector2fType;
        getAbsoluteOffset.CppReturnTypeName = "Rml::Vector2<float>";

        var @event = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Event");
        @event.ClearIgnoreReason();
        @event.Bases.Clear();
        @event.ManagedBaseTypeName = null;
        foreach (var method in @event.Members.OfType<FunctionBinding>())
        {
            if (method.NativeName is "GetCurrentElement" or "GetPhase" or "GetTargetElement" or "IsPropagating")
                method.ClearIgnoreReason();
            else
                method.SetSilentExclusionReason("RmlUi event member is outside the event listener binding slice");
        }

        eventListener.ClearIgnoreReason();
        eventListener.Bases.Clear();
        eventListener.ManagedBaseTypeName = null;
        foreach (var method in eventListener.Members.OfType<FunctionBinding>())
        {
            if (method.IsConstructor || method.IsVirtual)
                method.ClearIgnoreReason();
        }

        var eventPhase = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::EventPhase");
        eventPhase.ClearIgnoreReason();
        var focusFlag = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::FocusFlag");
        focusFlag.ClearIgnoreReason();
        var modalFlag = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::ModalFlag");
        modalFlag.ClearIgnoreReason();
        var boxArea = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::BoxArea");
        boxArea.ClearIgnoreReason();
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
        propertyId.ClearIgnoreReason();
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
        var keyIdentifier = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::Input::KeyIdentifier");
        keyIdentifier.ClearIgnoreReason();
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
        var keyModifier = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::Input::KeyModifier");
        keyModifier.ClearIgnoreReason();
        var fontWeight = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::Style::FontWeight");
        fontWeight.ClearIgnoreReason();
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
        renderInterfaceCompatibility.ClearIgnoreReason();
        renderInterfaceCompatibility.Bases.Clear();
        renderInterfaceCompatibility.ManagedBaseTypeName = null;
        foreach (var method in renderInterfaceCompatibility.Members.OfType<FunctionBinding>())
        {
            if (method.IsConstructor || method.IsVirtual || method.NativeName == "GetAdaptedInterface")
                method.ClearIgnoreReason();
        }

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
        log.ClearIgnoreReason();
        foreach (var method in log.Members.OfType<FunctionBinding>())
            method.SetSilentExclusionReason("RmlUi logging helpers are outside the interface binding slice");

        var logMessage = systemInterface.Members.OfType<FunctionBinding>().First(static method =>
            method.NativeName == "LogMessage");
        var logMessageType = logMessage.Parameters.First(static parameter => parameter.NativeName == "type");
        var logType = compilation.Enums.First(static @enum =>
            @enum.CppQualifiedName == "Rml::Log::Type");
        logType.CppQualifiedName = "Rml::Log::Type";
        logType.ManagedName = "LogType";
        logType.ManagedNamespace = descriptor.ManagedNamespace;
        logType.ClearIgnoreReason();

        logMessageType.Type = BindingType.CreateEnum(
            logType.CppQualifiedName, logType.ManagedName, enumId: logType.SymbolId);
        logMessageType.CppTypeName = logType.CppQualifiedName;

        var vector2 = compilation.Records.First(static record =>
            record.CppQualifiedName == "Rml::Vector2");
        vector2.ClearIgnoreReason();
        vector2.Members.Clear();

        foreach (var function in compilation.Functions.Where(static function =>
                     function.Parent is not RecordBinding &&
                     function.CppQualifiedName is
                         "Rml::CreateContext" or "Rml::Debugger::Initialise" or "Rml::Debugger::IsVisible" or
                         "Rml::Debugger::SetVisible" or "Rml::Initialise" or "Rml::LoadFontFace" or
                         "Rml::ReleaseTextures" or "Rml::RemoveContext" or "Rml::Shutdown" or
                         "Rml::GetFileInterface" or "Rml::SetFileInterface" or
                         "Rml::GetFontEngineInterface" or "Rml::SetFontEngineInterface" or
                         "Rml::GetRenderInterface" or "Rml::SetRenderInterface" or
                         "Rml::GetSystemInterface" or "Rml::SetSystemInterface"))
        {
            if (function.CppQualifiedName == "Rml::LoadFontFace" &&
                function.Parameters.FirstOrDefault()?.CppTypeName != "const Rml::String&")
            {
                function.SetSilentExclusionReason("RmlUi memory font loading is outside the current binding slice");
                continue;
            }

            function.ClearIgnoreReason();
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

    private static int PruneDependencySurface(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface == "glslang")
        {
            var glslangPrunedCount = 0;
            for (var index = compilation.Members.Count - 1; index >= 0; --index)
            {
                if (compilation.Members[index] is not FunctionBinding function ||
                    !UnsupportedGlslangFunctions.Contains(function.NativeName))
                {
                    continue;
                }

                compilation.Members.RemoveAt(index);
                glslangPrunedCount++;
            }

            compilation.InvalidateCaches();
            return glslangPrunedCount;
        }

        if (descriptor.Surface == "lz4")
        {
            var lz4PrunedCount = 0;
            for (var index = compilation.Members.Count - 1; index >= 0; --index)
            {
                if (compilation.Members[index] is FunctionBinding function &&
                    RetainedLz4Functions.Contains(function.NativeName))
                {
                    continue;
                }

                compilation.Members.RemoveAt(index);
                lz4PrunedCount++;
            }

            compilation.InvalidateCaches();
            return lz4PrunedCount;
        }

        if (descriptor.Surface == "harfbuzz")
        {
            var harfBuzzPrunedCount = 0;
            for (var index = compilation.Members.Count - 1; index >= 0; --index)
            {
                var member = compilation.Members[index];
                if (member is FunctionBinding function && RetainedHarfBuzzFunctions.Contains(function.NativeName))
                    continue;

                if (member is RecordBinding record && RetainedHarfBuzzRecords.Contains(record.NativeName))
                    continue;

                if (member is EnumBinding enumBinding && RetainedHarfBuzzEnums.Contains(enumBinding.NativeName))
                    continue;

                compilation.Members.RemoveAt(index);
                harfBuzzPrunedCount++;
            }

            for (var index = compilation.TypeAliases.Count - 1; index >= 0; --index)
            {
                if (RetainedHarfBuzzAliases.Contains(compilation.TypeAliases[index].NativeName))
                    continue;

                compilation.TypeAliases.RemoveAt(index);
                harfBuzzPrunedCount++;
            }

            compilation.InvalidateCaches();
            return harfBuzzPrunedCount;
        }

        if (descriptor.Surface == "rmlui")
        {
            var rmlUiPrunedCount = 0;
            for (var index = compilation.Members.Count - 1; index >= 0; --index)
            {
                var member = compilation.Members[index];
                if (member is FunctionBinding function &&
                    function.CppQualifiedName is
                        "Rml::CreateContext" or "Rml::Debugger::Initialise" or "Rml::Debugger::IsVisible" or
                        "Rml::Debugger::SetVisible" or "Rml::Initialise" or "Rml::LoadFontFace" or
                        "Rml::ReleaseTextures" or "Rml::RemoveContext" or "Rml::Shutdown" or
                        "Rml::GetFileInterface" or "Rml::SetFileInterface" or
                        "Rml::GetFontEngineInterface" or "Rml::SetFontEngineInterface" or
                        "Rml::GetRenderInterface" or "Rml::SetRenderInterface" or
                        "Rml::GetSystemInterface" or "Rml::SetSystemInterface")
                {
                    continue;
                }

                if (member is RecordBinding record &&
                    (record.CppQualifiedName is
                         "Rml::Context" or "Rml::Element" or "Rml::Event" or
                         "Rml::ElementDocument" or "Rml::EventListener" or "Rml::FileInterface" or
                         "Rml::Factory" or "Rml::FontEngineInterface" or "Rml::Log" or "Rml::Property" or
                         "Rml::RenderInterfaceCompatibility" or "Rml::SystemInterface" or
                         "RedBoltNative::RmlHarfBuzzFontEngine" ||
                     record.CppQualifiedName.StartsWith("Rml::Vector2", StringComparison.Ordinal) ||
                     record.ManagedNamespace == "Std" && record.ManagedName == "String"))
                {
                    continue;
                }

                if (member is EnumBinding @enum &&
                    @enum.CppQualifiedName is
                        "Rml::BoxArea" or "Rml::EventPhase" or "Rml::FocusFlag" or "Rml::Log::Type" or
                        "Rml::ModalFlag" or "Rml::PropertyId" or
                        "Rml::Input::KeyIdentifier" or "Rml::Input::KeyModifier" or "Rml::Style::FontWeight")
                    continue;

                compilation.Members.RemoveAt(index);
                rmlUiPrunedCount++;
            }

            for (var index = compilation.TypeAliases.Count - 1; index >= 0; --index)
            {
                if (compilation.TypeAliases[index].CppQualifiedName == "Rml::Vector2f")
                    continue;

                compilation.TypeAliases.RemoveAt(index);
                rmlUiPrunedCount++;
            }

            compilation.InvalidateCaches();
            return rmlUiPrunedCount;
        }

        if (descriptor.Surface == "sdl")
        {
            var sdlPrunedCount = 0;
            for (var index = compilation.Members.Count - 1; index >= 0; --index)
            {
                if (compilation.Members[index] is not FunctionBinding { NativeName: "SDL_GetRevisionNumber" })
                    continue;

                compilation.Members.RemoveAt(index);
                sdlPrunedCount++;
            }

            compilation.InvalidateCaches();
            return sdlPrunedCount;
        }

        if (descriptor.Surface == "xatlas")
        {
            var xatlasPrunedCount = 0;
            for (var index = compilation.Members.Count - 1; index >= 0; --index)
            {
                var member = compilation.Members[index];
                if (member is FunctionBinding function && RetainedXAtlasFunctions.Contains(function.NativeName))
                    continue;

                if (member is EnumBinding @enum && RetainedXAtlasEnums.Contains(@enum.ManagedName))
                    continue;

                if (member is RecordBinding nativeArray && nativeArray.HasMetadata(MetadataKeys.NativeArrayRecord))
                    continue;
                if (member is RecordBinding record &&
                    RetainedXAtlasRecordFields.TryGetValue(record.NativeName, out var retainedFields))
                {
                    for (var memberIndex = record.Members.Count - 1; memberIndex >= 0; --memberIndex)
                    {
                        if (record.Members[memberIndex] is FieldBinding field && retainedFields.Contains(field.NativeName))
                            continue;

                        if (record.Members[memberIndex] is FunctionBinding { IsConstructor: true } or EnumBinding)
                            continue;

                        record.Members.RemoveAt(memberIndex);
                        xatlasPrunedCount++;
                    }
                    continue;
                }

                compilation.Members.RemoveAt(index);
                xatlasPrunedCount++;
            }

            compilation.InvalidateCaches();
            return xatlasPrunedCount;
        }

        if (descriptor.Surface is "dearimgui" or "stb" or "spirvcross" or "spirvtools" or "spirvoptimizer" or "shadercompiler")
            return 0;

        if (descriptor.Surface == "webp")
        {
            var webpPrunedCount = 0;
            for (var index = compilation.Members.Count - 1; index >= 0; --index)
            {
                var member = compilation.Members[index];
                if (member is FunctionBinding function &&
                    (RetainedWebPFunctions.Contains(function.NativeName) || RetainedWebPFunctions.Contains(function.ManagedName)))
                {
                    continue;
                }

                if (member is RecordBinding record && RetainedWebPRecordFields.TryGetValue(record.NativeName, out var retainedFields))
                {
                    for (var memberIndex = record.Members.Count - 1; memberIndex >= 0; --memberIndex)
                    {
                        if (record.Members[memberIndex] is FieldBinding field && retainedFields.Contains(field.NativeName))
                            continue;

                        if (record.Members[memberIndex] is FunctionBinding { IsConstructor: true })
                            continue;

                        record.Members.RemoveAt(memberIndex);
                        webpPrunedCount++;
                    }
                    continue;
                }

                if (member is EnumBinding enumBinding &&
                    (RetainedWebPEnums.Contains(enumBinding.NativeName) || RetainedWebPEnums.Contains(enumBinding.ManagedName)))
                {
                    continue;
                }

                compilation.Members.RemoveAt(index);
                webpPrunedCount++;
            }

            compilation.InvalidateCaches();
            return webpPrunedCount;
        }

        if (descriptor.Surface == "diligent")
        {
            var diligentPrunedCount = PruneDiligentSurface(compilation);
            compilation.InvalidateCaches();
            return diligentPrunedCount;
        }

        var prunedCount = PruneMembers(compilation.Members, descriptor);
        compilation.InvalidateCaches();
        return prunedCount;
    }

    private static GeneratedBindingSet AddRmlUiLegacyAbi(
        GeneratedBindingSet bindings, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "rmlui")
            return bindings;

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
                        static_assert(std::is_standard_layout_v<Rml::Vector2i>);
                        static_assert(sizeof(Rml::Vector2i) == sizeof(int) * 2);
                        static_assert(offsetof(Rml::Vector2i, x) == 0);
                        static_assert(offsetof(Rml::Vector2i, y) == sizeof(int));
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

    private static int PruneImGuiSurfaceIfApplicable(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "dearimgui")
            return 0;

        return PruneImGuiSurface(compilation);
    }

    private static void FixImGuiFunctions(
        BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "dearimgui")
            return;

        foreach (var function in compilation.Functions)
        {
            if (function.NativeName is not ("InputText" or "InputTextMultiline" or "InputTextWithHint"))
                continue;

            for (var index = function.Parameters.Count - 1; index >= 0; --index)
            {
                if (function.Parameters[index].NativeName is "callback" or "user_data")
                    function.Parameters.RemoveAt(index);
            }
            var buffer = function.Parameters.First(static parameter => parameter.NativeName == "buf");
            buffer.Type = BindingType.IntPtr;
            buffer.Direction = BindingParameterDirection.In;
            buffer.AbiKind = BindingParameterAbiKind.Value;
            buffer.CppTypeName = "char*";
            function.ClearIgnoreReason();
            function.SetMetadata(MetadataKeys.NativeDefaultArgumentAdapter, true);
        }

        foreach (var function in compilation.Functions)
        {
            if (function.CppQualifiedName == "ImGui::Begin")
                function.Parameters.First(static parameter => parameter.NativeName == "p_open").Direction = BindingParameterDirection.Ref;
            if (function.NativeName == "Checkbox")
                function.Parameters.First(static parameter => parameter.NativeName == "v").Direction = BindingParameterDirection.Ref;
        }
    }

    private static void ApplyImGuiEnumAliasSurfaceIfApplicable(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "dearimgui")
            return;

        ApplyImGuiEnumAliasSurface(compilation);
    }

    private static int PruneDiligentSurface(BindingCompilation compilation)
    {
        var prunedCount = 0;
        prunedCount += PruneDiligentTopLevelFunctions(compilation.Members);

        foreach (var record in compilation.Records.ToArray())
        {
            if (!record.HasMetadata(MetadataKeys.NativeInterfaceWrapper))
                continue;

            prunedCount += PruneDiligentInterfaceMethods(record);
        }

        return prunedCount;
    }

    private static int PruneDiligentTopLevelFunctions(IList<BindingNode> members)
    {
        var prunedCount = 0;
        for (var index = members.Count - 1; index >= 0; --index)
        {
            if (members[index] is not FunctionBinding function)
                continue;

            if (IsRetainedDiligentFunction(function))
                continue;

            members.RemoveAt(index);
            prunedCount++;
        }

        return prunedCount;
    }

    private static int PruneDiligentInterfaceMethods(RecordBinding record)
    {
        var prunedCount = 0;
        for (var index = record.Members.Count - 1; index >= 0; --index)
        {
            if (record.Members[index] is not FunctionBinding method)
                continue;

            if (IsRetainedDiligentInterfaceMethod(record, method))
                continue;

            record.Members.RemoveAt(index);
            prunedCount++;
        }

        return prunedCount;
    }

    private static readonly HashSet<string> ImGuiProblematicRecordCppNames = new(StringComparer.Ordinal)
    {
        "ImFontGlyph",
        "ImFontBaked",
        "ImGuiStoragePair",
        "ImGuiInputTextCallbackData",
        "ImFontAtlas"
    };

    private static readonly HashSet<string> ImGuiProblematicFunctionNames = new(StringComparer.Ordinal)
    {
        "IsMouseReleasedWithDelay",
        "GetItemFlags",
        "GetMouseCursor",
        "TableGetColumnFlags"
    };

    private static int PruneImGuiSurface(BindingCompilation compilation)
    {
        var prunedCount = 0;

        foreach (var record in compilation.Records)
        {
            if (ImGuiProblematicRecordCppNames.Contains(record.CppQualifiedName))
            {
                record.SetSilentExclusionReason("Dear ImGui record contains bit-fields, anonymous unions, or char* fields not yet supported by SharpCpp rendering");
                prunedCount++;
            }
        }

        foreach (var function in compilation.Functions)
        {
            if (ImGuiProblematicFunctionNames.Contains(function.NativeName))
            {
                function.SetSilentExclusionReason("Dear ImGui function has unsupported C++ syntax in the managed surface");
                prunedCount++;
            }
        }

        PruneImGuiFunctionsWithExcludedTypes(compilation);
        compilation.InvalidateCaches();
        return prunedCount;
    }

    private static void ApplyImGuiEnumAliasSurface(BindingCompilation compilation)
    {
        var enumsByNativeName = compilation.Enums.ToDictionary(static @enum => @enum.NativeName, StringComparer.Ordinal);
        foreach (var @enum in compilation.Enums)
        {
            if (@enum.NativeName.EndsWith("_", StringComparison.Ordinal))
                @enum.ManagedName = @enum.NativeName.TrimEnd('_');
            if (@enum.NativeName.EndsWith("Flags_", StringComparison.Ordinal))
                @enum.IsFlags = true;
        }

        var enumsByAliasName = new Dictionary<string, EnumBinding>(StringComparer.Ordinal);
        for (var index = compilation.TypeAliases.Count - 1; index >= 0; --index)
        {
            var alias = compilation.TypeAliases[index];
            if (alias.TargetType.Kind != BindingTypeKind.Primitive ||
                alias.TargetType.PrimitiveKind != BindingPrimitiveKind.Int32 ||
                !enumsByNativeName.TryGetValue(alias.NativeName + "_", out var @enum))
            {
                continue;
            }

            enumsByAliasName[alias.NativeName] = @enum;
            compilation.TypeAliases.RemoveAt(index);
        }

        foreach (var function in compilation.Functions)
        {
            function.ReturnType = RemapImGuiEnumAliases(function.ReturnType, enumsByAliasName);

            foreach (var parameter in function.Parameters)
                parameter.Type = RemapImGuiEnumAliases(parameter.Type, enumsByAliasName);
        }

        foreach (var field in compilation.Records.SelectMany(static record => record.Members.OfType<FieldBinding>()))
            field.Type = RemapImGuiEnumAliases(field.Type, enumsByAliasName);

        compilation.InvalidateCaches();
    }

    private static BindingType RemapImGuiEnumAliases(
        BindingType type,
        IReadOnlyDictionary<string, EnumBinding> enumsByAliasName)
    {
        if (!string.IsNullOrWhiteSpace(type.AliasNativeName) &&
            enumsByAliasName.TryGetValue(type.AliasNativeName, out var @enum) &&
            type.Kind == BindingTypeKind.Primitive &&
            type.PrimitiveKind == BindingPrimitiveKind.Int32)
        {
            var nativeCppTypeName = type.AliasCppQualifiedName ?? type.AliasNativeName ?? type.CppTypeName;
            var remappedType = BindingType.CreateEnum(nativeCppTypeName, @enum.ManagedName, enumId: @enum.SymbolId)
                .WithCppIsReference(type.Marshaller.CppIsReference);
            if (type.FixedArrayElementType is not null && type.FixedArrayLength is not null)
                remappedType = remappedType.WithFixedArray(type.FixedArrayElementType, type.FixedArrayLength.Value);
            if (type.DynamicArrayElementType is not null)
                remappedType = remappedType.WithDynamicArray(type.DynamicArrayElementType);
            return remappedType;
        }

        if (type.IsCallback)
        {
            var callbackReturnType = type.CallbackReturnType is null
                ? null
                : RemapImGuiEnumAliases(type.CallbackReturnType, enumsByAliasName);
            var callbackParameterTypes = type.CallbackParameterTypes
                .Select(parameter => RemapImGuiEnumAliases(parameter, enumsByAliasName))
                .ToArray();
            if (callbackReturnType is not null)
                type = type.WithCallbackSignature(callbackReturnType, callbackParameterTypes);
        }

        if (type.GenericArguments.Count > 0)
            type = type.WithGenericArguments(type.GenericArguments.Select(argument => RemapImGuiEnumAliases(argument, enumsByAliasName)).ToArray());
        if (type.FixedArrayElementType is not null && type.FixedArrayLength is not null)
            type = type.WithFixedArray(RemapImGuiEnumAliases(type.FixedArrayElementType, enumsByAliasName), type.FixedArrayLength.Value);
        if (type.DynamicArrayElementType is not null)
            type = type.WithDynamicArray(RemapImGuiEnumAliases(type.DynamicArrayElementType, enumsByAliasName));

        return type;
    }

    private static void PruneImGuiFunctionsWithExcludedTypes(BindingCompilation compilation)
    {
        var excludedRecordSymbolIds = compilation.Records
            .Where(static record => record.IsIgnored())
            .Select(static record => record.SymbolId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var function in compilation.Functions)
        {
            if (function.IsIgnored())
                continue;

            if (ReferencesExcludedRecord(function.ReturnType, excludedRecordSymbolIds) ||
                function.Parameters.Any(p => ReferencesExcludedRecord(p.Type, excludedRecordSymbolIds)))
            {
                function.SetSilentExclusionReason("Dear ImGui function references an excluded record type");
            }
        }

        foreach (var field in compilation.Records.SelectMany(static r => r.Members.OfType<FieldBinding>()))
        {
            if (field.IsIgnored())
                continue;

            if (ReferencesExcludedRecord(field.Type, excludedRecordSymbolIds))
            {
                field.SetSilentExclusionReason("Dear ImGui field references an excluded record type");
            }
        }
    }

    private static bool ReferencesExcludedRecord(BindingType type, HashSet<string> excludedSymbolIds)
    {
        if (type.RecordId is not null && excludedSymbolIds.Contains(type.RecordId))
            return true;

        return type.GenericArguments.Any(a => ReferencesExcludedRecord(a, excludedSymbolIds)) ||
            (type.FixedArrayElementType is not null && ReferencesExcludedRecord(type.FixedArrayElementType, excludedSymbolIds)) ||
            (type.DynamicArrayElementType is not null && ReferencesExcludedRecord(type.DynamicArrayElementType, excludedSymbolIds));
    }

    private static bool IsRetainedDiligentFunction(FunctionBinding function)
    {
        return RetainedDiligentFunctions.Contains(function.ManagedName) ||
            RetainedDiligentFunctions.Contains(function.NativeName) ||
            RetainedDiligentFunctions.Any(name =>
                function.CppQualifiedName.EndsWith($"::{name}", StringComparison.Ordinal));
    }

    private static bool IsRetainedDiligentInterfaceMethod(RecordBinding record, FunctionBinding method)
    {
        if (HasUnsupportedDiligentInterfaceSignature(method))
            return false;

        return record.CppQualifiedName switch
        {
            "Diligent::IObject" =>
                RetainedDiligentBaseInterfaceMethods.Contains(method.ManagedName) ||
                RetainedDiligentBaseInterfaceMethods.Contains(method.NativeName) ||
                RetainedDiligentBaseInterfaceMethods.Any(name => method.CppQualifiedName.EndsWith($"::{name}", StringComparison.Ordinal)),
            _ => true
        };
    }

    private static bool HasUnsupportedDiligentInterfaceSignature(FunctionBinding method)
    {
        if (method.ReturnType.Kind == BindingTypeKind.Primitive && method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Unsupported)
            return true;

        foreach (var parameter in method.Parameters)
        {
            if (parameter.CppTypeName.Contains("&*", StringComparison.Ordinal) ||
                parameter.CppTypeName.Contains("const void**", StringComparison.Ordinal) ||
                parameter.CppTypeName.Contains("void**", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int PruneInvalidTypeAliases(BindingCompilation compilation)
    {
        var prunedCount = 0;
        for (var index = compilation.TypeAliases.Count - 1; index >= 0; --index)
        {
            var alias = compilation.TypeAliases[index];
            if (alias.TargetType.Kind == BindingTypeKind.Primitive &&
                alias.TargetType.PrimitiveKind == BindingPrimitiveKind.Void)
            {
                compilation.TypeAliases.RemoveAt(index);
                prunedCount++;
            }
        }

        return prunedCount;
    }

    private static void FixSdlVoidPointerFunctions(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "sdl")
            return;

        // SDL_GLContext is a typedef of void*. Alias resolution must not turn the
        // unrelated SDL_free(void*) parameter into an SDL_GLContext wrapper.
        var free = compilation.Functions.FirstOrDefault(static function => function.NativeName == "SDL_free");
        if (free is null || free.Parameters.Count != 1)
            return;

        free.Parameters[0].Type = BindingType.IntPtr;
        free.Parameters[0].CppTypeName = "void*";
        free.Parameters[0].AbiKind = BindingParameterAbiKind.Pointer;
    }

    private static void FixStbVorbisBufferFunctions(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "stb")
            return;

        var function = compilation.Functions.FirstOrDefault(static item => item.NativeName == "stb_vorbis_get_samples_short_interleaved");
        if (function is null || function.Parameters.Count != 4)
            return;

        function.Parameters[2].Type = BindingType.IntPtr;
        function.Parameters[2].Direction = BindingParameterDirection.In;
        function.Parameters[2].AbiKind = BindingParameterAbiKind.Value;
    }

    private static void FixHarfBuzzTypes(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "harfbuzz")
            return;

        var createBlob = compilation.Functions.First(static function =>
            function.NativeName == "hb_blob_create_from_file_or_fail");
        var fileName = createBlob.Parameters.First(static parameter => parameter.NativeName == "file_name");
        fileName.Type = BindingType.CreateConfigured("const char*", "string");
        fileName.Direction = BindingParameterDirection.In;
        fileName.AbiKind = BindingParameterAbiKind.Value;
        fileName.CppTypeName = "const char*";
    }

    private static int PruneMembers(IList<BindingNode> members, BindingSurfaceDescriptor descriptor)
    {
        var prunedCount = 0;
        for (var index = members.Count - 1; index >= 0; --index)
        {
            var member = members[index];
            if (member is RecordBinding record)
            {
                prunedCount += PruneMembers(record.Members, descriptor);
                if (!IsRetainedDependencyRecord(record, descriptor))
                {
                    members.RemoveAt(index);
                    prunedCount++;
                }
            }
            else if (member is EnumBinding)
            {
                members.RemoveAt(index);
                prunedCount++;
            }
            else if (member is FunctionBinding function)
            {
                if (!IsRetainedDependencyFunction(function, descriptor))
                {
                    members.RemoveAt(index);
                    prunedCount++;
                }
            }
            else if (member is ManagedClassBinding)
            {
                members.RemoveAt(index);
                prunedCount++;
            }
        }

        return prunedCount;
    }

    private static bool IsRetainedDependencyFunction(FunctionBinding function, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "sdl")
            return false;

        return RetainedDependencyFunctions.Contains(function.CppQualifiedName) ||
            RetainedDependencyFunctions.Contains(function.NativeName) ||
            RetainedDependencyFunctions.Contains(function.ManagedName) ||
            RetainedDependencyFunctions.Any(name =>
                function.CppQualifiedName.EndsWith($"::{name}", StringComparison.Ordinal) ||
                function.NativeName.EndsWith(name, StringComparison.Ordinal));
    }

    private static bool IsRetainedDependencyRecord(RecordBinding record, BindingSurfaceDescriptor descriptor)
    {
        return descriptor.Surface == "sdl" &&
            (RetainedDependencyRecords.Contains(record.CppQualifiedName) ||
             RetainedDependencyRecords.Contains(record.NativeName) ||
             RetainedDependencyRecords.Contains(record.ManagedName));
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: RedBolt.Generator.Dependencies --surface <sdl|diligent|dearimgui|stb|lz4|harfbuzz|rmlui|webp|xatlas|glslang|spirvcross|spirvtools> --header <path> --include-name <include> --output-dir <dir> --file-stem <stem> --metadata <path> --log <path> --sharpcpp-root <path> --compiler <path> [--include <dir>]... [--define <name[=value]>]... [--owned-header <path>]...");
    }

    private sealed record BindingSurfaceDescriptor(
        string Surface,
        string ProjectName,
        string ManagedNamespace,
        string ManagedClassName,
        string NativeLibraryName,
        string EntryPointPrefix,
        string ClangLanguageArgument,
        string LanguageStandardArgument,
        bool UseMarshallerConfiguration)
    {
        public static BindingSurfaceDescriptor Create(string surface, string nativeLibraryName)
        {
            return surface.ToLowerInvariant() switch
            {
                "sdl" => new BindingSurfaceDescriptor(
                    "sdl",
                    "RedBolt.Bindings.Sdl",
                    "RedBolt.Bindings.Sdl",
                    "SdlNativeBindings",
                    nativeLibraryName,
                    "redbolt_sdl",
                    "-xc",
                    "-std=c17",
                    true),
                "diligent" => new BindingSurfaceDescriptor(
                    "diligent",
                    "RedBolt.Bindings.Diligent",
                    "RedBolt.Bindings.Diligent",
                    "DiligentNativeBindings",
                    nativeLibraryName,
                    "redbolt_diligent",
                    "-xc++",
                    "-std=c++20",
                    true),
                "dearimgui" => new BindingSurfaceDescriptor(
                    "dearimgui",
                    "RedBolt.Bindings.DearImGui",
                    "RedBolt.Bindings.DearImGui",
                    "ImGui",
                    nativeLibraryName,
                    "redbolt_dearimgui",
                    "-xc++",
                    "-std=c++17",
                    true),
                "stb" => new BindingSurfaceDescriptor(
                    "stb",
                    "RedBolt.Bindings.Stb",
                    "RedBolt.Bindings.Stb",
                    "StbVorbisNativeBindings",
                    nativeLibraryName,
                    "redbolt_stb",
                    "-xc",
                    "-std=c17",
                    true),
                "lz4" => new BindingSurfaceDescriptor(
                    "lz4",
                    "RedBolt.Bindings.Lz4",
                    "RedBolt.Bindings.Lz4",
                    "Lz4NativeBindings",
                    nativeLibraryName,
                    "redbolt_lz4",
                    "-xc++",
                    "-std=c++20",
                    false),
                "harfbuzz" => new BindingSurfaceDescriptor(
                    "harfbuzz",
                    "RedBolt.Bindings.HarfBuzz",
                    "RedBolt.Bindings.HarfBuzz",
                    "HarfBuzzNativeBindings",
                    nativeLibraryName,
                    "redbolt_harfbuzz",
                    "-xc",
                    "-std=c17",
                    false),
                "rmlui" => new BindingSurfaceDescriptor(
                    "rmlui",
                    "RedBolt.Bindings.RmlUi",
                    "RedBolt.Bindings.RmlUi",
                    "RmlUiNativeBindings",
                    nativeLibraryName,
                    "redbolt_rmlui",
                    "-xc++",
                    "-std=c++20",
                    true),
                "webp" => new BindingSurfaceDescriptor(
                    "webp",
                    "RedBolt.Bindings.WebP",
                    "RedBolt.Bindings.WebP",
                    "WebPNativeBindings",
                    nativeLibraryName,
                    "redbolt_webp",
                    "-xc++",
                    "-std=c++20",
                    false),
                "xatlas" => new BindingSurfaceDescriptor(
                    "xatlas",
                    "RedBolt.Bindings.XAtlas",
                    "RedBolt.Bindings.XAtlas",
                    "XAtlasNativeBindings",
                    nativeLibraryName,
                    "redbolt_xatlas",
                    "-xc++",
                    "-std=c++20",
                    true),
                "glslang" => new BindingSurfaceDescriptor(
                    "glslang",
                    "RedBolt.Bindings.Glslang",
                    "RedBolt.Bindings.Glslang",
                    "GlslangNativeBindings",
                    nativeLibraryName,
                    "redbolt_glslang",
                    "-xc",
                    "-std=c17",
                    false),
                "spirvcross" => new BindingSurfaceDescriptor(
                    "spirvcross",
                    "RedBolt.Bindings.SpirVCross",
                    "RedBolt.Bindings.SpirVCross",
                    "SpirVCrossNativeBindings",
                    nativeLibraryName,
                    "redbolt_spirvcross",
                    "-xc",
                    "-std=c17",
                    false),
                "spirvtools" => new BindingSurfaceDescriptor(
                    "spirvtools",
                    "RedBolt.Bindings.SpirVTools",
                    "RedBolt.Bindings.SpirVTools",
                    "SpirVToolsNativeBindings",
                    nativeLibraryName,
                    "redbolt_spirvtools",
                    "-xc",
                    "-std=c17",
                    false),
                "spirvoptimizer" => new BindingSurfaceDescriptor(
                    "spirvoptimizer",
                    "RedBolt.Bindings.SpirVTools",
                    "RedBolt.Bindings.SpirVTools",
                    "SpirVOptimizerNativeBindings",
                    nativeLibraryName,
                    "redbolt_spirvoptimizer",
                    "-xc++",
                    "-std=c++20",
                    true),
                "shadercompiler" => new BindingSurfaceDescriptor(
                    "shadercompiler",
                    "RedBolt.Bindings.ShaderCompiler",
                    "RedBolt.Bindings.ShaderCompiler",
                    "ShaderCompilerNativeBindings",
                    nativeLibraryName,
                    "redbolt_shadercompiler",
                    "-xc++",
                    "-std=c++20",
                    true),
                _ => throw new ArgumentException($"Unknown binding surface '{surface}'.", nameof(surface))
            };
        }
    }

    private sealed class GeneratorOptions
    {
        public required string Surface { get; init; }
        public required string HeaderPath { get; init; }
        public required string HeaderIncludePath { get; init; }
        public required string OutputDirectory { get; init; }
        public required string FileStem { get; init; }
        public required string MetadataPath { get; init; }
        public required string LogPath { get; init; }
        public required string SharpCppRoot { get; init; }
        public required string RedBoltRoot { get; init; }
        public required string NativeLibraryName { get; init; }
        public required string CompilerPath { get; init; }
        public required IReadOnlyList<string> IncludeDirectories { get; init; }
        public required IReadOnlyList<string> Defines { get; init; }
        public required IReadOnlyList<string> OwnedHeaders { get; init; }

        public static GeneratorOptions Parse(string[] args)
        {
            var includes = GetRepeatedOption(args, "--include").Select(Path.GetFullPath).ToArray();
            var defines = GetRepeatedOption(args, "--define").ToArray();
            var ownedHeaders = GetRepeatedOption(args, "--owned-header").Select(Path.GetFullPath).ToArray();

            return new GeneratorOptions
            {
                Surface = RequireOption(args, "--surface"),
                HeaderPath = Path.GetFullPath(RequireOption(args, "--header")),
                HeaderIncludePath = RequireOption(args, "--include-name"),
                OutputDirectory = Path.GetFullPath(RequireOption(args, "--output-dir")),
                FileStem = RequireOption(args, "--file-stem"),
                MetadataPath = Path.GetFullPath(RequireOption(args, "--metadata")),
                LogPath = Path.GetFullPath(RequireOption(args, "--log")),
                SharpCppRoot = Path.GetFullPath(RequireOption(args, "--sharpcpp-root")),
                RedBoltRoot = Path.GetFullPath(RequireOption(args, "--redbolt-root")),
                NativeLibraryName = RequireOption(args, "--native-library-name"),
                CompilerPath = Path.GetFullPath(RequireOption(args, "--compiler")),
                IncludeDirectories = includes,
                Defines = defines,
                OwnedHeaders = ownedHeaders
            };
        }

        private static string RequireOption(string[] args, string name)
        {
            var value = GetOption(args, name);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Missing required option '{name}'.");

            return value;
        }

        private static string? GetOption(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; ++i)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            }

            return null;
        }

        private static IEnumerable<string> GetRepeatedOption(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; ++i)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    yield return args[i + 1];
            }
        }
    }
}
