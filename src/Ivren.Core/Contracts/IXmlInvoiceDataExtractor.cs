using Ivren.Core.Models;

namespace Ivren.Core.Contracts;

public interface IXmlInvoiceDataExtractor
{
    XmlInvoiceExtractionResult Extract(PdfAnalysisResult analysisResult);
}
