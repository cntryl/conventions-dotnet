using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Cntryl.Conventions.Testing.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

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
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ConventionDiagnostics.TestNamingId];

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(ConventionDiagnostics.SuggestedNameProperty, out var suggested) ||
                string.IsNullOrEmpty(suggested))
            {
                continue;
            }

            var declaration = root.FindNode(diagnostic.Location.SourceSpan)
                .FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (declaration is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Rename to '{suggested}'",
                    cancellationToken => RenameAsync(context.Document, declaration, suggested!, cancellationToken),
                    $"ConventionsRenameTest:{suggested}"),
                diagnostic);
        }
    }

    static async Task<Solution> RenameAsync(
        Document document,
        MethodDeclarationSyntax declaration,
        string newName,
        System.Threading.CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var symbol = model?.GetDeclaredSymbol(declaration, cancellationToken);
        if (symbol is null)
        {
            return document.Project.Solution;
        }

        var solution = document.Project.Solution;
        return await Renamer.RenameSymbolAsync(
            solution,
            symbol,
            new SymbolRenameOptions(),
            newName,
            cancellationToken).ConfigureAwait(false);
    }
}
