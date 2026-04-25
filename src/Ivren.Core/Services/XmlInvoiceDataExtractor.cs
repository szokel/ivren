using System.Text;
using System.Xml.Linq;
using System.Xml;
using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.Core.Services;

public sealed class XmlInvoiceDataExtractor : IXmlInvoiceDataExtractor
{
    private static readonly string[] XmlEncodingDiagnostics =
    [
        "utf-8",
        "utf-16",
        "iso-8859-2",
        "windows-1250"
    ];

    static XmlInvoiceDataExtractor()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static IReadOnlyList<string> GetXmlEncodingSupportDiagnostics()
        => XmlEncodingDiagnostics
            .Select(static encodingName => $"{encodingName}: {(CanParseDiagnosticXml(encodingName) ? "supported" : "not supported")}")
            .ToArray();

    public XmlInvoiceExtractionResult Extract(PdfAnalysisResult analysisResult)
    {
        ArgumentNullException.ThrowIfNull(analysisResult);

        var documents = new List<XmlInvoiceDocument>();
        var messages = new List<string>();

        foreach (var embeddedFile in analysisResult.EmbeddedFiles)
        {
            if (!LooksLikeXml(embeddedFile))
            {
                messages.Add($"Embedded file is not an XML candidate: {embeddedFile.FileName}");
                continue;
            }

            messages.Add($"Embedded file is an XML candidate: {embeddedFile.FileName}");
            if (!TryLoadXmlDocument(embeddedFile.Content, out var document, out var parseError))
            {
                messages.Add($"XML parsing failed for {embeddedFile.FileName}: {parseError}");
                continue;
            }

            var rootName = document.Root?.Name.LocalName ?? "(none)";
            var hasSzamlaSzam = document.Descendants()
                .Any(static element => string.Equals(element.Name.LocalName, "SZAMLA_SZAM", StringComparison.OrdinalIgnoreCase));

            documents.Add(new XmlInvoiceDocument(embeddedFile.FileName, document.ToString(SaveOptions.DisableFormatting)));
            messages.Add($"XML parsing succeeded for {embeddedFile.FileName}.");
            messages.Add($"XML root element for {embeddedFile.FileName}: {rootName}");
            messages.Add($"XML contains SZAMLA_SZAM for {embeddedFile.FileName}: {(hasSzamlaSzam ? "Yes" : "No")}");
            messages.Add($"Usable embedded XML found: {embeddedFile.FileName}");
        }

        if (documents.Count == 0)
        {
            messages.Add("No usable embedded XML invoice data was found.");
        }

        return new XmlInvoiceExtractionResult(documents, messages);
    }

    private static bool LooksLikeXml(PdfEmbeddedFile embeddedFile)
    {
        if (embeddedFile.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(embeddedFile.MediaType, "XML", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var preview = DecodePreviewText(embeddedFile.Content).TrimStart('\0', '\uFEFF', ' ', '\r', '\n', '\t');
        return preview.StartsWith("<", StringComparison.Ordinal);
    }

    private static bool TryLoadXmlDocument(byte[] contentBytes, out XDocument document, out string error)
    {
        try
        {
            using var stream = new MemoryStream(contentBytes);
            using var xmlReader = XmlReader.Create(
                stream,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    XmlResolver = null,
                    CloseInput = false
                });

            document = XDocument.Load(xmlReader, LoadOptions.None);
            error = string.Empty;
            return document.Root is not null;
        }
        catch (Exception exception) when (exception is XmlException
            or DecoderFallbackException
            or IOException
            or InvalidOperationException)
        {
            document = null!;
            error = exception.Message;
            return false;
        }
    }

    private static bool CanParseDiagnosticXml(string encodingName)
    {
        try
        {
            var encoding = Encoding.GetEncoding(encodingName);
            var xml = $"<?xml version=\"1.0\" encoding=\"{encodingName}\"?><SZAMLA><SZAMLA_SZAM>83502860670</SZAMLA_SZAM><ARVIZTURO>Árvíztűrő tükörfúrógép</ARVIZTURO></SZAMLA>";
            return TryLoadXmlDocument(encoding.GetBytes(xml), out var document, out _)
                && string.Equals(document.Root?.Name.LocalName, "SZAMLA", StringComparison.Ordinal)
                && document.Descendants("SZAMLA_SZAM").Any(static element => element.Value == "83502860670");
        }
        catch
        {
            return false;
        }
    }

    private static string DecodePreviewText(byte[] contentBytes)
    {
        var previewBytes = contentBytes.Take(200).ToArray();
        if (previewBytes.Length >= 2 && previewBytes[0] == 0xFE && previewBytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(previewBytes[2..]);
        }

        if (previewBytes.Length >= 2 && previewBytes[0] == 0xFF && previewBytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(previewBytes[2..]);
        }

        return Encoding.UTF8.GetString(previewBytes);
    }
}
