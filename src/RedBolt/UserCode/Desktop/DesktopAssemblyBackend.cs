// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.UserCode.Desktop;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

/// <summary>A desktop assembly bundle and the host modules whose type identity it must share.</summary>
public sealed record DesktopAssemblyInput : AssemblyLoadInput
{
    public DesktopAssemblyInput(
        string assemblyPath,
        IEnumerable<Assembly>? sharedAssemblies = null,
        IEnumerable<string>? sharedNativeLibraries = null,
        IEnumerable<string>? dependencyPaths = null,
        IEnumerable<string>? nativeDependencyPaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        AssemblyPath = Path.GetFullPath(assemblyPath);
        SharedAssemblies = sharedAssemblies?.ToArray() ?? [];
        SharedNativeLibraries = sharedNativeLibraries?.ToArray() ?? [];
        DependencyPaths = dependencyPaths?.Select(Path.GetFullPath).ToArray() ?? [];
        NativeDependencyPaths = nativeDependencyPaths?.Select(Path.GetFullPath).ToArray() ?? [];
    }

    public string AssemblyPath { get; }

    public IReadOnlyList<Assembly> SharedAssemblies { get; }

    /// <summary>
    /// Native wrapper libraries owned by the host for process lifetime. Plugins must only dispose the native
    /// resources they create through these libraries and must never unload or privately duplicate the libraries.
    /// </summary>
    public IReadOnlyList<string> SharedNativeLibraries { get; }

    /// <summary>Private managed dependencies used when the bundle has no component dependency manifest.</summary>
    public IReadOnlyList<string> DependencyPaths { get; }

    /// <summary>Private native dependencies used when the bundle has no component dependency manifest.</summary>
    public IReadOnlyList<string> NativeDependencyPaths { get; }
}

internal sealed record DesktopAssemblyImageInput(
    string AssemblyName,
    byte[] PeImage,
    byte[]? PdbImage,
    IReadOnlyList<Assembly> SharedAssemblies) : AssemblyLoadInput;

public abstract class DesktopAssemblyBackend : IAssemblyBackend
{
    private readonly bool _collectible;
    private readonly bool _allowReplacement;

    protected DesktopAssemblyBackend(bool collectible, bool allowReplacement)
    {
        EnsureSupported();
        _collectible = collectible;
        _allowReplacement = allowReplacement;
    }

    public UserCodeAssembly? Current { get; private set; }

    public AssemblyPreparation Prepare(AssemblyLoadInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_allowReplacement && Current is not null)
        {
            return AssemblyPreparation.Failure(this, new UserCodeDiagnostic(
                UserCodeDiagnosticSeverity.Error, "A desktop load-only backend already has an active assembly."));
        }

