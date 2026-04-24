using System.Text.Json;
using System.Text.Json.Serialization;
using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.Core.Services;

public sealed class JsonLinesAuditLogService : IAuditLogService
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public AuditLogWriteResult Write(string auditLogFolderPath, InvoiceAuditLogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(auditLogFolderPath))
        {
            return new AuditLogWriteResult(false, null, "Audit log was not written because no audit log folder was configured.", null);
        }

        if (!Directory.Exists(auditLogFolderPath))
        {
            return new AuditLogWriteResult(false, null, $"Audit log folder does not exist: {auditLogFolderPath}", null);
        }

        var logFilePath = Path.Combine(auditLogFolderPath, $"ivren-audit-{entry.Timestamp:yyyy-MM-dd}.log");

        try
        {
            var jsonLine = JsonSerializer.Serialize(entry, JsonOptions);
            File.AppendAllText(logFilePath, jsonLine + Environment.NewLine);
            return new AuditLogWriteResult(true, logFilePath, $"Audit log entry written: {logFilePath}", null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new AuditLogWriteResult(false, logFilePath, $"Audit log entry could not be written: {logFilePath}", exception.Message);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
