using System;

namespace Cntryl.Conventions.Testing;

static class TestVerbInflection
{
    internal const string ShouldPrefix = "Should";

    // Each special ending has a distinct base suffix. The only other spelling that
    // can produce the same verb is the regular base + "s" form.
    static readonly (string Base, string ThirdPerson)[] Endings =
        [("y", "ies"), ("ss", "sses"), ("sh", "shes"), ("ch", "ches"), ("x", "xes"), ("z", "zes")];

    internal static string? TryDeriveShouldName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.StartsWith(ShouldPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var end = WordEnd(name, 0);
        if (end < 3 || name[end - 1] != 's' || name[end - 2] == 's')
        {
            return null;
        }

        var verb = name.AsSpan(0, end);
        foreach (var (baseEnding, thirdPerson) in Endings)
        {
            if (verb.EndsWith(thirdPerson.AsSpan(), StringComparison.Ordinal))
            {
                return ShouldPrefix + name.Substring(0, end - thirdPerson.Length) + baseEnding + name.Substring(end);
            }
        }

        return ShouldPrefix + name.Substring(0, end - 1) + name.Substring(end);
    }

    internal static string? GetCompetingSpelling(string name, string suggestion)
    {
        var start = ShouldPrefix.Length;
        if (suggestion.Length <= start || !suggestion.StartsWith(ShouldPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var end = WordEnd(suggestion, start);
        var verb = suggestion.AsSpan(start, end - start);
        foreach (var (baseEnding, thirdPerson) in Endings)
        {
            if (verb.EndsWith(baseEnding.AsSpan(), StringComparison.Ordinal))
            {
                var originalVerb = name.AsSpan(0, WordEnd(name, 0));
                return originalVerb.EndsWith(thirdPerson.AsSpan(), StringComparison.Ordinal)
                    ? suggestion.Substring(start, verb.Length) + "s" + suggestion.Substring(end)
                    : suggestion.Substring(start, verb.Length - baseEnding.Length) + thirdPerson + suggestion.Substring(end);
            }
        }

        return null;
    }

    static int WordEnd(string name, int start)
    {
        var end = start + 1;
        while (end < name.Length && !char.IsUpper(name[end]))
        {
            end++;
        }

        return end;
    }
}
