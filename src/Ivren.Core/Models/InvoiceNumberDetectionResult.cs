namespace Ivren.Core.Models;

public sealed record InvoiceNumberDetectionResult(
    bool Success,
    string? InvoiceNumber,
    DetectionSource Source,
    string Message)
{
    public static InvoiceNumberDetectionResult NotFound(DetectionSource source, string message)
        => new(false, null, source, message);

    public static InvoiceNumberDetectionResult Found(string invoiceNumber, DetectionSource source, string message)
        => new(true, invoiceNumber, source, message);
}
