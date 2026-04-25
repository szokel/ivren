using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.Core.Services;

public sealed class InvoiceNumberDetector : IInvoiceNumberDetector
{
    private static readonly InvoiceDetectionOptions DefaultDetectionOptions = new();

    private const string InvoiceLabelPattern =
        @"invoice\s*number|invoice\s*no|szamlaszam|szamla\s*sorszama|szamla\s*szama|sorszam";

    private static readonly string[] XmlElementCandidates =
    [
        "invoicenumber",
        "invoice_no",
        "invoiceno",
        "szamlaszam",
        "szamla_szam",
        "szamla_sorszam",
        "sorszam",
        "szamlaazonosito"
    ];

    private static readonly string[] XmlAttributeCandidates =
    [
        "invoicenumber",
        "invoice_no",
        "invoiceno",
        "szamlaszam",
        "szlaszam",
        "szamla_szam",
        "szamla_sorszam",
        "sorszam",
        "szamlaazonosito"
    ];

    private static readonly Regex InvoiceCandidateRegex = new(
        @"[A-Z0-9][A-Z0-9/\-_.]{5,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LabeledInvoiceNumberRegex = new(
        $@"(?:{InvoiceLabelPattern})[^A-Z0-9]{{0,50}}(?<value>[A-Z0-9/\-_.]{{6,}})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex InvoiceLabelRegex = new(
        $@"(?:{InvoiceLabelPattern})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex FocusedInvoiceLabelRegex = new(
        $@"^(?:{InvoiceLabelPattern})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex StandaloneHeadingInvoiceCandidateRegex = new(
        @"[A-Z][A-Z0-9]*(?:-[A-Z0-9]+)*-\d{4}-\d{4,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex DateLikeCandidateRegex = new(
        @"^\d{4}[./-]\d{2}[./-]\d{2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BankAccountLabelRegex = new(
        @"\b(?:account\s+(?:no|number)|bank\s+account|bankszamla(?:szam)?[a-z]*)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex BankBlockHeadingRegex = new(
        @"\b(?:banki\s+adatok|bank\s+details)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex BankCredentialRegex = new(
        @"\b(?:iban|swift|bic)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> TableHeaderColumnsBeforeInvoiceNumber = new(StringComparer.Ordinal)
    {
        "referencia szam",
        "reference no",
        "szamla kelte",
        "invoice date",
        "teljesites datuma",
        "performance date",
        "fizetesi hatarido",
        "term of payment",
        "fizetesi mod",
        "way of payment"
    };

    private static readonly string[] StandaloneInvoiceHeadingRejectedContinuations =
    [
        "szam",
        "sorszam",
        "kelte",
        "datum",
        "tartalma"
    ];

    public InvoiceNumberDetectionResult DetectFromXml(XmlInvoiceExtractionResult xmlExtractionResult)
    {
        ArgumentNullException.ThrowIfNull(xmlExtractionResult);

        foreach (var document in xmlExtractionResult.Documents)
        {
            if (!TryParseXml(document.Content, out var xDocument))
            {
                continue;
            }

            foreach (var candidateName in XmlElementCandidates)
            {
                var match = xDocument.Descendants()
                    .FirstOrDefault(element => NormalizeForComparison(element.Name.LocalName) == candidateName
                        && TryReadCandidateValue(element.Value, out _));

                if (match is null)
                {
                    continue;
                }

                var invoiceNumber = CleanCandidate(match.Value);
                if (!string.IsNullOrWhiteSpace(invoiceNumber))
                {
                    return InvoiceNumberDetectionResult.Found(
                        invoiceNumber,
                        DetectionSource.Xml,
                        $"Invoice number detected from XML element '{match.Name.LocalName}' in {document.SourceName}.",
                        0.99,
                        ConfidenceLevel.High);
                }
            }

            foreach (var candidateName in XmlAttributeCandidates)
            {
                var match = xDocument.Descendants()
                    .Attributes()
                    .FirstOrDefault(attribute => NormalizeForComparison(attribute.Name.LocalName) == candidateName
                        && TryReadCandidateValue(attribute.Value, out _));

                if (match is null)
                {
                    continue;
                }

                var invoiceNumber = CleanCandidate(match.Value);
                if (!string.IsNullOrWhiteSpace(invoiceNumber))
                {
                    return InvoiceNumberDetectionResult.Found(
                        invoiceNumber,
                        DetectionSource.Xml,
                        $"Invoice number detected from XML attribute '{match.Name.LocalName}' in {document.SourceName}.",
                        0.98,
                        ConfidenceLevel.High);
                }
            }

            var fallback = InvoiceCandidateRegex.Match(document.Content);
            if (fallback.Success && TryReadCandidateValue(fallback.Value, out var xmlFallbackCandidate))
            {
                return InvoiceNumberDetectionResult.Found(
                    xmlFallbackCandidate,
                    DetectionSource.Xml,
                    $"Invoice number candidate detected from XML content fallback in {document.SourceName}.",
                    0.95,
                    ConfidenceLevel.High);
            }
        }

        return InvoiceNumberDetectionResult.NotFound(DetectionSource.Xml, "No invoice number could be detected from embedded XML data.");
    }

    public InvoiceNumberDetectionResult DetectFromText(
        TextExtractionResult textExtractionResult,
        InvoiceDetectionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(textExtractionResult);
        options ??= DefaultDetectionOptions;

        if (textExtractionResult.Tokens.Count == 0)
        {
            return InvoiceNumberDetectionResult.NotFound(
                DetectionSource.Text,
                "No selectable text was extracted from the PDF. OCR support will be needed for image-based invoices.");
        }

        for (var index = 0; index < textExtractionResult.Tokens.Count; index++)
        {
            var token = textExtractionResult.Tokens[index];
            var normalized = NormalizeForComparison(token);

            if (IsBankAccountInvoiceLabelContext(textExtractionResult.Tokens, index, normalized))
            {
                continue;
            }

            if (TryExtractCandidateFromLabeledToken(token, normalized, options, out var inlineCandidate))
            {
                return InvoiceNumberDetectionResult.Found(
                    inlineCandidate,
                    DetectionSource.Text,
                    "Invoice number detected from labeled PDF text.",
                    0.88,
                    ConfidenceLevel.High);
            }

            if (TryReadCandidateFromBilingualInvoiceTable(textExtractionResult.Tokens, index, options, out var tableCandidate))
            {
                return InvoiceNumberDetectionResult.Found(
                    tableCandidate,
                    DetectionSource.Text,
                    "Invoice number detected from a bilingual invoice-number table header.",
                    0.90,
                    ConfidenceLevel.High);
            }

            if (TryReadCandidateAfterStandaloneInvoiceHeading(textExtractionResult.Tokens, index, options, out var standaloneHeadingCandidate))
            {
                return InvoiceNumberDetectionResult.Found(
                    standaloneHeadingCandidate,
                    DetectionSource.Text,
                    "Invoice number detected from text after a standalone invoice heading.",
                    0.76,
                    ConfidenceLevel.Medium);
            }

            if (!LooksLikeFocusedInvoiceLabel(normalized))
            {
                continue;
            }

            for (var offset = 1; offset <= options.NearbyLabelScanWindow && index + offset < textExtractionResult.Tokens.Count; offset++)
            {
                if (TryReadFragmentedHyphenatedCandidate(textExtractionResult.Tokens, index + offset, options, out var fragmentedCandidate))
                {
                    return InvoiceNumberDetectionResult.Found(
                        fragmentedCandidate,
                        DetectionSource.Text,
                        "Invoice number detected from fragmented hyphenated text near an invoice label.",
                        IsStrongExplicitInvoiceLabel(normalized) ? 0.85 : 0.72,
                        IsStrongExplicitInvoiceLabel(normalized) ? ConfidenceLevel.High : ConfidenceLevel.Medium);
                }

                if (TryReadCandidateValue(textExtractionResult.Tokens[index + offset], options, out var candidate))
                {
                    return InvoiceNumberDetectionResult.Found(
                        candidate,
                        DetectionSource.Text,
                        "Invoice number detected from text near an invoice label.",
                        IsStrongExplicitInvoiceLabel(normalized) ? 0.85 : 0.72,
                        IsStrongExplicitInvoiceLabel(normalized) ? ConfidenceLevel.High : ConfidenceLevel.Medium);
                }
            }

            if (TryReadCandidateValueFromAdjacentTokens(textExtractionResult.Tokens, index + 1, options.NearbyLabelScanWindow, options, out var adjacentCandidate))
            {
                return InvoiceNumberDetectionResult.Found(
                    adjacentCandidate,
                    DetectionSource.Text,
                    "Invoice number detected from adjacent text fragments near an invoice label.",
                    IsStrongExplicitInvoiceLabel(normalized) ? 0.82 : 0.70,
                    IsStrongExplicitInvoiceLabel(normalized) ? ConfidenceLevel.High : ConfidenceLevel.Medium);
            }
        }

        var regexMatch = Regex.Match(
            NormalizeForComparison(textExtractionResult.FullText),
            $@"(?:{InvoiceLabelPattern})[^A-Z0-9]{{0,50}}(?<value>[A-Z0-9/\-_.]{{6,}})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (regexMatch.Success)
        {
            var fallbackCandidate = CleanCandidate(regexMatch.Groups["value"].Value);
            if (IsValidInvoiceNumberCandidate(fallbackCandidate, options)
                && !IsBankAccountTextContext(NormalizeForComparison(textExtractionResult.FullText), regexMatch.Index))
            {
                return InvoiceNumberDetectionResult.Found(
                    fallbackCandidate,
                    DetectionSource.Text,
                    "Invoice number detected from label-linked extracted PDF text using fallback pattern matching.",
                    0.60,
                    ConfidenceLevel.Low);
            }
        }

        return InvoiceNumberDetectionResult.NotFound(DetectionSource.Text, "No invoice number could be detected from selectable PDF text.");
    }

    private static bool TryParseXml(string xml, out XDocument document)
    {
        try
        {
            document = XDocument.Parse(xml, LoadOptions.None);
            return true;
        }
        catch
        {
            document = null!;
            return false;
        }
    }

    private static bool TryExtractCandidateFromLabeledToken(
        string token,
        string normalizedToken,
        InvoiceDetectionOptions options,
        out string invoiceNumber)
    {
        if (!LooksLikeInvoiceLabel(normalizedToken))
        {
            invoiceNumber = string.Empty;
            return false;
        }

        var match = LabeledInvoiceNumberRegex.Match(normalizedToken);
        if (!match.Success)
        {
            invoiceNumber = string.Empty;
            return false;
        }

        invoiceNumber = CleanCandidate(match.Groups["value"].Value);
        foreach (Match rawCandidateMatch in InvoiceCandidateRegex.Matches(token))
        {
            var rawCandidate = CleanCandidate(rawCandidateMatch.Value);
            if (!IsValidInvoiceNumberCandidate(rawCandidate, options))
            {
                continue;
            }

            invoiceNumber = rawCandidate;
            break;
        }

        return IsValidInvoiceNumberCandidate(invoiceNumber, options);
    }

    private static bool TryReadCandidateAfterStandaloneInvoiceHeading(
        IReadOnlyList<string> tokens,
        int headingStartIndex,
        InvoiceDetectionOptions options,
        out string invoiceNumber)
    {
        if (!TryReadStandaloneInvoiceHeading(tokens, headingStartIndex, out var candidateStartIndex))
        {
            invoiceNumber = string.Empty;
            return false;
        }

        return TryReadStandaloneHeadingCandidateFromFollowingTokens(tokens, candidateStartIndex, 40, options, out invoiceNumber);
    }

    private static bool TryReadCandidateFromBilingualInvoiceTable(
        IReadOnlyList<string> tokens,
        int labelIndex,
        InvoiceDetectionOptions options,
        out string invoiceNumber)
    {
        invoiceNumber = string.Empty;
        if (!LooksLikeBilingualInvoiceNumberHeader(tokens, labelIndex, out var labelEndIndex)
            || IsBankAccountInvoiceLabelContext(tokens, labelIndex, NormalizeForComparison(tokens[labelIndex])))
        {
            return false;
        }

        var precedingColumns = CountPrecedingTableColumns(tokens, labelIndex);
        if (precedingColumns == 0)
        {
            return false;
        }

        var candidateIndex = labelEndIndex + 1 + precedingColumns;
        if (candidateIndex >= tokens.Count)
        {
            return false;
        }

        return TryReadCandidateValue(tokens[candidateIndex], options, out invoiceNumber);
    }

    private static bool LooksLikeBilingualInvoiceNumberHeader(
        IReadOnlyList<string> tokens,
        int labelIndex,
        out int labelEndIndex)
    {
        labelEndIndex = labelIndex;
        var normalized = NormalizeForComparison(tokens[labelIndex]);

        if (normalized.Contains("szamlaszam invoice no", StringComparison.Ordinal)
            || normalized.Contains("szamla szama invoice number", StringComparison.Ordinal))
        {
            return true;
        }

        if (!IsHungarianInvoiceNumberLabel(normalized) || labelIndex + 1 >= tokens.Count)
        {
            return false;
        }

        var next = NormalizeHeaderLabel(tokens[labelIndex + 1]);
        if (next is "invoice no" or "invoice number")
        {
            labelEndIndex = labelIndex + 1;
            return true;
        }

        return false;
    }

    private static int CountPrecedingTableColumns(IReadOnlyList<string> tokens, int labelIndex)
    {
        const int maxHeaderLookbehind = 14;
        var count = 0;
        var startIndex = Math.Max(0, labelIndex - maxHeaderLookbehind);

        for (var index = startIndex; index < labelIndex; index++)
        {
            var normalized = NormalizeHeaderLabel(tokens[index]);
            if (TableHeaderColumnsBeforeInvoiceNumber.Contains(normalized))
            {
                count++;
            }
        }

        return count / 2;
    }

    private static bool TryReadStandaloneInvoiceHeading(
        IReadOnlyList<string> tokens,
        int startIndex,
        out int candidateStartIndex)
    {
        const string invoiceHeading = "szamla";

        var normalizedToken = NormalizeForComparison(tokens[startIndex]);
        if (normalizedToken == invoiceHeading)
        {
            candidateStartIndex = startIndex + 1;
            return !NextTokensSpellAny(tokens, candidateStartIndex, StandaloneInvoiceHeadingRejectedContinuations);
        }

        var builder = new StringBuilder(invoiceHeading.Length);
        var index = startIndex;

        while (index < tokens.Count && builder.Length < invoiceHeading.Length)
        {
            var normalized = NormalizeForComparison(tokens[index]);
            if (normalized.Length != 1 || !char.IsLetter(normalized[0]))
            {
                candidateStartIndex = -1;
                return false;
            }

            builder.Append(normalized);
            if (!invoiceHeading.StartsWith(builder.ToString(), StringComparison.Ordinal))
            {
                candidateStartIndex = -1;
                return false;
            }

            index++;
        }

        if (builder.ToString() != invoiceHeading)
        {
            candidateStartIndex = -1;
            return false;
        }

        candidateStartIndex = index;
        return !NextTokensSpellAny(tokens, candidateStartIndex, StandaloneInvoiceHeadingRejectedContinuations);
    }

    private static bool TryReadStandaloneHeadingCandidateFromFollowingTokens(
        IReadOnlyList<string> tokens,
        int startIndex,
        int maxTokenCount,
        InvoiceDetectionOptions options,
        out string invoiceNumber)
    {
        var builder = new StringBuilder();
        var bestCandidate = string.Empty;

        for (var offset = 0; offset < maxTokenCount && startIndex + offset < tokens.Count; offset++)
        {
            var tokenIndex = startIndex + offset;
            if (!string.IsNullOrWhiteSpace(bestCandidate) && LooksLikePageMarker(tokens, tokenIndex))
            {
                invoiceNumber = bestCandidate;
                return true;
            }

            var token = tokens[tokenIndex];
            if (!IsInvoiceCodeTokenPart(token))
            {
                if (builder.Length == 0)
                {
                    continue;
                }

                break;
            }

            builder.Append(token);
            if (TryReadStandaloneHeadingCandidate(builder.ToString(), options, out var candidate))
            {
                bestCandidate = candidate;
            }
        }

        invoiceNumber = bestCandidate;
        return !string.IsNullOrWhiteSpace(invoiceNumber);
    }

    private static bool TryReadStandaloneHeadingCandidate(
        string rawValue,
        InvoiceDetectionOptions options,
        out string invoiceNumber)
    {
        var match = StandaloneHeadingInvoiceCandidateRegex.Match(rawValue);
        if (!match.Success)
        {
            invoiceNumber = string.Empty;
            return false;
        }

        invoiceNumber = CleanCandidate(match.Value);
        return IsValidStandaloneHeadingCandidate(invoiceNumber, options);
    }

    private static bool LooksLikeInvoiceLabel(string normalizedToken)
        => InvoiceLabelRegex.IsMatch(normalizedToken);

    private static bool LooksLikeFocusedInvoiceLabel(string normalizedToken)
        => FocusedInvoiceLabelRegex.IsMatch(normalizedToken);

    private static bool IsHungarianInvoiceNumberLabel(string normalizedToken)
        => normalizedToken is "szamlaszam" or "szamla szama" or "szamla sorszama" or "sorszam";

    private static bool IsStrongExplicitInvoiceLabel(string normalizedToken)
        => normalizedToken.Contains("invoice number", StringComparison.Ordinal)
            || normalizedToken.Contains("invoice no", StringComparison.Ordinal)
            || normalizedToken.Contains("szamla szama", StringComparison.Ordinal)
            || normalizedToken.Contains("szamla sorszama", StringComparison.Ordinal)
            || normalizedToken.Contains("szamlaszam", StringComparison.Ordinal)
            || normalizedToken.Contains("sorszam", StringComparison.Ordinal);

    private static string NormalizeHeaderLabel(string value)
        => NormalizeForComparison(value).Trim('.', ',', ';', ':');

    private static bool IsBankAccountInvoiceLabelContext(
        IReadOnlyList<string> tokens,
        int labelIndex,
        string normalizedToken)
    {
        if (!LooksLikeInvoiceLabel(normalizedToken))
        {
            return false;
        }

        if (BankAccountLabelRegex.IsMatch(normalizedToken))
        {
            return true;
        }

        var startIndex = Math.Max(0, labelIndex - 8);
        var endIndex = Math.Min(tokens.Count - 1, labelIndex + 6);
        var context = string.Join(
            ' ',
            Enumerable.Range(startIndex, endIndex - startIndex + 1)
                .Select(index => NormalizeForComparison(tokens[index])));

        return BankBlockHeadingRegex.IsMatch(context) || BankCredentialRegex.IsMatch(context);
    }

    private static bool IsBankAccountTextContext(string normalizedFullText, int matchIndex)
    {
        var startIndex = Math.Max(0, matchIndex - 120);
        var length = Math.Min(normalizedFullText.Length - startIndex, 240);
        var context = normalizedFullText.Substring(startIndex, length);
        return BankAccountLabelRegex.IsMatch(context)
            || BankBlockHeadingRegex.IsMatch(context)
            || BankCredentialRegex.IsMatch(context);
    }

    private static bool TryReadCandidateValue(string rawValue, out string invoiceNumber)
        => TryReadCandidateValue(rawValue, DefaultDetectionOptions, out invoiceNumber);

    private static bool TryReadCandidateValue(
        string rawValue,
        InvoiceDetectionOptions options,
        out string invoiceNumber)
    {
        var match = InvoiceCandidateRegex.Match(rawValue);
        if (!match.Success)
        {
            invoiceNumber = string.Empty;
            return false;
        }

        invoiceNumber = CleanCandidate(match.Value);
        return IsValidInvoiceNumberCandidate(invoiceNumber, options);
    }

    private static bool TryReadCandidateValueFromAdjacentTokens(
        IReadOnlyList<string> tokens,
        int startIndex,
        int maxTokenCount,
        InvoiceDetectionOptions options,
        out string invoiceNumber)
    {
        var builder = new StringBuilder();

        for (var offset = 0; offset < maxTokenCount && startIndex + offset < tokens.Count; offset++)
        {
            var token = tokens[startIndex + offset];
            if (IsRejectedDateLikeToken(token, options))
            {
                builder.Clear();
                continue;
            }

            if (!IsInvoiceCodeTokenPart(token))
            {
                if (builder.Length > 0)
                {
                    break;
                }

                continue;
            }

            builder.Append(token);

            if (!TryReadCandidateValue(builder.ToString(), options, out invoiceNumber))
            {
                continue;
            }

            return true;
        }

        invoiceNumber = string.Empty;
        return false;
    }

    private static bool TryReadFragmentedHyphenatedCandidate(
        IReadOnlyList<string> tokens,
        int startIndex,
        InvoiceDetectionOptions options,
        out string invoiceNumber)
    {
        invoiceNumber = string.Empty;
        if (startIndex >= tokens.Count || !IsInvoiceCodeTokenPart(tokens[startIndex]))
        {
            return false;
        }

        var firstToken = tokens[startIndex];
        var builder = new StringBuilder(firstToken);
        if (!HasValidHyphenatedFragmentStart(firstToken))
        {
            return false;
        }

        var bestCandidate = string.Empty;
        for (var offset = 1; offset <= 3 && startIndex + offset < tokens.Count; offset++)
        {
            var nextToken = tokens[startIndex + offset];
            if (!ShouldAppendHyphenatedFragment(builder.ToString(), nextToken))
            {
                break;
            }

            builder.Append(nextToken);
            if (TryReadCandidateValue(builder.ToString(), options, out var candidate)
                && candidate.Length > firstToken.Length)
            {
                bestCandidate = candidate;
            }
        }

        invoiceNumber = bestCandidate;
        return !string.IsNullOrWhiteSpace(invoiceNumber);
    }

    private static bool ShouldAppendHyphenatedFragment(string currentValue, string nextToken)
    {
        if (string.IsNullOrWhiteSpace(nextToken)
            || nextToken.Length > 8
            || !nextToken.All(character => char.IsLetterOrDigit(character) || character == '-'))
        {
            return false;
        }

        var trimmedNext = nextToken.TrimEnd('-');
        if (string.IsNullOrWhiteSpace(trimmedNext))
        {
            return false;
        }

        return currentValue.EndsWith("-", StringComparison.Ordinal)
            || (currentValue.Contains("-", StringComparison.Ordinal)
                && nextToken.EndsWith("-", StringComparison.Ordinal)
                && trimmedNext.All(char.IsDigit));
    }

    private static bool HasValidHyphenatedFragmentStart(string token)
    {
        var hyphenIndex = token.IndexOf('-');
        if (hyphenIndex < 0)
        {
            return false;
        }

        var prefix = token[..hyphenIndex];
        return prefix.Any(char.IsLetter)
            || prefix.Length >= 4;
    }

    private static bool IsValidInvoiceNumberCandidate(string? invoiceNumber)
        => IsValidInvoiceNumberCandidate(invoiceNumber, DefaultDetectionOptions);

    private static bool IsValidInvoiceNumberCandidate(string? invoiceNumber, InvoiceDetectionOptions options)
        => !string.IsNullOrWhiteSpace(invoiceNumber)
            && invoiceNumber.Any(char.IsDigit)
            && (!options.RejectDateLikeCandidates || !DateLikeCandidateRegex.IsMatch(invoiceNumber));

    private static bool IsValidStandaloneHeadingCandidate(string? invoiceNumber, InvoiceDetectionOptions options)
    {
        if (!IsValidInvoiceNumberCandidate(invoiceNumber, options))
        {
            return false;
        }

        var candidate = invoiceNumber!;
        return candidate.Any(char.IsLetter)
            && candidate.Any(character => character is '-' or '/' or '_' or '.');
    }

    private static bool IsRejectedDateLikeToken(string token, InvoiceDetectionOptions options)
        => options.RejectDateLikeCandidates
            && DateLikeCandidateRegex.IsMatch(CleanCandidate(token));

    private static bool IsInvoiceCodeTokenPart(string token)
        => token.All(character => char.IsLetterOrDigit(character) || character is '-' or '/' or '_' or '.');

    private static bool LooksLikePageMarker(IReadOnlyList<string> tokens, int startIndex)
        => startIndex + 2 < tokens.Count
            && tokens[startIndex].Length == 1
            && char.IsDigit(tokens[startIndex][0])
            && tokens[startIndex + 1] == "/"
            && tokens[startIndex + 2].Length == 1
            && char.IsDigit(tokens[startIndex + 2][0]);

    private static bool NextTokensSpellAny(
        IReadOnlyList<string> tokens,
        int startIndex,
        IReadOnlyList<string> words)
        => words.Any(word => NextTokensSpell(tokens, startIndex, word));

    private static bool NextTokensSpell(IReadOnlyList<string> tokens, int startIndex, string word)
    {
        var builder = new StringBuilder(word.Length);

        for (var index = startIndex; index < tokens.Count && builder.Length < word.Length; index++)
        {
            var normalized = NormalizeForComparison(tokens[index]);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (normalized.Length > word.Length - builder.Length)
            {
                return false;
            }

            builder.Append(normalized);
            if (!word.StartsWith(builder.ToString(), StringComparison.Ordinal))
            {
                return false;
            }
        }

        return builder.ToString() == word;
    }

    private static string CleanCandidate(string value)
        => value.Trim().Trim(':', ';', ',', '.', '-', '_', '/', '\\');

    private static string NormalizeForComparison(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsControl(character))
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(character);
        }

        var cleaned = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        cleaned = cleaned.Replace("/", " ", StringComparison.Ordinal)
            .Replace("\\", " ", StringComparison.Ordinal)
            .Replace(":", " ", StringComparison.Ordinal);

        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }
}
