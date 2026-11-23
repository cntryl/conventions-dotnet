using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Cntryl.Conventions.Analyzers.Tests;

public sealed class DeclarationWrappingAnalyzerTests
{
    [Fact]
    public async Task ReportsMixedWrappedMethodParameters()
    {
        // Arrange
        const string source = """
            class Example
            {
                void Execute(
                    int first,
                    int second) { }
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ConventionDiagnostics.DeclarationWrappingId, diagnostic.Id);
    }

    [Fact]
    public async Task ReportsMixedWrappedConstructorParameters()
    {
        // Arrange
        const string source = """
            class Example
            {
                Example(int first,
                        int second) { }
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ConventionDiagnostics.DeclarationWrappingId, diagnostic.Id);
    }

    [Fact]
    public async Task AllowsAtomicWrappedParameters()
    {
        // Arrange
        const string source = """
            class Example
            {
                void Execute(
                    int first,
                    int second
                )
                { }
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task AllowsSingleLineParameters()
    {
        // Arrange
        const string source = """
            class Example
            {
                void Execute(int first, int second) { }
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsMissingWhitespaceAroundControlFlow()
    {
        // Arrange
        const string source = """
            class Example
            {
                void Execute(bool ready)
                {
                    var value = 1;
                    if (ready)
                    {
                        value++;
                    }
                    value++;
                }
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source, new ControlFlowSpacingAnalyzer());

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ConventionDiagnostics.ControlFlowSpacingId, diagnostic.Id);
    }

    [Fact]
    public async Task AllowsWhitespaceAroundControlFlow()
    {
        // Arrange
        const string source = """
            class Example
            {
                void Execute(bool ready)
                {
                    var value = 1;

                    if (ready)
                    {
                        value++;
                    }

                    value++;
                }
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source, new ControlFlowSpacingAnalyzer());

        // Assert
        Assert.Empty(diagnostics);
    }

    static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source, DiagnosticAnalyzer? analyzer = null)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Snippet",
            "Snippet",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            metadataReferences:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
            ]));
        var document = workspace.AddDocument(project.Id, "Snippet.cs", SourceText.From(source));
        var compilation = await document.Project.GetCompilationAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(compilation);
        return await compilation.WithAnalyzers(
            [analyzer ?? new DeclarationWrappingAnalyzer()],
            document.Project.AnalyzerOptions)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }
}
