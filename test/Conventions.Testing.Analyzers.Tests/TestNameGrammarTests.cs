using Cntryl.Conventions.Testing.Analyzers;

namespace Cntryl.Conventions.Testing.Analyzers.Tests;

public sealed class TestNameGrammarTests
{
    [Theory]
    [InlineData("ShouldReturnGivenWhenCalled", "must state a precondition after 'Given'")]
    [InlineData("ShouldWhenCalled", "must state an outcome between 'Should' and 'When'")]
    [InlineData("ShouldReturnWhenGivenInput", "must state an action after 'When'")]
    public void ShouldRequireClauseContentGivenAdjacentKeywords(string name, string expected)
    {
        // Arrange
        const bool requireGiven = false;

        // Act
        var violations = TestNameGrammar.Violations(name, requireGiven, requireWhen: true);

        // Assert
        Assert.Contains(expected, violations);
    }

    [Theory]
    [InlineData("Applies", "Apply")]
    [InlineData("Passes", "Pass")]
    [InlineData("Matches", "Match")]
    [InlineData("Read", null)]
    [InlineData("Pass", null)]
    public void ShouldDeriveOnlySupportedVerbFormsGivenATestName(string verb, string? baseVerb)
    {
        // Arrange
        var name = verb + "ValueGivenInput";
        var expected = baseVerb is null ? null : "Should" + baseVerb + "ValueGivenInput";

        // Act
        var suggestion = TestNameGrammar.TryDeriveShouldName(name);

        // Assert
        Assert.Equal(expected, suggestion);
    }
}
