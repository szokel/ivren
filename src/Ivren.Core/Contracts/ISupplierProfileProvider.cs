using Ivren.Core.Models;

namespace Ivren.Core.Contracts;

public interface ISupplierProfileProvider
{
    SupplierProfileSelection SelectProfile(
        XmlInvoiceExtractionResult xmlExtractionResult,
        TextExtractionResult textExtractionResult);
}
