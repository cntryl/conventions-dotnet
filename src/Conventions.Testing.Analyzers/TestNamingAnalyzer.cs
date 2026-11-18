using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cntryl.Conventions.Testing.Analyzers;

/// <summary>
/// Reports test methods whose names do not read as Should&lt;Outcome&gt;Given&lt;State&gt;When&lt;Action&gt;.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestNamingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Option key controlling whether a <c>Given</c> clause is required. Defaults to true.</summary>
    public const string RequireGivenOption = "conventions_test_naming_require_given";

    /// <summary>Option key controlling whether a <c>When</c> clause is required. Defaults to false.</summary>
    public const string RequireWhenOption = "conventions_test_naming_require_when";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [ConventionDiagnostics.TestNaming];

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

            var provider = compilationStart.Options.AnalyzerConfigOptionsProvider;
            var optionsByTree = new ConcurrentDictionary<SyntaxTree, (bool RequireGiven, bool RequireWhen)>();
            Func<SyntaxTree, (bool, bool)> readOptions = tree =>
            {
                var options = provider.GetOptions(tree);
                return (ReadFlag(options, RequireGivenOption, true), ReadFlag(options, RequireWhenOption, false));
            };
            compilationStart.RegisterSymbolAction(context =>
            {
                var method = (IMethodSymbol)context.Symbol;
                // The defining declaration owns the name and its file's naming options.
                if (!detection.IsTestMethod(method) || method.PartialDefinitionPart is not null)
                {
                    return;
                }

                var location = method.Locations[0];
                var (requireGiven, requireWhen) = optionsByTree.GetOrAdd(location.SourceTree!, readOptions);
                Analyze(context, method.Name, location, requireGiven, requireWhen);
            }, SymbolKind.Method);
        });
    }

    static void Analyze(SymbolAnalysisContext context, string name, Location location, bool requireGiven, bool requireWhen)
    {
        var problems = TestNameGrammar.Violations(name, requireGiven, requireWhen);
        if (problems.Count == 0)
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty;
        var suggested = problems.Count == 1 ? TestNameGrammar.TryDeriveShouldName(name) : null;
        if (suggested is not null && TestNameGrammar.Violations(suggested, requireGiven, requireWhen).Count == 0)
        {
            properties = properties.Add(ConventionDiagnostics.SuggestedNameProperty, suggested);
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ConventionDiagnostics.TestNaming,
            location,
            properties,
            name,
            problems.Count == 1 ? problems[0] : string.Join("; and ", problems)));
    }

    static bool ReadFlag(AnalyzerConfigOptions options, string key, bool defaultValue)
    {
        if (options.TryGetValue(key, out var raw) && bool.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }
}
