using System.Text;
using System.Text.RegularExpressions;
using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.Core.Services;

public sealed class PdfTextExtractionService : ITextExtractionService
{
    private static readonly (Regex Pattern, string Replacement)[] HungarianFragmentRepairs =
    [
        (new Regex(@"\bbanksz\s+mlasz\s+ma\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase), "bankszamlaszama"),
        (new Regex(@"\bszámlasorszáma\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase), "Számla sorszáma"),
        (new Regex(@"\bszamlasorszama\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase), "szamla sorszama"),
        (new Regex(@"\bad\s+sz\s+ma\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase), "adoszama"),
        (new Regex(@"\bsz\s+mla\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase), "szamla"),
        (new Regex(@"\bsorsz\s+ma\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase), "sorszama"),
        (new Regex(@"\bsz\s+ma\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase), "szama"),
        (new Regex(@"\belsz\s+mol\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase), "elszamol")
    ];

    public TextExtractionResult Extract(PdfAnalysisResult analysisResult)
    {
        ArgumentNullException.ThrowIfNull(analysisResult);

        var tokens = analysisResult.TextTokens
            .Select(NormalizeToken)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        var joinedText = string.Join(" ", tokens);
        var fullText = LooksLikeCharacterSpacedText(joinedText)
            ? CollapseCharacterSpacedText(joinedText)
            : joinedText;

        fullText = RepairHungarianFragmentedWords(fullText);
        var messages = new List<string> { $"Text extraction produced {tokens.Length} normalized token(s)." };

        return new TextExtractionResult(tokens, fullText, messages);
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        var normalized = Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
        return LooksLikeCharacterSpacedText(normalized)
            ? CollapseCharacterSpacedText(normalized)
            : normalized;
    }

    private static string RepairHungarianFragmentedWords(string value)
    {
        var repaired = value;
        foreach (var (pattern, replacement) in HungarianFragmentRepairs)
        {
            repaired = pattern.Replace(repaired, replacement);
        }

        return Regex.Replace(repaired, @"\s+", " ").Trim();
    }

    private static bool LooksLikeCharacterSpacedText(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 8)
        {
            return false;
        }

        var singleCharacterParts = parts.Count(static part => part.Length == 1 && !char.IsPunctuation(part[0]));
        return singleCharacterParts >= parts.Length * 0.65;
    }

    private static string CollapseCharacterSpacedText(string value)
    {
        var builder = new StringBuilder(value.Length);
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (part.Length == 1 && !char.IsPunctuation(part[0]))
            {
                builder.Append(part);
                continue;
            }

            if (builder.Length > 0 && !char.IsPunctuation(part[0]))
            {
                builder.Append(' ');
            }

            builder.Append(part);
        }

        return builder.ToString();
    }
}
