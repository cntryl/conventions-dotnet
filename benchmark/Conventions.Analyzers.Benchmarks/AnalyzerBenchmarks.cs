using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text;
using BenchmarkDotNet.Attributes;
using Cntryl.Conventions.Testing.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cntryl.Conventions.Analyzers.Benchmarks;

[MemoryDiagnoser]
[InProcess]
public class AnalyzerBenchmarks
{
    static readonly MetadataReference[] RuntimeReferences =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
    ];

    static readonly ImmutableArray<DiagnosticAnalyzer> GeneralAnalyzers =
    [
        new DeclarationWrappingAnalyzer(),
        new ControlFlowSpacingAnalyzer(),
    ];

    static readonly ImmutableArray<DiagnosticAnalyzer> TestAnalyzers =
    [
        new TestNamingAnalyzer(),
        new TestStructureAnalyzer(),
    ];

    Compilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        const int testCount = 500;
        var source = new StringBuilder("""
            namespace Xunit
            {
                public sealed class FactAttribute : System.Attribute { }
            }

            class Sample
            {
            """);

        for (var index = 0; index < testCount; index++)
        {
            var method = $$"""

                [Xunit.Fact]
            void ShouldPassGivenReadyWhenExecuted{{index.ToString(CultureInfo.InvariantCulture)}}(
                    int first,
                    int second
                )
                {
                    var value = first + second;

                    if (value > 0)
                    {
                        value++;
                    }

                    // Arrange
                    var expected = value;
                    // Act
                    value++;
                    // Assert
                    Xunit.Assert.Equal(expected, value - 1);
                }
            """;
            source.AppendLine(method);
        }

        source.AppendLine("}");
        var tree = CSharpSyntaxTree.ParseText(source.ToString());
        var assertionStub = CSharpSyntaxTree.ParseText("""
            namespace Xunit
            {
                public static class Assert
                {
                    public static void Equal(int expected, int actual) { }
                }
            }
            """);
        _compilation = CSharpCompilation.Create(
            "Benchmark",
            [tree, assertionStub],
            RuntimeReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Benchmark]
    public Task<ImmutableArray<Diagnostic>> General() =>
        RunAsync(GeneralAnalyzers);

    [Benchmark]
    public Task<ImmutableArray<Diagnostic>> Testing() =>
        RunAsync(TestAnalyzers);

    [Benchmark]
    public Task<ImmutableArray<Diagnostic>> All() =>
        RunAsync(GeneralAnalyzers.AddRange(TestAnalyzers));

    async Task<ImmutableArray<Diagnostic>> RunAsync(ImmutableArray<DiagnosticAnalyzer> analyzers)
    {
        var result = await _compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        return result;
    }
}
