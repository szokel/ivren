using Ivren.Core.Models;

namespace Ivren.Core.Contracts;

public interface IInvoiceNumberDetector
{
    InvoiceNumberDetectionResult DetectFromXml(XmlInvoiceExtractionResult xmlExtractionResult);
    InvoiceNumberDetectionResult DetectFromText(TextExtractionResult textExtractionResult);
}
