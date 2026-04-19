using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.Core.Services;

public sealed class InvoiceNumberDetector : IInvoiceNumberDetector
{
    private static readonly string[] XmlElementCandidates =
    [
        "invoicenumber",
        "invoice_no",
        "invoiceno",
        "szamlaszam",
        "szamla_sorszam",
        "sorszam",
        "szamlaazonosito"
    ];

    private static readonly Regex InvoiceCandidateRegex = new(
        @"[A-Z0-9][A-Z0-9/\-_.]{5,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LabeledInvoiceNumberRegex = new(
        @"(?:invoice\s*number|invoice\s*no|szamlaszam|szamla\s*sorszama|szamla\s*szama|sorszam)[^A-Z0-9]{0,50}(?<value>[A-Z0-9/\-_.]{6,})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

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
                        $"Invoice number detected from XML element '{match.Name.LocalName}' in {document.SourceName}.");
                }
            }

            var fallback = InvoiceCandidateRegex.Match(document.Content);
            if (fallback.Success)
            {
                return InvoiceNumberDetectionResult.Found(
                    fallback.Value,
                    DetectionSource.Xml,
                    $"Invoice number candidate detected from XML content fallback in {document.SourceName}.");
            }
        }

        return InvoiceNumberDetectionResult.NotFound(DetectionSource.Xml, "No invoice number could be detected from embedded XML data.");
    }

    public InvoiceNumberDetectionResult DetectFromText(TextExtractionResult textExtractionResult)
    {
        ArgumentNullException.ThrowIfNull(textExtractionResult);

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

            if (TryExtractCandidateFromLabeledToken(token, normalized, out var inlineCandidate))
            {
                return InvoiceNumberDetectionResult.Found(
                    inlineCandidate,
                    DetectionSource.Text,
                    "Invoice number detected from labeled PDF text.");
            }

            if (!LooksLikeFocusedInvoiceLabel(normalized))
            {
                continue;
            }

            for (var offset = 1; offset <= 3 && index + offset < textExtractionResult.Tokens.Count; offset++)
            {
                if (TryReadCandidateValue(textExtractionResult.Tokens[index + offset], out var candidate))
                {
                    return InvoiceNumberDetectionResult.Found(
                        candidate,
                        DetectionSource.Text,
                        "Invoice number detected from text near an invoice label.");
                }
            }
        }

        var regexMatch = Regex.Match(
            NormalizeForComparison(textExtractionResult.FullText),
            @"(?:invoice\s*number|invoice\s*no|szamlaszam|szamla\s*sorszama|szamla\s*szama|sorszam)[^A-Z0-9]{0,50}(?<value>[A-Z0-9/\-_.]{6,})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (regexMatch.Success)
        {
            return InvoiceNumberDetectionResult.Found(
                regexMatch.Groups["value"].Value,
                DetectionSource.Text,
                "Invoice number detected from the extracted PDF text using fallback pattern matching.");
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

    private static bool TryExtractCandidateFromLabeledToken(string token, string normalizedToken, out string invoiceNumber)
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
            if (string.IsNullOrWhiteSpace(rawCandidate) || !rawCandidate.Any(char.IsDigit))
            {
                continue;
            }

            invoiceNumber = rawCandidate;
            break;
        }

        return !string.IsNullOrWhiteSpace(invoiceNumber)
            && invoiceNumber.Any(char.IsDigit);
    }

    private static bool LooksLikeInvoiceLabel(string normalizedToken)
        => normalizedToken.Contains("invoice number", StringComparison.Ordinal)
            || normalizedToken.Contains("invoice no", StringComparison.Ordinal)
            || normalizedToken.Contains("szamlaszam", StringComparison.Ordinal)
            || normalizedToken.Contains("szamla sorszama", StringComparison.Ordinal)
            || normalizedToken.Contains("szamla szama", StringComparison.Ordinal)
            || normalizedToken.Contains("sorszam", StringComparison.Ordinal);

    private static bool LooksLikeFocusedInvoiceLabel(string normalizedToken)
        => normalizedToken.StartsWith("invoice number", StringComparison.Ordinal)
            || normalizedToken.StartsWith("invoice no", StringComparison.Ordinal)
            || normalizedToken.StartsWith("szamlaszam", StringComparison.Ordinal)
            || normalizedToken.StartsWith("szamla sorszama", StringComparison.Ordinal)
            || normalizedToken.StartsWith("szamla szama", StringComparison.Ordinal)
            || normalizedToken.StartsWith("sorszam", StringComparison.Ordinal);

    private static bool TryReadCandidateValue(string rawValue, out string invoiceNumber)
    {
        var match = InvoiceCandidateRegex.Match(rawValue);
        if (!match.Success)
        {
            invoiceNumber = string.Empty;
            return false;
        }

        invoiceNumber = CleanCandidate(match.Value);
        return !string.IsNullOrWhiteSpace(invoiceNumber)
            && invoiceNumber.Any(char.IsDigit);
    }

    private static string CleanCandidate(string value)
        => value.Trim().Trim(':', ';', ',', '.', '-', '_', '/', '\\');

    private static string NormalizeForComparison(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var cleaned = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        cleaned = cleaned.Replace("/", " ", StringComparison.Ordinal)
            .Replace("\\", " ", StringComparison.Ordinal)
            .Replace(":", " ", StringComparison.Ordinal);

        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }
}
