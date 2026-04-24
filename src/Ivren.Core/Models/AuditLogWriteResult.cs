namespace Ivren.Core.Models;

public sealed record AuditLogWriteResult(
    bool Success,
    string? LogFilePath,
    string Message,
    string? Error);
