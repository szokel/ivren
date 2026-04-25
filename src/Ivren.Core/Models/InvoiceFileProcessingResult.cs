namespace Ivren.Core.Models;

public sealed record InvoiceFileProcessingResult(
    string SourceFilePath,
    FileProcessStatus Status,
    DetectionSource DetectionSource,
    string? InvoiceNumber,
    string? SanitizedInvoiceNumber,
    string? TargetFilePath,
    bool DryRunEnabled,
    bool RenameSkippedDueToDryRun,
    bool Renamed,
    IReadOnlyList<string> Messages,
    bool IsEncrypted = false,
    ProcessingFailureReason FailureReason = ProcessingFailureReason.None,
    double ConfidenceScore = 0.0,
    ConfidenceLevel ConfidenceLevel = ConfidenceLevel.Low,
    bool IsUncertain = true)
{
    public string Summary => Messages.LastOrDefault() ?? string.Empty;
}
