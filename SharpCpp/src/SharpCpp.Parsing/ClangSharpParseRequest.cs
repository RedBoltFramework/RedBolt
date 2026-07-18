using SharpCpp.Metadata.Import;

namespace SharpCpp.Parsing;

public sealed record ClangSharpParseRequest(
    string ProjectName,
    string ManagedNamespace,
    string ManagedClassName,
    string NativeLibraryName,
    string HeaderFilePath,
    string HeaderIncludePath,
    IReadOnlyList<string> ClangArguments,
    IReadOnlyList<string> MarshallerConfigurationFiles,
    string EntryPointPrefix,
    IReadOnlyList<string>? OwnedFileRoots = null,
    IReadOnlyDictionary<string, string>? OwnedFileRootIncludePrefixes = null,
    BindingMetadataImportCatalog? ImportedBindingMetadata = null,
    IReadOnlySet<string>? DirectorRecordCppTypeNames = null,
    IReadOnlyList<string>? MarshallerClangArguments = null);
