// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RedBolt.ExternalModule.Packaging;

public static class PackageVersions
{
    public const string Module = "module";
    public const string RedBolt = "redbolt";
}

public enum BuildWhen
{
    Always,
    Linux,
    Windows,
    Android,
    Web
}

public sealed record PackageFile(string Source, string Destination, string? RequiredRid = null);

public sealed record PackageDependency(string Id, string VersionSource);

public sealed record ManagedPackage(
    string Id,
    string Description,
    IReadOnlyList<PackageFile> Files,
    IReadOnlyList<PackageDependency> Dependencies);

public sealed record ResourcePackage(string Id, string Name, string Source);

public sealed record RuntimePackage(
    string PackagePrefix,
    string DisplayName,
    string NativeGlob,
    string WebArchive,
    IReadOnlyList<PackageDependency> Dependencies);

public sealed record BuildStep(
    string Project,
    BuildWhen When = BuildWhen.Always,
    bool NoRestore = false,
    params string[] Arguments);

public sealed record ExternalPackageFeed(
    string Key,
    string Repository,
    string Option,
    string VersionFile,
    IReadOnlyList<string> RequiredPackages,
    IReadOnlyList<string> PackagePrefixes);

public sealed record Consumer(
    string PackageId,
    string Source,
    bool OnlyWithLinuxRuntime = false);

public sealed record ExternalModulePackageManifest
{
    public required string Name { get; init; }
    public IReadOnlyList<string> BasePackages { get; init; } = ["RedBolt.Core"];
    public bool RequireBaseRuntime { get; init; }
    public IReadOnlyList<string> SupportedRids { get; init; } = [];
    public IReadOnlyList<string> DefaultRids { get; init; } = [];
    public IReadOnlyList<ExternalPackageFeed> ExternalFeeds { get; init; } = [];
    public IReadOnlyList<BuildStep> BuildSteps { get; init; } = [];
    public IReadOnlyList<ResourcePackage> ResourcePackages { get; init; } = [];
    public IReadOnlyList<ManagedPackage> ManagedPackages { get; init; } = [];
    public RuntimePackage? RuntimePackage { get; init; }
    public required Consumer Consumer { get; init; }
}

