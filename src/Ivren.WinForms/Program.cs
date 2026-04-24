using Ivren.Core.Contracts;
using Ivren.Core.Services;

namespace Ivren.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var processor = BuildProcessor();
        Application.Run(new MainForm(processor));
    }

    private static IInvoiceFileProcessor BuildProcessor()
    {
        var pdfAnalysisService = new PdfAnalysisService();
        var xmlInvoiceDataExtractor = new XmlInvoiceDataExtractor();
        var textExtractionService = new PdfTextExtractionService();
        var invoiceNumberDetector = new InvoiceNumberDetector();
        var filenameSanitizer = new WindowsFilenameSanitizer();
        var fileRenameService = new FileRenameService();
        var auditLogService = new JsonLinesAuditLogService();

        return new InvoiceFileProcessor(
            pdfAnalysisService,
            xmlInvoiceDataExtractor,
            textExtractionService,
            invoiceNumberDetector,
            filenameSanitizer,
            fileRenameService,
            auditLogService);
    }
}
