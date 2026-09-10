using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cntryl.Conventions.Analyzers;

/// <summary>
/// Locates the Arrange/Act/Assert phase markers inside a test body.
/// </summary>
public static class TestPhaseComments
{
    /// <summary>The comment that opens the arrange phase.</summary>
    public const string Arrange = "// Arrange";

    /// <summary>The comment that opens the act phase.</summary>
    public const string Act = "// Act";

    /// <summary>The comment that opens the assert phase.</summary>
    public const string Assert = "// Assert";

    /// <summary>
    /// Returns the source position of each phase marker in <paramref name="body"/>, or -1 when absent.
    /// A marker may carry trailing prose, so <c>// Arrange: two subscribers</c> counts as arrange.
    /// </summary>
    public static (int ArrangeAt, int ActAt, int AssertAt) Locate(BlockSyntax body)
    {
        if (body is null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        var arrangeAt = -1;
        var actAt = -1;
        var assertAt = -1;

        foreach (var trivia in body.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                continue;
            }

            var text = trivia.ToString().Trim();
            var position = trivia.SpanStart;

            // Assert is tested before Act because "// Assert" also starts with "// As".
            if (assertAt < 0 && Matches(text, Assert))
            {
                assertAt = position;
            }
            else if (arrangeAt < 0 && Matches(text, Arrange))
            {
                arrangeAt = position;
            }
            else if (actAt < 0 && Matches(text, Act))
            {
                actAt = position;
            }
        }

        return (arrangeAt, actAt, assertAt);
    }

    /// <summary>
    /// Describes every way the located markers depart from an ordered Arrange/Act/Assert body.
    /// An empty list means the body conforms.
    /// </summary>
    public static IReadOnlyList<string> Violations(int arrangeAt, int actAt, int assertAt)
    {
        var problems = new List<string>();
        var missing = new List<string>();

        if (arrangeAt < 0)
        {
            missing.Add("'// Arrange'");
        }

        if (actAt < 0)
        {
            missing.Add("'// Act'");
        }

        if (assertAt < 0)
        {
            missing.Add("'// Assert'");
        }

        if (missing.Count > 0)
        {
            problems.Add("is missing " + string.Join(", ", missing));
        }

        if (arrangeAt >= 0 && actAt >= 0 && actAt < arrangeAt)
        {
            problems.Add("declares '// Act' before '// Arrange'");
        }

        if (actAt >= 0 && assertAt >= 0 && assertAt < actAt)
        {
            problems.Add("declares '// Assert' before '// Act'");
        }

        return problems;
    }

    static bool Matches(string text, string marker)
    {
        if (!text.StartsWith(marker, StringComparison.Ordinal))
        {
            return false;
        }

        if (text.Length == marker.Length)
        {
            return true;
        }

        var next = text[marker.Length];
        return !char.IsLetterOrDigit(next);
    }
}
