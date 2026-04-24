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
                messages.Add("Dry-run mode is enabled. The file will be analyzed, but no file move or rename will be executed.");
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
                var failedTargetPath = BuildFailedTargetFilePath(filePath, options);

                if (options.DryRun)
                {
                    AddDryRunFailedMoveMessages(messages, failedTargetPath);

                    return new InvoiceFileProcessingResult(
                        filePath,
                        FileProcessStatus.Failed,
                        detectionResult.Source,
                        null,
                        null,
                        failedTargetPath,
                        true,
                        true,
                        false,
                        messages);
                }

                var failedMoveResult = MoveFailedFile(filePath, options);
                messages.Add(failedMoveResult.Message);

                return new InvoiceFileProcessingResult(
                    filePath,
                    FileProcessStatus.Failed,
                    detectionResult.Source,
                    null,
                    null,
                    failedMoveResult.TargetFilePath,
                    options.DryRun,
                    false,
                    failedMoveResult.Renamed,
                    messages);
            }

            var sanitizedFileName = _filenameSanitizer.Sanitize(detectionResult.InvoiceNumber);
            if (string.IsNullOrWhiteSpace(sanitizedFileName))
            {
                messages.Add("Processing failed because the sanitized invoice number produced an empty file name.");
                var failedTargetPath = BuildFailedTargetFilePath(filePath, options);

                if (options.DryRun)
                {
                    AddDryRunFailedMoveMessages(messages, failedTargetPath);

                    return new InvoiceFileProcessingResult(
                        filePath,
                        FileProcessStatus.Failed,
                        detectionResult.Source,
                        detectionResult.InvoiceNumber,
                        null,
                        failedTargetPath,
                        options.DryRun,
                        true,
                        false,
                        messages);
                }

                var failedMoveResult = MoveFailedFile(filePath, options);
                messages.Add(failedMoveResult.Message);

                return new InvoiceFileProcessingResult(
                    filePath,
                    FileProcessStatus.Failed,
                    detectionResult.Source,
                    detectionResult.InvoiceNumber,
                    null,
                    failedMoveResult.TargetFilePath,
                    options.DryRun,
                    false,
                    failedMoveResult.Renamed,
                    messages);
            }

            var targetFilePath = BuildSuccessfulTargetFilePath(filePath, sanitizedFileName, options);
            messages.Add($"Sanitized target file name: {sanitizedFileName}{Path.GetExtension(targetFilePath)}");
            messages.Add($"Target file path: {targetFilePath}");

            if (options.DryRun)
            {
                messages.Add("File move and rename skipped because dry-run mode is enabled.");

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

            var renameResult = RenameSuccessfulFile(filePath, sanitizedFileName, options);
            messages.Add(renameResult.Message);
            if (!renameResult.Success)
            {
                var failedMoveResult = MoveFailedFile(filePath, options);
                messages.Add(failedMoveResult.Message);

                return new InvoiceFileProcessingResult(
                    filePath,
                    FileProcessStatus.Failed,
                    detectionResult.Source,
                    detectionResult.InvoiceNumber,
                    sanitizedFileName,
                    failedMoveResult.TargetFilePath ?? renameResult.TargetFilePath,
                    options.DryRun,
                    false,
                    failedMoveResult.Renamed,
                    messages);
            }

            return new InvoiceFileProcessingResult(
                filePath,
                FileProcessStatus.Success,
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

    private string BuildSuccessfulTargetFilePath(
        string sourceFilePath,
        string targetFileNameWithoutExtension,
        InvoiceFileProcessingOptions options)
    {
        var targetDirectory = string.IsNullOrWhiteSpace(options.RenamedFolderPath)
            ? Path.GetDirectoryName(sourceFilePath)
            : options.RenamedFolderPath;

        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new InvalidOperationException("The successful-files target directory could not be determined.");
        }

        return _fileRenameService.BuildTargetPath(sourceFilePath, targetDirectory, targetFileNameWithoutExtension);
    }

    private string? BuildFailedTargetFilePath(string sourceFilePath, InvoiceFileProcessingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.FailedFolderPath))
        {
            return null;
        }

        return _fileRenameService.BuildTargetPathPreservingName(sourceFilePath, options.FailedFolderPath);
    }

    private FileRenameResult RenameSuccessfulFile(
        string sourceFilePath,
        string targetFileNameWithoutExtension,
        InvoiceFileProcessingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RenamedFolderPath))
        {
            return _fileRenameService.Rename(sourceFilePath, targetFileNameWithoutExtension);
        }

        return _fileRenameService.RenameToFolder(
            sourceFilePath,
            options.RenamedFolderPath,
            targetFileNameWithoutExtension);
    }

    private FileRenameResult MoveFailedFile(string sourceFilePath, InvoiceFileProcessingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.FailedFolderPath))
        {
            return new FileRenameResult(false, false, null, "The failed file was not moved because no failed-files folder was configured.");
        }

        return _fileRenameService.MoveToFolderPreservingName(sourceFilePath, options.FailedFolderPath);
    }

    private static void AddDryRunFailedMoveMessages(List<string> messages, string? failedTargetPath)
    {
        if (string.IsNullOrWhiteSpace(failedTargetPath))
        {
            messages.Add("Failed-file move skipped because dry-run mode is enabled, but no failed-files folder is configured.");
            return;
        }

        messages.Add($"Failed-file target path: {failedTargetPath}");
        messages.Add("Failed-file move skipped because dry-run mode is enabled.");
    }
}
