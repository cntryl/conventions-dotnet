using Cntryl.Conventions.Testing.Analyzers;
using Cntryl.Conventions.Testing.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Cntryl.Conventions.Testing.Tests;

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

    [Fact]
    public async Task ShouldRenameEachMethodAndReferenceGivenDifferentSuggestionsWhenFixingAll()
    {
        // Arrange
        const string source = """
            using Xunit;
            public class Sample
            {
                [Fact] public void AcceptsRouteGivenInput() => RejectsRouteGivenNoInput();
                [Fact] public void RejectsRouteGivenNoInput() { }
                [Fact] public void ManualName() { }
                public string Example() => nameof(AcceptsRouteGivenInput);
            }
            """;

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAllAsync(
            source, new TestNamingAnalyzer(), new TestNamingCodeFixProvider(), ConventionDiagnostics.TestNamingId);

        // Assert
        Assert.Equal(
            source.Replace("AcceptsRouteGivenInput", "ShouldAcceptRouteGivenInput", StringComparison.Ordinal)
                .Replace("RejectsRouteGivenNoInput", "ShouldRejectRouteGivenNoInput", StringComparison.Ordinal),
            fixedSource);
    }

    [Theory]
    [InlineData("public int ShouldAcceptGivenInput { get; set; }")]
    [InlineData("public void ShouldAcceptGivenInput<U>(out U value) => value = default;")]
    public async Task ShouldSkipRenameGivenAConflictingMemberWhenApplyingFix(string member)
    {
        // Arrange
        var source = $$"""
            partial class Sample { [Xunit.Fact] public void AcceptsGivenInput<T>(ref T value) { } }
            partial class Sample { {{member}} }
            """;

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAsync(
            source, new TestNamingAnalyzer(), new TestNamingCodeFixProvider(), ConventionDiagnostics.TestNamingId);

        // Assert
        Assert.Equal(source, fixedSource);
    }

    [Fact]
    public async Task ShouldRenameGivenACompatibleOverloadWhenApplyingFix()
    {
        // Arrange
        const string source = """
            class Sample
            {
                [Xunit.Fact] public void AcceptsGivenInput(int value) { }
                public void ShouldAcceptGivenInput(string value) { }
                public void Run() => AcceptsGivenInput(1);
            }
            """;

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAsync(
            source, new TestNamingAnalyzer(), new TestNamingCodeFixProvider(), ConventionDiagnostics.TestNamingId);

        // Assert
        Assert.Equal(source.Replace("AcceptsGivenInput", "ShouldAcceptGivenInput", StringComparison.Ordinal), fixedSource);
    }

    [Theory]
    [InlineData("Tries", "Trys", "Try")]
    [InlineData("Fixes", "Fixs", "Fix")]
    [InlineData("Washes", "Washs", "Wash")]
    [InlineData("Matches", "Matchs", "Match")]
    [InlineData("Buzzes", "Buzzs", "Buzz")]
    public async Task ShouldKeepOneOriginalNameGivenConvergingVerbsWhenFixingAll(string first, string second, string target)
    {
        // Arrange
        var source = $$"""
            class Sample
            {
                [Xunit.Fact] public void {{first}}GivenInput() { }
                [Xunit.Fact] public void {{second}}GivenInput() { }
            }
            """;

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAllAsync(
            source, new TestNamingAnalyzer(), new TestNamingCodeFixProvider(), ConventionDiagnostics.TestNamingId);

        // Assert
        var expectedName = $"Should{target}GivenInput";
        Assert.True(
            fixedSource == source.Replace($"{first}GivenInput", expectedName, StringComparison.Ordinal) ||
            fixedSource == source.Replace($"{second}GivenInput", expectedName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldRenameBothTestsGivenACompetingHelperWithAnAttributeWhenFixingAll()
    {
        // Arrange
        const string source = """
            partial class Sample
            {
                [Xunit.Fact] public void TriesGivenInput() { }
                [System.Obsolete] public void TrysGivenInput() { }
                [Xunit.Fact] public void AcceptsGivenInput() { }
            }
            """;

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAllAsync(
            source, new TestNamingAnalyzer(), new TestNamingCodeFixProvider(), ConventionDiagnostics.TestNamingId);

        // Assert
        Assert.Equal(source.Replace("TriesGivenInput", "ShouldTryGivenInput", StringComparison.Ordinal)
            .Replace("AcceptsGivenInput", "ShouldAcceptGivenInput", StringComparison.Ordinal), fixedSource);
    }

    [Fact]
    public async Task ShouldRenameBothTestsGivenACompetingNameWithoutAFixInAnotherFileWhenFixingAll()
    {
        // Arrange
        const string source = """
            partial class Sample
            {
                [Xunit.Fact] public void TriesGivenInput() { }
                [Xunit.Fact] public void AcceptsGivenInput() { }
            }
            """;
        using var workspace = AnalyzerHarness.CreateWorkspace(source, out var document);
        var project = document.Project.AddDocument("Other.cs",
            "partial class Sample { [Xunit.Fact] public void TrysGivenInput() { } }", filePath: Path.GetFullPath("Other.cs")).Project;
        var config = project.AddAnalyzerConfigDocument(".editorconfig", SourceText.From("""
            root = true
            [Other.cs]
            conventions_test_naming_require_when = true
            """), filePath: Path.GetFullPath(".editorconfig"));

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAllAsync(config.Project.GetDocument(document.Id)!,
            new TestNamingAnalyzer(), new TestNamingCodeFixProvider(), ConventionDiagnostics.TestNamingId);

        // Assert
        Assert.Equal(source.Replace("TriesGivenInput", "ShouldTryGivenInput", StringComparison.Ordinal)
            .Replace("AcceptsGivenInput", "ShouldAcceptGivenInput", StringComparison.Ordinal), fixedSource);
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
