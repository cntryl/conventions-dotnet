using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Cntryl.Conventions.Testing.Analyzers.Tests;

/// <summary>
/// Compiles a snippet against a minimal xUnit-shaped stub and runs analyzers or code fixes over it.
/// </summary>
static class AnalyzerHarness
{
    static readonly MetadataReference[] RuntimeReferences =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
    ];

    const string XunitStub = """
        namespace Xunit
        {
            public class FactAttribute : System.Attribute { }
            public class TheoryAttribute : FactAttribute { }
            public static class Assert
            {
                public static void True(bool condition) { }
                public static void Equal(object expected, object actual) { }
            }
        }
        """;

    internal static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        bool includeXunit = true)
    {
        using var workspace = CreateWorkspace(source, out var document, includeXunit);
        return await GetDiagnosticsAsync(document, analyzer);
    }

    internal static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Document document, DiagnosticAnalyzer analyzer)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await document.Project.GetCompilationAsync(cancellationToken);
        Assert.NotNull(compilation);
        Assert.DoesNotContain(compilation.GetDiagnostics(cancellationToken), d => d.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([analyzer], document.Project.AnalyzerOptions)
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        return diagnostics;
    }

    internal static async Task<string> ApplyFixAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        string diagnosticId,
        bool omitProperties = false)
    {
        using var workspace = CreateWorkspace(source, out var document);
        var diagnostics = await GetDiagnosticsAsync(document, analyzer);
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == diagnosticId));
        if (omitProperties)
        {
            diagnostic = Diagnostic.Create(diagnostic.Id, diagnostic.Descriptor.Category, diagnostic.GetMessage(),
                diagnostic.Severity, diagnostic.DefaultSeverity, diagnostic.Descriptor.IsEnabledByDefault,
                diagnostic.WarningLevel, location: diagnostic.Location);
        }

        var action = await GetActionAsync(document, provider, diagnostic);
        return action is null ? source : await ApplyActionAsync(document, action);
    }

    internal static async Task<string> ApplyFixAllAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        string diagnosticId)
    {
        using var workspace = CreateWorkspace(source, out var document);
        return await ApplyFixAllAsync(document, analyzer, provider, diagnosticId);
    }

    internal static async Task<string> ApplyFixAllAsync(
        Document document,
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        string diagnosticId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = await document.GetSyntaxTreeAsync(cancellationToken);
        var diagnostics = (await GetDiagnosticsAsync(document, analyzer))
            .Where(d => d.Id == diagnosticId && d.Location.SourceTree == tree)
            .ToImmutableArray();
        var fixAll = provider.GetFixAllProvider();
        Assert.NotNull(fixAll);
        foreach (var diagnostic in diagnostics)
        {
            var action = await GetActionAsync(document, provider, diagnostic);
            if (action is null)
            {
                continue;
            }

            var context = new FixAllContext(document, provider, FixAllScope.Document,
                action.EquivalenceKey, [diagnosticId], new DocumentDiagnostics(document.Id, diagnostics), cancellationToken);
            var batch = await fixAll.GetFixAsync(context);
            Assert.NotNull(batch);
            return await ApplyActionAsync(document, batch);
        }

        return (await document.GetTextAsync()).ToString();
    }

    static async Task<CodeAction?> GetActionAsync(Document document, CodeFixProvider provider, Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new CodeFixContext(
            document, diagnostic, (action, _) => actions.Add(action), TestContext.Current.CancellationToken));
        return actions.SingleOrDefault();
    }

    static async Task<string> ApplyActionAsync(Document document, CodeAction action)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var operations = await action.GetOperationsAsync(cancellationToken);
        var applied = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var changed = applied.GetDocument(document.Id)!;
        var compilation = await changed.Project.GetCompilationAsync(cancellationToken);
        Assert.NotNull(compilation);
        Assert.DoesNotContain(compilation.GetDiagnostics(cancellationToken), d => d.Severity == DiagnosticSeverity.Error);
        return (await changed.GetTextAsync(cancellationToken)).ToString();
    }

    sealed class DocumentDiagnostics(DocumentId documentId, ImmutableArray<Diagnostic> diagnostics) : FixAllContext.DiagnosticProvider
    {
        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(document.Id == documentId ? diagnostics : []);

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult(Enumerable.Empty<Diagnostic>());

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(project.Id == documentId.ProjectId ? diagnostics : []);
    }

    internal static AdhocWorkspace CreateWorkspace(string source, out Document document, bool includeXunit = true)
    {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Snippet",
            "Snippet",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            metadataReferences: RuntimeReferences));

        if (includeXunit)
        {
            workspace.AddDocument(project.Id, "Stub.cs", SourceText.From(XunitStub));
        }

        document = workspace.AddDocument(project.Id, "Tests.cs", SourceText.From(source))
            .WithFilePath(Path.GetFullPath("Tests.cs"));
        return workspace;
    }
}
