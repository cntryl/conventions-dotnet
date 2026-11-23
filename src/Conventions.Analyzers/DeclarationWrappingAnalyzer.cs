using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Cntryl.Conventions.Analyzers;

/// <summary>Reports declaration parameter lists that are wrapped inconsistently.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeclarationWrappingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [ConventionDiagnostics.DeclarationWrapping];

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
            AnalyzeParameterList,
            SyntaxKind.ParameterList);
    }

    static void AnalyzeParameterList(SyntaxNodeAnalysisContext context)
    {
        var parameterList = (ParameterListSyntax)context.Node;
        if (parameterList.Parameters.Count < 2)
        {
            return;
        }

        var parent = parameterList.Parent;
        if (parent is not (MethodDeclarationSyntax or ConstructorDeclarationSyntax or LocalFunctionStatementSyntax or
            DelegateDeclarationSyntax))
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        var openLine = text.Lines.GetLineFromPosition(parameterList.OpenParenToken.SpanStart).LineNumber;
        var closeLine = text.Lines.GetLineFromPosition(parameterList.CloseParenToken.SpanStart).LineNumber;
        var firstLine = text.Lines.GetLineFromPosition(parameterList.Parameters[0].SpanStart).LineNumber;
        var lastLine = text.Lines.GetLineFromPosition(
            parameterList.Parameters[parameterList.Parameters.Count - 1].Span.End).LineNumber;

        if (openLine == closeLine)
        {
            return;
        }

        var isAtomic = openLine < firstLine
            && closeLine > lastLine
            && ParameterStartsAreDistinct(text, parameterList);

        if (isAtomic)
        {
            return;
        }

        var name = parent switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
            LocalFunctionStatementSyntax local => local.Identifier.ValueText,
            DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.Identifier.ValueText,
            _ => "declaration"
        };

        context.ReportDiagnostic(Diagnostic.Create(
            ConventionDiagnostics.DeclarationWrapping,
            parameterList.GetLocation(),
            name));
    }

    static bool ParameterStartsAreDistinct(SourceText text, ParameterListSyntax parameterList)
    {
        var previousLine = -1;
        foreach (var parameter in parameterList.Parameters)
        {
            var line = text.Lines.GetLineFromPosition(parameter.SpanStart).LineNumber;
            if (line == previousLine)
            {
                return false;
            }

            previousLine = line;
        }

        return true;
    }
}
