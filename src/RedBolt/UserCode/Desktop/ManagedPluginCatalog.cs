namespace RedBolt.UserCode.Desktop;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

using Microsoft.Extensions.Logging;

using RedBolt.Logging;
using RedBolt.Plugins;

/// <summary>Discovers metadata-marked managed plugins without loading candidate assemblies.</summary>
public sealed class ManagedPluginCatalog
{
    public ManagedPluginCatalog(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths.Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).ToArray();
    }

    public static ManagedPluginCatalog FromParameters(Engine.EngineParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var paths = new List<string> { AppContext.BaseDirectory };
        paths.AddRange(parameters.PluginPaths.Split(
            [';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return new ManagedPluginCatalog(paths);
    }

    public IReadOnlyList<ManagedPluginDescriptor> Scan()
    {
        var descriptors = new List<ManagedPluginDescriptor>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directory in _paths)
        {
            if (!Directory.Exists(directory))
                continue;
            foreach (var path in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                var descriptor = TryReadDescriptor(path);
                if (descriptor is null)
                    continue;
                if (!names.Add(descriptor.Name))
                {
                    Log.GetLogger().LogError(
                        "Duplicate managed plugin name '{Plugin}' was found at '{Path}'.", descriptor.Name, path);
                    descriptors.RemoveAll(candidate => candidate.Name == descriptor.Name);
                    continue;
                }
                descriptors.Add(descriptor);
            }
        }
        return descriptors.OrderBy(static descriptor => descriptor.Name, StringComparer.Ordinal).ToArray();
    }

    public void Populate(PluginManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        foreach (var descriptor in Scan())
            manager.AddPlugin(new ManagedAssemblyPlugin(descriptor));
    }

    internal static ManagedPluginDescriptor? TryReadDescriptor(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
            if (!pe.HasMetadata)
                return null;
            var reader = pe.GetMetadataReader();
            foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
            {
                var attribute = reader.GetCustomAttribute(handle);
                if (!IsPluginAttribute(reader, attribute.Constructor))
                    continue;
                var blob = reader.GetBlobReader(attribute.Value);
                if (blob.ReadUInt16() != 1)
                    return null;
                var name = blob.ReadSerializedString();
                _ = blob.ReadSerializedString(); // Serialized System.Type application entry point.
                if (string.IsNullOrWhiteSpace(name))
                    return null;
                var options = ReadPluginOptions(ref blob);
                var info = new FileInfo(path);
                return new ManagedPluginDescriptor(name, Path.GetFullPath(path), options.ReloadPolicy, options.AutoLoad,
                    info.LastWriteTimeUtc, info.Length);
            }
        }
        catch (Exception exception) when (exception is IOException or BadImageFormatException
            or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.GetLogger().LogDebug(exception, "Skipping non-plugin assembly '{Path}'.", path);
        }
        return null;
    }

    private static bool IsPluginAttribute(MetadataReader reader, EntityHandle constructor)
    {
        EntityHandle parent = constructor.Kind switch
        {
            HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)constructor).Parent,
            HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
            _ => default,
        };
        return parent.Kind switch
        {
            HandleKind.TypeReference => IsPluginType(reader,
                reader.GetTypeReference((TypeReferenceHandle)parent).Namespace,
                reader.GetTypeReference((TypeReferenceHandle)parent).Name),
            HandleKind.TypeDefinition => IsPluginType(reader,
                reader.GetTypeDefinition((TypeDefinitionHandle)parent).Namespace,
                reader.GetTypeDefinition((TypeDefinitionHandle)parent).Name),
            _ => false,
        };
    }

    private static bool IsPluginType(MetadataReader reader, StringHandle namespaceHandle, StringHandle nameHandle) =>
        reader.StringComparer.Equals(namespaceHandle, "RedBolt.Plugins")
        && reader.StringComparer.Equals(nameHandle, nameof(PluginModuleAttribute));

    private static (PluginReloadPolicy ReloadPolicy, bool AutoLoad) ReadPluginOptions(ref BlobReader blob)
    {
        var reloadPolicy = PluginReloadPolicy.Collectible;
        var autoLoad = false;
        if (blob.RemainingBytes < 2)
            return (reloadPolicy, autoLoad);
        var count = blob.ReadUInt16();
        for (var index = 0; index < count && blob.RemainingBytes > 0; ++index)
        {
            _ = blob.ReadByte(); // Field or property.
            var type = blob.ReadByte();
            if (type == 0x55) // Enum.
                _ = blob.ReadSerializedString();
            var name = blob.ReadSerializedString();
            if (type == 0x55 && blob.RemainingBytes >= sizeof(int))
            {
                var value = blob.ReadInt32();
                if (name == nameof(PluginModuleAttribute.ReloadPolicy))
                    reloadPolicy = (PluginReloadPolicy)value;
            }
            else if (type == 0x02 && blob.RemainingBytes >= sizeof(byte))
            {
                var value = blob.ReadByte() != 0;
                if (name == nameof(PluginModuleAttribute.AutoLoad))
                    autoLoad = value;
            }
            else
                break;
        }
        return (reloadPolicy, autoLoad);
    }

    private readonly IReadOnlyList<string> _paths;
}

