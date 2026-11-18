using Cntryl.Conventions.Testing.Analyzers;
using Cntryl.Conventions.Testing.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cntryl.Conventions.Testing.Tests;

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
    public async Task ShouldReportOnceGivenPartialTestMethodWhenAnalyzing()
    {
        // Arrange
        const string source = """
            using Xunit;

            public partial class Sample
            {
                [Fact]
                partial void ShouldDoubleValueGivenOneWhenMultiplying();
            }

            public partial class Sample
            {
                partial void ShouldDoubleValueGivenOneWhenMultiplying()
                {
                    var value = 1;
                    var doubled = value * 2;
                    Assert.Equal(2, doubled);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestStructureAnalyzer());

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ConventionDiagnostics.TestStructureId, diagnostic.Id);
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

    [Theory]
    [InlineData(0, false)]
    [InlineData(5000, false)]
    [InlineData(0, true)]
    public void ShouldLocateOnlyRealMarkersGivenCommentLikeTextWhenScanning(int padding, bool detach)
    {
        // Arrange
        var source = $$""""
            class Sample
            {
                void Method()
                    // Arrange outside braces
                {
                    // Arrange: real
                    string s = """// Assert""";
                    /* // Act */
                    #region // Arrange
                    #endregion
                    #if false
                    // Act
                    #endif
                    // ArrangeExtra
                    // An example of // Act
                    // {{new string('x', padding)}}
                    // Act: real
                } // Assert outside braces
            }
            """";
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
        var body = tree.GetRoot(cancellationToken).DescendantNodes().OfType<BlockSyntax>().Single();
        if (detach)
        {
            body = body.WithOpenBraceToken(body.OpenBraceToken.WithLeadingTrivia(SyntaxFactory.Comment("// Arrange outside braces")));
        }

        var expectedArrange = body.FullSpan.Start + body.ToFullString().IndexOf("// Arrange: real", StringComparison.Ordinal);
        var expectedAct = body.FullSpan.Start + body.ToFullString().IndexOf("// Act: real", StringComparison.Ordinal);

        // Act
        var markers = TestPhaseComments.Locate(body);

        // Assert
        Assert.Equal((expectedArrange, expectedAct, -1), markers);
    }

    [Theory]
    [InlineData("Xunit.Assert.True(true);")]
    [InlineData("global::Xunit.Assert.True(true);")]
    public async Task ShouldLocateQualifiedAssertionGivenAFrameworkNamespaceWhenApplyingFix(string assertion)
    {
        // Arrange
        var source = Body("int x = 1;\nx++;\n" + assertion);

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAsync(
            source, new TestStructureAnalyzer(), new TestStructureCodeFixProvider(), ConventionDiagnostics.TestStructureId);

        // Assert
        Assert.Contains("// Assert\n" + assertion, fixedSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("// Arrange\n", false)]
    public async Task ShouldInspectTheBodyGivenALegacyDiagnosticWhenApplyingFix(string opening, bool expectFix)
    {
        // Arrange
        var source = "using Xunit; class Sample { [Fact] void Example() { " + opening +
            "int x = 1; x++; Assert.Equal(2, x); } }";

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAsync(source, new TestStructureAnalyzer(),
            new TestStructureCodeFixProvider(), ConventionDiagnostics.TestStructureId, omitProperties: true);

        // Assert
        if (expectFix)
        {
            Assert.Empty(await AnalyzerHarness.GetDiagnosticsAsync(fixedSource, new TestStructureAnalyzer()));
        }
        else
        {
            Assert.Equal(source, fixedSource);
        }
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task ShouldPreserveLayoutGivenCustomIndentationAndLineEndingsWhenApplyingFix(string newLine)
    {
        // Arrange
        var source = Body("\tvar value = 1;\n\t\n\tvar doubled = value * 2;\n\tAssert.Equal(2, doubled);")
            .ReplaceLineEndings(newLine);
        var expected = Body("\t// Arrange\n\tvar value = 1;\n\t\n\t// Act\n\tvar doubled = value * 2;\n\n\t// Assert\n\tAssert.Equal(2, doubled);")
            .ReplaceLineEndings(newLine);

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAsync(
            source, new TestStructureAnalyzer(), new TestStructureCodeFixProvider(), ConventionDiagnostics.TestStructureId);

        // Assert
        Assert.Equal(expected, fixedSource);
    }

    [Theory]
    [InlineData("System.Action check = () => Assert.True(false);")]
    [InlineData("void Check() { Assert.True(false); }")]
    public async Task ShouldSkipDeferredAssertionsGivenASeparateActWhenApplyingFix(string deferred)
    {
        // Arrange
        var source = Body(deferred + "\nint result = 1;\nAssert.Equal(1, result);");
        var expected = Body("// Arrange\n" + deferred + "\n\n// Act\nint result = 1;\n\n// Assert\nAssert.Equal(1, result);");

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAsync(
            source, new TestStructureAnalyzer(), new TestStructureCodeFixProvider(), ConventionDiagnostics.TestStructureId);

        // Assert
        Assert.Equal(expected, fixedSource);
    }

    [Fact]
    public async Task ShouldFixEligibleBodiesGivenPartiallyMarkedAndCompactMethodsWhenApplyingFixAll()
    {
        // Arrange
        const string source = """
            using Xunit;

            class Sample
            {
                [Fact] public void First() { int x = 1; x++; Assert.Equal(2, x); }
                [Fact] public void Second() { int x = 2; x++; Assert.Equal(3, x); }
                [Fact] public void Third()
                {
                    // Act
                    int x = 3;
                    x++;
                    Assert.Equal(4, x);
                }
            }
            """;

        // Act
        var fixedSource = await AnalyzerHarness.ApplyFixAllAsync(
            source, new TestStructureAnalyzer(), new TestStructureCodeFixProvider(), ConventionDiagnostics.TestStructureId);
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(fixedSource, new TestStructureAnalyzer());

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("'Third'", diagnostic.GetMessage(), StringComparison.Ordinal);
        var untouchedStart = source.IndexOf("[Fact] public void Third", StringComparison.Ordinal);
        Assert.EndsWith(source[untouchedStart..], fixedSource, StringComparison.Ordinal);
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
