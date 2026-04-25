using System.Globalization;
using System.Text;
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
    private readonly IAuditLogService _auditLogService;
    private readonly ISupplierProfileProvider _supplierProfileProvider;

    public InvoiceFileProcessor(
        IPdfAnalysisService pdfAnalysisService,
        IXmlInvoiceDataExtractor xmlInvoiceDataExtractor,
        ITextExtractionService textExtractionService,
        IInvoiceNumberDetector invoiceNumberDetector,
        IFilenameSanitizer filenameSanitizer,
        IFileRenameService fileRenameService,
        IAuditLogService? auditLogService = null,
        ISupplierProfileProvider? supplierProfileProvider = null)
    {
        _pdfAnalysisService = pdfAnalysisService;
        _xmlInvoiceDataExtractor = xmlInvoiceDataExtractor;
        _textExtractionService = textExtractionService;
        _invoiceNumberDetector = invoiceNumberDetector;
        _filenameSanitizer = filenameSanitizer;
        _fileRenameService = fileRenameService;
        _auditLogService = auditLogService ?? new JsonLinesAuditLogService();
        _supplierProfileProvider = supplierProfileProvider ?? new JsonSupplierProfileProvider();
    }

    public InvoiceFileProcessingResult Process(string filePath, InvoiceFileProcessingOptions? options = null)
    {
        options ??= new InvoiceFileProcessingOptions();
        var messages = new List<string>();
        InvoiceFileProcessingResult Complete(InvoiceFileProcessingResult result, string? error = null)
        {
            WriteAuditLogIfNeeded(options, result, messages, error);
            return result;
        }

        try
        {
            messages.Add($"Processing started: {filePath}");
            if (options.DryRun)
            {
                messages.Add("Dry-run mode is enabled. The file will be analyzed, but no file move or rename will be executed.");
            }

            var analysisResult = _pdfAnalysisService.Analyze(filePath);
            messages.AddRange(analysisResult.Messages);
            AddEmbeddedFileDiagnostics(analysisResult, messages);
            if (analysisResult.IsEncrypted)
            {
                var failedTargetPath = BuildFailedTargetFilePath(filePath, options);
                messages.Add("Detection confidence: 0.00 (Low), uncertain: Yes.");

                if (options.DryRun)
                {
                    AddDryRunFailedMoveMessages(messages, failedTargetPath);

                    return Complete(new InvoiceFileProcessingResult(
                        filePath,
                        FileProcessStatus.Failed,
                        DetectionSource.None,
                        null,
                        null,
                        failedTargetPath,
                        true,
                        true,
                        false,
                        messages,
                        IsEncrypted: true,
                        FailureReason: ProcessingFailureReason.PasswordProtected));
                }

                var failedMoveResult = MoveFailedFile(filePath, options);
                messages.Add(failedMoveResult.Message);

                return Complete(new InvoiceFileProcessingResult(
                    filePath,
                    FileProcessStatus.Failed,
                    DetectionSource.None,
                    null,
                    null,
                    failedMoveResult.TargetFilePath,
                    options.DryRun,
                    false,
                    failedMoveResult.Renamed,
                    messages,
                    IsEncrypted: true,
                    FailureReason: ProcessingFailureReason.PasswordProtected));
            }

            var xmlResult = _xmlInvoiceDataExtractor.Extract(analysisResult);
            messages.AddRange(xmlResult.Messages);

            var detectionResult = _invoiceNumberDetector.DetectFromXml(xmlResult);
            messages.Add(detectionResult.Message);
            messages.Add(BuildXmlDetectionResultMessage(detectionResult));
            var xmlPathSucceeded = detectionResult.Success
                && detectionResult.Source == DetectionSource.Xml
                && !string.IsNullOrWhiteSpace(detectionResult.InvoiceNumber);

            if (xmlPathSucceeded)
            {
                messages.Add("XML path succeeded, skipping text fallback.");
            }
            else
            {
                messages.Add($"XML path failed, falling back to text. Reason: {detectionResult.Message}");
                var textResult = _textExtractionService.Extract(analysisResult);
                messages.AddRange(textResult.Messages);

                var supplierProfileSelection = _supplierProfileProvider.SelectProfile(xmlResult, textResult);
                messages.Add(supplierProfileSelection.Message);

                detectionResult = _invoiceNumberDetector.DetectFromText(
                    textResult,
                    supplierProfileSelection.Profile.ToDetectionOptions());
                messages.Add(detectionResult.Message);
            }

            if (xmlPathSucceeded && detectionResult.Source != DetectionSource.Xml)
            {
                const string consistencyError = "BUG: XML succeeded but final detection source was overwritten.";
                messages.Add(consistencyError);
                throw new InvalidOperationException(consistencyError);
            }

            messages.Add(BuildConfidenceMessage(
                detectionResult.ConfidenceScore,
                detectionResult.ConfidenceLevel,
                detectionResult.IsUncertain));

            if (!detectionResult.Success || string.IsNullOrWhiteSpace(detectionResult.InvoiceNumber))
            {
                messages.Add("Processing failed because no invoice number could be determined.");
                var failedTargetPath = BuildFailedTargetFilePath(filePath, options);

                if (options.DryRun)
                {
                    AddDryRunFailedMoveMessages(messages, failedTargetPath);

                    return Complete(new InvoiceFileProcessingResult(
                        filePath,
                        FileProcessStatus.Failed,
                        detectionResult.Source,
                        null,
                        null,
                        failedTargetPath,
                        true,
                        true,
                        false,
                        messages));
                }

                var failedMoveResult = MoveFailedFile(filePath, options);
                messages.Add(failedMoveResult.Message);

                return Complete(new InvoiceFileProcessingResult(
                    filePath,
                    FileProcessStatus.Failed,
                    detectionResult.Source,
                    null,
                    null,
                    failedMoveResult.TargetFilePath,
                    options.DryRun,
                    false,
                    failedMoveResult.Renamed,
                    messages));
            }

            var sanitizedFileName = _filenameSanitizer.Sanitize(detectionResult.InvoiceNumber);
            if (string.IsNullOrWhiteSpace(sanitizedFileName))
            {
                messages.Add("Processing failed because the sanitized invoice number produced an empty file name.");
                var failedTargetPath = BuildFailedTargetFilePath(filePath, options);

                if (options.DryRun)
                {
                    AddDryRunFailedMoveMessages(messages, failedTargetPath);

                    return Complete(new InvoiceFileProcessingResult(
                        filePath,
                        FileProcessStatus.Failed,
                        detectionResult.Source,
                        detectionResult.InvoiceNumber,
                        null,
                        failedTargetPath,
                        options.DryRun,
                        true,
                        false,
                        messages));
                }

                var failedMoveResult = MoveFailedFile(filePath, options);
                messages.Add(failedMoveResult.Message);

                return Complete(new InvoiceFileProcessingResult(
                    filePath,
                    FileProcessStatus.Failed,
                    detectionResult.Source,
                    detectionResult.InvoiceNumber,
                    null,
                    failedMoveResult.TargetFilePath,
                    options.DryRun,
                    false,
                    failedMoveResult.Renamed,
                    messages));
            }

            var targetFilePath = BuildSuccessfulTargetFilePath(filePath, sanitizedFileName, options);
            messages.Add($"Sanitized target file name: {sanitizedFileName}{Path.GetExtension(targetFilePath)}");
            messages.Add($"Target file path: {targetFilePath}");

            if (options.DryRun)
            {
                messages.Add("File move and rename skipped because dry-run mode is enabled.");

                return Complete(new InvoiceFileProcessingResult(
                    filePath,
                    FileProcessStatus.Success,
                    detectionResult.Source,
                    detectionResult.InvoiceNumber,
                    sanitizedFileName,
                    targetFilePath,
                    true,
                    true,
                    false,
                    messages,
                    ConfidenceScore: detectionResult.ConfidenceScore,
                    ConfidenceLevel: detectionResult.ConfidenceLevel,
                    IsUncertain: detectionResult.IsUncertain));
            }

            var renameResult = RenameSuccessfulFile(filePath, sanitizedFileName, options);
            messages.Add(renameResult.Message);
            if (!renameResult.Success)
            {
                var failedMoveResult = MoveFailedFile(filePath, options);
                messages.Add(failedMoveResult.Message);

                return Complete(new InvoiceFileProcessingResult(
                    filePath,
                    FileProcessStatus.Failed,
                    detectionResult.Source,
                    detectionResult.InvoiceNumber,
                    sanitizedFileName,
                    failedMoveResult.TargetFilePath ?? renameResult.TargetFilePath,
                    options.DryRun,
                    false,
                    failedMoveResult.Renamed,
                    messages,
                    ConfidenceScore: detectionResult.ConfidenceScore,
                    ConfidenceLevel: detectionResult.ConfidenceLevel,
                    IsUncertain: detectionResult.IsUncertain));
            }

            return Complete(new InvoiceFileProcessingResult(
                filePath,
                FileProcessStatus.Success,
                detectionResult.Source,
                detectionResult.InvoiceNumber,
                sanitizedFileName,
                renameResult.TargetFilePath,
                options.DryRun,
                false,
                renameResult.Renamed,
                messages,
                ConfidenceScore: detectionResult.ConfidenceScore,
                ConfidenceLevel: detectionResult.ConfidenceLevel,
                IsUncertain: detectionResult.IsUncertain));
        }
        catch (Exception exception)
        {
            messages.Add($"Unhandled processing error: {exception.Message}");

            return Complete(new InvoiceFileProcessingResult(
                filePath,
                FileProcessStatus.Failed,
                DetectionSource.None,
                null,
                null,
                null,
                options.DryRun,
                false,
                false,
                messages),
                exception.Message);
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

    private static void AddEmbeddedFileDiagnostics(PdfAnalysisResult analysisResult, ICollection<string> messages)
    {
        messages.Add($"Embedded file diagnostics: count={analysisResult.EmbeddedFiles.Count}");

        for (var index = 0; index < analysisResult.EmbeddedFiles.Count; index++)
        {
            var embeddedFile = analysisResult.EmbeddedFiles[index];
            messages.Add(
                $"Embedded file #{index + 1}: filename={embeddedFile.FileName}; byteLength={embeddedFile.Content.Length}; isXmlCandidate={(LooksLikeXmlCandidate(embeddedFile) ? "Yes" : "No")}; preview={BuildEmbeddedFilePreview(embeddedFile.Content)}");
        }
    }

    private static bool LooksLikeXmlCandidate(PdfEmbeddedFile embeddedFile)
    {
        if (embeddedFile.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(embeddedFile.MediaType, "XML", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var preview = DecodePreviewText(embeddedFile.Content).TrimStart('\0', '\uFEFF', ' ', '\r', '\n', '\t');
        return preview.StartsWith("<", StringComparison.Ordinal);
    }

    private static string BuildEmbeddedFilePreview(byte[] contentBytes)
    {
        if (contentBytes.Length == 0)
        {
            return "(empty)";
        }

        var preview = DecodePreviewText(contentBytes.Take(80).ToArray());
        var builder = new StringBuilder(preview.Length);
        foreach (var character in preview)
        {
            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        return builder.ToString().Trim();
    }

    private static string DecodePreviewText(byte[] contentBytes)
    {
        if (contentBytes.Length >= 2 && contentBytes[0] == 0xFE && contentBytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(contentBytes[2..]);
        }

        if (contentBytes.Length >= 2 && contentBytes[0] == 0xFF && contentBytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(contentBytes[2..]);
        }

        return Encoding.Latin1.GetString(contentBytes);
    }

    private void WriteAuditLogIfNeeded(
        InvoiceFileProcessingOptions options,
        InvoiceFileProcessingResult result,
        List<string> messages,
        string? error)
    {
        if (options.DryRun)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.AuditLogFolderPath))
        {
            messages.Add("Audit log entry was not written because no audit log folder was configured.");
            return;
        }

        var entry = new InvoiceAuditLogEntry(
            DateTimeOffset.Now,
            result.SourceFilePath,
            result.Status == FileProcessStatus.Success ? "Renamed" : "Failed",
            result.DetectionSource,
            result.InvoiceNumber,
            result.TargetFilePath,
            result.DryRunEnabled,
            result.Status == FileProcessStatus.Success,
            BuildAuditMessage(result),
            error,
            result.IsEncrypted,
            result.FailureReason,
            result.ConfidenceScore,
            result.ConfidenceLevel,
            result.IsUncertain);

        var auditResult = _auditLogService.Write(options.AuditLogFolderPath, entry);
        messages.Add(auditResult.Message);
        if (!string.IsNullOrWhiteSpace(auditResult.Error))
        {
            messages.Add($"Audit log error: {auditResult.Error}");
        }
    }

    private static string BuildAuditMessage(InvoiceFileProcessingResult result)
        => result.FailureReason == ProcessingFailureReason.PasswordProtected
            ? "Password-protected PDF"
            : result.Summary;

    private static string BuildXmlDetectionResultMessage(InvoiceNumberDetectionResult detectionResult)
        => $"XML detection result: success={(detectionResult.Success ? "Yes" : "No")}; source={detectionResult.Source}; invoiceNumber={detectionResult.InvoiceNumber ?? "(none)"}; confidence={detectionResult.ConfidenceScore:0.00} {detectionResult.ConfidenceLevel}; message={detectionResult.Message}";

    private static string BuildConfidenceMessage(
        double confidenceScore,
        ConfidenceLevel confidenceLevel,
        bool isUncertain)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"Detection confidence: {confidenceScore:0.00} ({confidenceLevel}), uncertain: {(isUncertain ? "Yes" : "No")}.");
}
