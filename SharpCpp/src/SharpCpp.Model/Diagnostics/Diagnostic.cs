namespace SharpCpp.Model.Diagnostics;

public sealed record Diagnostic
{
    public Diagnostic(
        string code,
        DiagnosticSeverity severity,
        DiagnosticStage stage,
        string message,
        BindingSourceReference? source = null,
        IReadOnlyList<BindingSourceReference>? relatedLocations = null,
        string? affectedSymbolId = null,
        string? remediationHint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Severity = severity;
        Stage = stage;
        Message = message;
        Source = source;
        RelatedLocations = relatedLocations ?? Array.Empty<BindingSourceReference>();
        AffectedSymbolId = affectedSymbolId;
        RemediationHint = remediationHint;
    }

    public string Code { get; }

    public DiagnosticSeverity Severity { get; }

    public DiagnosticStage Stage { get; }

    public string Message { get; }

    public BindingSourceReference? Source { get; }

    public IReadOnlyList<BindingSourceReference> RelatedLocations { get; }

    public string? AffectedSymbolId { get; }

    public string? RemediationHint { get; }

    public override string ToString()
    {
        var sourceText = Source?.ToString() ?? "(unknown)";
        return $"{sourceText} {Severity} {Code} [{Stage}]: {Message}";
    }
}
