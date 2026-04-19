using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ivren.Core.Internal.Pdf;

internal static class PdfTextTokenExtractor
{
    private static readonly Regex LiteralTokenRegex = new(
        @"\((?<value>(?:\\.|[^\\)])*)\)\s*(?:Tj|'|"")",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ArrayTokenRegex = new(
        @"\[(?<value>.*?)\]\s*TJ",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LiteralItemRegex = new(
        @"\((?<value>(?:\\.|[^\\)])*)\)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HexItemRegex = new(
        @"<(?<value>[0-9A-Fa-f]+)>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static IReadOnlyList<string> ExtractTokens(string decodedStream)
    {
        var tokens = new List<string>();

        foreach (Match match in LiteralTokenRegex.Matches(decodedStream))
        {
            AddToken(tokens, DecodePdfLiteralString(match.Groups["value"].Value));
        }

        foreach (Match match in ArrayTokenRegex.Matches(decodedStream))
        {
            foreach (Match literal in LiteralItemRegex.Matches(match.Groups["value"].Value))
            {
                AddToken(tokens, DecodePdfLiteralString(literal.Groups["value"].Value));
            }

            foreach (Match hex in HexItemRegex.Matches(match.Groups["value"].Value))
            {
                AddToken(tokens, DecodePdfHexString(hex.Groups["value"].Value));
            }
        }

        return tokens;
    }

    public static string DecodePdfLiteralString(string value)
    {
        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current != '\\')
            {
                builder.Append(current);
                continue;
            }

            if (index == value.Length - 1)
            {
                break;
            }

            index++;
            var escaped = value[index];

            switch (escaped)
            {
                case 'n':
                    builder.Append('\n');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case 'b':
                    builder.Append('\b');
                    break;
                case 'f':
                    builder.Append('\f');
                    break;
                case '(':
                case ')':
                case '\\':
                    builder.Append(escaped);
                    break;
                case '\r':
                    if (index + 1 < value.Length && value[index + 1] == '\n')
                    {
                        index++;
                    }
                    break;
                case '\n':
                    break;
                default:
                    if (escaped is >= '0' and <= '7')
                    {
                        var octal = new StringBuilder().Append(escaped);
                        for (var octalIndex = 0; octalIndex < 2 && index + 1 < value.Length && value[index + 1] is >= '0' and <= '7'; octalIndex++)
                        {
                            index++;
                            octal.Append(value[index]);
                        }

                        var octalValue = Convert.ToInt32(octal.ToString(), 8);
                        builder.Append((char)octalValue);
                    }
                    else
                    {
                        builder.Append(escaped);
                    }
                    break;
            }
        }

        return builder.ToString();
    }

    public static string DecodePdfHexString(string value)
    {
        if (value.Length % 2 != 0)
        {
            value += "0";
        }

        var bytes = new byte[value.Length / 2];
        for (var index = 0; index < value.Length; index += 2)
        {
            bytes[index / 2] = byte.Parse(value.Substring(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return Encoding.Latin1.GetString(bytes);
    }

    public static IReadOnlyList<string> ExtractArraySegments(string value)
    {
        var tokens = new List<string>();

        foreach (Match literal in LiteralItemRegex.Matches(value))
        {
            AddToken(tokens, DecodePdfLiteralString(literal.Groups["value"].Value));
        }

        foreach (Match hex in HexItemRegex.Matches(value))
        {
            AddToken(tokens, DecodePdfHexString(hex.Groups["value"].Value));
        }

        return tokens;
    }

    public static void AddToken(ICollection<string> tokens, string value)
    {
        var normalized = value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        tokens.Add(normalized);
    }
}
