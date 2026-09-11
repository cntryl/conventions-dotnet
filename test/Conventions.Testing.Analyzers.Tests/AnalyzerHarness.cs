using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Cntryl.Conventions.Testing.Tests;

/// <summary>
/// Compiles a snippet against a minimal xUnit-shaped stub and runs analyzers or code fixes over it.
/// </summary>
static class AnalyzerHarness
{
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
        DiagnosticAnalyzer analyzer)
    {
        using var workspace = CreateWorkspace(source, out var document);
        var compilation = await document.Project.GetCompilationAsync();
        Assert.NotNull(compilation);
        Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        return await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
    }

    internal static async Task<string> ApplyFixAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        string diagnosticId)
    {
        using var workspace = CreateWorkspace(source, out var document);
        var compilation = await document.Project.GetCompilationAsync();
        Assert.NotNull(compilation);
        var diagnostics = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == diagnosticId));

        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None));

        if (actions.Count == 0)
        {
            return source;
        }

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applied = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var changed = applied.GetDocument(document.Id)!;
        return (await changed.GetTextAsync()).ToString();
    }

    static AdhocWorkspace CreateWorkspace(string source, out Document document)
    {
        var workspace = new AdhocWorkspace();
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
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            ]));

        workspace.AddDocument(project.Id, "Stub.cs", SourceText.From(XunitStub));
        document = workspace.AddDocument(project.Id, "Tests.cs", SourceText.From(source));
        return workspace;
    }
}
