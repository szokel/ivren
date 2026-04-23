using System.Text;
using System.Xml.Linq;
using System.Xml;
using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.Core.Services;

public sealed class XmlInvoiceDataExtractor : IXmlInvoiceDataExtractor
{
    public XmlInvoiceExtractionResult Extract(PdfAnalysisResult analysisResult)
    {
        ArgumentNullException.ThrowIfNull(analysisResult);

        var documents = new List<XmlInvoiceDocument>();
        var messages = new List<string>();

        foreach (var embeddedFile in analysisResult.EmbeddedFiles)
        {
            if (!LooksLikeXml(embeddedFile))
            {
                continue;
            }

            if (!TryLoadXmlContent(embeddedFile.Content, out var content))
            {
                continue;
            }

            try
            {
                _ = XDocument.Parse(content, LoadOptions.None);
                documents.Add(new XmlInvoiceDocument(embeddedFile.FileName, content));
                messages.Add($"Usable embedded XML found: {embeddedFile.FileName}");
            }
            catch
            {
                messages.Add($"Embedded file looked like XML but could not be parsed: {embeddedFile.FileName}");
            }
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

    private static bool TryLoadXmlContent(byte[] contentBytes, out string content)
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

            var document = XDocument.Load(xmlReader, LoadOptions.None);
            content = document.ToString(SaveOptions.DisableFormatting);
            return !string.IsNullOrWhiteSpace(content);
        }
        catch
        {
            content = string.Empty;
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
