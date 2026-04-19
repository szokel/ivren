namespace Ivren.Core.Models;

public sealed record XmlInvoiceExtractionResult(
    IReadOnlyList<XmlInvoiceDocument> Documents,
    IReadOnlyList<string> Messages);
