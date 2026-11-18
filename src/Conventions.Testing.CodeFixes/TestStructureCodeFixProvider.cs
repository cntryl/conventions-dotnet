using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Cntryl.Conventions.Testing.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Cntryl.Conventions.Testing.CodeFixes;

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
    static readonly FixAllProvider DocumentFixer = FixAllProvider.Create(FixDocumentAsync,
        [FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution, FixAllScope.ContainingMember, FixAllScope.ContainingType]);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [ConventionDiagnostics.TestStructureId];

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => DocumentFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = null;
        foreach (var diagnostic in context.Diagnostics)
        {
            var hasMarkers = HasMarkers(diagnostic);
            if (hasMarkers is true)
            {
                continue;
            }

            root ??= await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null || FindPhaseStarts(root, diagnostic, hasMarkers is null, context.CancellationToken) is not { } starts)
            {
                continue;
            }

            var document = context.Document;
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add Arrange/Act/Assert comments",
                    async cancellationToken =>
                    {
                        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                        var changes = new List<TextChange>(3);
                        AddPhaseComments(text, starts, changes);
                        return document.WithText(text.WithChanges(changes));
                    },
                    "ConventionsAddPhaseComments"),
                diagnostic);
        }
    }

    static async Task<Document?> FixDocumentAsync(FixAllContext context, Document document, ImmutableArray<Diagnostic> diagnostics)
    {
        var cancellationToken = context.CancellationToken;
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return null;
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var changes = new List<TextChange>();
        foreach (var diagnostic in diagnostics)
        {
            var hasMarkers = HasMarkers(diagnostic);
            if (hasMarkers is not true && FindPhaseStarts(root, diagnostic, hasMarkers is null, cancellationToken) is { } starts)
            {
                AddPhaseComments(text, starts, changes);
            }
        }

        return changes.Count == 0 ? null : document.WithText(text.WithChanges(changes));
    }

    static (int Arrange, int Act, int Assert)? FindPhaseStarts(
        SyntaxNode root, Diagnostic diagnostic, bool verifyMarkers, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var declaration = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent as MethodDeclarationSyntax;
        if (declaration?.Body is not { Statements.Count: >= 3 } body)
        {
            return null;
        }

        // Diagnostics from older analyzer packages carry no marker information.
        if (verifyMarkers && TestPhaseComments.Locate(body) != (-1, -1, -1))
        {
            return null;
        }

        var statements = body.Statements;
        for (var index = 0; index < statements.Count; index++)
        {
            foreach (var node in statements[index].DescendantNodes(static n =>
                n is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (node is InvocationExpressionSyntax invocation && IsAssertion(invocation))
                {
                    return index < 2 ? null :
                        (statements[0].SpanStart, statements[index - 1].SpanStart, statements[index].SpanStart);
                }
            }
        }

        return null;
    }

    static bool? HasMarkers(Diagnostic diagnostic) =>
        diagnostic.Properties.TryGetValue(ConventionDiagnostics.HasPhaseMarkersProperty, out var value) ? value != "false" : null;

    static void AddPhaseComments(SourceText text, (int Arrange, int Act, int Assert) starts, List<TextChange> changes)
    {
        changes.Add(InsertComment(text, starts.Arrange, TestPhaseComments.Arrange, blankLineBefore: false));
        changes.Add(InsertComment(text, starts.Act, TestPhaseComments.Act, blankLineBefore: true));
        changes.Add(InsertComment(text, starts.Assert, TestPhaseComments.Assert, blankLineBefore: true));
    }

    static TextChange InsertComment(SourceText text, int position, string comment, bool blankLineBefore)
    {
        var line = text.Lines.GetLineFromPosition(position);
        var previousLine = line.LineNumber == 0 ? line : text.Lines[line.LineNumber - 1];
        var lineBreak = line.EndIncludingLineBreak > line.End ? line : previousLine;
        var newLine = lineBreak.EndIncludingLineBreak > lineBreak.End
            ? text.ToString(TextSpan.FromBounds(lineBreak.End, lineBreak.EndIncludingLineBreak))
            : "\n";

        var indentEnd = SkipWhitespace(text, line.Start, position);
        var indent = text.ToString(TextSpan.FromBounds(line.Start, indentEnd));
        if (indentEnd < position)
        {
            // A compact body or a block comment shares this line with the statement.
            return new TextChange(new TextSpan(position, 0),
                newLine + (blankLineBefore ? newLine : "") + indent + comment + newLine + indent);
        }

        // Leave the original statement and its indentation intact.
        var blank = blankLineBefore && SkipWhitespace(text, previousLine.Start, previousLine.End) < previousLine.End ? newLine : "";
        return new TextChange(new TextSpan(line.Start, 0), blank + indent + comment + newLine);
    }

    static int SkipWhitespace(SourceText text, int position, int end)
    {
        while (position < end && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        return position;
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
                    return IsAssertType(identifier.Identifier.ValueText);
                case MemberAccessExpressionSyntax nested:
                    if (IsAssertType(nested.Name.Identifier.ValueText) && nested.Expression.ToString() is
                        "Xunit" or "global::Xunit" or
                        "NUnit.Framework" or "global::NUnit.Framework" or
                        "Microsoft.VisualStudio.TestTools.UnitTesting" or "global::Microsoft.VisualStudio.TestTools.UnitTesting")
                    {
                        return true;
                    }

                    expression = nested.Expression;
                    continue;
                default:
                    return false;
            }
        }
    }

    static bool IsAssertType(string name) => name is "Assert" or "StringAssert" or "CollectionAssert";
}
