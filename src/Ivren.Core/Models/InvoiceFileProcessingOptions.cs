namespace Ivren.Core.Models;

public sealed record InvoiceFileProcessingOptions(
    bool DryRun = false,
    string? RenamedFolderPath = null,
    string? FailedFolderPath = null,
    string? AuditLogFolderPath = null);
