// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

try
{
    return new Packager(args).Run();
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

internal sealed class Packager
{
    private const string Configuration = "Release";

    private static readonly string[] ModuleOrder =
    [
        "Core", "Audio", "Utility", "DearImGui"
    ];

    private static readonly Dictionary<string, string[]> NativeModules = new(StringComparer.Ordinal)
    {
        ["Core"] = ["SDL", "DILIGENT", "STB", "LZ4", "HARFBUZZ", "WEBP", "GLSLANG", "SPIRV_CROSS", "SPIRV_TOOLS", "SHADER_COMPILER"],
        ["DearImGui"] = ["IMGUI"],
        ["Utility"] = ["TINYGLTF"]
    };

    private static readonly Dictionary<string, string[]> NativeFallbackPatterns = new(StringComparer.Ordinal)
    {
        ["SDL"] = ["*RedBolt.Native.Sdl*"],
        ["DILIGENT"] = ["*RedBolt.Native.Diligent*"],
        ["IMGUI"] = ["*RedBolt.Native.DearImGui*"],
        ["STB"] = ["*RedBolt.Native.Stb*"],
        ["TINYGLTF"] = ["*RedBolt.Native.TinyGltf*"],
        ["LZ4"] = ["*RedBolt.Native.Lz4*"],
        ["HARFBUZZ"] = ["*RedBolt.Native.HarfBuzz*"],
        ["WEBP"] = ["*RedBolt.Native.WebP*"],
        ["GLSLANG"] = ["*RedBolt.Native.Glslang*"],
        ["SPIRV_CROSS"] = ["*RedBolt.Native.SpirVCross*"],
        ["SPIRV_TOOLS"] = ["*RedBolt.Native.SpirVTools*"],
        ["SHADER_COMPILER"] = ["*RedBolt.Native.ShaderCompiler*"]
    };

    private static readonly string ConsumerProgram = """
        using System;
        using System.IO;

        using SharpCpp.Parsing;
        using SharpCpp.Pipeline;
        using SharpCpp.Rendering;

        using RedBolt.Utility;

        var includeRoot = args[0];
        var workRoot = Path.Combine(AppContext.BaseDirectory, "generated");
        var header = Path.Combine(workRoot, "Fixture.hpp");
        var marshaller = Path.Combine(includeRoot, "SharpCpp", "Marshaller.hpp");
        Directory.CreateDirectory(workRoot);
        File.WriteAllText(header, "int fixture_add(int left, int right);");
        var clangArguments = new[] { "-xc++", "-std=c++20", $"-I{includeRoot}" };
        var compilation = new ClangSharpHeaderParser().Parse(new ClangSharpParseRequest(
            "Fixture", "Fixture", "FixtureNative", "Fixture.Native", header, "Fixture.hpp",
            clangArguments, new[] { marshaller }, "fixture"));
        BindingCompilationBootstrapPipeline.Apply(compilation, new[] { marshaller }, clangArguments);
        BindingOutputRenderer.Render(compilation, "Fixture").WriteToDirectory(workRoot);
        if (!File.Exists(Path.Combine(workRoot, "Fixture.Bindings.g.cs"))
            || !File.Exists(Path.Combine(workRoot, "Fixture.Bindings.g.cpp")))
            throw new InvalidOperationException("SharpCpp did not generate both binding outputs.");
        Vector3f alias = default;
        Console.WriteLine($"{RedBolt.Core.Context.Instance.GetType().FullName} {Icons.Folder} {alias}");
        """;

    private readonly string[] _arguments;
    private readonly string _scriptDirectory;
    private readonly string _repositoryRoot;
    private readonly string _sharpCppRoot;
    private readonly string _buildRoot;
    private readonly string _packageProject;
    private readonly string _packageRoot;
    private readonly string _stageRoot;
    private readonly string _outputRoot;
    private readonly Dictionary<string, ProjectMetadata> _metadata = new(PathComparer);
    private readonly List<string> _rids = [];
    private readonly List<string> _modules = [];
    private Dictionary<string, string> _selection = new(StringComparer.Ordinal);
    private string _packageVersion = "";
    private string _repositoryCommit = "";
    private bool _skipBuild;

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public Packager(string[] arguments)
    {
        _arguments = arguments;
        _scriptDirectory = Path.GetDirectoryName(GetSourcePath())!;
        _repositoryRoot = Path.GetFullPath(Path.Combine(_scriptDirectory, "..", ".."));
        _buildRoot = ResolveBuildRoot(_repositoryRoot);
        _sharpCppRoot = ResolveSharpCppRoot(_repositoryRoot);
        _packageProject = Path.Combine(_scriptDirectory, "RedBolt.Package.csproj");
        _packageRoot = ResolvePackageRoot(_repositoryRoot);
        _stageRoot = Path.Combine(_packageRoot, "stage");
        _outputRoot = Path.Combine(_packageRoot, "out");
    }

    public int Run()
    {
        if (_arguments.Length == 0 || _arguments[0] is not ("create" or "validate" or "version"))
            return Usage();

        var command = _arguments[0];
        foreach (var argument in _arguments.Skip(1))
        {
            if (argument == "--no-build")
                _skipBuild = true;
            else
                return Usage();
        }

        _packageVersion = GitVersion();
        if (command == "version")
        {
            Console.WriteLine(_packageVersion);
            return 0;
        }

        _repositoryCommit = Git("rev-parse", "HEAD").Trim();
        ReadSelection();
        if (command == "validate")
        {
            ValidatePackages();
            return 0;
        }

        if (!_skipBuild)
            RunCommand("dotnet", ["build", Path.Combine(_repositoryRoot, "RedBolt.slnx"), "--configuration", Configuration, BuildRootProperty]);

        RecreateDirectory(_stageRoot);
        RecreateDirectory(_outputRoot);
        Directory.CreateDirectory(_packageRoot);
        File.WriteAllText(Path.Combine(_packageRoot, "version.txt"), _packageVersion + Environment.NewLine);

        CreateSharpCppPackages();
        foreach (var module in _modules)
            StageManagedPackage(module);
        foreach (var name in new[] { "CoreData", "SampleData", "EditorData" })
            StageResourcePackage(name);
        foreach (var rid in _rids)
        foreach (var module in _modules.Where(module =>
                     SupportedOnRid(module, rid) && HasRuntimePackage(module, rid)))
            StageRuntimePackage(module, rid);
        foreach (var rid in _rids.Where(IsDesktopRid))
        {
            if (!_skipBuild)
            {
                RunCommand("dotnet",
                [
                    "build", Path.Combine(_repositoryRoot, "src", "RedBolt", "Tools", "PackageTool", "RedBolt.PackageTool.csproj"),
                    "--configuration", Configuration,
                    BuildRootProperty,
                    $"-p:RedBoltPlatform={rid}",
                    "-p:RedBoltIgnoreBuildSelection=true"
                ]);
            }
            StageToolPackage("PackageTool", rid);
            if (IsEnabled("RedBoltEditorEnabled"))
                StageToolPackage("Editor", rid);
        }

        ValidatePackages();
        return 0;
    }

    private static string GetSourcePath([CallerFilePath] string path = "") => path;

    // Same rule as the MSBuild props: an explicit REDBOLT_BUILD_ROOT, the monorepo root's build/ when
    // RedBolt.slnx sits beside this repository, otherwise this repository's build/.
    private static string ResolveBuildRoot(string repositoryRoot)
    {
        var explicitRoot = Environment.GetEnvironmentVariable("REDBOLT_BUILD_ROOT");
        if (!string.IsNullOrEmpty(explicitRoot))
            return Path.GetFullPath(explicitRoot);
        var parent = Path.GetFullPath(Path.Combine(repositoryRoot, ".."));
        return File.Exists(Path.Combine(parent, "RedBolt.slnx"))
            ? Path.Combine(parent, "build")
            : Path.Combine(repositoryRoot, "build");
    }

    // Same rule as the MSBuild props: an explicit RedBoltSharpCppRoot, the monorepo's extern/SharpCpp when
    // this repository sits inside the monorepo and that checkout exists, otherwise the CPM checkout below
    // this repository.
    private static string ResolveSharpCppRoot(string repositoryRoot)
    {
        var explicitRoot = Environment.GetEnvironmentVariable("RedBoltSharpCppRoot");
        if (!string.IsNullOrEmpty(explicitRoot))
            return Path.GetFullPath(explicitRoot);
        var parent = Path.GetFullPath(Path.Combine(repositoryRoot, ".."));
        var monorepoRoot = Path.Combine(parent, "extern", "SharpCpp");
        return File.Exists(Path.Combine(parent, "RedBolt.slnx")) && File.Exists(Path.Combine(monorepoRoot, "CMakeLists.txt"))
            ? monorepoRoot
            : Path.Combine(repositoryRoot, "extern", "SharpCpp");
    }

    // Package outputs are namespaced per repository below the build root, because the monorepo shares one
    // build root: <build root>/package/nuget/<repository directory name>/{stage,out,version.txt,...}.
    private static string ResolvePackageRoot(string repositoryRoot) =>
        Path.Combine(ResolveBuildRoot(repositoryRoot), "package", "nuget", Path.GetFileName(repositoryRoot));

    private string BuildRootProperty => $"-p:RedBoltBuildRoot={_buildRoot}";

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: dotnet run --file packaging/nuget/package.cs -- create [--no-build]");
        Console.Error.WriteLine("       dotnet run --file packaging/nuget/package.cs -- validate");
        Console.Error.WriteLine("       dotnet run --file packaging/nuget/package.cs -- version");
        return 2;
    }

    private string GitVersion()
    {
        var sha = Git("rev-parse", "--short=8", "HEAD").Trim();
        var height = Git("rev-list", "--count", "HEAD").Trim();
        var tag = GitOptional("describe", "--tags", "--match", "v[0-9]*.[0-9]*.[0-9]*", "--abbrev=0").Trim();
        var exact = GitOptional("describe", "--tags", "--match", "v[0-9]*.[0-9]*.[0-9]*", "--exact-match").Trim();
        var dirty = Capture("git", ["-C", _repositoryRoot, "diff", "--quiet"], allowFailure: true).ExitCode != 0
                    || Capture("git", ["-C", _repositoryRoot, "diff", "--cached", "--quiet"], allowFailure: true).ExitCode != 0
                    || Git("ls-files", "--others", "--exclude-standard").Length != 0;
        if (exact.Length != 0 && !dirty)
            return exact[1..];
        var suffix = dirty ? ".dirty" : "";
        if (tag.Length == 0)
            return $"0.1.0-dev.{height}{suffix}+{sha}";
        var parts = tag[1..].Split('.').Select(int.Parse).ToArray();
        var distance = Git("rev-list", "--count", $"{tag}..HEAD").Trim();
        return $"{parts[0]}.{parts[1]}.{parts[2] + 1}-dev.{distance}{suffix}+{sha}";
    }

    private string Git(params string[] arguments) =>
        Capture("git", ["-C", _repositoryRoot, .. arguments]).Output;

    private string GitOptional(params string[] arguments) =>
        Capture("git", ["-C", _repositoryRoot, .. arguments], allowFailure: true).Output;

    private void ReadSelection()
    {
        const string properties = "RedBoltLinuxX64Enabled,RedBoltWinX64Enabled,RedBoltAndroidX64Enabled,RedBoltAndroidArm64Enabled,RedBoltBrowserWasmEnabled,RedBoltAudioEnabled,RedBoltDearImGuiEnabled,RedBoltUtilityEnabled,RedBoltEditorEnabled";
        var json = Capture("dotnet",
        [
            "msbuild", Path.Combine(_repositoryRoot, "src", "RedBolt", "Core", "RedBolt.Core.csproj"),
            BuildRootProperty,
            $"-getProperty:{properties}"
        ]).Output;
        using var document = JsonDocument.Parse(json);
        _selection = document.RootElement.GetProperty("Properties").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString() ?? "", StringComparer.Ordinal);

        if (IsEnabled("RedBoltLinuxX64Enabled")) _rids.Add("linux-x64");
        if (IsEnabled("RedBoltWinX64Enabled")) _rids.Add("win-x64");
        if (IsEnabled("RedBoltAndroidX64Enabled")) _rids.Add("android-x64");
        if (IsEnabled("RedBoltAndroidArm64Enabled")) _rids.Add("android-arm64");
        if (IsEnabled("RedBoltBrowserWasmEnabled")) _rids.Add("browser-wasm");
        if (_rids.Count == 0)
            throw new InvalidOperationException("No packageable RedBolt RIDs are enabled.");

        _modules.Add("Core");
        _modules.AddRange(ModuleOrder.Skip(1).Where(module => IsEnabled($"RedBolt{module}Enabled")));
    }

    private bool IsEnabled(string property) =>
        _selection.TryGetValue(property, out var value) && value.Equals("true", StringComparison.OrdinalIgnoreCase);

    private static bool IsDesktopRid(string rid) => rid.StartsWith("linux-", StringComparison.Ordinal)
                                                     || rid.StartsWith("win-", StringComparison.Ordinal);

    private static bool SupportedOnRid(string module, string rid) =>
        module != "Utility" || rid != "browser-wasm";

    private string ManagedFile(string rid, string project, string? assembly = null)
    {
        assembly ??= project;
        if (IsDesktopRid(rid))
        {
            var directory = project.StartsWith("RedBolt.Bindings.", StringComparison.Ordinal) ? "extern" : "core";
            return Path.Combine(_buildRoot, "bin", Configuration, rid, directory, assembly + ".dll");
        }
        return Path.Combine(_buildRoot, "obj", "managed", Configuration, rid, "staging", project, assembly + ".dll");
    }

    private void StageCommon(string stage)
    {
        Directory.CreateDirectory(stage);
        File.Copy(Path.Combine(_scriptDirectory, "README.md"), Path.Combine(stage, "README.md"), true);
        File.Copy(Path.Combine(_repositoryRoot, "LICENSE"), Path.Combine(stage, "LICENSE.txt"), true);
    }

    private ProjectMetadata Metadata(string project, string? rid = null)
    {
        project = Path.GetFullPath(project);
        rid ??= _rids[0];
        var cacheKey = project + "\n" + rid;
        if (_metadata.TryGetValue(cacheKey, out var cached))
            return cached;
        var json = Capture("dotnet",
        [
            "msbuild", project,
            BuildRootProperty,
            "-getItem:ProjectReference,PackageReference,Compile",
            "-getProperty:ImplicitUsings,Nullable,AllowUnsafeBlocks",
            $"-p:Configuration={Configuration}",
            $"-p:RedBoltPlatform={rid}",
            "-p:RedBoltIgnoreBuildSelection=true",
            "--nologo"
        ]).Output;
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var items = root.GetProperty("Items");
        var projectReferences = ReadItems(items, "ProjectReference")
            .Select(item => new ProjectReference(
                String(item, "Filename"),
                !String(item, "ReferenceOutputAssembly").Equals("false", StringComparison.OrdinalIgnoreCase)))
            .Where(reference => reference.Name.Length != 0)
            .ToArray();
        var packageReferences = ReadItems(items, "PackageReference")
            .Select(item => new PackageReference(
                String(item, "Identity"), String(item, "Version"), String(item, "PrivateAssets")))
            .Where(reference => reference.Id.Length != 0)
            .ToArray();
        var compileFiles = ReadItems(items, "Compile")
            .Select(item => String(item, "FullPath"))
            .Where(path => path.Length != 0)
            .Distinct(PathComparer)
            .ToArray();
        var properties = root.TryGetProperty("Properties", out var propertyObject)
            ? propertyObject.EnumerateObject().ToDictionary(
                property => property.Name, property => property.Value.GetString() ?? "", StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        cached = new ProjectMetadata(projectReferences, packageReferences, compileFiles, properties);
        _metadata.Add(cacheKey, cached);
        return cached;
    }

    private static IEnumerable<JsonElement> ReadItems(JsonElement items, string name) =>
        items.TryGetProperty(name, out var array) ? array.EnumerateArray() : [];

    private static string String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";

    private IEnumerable<Dependency> PackageDependencies(string project, string? rid = null)
    {
        var metadata = Metadata(project, rid);
        foreach (var reference in metadata.ProjectReferences.Where(reference => reference.ReferenceOutputAssembly))
        {
            if (reference.Name.StartsWith("RedBolt.", StringComparison.Ordinal)
                && _modules.Contains(reference.Name["RedBolt.".Length..], StringComparer.Ordinal))
                yield return new Dependency(reference.Name, _packageVersion);
            else if (reference.Name.StartsWith("SharpCpp.", StringComparison.Ordinal))
                yield return new Dependency(reference.Name, _packageVersion);
        }
        foreach (var reference in metadata.PackageReferences)
        {
            if (reference.PrivateAssets.Equals("all", StringComparison.OrdinalIgnoreCase)
                || reference.Id.StartsWith("libClang.runtime.", StringComparison.Ordinal)
                || reference.Id.StartsWith("libClangSharp.runtime.", StringComparison.Ordinal))
                continue;
            yield return new Dependency(reference.Id, reference.Version);
        }
    }

    private void MakePackage(string id, string description, string stage, IEnumerable<Dependency> dependencies)
    {
        var spec = Path.Combine(stage, "package.props");
        WriteXml(spec,
            new XElement("Project",
                new XElement("ItemGroup",
                    dependencies.Distinct().OrderBy(dependency => dependency.Id, StringComparer.Ordinal)
                        .Select(dependency => new XElement("PackageReference",
                            new XAttribute("Include", dependency.Id),
                            new XAttribute("Version", dependency.Version),
                            new XAttribute("PrivateAssets", "none"))))));

        string[] properties =
        [
            $"-p:RedBoltPackageId={id}",
            $"-p:RedBoltPackageVersion={_packageVersion}",
            $"-p:RedBoltPackageDescription={description}",
            $"-p:RedBoltPackageStage={stage}",
            $"-p:RedBoltPackageSpec={spec}",
            BuildRootProperty,
            $"-p:RedBoltRepositoryCommit={_repositoryCommit}",
            $"-p:PackageOutputPath={_outputRoot}",
            $"-p:RestoreAdditionalProjectSources={_outputRoot}"
        ];
        RunCommand("dotnet", ["restore", _packageProject, .. properties, "--nologo"]);
        RunCommand("dotnet",
        [
            "pack", _packageProject, "--configuration", Configuration, "--no-build", "--no-restore",
            .. properties, "--nologo"
        ]);
    }

    private void CreateSharpCppPackages()
    {
        StageSharpCppPackage("SharpCpp.Support", "Managed and native support contracts for generated SharpCpp bindings.");
        StageSharpCppPackage("SharpCpp.Std", "Managed C++ standard-library wrappers for SharpCpp.");
        StageSharpCppPackage("SharpCpp.Model", "Reusable SharpCpp binding model.");
        StageSharpCppPackage("SharpCpp.Metadata", "SharpCpp binding metadata import and export.");
        StageSharpCppPackage("SharpCpp.Parsing", "Clang-based C and C++ parsing for SharpCpp.");
        StageSharpCppPackage("SharpCpp.Pipeline", "Reusable SharpCpp binding transformation pipeline.");
        StageSharpCppPackage("SharpCpp.Rendering", "Managed and native binding rendering for SharpCpp.");

        var parsingProject = Path.Combine(_sharpCppRoot, "src", "SharpCpp.Parsing", "SharpCpp.Parsing.csproj");
        var runtimeReferences = Metadata(parsingProject).PackageReferences
            .Where(reference => Regex.IsMatch(reference.Id, "^libClang(Sharp)?\\.runtime\\."))
            .ToArray();
        foreach (var rid in new[] { "linux-x64", "win-x64" })
        {
            var id = $"SharpCpp.Parsing.Runtime.{rid}";
            var stage = Path.Combine(_stageRoot, id);
            StageCommon(stage);
            var dependencies = runtimeReferences.Select(reference => new Dependency(
                    Regex.Replace(reference.Id, @"\.[^.]+-[^.]+$", "." + rid), reference.Version))
                .Prepend(new Dependency("SharpCpp.Parsing", _packageVersion));
            MakePackage(id, $"Native Clang runtime for SharpCpp parsing on {rid}.", stage, dependencies);
        }
    }

    private void StageSharpCppPackage(string package, string description)
    {
        var stage = Path.Combine(_stageRoot, package);
        var source = Path.Combine(_buildRoot, "bin", Configuration, "host", "extern", package + ".dll");
        StageCommon(stage);
        File.Copy(Path.Combine(_scriptDirectory, "SharpCpp.LICENSE.txt"), Path.Combine(stage, "LICENSE.txt"), true);
        RequireFile(source, "SharpCpp assembly");
        var library = Path.Combine(stage, "lib", "net10.0");
        Directory.CreateDirectory(library);
        File.Copy(source, Path.Combine(library, Path.GetFileName(source)), true);
        if (package == "SharpCpp.Support")
        {
            CopyDirectory(
                Path.Combine(_sharpCppRoot, "include", "SharpCpp"),
                Path.Combine(stage, "content", "include", "SharpCpp"));
            WriteXml(Path.Combine(stage, "buildTransitive", "SharpCpp.Support.props"),
                new XElement("Project",
                    new XElement("PropertyGroup",
                        new XElement("SharpCppIncludeRoot",
                            "$([System.IO.Path]::GetFullPath(`$(MSBuildThisFileDirectory)../content/include`))"))));
        }
        var project = Path.Combine(_sharpCppRoot, "src", package, package + ".csproj");
        MakePackage(package, description, stage, PackageDependencies(project));
    }

    private void StageManagedPackage(string module)
    {
        var package = $"RedBolt.{module}";
        var project = Path.Combine(_repositoryRoot, "src", "RedBolt", module, package + ".csproj");
        var stage = Path.Combine(_stageRoot, package);
        StageCommon(stage);
        Directory.CreateDirectory(Path.Combine(stage, "lib", "net10.0"));
        File.WriteAllBytes(Path.Combine(stage, "lib", "net10.0", "_._"), []);
        foreach (var rid in _rids.Where(rid => SupportedOnRid(module, rid)))
        {
            var source = ManagedFile(rid, package);
            RequireFile(source, "managed assembly");
            var library = Path.Combine(stage, "redbolt", rid, "lib");
            Directory.CreateDirectory(library);
            File.Copy(source, Path.Combine(library, Path.GetFileName(source)), true);
            foreach (var binding in Metadata(project, rid).ProjectReferences
                         .Where(reference => reference.Name.StartsWith("RedBolt.Bindings.", StringComparison.Ordinal))
                         .Select(reference => reference.Name)
                         .Order())
            {
                var bindingSource = ManagedFile(rid, binding);
                RequireFile(bindingSource, "binding assembly");
                File.Copy(bindingSource, Path.Combine(library, Path.GetFileName(bindingSource)), true);
            }
        }

        var buildTransitive = Path.Combine(stage, "buildTransitive");
        Directory.CreateDirectory(buildTransitive);
        if (module == "Core")
        {
            File.Copy(
                Path.Combine(_repositoryRoot, "extern", "IconFontCppHeaders", "licence.txt"),
                Path.Combine(stage, "IconFontCppHeaders.LICENSE.txt"), true);
            var analyzer = Path.Combine(_buildRoot, "bin", Configuration, "host", "tools", "RedBolt.Generator.Reflection.dll");
            RequireFile(analyzer, "reflection analyzer");
            var analyzers = Path.Combine(stage, "analyzers", "dotnet", "cs");
            Directory.CreateDirectory(analyzers);
            File.Copy(analyzer, Path.Combine(analyzers, Path.GetFileName(analyzer)), true);
            File.Copy(Path.Combine(_repositoryRoot, "src", "RedBolt", "Core", "GlobalAliases.cs"),
                Path.Combine(buildTransitive, "RedBolt.GlobalAliases.cs"), true);
            WriteXml(Path.Combine(buildTransitive, package + ".props"),
                new XElement("Project",
                    new XElement("ItemGroup",
                        new XElement("Compile",
                            new XAttribute("Include", "$(MSBuildThisFileDirectory)RedBolt.GlobalAliases.cs"),
                            new XAttribute("Link", "GlobalAliases.cs"),
                            new XAttribute("Visible", "false")))));
        }

        var assetRoot = $"_RedBolt{module}AssetRoot";
        WriteXml(Path.Combine(buildTransitive, package + ".targets"),
            new XElement("Project",
                new XElement("PropertyGroup",
                    new XElement(assetRoot,
                        $"$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory)../redbolt/$(RuntimeIdentifier)/lib'))")),
                new XElement("ItemGroup",
                    new XAttribute("Condition", $"Exists('$({assetRoot})/RedBolt.{module}.dll')"),
                    new XElement("Reference",
                        new XAttribute("Include", $"$({assetRoot})/*.dll"),
                        new XElement("Private", "true"))),
                new XElement("Target",
                    new XAttribute("Name", $"RedBoltValidate{module}RuntimeIdentifier"),
                    new XAttribute("BeforeTargets", "ResolveReferences"),
                    new XElement("Error",
                        new XAttribute("Condition", "'$(RuntimeIdentifier)' == ''"),
                        new XAttribute("Text", $"{package} requires one RuntimeIdentifier.")),
                    new XElement("Error",
                        new XAttribute("Condition", $"'$(RuntimeIdentifier)' != '' and !Exists('$({assetRoot})/RedBolt.{module}.dll')"),
                        new XAttribute("Text", $"{package} does not support RuntimeIdentifier '$(RuntimeIdentifier)'.")))));

        MakePackage(package, $"{module} module for the RedBolt engine.", stage, PackageDependencies(project));
    }

    private IEnumerable<Dependency> RequiredRuntimePackages(string module, string rid) =>
        RequiredRuntimePackages(module, rid, new HashSet<string>(StringComparer.Ordinal));

    private IEnumerable<Dependency> RequiredRuntimePackages(string module, string rid, HashSet<string> visited)
    {
        if (!visited.Add(module))
            yield break;

        var project = Path.Combine(_repositoryRoot, "src", "RedBolt", module, $"RedBolt.{module}.csproj");
        foreach (var reference in Metadata(project, rid).ProjectReferences
                     .Where(reference => reference.ReferenceOutputAssembly
                                         && reference.Name.StartsWith("RedBolt.", StringComparison.Ordinal)))
        {
            var referencedModule = reference.Name["RedBolt.".Length..];
            if (!_modules.Contains(referencedModule, StringComparer.Ordinal)
                || !SupportedOnRid(referencedModule, rid))
                continue;
            if (HasRuntimePackage(referencedModule, rid))
                yield return new Dependency($"{reference.Name}.Runtime.{rid}", _packageVersion);
            else
                foreach (var dependency in RequiredRuntimePackages(referencedModule, rid, visited))
                    yield return dependency;
        }
    }

    private IEnumerable<Dependency> ModulePackages(string module, string rid)
    {
        if (HasRuntimePackage(module, rid))
        {
            yield return new Dependency($"RedBolt.{module}.Runtime.{rid}", _packageVersion);
            yield break;
        }

        yield return new Dependency($"RedBolt.{module}", _packageVersion);
        foreach (var dependency in RequiredRuntimePackages(module, rid))
            yield return dependency;
    }

    private void StageRuntimePackage(string module, string rid)
    {
        var package = $"RedBolt.{module}.Runtime.{rid}";
        var stage = Path.Combine(_stageRoot, package);
        StageCommon(stage);
        if (rid == "browser-wasm")
        {
            if (module == "Core")
            {
                var artifact = Path.Combine(_buildRoot, "native", "redbolt", rid, Configuration, "lib", Configuration, "wasm", "RedBolt.Native.Web.a");
                RequireFile(artifact, "WebAssembly runtime archive");
                var native = Path.Combine(stage, "runtimes", rid, "native");
                Directory.CreateDirectory(native);
                File.Copy(artifact, Path.Combine(native, Path.GetFileName(artifact)), true);
                WriteXml(Path.Combine(stage, "buildTransitive", package + ".targets"),
                    new XElement("Project",
                        new XElement("ItemGroup",
                            new XAttribute("Condition", "'$(RuntimeIdentifier)' == 'browser-wasm'"),
                            new XElement("NativeFileReference",
                                new XAttribute("Include", "$(MSBuildThisFileDirectory)../runtimes/browser-wasm/native/RedBolt.Native.Web.a")))));
            }
        }
        else
        {
            var nativeRoot = Path.Combine(_buildRoot, "bin", Configuration, rid, "extern", "runtimes", rid, "native");
            if (!Directory.Exists(nativeRoot))
                throw new InvalidOperationException($"Missing native runtime directory: {nativeRoot}");
            var destination = Path.Combine(stage, "runtimes", rid, "native");
            Directory.CreateDirectory(destination);
            foreach (var nativeModule in NativeModulesFor(module, rid))
            {
                var manifest = Path.Combine(_buildRoot, "native", "redbolt", rid, Configuration, "generated", "native-modules", nativeModule + ".json");
                if (File.Exists(manifest))
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(manifest));
                    foreach (var artifact in document.RootElement.GetProperty("artifacts").EnumerateArray()
                                 .Select(element => element.GetString() ?? "").Where(name => name.Length != 0))
                    {
                        var source = Path.Combine(nativeRoot, artifact);
                        RequireFile(source, "manifest artifact");
                        File.Copy(source, Path.Combine(destination, artifact), true);
                    }
                }
                else
                    CopyNativeFallback(nativeModule, nativeRoot, destination);
            }
        }

        var dependencies = RequiredRuntimePackages(module, rid)
            .Prepend(new Dependency($"RedBolt.{module}", _packageVersion));
        MakePackage(package, $"Runtime assets for RedBolt {module} on {rid}.", stage, dependencies);
    }

    private static bool HasRuntimePackage(string module, string rid) =>
        rid == "browser-wasm" ? module == "Core" : NativeModulesFor(module, rid).Any();

    private static IEnumerable<string> NativeModulesFor(string module, string rid)
    {
        if (!NativeModules.TryGetValue(module, out var modules))
            return [];
        return modules;
    }

    private static void CopyNativeFallback(string nativeModule, string sourceRoot, string destination)
    {
        if (!NativeFallbackPatterns.TryGetValue(nativeModule, out var patterns))
            return;
        var files = patterns.SelectMany(pattern => Directory.GetFiles(sourceRoot, pattern, SearchOption.TopDirectoryOnly))
            .Distinct(PathComparer);
        foreach (var file in files)
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
    }

    private void StageResourcePackage(string name)
    {
        var package = $"RedBolt.{name}";
        var stage = Path.Combine(_stageRoot, package);
        StageCommon(stage);
        CopyDirectory(Path.Combine(_repositoryRoot, "bin", name), Path.Combine(stage, "content", name));
        Directory.CreateDirectory(Path.Combine(stage, "lib", "net10.0"));
        File.WriteAllBytes(Path.Combine(stage, "lib", "net10.0", "_._"), []);
        WriteXml(Path.Combine(stage, "buildTransitive", package + ".targets"),
            new XElement("Project",
                new XElement("ItemGroup",
                    new XElement("None",
                        new XAttribute("Include", $"$(MSBuildThisFileDirectory)../content/{name}/**/*"),
                        new XElement("Link", $"{name}/%(RecursiveDir)%(Filename)%(Extension)"),
                        new XElement("CopyToOutputDirectory", "PreserveNewest"),
                        new XElement("CopyToPublishDirectory", "PreserveNewest"),
                        new XElement("TargetPath", $"{name}/%(RecursiveDir)%(Filename)%(Extension)")))));
        MakePackage(package, $"{name} resources for RedBolt applications.", stage, []);
    }

    private void StageToolPackage(string tool, string rid)
    {
        var package = $"RedBolt.{tool}.{rid}";
        var stage = Path.Combine(_stageRoot, package);
        var sourceRoot = tool == "Editor"
            ? Path.Combine(_buildRoot, "bin", Configuration, rid, "tools")
            : Path.Combine(_buildRoot, "bin", Configuration, "host", "tools", rid);
        StageCommon(stage);
        var destination = Path.Combine(stage, "tools", rid);
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(sourceRoot, $"RedBolt.{tool}*", SearchOption.TopDirectoryOnly))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        RequireFile(Path.Combine(destination, $"RedBolt.{tool}.dll"), "tool assembly");
        Directory.CreateDirectory(Path.Combine(stage, "lib", "net10.0"));
        File.WriteAllBytes(Path.Combine(stage, "lib", "net10.0", "_._"), []);

        var property = $"RedBolt{tool}Path";
        WriteXml(Path.Combine(stage, "buildTransitive", package + ".targets"),
            new XElement("Project",
                new XElement("PropertyGroup",
                    new XElement(property, $"$(TargetDir)RedBolt.{tool}.dll")),
                new XElement("Target",
                    new XAttribute("Name", $"RedBoltStage{tool}"),
                    new XAttribute("AfterTargets", "Build"),
                    new XElement("ItemGroup",
                        new XElement($"_RedBolt{tool}Files",
                            new XAttribute("Include", $"$(MSBuildThisFileDirectory)../tools/{rid}/*"))),
                    new XElement("Copy",
                        new XAttribute("SourceFiles", $"@(_RedBolt{tool}Files)"),
                        new XAttribute("DestinationFolder", "$(TargetDir)"),
                        new XAttribute("SkipUnchangedFiles", "true")))));

        var project = tool == "Editor"
            ? Path.Combine(_repositoryRoot, "src", "RedBolt", "Editor", "RedBolt.Editor.csproj")
            : Path.Combine(_repositoryRoot, "src", "RedBolt", "Tools", "PackageTool", "RedBolt.PackageTool.csproj");
        var dependencies = ToolDependencies(project, rid).ToList();
        if (tool == "Editor")
        {
            dependencies.Add(new Dependency("RedBolt.CoreData", _packageVersion));
            dependencies.Add(new Dependency("RedBolt.EditorData", _packageVersion));
        }
        MakePackage(package, $"{tool} for RedBolt on {rid}.", stage, dependencies);
    }

    private IEnumerable<Dependency> ToolDependencies(string project, string rid)
    {
        var metadata = Metadata(project, rid);
        foreach (var reference in metadata.ProjectReferences.Where(reference => reference.ReferenceOutputAssembly))
        {
            if (reference.Name.StartsWith("RedBolt.", StringComparison.Ordinal))
            {
                var module = reference.Name["RedBolt.".Length..];
                if (_modules.Contains(module, StringComparer.Ordinal) && SupportedOnRid(module, rid))
                    foreach (var dependency in ModulePackages(module, rid))
                        yield return dependency;
            }
            else if (reference.Name.StartsWith("SharpCpp.", StringComparison.Ordinal))
                yield return new Dependency(reference.Name, _packageVersion);
        }
        foreach (var reference in metadata.PackageReferences.Where(reference => !reference.PrivateAssets.Equals("all", StringComparison.OrdinalIgnoreCase)))
            yield return new Dependency(reference.Id, reference.Version);
    }

    private void ValidatePackages()
    {
        if (!Directory.Exists(_outputRoot))
            throw new InvalidOperationException($"No packages found in {_outputRoot}");
        var packages = Directory.GetFiles(_outputRoot, "*.nupkg", SearchOption.TopDirectoryOnly);
        if (packages.Length == 0)
            throw new InvalidOperationException($"No packages found in {_outputRoot}");
        foreach (var package in packages)
        {
            using var archive = ZipFile.OpenRead(package);
            foreach (var entry in archive.Entries.Where(entry => entry.Name.Length != 0))
            {
                using var stream = entry.Open();
                stream.CopyTo(Stream.Null);
            }
        }
        if (packages.Any(package => Path.GetFileName(package).Contains("Sample", StringComparison.OrdinalIgnoreCase)
                && !Path.GetFileName(package).StartsWith("RedBolt.SampleData.", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Sample packages must not be created.");
        foreach (var expected in new[] { "RedBolt.CoreData", "RedBolt.SampleData", "RedBolt.EditorData", "SharpCpp.Support", "RedBolt.Core" })
        {
            if (!packages.Any(package => Path.GetFileName(package).StartsWith(expected + ".", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Expected package was not created: {expected}");
        }
        var corePackage = packages.Single(package =>
            Path.GetFileName(package).StartsWith("RedBolt.Core.", StringComparison.OrdinalIgnoreCase)
            && !Path.GetFileName(package).StartsWith("RedBolt.Core.Runtime.", StringComparison.OrdinalIgnoreCase));
        using (var archive = ZipFile.OpenRead(corePackage))
        {
            if (!archive.Entries.Any(entry => entry.FullName.Equals(
                    "IconFontCppHeaders.LICENSE.txt", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("RedBolt.Core does not contain the IconFontCppHeaders license.");
        }
        foreach (var validationRid in _rids)
        foreach (var module in _modules.Where(module => SupportedOnRid(module, validationRid)))
        {
            var runtimePackage = $"RedBolt.{module}.Runtime.{validationRid}";
            if (HasPackage(packages, runtimePackage) != HasRuntimePackage(module, validationRid))
                throw new InvalidOperationException($"Unexpected runtime package state: {runtimePackage}");
        }

        var rid = _rids[0];
        var consumer = Path.Combine(_packageRoot, "consumer");
        var consumerPackages = Path.Combine(_packageRoot, "consumer-packages");
        RecreateDirectory(consumer);
        RecreateDirectory(consumerPackages);
        WriteNuGetConfig(Path.Combine(consumer, "NuGet.Config"), consumerPackages);

        var references = new List<Dependency>
        {
            new($"RedBolt.Core.Runtime.{rid}", _packageVersion),
            new("SharpCpp.Model", _packageVersion),
            new("SharpCpp.Pipeline", _packageVersion),
            new("SharpCpp.Rendering", _packageVersion),
            new($"SharpCpp.Parsing.Runtime.{HostSharpCppRid()}", _packageVersion),
            new("SharpCpp.Support", _packageVersion)
        };
        var editorPackage = $"RedBolt.Editor.{rid}";
        var packageToolPackage = $"RedBolt.PackageTool.{rid}";
        var hasEditor = HasPackage(packages, editorPackage);
        var hasPackageTool = HasPackage(packages, packageToolPackage);
        if (hasEditor) references.Add(new Dependency(editorPackage, _packageVersion));
        if (hasPackageTool) references.Add(new Dependency(packageToolPackage, _packageVersion));
        WriteXml(Path.Combine(consumer, "Consumer.csproj"),
            new XElement("Project",
                new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup",
                    new XElement("TargetFramework", "net10.0"),
                    new XElement("RuntimeIdentifier", rid),
                    new XElement("OutputType", "Exe")),
                new XElement("ItemGroup", references.Select(PackageReferenceElement))));
        File.WriteAllText(Path.Combine(consumer, "Program.cs"), ConsumerProgram + Environment.NewLine);
        RunCommand("dotnet", ["build", Path.Combine(consumer, "Consumer.csproj"), "--configuration", Configuration, "--nologo"]);

        var consumerOutput = Path.Combine(consumer, "bin", Configuration, "net10.0", rid);
        if (hasEditor)
        {
            RequireFile(Path.Combine(consumerOutput, "RedBolt.Editor.dll"), "packaged Editor");
            RequireDirectory(Path.Combine(consumerOutput, "CoreData"), "CoreData output");
            RequireDirectory(Path.Combine(consumerOutput, "EditorData"), "EditorData output");
            if (Directory.Exists(Path.Combine(consumerOutput, "SampleData")))
                throw new InvalidOperationException("Editor must not depend on RedBolt.SampleData.");
        }
        if (hasPackageTool)
        {
            var input = Path.Combine(consumer, "package-input");
            Directory.CreateDirectory(input);
            File.WriteAllText(Path.Combine(input, "value.txt"), "package smoke" + Environment.NewLine);
            var output = Path.Combine(consumer, "package-smoke.pak");
            var environment = new Dictionary<string, string?>();
            if (OperatingSystem.IsLinux())
            {
                var current = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
                environment["LD_LIBRARY_PATH"] = current is { Length: > 0 }
                    ? consumerOutput + Path.PathSeparator + current
                    : consumerOutput;
            }
            RunCommand("dotnet",
                [Path.Combine(consumerOutput, "RedBolt.PackageTool.dll"), input, output, "-q", "-c"],
                environment: environment);
            if (!File.Exists(output) || new FileInfo(output).Length == 0)
                throw new InvalidOperationException("Packaged PackageTool did not create an archive.");
        }

        var normalizedVersion = _packageVersion.Split('+')[0];
        var includeRoot = Path.Combine(consumerPackages, "sharpcpp.support", normalizedVersion, "content", "include");
        RunCommand("dotnet",
        [
            "run", "--project", Path.Combine(consumer, "Consumer.csproj"), "--configuration", Configuration,
            "--no-build", "--", includeRoot
        ]);
        ValidateSamplePackages();
        Console.WriteLine($"Validated {packages.Length} packages in {_outputRoot}");
    }

    private static bool HasPackage(IEnumerable<string> packages, string id) =>
        packages.Any(package => Path.GetFileName(package).StartsWith(id + ".", StringComparison.OrdinalIgnoreCase));

    private void ValidateSamplePackages()
    {
        var rid = _rids[0];
        if (!IsDesktopRid(rid))
        {
            Console.WriteLine($"Skipping desktop sample package validation for {rid}.");
            return;
        }

        var sampleRoot = Path.Combine(_packageRoot, "sample-consumers");
        var samplePackages = Path.Combine(_packageRoot, "sample-consumer-packages");
        RecreateDirectory(sampleRoot);
        RecreateDirectory(samplePackages);
        WriteNuGetConfig(Path.Combine(sampleRoot, "NuGet.Config"), samplePackages);
        var projects = new List<string>();
        var sourceRoot = Path.Combine(_repositoryRoot, "src", "RedBolt", "Sample");
        foreach (var project in Directory.GetFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories).Order(PathComparer))
        {
            var name = Path.GetFileName(Path.GetDirectoryName(project))!;
            if (name is "Android" or "Web")
                continue;
            var metadata = Metadata(project, rid);
            var missing = MissingSampleModules(metadata, rid).Distinct().Order().ToArray();
            if (missing.Length != 0)
            {
                Console.WriteLine($"Skipping sample {name} because these modules are not packaged: {string.Join(' ', missing)}");
                continue;
            }

            var projectDirectory = Path.Combine(sampleRoot, name);
            Directory.CreateDirectory(projectDirectory);
            var packageReferences = SampleDependencies(metadata, rid).Distinct().OrderBy(dependency => dependency.Id, StringComparer.Ordinal);
            var compileFiles = metadata.CompileFiles
                .Where(path => Path.GetFileName(path) != "GlobalAliases.cs")
                .Order(PathComparer)
                .Select(path => new XElement("Compile", new XAttribute("Include", path)));
            var generatedProject = Path.Combine(projectDirectory, name + ".csproj");
            WriteXml(generatedProject,
                new XElement("Project",
                    new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    new XElement("PropertyGroup",
                        new XElement("TargetFramework", "net10.0"),
                        new XElement("RuntimeIdentifier", rid),
                        new XElement("OutputType", "Exe"),
                        new XElement("EnableDefaultCompileItems", "false"),
                        new XElement("ImplicitUsings", metadata.Property("ImplicitUsings", "disable")),
                        new XElement("Nullable", metadata.Property("Nullable", "enable")),
                        new XElement("AllowUnsafeBlocks", metadata.Property("AllowUnsafeBlocks", "true"))),
                    new XElement("ItemGroup", compileFiles),
                    new XElement("ItemGroup", packageReferences.Select(PackageReferenceElement))));
            projects.Add(generatedProject);
        }

        var traversal = Path.Combine(sampleRoot, "Samples.proj");
        WriteXml(traversal,
            new XElement("Project",
                new XElement("ItemGroup",
                    projects.Select(project => new XElement("SampleProject",
                        new XAttribute("Include", Path.GetRelativePath(sampleRoot, project))))),
                new XElement("Target",
                    new XAttribute("Name", "Restore"),
                    new XElement("MSBuild",
                        new XAttribute("Projects", "@(SampleProject)"),
                        new XAttribute("Targets", "Restore"),
                        new XAttribute("BuildInParallel", "true"))),
                new XElement("Target",
                    new XAttribute("Name", "Build"),
                    new XAttribute("DependsOnTargets", "Restore"),
                    new XElement("MSBuild",
                        new XAttribute("Projects", "@(SampleProject)"),
                        new XAttribute("Targets", "Build"),
                        new XAttribute("BuildInParallel", "true"),
                        new XAttribute("Properties", $"Configuration={Configuration}")))));
        RunCommand("dotnet", ["msbuild", traversal, "-target:Build", "-maxCpuCount", "--nologo"]);
        Console.WriteLine($"Validated {projects.Count} desktop samples as package-only consumers.");
    }

    private IEnumerable<Dependency> SampleDependencies(ProjectMetadata metadata, string rid)
    {
        foreach (var reference in metadata.ProjectReferences.Where(reference => reference.ReferenceOutputAssembly))
        {
            if (reference.Name.StartsWith("RedBolt.Bindings.", StringComparison.Ordinal)
                || reference.Name.StartsWith("RedBolt.Generator.", StringComparison.Ordinal)
                || reference.Name.StartsWith("RedBolt.Native.", StringComparison.Ordinal))
                continue;
            if (reference.Name.StartsWith("RedBolt.", StringComparison.Ordinal))
            {
                var module = reference.Name["RedBolt.".Length..];
                if (_modules.Contains(module, StringComparer.Ordinal) && SupportedOnRid(module, rid))
                    foreach (var dependency in ModulePackages(module, rid))
                        yield return dependency;
            }
            else if (reference.Name.StartsWith("SharpCpp.", StringComparison.Ordinal))
                yield return new Dependency(reference.Name, _packageVersion);
        }
        foreach (var reference in metadata.PackageReferences.Where(reference => !reference.PrivateAssets.Equals("all", StringComparison.OrdinalIgnoreCase)))
            yield return new Dependency(reference.Id, reference.Version);
        yield return new Dependency("RedBolt.CoreData", _packageVersion);
        yield return new Dependency("RedBolt.SampleData", _packageVersion);
    }

    private IEnumerable<string> MissingSampleModules(ProjectMetadata metadata, string rid)
    {
        foreach (var reference in metadata.ProjectReferences.Where(reference => reference.ReferenceOutputAssembly))
        {
            if (!reference.Name.StartsWith("RedBolt.", StringComparison.Ordinal)
                || reference.Name.StartsWith("RedBolt.Bindings.", StringComparison.Ordinal)
                || reference.Name.StartsWith("RedBolt.Generator.", StringComparison.Ordinal)
                || reference.Name.StartsWith("RedBolt.Native.", StringComparison.Ordinal))
                continue;
            var module = reference.Name["RedBolt.".Length..];
            if (!_modules.Contains(module, StringComparer.Ordinal) || !SupportedOnRid(module, rid))
                yield return module;
        }
    }

    private void WriteNuGetConfig(string path, string packageFolder)
    {
        WriteXml(path,
            new XElement("configuration",
                new XElement("config",
                    new XElement("add",
                        new XAttribute("key", "globalPackagesFolder"),
                        new XAttribute("value", packageFolder))),
                new XElement("packageSources",
                    new XElement("clear"),
                    new XElement("add", new XAttribute("key", "local"), new XAttribute("value", _outputRoot)),
                    new XElement("add", new XAttribute("key", "nuget.org"), new XAttribute("value", "https://api.nuget.org/v3/index.json")))));
    }

    private static XElement PackageReferenceElement(Dependency dependency) =>
        new("PackageReference", new XAttribute("Include", dependency.Id), new XAttribute("Version", dependency.Version));

    private static string HostSharpCppRid()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new InvalidOperationException("SharpCpp parsing validation currently requires an x64 host.");
        if (OperatingSystem.IsLinux()) return "linux-x64";
        if (OperatingSystem.IsWindows()) return "win-x64";
        throw new InvalidOperationException("SharpCpp parsing validation is supported on Linux and Windows hosts.");
    }

    private void RecreateDirectory(string path)
    {
        path = Path.GetFullPath(path);
        var root = Path.GetFullPath(_packageRoot) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, PathComparison) && !path.Equals(Path.GetFullPath(_packageRoot), PathComparison))
            throw new InvalidOperationException($"Refusing to replace directory outside the package build root: {path}");
        if (Directory.Exists(path))
            Directory.Delete(path, true);
        Directory.CreateDirectory(path);
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static void CopyDirectory(string source, string destination)
    {
        RequireDirectory(source, "source directory");
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static void WriteXml(string path, XElement root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        new XDocument(root).Save(path);
    }

    private static void RequireFile(string path, string description)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Missing {description}: {path}");
    }

    private static void RequireDirectory(string path, string description)
    {
        if (!Directory.Exists(path))
            throw new InvalidOperationException($"Missing {description}: {path}");
    }

    private CommandResult Capture(string executable, IEnumerable<string> arguments, bool allowFailure = false)
    {
        using var process = new Process
        {
            StartInfo = StartInfo(executable, arguments, redirect: true)
        };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(output, error);
        if (process.ExitCode != 0 && !allowFailure)
            throw new InvalidOperationException(
                $"Command failed ({process.ExitCode}): {executable} {string.Join(' ', arguments)}{Environment.NewLine}{error.Result}");
        return new CommandResult(process.ExitCode, output.Result, error.Result);
    }

    private void RunCommand(string executable, IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        using var process = new Process { StartInfo = StartInfo(executable, arguments, redirect: false) };
        if (environment is not null)
        {
            foreach (var pair in environment)
                process.StartInfo.Environment[pair.Key] = pair.Value;
        }
        process.Start();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Command failed ({process.ExitCode}): {executable} {string.Join(' ', arguments)}");
    }

    private ProcessStartInfo StartInfo(string executable, IEnumerable<string> arguments, bool redirect)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = _repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    private sealed record ProjectReference(string Name, bool ReferenceOutputAssembly);
    private sealed record PackageReference(string Id, string Version, string PrivateAssets);
    private sealed record Dependency(string Id, string Version);
    private sealed record CommandResult(int ExitCode, string Output, string Error);

    private sealed record ProjectMetadata(
        ProjectReference[] ProjectReferences,
        PackageReference[] PackageReferences,
        string[] CompileFiles,
        Dictionary<string, string> Properties)
    {
        public string Property(string name, string defaultValue) =>
            Properties.TryGetValue(name, out var value) && value.Length != 0 ? value : defaultValue;
    }
}