        try
        {
            var candidate = input switch
            {
                DesktopAssemblyInput file => LoadFile(file),
                DesktopAssemblyImageInput image => LoadImage(image),
                _ => null,
            };
            return candidate is null
                ? AssemblyPreparation.Failure(this, new UserCodeDiagnostic(
                    UserCodeDiagnosticSeverity.Error, "The desktop backend requires a desktop assembly input."))
                : AssemblyPreparation.Success(this, candidate);
        }
        catch (Exception exception) when (exception is IOException or BadImageFormatException
            or FileLoadException or FileNotFoundException or ArgumentException)
        {
            return AssemblyPreparation.Failure(this, new UserCodeDiagnostic(
                UserCodeDiagnosticSeverity.Error, exception.Message));
        }
    }

    public UserCodeAssembly Commit(AssemblyPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (!_allowReplacement && Current is not null)
            throw new InvalidOperationException("A desktop load-only backend cannot replace its active assembly.");

        var candidate = preparation.TakeCandidate(this);
        var previous = Current;
        Current = candidate;
        previous?.Dispose();
        return candidate;
    }

    public void Unload()
    {
        Current?.Dispose();
        Current = null;
    }

    public void Dispose()
    {
        Unload();
        GC.SuppressFinalize(this);
    }

    private DesktopUserCodeAssembly LoadFile(DesktopAssemblyInput input)
    {
        if (!File.Exists(input.AssemblyPath))
            throw new FileNotFoundException("User-code assembly was not found.", input.AssemblyPath);

        var shared = CreateSharedAssemblyMap(input.SharedAssemblies);
        var context = new UserCodeLoadContext(
            Path.GetFileNameWithoutExtension(input.AssemblyPath), _collectible,
            new AssemblyDependencyResolver(input.AssemblyPath), shared, input.SharedNativeLibraries,
            input.DependencyPaths, input.NativeDependencyPaths);
        try
        {
            using var assemblyStream = OpenRead(input.AssemblyPath);
            var pdbPath = Path.ChangeExtension(input.AssemblyPath, ".pdb");
            Assembly assembly;
            if (File.Exists(pdbPath))
            {
                using var pdbStream = OpenRead(pdbPath);
                assembly = context.LoadFromStream(assemblyStream, pdbStream);
            }
            else
                assembly = context.LoadFromStream(assemblyStream);
            var dependencyClosure = context.LoadDependencyClosure(assembly);
            return new DesktopUserCodeAssembly(assembly, context, _collectible, dependencyClosure);
        }
        catch
        {
            if (_collectible)
                context.Unload();
            throw;
        }
    }

    private DesktopUserCodeAssembly LoadImage(DesktopAssemblyImageInput input)
    {
        var context = new UserCodeLoadContext(
            input.AssemblyName, _collectible, null, CreateSharedAssemblyMap(input.SharedAssemblies), []);
        try
        {
            using var peStream = new MemoryStream(input.PeImage, false);
            Assembly assembly;
            if (input.PdbImage is not null)
            {
                using var pdbStream = new MemoryStream(input.PdbImage, false);
                assembly = context.LoadFromStream(peStream, pdbStream);
            }
            else
                assembly = context.LoadFromStream(peStream);
            var dependencyClosure = context.LoadDependencyClosure(assembly);
            return new DesktopUserCodeAssembly(assembly, context, _collectible, dependencyClosure);
        }
        catch
        {
            if (_collectible)
                context.Unload();
            throw;
        }
    }

    // Shared assemblies are matched by simple name. The host's own copy (default context) always wins, so a
    // private copy in another plugin's context cannot replace it; otherwise the first one seen wins.
    internal static Dictionary<string, Assembly> CreateSharedAssemblyMap(IEnumerable<Assembly> assemblies)
    {
        var result = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        AddShared(typeof(Core.Context).Assembly);
        foreach (var assembly in assemblies)
            AddShared(assembly);
        return result;

        void AddShared(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            if (string.IsNullOrEmpty(name))
                return;
            if (!result.TryGetValue(name, out var existing)
                || (!IsDefaultContext(existing) && IsDefaultContext(assembly)))
                result[name] = assembly;
        }
    }

    private static bool IsDefaultContext(Assembly assembly) =>
        AssemblyLoadContext.GetLoadContext(assembly) == AssemblyLoadContext.Default;

    private static FileStream OpenRead(string path) => new(
        path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static void EnsureSupported()
    {
#if !REDBOLT_PLATFORM_WINDOWS && !REDBOLT_PLATFORM_LINUX
        throw new PlatformNotSupportedException("Desktop user-code loading requires a desktop build.");
#else
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            throw new PlatformNotSupportedException(
                "Desktop user-code loading requires dynamic code support.");
        }
#endif
    }

    private sealed class DesktopUserCodeAssembly : UserCodeAssembly
    {
        private UserCodeLoadContext? _context;
        private readonly WeakReference _collectionReference;

        public DesktopUserCodeAssembly(
            Assembly assembly,
            UserCodeLoadContext context,
            bool collectible,
            IReadOnlyList<Assembly> dependencyClosure)
            : base(assembly, collectible, dependencyClosure)
        {
            _context = context;
            _collectionReference = new WeakReference(context);
        }

        public override WeakReference CollectionReference => _collectionReference;

        protected override void DisposeCore()
        {
            if (_context is not { } context)
                return;
            _context = null;
            if (context.IsCollectible)
                context.Unload();
        }
    }

    private sealed class UserCodeLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver? _resolver;
        private readonly IReadOnlyDictionary<string, Assembly> _sharedAssemblies;
        private readonly HashSet<string> _sharedNativeLibraries;
        private readonly IReadOnlyDictionary<string, string> _dependencyPaths;
        private readonly IReadOnlyDictionary<string, string> _nativeDependencyPaths;

        public UserCodeLoadContext(
            string name,
            bool collectible,
            AssemblyDependencyResolver? resolver,
            IReadOnlyDictionary<string, Assembly> sharedAssemblies,
            IEnumerable<string> sharedNativeLibraries,
            IEnumerable<string>? dependencyPaths = null,
            IEnumerable<string>? nativeDependencyPaths = null)
            : base($"RedBolt.UserCode:{name}:{Guid.NewGuid():N}", collectible)
        {
            _resolver = resolver;
            _sharedAssemblies = sharedAssemblies;
            _sharedNativeLibraries = new HashSet<string>(sharedNativeLibraries, StringComparer.OrdinalIgnoreCase);
            _dependencyPaths = (dependencyPaths ?? []).ToDictionary(
                static path => AssemblyName.GetAssemblyName(path).Name!, Path.GetFullPath,
                StringComparer.OrdinalIgnoreCase);
            _nativeDependencyPaths = CreateNativeDependencyMap(nativeDependencyPaths ?? []);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is { } name && _sharedAssemblies.TryGetValue(name, out var shared))
            {
                var sharedName = shared.GetName();
                if (assemblyName.Version is not null && sharedName.Version is not null
                    && sharedName.Version < assemblyName.Version)
                {
                    throw new FileLoadException(
                        $"Shared assembly '{sharedName}' does not satisfy requested identity '{assemblyName}'.");
                }
                return shared;
            }

            var path = _resolver?.ResolveAssemblyToPath(assemblyName);
            if (path is null && assemblyName.Name is { } dependencyName)
                _dependencyPaths.TryGetValue(dependencyName, out path);
            if (path is null)
                return null;
            using var stream = OpenRead(path);
            var pdbPath = Path.ChangeExtension(path, ".pdb");
            if (!File.Exists(pdbPath))
                return LoadFromStream(stream);
            using var pdbStream = OpenRead(pdbPath);
            return LoadFromStream(stream, pdbStream);
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            if (_sharedNativeLibraries.Contains(unmanagedDllName))
                return 0;
            var path = _resolver?.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (path is null)
                _nativeDependencyPaths.TryGetValue(unmanagedDllName, out path);
            return path is null ? 0 : LoadUnmanagedDllFromPath(path);
        }

        public IReadOnlyList<Assembly> LoadDependencyClosure(Assembly root)
        {
            ArgumentNullException.ThrowIfNull(root);
            var result = new List<Assembly>();
            var visited = new HashSet<Assembly>();

            Visit(root);
            return result;

            void Visit(Assembly assembly)
            {
                if (!visited.Add(assembly))
                    return;

                foreach (var reference in assembly.GetReferencedAssemblies())
                {
                    Assembly dependency;
                    try
                    {
                        dependency = LoadFromAssemblyName(reference);
                    }
                    catch (Exception exception) when (exception is FileNotFoundException
                        or FileLoadException or BadImageFormatException)
                    {
                        // Preserve lazy-loading behavior for optional references. If code eventually uses one,
                        // normal assembly resolution will report the original error at that point.
                        continue;
                    }

                    if (GetLoadContext(dependency) == this)
                        Visit(dependency);
                    else if (visited.Add(dependency))
                        result.Add(dependency);
                }
                result.Add(assembly);
            }
        }

        private static IReadOnlyDictionary<string, string> CreateNativeDependencyMap(IEnumerable<string> paths)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                var fullPath = Path.GetFullPath(path);
                result.Add(Path.GetFileName(path), fullPath);
                result.TryAdd(Path.GetFileNameWithoutExtension(path), fullPath);
            }
            return result;
        }
    }
}

public sealed class DesktopLoadOnlyAssemblyBackend : DesktopAssemblyBackend
{
    public DesktopLoadOnlyAssemblyBackend() : base(false, false)
    {
    }
}

public sealed class ReloadableAssemblyBackend : DesktopAssemblyBackend
{
    public ReloadableAssemblyBackend() : base(true, true)
    {
    }
}
