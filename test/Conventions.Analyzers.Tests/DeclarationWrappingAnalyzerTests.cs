using System.Collections.Immutable;
using Cntryl.Conventions.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
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

    [Fact]
    public async Task FixesMixedWrappedMethodParameters()
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
        var fixedSource = await ApplyFixAsync(
            source,
            new DeclarationWrappingAnalyzer(),
            new DeclarationWrappingCodeFixProvider(),
            ConventionDiagnostics.DeclarationWrappingId);

        // Assert
        Assert.Contains("""
                    int first,
                    int second
                )
            """, fixedSource);
    }

    [Fact]
    public async Task FixesMissingWhitespaceAroundControlFlow()
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
        var fixedSource = await ApplyFixAsync(
            source,
            new ControlFlowSpacingAnalyzer(),
            new ControlFlowSpacingCodeFixProvider(),
            ConventionDiagnostics.ControlFlowSpacingId);

        // Assert
        Assert.Contains("\n\n", fixedSource);
        Assert.Contains("if (ready)", fixedSource);
        Assert.Contains("value++;", fixedSource);
    }

    [Fact]
    public async Task FixesSameLineControlFlowBoundary()
    {
        // Arrange
        const string source = """
            class Example
            {
                void Execute(bool ready)
                {
                    var value = 1; if (ready) { value++; }
                    value++;
                }
            }
            """;

        // Act
        var fixedSource = await ApplyFixAsync(
            source,
            new ControlFlowSpacingAnalyzer(),
            new ControlFlowSpacingCodeFixProvider(),
            ConventionDiagnostics.ControlFlowSpacingId);

        // Assert
        Assert.Contains("var value = 1;", fixedSource);
        Assert.Contains("if (ready)", fixedSource);
        Assert.Contains("value++;", fixedSource);
    }

    [Fact]
    public async Task IgnoresIncompleteDeclarationParameterList()
    {
        // Arrange
        const string source = """
            class Example
            {
                void Execute(
                    int first,
                    int second
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task AllowsPrimaryConstructorWrapping()
    {
        // Arrange
        const string source = """
            class Example(
                int first,
                int second
            )
            {
            }
            """;

        // Act
        var diagnostics = await GetDiagnosticsAsync(source);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task FixAllProducesOneBoundaryAtAdjacentControlFlow()
    {
        // Arrange
        const string source = """
            class Example
            {
                void Execute(bool first, bool second)
                {
                    if (first) { }
                    while (second) { break; }
                    var value = 1;
                }
            }
            """;

        // Act
        var fixedSource = await ApplyFixAllAsync(
            source,
            new ControlFlowSpacingAnalyzer(),
            new ControlFlowSpacingCodeFixProvider(),
            ConventionDiagnostics.ControlFlowSpacingId);

        // Assert
        Assert.Contains("}\n\n", fixedSource);
        Assert.Contains("while (second)", fixedSource);
        Assert.DoesNotContain("\n\n\n", fixedSource);
    }

    static async Task<string> ApplyFixAsync(string source, DiagnosticAnalyzer analyzer, CodeFixProvider provider, string diagnosticId)
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
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await document.Project.GetCompilationAsync(cancellationToken);
        Assert.NotNull(compilation);
        var diagnostics = await compilation.WithAnalyzers(
            [analyzer],
            document.Project.AnalyzerOptions)
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == diagnosticId));
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            cancellationToken));
        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(cancellationToken);
        var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var changedDocument = changedSolution.GetDocument(document.Id)!;
        return (await changedDocument.GetTextAsync(cancellationToken)).ToString();
    }

    static async Task<string> ApplyFixAllAsync(string source, DiagnosticAnalyzer analyzer, CodeFixProvider provider, string diagnosticId)
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
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await document.Project.GetCompilationAsync(cancellationToken);
        Assert.NotNull(compilation);
        var diagnostics = await compilation.WithAnalyzers(
            [analyzer],
            document.Project.AnalyzerOptions)
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
        var matching = diagnostics.Where(d => d.Id == diagnosticId).ToImmutableArray();
        Assert.NotEmpty(matching);
        var documentDiagnostics = new DocumentDiagnostics(document.Id, matching);
        var diagnostic = matching[0];
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            cancellationToken));
        var action = Assert.Single(actions);

        var fixAll = provider.GetFixAllProvider();
        Assert.NotNull(fixAll);
        var fixAllContext = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            action.EquivalenceKey!,
            [diagnosticId],
            documentDiagnostics,
            cancellationToken);
        var fixAllAction = await fixAll.GetFixAsync(fixAllContext);
        Assert.NotNull(fixAllAction);
        var fixAllOperations = await fixAllAction.GetOperationsAsync(cancellationToken);
        var fixedSolution = fixAllOperations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var fixedDocument = fixedSolution.GetDocument(document.Id)!;
        return (await fixedDocument.GetTextAsync(cancellationToken)).ToString();
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

    sealed class DocumentDiagnostics(DocumentId documentId, ImmutableArray<Diagnostic> diagnostics)
        : FixAllContext.DiagnosticProvider
    {
        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Diagnostic>>(
                document.Id == documentId ? diagnostics : []);
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>([]);

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>([]);
    }
}
