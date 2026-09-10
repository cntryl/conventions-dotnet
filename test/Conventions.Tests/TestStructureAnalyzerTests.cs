using Cntryl.Conventions.Analyzers;
using Cntryl.Conventions.CodeFixes;

namespace Cntryl.Conventions.Tests;

public sealed class TestStructureAnalyzerTests
{
    [Fact]
    public async Task ShouldAcceptBodyGivenOrderedPhaseCommentsWhenAnalyzing()
    {
        // Arrange
        var source = Body("""
                    // Arrange
                    var value = 1;

                    // Act
                    var doubled = value * 2;

                    // Assert
                    Assert.Equal(2, doubled);
            """);

        // Act
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestStructureAnalyzer());

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ShouldReportMissingPhasesGivenBareBodyWhenAnalyzing()
    {
        // Arrange
        var source = Body("""
                    var value = 1;
                    var doubled = value * 2;
                    Assert.Equal(2, doubled);
            """);

        // Act
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestStructureAnalyzer());

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ConventionDiagnostics.TestStructureId, diagnostic.Id);
        Assert.Contains("'// Arrange', '// Act', '// Assert'", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReportOutOfOrderPhasesGivenAssertBeforeActWhenAnalyzing()
    {
        // Arrange
        var source = Body("""
                    // Arrange
                    var value = 1;

                    // Assert
                    var doubled = value * 2;

                    // Act
                    Assert.Equal(2, doubled);
            """);

        // Act
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestStructureAnalyzer());

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("declares '// Assert' before '// Act'", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldAcceptPhaseCommentGivenTrailingProseWhenAnalyzing()
    {
        // Arrange
        var source = Body("""
                    // Arrange: one warm entry
                    var value = 1;

                    // Act - double it
                    var doubled = value * 2;

                    // Assert
                    Assert.Equal(2, doubled);
            """);

        // Act
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestStructureAnalyzer());

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ShouldInsertPhaseCommentsGivenLocatableAssertionWhenApplyingFix()
    {
        // Arrange
        var source = Body("""
                    var value = 1;
                    var doubled = value * 2;
                    Assert.Equal(2, doubled);
            """);

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAsync(
            source,
            new TestStructureAnalyzer(),
            new TestStructureCodeFixProvider(),
            ConventionDiagnostics.TestStructureId);

        // Assert
        Assert.Contains("// Arrange", fixedSource, StringComparison.Ordinal);
        Assert.Contains("// Act", fixedSource, StringComparison.Ordinal);
        Assert.Contains("// Assert", fixedSource, StringComparison.Ordinal);
        var rediagnosed = await AnalyzerHarness.GetDiagnosticsAsync(fixedSource, new TestStructureAnalyzer());
        Assert.Empty(rediagnosed);
    }

    [Fact]
    public async Task ShouldOfferNoFixGivenIndistinguishableActPhaseWhenApplyingFix()
    {
        // Arrange
        var source = Body("""
                    Assert.True(true);
            """);

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAsync(
            source,
            new TestStructureAnalyzer(),
            new TestStructureCodeFixProvider(),
            ConventionDiagnostics.TestStructureId);

        // Assert
        Assert.Equal(source, fixedSource);
    }

    static string Body(string statements) => $$"""
        using Xunit;

        public class Sample
        {
            [Fact]
            public void ShouldDoubleValueGivenOneWhenMultiplying()
            {
        {{statements}}
            }
        }
        """;
}
