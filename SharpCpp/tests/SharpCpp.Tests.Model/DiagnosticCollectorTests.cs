using SharpCpp.Model.Diagnostics;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class DiagnosticCollectorTests
{
    [Fact]
    public void Adds_diagnostics_in_sequence_order()
    {
        var collector = new DiagnosticCollector();

        collector.Warning(DiagnosticStage.Validation, "VALID0001", "warning");
        collector.Error(DiagnosticStage.Parse, "PARSE0001", "error", new BindingSourceReference("a.hpp", 12, 4));
        collector.Info(DiagnosticStage.Metadata, "META0001", "info");

        Assert.Equal(new[] { "VALID0001", "PARSE0001", "META0001" }, collector.Diagnostics.Select(static diagnostic => diagnostic.Code));
        Assert.True(collector.HasErrors);
        Assert.True(collector.HasWarnings);
        Assert.True(collector.HasDiagnostics);
    }

    [Fact]
    public void Clear_removes_all_diagnostics()
    {
        var collector = new DiagnosticCollector();
        collector.Error(DiagnosticStage.Pass, "PASS0001", "broken");

        collector.Clear();

        Assert.Empty(collector.Diagnostics);
        Assert.False(collector.HasDiagnostics);
        Assert.False(collector.HasErrors);
        Assert.False(collector.HasWarnings);
    }
}
