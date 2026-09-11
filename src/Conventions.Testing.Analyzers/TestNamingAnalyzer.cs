using System;
using System.Collections.Generic;
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

        var location = method.Locations.Length > 0 ? method.Locations[0] : Location.None;
        var options = location.SourceTree is null
            ? null
            : context.Options.AnalyzerConfigOptionsProvider.GetOptions(location.SourceTree);

        var requireGiven = ReadFlag(options, RequireGivenOption, defaultValue: true);
        var requireWhen = ReadFlag(options, RequireWhenOption, defaultValue: false);

        var problems = TestNameGrammar.Violations(method.Name, requireGiven, requireWhen);
        if (problems.Count == 0)
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty;
        var suggested = TestNameGrammar.TryDeriveShouldName(method.Name);
        if (suggested is not null && TestNameGrammar.Violations(suggested, requireGiven, requireWhen).Count == 0)
        {
            properties = properties.Add(ConventionDiagnostics.SuggestedNameProperty, suggested);
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ConventionDiagnostics.TestNaming,
            location,
            properties,
            method.Name,
            Describe(problems)));
    }

    static string Describe(IReadOnlyList<string> problems)
    {
        if (problems.Count == 1)
        {
            return problems[0];
        }

        var parts = new string[problems.Count];
        for (var i = 0; i < problems.Count; i++)
        {
            parts[i] = problems[i];
        }

        return string.Join("; and ", parts);
    }

    static bool ReadFlag(AnalyzerConfigOptions? options, string key, bool defaultValue)
    {
        if (options is not null && options.TryGetValue(key, out var raw) && bool.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }
}
