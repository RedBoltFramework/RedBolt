using System.Collections.Generic;

namespace SharpCpp.Parsing;

public static class SharpCppAttributeScanner
{
    public sealed record AttributeMatch(string Name, string Body, int StartIndex, int EndIndex);

    public static IReadOnlyList<AttributeMatch> Scan(string text)
        => Scan(text.AsSpan());

    public static IReadOnlyList<AttributeMatch> Scan(ReadOnlySpan<char> text)
    {
        var matches = new List<AttributeMatch>();

        for (var index = 0; index < text.Length; index++)
        {
            var cursor = index;
            if (!TryConsumeBracketPair(text, ref cursor, '[', '['))
            {
                continue;
            }

            SkipWhitespace(text, ref cursor);
            if (!TryReadIdentifier(text, ref cursor, out var namespaceName) || !string.Equals(namespaceName, "sharpcpp", StringComparison.Ordinal))
            {
                continue;
            }

            SkipWhitespace(text, ref cursor);
            if (!TryConsume(text, ref cursor, ':') || !TryConsume(text, ref cursor, ':'))
            {
                continue;
            }

            SkipWhitespace(text, ref cursor);
            if (!TryReadIdentifier(text, ref cursor, out var attributeName))
            {
                continue;
            }

            SkipWhitespace(text, ref cursor);
            string body = string.Empty;

            if (TryConsume(text, ref cursor, '('))
            {
                var bodyContentStart = cursor;
                if (!TryReadBalancedParentheses(text, ref cursor, out var bodyContentEnd))
                {
                    continue;
                }

                body = text[bodyContentStart..bodyContentEnd].ToString();
                SkipWhitespace(text, ref cursor);
            }

            if (!TryConsumeBracketPair(text, ref cursor, ']', ']'))
            {
                continue;
            }

            matches.Add(new AttributeMatch(attributeName, body, index, cursor));
            index = cursor - 1;
        }

        return matches;
    }

    private static void SkipWhitespace(ReadOnlySpan<char> text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
    }

    private static bool TryConsume(ReadOnlySpan<char> text, ref int index, char expected)
    {
        if (index >= text.Length || text[index] != expected)
        {
            return false;
        }

        index++;
        return true;
    }

    private static bool TryConsumeBracketPair(ReadOnlySpan<char> text, ref int index, char first, char second)
    {
        var cursor = index;
        if (!TryConsume(text, ref cursor, first))
        {
            return false;
        }

        SkipWhitespace(text, ref cursor);
        if (!TryConsume(text, ref cursor, second))
        {
            return false;
        }

        index = cursor;
        return true;
    }

    private static bool TryReadIdentifier(ReadOnlySpan<char> text, ref int index, out string identifier)
    {
        var start = index;
        while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_'))
        {
            index++;
        }

        identifier = index > start ? text[start..index].ToString() : string.Empty;
        return index > start;
    }

    private static bool TryReadBalancedParentheses(ReadOnlySpan<char> text, ref int index, out int contentEnd)
    {
        var depth = 1;
        var inString = false;
        var escaping = false;

        while (index < text.Length)
        {
            var ch = text[index++];

            if (inString)
            {
                if (escaping)
                {
                    escaping = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaping = true;
                }
                else if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')')
            {
                depth--;
                if (depth == 0)
                {
                    contentEnd = index - 1;
                    return true;
                }
            }
        }

        contentEnd = default;
        return false;
    }
}