public static class ExternalModulePackager
{
    public static int Run(string[] arguments, ExternalModulePackageManifest manifest, string sourcePath)
    {
        try
        {
            return new Runner(arguments, manifest, sourcePath).Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private sealed class Runner
    {
        private const string Configuration = "Release";
        private readonly string[] _arguments;
        private readonly ExternalModulePackageManifest _manifest;
        private readonly string _scriptDirectory;
        private readonly string _repositoryRoot;
        private readonly string _redBoltRoot;
        private readonly string _buildRoot;
        private readonly string _packageProject;
        private readonly string _packageRoot;
        private readonly string _stageRoot;
        private readonly string _outputRoot;
        private readonly string _baseOutputRoot;
        private readonly List<string> _rids = [];
        private readonly Dictionary<string, string> _versions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _feedRoots = new(StringComparer.Ordinal);
        private string _packageVersion = "";
        private string _repositoryCommit = "";
        private bool _skipBuild;

        public Runner(string[] arguments, ExternalModulePackageManifest manifest, string sourcePath)
        {
            _arguments = arguments;
            _manifest = manifest;
            _scriptDirectory = Path.GetDirectoryName(sourcePath)!;
            _repositoryRoot = Path.GetFullPath(Path.Combine(_scriptDirectory, "..", ".."));
            _redBoltRoot = Path.GetFullPath(Path.Combine(_repositoryRoot, "..", "RedBolt"));
            _packageProject = Path.Combine(_scriptDirectory, "RedBolt.Package.csproj");
            _buildRoot = ResolveBuildRoot(_repositoryRoot);
            _packageRoot = ResolvePackageRoot(_repositoryRoot);
            _stageRoot = Path.Combine(_packageRoot, "stage");
            _outputRoot = Path.Combine(_packageRoot, "out");
            _baseOutputRoot = Path.Combine(ResolvePackageRoot(_redBoltRoot), "out");
        }

        public int Run()
        {
            if (_arguments.Length == 0 || _arguments[0] is not ("create" or "validate" or "version"))
                return Usage();

            var command = _arguments[0];
            ParseArguments();
            _packageVersion = GitVersion();
            _versions[PackageVersions.Module] = NormalizeDependencyVersion(_packageVersion);
            if (command == "version")
            {
                Console.WriteLine(_packageVersion);
                return 0;
            }

            _repositoryCommit = GitOrEmpty("rev-parse", "HEAD").Trim();
            ResolveDependencyVersions();
            VerifyDependencyPackages();

            if (command == "validate")
            {
                ValidatePackages();
                return 0;
            }

            if (!_skipBuild)
                BuildSelectedOutputs();

            RecreateDirectory(_stageRoot);
            RecreateDirectory(_outputRoot);
            RecreateDirectory(Path.Combine(_packageRoot, "restore-packages"));
            Directory.CreateDirectory(_packageRoot);
            File.WriteAllText(Path.Combine(_packageRoot, "version.txt"), _packageVersion + Environment.NewLine);
            File.WriteAllText(Path.Combine(_packageRoot, "redbolt-version.txt"), Version(PackageVersions.RedBolt) + Environment.NewLine);
            foreach (var feed in _manifest.ExternalFeeds)
                File.WriteAllText(Path.Combine(_packageRoot, feed.VersionFile), Version(feed.Key) + Environment.NewLine);

            foreach (var package in _manifest.ResourcePackages)
                StageResourcePackage(package);
            foreach (var package in _manifest.ManagedPackages)
                StageManagedPackage(package);
            if (_manifest.RuntimePackage is not null)
            {
                foreach (var rid in _rids)
                    StageRuntimePackage(_manifest.RuntimePackage, rid);
            }

            ValidatePackages();
            return 0;
        }

        private void ParseArguments()
        {
            var externalOptions = _manifest.ExternalFeeds.ToDictionary(feed => feed.Option, StringComparer.Ordinal);
            for (var index = 1; index < _arguments.Length; ++index)
            {
                var argument = _arguments[index];
                if (argument == "--no-build")
                {
                    _skipBuild = true;
                }
                else if (argument == "--rids" && index + 1 < _arguments.Length && _manifest.SupportedRids.Count != 0)
                {
                    _rids.AddRange(_arguments[++index].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                }
                else if (argument == "--redbolt-version" && index + 1 < _arguments.Length)
                {
                    _versions[PackageVersions.RedBolt] = NormalizeDependencyVersion(_arguments[++index]);
                }
                else if (externalOptions.TryGetValue(argument, out var feed) && index + 1 < _arguments.Length)
                {
                    _versions[feed.Key] = NormalizeDependencyVersion(_arguments[++index]);
                }
                else
                {
                    throw new InvalidOperationException("Unknown or incomplete package option: " + argument);
                }
            }

            if (_manifest.SupportedRids.Count == 0)
                return;
            if (_rids.Count == 0)
                _rids.AddRange(_manifest.DefaultRids);
            foreach (var rid in _rids)
            {
                if (!_manifest.SupportedRids.Contains(rid, StringComparer.Ordinal))
                    throw new InvalidOperationException($"Unsupported {_manifest.Name} runtime identifier '{rid}'.");
            }
        }

        private void ResolveDependencyVersions()
        {
            if (!_versions.ContainsKey(PackageVersions.RedBolt))
            {
                _versions[PackageVersions.RedBolt] = ResolveRepositoryVersion(
                    _redBoltRoot,
                    Path.Combine(_redBoltRoot, "packaging", "nuget", "package.cs"));
            }

            foreach (var feed in _manifest.ExternalFeeds)
            {
                var root = Path.GetFullPath(Path.Combine(_repositoryRoot, "..", feed.Repository));
                _feedRoots[feed.Key] = Path.Combine(ResolvePackageRoot(root), "out");
                if (!_versions.ContainsKey(feed.Key))
                {
                    _versions[feed.Key] = ResolveRepositoryVersion(
                        root,
                        Path.Combine(root, "packaging", "nuget", "package.cs"));
                }
            }
        }

        private string ResolveRepositoryVersion(string root, string script)
        {
            var versionFile = Path.Combine(ResolvePackageRoot(root), "version.txt");
            var version = File.Exists(versionFile)
                ? File.ReadAllText(versionFile).Trim()
                : RunCapture("dotnet", ["run", "--file", script, "--", "version"]).Trim();
            return NormalizeDependencyVersion(version);
        }

        private void VerifyDependencyPackages()
        {
            if (!Directory.Exists(_baseOutputRoot))
                throw new InvalidOperationException($"Base RedBolt package feed is missing: {_baseOutputRoot}");
            foreach (var package in _manifest.BasePackages)
                RequirePackage(_baseOutputRoot, package, Version(PackageVersions.RedBolt));
            if (_manifest.RequireBaseRuntime)
            {
                foreach (var rid in _rids)
                    RequirePackage(_baseOutputRoot, $"RedBolt.Core.Runtime.{rid}", Version(PackageVersions.RedBolt));
            }

            foreach (var feed in _manifest.ExternalFeeds)
            {
                foreach (var package in feed.RequiredPackages)
                    RequirePackage(_feedRoots[feed.Key], package, Version(feed.Key));
            }
        }

        private void BuildSelectedOutputs()
        {
            var baseCore = Path.Combine(ResolveBuildRoot(_redBoltRoot), "bin", Configuration, "linux-x64", "core", "RedBolt.Core.dll");
            if (!File.Exists(baseCore))
                throw new InvalidOperationException($"Build the matching base RedBolt Release outputs first: {baseCore}");

            var jobs = AndroidEmulatorIsRunning() ? 1 : 4;
            foreach (var step in _manifest.BuildSteps.Where(ShouldRun))
            {
                var arguments = new List<string>
                {
                    "build", Project(step.Project), "--configuration", Configuration
                };
                if (step.NoRestore)
                    arguments.Add("--no-restore");
                arguments.Add($"-m:{jobs}");
                arguments.Add(BuildRootProperty);
                arguments.AddRange(step.Arguments);
                RunCommand("dotnet", arguments);
            }
        }

        private bool ShouldRun(BuildStep step) => step.When switch
        {
            BuildWhen.Always => true,
            BuildWhen.Linux => _rids.Contains("linux-x64"),
            BuildWhen.Windows => _rids.Contains("win-x64"),
            BuildWhen.Android => _rids.Any(rid => rid.StartsWith("android-", StringComparison.Ordinal)),
            BuildWhen.Web => _rids.Contains("browser-wasm"),
            _ => false
        };

        private void StageManagedPackage(ManagedPackage package)
        {
            var stage = PrepareStage(package.Id);
            foreach (var file in package.Files.Where(file =>
                         file.RequiredRid is null || _rids.Contains(file.RequiredRid)))
                CopyRequired(RepositoryPath(file.Source), Path.Combine(stage, file.Destination));
            Pack(package.Id, package.Description, stage, package.Dependencies);
        }

        private void StageResourcePackage(ResourcePackage package)
        {
            var stage = PrepareStage(package.Id);
            CopyTree(RepositoryPath(package.Source), Path.Combine(stage, "content", package.Name));
            var targetsDirectory = Path.Combine(stage, "buildTransitive");
            Directory.CreateDirectory(targetsDirectory);
            File.WriteAllText(Path.Combine(targetsDirectory, package.Id + ".targets"), $"""
                <Project>
                  <ItemGroup>
                    <None Include="$(MSBuildThisFileDirectory)../content/{package.Name}/**/*">
                      <Link>{package.Name}/%(RecursiveDir)%(Filename)%(Extension)</Link>
                      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
                      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
                      <TargetPath>{package.Name}/%(RecursiveDir)%(Filename)%(Extension)</TargetPath>
                    </None>
                  </ItemGroup>
                </Project>
                """);
            Pack(package.Id, $"{package.Name} resources for {_manifest.Name}.", stage, []);
        }

        private void StageRuntimePackage(RuntimePackage package, string rid)
        {
            var packageId = $"{package.PackagePrefix}.{rid}";
            var stage = PrepareStage(packageId);
            if (rid == "browser-wasm")
            {
                if (!string.IsNullOrWhiteSpace(package.WebArchive))
                {
                    var archive = RepositoryPath(ExpandRid(package.WebArchive, rid));
                    CopyRequired(archive, Path.Combine(stage, "buildTransitive", "native", Path.GetFileName(archive)));
                    File.WriteAllText(Path.Combine(stage, "buildTransitive", packageId + ".targets"), $"""
                        <Project>
                          <ItemGroup>
                            <NativeFileReference Include="$(MSBuildThisFileDirectory)native/{Path.GetFileName(archive)}" />
                          </ItemGroup>
                        </Project>
                        """);
                }
            }
            else
            {
                var nativeRoot = RepositoryPath($"build/bin/{Configuration}/{rid}/extern/runtimes/{rid}/native");
                var nativeFiles = Directory.Exists(nativeRoot)
                    ? Directory.GetFiles(nativeRoot, package.NativeGlob, SearchOption.TopDirectoryOnly)
                    : [];
                if (nativeFiles.Length == 0)
                    throw new InvalidOperationException($"The {package.DisplayName} native runtime for {rid} is missing below {nativeRoot}.");
                foreach (var file in nativeFiles)
                    CopyRequired(file, Path.Combine(stage, "runtimes", rid, "native", Path.GetFileName(file)));
            }

            Pack(packageId, $"{package.PackagePrefix[..^8]} native runtime for {rid}.", stage,
                package.Dependencies.Select(dependency => ExpandRid(dependency, rid)).ToArray());
        }

        private string PrepareStage(string packageId)
        {
            var stage = Path.Combine(_stageRoot, packageId);
            RecreateDirectory(stage);
            File.Copy(Path.Combine(_scriptDirectory, "README.md"), Path.Combine(stage, "README.md"), true);
            return stage;
        }

        private void Pack(string packageId, string description, string stage, IReadOnlyList<PackageDependency> dependencies)
        {
            var spec = Path.Combine(stage, "package.props");
            var project = new XElement("Project");
            if (dependencies.Count != 0)
            {
                project.Add(new XElement("ItemGroup",
                    dependencies.Select(dependency => new XElement("PackageReference",
                        new XAttribute("Include", dependency.Id),
                        new XAttribute("Version", $"[{Version(dependency.VersionSource)}]")))));
            }
            new XDocument(project).Save(spec);

            var sources = new[] { _outputRoot }
                .Concat(_manifest.ExternalFeeds.Select(feed => _feedRoots[feed.Key]))
                .Append(_baseOutputRoot)
                .Append("https://api.nuget.org/v3/index.json");
            var properties = new[]
            {
                $"-p:RedBoltPackageId={packageId}",
                $"-p:RedBoltPackageVersion={_packageVersion}",
                $"-p:RedBoltPackageDescription={description}",
                $"-p:RedBoltRepositoryCommit={_repositoryCommit}",
                $"-p:RedBoltPackageStage={stage}",
                $"-p:RedBoltPackageSpec={spec}",
                BuildRootProperty,
                $"-p:RestorePackagesPath={Path.Combine(_packageRoot, "restore-packages")}",
                $"-p:RestoreSources={string.Join(';', sources)}"
            };
            RunCommand("dotnet", ["restore", _packageProject, .. properties]);
            RunCommand("dotnet",
            [
                "pack", _packageProject, "--configuration", Configuration, "--no-build", "--no-restore",
                "--output", _outputRoot,
                .. properties
            ]);
        }

        private void ValidatePackages()
        {
            var expected = _manifest.ResourcePackages.Select(package => package.Id)
                .Concat(_manifest.ManagedPackages.Select(package => package.Id))
                .Concat(_manifest.RuntimePackage is null
                    ? []
                    : _rids.Select(rid => $"{_manifest.RuntimePackage.PackagePrefix}.{rid}"))
                .ToArray();
            foreach (var id in expected)
                RequirePackage(_outputRoot, id, Version(PackageVersions.Module));

            foreach (var id in expected)
            {
                using var archive = ZipFile.OpenRead(RequirePackage(_outputRoot, id, Version(PackageVersions.Module)));
                var nuspec = ReadNuspec(archive);
                foreach (var dependency in nuspec.Descendants().Where(element => element.Name.LocalName == "dependency"))
                {
                    var dependencyId = dependency.Attribute("id")?.Value ?? "";
                    if (!dependencyId.StartsWith("RedBolt", StringComparison.Ordinal)
                        && !dependencyId.StartsWith("SharpCpp", StringComparison.Ordinal))
                        continue;
                    var expectedVersion = ExpectedDependencyVersion(dependencyId);
                    if (dependency.Attribute("version")?.Value != $"[{expectedVersion}]")
                        throw new InvalidOperationException($"{id} does not pin {dependencyId} to [{expectedVersion}].");
                }
            }

            if (!_manifest.Consumer.OnlyWithLinuxRuntime || _rids.Contains("linux-x64"))
                ValidateConsumer();
            var externalVersions = string.Join("", _manifest.ExternalFeeds.Select(feed => $" and {feed.Repository} {Version(feed.Key)}"));
            Console.WriteLine($"Validated {expected.Length} {_manifest.Name} packages at {_packageVersion} against RedBolt {Version(PackageVersions.RedBolt)}{externalVersions}.");
        }

        private string ExpectedDependencyVersion(string dependencyId)
        {
            if (dependencyId.StartsWith(_manifest.Name, StringComparison.Ordinal))
                return Version(PackageVersions.Module);
            foreach (var feed in _manifest.ExternalFeeds)
            {
                if (feed.PackagePrefixes.Any(prefix => dependencyId.StartsWith(prefix, StringComparison.Ordinal)))
                    return Version(feed.Key);
            }
            return Version(PackageVersions.RedBolt);
        }

        private void ValidateConsumer()
        {
            var root = Path.Combine(_packageRoot, "consumer");
            var packages = Path.Combine(_packageRoot, "consumer-packages");
            RecreateDirectory(root);
            RecreateDirectory(packages);
            File.WriteAllText(Path.Combine(root, "Consumer.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <RestorePackagesPath>../consumer-packages</RestorePackagesPath>
                    <TargetFramework>net10.0</TargetFramework>
                    <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="{_manifest.Consumer.PackageId}" Version="[{Version(PackageVersions.Module)}]" />
                    {(_manifest.Consumer.PackageId.EndsWith("Runtime.linux-x64", StringComparison.Ordinal) ? "" : $"<PackageReference Include=\"RedBolt.Core.Runtime.linux-x64\" Version=\"[{Version(PackageVersions.RedBolt)}]\" />")}
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(root, "Program.cs"), _manifest.Consumer.Source);
            var sources = new[] { _outputRoot }
                .Concat(_manifest.ExternalFeeds.Select(feed => _feedRoots[feed.Key]))
                .Append(_baseOutputRoot)
                .Append("https://api.nuget.org/v3/index.json");
            RunCommand("dotnet",
            [
                "build", Path.Combine(root, "Consumer.csproj"),
                "--configuration", Configuration,
                $"-m:{(AndroidEmulatorIsRunning() ? 1 : 4)}",
                $"-p:RestoreSources={string.Join(';', sources)}"
            ]);
        }

        private string RequirePackage(string feed, string id, string version)
        {
            if (!Directory.Exists(feed))
                throw new InvalidOperationException($"Package feed is missing: {feed}");
            foreach (var path in Directory.GetFiles(feed, "*.nupkg", SearchOption.TopDirectoryOnly))
            {
                using var archive = ZipFile.OpenRead(path);
                var nuspec = ReadNuspec(archive);
                var candidateId = nuspec.Descendants().First(element => element.Name.LocalName == "id").Value;
                var candidateVersion = NormalizeDependencyVersion(
                    nuspec.Descendants().First(element => element.Name.LocalName == "version").Value);
                if (candidateId == id && candidateVersion == version)
                    return path;
            }
            throw new InvalidOperationException($"Required package {id} [{version}] is missing from {feed}.");
        }

        private static XDocument ReadNuspec(ZipArchive archive)
        {
            var entry = archive.Entries.Single(candidate => candidate.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            using var stream = entry.Open();
            return XDocument.Load(stream);
        }

        private string Project(string path) => RepositoryPath(Path.Combine("src", "RedBolt", path));

        // Paths under "build/" (built outputs) follow the resolved build root; everything else is repository-relative.
        private string RepositoryPath(string path)
        {
            var normalized = path.Replace('\\', '/');
            while (normalized.StartsWith("./", StringComparison.Ordinal))
                normalized = normalized[2..];
            return normalized.StartsWith("build/", StringComparison.Ordinal)
                ? Path.GetFullPath(Path.Combine(_buildRoot, normalized["build/".Length..]))
                : Path.GetFullPath(Path.Combine(_repositoryRoot, path));
        }

        private string BuildRootProperty => $"-p:RedBoltBuildRoot={_buildRoot}";

        // Same default as the MSBuild props: an explicit REDBOLT_BUILD_ROOT, otherwise the build/ beside the
        // repository. RedBolt.Workspace.props is not read; set REDBOLT_BUILD_ROOT to the same directory.
        private static string ResolveBuildRoot(string repositoryRoot)
        {
            var explicitRoot = Environment.GetEnvironmentVariable("REDBOLT_BUILD_ROOT");
            if (!string.IsNullOrEmpty(explicitRoot))
                return Path.GetFullPath(explicitRoot);
            return Path.GetFullPath(Path.Combine(repositoryRoot, "..", "build"));
        }

        // Package outputs are namespaced per repository below the build root, because the monorepo shares one
        // build root: <build root>/package/nuget/<repository directory name>/{stage,out,version.txt,...}.
        private static string ResolvePackageRoot(string repositoryRoot) =>
            Path.Combine(ResolveBuildRoot(repositoryRoot), "package", "nuget",
                Path.GetFileName(Path.TrimEndingDirectorySeparator(repositoryRoot)));

        private string Version(string source) => _versions.TryGetValue(source, out var version)
            ? version
            : throw new InvalidOperationException($"Unknown package version source '{source}'.");

        private static PackageDependency ExpandRid(PackageDependency dependency, string rid) =>
            dependency with { Id = ExpandRid(dependency.Id, rid) };

        private static string ExpandRid(string value, string rid) => value.Replace("{rid}", rid, StringComparison.Ordinal);

        private static void CopyRequired(string source, string destination)
        {
            if (!File.Exists(source))
                throw new InvalidOperationException("Required package input is missing: " + source);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, true);
        }

        private static void CopyTree(string source, string destination)
        {
            if (!Directory.Exists(source))
                throw new InvalidOperationException("Required resource directory is missing: " + source);
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                CopyRequired(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
        }

        private static void RecreateDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            Directory.CreateDirectory(path);
        }

        private string GitVersion()
        {
            var commit = GitOrEmpty("rev-parse", "--short=8", "HEAD").Trim();
            if (commit.Length == 0)
                return "0.1.0-dev.0+working";
            var exactTag = GitOrEmpty("describe", "--tags", "--exact-match", "--match", "v[0-9]*").Trim();
            if (Regex.IsMatch(exactTag, "^v[0-9]+\\.[0-9]+\\.[0-9]+$"))
                return exactTag[1..];
            var heightText = GitOrEmpty("rev-list", "--count", "HEAD").Trim();
            var height = int.TryParse(heightText, out var parsedHeight) ? parsedHeight : 0;
            var dirty = GitOrEmpty("status", "--porcelain").Length == 0 ? "" : ".dirty";
            return $"0.1.0-dev.{height}{dirty}+{commit}";
        }

        private string GitOrEmpty(params string[] arguments)
        {
            try { return RunCapture("git", ["-C", _repositoryRoot, .. arguments]); }
            catch { return ""; }
        }

        private static string NormalizeDependencyVersion(string version)
        {
            var normalized = version.Trim();
            var metadata = normalized.IndexOf('+');
            return metadata < 0 ? normalized : normalized[..metadata];
        }

        private static bool AndroidEmulatorIsRunning()
        {
            try
            {
                return Process.GetProcesses().Any(process =>
                    process.ProcessName.Contains("emulator", StringComparison.OrdinalIgnoreCase)
                    || process.ProcessName.Contains("qemu-system", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return true;
            }
        }

        private static string RunCapture(string fileName, IReadOnlyList<string> arguments)
        {
            var startInfo = CreateStartInfo(fileName, arguments);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            using var process = Process.Start(startInfo)!;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"{fileName} failed: {error.Trim()}");
            return output;
        }

        private static void RunCommand(string fileName, IReadOnlyList<string> arguments)
        {
            using var process = Process.Start(CreateStartInfo(fileName, arguments))!;
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}.");
        }

        private static ProcessStartInfo CreateStartInfo(string fileName, IReadOnlyList<string> arguments)
        {
            var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = false };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);
            return startInfo;
        }

        private int Usage()
        {
            var options = new List<string> { "[--no-build]" };
            if (_manifest.SupportedRids.Count != 0)
                options.Add("[--rids RID,...]");
            options.Add("[--redbolt-version VERSION]");
            options.AddRange(_manifest.ExternalFeeds.Select(feed => $"[{feed.Option} VERSION]"));
            Console.Error.WriteLine($"Usage: dotnet run --file packaging/nuget/package.cs -- <create|validate|version> {string.Join(' ', options)}");
            return 2;
        }
    }
}
