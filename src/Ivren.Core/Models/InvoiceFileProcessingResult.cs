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
    IReadOnlyList<string> Messages)
{
    public string Summary => Messages.LastOrDefault() ?? string.Empty;
}
