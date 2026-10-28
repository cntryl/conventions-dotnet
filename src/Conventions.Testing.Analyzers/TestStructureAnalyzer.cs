using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cntryl.Conventions.Testing.Analyzers;

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

            // A symbol action rides on attribute binding the compiler performs anyway, whereas a
            // syntax-node action would force a semantic model and per-node binding the build
            // otherwise never needs. The body is reached through the declaring syntax instead.
            compilationStart.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, detection),
                SymbolKind.Method);
        });
    }

    static void Analyze(SymbolAnalysisContext context, TestMethodDetection detection)
    {
        if (context.Symbol is not IMethodSymbol method || !detection.IsTestMethod(method))
        {
            return;
        }

        // Each part of a partial method arrives as its own symbol carrying the merged attributes,
        // so the part without a body simply reports nothing.
        foreach (var reference in method.DeclaringSyntaxReferences)
        {
            // An expression-bodied test has no statement sequence to phase.
            if (reference.GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax { Body: { } body } declaration)
            {
                continue;
            }

            var (arrangeAt, actAt, assertAt) = TestPhaseComments.Locate(body);
            var problems = TestPhaseComments.Violations(arrangeAt, actAt, assertAt);
            if (problems.Count == 0)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                ConventionDiagnostics.TestStructure,
                declaration.Identifier.GetLocation(),
                declaration.Identifier.ValueText,
                string.Join("; and ", problems)));
        }
    }
}
