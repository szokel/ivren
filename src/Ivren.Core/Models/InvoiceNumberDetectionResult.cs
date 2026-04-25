namespace Ivren.Core.Models;

public sealed record InvoiceNumberDetectionResult(
    bool Success,
    string? InvoiceNumber,
    DetectionSource Source,
    string Message,
    double ConfidenceScore,
    ConfidenceLevel ConfidenceLevel,
    bool IsUncertain)
{
    public static InvoiceNumberDetectionResult NotFound(DetectionSource source, string message)
        => new(false, null, source, message, 0.0, ConfidenceLevel.Low, true);

    public static InvoiceNumberDetectionResult Found(
        string invoiceNumber,
        DetectionSource source,
        string message,
        double confidenceScore,
        ConfidenceLevel confidenceLevel)
        => new(
            true,
            invoiceNumber,
            source,
            message,
            confidenceScore,
            confidenceLevel,
            confidenceScore < 0.7);
}
