using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Cntryl.Conventions.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Cntryl.Conventions.CodeFixes;

/// <summary>Adds blank lines around control-flow statements.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ControlFlowSpacingCodeFixProvider)), Shared]
public sealed class ControlFlowSpacingCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [ConventionDiagnostics.ControlFlowSpacingId];

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.AncestorsAndSelf()
                .OfType<StatementSyntax>().FirstOrDefault() is not { } statement)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add surrounding blank lines",
                    cancellationToken => FixAsync(context.Document, statement, cancellationToken),
                    nameof(ControlFlowSpacingCodeFixProvider)),
                diagnostic);
        }
    }

    static async Task<Document> FixAsync(
        Document document,
        StatementSyntax statement,
        CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var statementStartLine = text.Lines.GetLineFromPosition(statement.SpanStart);
        var statementStart = statementStartLine.Start;

        var insertions = new System.Collections.Generic.Dictionary<int, int>();
        if (statement.Parent is BlockSyntax block)
        {
            var index = block.Statements.IndexOf(statement);
            if (index > 0)
            {
                var boundary = statementStart;
                var previousLine = text.Lines.GetLineFromPosition(block.Statements[index - 1].Span.End).LineNumber;
                var statementLine = text.Lines.GetLineFromPosition(statementStart).LineNumber;
                insertions[boundary] = previousLine == statementLine ? 2 : 1;
            }

            if (index + 1 < block.Statements.Count)
            {
                var nextStart = text.Lines.GetLineFromPosition(
                    block.Statements[index + 1].SpanStart).Start;
                var statementLine = text.Lines.GetLineFromPosition(statement.Span.End).LineNumber;
                var nextLine = text.Lines.GetLineFromPosition(nextStart).LineNumber;
                insertions[nextStart] = statementLine == nextLine ? 2 : 1;
            }
        }

        if (insertions.Count == 0)
        {
            return document;
        }

        var changes = new System.Collections.Generic.List<TextChange>(insertions.Count);
        var newLine = GetNewLine(text);
        foreach (var insertion in insertions)
        {
            changes.Add(new TextChange(
                TextSpan.FromBounds(insertion.Key, insertion.Key),
                newLine + (insertion.Value == 2 ? newLine : string.Empty)));
        }

        var changedText = text.WithChanges(changes);
        return document.WithText(changedText);
    }

    static string GetNewLine(SourceText text)
    {
        if (text.Lines.Count > 1)
        {
            var first = text.Lines[0];
            var length = first.EndIncludingLineBreak - first.End;
            if (length > 0)
            {
                return text.ToString(TextSpan.FromBounds(first.End, first.EndIncludingLineBreak));
            }
        }

        return "\n";
    }
}
