using System.Security.Principal;
using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.Service;

public sealed class IvrenWorker : BackgroundService
{
    private readonly ILogger<IvrenWorker> _logger;
    private readonly IInvoiceFileProcessor _invoiceFileProcessor;
    private readonly ServiceSettings _settings;
    private readonly ServiceRuntimePaths _runtimePaths;

    public IvrenWorker(
        ILogger<IvrenWorker> logger,
        IInvoiceFileProcessor invoiceFileProcessor,
        ServiceSettings settings,
        ServiceRuntimePaths runtimePaths)
    {
        _logger = logger;
        _invoiceFileProcessor = invoiceFileProcessor;
        _settings = settings;
        _runtimePaths = runtimePaths;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStartup();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ProcessCycle(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected processing cycle error.");
            }

            try
            {
                await Task.Delay(_settings.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Ivren service is stopping.");
    }

    private void LogStartup()
    {
        _logger.LogInformation("Ivren PDF Invoice Rename Service starting.");
        _logger.LogInformation("Running as Windows user: {User}", GetWindowsIdentityName());
        _logger.LogInformation("App base directory: {AppBaseDirectory}", _runtimePaths.AppBaseDirectory);
        _logger.LogInformation("Settings file: {SettingsPath}", _runtimePaths.SettingsPath);
        _logger.LogInformation("Supplier profiles file: {SupplierProfilesPath}", _runtimePaths.SupplierProfilesPath);
        _logger.LogInformation(
            "Configuration: InputFolder={InputFolder}; RenamedFolder={RenamedFolder}; FailedFolder={FailedFolder}; AuditLogFolder={AuditLogFolder}; PollIntervalSeconds={PollIntervalSeconds}; FileReadyDelaySeconds={FileReadyDelaySeconds}; DryRun={DryRun}",
            _settings.InputFolder,
            _settings.RenamedFolder,
            _settings.FailedFolder,
            _settings.AuditLogFolder,
            _settings.PollIntervalSeconds,
            _settings.FileReadyDelaySeconds,
            _settings.DryRun);
    }

    private void ProcessCycle(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing cycle started.");
        if (!ValidateConfiguredFolders())
        {
            _logger.LogWarning("Processing cycle skipped because one or more configured folders are missing.");
            return;
        }

        string[] pdfFiles;
        try
        {
            pdfFiles = Directory.GetFiles(_settings.InputFolder, "*.pdf", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or DirectoryNotFoundException)
        {
            _logger.LogError(exception, "Could not enumerate PDF files in input folder: {InputFolder}", _settings.InputFolder);
            return;
        }

        _logger.LogInformation("Processing cycle found {PdfCount} PDF file(s).", pdfFiles.Length);
        foreach (var pdfFile in pdfFiles)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            ProcessFileIfReady(pdfFile);
        }

        _logger.LogInformation("Processing cycle finished.");
    }

    private bool ValidateConfiguredFolders()
    {
        var valid = true;
        valid &= ValidateFolder("InputFolder", _settings.InputFolder);
        valid &= ValidateFolder("RenamedFolder", _settings.RenamedFolder);
        valid &= ValidateFolder("FailedFolder", _settings.FailedFolder);
        valid &= ValidateFolder("AuditLogFolder", _settings.AuditLogFolder);
        return valid;
    }

    private bool ValidateFolder(string settingName, string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            _logger.LogError("{SettingName} is empty.", settingName);
            return false;
        }

        if (Directory.Exists(folderPath))
        {
            return true;
        }

        _logger.LogError("{SettingName} does not exist: {FolderPath}", settingName, folderPath);
        return false;
    }

    private void ProcessFileIfReady(string pdfFile)
    {
        if (!IsFileReady(pdfFile, out var notReadyReason))
        {
            _logger.LogInformation("Skipping file that is not ready yet: {PdfFile}. Reason: {Reason}", pdfFile, notReadyReason);
            return;
        }

        _logger.LogInformation("Processing file started: {PdfFile}", pdfFile);

        try
        {
            var result = _invoiceFileProcessor.Process(
                pdfFile,
                new InvoiceFileProcessingOptions(
                    _settings.DryRun,
                    _settings.RenamedFolder,
                    _settings.FailedFolder,
                    _settings.AuditLogFolder));

            foreach (var message in result.Messages)
            {
                _logger.LogInformation("{CoreMessage}", message);
            }

            _logger.LogInformation(
                "Processing file finished: {PdfFile}. Status={Status}; Source={DetectionSource}; InvoiceNumber={InvoiceNumber}; Target={TargetFilePath}; Confidence={ConfidenceScore:0.00} {ConfidenceLevel}; Uncertain={IsUncertain}",
                pdfFile,
                result.Status,
                result.DetectionSource,
                result.InvoiceNumber ?? "(none)",
                result.TargetFilePath ?? "(none)",
                result.ConfidenceScore,
                result.ConfidenceLevel,
                result.IsUncertain);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Processing file failed with an unexpected exception: {PdfFile}", pdfFile);
        }
    }

    private bool IsFileReady(string filePath, out string reason)
    {
        if (!File.Exists(filePath))
        {
            reason = "File no longer exists.";
            return false;
        }

        DateTime lastWriteTimeUtc;
        try
        {
            lastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            reason = $"Could not read LastWriteTimeUtc. {exception.Message}";
            return false;
        }

        var age = DateTime.UtcNow - lastWriteTimeUtc;
        if (age < _settings.FileReadyDelay)
        {
            reason = $"File was modified {age.TotalSeconds:0.0}s ago; waiting for {_settings.FileReadyDelay.TotalSeconds:0.0}s.";
            return false;
        }

        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            reason = string.Empty;
            return stream.Length >= 0;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            reason = $"File is locked or cannot be opened exclusively. {exception.Message}";
            return false;
        }
    }

    private static string GetWindowsIdentityName()
    {
        try
        {
            return WindowsIdentity.GetCurrent().Name;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or InvalidOperationException)
        {
            return Environment.UserName;
        }
    }
}
