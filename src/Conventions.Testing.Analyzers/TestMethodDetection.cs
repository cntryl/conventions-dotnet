using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Cntryl.Conventions.Testing.Analyzers;

/// <summary>
/// Resolves the test-method attributes present in a compilation so the analyzers stay inert
/// in projects that reference no supported test framework.
/// </summary>
sealed class TestMethodDetection
{
    static readonly string[] AttributeMetadataNames =
    [
        "Xunit.FactAttribute",
        "Xunit.TheoryAttribute",
        "NUnit.Framework.TestAttribute",
        "NUnit.Framework.TestCaseAttribute",
        "NUnit.Framework.TestCaseSourceAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute",
    ];

    readonly ImmutableArray<INamedTypeSymbol> _attributes;

    TestMethodDetection(ImmutableArray<INamedTypeSymbol> attributes) => _attributes = attributes;

    /// <summary>True when the compilation references no supported test framework.</summary>
    internal bool IsEmpty => _attributes.IsEmpty;

    internal static TestMethodDetection Create(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        foreach (var metadataName in AttributeMetadataNames)
        {
            var symbol = compilation.GetTypeByMetadataName(metadataName);
            if (symbol is not null)
            {
                builder.Add(symbol);
            }
        }

        return new TestMethodDetection(builder.ToImmutable());
    }

    internal bool IsTestMethod(IMethodSymbol method)
    {
        if (_attributes.IsEmpty || method.MethodKind != MethodKind.Ordinary)
        {
            return false;
        }

        foreach (var attribute in method.GetAttributes())
        {
            if (Matches(attribute.AttributeClass))
            {
                return true;
            }
        }

        return false;
    }

    bool Matches(INamedTypeSymbol? attributeClass)
    {
        for (var current = attributeClass?.OriginalDefinition; current is not null; current = current.BaseType?.OriginalDefinition)
        {
            foreach (var known in _attributes)
            {
                if (SymbolEqualityComparer.Default.Equals(current, known))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
