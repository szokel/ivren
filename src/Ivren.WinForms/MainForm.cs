using System.Diagnostics;
using System.Text.Json;
using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.WinForms;

public partial class MainForm : Form
{
    private const string SettingsFileName = "Ivren.WinForms.settings.json";
    private const string UserStateFileName = "Ivren.WinForms.user.json";
    private const int MaxFolderHistoryEntries = 10;
    private readonly IInvoiceFileProcessor _invoiceFileProcessor;
    private List<string> _folderHistory = [];
    private List<string> _renamedFolderHistory = [];
    private List<string> _failedFolderHistory = [];
    private List<string> _auditLogFolderHistory = [];
    private string? _lastUsedRenamedFolder;
    private string? _lastUsedFailedFolder;
    private string? _lastUsedAuditLogFolder;
    private string? _selectedResultPdfPath;

    public MainForm(IInvoiceFileProcessor invoiceFileProcessor)
    {
        _invoiceFileProcessor = invoiceFileProcessor ?? throw new ArgumentNullException(nameof(invoiceFileProcessor));

        InitializeComponent();
        ConfigureResultsGrid();
        resultsGrid.CellDoubleClick += resultsGrid_CellDoubleClick;
        folderPathComboBox.TextChanged += (_, _) => UpdateProcessFolderButtonState();
        InitializeStartupFolders();
        UpdateProcessFolderButtonState();
    }

