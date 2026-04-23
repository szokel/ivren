using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.Core.Services;

public sealed class InvoiceNumberDetector : IInvoiceNumberDetector
{
    private const string InvoiceLabelPattern =
        @"invoice\s*number|invoice\s*no|szamlaszam|szamla\s*sorszama|szamla\s*szama|sorszam";

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

    private static readonly string[] XmlAttributeCandidates =
    [
        "invoicenumber",
        "invoice_no",
        "invoiceno",
        "szamlaszam",
        "szlaszam",
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
                        $"Invoice number detected from XML attribute '{match.Name.LocalName}' in {document.SourceName}.");
                }
            }

            var fallback = InvoiceCandidateRegex.Match(document.Content);
            if (fallback.Success && TryReadCandidateValue(fallback.Value, out var xmlFallbackCandidate))
            {
                return InvoiceNumberDetectionResult.Found(
                    xmlFallbackCandidate,
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
            $@"(?:{InvoiceLabelPattern})[^A-Z0-9]{{0,50}}(?<value>[A-Z0-9/\-_.]{{6,}})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (regexMatch.Success)
        {
            var fallbackCandidate = CleanCandidate(regexMatch.Groups["value"].Value);
            if (IsValidInvoiceNumberCandidate(fallbackCandidate))
            {
                return InvoiceNumberDetectionResult.Found(
                    fallbackCandidate,
                    DetectionSource.Text,
                    "Invoice number detected from label-linked extracted PDF text using fallback pattern matching.");
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
            if (!IsValidInvoiceNumberCandidate(rawCandidate))
            {
                continue;
            }

            invoiceNumber = rawCandidate;
            break;
        }

        return IsValidInvoiceNumberCandidate(invoiceNumber);
    }

    private static bool LooksLikeInvoiceLabel(string normalizedToken)
        => InvoiceLabelRegex.IsMatch(normalizedToken);

    private static bool LooksLikeFocusedInvoiceLabel(string normalizedToken)
        => FocusedInvoiceLabelRegex.IsMatch(normalizedToken);

    private static bool TryReadCandidateValue(string rawValue, out string invoiceNumber)
    {
        var match = InvoiceCandidateRegex.Match(rawValue);
        if (!match.Success)
        {
            invoiceNumber = string.Empty;
            return false;
        }

        invoiceNumber = CleanCandidate(match.Value);
        return IsValidInvoiceNumberCandidate(invoiceNumber);
    }

    private static bool IsValidInvoiceNumberCandidate(string? invoiceNumber)
        => !string.IsNullOrWhiteSpace(invoiceNumber)
            && invoiceNumber.Any(char.IsDigit);

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
