using System;
using System.Collections.Generic;

namespace Cntryl.Conventions.Analyzers;

/// <summary>
/// Parses PascalCase test names into their Should/Given/When clauses.
/// </summary>
public static class TestNameGrammar
{
    /// <summary>The required prefix that states the expected outcome.</summary>
    public const string ShouldPrefix = "Should";

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
        if (name is null || word is null)
        {
            return -1;
        }

        var index = name.IndexOf(word, StringComparison.Ordinal);
        while (index >= 0)
        {
            var end = index + word.Length;
            if (index > 0 && (end == name.Length || char.IsUpper(name[end]) || char.IsDigit(name[end])))
            {
                return index;
            }

            index = index + 1 <= name.Length - word.Length
                ? name.IndexOf(word, index + 1, StringComparison.Ordinal)
                : -1;
        }

        return -1;
    }

    /// <summary>
    /// Describes every way <paramref name="name"/> departs from the configured grammar.
    /// An empty list means the name conforms.
    /// </summary>
    public static IReadOnlyList<string> Violations(string name, bool requireGiven, bool requireWhen)
    {
        var problems = new List<string>();
        if (string.IsNullOrEmpty(name))
        {
            problems.Add("must be named Should<Outcome>");
            return problems;
        }

        var hasShould = name.StartsWith(ShouldPrefix, StringComparison.Ordinal);
        if (!hasShould)
        {
            problems.Add("must start with 'Should'");
        }
        else if (name.Length == ShouldPrefix.Length)
        {
            problems.Add("must state an outcome after 'Should'");
        }

        var givenIndex = IndexOfWord(name, GivenKeyword);
        var whenIndex = IndexOfWord(name, WhenKeyword);

        if (requireGiven && givenIndex < 0)
        {
            problems.Add("must state a precondition with 'Given<State>'");
        }

        if (requireWhen && whenIndex < 0)
        {
            problems.Add("must state a trigger with 'When<Action>'");
        }

        if (hasShould && givenIndex == ShouldPrefix.Length)
        {
            problems.Add("must state an outcome between 'Should' and 'Given'");
        }

        if (givenIndex >= 0 && whenIndex >= 0 && whenIndex < givenIndex)
        {
            problems.Add("must order clauses as Should, then Given, then When");
        }

        if (givenIndex >= 0 && givenIndex + GivenKeyword.Length == name.Length)
        {
            problems.Add("must state a precondition after 'Given'");
        }

        if (whenIndex >= 0 && whenIndex + WhenKeyword.Length == name.Length)
        {
            problems.Add("must state an action after 'When'");
        }

        return problems;
    }

    /// <summary>
    /// Derives a <c>Should</c>-prefixed replacement for a third-person test name such as
    /// <c>AcceptsValidRoute</c>, or <see langword="null"/> when no safe rewrite exists.
    /// </summary>
    public static string? TryDeriveShouldName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.StartsWith(ShouldPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var wordEnd = 1;
        while (wordEnd < name.Length && !char.IsUpper(name[wordEnd]))
        {
            wordEnd++;
        }

        var verb = name.Substring(0, wordEnd);
        var remainder = name.Substring(wordEnd);
        var singular = ToBaseVerb(verb);
        return singular is null ? null : ShouldPrefix + singular + remainder;
    }

    static string? ToBaseVerb(string verb)
    {
        if (verb.Length < 3 || !verb.EndsWith("s", StringComparison.Ordinal) || verb.EndsWith("ss", StringComparison.Ordinal))
        {
            return null;
        }

        if (verb.EndsWith("ies", StringComparison.Ordinal))
        {
            return verb.Substring(0, verb.Length - 3) + "y";
        }

        foreach (var ending in new[] { "sses", "shes", "ches", "xes", "zes" })
        {
            if (verb.EndsWith(ending, StringComparison.Ordinal))
            {
                return verb.Substring(0, verb.Length - 2);
            }
        }

        return verb.Substring(0, verb.Length - 1);
    }
}
