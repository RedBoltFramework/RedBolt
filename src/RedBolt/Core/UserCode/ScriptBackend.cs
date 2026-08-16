// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.UserCode;

using System.Reflection;

/// <summary>Input accepted by a script backend.</summary>
public abstract record ScriptBundleInput;

/// <summary>A script bundle compiled and referenced at build time.</summary>
public sealed record PrecompiledScriptBundle(Assembly Assembly) : ScriptBundleInput;

public sealed class ScriptPreparation : IDisposable
{
    internal ScriptPreparation(
        AssemblyPreparation? assemblyPreparation,
        bool unchanged,
        string fingerprint,
        IReadOnlyList<UserCodeDiagnostic> diagnostics)
    {
        AssemblyPreparation = assemblyPreparation;
        Unchanged = unchanged;
        Fingerprint = fingerprint;
        Diagnostics = diagnostics;
    }

    internal AssemblyPreparation? AssemblyPreparation { get; private set; }

    internal string Fingerprint { get; }

    public UserCodeAssembly? Candidate => AssemblyPreparation?.Candidate;

    public IReadOnlyList<UserCodeDiagnostic> Diagnostics { get; }

    public bool Succeeded => Unchanged || Candidate is not null;

    public bool Unchanged { get; }

    internal AssemblyPreparation TakeAssemblyPreparation()
    {
        var preparation = AssemblyPreparation
            ?? throw new InvalidOperationException("Script preparation has no assembly candidate.");
        AssemblyPreparation = null;
        return preparation;
    }

    public void Dispose()
    {
        AssemblyPreparation?.Dispose();
        AssemblyPreparation = null;
    }
}

public interface IScriptBackend : IDisposable
{
    UserCodeAssembly? Current { get; }

    ScriptPreparation Prepare(ScriptBundleInput input);

    /// <summary>
    /// Replace the active script generation after the consumer has deterministically torn down the previous
    /// generation, including native resources, callbacks, worker threads, and scoped reflection.
    /// </summary>
    UserCodeAssembly Commit(ScriptPreparation preparation);

    void Unload();
}

/// <summary>Loads one script bundle that was compiled ahead of time.</summary>
public sealed class PrecompiledScriptBackend : IScriptBackend
{
    private readonly LoadOnlyAssemblyBackend _assemblies = new();
    private string _fingerprint = string.Empty;

    public UserCodeAssembly? Current => _assemblies.Current;

    public ScriptPreparation Prepare(ScriptBundleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input is not PrecompiledScriptBundle bundle)
        {
            return new ScriptPreparation(null, false, string.Empty,
                [new UserCodeDiagnostic(UserCodeDiagnosticSeverity.Error,
                    "The precompiled script backend requires a precompiled bundle.")]);
        }

        var fingerprint = bundle.Assembly.ManifestModule.ModuleVersionId.ToString("N");
        if (Current is not null && fingerprint == _fingerprint)
            return new ScriptPreparation(null, true, fingerprint, []);

        var preparation = _assemblies.Prepare(new PrecompiledAssemblyInput(bundle.Assembly));
        return new ScriptPreparation(preparation, false, fingerprint, preparation.Diagnostics);
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
}