    private void ConfigureResultsGrid()
    {
        resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "FileName",
            HeaderText = "File",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 35
        });

        resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "Status",
            Width = 80
        });

        resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "FailureReason",
            HeaderText = "Failure Reason",
            Width = 130
        });

        resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "DetectionSource",
            HeaderText = "Source",
            Width = 80
        });

        resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "InvoiceNumber",
            HeaderText = "Invoice Number",
            Width = 140
        });

        resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Confidence",
            HeaderText = "Confidence",
            Width = 90
        });

        resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ConfidenceLevel",
            HeaderText = "Confidence Level",
            Width = 115
        });

        resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Uncertain",
            HeaderText = "Uncertain",
            Width = 80
        });

        resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "TargetFileName",
            HeaderText = "Target Path",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 25
        });

        resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "RenameAction",
            HeaderText = "Rename",
            Width = 130
        });

        resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Message",
            HeaderText = "Message",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 40
        });
    }

    private void browseFolderButton_Click(object sender, EventArgs e)
    {
        if (TryBrowseForFolder("Select the folder that contains invoice PDFs.", folderPathComboBox.Text, out var selectedPath))
        {
            ApplySelectedFolder(selectedPath);
        }
    }

    private void browseRenamedFolderButton_Click(object sender, EventArgs e)
    {
        if (TryBrowseForFolder("Select the folder for successfully renamed PDFs.", renamedFolderComboBox.Text, out var selectedPath))
        {
            ApplyFolderText(renamedFolderComboBox, selectedPath, $"Renamed PDFs folder selected: {selectedPath}");
        }
    }

    private void browseFailedFolderButton_Click(object sender, EventArgs e)
    {
        if (TryBrowseForFolder("Select the folder for PDFs that could not be renamed.", failedFolderComboBox.Text, out var selectedPath))
        {
            ApplyFolderText(failedFolderComboBox, selectedPath, $"Failed PDFs folder selected: {selectedPath}");
        }
    }

    private void browseAuditLogFolderButton_Click(object sender, EventArgs e)
    {
        if (TryBrowseForFolder("Select the folder for audit log files.", auditLogFolderComboBox.Text, out var selectedPath))
        {
            ApplyFolderText(auditLogFolderComboBox, selectedPath, $"Audit log folder selected: {selectedPath}");
        }
    }

    private async void processFolderButton_Click(object sender, EventArgs e)
    {
        var folderPath = folderPathComboBox.Text.Trim();
        if (!Directory.Exists(folderPath))
        {
            MessageBox.Show(this, "Please select an existing folder.", "Folder Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!TryReadProcessingFolders(out var renamedFolderPath, out var failedFolderPath, out var auditLogFolderPath))
        {
            return;
        }

        SaveFolderUsage(folderPath, renamedFolderPath, failedFolderPath, auditLogFolderPath);

        var pdfFiles = Directory.GetFiles(folderPath, "*.pdf", SearchOption.TopDirectoryOnly)
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (pdfFiles.Length == 0)
        {
            MessageBox.Show(this, "The selected folder does not contain any PDF files.", "No PDFs Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await ProcessFilesAsync(pdfFiles, $"Processing {pdfFiles.Length} PDF file(s) from folder.");
    }

    private async void processSingleFileButton_Click(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Title = "Select a PDF invoice file",
            CheckFileExists = true,
            Multiselect = false
        };

        if (Directory.Exists(folderPathComboBox.Text))
        {
            dialog.InitialDirectory = folderPathComboBox.Text;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!TryReadProcessingFolders(out _, out _, out _))
        {
            return;
        }

        var selectedFolder = Path.GetDirectoryName(dialog.FileName);
        if (!string.IsNullOrWhiteSpace(selectedFolder))
        {
            ApplySelectedFolder(selectedFolder);
        }

        await ProcessFilesAsync([dialog.FileName], "Processing one selected PDF file.");
    }

    private async Task ProcessFilesAsync(IReadOnlyList<string> filePaths, string startMessage)
    {
        ToggleUi(false);
        resultsGrid.Rows.Clear();
        logTextBox.Clear();
        AppendLog(startMessage);
        AppendLog(dryRunCheckBox.Checked
            ? "Dry-run mode is enabled. Files will not be moved or renamed."
            : "Dry-run mode is disabled. Successful detections will move files to the renamed folder; failures will move to the failed folder.");

        var options = new InvoiceFileProcessingOptions(
            dryRunCheckBox.Checked,
            EmptyToNull(renamedFolderComboBox.Text.Trim()),
            EmptyToNull(failedFolderComboBox.Text.Trim()),
            EmptyToNull(auditLogFolderComboBox.Text.Trim()));

        foreach (var filePath in filePaths)
        {
            AppendLog($"Starting: {filePath}");
            var result = await Task.Run(() => _invoiceFileProcessor.Process(filePath, options));
            AddResultRow(result);

            foreach (var message in result.Messages)
            {
                AppendLog(message);
            }

            AppendLog(string.Empty);
        }

        AppendLog("Processing finished.");
        ToggleUi(true);
    }

    private void AddResultRow(InvoiceFileProcessingResult result)
    {
        var targetPath = string.IsNullOrWhiteSpace(result.TargetFilePath)
            ? string.Empty
            : result.TargetFilePath;
        var renameAction = result.RenameSkippedDueToDryRun
            ? "Skipped (Dry Run)"
            : result.Renamed
                ? "Moved"
                : "Not Renamed";

        var rowIndex = resultsGrid.Rows.Add(
            Path.GetFileName(result.SourceFilePath),
            result.Status.ToString(),
            result.FailureReason == ProcessingFailureReason.None ? string.Empty : result.FailureReason.ToString(),
            result.DetectionSource.ToString(),
            result.InvoiceNumber ?? string.Empty,
            result.ConfidenceScore.ToString("0.00"),
            result.ConfidenceLevel.ToString(),
            result.IsUncertain ? "Yes" : "No",
            targetPath,
            renameAction,
            result.Summary);

        resultsGrid.Rows[rowIndex].Tag = new ResultRowFilePaths(
            result.SourceFilePath,
            result.TargetFilePath);

        ApplyConfidenceRowStyle(resultsGrid.Rows[rowIndex], result);
    }

    private static void ApplyConfidenceRowStyle(DataGridViewRow row, InvoiceFileProcessingResult result)
    {
        if (result.ConfidenceLevel == ConfidenceLevel.Low || result.IsUncertain)
        {
            row.DefaultCellStyle.BackColor = Color.MistyRose;
            return;
        }

        if (result.ConfidenceLevel == ConfidenceLevel.Medium)
        {
            row.DefaultCellStyle.BackColor = Color.LemonChiffon;
        }
    }

    private void resultsGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= resultsGrid.Rows.Count)
        {
            return;
        }

        if (resultsGrid.Rows[e.RowIndex].Tag is not ResultRowFilePaths filePaths)
        {
            AppendLog("Could not open PDF because the selected result row has no file path information.");
            return;
        }

        var filePath = ResolveOpenableResultFilePath(filePaths);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            AppendLog($"Could not open PDF. Neither target nor original file exists. Original: {filePaths.SourceFilePath}; Target: {filePaths.TargetFilePath ?? "(none)"}");
            MessageBox.Show(this, "The PDF file could not be found. It may have been moved or deleted.", "PDF Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(filePath)
            {
                UseShellExecute = true
            });
            AppendLog($"Opened PDF: {filePath}");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            AppendLog($"Could not open PDF: {filePath}. {exception.Message}");
            MessageBox.Show(this, $"The PDF file could not be opened.\n\n{exception.Message}", "Open PDF Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string? ResolveOpenableResultFilePath(ResultRowFilePaths filePaths)
    {
        if (!string.IsNullOrWhiteSpace(filePaths.TargetFilePath) && File.Exists(filePaths.TargetFilePath))
        {
            return filePaths.TargetFilePath;
        }

        return File.Exists(filePaths.SourceFilePath)
            ? filePaths.SourceFilePath
            : null;
    }

    private void resultsGrid_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var hitTest = resultsGrid.HitTest(e.X, e.Y);
        if (hitTest.RowIndex < 0 || hitTest.RowIndex >= resultsGrid.Rows.Count)
        {
            _selectedResultPdfPath = null;
            copyPdfPathMenuItem.Enabled = false;
            openPdfLocationMenuItem.Enabled = false;
            resultsContextMenu.Show(resultsGrid, e.Location);
            return;
        }

        resultsGrid.ClearSelection();
        var row = resultsGrid.Rows[hitTest.RowIndex];
        row.Selected = true;
        resultsGrid.CurrentCell = row.Cells[Math.Max(hitTest.ColumnIndex, 0)];

        if (row.Tag is ResultRowFilePaths filePaths)
        {
            _selectedResultPdfPath = ResolveOpenableResultFilePath(filePaths);
        }
        else
        {
            _selectedResultPdfPath = null;
        }

        var hasValidPath = !string.IsNullOrWhiteSpace(_selectedResultPdfPath);
        copyPdfPathMenuItem.Enabled = hasValidPath;
        openPdfLocationMenuItem.Enabled = hasValidPath;

        if (!hasValidPath)
        {
            AppendLog("No existing PDF file is available for the selected result row.");
        }

        resultsContextMenu.Show(resultsGrid, e.Location);
    }

    private void copyPdfPathMenuItem_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedResultPdfPath))
        {
            AppendLog("Could not copy PDF path because no existing PDF file is available for the selected result row.");
            return;
        }

        Clipboard.SetText(_selectedResultPdfPath);
        AppendLog($"Copied PDF path to clipboard: {_selectedResultPdfPath}");
    }

    private void openPdfLocationMenuItem_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedResultPdfPath))
        {
            AppendLog("Could not open PDF location because no existing PDF file is available for the selected result row.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_selectedResultPdfPath}\"")
            {
                UseShellExecute = true
            });
            AppendLog($"Opened PDF location: {_selectedResultPdfPath}");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            AppendLog($"Could not open PDF location: {_selectedResultPdfPath}. {exception.Message}");
            MessageBox.Show(this, $"The PDF location could not be opened.\n\n{exception.Message}", "Open Location Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            logTextBox.AppendText(Environment.NewLine);
            return;
        }

        logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void InitializeStartupFolders()
    {
        var userStateLoaded = TryLoadUserState(out var userState);
        _folderHistory = BuildFolderHistory(userState.FolderHistory, userState.LastUsedFolder);
        _renamedFolderHistory = BuildFolderHistory(userState.RenamedFolderHistory, userState.LastUsedRenamedFolder);
        _failedFolderHistory = BuildFolderHistory(userState.FailedFolderHistory, userState.LastUsedFailedFolder);
        _auditLogFolderHistory = BuildFolderHistory(userState.AuditLogFolderHistory, userState.LastUsedAuditLogFolder);
        RefreshFolderHistoryDropdown(folderPathComboBox, _folderHistory);
        RefreshFolderHistoryDropdown(renamedFolderComboBox, _renamedFolderHistory);
        RefreshFolderHistoryDropdown(failedFolderComboBox, _failedFolderHistory);
        RefreshFolderHistoryDropdown(auditLogFolderComboBox, _auditLogFolderHistory);
        if (!string.IsNullOrWhiteSpace(userState.LastUsedRenamedFolder) && Directory.Exists(userState.LastUsedRenamedFolder))
        {
            _lastUsedRenamedFolder = NormalizeFolderPathForState(userState.LastUsedRenamedFolder);
        }

        if (!string.IsNullOrWhiteSpace(userState.LastUsedFailedFolder) && Directory.Exists(userState.LastUsedFailedFolder))
        {
            _lastUsedFailedFolder = NormalizeFolderPathForState(userState.LastUsedFailedFolder);
        }

        if (!string.IsNullOrWhiteSpace(userState.LastUsedAuditLogFolder) && Directory.Exists(userState.LastUsedAuditLogFolder))
        {
            _lastUsedAuditLogFolder = NormalizeFolderPathForState(userState.LastUsedAuditLogFolder);
        }

        TryLoadStartupSettings(out var settings);

        if (userStateLoaded
            && !string.IsNullOrWhiteSpace(userState.LastUsedFolder)
            && Directory.Exists(userState.LastUsedFolder))
        {
            ApplySelectedFolder(
                userState.LastUsedFolder,
                logMessage: $"Using last used folder: {userState.LastUsedFolder}");
        }
        else if (string.IsNullOrWhiteSpace(settings.DefaultFolder))
        {
            folderPathComboBox.Text = string.Empty;
            UpdateProcessFolderButtonState();
            AppendLog($"The settings file '{SettingsFileName}' does not define a usable DefaultFolder value.");
        }
        else if (!Directory.Exists(settings.DefaultFolder))
        {
            AppendLog($"The configured DefaultFolder does not exist: {settings.DefaultFolder}");
            folderPathComboBox.Text = string.Empty;
            UpdateProcessFolderButtonState();
        }
        else
        {
            ApplySelectedFolder(
                settings.DefaultFolder,
                logMessage: $"Using default folder: {settings.DefaultFolder}");
        }

        var activeRenamedFolder = ApplyStartupOutputFolder(
            renamedFolderComboBox,
            _lastUsedRenamedFolder,
            settings.RenamedFolder,
            "renamed PDFs");
        EnsureActiveFolderInHistory(renamedFolderComboBox, _renamedFolderHistory, activeRenamedFolder);

        var activeFailedFolder = ApplyStartupOutputFolder(
            failedFolderComboBox,
            _lastUsedFailedFolder,
            settings.FailedFolder,
            "failed PDFs");
        EnsureActiveFolderInHistory(failedFolderComboBox, _failedFolderHistory, activeFailedFolder);

        var activeAuditLogFolder = ApplyStartupOutputFolder(
            auditLogFolderComboBox,
            _lastUsedAuditLogFolder,
            settings.AuditLogFolder,
            "audit log");
        EnsureActiveFolderInHistory(auditLogFolderComboBox, _auditLogFolderHistory, activeAuditLogFolder);
    }

    private void ApplySelectedFolder(string folderPath, string? logMessage = null)
    {
        folderPathComboBox.Text = folderPath;
        AppendLog(logMessage ?? $"Folder selected: {folderPath}");
    }

    private void ApplyFolderText(ComboBox comboBox, string folderPath, string logMessage)
    {
        comboBox.Text = folderPath;
        AppendLog(logMessage);
    }

    private string? ApplyStartupOutputFolder(
        ComboBox comboBox,
        string? lastUsedFolder,
        string? configuredFolder,
        string displayName)
    {
        if (!string.IsNullOrWhiteSpace(lastUsedFolder))
        {
            var normalizedLastUsedFolder = NormalizeFolderPathForState(lastUsedFolder);
            if (Directory.Exists(normalizedLastUsedFolder))
            {
                comboBox.Text = normalizedLastUsedFolder;
                AppendLog($"Using last used {displayName} folder: {normalizedLastUsedFolder}");
                return normalizedLastUsedFolder;
            }
        }

        if (string.IsNullOrWhiteSpace(configuredFolder))
        {
            comboBox.Text = string.Empty;
            AppendLog($"The settings file '{SettingsFileName}' does not define a usable {displayName} folder value.");
            return null;
        }

        var normalizedConfiguredFolder = NormalizeFolderPathForState(configuredFolder);
        if (!Directory.Exists(normalizedConfiguredFolder)
            && !TryCreateStartupOutputFolder(normalizedConfiguredFolder, displayName))
        {
            comboBox.Text = string.Empty;
            return null;
        }

        comboBox.Text = normalizedConfiguredFolder;
        AppendLog($"Using default {displayName} folder: {normalizedConfiguredFolder}");
        return normalizedConfiguredFolder;
    }

    private bool TryCreateStartupOutputFolder(string folderPath, string displayName)
    {
        try
        {
            Directory.CreateDirectory(folderPath);
            AppendLog($"Created {displayName} folder: {folderPath}");
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            AppendLog($"The configured {displayName} folder could not be created: {folderPath}. {exception.Message}");
            return false;
        }
    }

    private void SaveFolderUsage(string folderPath, string renamedFolderPath, string failedFolderPath, string auditLogFolderPath)
    {
        var normalizedFolderPath = NormalizeFolderPathForState(folderPath);
        var updatedHistory = new List<string> { normalizedFolderPath };

        foreach (var existingFolder in _folderHistory)
        {
            if (updatedHistory.Contains(existingFolder, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            updatedHistory.Add(existingFolder);
            if (updatedHistory.Count == MaxFolderHistoryEntries)
            {
                break;
            }
        }

        _folderHistory = updatedHistory;
        RefreshFolderHistoryDropdown(folderPathComboBox, _folderHistory);
        folderPathComboBox.Text = normalizedFolderPath;
        if (Directory.Exists(renamedFolderPath))
        {
            _lastUsedRenamedFolder = NormalizeFolderPathForState(renamedFolderPath);
            _renamedFolderHistory = MoveFolderToHistoryFront(_renamedFolderHistory, _lastUsedRenamedFolder);
            RefreshFolderHistoryDropdown(renamedFolderComboBox, _renamedFolderHistory);
            renamedFolderComboBox.Text = _lastUsedRenamedFolder;
        }

        if (Directory.Exists(failedFolderPath))
        {
            _lastUsedFailedFolder = NormalizeFolderPathForState(failedFolderPath);
            _failedFolderHistory = MoveFolderToHistoryFront(_failedFolderHistory, _lastUsedFailedFolder);
            RefreshFolderHistoryDropdown(failedFolderComboBox, _failedFolderHistory);
            failedFolderComboBox.Text = _lastUsedFailedFolder;
        }

        if (Directory.Exists(auditLogFolderPath))
        {
            _lastUsedAuditLogFolder = NormalizeFolderPathForState(auditLogFolderPath);
            _auditLogFolderHistory = MoveFolderToHistoryFront(_auditLogFolderHistory, _lastUsedAuditLogFolder);
            RefreshFolderHistoryDropdown(auditLogFolderComboBox, _auditLogFolderHistory);
            auditLogFolderComboBox.Text = _lastUsedAuditLogFolder;
        }

        TrySaveUserState(new UserState
        {
            LastUsedFolder = normalizedFolderPath,
            FolderHistory = _folderHistory.ToArray(),
            LastUsedRenamedFolder = _lastUsedRenamedFolder,
            LastUsedFailedFolder = _lastUsedFailedFolder,
            LastUsedAuditLogFolder = _lastUsedAuditLogFolder,
            RenamedFolderHistory = _renamedFolderHistory.ToArray(),
            FailedFolderHistory = _failedFolderHistory.ToArray(),
            AuditLogFolderHistory = _auditLogFolderHistory.ToArray()
        });
    }

    private bool TryBrowseForFolder(string description, string currentPath, out string selectedPath)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            InitialDirectory = Directory.Exists(currentPath) ? currentPath : string.Empty,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            selectedPath = dialog.SelectedPath;
            return true;
        }

        selectedPath = string.Empty;
        return false;
    }

    private static void RefreshFolderHistoryDropdown(ComboBox comboBox, IReadOnlyList<string> history)
    {
        comboBox.BeginUpdate();
        try
        {
            comboBox.Items.Clear();
            comboBox.Items.AddRange(history.Cast<object>().ToArray());
        }
        finally
        {
            comboBox.EndUpdate();
        }
    }

    private static List<string> BuildFolderHistory(IReadOnlyList<string> savedHistory, string? activeFolder)
    {
        var history = new List<string>();
        foreach (var folderPath in savedHistory)
        {
            AddFolderToHistory(history, folderPath);
            if (history.Count == MaxFolderHistoryEntries)
            {
                break;
            }
        }

        var activeFolderPath = string.IsNullOrWhiteSpace(activeFolder)
            ? string.Empty
            : NormalizeFolderPathForState(activeFolder);

        if (!string.IsNullOrWhiteSpace(activeFolderPath)
            && Directory.Exists(activeFolderPath)
            && !history.Contains(activeFolderPath, StringComparer.OrdinalIgnoreCase))
        {
            history.Insert(0, activeFolderPath);
        }

        return history
            .Take(MaxFolderHistoryEntries)
            .ToList();
    }

    private static void EnsureActiveFolderInHistory(ComboBox comboBox, List<string> history, string? activeFolder)
    {
        if (string.IsNullOrWhiteSpace(activeFolder) || !Directory.Exists(activeFolder))
        {
            return;
        }

        var normalizedFolderPath = NormalizeFolderPathForState(activeFolder);
        if (!history.Contains(normalizedFolderPath, StringComparer.OrdinalIgnoreCase))
        {
            history.Insert(0, normalizedFolderPath);
            if (history.Count > MaxFolderHistoryEntries)
            {
                history.RemoveRange(MaxFolderHistoryEntries, history.Count - MaxFolderHistoryEntries);
            }
        }

        RefreshFolderHistoryDropdown(comboBox, history);
    }

    private static List<string> MoveFolderToHistoryFront(IReadOnlyList<string> history, string folderPath)
    {
        var normalizedFolderPath = NormalizeFolderPathForState(folderPath);
        var updatedHistory = new List<string> { normalizedFolderPath };

        foreach (var existingFolder in history)
        {
            if (updatedHistory.Contains(existingFolder, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            updatedHistory.Add(existingFolder);
            if (updatedHistory.Count == MaxFolderHistoryEntries)
            {
                break;
            }
        }

        return updatedHistory;
    }

    private static void AddFolderToHistory(List<string> history, string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        var normalizedFolderPath = NormalizeFolderPathForState(folderPath);
        if (history.Contains(normalizedFolderPath, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        history.Add(normalizedFolderPath);
    }

    private static string NormalizeFolderPathForState(string folderPath)
    {
        try
        {
            return Path.GetFullPath(folderPath.Trim());
        }
        catch
        {
            return folderPath.Trim();
        }
    }

    private bool TryLoadStartupSettings(out StartupSettings settings)
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        if (!File.Exists(settingsPath))
        {
            AppendLog($"Settings file not found: {settingsPath}");
            settings = new StartupSettings();
            return false;
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            settings = JsonSerializer.Deserialize<StartupSettings>(json) ?? new StartupSettings();
            return true;
        }
        catch (JsonException)
        {
            AppendLog($"Settings file is not valid JSON: {settingsPath}");
            settings = new StartupSettings();
            return false;
        }
        catch (IOException)
        {
            AppendLog($"Settings file could not be read: {settingsPath}");
            settings = new StartupSettings();
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            AppendLog($"Settings file could not be accessed: {settingsPath}");
            settings = new StartupSettings();
            return false;
        }
    }

    private bool TryLoadUserState(out UserState userState)
    {
        var userStatePath = GetUserStateFilePath();
        if (!File.Exists(userStatePath))
        {
            userState = new UserState();
            return false;
        }

        try
        {
            var json = File.ReadAllText(userStatePath);
            userState = JsonSerializer.Deserialize<UserState>(json) ?? new UserState();
            return true;
        }
        catch (JsonException)
        {
            AppendLog($"User-state file is not valid JSON: {userStatePath}");
            userState = new UserState();
            return false;
        }
        catch (IOException)
        {
            AppendLog($"User-state file could not be read: {userStatePath}");
            userState = new UserState();
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            AppendLog($"User-state file could not be accessed: {userStatePath}");
            userState = new UserState();
            return false;
        }
    }

    private void TrySaveUserState(UserState userState)
    {
        var userStatePath = GetUserStateFilePath();

        try
        {
            var directoryPath = Path.GetDirectoryName(userStatePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var json = JsonSerializer.Serialize(userState, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(userStatePath, json);
        }
        catch (IOException)
        {
            AppendLog($"User-state file could not be written: {userStatePath}");
        }
        catch (UnauthorizedAccessException)
        {
            AppendLog($"User-state file could not be accessed for writing: {userStatePath}");
        }
    }

    private static string GetUserStateFilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Ivren", UserStateFileName);
    }

    private void UpdateProcessFolderButtonState()
    {
        processFolderButton.Enabled = Directory.Exists(folderPathComboBox.Text.Trim());
    }

    private void ToggleUi(bool enabled)
    {
        browseFolderButton.Enabled = enabled;
        browseRenamedFolderButton.Enabled = enabled;
        browseFailedFolderButton.Enabled = enabled;
        browseAuditLogFolderButton.Enabled = enabled;
        processFolderButton.Enabled = enabled && Directory.Exists(folderPathComboBox.Text.Trim());
        processSingleFileButton.Enabled = enabled;
        folderPathComboBox.Enabled = enabled;
        renamedFolderComboBox.Enabled = enabled;
        failedFolderComboBox.Enabled = enabled;
        auditLogFolderComboBox.Enabled = enabled;
        dryRunCheckBox.Enabled = enabled;
    }

    private static string? EmptyToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private bool TryReadProcessingFolders(out string renamedFolderPath, out string failedFolderPath, out string auditLogFolderPath)
    {
        renamedFolderPath = renamedFolderComboBox.Text.Trim();
        failedFolderPath = failedFolderComboBox.Text.Trim();
        auditLogFolderPath = auditLogFolderComboBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(renamedFolderPath))
        {
            MessageBox.Show(this, "Please enter the renamed PDFs folder.", "Renamed Folder Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(failedFolderPath))
        {
            MessageBox.Show(this, "Please enter the failed PDFs folder.", "Failed Folder Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (dryRunCheckBox.Checked)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(auditLogFolderPath))
        {
            MessageBox.Show(this, "Please enter the audit log folder.", "Audit Log Folder Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (!Directory.Exists(renamedFolderPath))
        {
            MessageBox.Show(this, "The renamed PDFs folder must exist before non-dry-run processing.", "Renamed Folder Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (!Directory.Exists(failedFolderPath))
        {
            MessageBox.Show(this, "The failed PDFs folder must exist before non-dry-run processing.", "Failed Folder Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (!Directory.Exists(auditLogFolderPath))
        {
            MessageBox.Show(this, "The audit log folder must exist before non-dry-run processing.", "Audit Log Folder Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private sealed class StartupSettings
    {
        public string? DefaultFolder { get; init; }

        public string? RenamedFolder { get; init; }

        public string? FailedFolder { get; init; }

        public string? AuditLogFolder { get; init; }
    }

    private sealed class UserState
    {
        public string? LastUsedFolder { get; init; }

        public string? LastUsedRenamedFolder { get; init; }

        public string? LastUsedFailedFolder { get; init; }

        public string? LastUsedAuditLogFolder { get; init; }

        public string[] FolderHistory { get; init; } = [];

        public string[] RenamedFolderHistory { get; init; } = [];

        public string[] FailedFolderHistory { get; init; } = [];

        public string[] AuditLogFolderHistory { get; init; } = [];
    }

    private sealed record ResultRowFilePaths(
        string SourceFilePath,
        string? TargetFilePath);
}
