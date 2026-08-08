namespace RedBolt.UserCode.Desktop;

using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using RedBolt.Generator.Reflection;

public sealed record ScriptSource(string Path, string Content);

/// <summary>A complete immutable snapshot of a desktop script bundle.</summary>
public sealed record SourceScriptBundle : ScriptBundleInput
{
    public SourceScriptBundle(
        string assemblyName,
        IEnumerable<ScriptSource> sources,
        IEnumerable<Assembly>? references = null,
        IEnumerable<string>? referencePaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        ArgumentNullException.ThrowIfNull(sources);
        AssemblyName = assemblyName;
        Sources = sources.ToArray();
        References = references?.ToArray() ?? [];
        ReferencePaths = referencePaths?.Select(Path.GetFullPath).ToArray() ?? [];
    }

    public string AssemblyName { get; }

    public IReadOnlyList<ScriptSource> Sources { get; }

    public IReadOnlyList<Assembly> References { get; }

    public IReadOnlyList<string> ReferencePaths { get; }
}

public abstract class DesktopScriptBackend : IScriptBackend
{
    private readonly IAssemblyBackend _assemblies;
    private string _fingerprint = string.Empty;

    protected DesktopScriptBackend(IAssemblyBackend assemblies)
    {
        _assemblies = assemblies;
    }

    public UserCodeAssembly? Current => _assemblies.Current;

    public ScriptPreparation Prepare(ScriptBundleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input is not SourceScriptBundle bundle)
        {
            return new ScriptPreparation(null, false, string.Empty,
                [new UserCodeDiagnostic(UserCodeDiagnosticSeverity.Error,
                    "A desktop script backend requires a source bundle.")]);
        }

        var validation = Validate(bundle);
        if (validation.Count != 0)
            return new ScriptPreparation(null, false, string.Empty, validation);

        var fingerprint = GetFingerprint(bundle);
        if (Current is not null && fingerprint == _fingerprint)
            return new ScriptPreparation(null, true, fingerprint, []);

        var compilation = Compile(bundle);
        if (compilation.Image is null)
            return new ScriptPreparation(null, false, fingerprint, compilation.Diagnostics);

        var assemblyPreparation = _assemblies.Prepare(new DesktopAssemblyImageInput(
            bundle.AssemblyName, compilation.Image, compilation.Pdb, bundle.References));
        var diagnostics = compilation.Diagnostics.Concat(assemblyPreparation.Diagnostics).ToArray();
        return new ScriptPreparation(assemblyPreparation, false, fingerprint, diagnostics);
    }

    public UserCodeAssembly Commit(ScriptPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (preparation.Unchanged)
            return Current ?? throw new InvalidOperationException("No script bundle is active.");
        using var assemblyPreparation = preparation.TakeAssemblyPreparation();
        var result = _assemblies.Commit(assemblyPreparation);
        _fingerprint = preparation.Fingerprint;
        return result;
    }

    public void Unload()
    {
        _assemblies.Unload();
        _fingerprint = string.Empty;
    }

    public void Dispose()
    {
        _assemblies.Dispose();
        GC.SuppressFinalize(this);
    }

    private static List<UserCodeDiagnostic> Validate(SourceScriptBundle bundle)
    {
        var diagnostics = new List<UserCodeDiagnostic>();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in bundle.Sources)
        {
            if (string.IsNullOrWhiteSpace(source.Path) || !source.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                diagnostics.Add(new UserCodeDiagnostic(UserCodeDiagnosticSeverity.Error,
                    "Script paths must have a .cs extension.", source.Path));
            else if (!paths.Add(source.Path.Replace('\\', '/')))
                diagnostics.Add(new UserCodeDiagnostic(UserCodeDiagnosticSeverity.Error,
                    "Script paths must be unique.", source.Path));
        }
        foreach (var path in bundle.ReferencePaths)
        {
            if (!File.Exists(path))
                diagnostics.Add(new UserCodeDiagnostic(UserCodeDiagnosticSeverity.Error,
                    "Metadata reference was not found.", path));
        }
        return diagnostics;
    }

    private static string GetFingerprint(SourceScriptBundle bundle)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(bundle.AssemblyName);
        foreach (var source in bundle.Sources.OrderBy(static source => source.Path, StringComparer.Ordinal))
        {
            Append(source.Path.Replace('\\', '/'));
            Append(source.Content);
        }
        foreach (var reference in bundle.References.OrderBy(static assembly => assembly.FullName, StringComparer.Ordinal))
            Append(reference.ManifestModule.ModuleVersionId.ToString("N"));
        foreach (var path in bundle.ReferencePaths.Order(StringComparer.Ordinal))
        {
            Append(path);
            var info = new FileInfo(path);
            Append(info.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(info.LastWriteTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return Convert.ToHexString(hash.GetHashAndReset());

        void Append(string value)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(value));
            hash.AppendData([0]);
        }
    }

    private static CompilationOutput Compile(SourceScriptBundle bundle)
    {
        var syntaxTrees = bundle.Sources.Select(source => CSharpSyntaxTree.ParseText(
            source.Content, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            source.Path.Replace('\\', '/'), Encoding.UTF8)).ToArray();
        var references = GetMetadataReferences(bundle);
        CSharpCompilation compilation = CSharpCompilation.Create(
            bundle.AssemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ReflectionGenerator().AsSourceGenerator()],
            parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var generatedCompilation, out var generatorDiagnostics);

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var emitResult = generatedCompilation.Emit(pe, pdb, options: new Microsoft.CodeAnalysis.Emit.EmitOptions(
            debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb));
        var allDiagnostics = generatorDiagnostics.Concat(emitResult.Diagnostics)
            .Where(static diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
            .Select(ToDiagnostic)
            .ToArray();
        return emitResult.Success
            ? new CompilationOutput(pe.ToArray(), pdb.ToArray(), allDiagnostics)
            : new CompilationOutput(null, null, allDiagnostics);
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences(SourceScriptBundle bundle)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string platformAssemblies)
        {
            foreach (var path in platformAssemblies.Split(Path.PathSeparator))
                paths.Add(path);
        }
        AddAssembly(typeof(Core.Context).Assembly);
        foreach (var assembly in bundle.References)
            AddAssembly(assembly);
        foreach (var path in bundle.ReferencePaths)
            paths.Add(path);
        return paths.Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToImmutableArray();

        void AddAssembly(Assembly assembly)
        {
            if (!string.IsNullOrEmpty(assembly.Location))
                paths.Add(assembly.Location);
        }
    }

    private static UserCodeDiagnostic ToDiagnostic(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        var position = span.StartLinePosition;
        return new UserCodeDiagnostic(
            diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => UserCodeDiagnosticSeverity.Error,
                DiagnosticSeverity.Warning => UserCodeDiagnosticSeverity.Warning,
                _ => UserCodeDiagnosticSeverity.Info,
            },
            diagnostic.GetMessage(),
            span.Path ?? string.Empty,
            position.Line + 1,
            position.Character + 1);
    }

    private sealed record CompilationOutput(
        byte[]? Image, byte[]? Pdb, IReadOnlyList<UserCodeDiagnostic> Diagnostics);
}

public sealed class DesktopLoadOnlyScriptBackend : DesktopScriptBackend
{
    public DesktopLoadOnlyScriptBackend() : base(new DesktopLoadOnlyAssemblyBackend())
    {
    }
}

public sealed class ReloadableScriptBackend : DesktopScriptBackend
{
    public ReloadableScriptBackend() : base(new ReloadableAssemblyBackend())
    {
    }
}
