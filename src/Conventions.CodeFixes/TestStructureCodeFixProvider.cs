using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cntryl.Conventions.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cntryl.Conventions.CodeFixes;

/// <summary>
/// Inserts the Arrange/Act/Assert phase comments into a test body.
/// </summary>
/// <remarks>
/// The assert phase is located from the first asserting statement and the act phase from the
/// statement before it. When those cannot be told apart - no assertion, or fewer than three
/// statements - no fix is offered rather than guessing the boundary.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TestStructureCodeFixProvider)), Shared]
public sealed class TestStructureCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ConventionDiagnostics.TestStructureId];

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
            var declaration = root.FindNode(diagnostic.Location.SourceSpan)
                .FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (declaration?.Body is not { } body)
            {
                continue;
            }

            var (arrangeAt, actAt, assertAt) = TestPhaseComments.Locate(body);
            if (arrangeAt >= 0 || actAt >= 0 || assertAt >= 0)
            {
                // A partially phased body may already order its sections deliberately.
                continue;
            }

            var assertIndex = FindFirstAssertingStatement(body);
            if (assertIndex < 2)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add Arrange/Act/Assert comments",
                    cancellationToken => AddPhaseCommentsAsync(context.Document, root, body, assertIndex, cancellationToken),
                    "ConventionsAddPhaseComments"),
                diagnostic);
        }
    }

    static Task<Document> AddPhaseCommentsAsync(
        Document document,
        SyntaxNode root,
        BlockSyntax body,
        int assertIndex,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var statements = body.Statements;
        var rewritten = statements
            .Select((statement, index) => index switch
            {
                0 => WithPhaseComment(statement, TestPhaseComments.Arrange, blankLineBefore: false),
                _ when index == assertIndex - 1 => WithPhaseComment(statement, TestPhaseComments.Act, blankLineBefore: true),
                _ when index == assertIndex => WithPhaseComment(statement, TestPhaseComments.Assert, blankLineBefore: true),
                _ => statement,
            });

        var updated = body.WithStatements(SyntaxFactory.List(rewritten));
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(body, updated)));
    }

    static StatementSyntax WithPhaseComment(StatementSyntax statement, string comment, bool blankLineBefore)
    {
        var leading = statement.GetLeadingTrivia();
        var indent = leading.LastOrDefault(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia));

        var addition = SyntaxFactory.TriviaList();
        if (blankLineBefore)
        {
            addition = addition.Add(SyntaxFactory.ElasticCarriageReturnLineFeed);
        }

        addition = addition
            .Add(indent)
            .Add(SyntaxFactory.Comment(comment))
            .Add(SyntaxFactory.ElasticCarriageReturnLineFeed)
            .Add(indent);

        // Existing leading trivia already ends with this statement's indentation, so the
        // comment is spliced in front of it rather than replacing it.
        var trimmed = leading;
        while (trimmed.Count > 0 && trimmed[trimmed.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia))
        {
            trimmed = trimmed.RemoveAt(trimmed.Count - 1);
        }

        return statement.WithLeadingTrivia(trimmed.AddRange(addition));
    }

    static int FindFirstAssertingStatement(BlockSyntax body)
    {
        for (var index = 0; index < body.Statements.Count; index++)
        {
            if (body.Statements[index].DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsAssertion))
            {
                return index;
            }
        }

        return -1;
    }

    static bool IsAssertion(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        var member = access.Name.Identifier.ValueText;
        if (member.StartsWith("Should", StringComparison.Ordinal) ||
            member.StartsWith("Verify", StringComparison.Ordinal))
        {
            return true;
        }

        for (var expression = access.Expression; ;)
        {
            switch (expression)
            {
                case IdentifierNameSyntax identifier:
                    return identifier.Identifier.ValueText is "Assert" or "StringAssert" or "CollectionAssert";
                case MemberAccessExpressionSyntax nested:
                    expression = nested.Expression;
                    continue;
                default:
                    return false;
            }
        }
    }
}
