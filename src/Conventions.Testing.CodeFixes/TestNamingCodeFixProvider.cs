using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Composition;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Cntryl.Conventions.Testing.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace Cntryl.Conventions.Testing.CodeFixes;

/// <summary>
/// Renames a third-person test method to its <c>Should</c>-prefixed form.
/// </summary>
/// <remarks>
/// Only the mechanically derivable part is offered. A missing <c>Given</c> or <c>When</c> clause
/// states intent that is not present anywhere in the source, so no fix is registered for it.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TestNamingCodeFixProvider)), Shared]
public sealed class TestNamingCodeFixProvider : CodeFixProvider
{
    const string RenameKey = "ConventionsRenameTest";
    static readonly ImmutableArray<DiagnosticAnalyzer> NamingAnalyzers = [new TestNamingAnalyzer()];
    static readonly ConditionalWeakTable<Project, ConcurrentDictionary<(SyntaxTree, TextSpan, string), bool>> CompetingFixes = new();

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [ConventionDiagnostics.TestNamingId];

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var cancellationToken = context.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(ConventionDiagnostics.SuggestedNameProperty, out var suggested) ||
                string.IsNullOrEmpty(suggested))
            {
                continue;
            }

            if (!context.Document.TryGetSemanticModel(out var model))
            {
                model = await context.Document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            }

            if (model is null)
            {
                continue;
            }

            if (!model.SyntaxTree.TryGetRoot(out var root))
            {
                root = await model.SyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
            }

            var span = diagnostic.Location.SourceSpan;
            if (root.FindToken(span.Start).Parent is not MethodDeclarationSyntax declaration ||
                declaration.Identifier.Span != span ||
                model.GetDeclaredSymbol(declaration, cancellationToken) is not IMethodSymbol method ||
                method.ContainingType.Name == suggested || HasConflict(model.Compilation, method, suggested!))
            {
                continue;
            }

            var key = RenameKey;
            var competingName = TestVerbInflection.GetCompetingSpelling(method.Name, suggested!);
            if (competingName is not null && method.ContainingType.GetMembers(competingName) is { IsEmpty: false } candidates &&
                await HasCompetingFixAsync(context.Document.Project, model, method, suggested!, candidates, cancellationToken).ConfigureAwait(false))
            {
                key += ":" + method.Name;
            }

            context.RegisterCodeFix(CreateAction(context.Document.Project.Solution, method, suggested!, key), diagnostic);
        }
    }

    static CodeAction CreateAction(Solution solution, IMethodSymbol method, string newName, string key) =>
        CodeAction.Create(
            $"Rename to '{newName}'",
            token => Renamer.RenameSymbolAsync(solution, method, new SymbolRenameOptions(), newName, token),
            key);

    static async Task<bool> HasCompetingFixAsync(
        Project project, SemanticModel model, IMethodSymbol method, string suggestion, ImmutableArray<ISymbol> candidates, CancellationToken cancellationToken)
    {
        // BatchFixer applies renames to the same starting solution. Only separate their
        // actions when the analyzer can offer both renames and they would create a duplicate.
        var compilation = model.Compilation;
        foreach (var candidate in candidates)
        {
            if (candidate is not IMethodSymbol other || other.GetAttributes().IsEmpty ||
                !HaveSameSignature(compilation, method, other) ||
                other.Locations.FirstOrDefault() is not { SourceTree: { } tree } location)
            {
                continue;
            }

            var span = location.SourceSpan;
            var key = (tree, span, suggestion);
            var cache = CompetingFixes.GetOrCreateValue(project);
            if (!cache.TryGetValue(key, out var eligible))
            {
                var analysis = compilation.WithAnalyzers(NamingAnalyzers, project.AnalyzerOptions);
                var diagnostics = await analysis.GetAnalyzerSemanticDiagnosticsAsync(
                    tree == model.SyntaxTree ? model : compilation.GetSemanticModel(tree), span, cancellationToken).ConfigureAwait(false);
                foreach (var diagnostic in diagnostics)
                {
                    if (diagnostic.Id == ConventionDiagnostics.TestNamingId && !diagnostic.IsSuppressed &&
                        diagnostic.Location.SourceTree == tree && diagnostic.Location.SourceSpan == span &&
                        diagnostic.Properties.TryGetValue(ConventionDiagnostics.SuggestedNameProperty, out var otherSuggestion) &&
                        otherSuggestion == suggestion)
                    {
                        eligible = true;
                        break;
                    }
                }
                cache.TryAdd(key, eligible);
            }

            if (eligible)
            {
                return true;
            }
        }

        return false;
    }

    static bool HasConflict(Compilation compilation, IMethodSymbol method, string name)
    {
        foreach (var member in method.ContainingType.GetMembers(name))
        {
            if (member is not IMethodSymbol other || HaveSameSignature(compilation, method, other))
            {
                return true;
            }
        }

        return false;
    }

    static bool HaveSameSignature(Compilation compilation, IMethodSymbol method, IMethodSymbol other)
    {
        if (method.Arity != other.Arity || method.Parameters.Length != other.Parameters.Length)
        {
            return false;
        }

        if (other.IsGenericMethod)
        {
            other = other.Construct(method.TypeArguments, method.TypeArgumentNullableAnnotations);
        }

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var left = method.Parameters[i];
            var right = other.Parameters[i];
            if ((left.RefKind == RefKind.None) != (right.RefKind == RefKind.None) ||
                !compilation.ClassifyCommonConversion(left.Type, right.Type).IsIdentity)
            {
                return false;
            }
        }

        return true;
    }
}
