using System;
using System.Collections.Generic;

namespace Cntryl.Conventions.Testing.Analyzers;

/// <summary>
/// Parses PascalCase test names into their Should/Given/When clauses.
/// </summary>
public static class TestNameGrammar
{
    /// <summary>The required prefix that states the expected outcome.</summary>
    public const string ShouldPrefix = TestVerbInflection.ShouldPrefix;

    /// <summary>The clause keyword that introduces the precondition.</summary>
    public const string GivenKeyword = "Given";

    /// <summary>The clause keyword that introduces the triggering action.</summary>
    public const string WhenKeyword = "When";

    /// <summary>
    /// Finds <paramref name="word"/> where it starts a PascalCase word inside <paramref name="name"/>,
    /// so <c>Given</c> matches <c>ShouldXGivenY</c> but not <c>ShouldForgivenessApply</c>.
    /// </summary>
    public static int IndexOfWord(string name, string word)
    {
        if (string.IsNullOrEmpty(name) || word is null)
        {
            return -1;
        }

        var index = name.IndexOf(word, 1, StringComparison.Ordinal);
        while (index >= 0)
        {
            var end = index + word.Length;
            if (end == name.Length || char.IsUpper(name[end]) || char.IsDigit(name[end]))
            {
                return index;
            }

            index = name.IndexOf(word, index + 1, StringComparison.Ordinal);
        }

        return -1;
    }

    /// <summary>
    /// Describes every way <paramref name="name"/> departs from the configured grammar.
    /// An empty list means the name conforms.
    /// </summary>
    public static IReadOnlyList<string> Violations(string name, bool requireGiven, bool requireWhen)
    {
        if (string.IsNullOrEmpty(name))
        {
            return ["must be named Should<Outcome>"];
        }

        List<string>? problems = null;
        var hasShould = name.StartsWith(ShouldPrefix, StringComparison.Ordinal);
        if (!hasShould)
        {
            (problems ??= []).Add("must start with 'Should'");
        }
        else if (name.Length == ShouldPrefix.Length)
        {
            (problems ??= []).Add("must state an outcome after 'Should'");
        }

        var givenIndex = IndexOfWord(name, GivenKeyword);
        var whenIndex = IndexOfWord(name, WhenKeyword);

        if (requireGiven && givenIndex < 0)
        {
            (problems ??= []).Add("must state a precondition with 'Given<State>'");
        }

        if (requireWhen && whenIndex < 0)
        {
            (problems ??= []).Add("must state a trigger with 'When<Action>'");
        }

        if (hasShould && givenIndex == ShouldPrefix.Length)
        {
            (problems ??= []).Add("must state an outcome between 'Should' and 'Given'");
        }
        else if (hasShould && whenIndex == ShouldPrefix.Length)
        {
            (problems ??= []).Add("must state an outcome between 'Should' and 'When'");
        }

        if (givenIndex >= 0 && whenIndex >= 0 && whenIndex < givenIndex)
        {
            (problems ??= []).Add("must order clauses as Should, then Given, then When");
        }

        if (givenIndex >= 0 && (givenIndex + GivenKeyword.Length == name.Length || givenIndex + GivenKeyword.Length == whenIndex))
        {
            (problems ??= []).Add("must state a precondition after 'Given'");
        }

        if (whenIndex >= 0 && (whenIndex + WhenKeyword.Length == name.Length || whenIndex + WhenKeyword.Length == givenIndex))
        {
            (problems ??= []).Add("must state an action after 'When'");
        }

        return problems is null ? Array.Empty<string>() : problems;
    }

    /// <summary>
    /// Derives a <c>Should</c>-prefixed replacement for a third-person test name such as
    /// <c>AcceptsValidRoute</c>, or <see langword="null"/> when no safe rewrite exists.
    /// </summary>
    public static string? TryDeriveShouldName(string name) => TestVerbInflection.TryDeriveShouldName(name);
}
