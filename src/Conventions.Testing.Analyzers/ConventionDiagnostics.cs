using Microsoft.CodeAnalysis;

namespace Cntryl.Conventions.Testing.Analyzers;

/// <summary>
/// Diagnostic descriptors reported by the convention analyzers.
/// </summary>
public static class ConventionDiagnostics
{
    /// <summary>Diagnostic id for Should/Given/When test naming.</summary>
    public const string TestNamingId = "CNTRYL0001";

    /// <summary>Diagnostic id for ordered Arrange/Act/Assert test structure.</summary>
    public const string TestStructureId = "CNTRYL0002";

    /// <summary>Diagnostic property carrying a mechanically derived replacement name.</summary>
    public const string SuggestedNameProperty = "SuggestedName";

    internal static readonly DiagnosticDescriptor TestNaming = new(
        TestNamingId,
        "Test name does not follow Should/Given/When",
        "Test method '{0}' {1}",
        "Naming",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A test name reads as a specification: Should<Outcome> states the expected behaviour, Given<State> the precondition, and When<Action> the trigger.");

    internal static readonly DiagnosticDescriptor TestStructure = new(
        TestStructureId,
        "Test body is missing ordered Arrange/Act/Assert comments",
        "Test method '{0}' {1}",
        "Style",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Arrange, Act and Assert comments mark the three phases of a test so a reader can locate the single action under test.");
}
