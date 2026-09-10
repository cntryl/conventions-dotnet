using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cntryl.Conventions.Analyzers;

/// <summary>
/// Reports test methods whose bodies do not carry ordered Arrange/Act/Assert comments.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestStructureAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [ConventionDiagnostics.TestStructure];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationStart =>
        {
            var detection = TestMethodDetection.Create(compilationStart.Compilation);
            if (detection.IsEmpty)
            {
                return;
            }

            compilationStart.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, detection),
                SyntaxKind.MethodDeclaration);
        });
    }

    static void Analyze(SyntaxNodeAnalysisContext context, TestMethodDetection detection)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;

        // An expression-bodied test has no statement sequence to phase.
        if (declaration.Body is not { } body)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } method ||
            !detection.IsTestMethod(method))
        {
            return;
        }

        var (arrangeAt, actAt, assertAt) = TestPhaseComments.Locate(body);
        var problems = TestPhaseComments.Violations(arrangeAt, actAt, assertAt);
        if (problems.Count == 0)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ConventionDiagnostics.TestStructure,
            declaration.Identifier.GetLocation(),
            declaration.Identifier.ValueText,
            string.Join("; and ", problems)));
    }
}
