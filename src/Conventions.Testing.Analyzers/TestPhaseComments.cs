using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Cntryl.Conventions.Testing.Analyzers;

/// <summary>
/// Locates the Arrange/Act/Assert phase markers inside a test body.
/// </summary>
public static class TestPhaseComments
{
    static readonly IReadOnlyList<string> AllMissing = Array.AsReadOnly(
        new[] { "is missing '// Arrange', '// Act', '// Assert'" });

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
    public static (int ArrangeAt, int ActAt, int AssertAt) Locate(BlockSyntax body) =>
        Scan(body, CancellationToken.None);

    /// <inheritdoc cref="Locate(BlockSyntax)"/>
    internal static (int ArrangeAt, int ActAt, int AssertAt) Scan(BlockSyntax body, CancellationToken cancellationToken)
    {
        if (body is null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var markers = (ArrangeAt: -1, ActAt: -1, AssertAt: -1);
        // Small methods are cheaper to search as text. Confirm every candidate against Roslyn
        // so strings, disabled code, and other trivia cannot masquerade as phase comments.
        var span = body.Span;
        var triviaSpan = span;
        var text = body.SyntaxTree.GetText(cancellationToken);
        if (span.Length <= 4096)
        {
            var buffer = ArrayPool<char>.Shared.Rent(span.Length);
            try
            {
                text.CopyTo(span.Start, buffer, 0, span.Length);
                var start = 0;
                while (true)
                {
                    var index = buffer.AsSpan(start, span.Length - start).IndexOf("// A".AsSpan());
                    if (index < 0)
                    {
                        return markers;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    var position = span.Start + start + index;
                    var trivia = body.FindTrivia(position);
                    if (trivia.SpanStart != position || !trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
                    {
                        // One literal or disabled block can contain many apparent markers.
                        // Let Roslyn skip it as a unit instead of resolving each match.
                        triviaSpan = TextSpan.FromBounds(position, span.End);
                        break;
                    }

                    if (RecordMarker(trivia, text, ref markers))
                    {
                        return markers;
                    }

                    start = trivia.Span.End - span.Start;
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        // A large literal contributes only a single token to the syntax walk.
        foreach (var trivia in body.DescendantTrivia(triviaSpan))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && RecordMarker(trivia, text, ref markers))
            {
                break;
            }
        }

        return markers;
    }

    static bool RecordMarker(
        SyntaxTrivia trivia,
        SourceText text,
        ref (int ArrangeAt, int ActAt, int AssertAt) markers)
    {
        if (markers.ArrangeAt < 0 && Matches(text, trivia.Span, Arrange))
        {
            markers.ArrangeAt = trivia.SpanStart;
        }
        else if (markers.ActAt < 0 && Matches(text, trivia.Span, Act))
        {
            markers.ActAt = trivia.SpanStart;
        }
        else if (markers.AssertAt < 0 && Matches(text, trivia.Span, Assert))
        {
            markers.AssertAt = trivia.SpanStart;
        }

        return markers.ArrangeAt >= 0 && markers.ActAt >= 0 && markers.AssertAt >= 0;
    }

    /// <summary>
    /// Describes every way the located markers depart from an ordered Arrange/Act/Assert body.
    /// An empty list means the body conforms.
    /// </summary>
    public static IReadOnlyList<string> Violations(int arrangeAt, int actAt, int assertAt)
    {
        if (arrangeAt >= 0 && arrangeAt <= actAt && actAt <= assertAt)
        {
            return Array.Empty<string>();
        }

        if (arrangeAt < 0 && actAt < 0 && assertAt < 0)
        {
            return AllMissing;
        }

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

    static bool Matches(SourceText text, TextSpan span, string marker)
    {
        if (span.Length < marker.Length)
        {
            return false;
        }

        for (var index = 0; index < marker.Length; index++)
        {
            if (text[span.Start + index] != marker[index])
            {
                return false;
            }
        }

        if (span.Length == marker.Length)
        {
            return true;
        }

        var next = text[span.Start + marker.Length];
        return !char.IsLetterOrDigit(next);
    }
}