public sealed record ManagedPluginDescriptor(
    string Name,
    string AssemblyPath,
    PluginReloadPolicy ReloadPolicy,
    bool AutoLoad,
    DateTime LastWriteTimeUtc,
    long Length);

internal sealed class ManagedAssemblyPlugin : Plugin
{
    public ManagedAssemblyPlugin(ManagedPluginDescriptor descriptor)
        : base(descriptor.Name, descriptor.ReloadPolicy, descriptor.AutoLoad)
    {
        _descriptor = descriptor;
    }

    public override bool IsOutOfDate
    {
        get
        {
            if (ReloadPolicy != PluginReloadPolicy.Collectible || Application is null)
                return false;
            var info = new FileInfo(_descriptor.AssemblyPath);
            return info.Exists && (info.LastWriteTimeUtc != _loadedWriteTimeUtc || info.Length != _loadedLength);
        }
    }

    public override bool IsReadyToReload => ManagedPluginCatalog.TryReadDescriptor(_descriptor.AssemblyPath) is not null;

    public override bool Load()
    {
        if (Application is not null)
            return true;

        var candidate = CreateCandidate();
        if (candidate is null)
            return false;
        _backend = candidate.Value.Backend;
        Application = candidate.Value.Application;
        Version = candidate.Value.Version;
        _loadedWriteTimeUtc = candidate.Value.WriteTimeUtc;
        _loadedLength = candidate.Value.Length;
        return true;
    }

    public override bool BeginReload()
    {
        if (ReloadPolicy != PluginReloadPolicy.Collectible || _backend?.Current is null || Application is null)
            return false;

        _backend.Current.Unregister();
        var candidate = CreateCandidate();
        if (candidate is null)
        {
            _backend.Current.Register(RedBolt.Core.Context.Instance);
            return false;
        }
        _previous = new Generation(_backend, Application, Version, _loadedWriteTimeUtc, _loadedLength);
        _backend = candidate.Value.Backend;
        Application = candidate.Value.Application;
        Version = candidate.Value.Version;
        _loadedWriteTimeUtc = candidate.Value.WriteTimeUtc;
        _loadedLength = candidate.Value.Length;
        return true;
    }

    public override void CommitReload()
    {
        if (_previous is not { } previous)
            return;
        previous.Application.Dispose();
        previous.Backend.Dispose();
        _previous = null;
    }

    public override void RollbackReload()
    {
        if (_previous is not { } previous)
            return;
        Application?.Dispose();
        _backend?.Dispose();
        previous.Backend.Current!.Register(RedBolt.Core.Context.Instance);
        _backend = previous.Backend;
        Application = previous.Application;
        Version = previous.Version;
        _loadedWriteTimeUtc = previous.WriteTimeUtc;
        _loadedLength = previous.Length;
        _previous = null;
    }

