using Microsoft.CodeAnalysis;

namespace Cntryl.Conventions.Analyzers;

/// <summary>Diagnostic descriptors reported by the general convention analyzers.</summary>
public static class ConventionDiagnostics
{
    /// <summary>Diagnostic id for partially wrapped declaration parameter lists.</summary>
    public const string DeclarationWrappingId = "CNTRYL1001";

    /// <summary>Diagnostic id for missing whitespace around control-flow blocks.</summary>
    public const string ControlFlowSpacingId = "CNTRYL1002";

    internal static readonly DiagnosticDescriptor DeclarationWrapping = new(
        DeclarationWrappingId,
        "Declaration parameter list is partially wrapped",
        "Declaration '{0}' has a partially wrapped parameter list",
        "Style",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When a declaration parameter list wraps, place every parameter on its own line and place the closing parenthesis on its own line.");

    internal static readonly DiagnosticDescriptor ControlFlowSpacing = new(
        ControlFlowSpacingId,
        "Control-flow block lacks surrounding blank lines",
        "Control-flow block '{0}' must have a blank line before and after",
        "Style",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Control-flow blocks such as if, for, foreach, while, switch and try are separated from surrounding statements by blank lines.");
}
