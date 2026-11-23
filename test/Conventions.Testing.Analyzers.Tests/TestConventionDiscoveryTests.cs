using Cntryl.Conventions.Testing.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Cntryl.Conventions.Testing.Analyzers.Tests;

public sealed class TestConventionDiscoveryTests
{
    [Theory]
    [InlineData("Xunit", "Fact")]
    [InlineData("Xunit", "Theory")]
    [InlineData("NUnit.Framework", "Test")]
    [InlineData("NUnit.Framework", "TestCase")]
    [InlineData("NUnit.Framework", "TestCaseSource")]
    [InlineData("Microsoft.VisualStudio.TestTools.UnitTesting", "TestMethod")]
    [InlineData("Microsoft.VisualStudio.TestTools.UnitTesting", "DataTestMethod")]
    public async Task ShouldFindBothViolationsGivenAnAliasedDerivedTestAttribute(string framework, string attribute)
    {
        // Arrange
        var source = $$"""
            using Spec = Suite.CustomAttribute;
            namespace {{framework}} { public class {{attribute}}Attribute : System.Attribute { } }
            namespace Suite
            {
                class CustomAttribute : {{framework}}.{{attribute}}Attribute { }
                class Sample
                {
                    [Spec] public void Broken() { }
                    [System.Obsolete] public void Helper() { }
                }
            }
            """;

        // Act
        var naming = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestNamingAnalyzer(), includeXunit: false);
        var structure = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestStructureAnalyzer(), includeXunit: false);

        // Assert
        Assert.Equal(ConventionDiagnostics.TestNamingId, Assert.Single(naming).Id);
        Assert.Equal(ConventionDiagnostics.TestStructureId, Assert.Single(structure).Id);
    }

    [Fact]
    public async Task ShouldIgnoreLookalikeAttributesGivenNoTestFramework()
    {
        // Arrange
        const string source = """
            class FactAttribute : System.Attribute { }
            class Sample { [Fact] public void Broken() { } }
            """;

        // Act
        var naming = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestNamingAnalyzer(), includeXunit: false);
        var structure = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestStructureAnalyzer(), includeXunit: false);

        // Assert
        Assert.Empty(naming);
        Assert.Empty(structure);
    }

    [Fact]
    public async Task ShouldFindTestsGivenAnExternAliasedFramework()
    {
        // Arrange
        const string source = """
            extern alias framework;
            class Sample { [framework::Xunit.Fact] public void Broken() { } }
            """;
        using var workspace = AnalyzerHarness.CreateWorkspace(source, out var document, includeXunit: false);
        var framework = document.Project.Solution.AddProject("Framework", "Framework", LanguageNames.CSharp)
            .WithCompilationOptions(document.Project.CompilationOptions!)
            .WithMetadataReferences(document.Project.MetadataReferences)
            .AddDocument("Fact.cs", "namespace Xunit { public class FactAttribute : System.Attribute { } }").Project;
        var project = framework.Solution.GetProject(document.Project.Id)!
            .AddProjectReference(new ProjectReference(framework.Id, aliases: ["framework"]));

        // Act
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(project.GetDocument(document.Id)!, new TestNamingAnalyzer());

        // Assert
        Assert.Equal(ConventionDiagnostics.TestNamingId, Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task ShouldReportOneNameGivenAPartialTestMethod()
    {
        // Arrange
        const string source = """
            partial class Sample
            {
                [Xunit.Fact] partial void Broken();
                partial void Broken() { }
            }
            """;

        // Act
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(source, new TestNamingAnalyzer());

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ConventionDiagnostics.TestNamingId, diagnostic.Id);
        Assert.Equal(source.IndexOf("Broken", StringComparison.Ordinal), diagnostic.Location.SourceSpan.Start);
    }

    [Fact]
    public async Task ShouldApplyNamingOptionsPerFileGivenDifferentEditorConfigSections()
    {
        // Arrange
        const string source = """
            using Xunit;
            class Sample { [Fact] public void ShouldAccept() { } }
            """;
        using var workspace = AnalyzerHarness.CreateWorkspace(source, out var document);
        var project = document.Project.AddDocument("Other.cs", source.Replace("Sample", "Other", StringComparison.Ordinal),
            filePath: Path.GetFullPath("Other.cs")).Project;
        var config = project.AddAnalyzerConfigDocument(".editorconfig", SourceText.From("""
            root = true
            [Tests.cs]
            conventions_test_naming_require_given = false
            [Other.cs]
            conventions_test_naming_require_given = invalid
            conventions_test_naming_require_when = true
            """), filePath: Path.GetFullPath(".editorconfig"));

        // Act
        var diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(config.Project.GetDocument(document.Id)!, new TestNamingAnalyzer());

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Other.cs", Path.GetFileName(diagnostic.Location.SourceTree!.FilePath));
        Assert.Contains("must state a precondition", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("must state a trigger", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.False(diagnostic.Properties.ContainsKey(ConventionDiagnostics.SuggestedNameProperty));
    }
}
