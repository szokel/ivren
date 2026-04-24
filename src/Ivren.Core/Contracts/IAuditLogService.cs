using Ivren.Core.Models;

namespace Ivren.Core.Contracts;

public interface IAuditLogService
{
    AuditLogWriteResult Write(string auditLogFolderPath, InvoiceAuditLogEntry entry);
}
