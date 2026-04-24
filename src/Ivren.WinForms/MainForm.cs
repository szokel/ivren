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
    private string? _lastUsedRenamedFolder;
    private string? _lastUsedFailedFolder;
    private string? _lastUsedAuditLogFolder;

    public MainForm(IInvoiceFileProcessor invoiceFileProcessor)
    {
        _invoiceFileProcessor = invoiceFileProcessor ?? throw new ArgumentNullException(nameof(invoiceFileProcessor));

        InitializeComponent();
        ConfigureResultsGrid();
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
        if (TryBrowseForFolder("Select the folder for successfully renamed PDFs.", renamedFolderTextBox.Text, out var selectedPath))
        {
            ApplyFolderText(renamedFolderTextBox, selectedPath, $"Renamed PDFs folder selected: {selectedPath}");
        }
    }

    private void browseFailedFolderButton_Click(object sender, EventArgs e)
    {
        if (TryBrowseForFolder("Select the folder for PDFs that could not be renamed.", failedFolderTextBox.Text, out var selectedPath))
        {
            ApplyFolderText(failedFolderTextBox, selectedPath, $"Failed PDFs folder selected: {selectedPath}");
        }
    }

    private void browseAuditLogFolderButton_Click(object sender, EventArgs e)
    {
        if (TryBrowseForFolder("Select the folder for audit log files.", auditLogFolderTextBox.Text, out var selectedPath))
        {
            ApplyFolderText(auditLogFolderTextBox, selectedPath, $"Audit log folder selected: {selectedPath}");
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
            EmptyToNull(renamedFolderTextBox.Text.Trim()),
            EmptyToNull(failedFolderTextBox.Text.Trim()),
            EmptyToNull(auditLogFolderTextBox.Text.Trim()));

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

        resultsGrid.Rows.Add(
            Path.GetFileName(result.SourceFilePath),
            result.Status.ToString(),
            result.DetectionSource.ToString(),
            result.InvoiceNumber ?? string.Empty,
            targetPath,
            renameAction,
            result.Summary);
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
        _folderHistory = BuildFolderHistory(userState);
        RefreshFolderHistoryDropdown();
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

        ApplyStartupOutputFolder(
            renamedFolderTextBox,
            _lastUsedRenamedFolder,
            settings.RenamedFolder,
            "renamed PDFs");

        ApplyStartupOutputFolder(
            failedFolderTextBox,
            _lastUsedFailedFolder,
            settings.FailedFolder,
            "failed PDFs");

        ApplyStartupOutputFolder(
            auditLogFolderTextBox,
            _lastUsedAuditLogFolder,
            settings.AuditLogFolder,
            "audit log");
    }

    private void ApplySelectedFolder(string folderPath, string? logMessage = null)
    {
        folderPathComboBox.Text = folderPath;
        AppendLog(logMessage ?? $"Folder selected: {folderPath}");
    }

    private void ApplyFolderText(TextBox textBox, string folderPath, string logMessage)
    {
        textBox.Text = folderPath;
        AppendLog(logMessage);
    }

    private void ApplyStartupOutputFolder(
        TextBox textBox,
        string? lastUsedFolder,
        string? configuredFolder,
        string displayName)
    {
        if (!string.IsNullOrWhiteSpace(lastUsedFolder) && Directory.Exists(lastUsedFolder))
        {
            textBox.Text = lastUsedFolder;
            AppendLog($"Using last used {displayName} folder: {lastUsedFolder}");
            return;
        }

        if (string.IsNullOrWhiteSpace(configuredFolder))
        {
            textBox.Text = string.Empty;
            AppendLog($"The settings file '{SettingsFileName}' does not define a usable {displayName} folder value.");
            return;
        }

        if (!Directory.Exists(configuredFolder))
        {
            textBox.Text = string.Empty;
            AppendLog($"The configured {displayName} folder does not exist: {configuredFolder}");
            return;
        }

        textBox.Text = configuredFolder;
        AppendLog($"Using default {displayName} folder: {configuredFolder}");
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
        RefreshFolderHistoryDropdown();
        folderPathComboBox.Text = normalizedFolderPath;
        if (Directory.Exists(renamedFolderPath))
        {
            _lastUsedRenamedFolder = NormalizeFolderPathForState(renamedFolderPath);
            renamedFolderTextBox.Text = _lastUsedRenamedFolder;
        }

        if (Directory.Exists(failedFolderPath))
        {
            _lastUsedFailedFolder = NormalizeFolderPathForState(failedFolderPath);
            failedFolderTextBox.Text = _lastUsedFailedFolder;
        }

        if (Directory.Exists(auditLogFolderPath))
        {
            _lastUsedAuditLogFolder = NormalizeFolderPathForState(auditLogFolderPath);
            auditLogFolderTextBox.Text = _lastUsedAuditLogFolder;
        }

        TrySaveUserState(new UserState
        {
            LastUsedFolder = normalizedFolderPath,
            FolderHistory = _folderHistory.ToArray(),
            LastUsedRenamedFolder = _lastUsedRenamedFolder,
            LastUsedFailedFolder = _lastUsedFailedFolder,
            LastUsedAuditLogFolder = _lastUsedAuditLogFolder
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

    private void RefreshFolderHistoryDropdown()
    {
        folderPathComboBox.BeginUpdate();
        try
        {
            folderPathComboBox.Items.Clear();
            folderPathComboBox.Items.AddRange(_folderHistory.Cast<object>().ToArray());
        }
        finally
        {
            folderPathComboBox.EndUpdate();
        }
    }

    private static List<string> BuildFolderHistory(UserState userState)
    {
        var history = new List<string>();
        foreach (var folderPath in userState.FolderHistory)
        {
            AddFolderToHistory(history, folderPath);
            if (history.Count == MaxFolderHistoryEntries)
            {
                break;
            }
        }

        var lastUsedFolder = string.IsNullOrWhiteSpace(userState.LastUsedFolder)
            ? string.Empty
            : NormalizeFolderPathForState(userState.LastUsedFolder);

        if (!string.IsNullOrWhiteSpace(lastUsedFolder)
            && Directory.Exists(lastUsedFolder)
            && !history.Contains(lastUsedFolder, StringComparer.OrdinalIgnoreCase))
        {
            history.Insert(0, lastUsedFolder);
        }

        return history
            .Take(MaxFolderHistoryEntries)
            .ToList();
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
        renamedFolderTextBox.Enabled = enabled;
        failedFolderTextBox.Enabled = enabled;
        auditLogFolderTextBox.Enabled = enabled;
        dryRunCheckBox.Enabled = enabled;
    }

    private static string? EmptyToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private bool TryReadProcessingFolders(out string renamedFolderPath, out string failedFolderPath, out string auditLogFolderPath)
    {
        renamedFolderPath = renamedFolderTextBox.Text.Trim();
        failedFolderPath = failedFolderTextBox.Text.Trim();
        auditLogFolderPath = auditLogFolderTextBox.Text.Trim();

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
    }
}
