using Ivren.Core.Models;

namespace Ivren.Core.Contracts;

public interface IInvoiceFileProcessor
{
    InvoiceFileProcessingResult Process(string filePath);
}
