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

/// <summary>Reformats declaration parameter lists atomically.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DeclarationWrappingCodeFixProvider)), Shared]
public sealed class DeclarationWrappingCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [ConventionDiagnostics.DeclarationWrappingId];

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not ParameterListSyntax parameterList)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Wrap declaration parameters atomically",
                    cancellationToken => FixAsync(context.Document, parameterList, cancellationToken),
                    nameof(DeclarationWrappingCodeFixProvider)),
                diagnostic);
        }
    }

    static async Task<Document> FixAsync(
        Document document,
        ParameterListSyntax parameterList,
        CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var line = text.Lines.GetLineFromPosition(parameterList.OpenParenToken.SpanStart);
        var indent = GetIndent(text, line.Start);
        var start = parameterList.OpenParenToken.Span.End;
        var end = parameterList.CloseParenToken.SpanStart;
        var parameters = parameterList.Parameters;
        var replacement = new System.Text.StringBuilder();
        var newLine = GetNewLine(text);
        var parameterIndent = indent + "    ";
        replacement.Append(newLine);
        for (var index = 0; index < parameters.Count; index++)
        {
            replacement.Append(parameterIndent);
            replacement.Append(parameters[index].ToFullString().Trim());
            if (index + 1 < parameters.Count)
            {
                replacement.Append(',');
            }

            replacement.Append(newLine);
        }

        replacement.Append(indent);
        var changedText = text.WithChanges(
            new TextChange(TextSpan.FromBounds(start, end), replacement.ToString()));
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

    static string GetIndent(SourceText text, int position)
    {
        var line = text.Lines.GetLineFromPosition(position);
        var chars = line.ToString();
        var length = 0;
        while (length < chars.Length && chars[length] is ' ' or '\t')
        {
            length++;
        }

        return length == 0 ? string.Empty : chars.Substring(0, length);
    }
}
