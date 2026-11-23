using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Cntryl.Conventions.Analyzers;

/// <summary>Reports control-flow statements that lack surrounding blank lines.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ControlFlowSpacingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [ConventionDiagnostics.ControlFlowSpacing];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeControlFlow,
            SyntaxKind.IfStatement,
            SyntaxKind.ForStatement,
            SyntaxKind.ForEachStatement,
            SyntaxKind.ForEachVariableStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.SwitchStatement,
            SyntaxKind.TryStatement);
    }

    static void AnalyzeControlFlow(SyntaxNodeAnalysisContext context)
    {
        var statement = (StatementSyntax)context.Node;
        if (statement.Parent is not BlockSyntax block)
        {
            return;
        }

        var index = block.Statements.IndexOf(statement);
        if (index < 0)
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        var missingBefore = index > 0
            && !HasBlankLineBefore(text, block.Statements[index - 1].Span, statement.Span);
        var missingAfter = index + 1 < block.Statements.Count
            && !HasBlankLineAfter(text, statement.Span, block.Statements[index + 1].Span);

        if (!missingBefore && !missingAfter)
        {
            return;
        }

        var name = statement switch
        {
            IfStatementSyntax => "if",
            ForStatementSyntax => "for",
            ForEachStatementSyntax => "foreach",
            ForEachVariableStatementSyntax => "foreach",
            WhileStatementSyntax => "while",
            SwitchStatementSyntax => "switch",
            TryStatementSyntax => "try/catch/finally",
            _ => "control-flow"
        };

        context.ReportDiagnostic(Diagnostic.Create(
            ConventionDiagnostics.ControlFlowSpacing,
            statement.GetFirstToken().GetLocation(),
            name));
    }

    static bool HasBlankLineBefore(
        SourceText text,
        TextSpan previous,
        TextSpan statement)
    {
        var previousLine = text.Lines.GetLineFromPosition(previous.End).LineNumber;
        var statementLine = text.Lines.GetLineFromPosition(statement.Start).LineNumber;
        return statementLine - previousLine >= 2;
    }

    static bool HasBlankLineAfter(
        SourceText text,
        TextSpan statement,
        TextSpan next)
    {
        var statementLine = text.Lines.GetLineFromPosition(statement.End).LineNumber;
        var nextLine = text.Lines.GetLineFromPosition(next.Start).LineNumber;
        return nextLine - statementLine >= 2;
    }
}
