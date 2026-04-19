using System.Text;
using System.Xml.Linq;
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

            var content = Encoding.UTF8.GetString(embeddedFile.Content).Trim('\0', '\uFEFF', ' ', '\r', '\n', '\t');
            if (string.IsNullOrWhiteSpace(content))
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

        var preview = Encoding.UTF8.GetString(embeddedFile.Content.Take(200).ToArray()).TrimStart('\0', '\uFEFF', ' ', '\r', '\n', '\t');
        return preview.StartsWith("<", StringComparison.Ordinal);
    }
}
