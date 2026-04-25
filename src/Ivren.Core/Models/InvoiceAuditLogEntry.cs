namespace Ivren.Core.Models;

public sealed record InvoiceAuditLogEntry(
    DateTimeOffset Timestamp,
    string OriginalPath,
    string Outcome,
    DetectionSource DetectionSource,
    string? InvoiceNumber,
    string? TargetPath,
    bool DryRun,
    bool Success,
    string Message,
    string? Error,
    bool IsEncrypted = false,
    ProcessingFailureReason FailureReason = ProcessingFailureReason.None,
    double ConfidenceScore = 0.0,
    ConfidenceLevel ConfidenceLevel = ConfidenceLevel.Low,
    bool IsUncertain = true);
