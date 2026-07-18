using System.Collections.Concurrent;

namespace SharpCpp.Model.Diagnostics;

public sealed class DiagnosticCollector
{
    private readonly ConcurrentQueue<StampedDiagnostic> _diagnostics = new();
    private long _nextSequence;

    public bool HasErrors => Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    public bool HasWarnings => Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);

    public bool HasDiagnostics => !_diagnostics.IsEmpty;

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics
        .OrderBy(static item => item.Sequence)
        .Select(static item => item.Diagnostic)
        .ToArray();

    public void Add(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        _diagnostics.Enqueue(new StampedDiagnostic(Interlocked.Increment(ref _nextSequence), diagnostic));
    }

    public void Error(
        DiagnosticStage stage,
        string code,
        string message,
        BindingSourceReference? source = null,
        IReadOnlyList<BindingSourceReference>? relatedLocations = null,
        string? affectedSymbolId = null,
        string? remediationHint = null)
    {
        Add(new Diagnostic(code, DiagnosticSeverity.Error, stage, message, source, relatedLocations, affectedSymbolId, remediationHint));
    }

    public void Warning(
        DiagnosticStage stage,
        string code,
        string message,
        BindingSourceReference? source = null,
        IReadOnlyList<BindingSourceReference>? relatedLocations = null,
        string? affectedSymbolId = null,
        string? remediationHint = null)
    {
        Add(new Diagnostic(code, DiagnosticSeverity.Warning, stage, message, source, relatedLocations, affectedSymbolId, remediationHint));
    }

    public void Info(
        DiagnosticStage stage,
        string code,
        string message,
        BindingSourceReference? source = null,
        IReadOnlyList<BindingSourceReference>? relatedLocations = null,
        string? affectedSymbolId = null,
        string? remediationHint = null)
    {
        Add(new Diagnostic(code, DiagnosticSeverity.Info, stage, message, source, relatedLocations, affectedSymbolId, remediationHint));
    }

    public void Clear()
    {
        while (_diagnostics.TryDequeue(out _))
        {
        }

        Interlocked.Exchange(ref _nextSequence, 0);
    }

    private sealed record StampedDiagnostic(long Sequence, Diagnostic Diagnostic);
}
