using Cntryl.Conventions.Analyzers;
using Cntryl.Conventions.CodeFixes;

namespace Cntryl.Conventions.Tests;

public sealed class TestNamingAnalyzerTests
{
    [Theory]
    [InlineData("ShouldReturnCachedValueGivenWarmCacheWhenReading")]
    [InlineData("ShouldRejectEmptyRouteGivenStrictValidation")]
    public async Task ShouldAcceptConformingNameGivenShouldAndGivenClausesWhenAnalyzing(string methodName)
    {
        // Arrange
        var source = TestClass(methodName);

        // Act
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestNamingAnalyzer());

        // Assert
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("AcceptsValidRoute", "must start with 'Should'")]
    [InlineData("ShouldReturnCachedValue", "must state a precondition with 'Given<State>'")]
    [InlineData("ShouldGivenWarmCacheReturnValue", "must state an outcome between 'Should' and 'Given'")]
    [InlineData("ShouldReturnValueWhenReadingGivenWarmCache", "must order clauses as Should, then Given, then When")]
    public async Task ShouldReportViolationGivenNonconformingNameWhenAnalyzing(string methodName, string expected)
    {
        // Arrange
        var source = TestClass(methodName);

        // Act
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestNamingAnalyzer());

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ConventionDiagnostics.TestNamingId, diagnostic.Id);
        Assert.Contains(expected, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldIgnoreNonTestMethodGivenNoTestAttributeWhenAnalyzing()
    {
        // Arrange
        var source = """
            public class Helper
            {
                public void AcceptsAnything() { }
            }
            """;

        // Act
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestNamingAnalyzer());

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ShouldNotMatchGivenInsideWordGivenForgivenessInNameWhenAnalyzing()
    {
        // Arrange
        var source = TestClass("ShouldApplyForgivenessGivenLateRenewal");

        // Act
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestNamingAnalyzer());

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ShouldRenameThirdPersonNameGivenDerivableVerbWhenApplyingFix()
    {
        // Arrange
        var source = TestClass("AcceptsValidRouteGivenStrictValidation");

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAsync(
            source,
            new TestNamingAnalyzer(),
            new TestNamingCodeFixProvider(),
            ConventionDiagnostics.TestNamingId);

        // Assert
        Assert.Contains("ShouldAcceptValidRouteGivenStrictValidation", fixedSource, StringComparison.Ordinal);
    }

    static string TestClass(string methodName) => $$"""
        using Xunit;

        public class Sample
        {
            [Fact]
            public void {{methodName}}()
            {
                var value = 1;
                var doubled = value * 2;
                Assert.Equal(2, doubled);
            }
        }
        """;
}
