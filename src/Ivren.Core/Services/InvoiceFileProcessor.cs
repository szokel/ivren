using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.Core.Services;

public sealed class InvoiceFileProcessor : IInvoiceFileProcessor
{
    private readonly IPdfAnalysisService _pdfAnalysisService;
    private readonly IXmlInvoiceDataExtractor _xmlInvoiceDataExtractor;
    private readonly ITextExtractionService _textExtractionService;
    private readonly IInvoiceNumberDetector _invoiceNumberDetector;
    private readonly IFilenameSanitizer _filenameSanitizer;
    private readonly IFileRenameService _fileRenameService;

    public InvoiceFileProcessor(
        IPdfAnalysisService pdfAnalysisService,
        IXmlInvoiceDataExtractor xmlInvoiceDataExtractor,
        ITextExtractionService textExtractionService,
        IInvoiceNumberDetector invoiceNumberDetector,
        IFilenameSanitizer filenameSanitizer,
        IFileRenameService fileRenameService)
    {
        _pdfAnalysisService = pdfAnalysisService;
        _xmlInvoiceDataExtractor = xmlInvoiceDataExtractor;
        _textExtractionService = textExtractionService;
        _invoiceNumberDetector = invoiceNumberDetector;
        _filenameSanitizer = filenameSanitizer;
        _fileRenameService = fileRenameService;
    }

    public InvoiceFileProcessingResult Process(string filePath, InvoiceFileProcessingOptions? options = null)
    {
        options ??= new InvoiceFileProcessingOptions();
        var messages = new List<string>();

        try
        {
            messages.Add($"Processing started: {filePath}");
            if (options.DryRun)
            {
                messages.Add("Dry-run mode is enabled. The file will be analyzed, but no rename will be executed.");
            }

            var analysisResult = _pdfAnalysisService.Analyze(filePath);
            messages.AddRange(analysisResult.Messages);

            var xmlResult = _xmlInvoiceDataExtractor.Extract(analysisResult);
            messages.AddRange(xmlResult.Messages);

            var detectionResult = _invoiceNumberDetector.DetectFromXml(xmlResult);
            messages.Add(detectionResult.Message);

            if (!detectionResult.Success)
            {
                var textResult = _textExtractionService.Extract(analysisResult);
                messages.AddRange(textResult.Messages);

                detectionResult = _invoiceNumberDetector.DetectFromText(textResult);
                messages.Add(detectionResult.Message);
            }

            if (!detectionResult.Success || string.IsNullOrWhiteSpace(detectionResult.InvoiceNumber))
            {
                messages.Add("Processing failed because no invoice number could be determined.");

                return new InvoiceFileProcessingResult(
                    filePath,
                    FileProcessStatus.Failed,
                    detectionResult.Source,
                    null,
                    null,
                    null,
                    options.DryRun,
                    false,
                    false,
                    messages);
            }

            var sanitizedFileName = _filenameSanitizer.Sanitize(detectionResult.InvoiceNumber);
            if (string.IsNullOrWhiteSpace(sanitizedFileName))
            {
                messages.Add("Processing failed because the sanitized invoice number produced an empty file name.");

                return new InvoiceFileProcessingResult(
                    filePath,
                    FileProcessStatus.Failed,
                    detectionResult.Source,
                    detectionResult.InvoiceNumber,
                    null,
                    null,
                    options.DryRun,
                    false,
                    false,
                    messages);
            }

            var targetFilePath = BuildTargetFilePath(filePath, sanitizedFileName);
            var targetFileName = Path.GetFileName(targetFilePath);
            messages.Add($"Sanitized target file name: {targetFileName}");

            if (options.DryRun)
            {
                messages.Add("Rename skipped because dry-run mode is enabled.");

                return new InvoiceFileProcessingResult(
                    filePath,
                    FileProcessStatus.Success,
                    detectionResult.Source,
                    detectionResult.InvoiceNumber,
                    sanitizedFileName,
                    targetFilePath,
                    true,
                    true,
                    false,
                    messages);
            }

            var renameResult = _fileRenameService.Rename(filePath, sanitizedFileName);
            messages.Add(renameResult.Message);

            return new InvoiceFileProcessingResult(
                filePath,
                renameResult.Success ? FileProcessStatus.Success : FileProcessStatus.Failed,
                detectionResult.Source,
                detectionResult.InvoiceNumber,
                sanitizedFileName,
                renameResult.TargetFilePath,
                options.DryRun,
                false,
                renameResult.Renamed,
                messages);
        }
        catch (Exception exception)
        {
            messages.Add($"Unhandled processing error: {exception.Message}");

            return new InvoiceFileProcessingResult(
                filePath,
                FileProcessStatus.Failed,
                DetectionSource.None,
                null,
                null,
                null,
                options.DryRun,
                false,
                false,
                messages);
        }
    }

    private static string BuildTargetFilePath(string sourceFilePath, string targetFileNameWithoutExtension)
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFilePath)
            ?? throw new InvalidOperationException("The source directory could not be determined.");

        var extension = Path.GetExtension(sourceFilePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".pdf";
        }

        return Path.Combine(sourceDirectory, targetFileNameWithoutExtension + extension);
    }
}
