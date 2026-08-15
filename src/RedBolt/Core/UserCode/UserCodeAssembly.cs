namespace RedBolt.UserCode;

using System.Reflection;

using RedBolt.Core;

/// <summary>Input accepted by a user-code assembly backend.</summary>
public abstract record AssemblyLoadInput;

/// <summary>An assembly that was compiled and referenced by the application at build time.</summary>
public sealed record PrecompiledAssemblyInput(Assembly Assembly) : AssemblyLoadInput;

/// <summary>Diagnostic produced while preparing user code.</summary>
public sealed record UserCodeDiagnostic(
    UserCodeDiagnosticSeverity Severity,
    string Message,
    string Path = "",
    int Line = 0,
    int Column = 0);

public enum UserCodeDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>Result of preparing a user-code assembly without changing the active generation.</summary>
public sealed class AssemblyPreparation : IDisposable
{
    private readonly object _owner;

    private AssemblyPreparation(
        object owner, UserCodeAssembly? candidate, IReadOnlyList<UserCodeDiagnostic> diagnostics)
    {
        _owner = owner;
        Candidate = candidate;
        Diagnostics = diagnostics;
    }

    public UserCodeAssembly? Candidate { get; private set; }

    public IReadOnlyList<UserCodeDiagnostic> Diagnostics { get; }

    public bool Succeeded => Candidate is not null;

    internal static AssemblyPreparation Success(
        object owner, UserCodeAssembly candidate, IReadOnlyList<UserCodeDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(candidate);
        return new AssemblyPreparation(owner, candidate, diagnostics ?? []);
    }

    internal static AssemblyPreparation Failure(
        object owner, params IReadOnlyList<UserCodeDiagnostic> diagnostics) => new(owner, null, diagnostics);

    internal UserCodeAssembly TakeCandidate(object owner)
    {
        if (!ReferenceEquals(owner, _owner))
            throw new InvalidOperationException("Preparation belongs to another assembly backend.");
        var candidate = Candidate ?? throw new InvalidOperationException("Preparation has no candidate.");
        Candidate = null;
        return candidate;
    }

    public void Dispose()
    {
        Candidate?.Dispose();
        Candidate = null;
    }
}

/// <summary>A prepared or active user-code assembly generation.</summary>
public abstract class UserCodeAssembly : IDisposable
{
    private readonly IReadOnlyList<Assembly> _assemblies;
    private List<AssemblyRegistration>? _registrations;
    private bool _disposed;

    protected UserCodeAssembly(
        Assembly assembly, bool isCollectible, IReadOnlyList<Assembly>? dependencyClosure = null)
    {
        Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        IsCollectible = isCollectible;
        _assemblies = dependencyClosure ?? [assembly];
        if (_assemblies.Count == 0 || !ReferenceEquals(_assemblies[^1], assembly))
        {
            throw new ArgumentException(
                "The dependency closure must end with the user-code assembly.", nameof(dependencyClosure));
        }
    }

    public Assembly Assembly { get; }

    public bool IsCollectible { get; }

    /// <summary>A weak reference used to verify eventual collectible-context reclamation.</summary>
    public virtual WeakReference? CollectionReference => null;

    /// <summary>Register generated RedBolt reflection from this generation and its dependency closure.</summary>
    public void Register(Context context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(context);
        if (_registrations is not null)
            throw new InvalidOperationException("The user-code assembly is already registered.");

        var registrations = new List<AssemblyRegistration>(_assemblies.Count);
        try
        {
            foreach (var assembly in _assemblies)
                registrations.Add(context.RegisterAssemblyScoped(assembly));
            _registrations = registrations;
        }
        catch
        {
            for (var index = registrations.Count - 1; index >= 0; --index)
                registrations[index].Dispose();
            throw;
        }
    }

    /// <summary>Remove generated RedBolt reflection while keeping the generation available for rollback.</summary>
    public void Unregister()
    {
        if (_registrations is not { } registrations)
            return;
        for (var index = registrations.Count - 1; index >= 0; --index)
            registrations[index].Dispose();
        _registrations = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Unregister();
        foreach (var assembly in _assemblies)
            GeneratedReflectionRegistration.ForgetCollectibleAssembly(assembly);
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    protected abstract void DisposeCore();
}

public interface IAssemblyBackend : IDisposable
{
    UserCodeAssembly? Current { get; }

    AssemblyPreparation Prepare(AssemblyLoadInput input);

    /// <summary>
    /// Replace the active generation. Before calling this, consumers must stop the previous plugin application,
    /// release its managed and native resources, unregister callbacks and workers, and call <see cref="UserCodeAssembly.Unregister"/>.
    /// </summary>
    UserCodeAssembly Commit(AssemblyPreparation preparation);

    void Unload();
}

/// <summary>Loads a statically referenced assembly once. The code image remains resident for process lifetime.</summary>
public sealed class LoadOnlyAssemblyBackend : IAssemblyBackend
{
    public UserCodeAssembly? Current { get; private set; }

    public AssemblyPreparation Prepare(AssemblyLoadInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (Current is not null)
            return AssemblyPreparation.Failure(this, new UserCodeDiagnostic(
                UserCodeDiagnosticSeverity.Error, "A load-only backend already has an active assembly."));
        if (input is not PrecompiledAssemblyInput precompiled)
            return AssemblyPreparation.Failure(this, new UserCodeDiagnostic(
                UserCodeDiagnosticSeverity.Error, "The load-only backend requires a precompiled assembly."));
        return AssemblyPreparation.Success(this, new PrecompiledUserCodeAssembly(precompiled.Assembly));
    }

    public UserCodeAssembly Commit(AssemblyPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (Current is not null)
            throw new InvalidOperationException("A load-only backend cannot replace its active assembly.");
        Current = preparation.TakeCandidate(this);
        return Current;
    }

    public void Unload()
    {
        Current?.Dispose();
        Current = null;
    }

    public void Dispose() => Unload();

    private sealed class PrecompiledUserCodeAssembly(Assembly assembly) : UserCodeAssembly(assembly, false)
    {
        protected override void DisposeCore()
        {
            // Default-context and AOT assemblies cannot be unloaded.
        }
    }
}