    private Generation? CreateCandidate()
    {
        IAssemblyBackend backend = ReloadPolicy == PluginReloadPolicy.Collectible
            ? new ReloadableAssemblyBackend()
            : new DesktopLoadOnlyAssemblyBackend();

        var directory = Path.GetDirectoryName(_descriptor.AssemblyPath)!;
        var shared = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => AssemblyLoadContext.GetLoadContext(assembly) == AssemblyLoadContext.Default)
            .ToArray();
        var outputRoot = Directory.GetParent(directory)?.FullName;
        var dependencyDirectories = new[]
            {
                directory,
                outputRoot is null ? null : Path.Combine(outputRoot, "core"),
                outputRoot is null ? null : Path.Combine(outputRoot, "extern"),
                outputRoot is null ? null : Path.Combine(outputRoot, "tools"),
            }
            .Where(static path => path is not null && Directory.Exists(path))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var dependencies = dependencyDirectories
            .SelectMany(static path => Directory.EnumerateFiles(path, "*.dll", SearchOption.TopDirectoryOnly))
            .Where(path => !string.Equals(Path.GetFullPath(path), _descriptor.AssemblyPath, StringComparison.Ordinal))
            .Select(static path => (Path: path, Name: TryGetAssemblyName(path)))
            .Where(static item => item.Name is not null)
            .GroupBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First().Path)
            .ToArray();
        var nativeDependencies = dependencyDirectories
            .SelectMany(static path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            .Where(static path => path.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase))
            .GroupBy(static path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        using var preparation = backend.Prepare(new DesktopAssemblyInput(
            _descriptor.AssemblyPath, shared, dependencyPaths: dependencies,
            nativeDependencyPaths: nativeDependencies));
        if (!preparation.Succeeded)
        {
            foreach (var diagnostic in preparation.Diagnostics)
                Log.GetLogger().LogError("Plugin '{Plugin}': {Diagnostic}", Name, diagnostic.Message);
            backend.Dispose();
            return null;
        }

        var generation = backend.Commit(preparation);
        try
        {
            generation.Register(RedBolt.Core.Context.Instance);
            var metadata = generation.Assembly.GetCustomAttribute<PluginModuleAttribute>()
                ?? throw new InvalidOperationException("Plugin metadata disappeared after loading.");
            if (!string.Equals(metadata.Name, Name, StringComparison.Ordinal))
                throw new InvalidOperationException("Plugin identity changed between discovery and load.");
            if (!typeof(PluginApplication).IsAssignableFrom(metadata.ApplicationType)
                || metadata.ApplicationType.IsAbstract)
            {
                throw new InvalidOperationException(
                    $"Plugin entry point '{metadata.ApplicationType}' is not a concrete PluginApplication.");
            }
            var application = (PluginApplication?)Activator.CreateInstance(metadata.ApplicationType)
                ?? throw new InvalidOperationException("Plugin application could not be constructed.");
            application.PluginName = Name;
            application.PluginDirectory = Path.GetDirectoryName(_descriptor.AssemblyPath)!;
            var info = new FileInfo(_descriptor.AssemblyPath);
            return new Generation(backend, application, Version + 1, info.LastWriteTimeUtc, info.Length);
        }
        catch (Exception exception)
        {
            Log.GetLogger().LogError(exception, "Plugin '{Plugin}' could not be activated.", Name);
            backend.Dispose();
            return null;
        }
    }

    public override void Unload()
    {
        Application?.Dispose();
        Application = null;
        _backend?.Dispose();
        _backend = null;
    }

    public override void Dispose()
    {
        Unload();
        base.Dispose();
    }

    private readonly ManagedPluginDescriptor _descriptor;
    private IAssemblyBackend? _backend;
    private Generation? _previous;
    private DateTime _loadedWriteTimeUtc;
    private long _loadedLength;

    private readonly record struct Generation(
        IAssemblyBackend Backend,
        PluginApplication Application,
        uint Version,
        DateTime WriteTimeUtc,
        long Length);

    private static string? TryGetAssemblyName(string path)
    {
        try
        {
            return AssemblyName.GetAssemblyName(path).Name;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }
}
