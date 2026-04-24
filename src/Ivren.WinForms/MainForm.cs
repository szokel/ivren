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

    public MainForm(IInvoiceFileProcessor invoiceFileProcessor)
    {
        _invoiceFileProcessor = invoiceFileProcessor ?? throw new ArgumentNullException(nameof(invoiceFileProcessor));

        InitializeComponent();
        ConfigureResultsGrid();
        folderPathComboBox.TextChanged += (_, _) => UpdateProcessFolderButtonState();
        InitializeStartupFolder();
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
            HeaderText = "Target File Name",
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
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the folder that contains invoice PDFs.",
            InitialDirectory = Directory.Exists(folderPathComboBox.Text) ? folderPathComboBox.Text : string.Empty,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ApplySelectedFolder(dialog.SelectedPath);
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

        SaveFolderUsage(folderPath);

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
            ? "Dry-run mode is enabled. Files will not be renamed."
            : "Dry-run mode is disabled. Successful detections will rename files.");

        var options = new InvoiceFileProcessingOptions(dryRunCheckBox.Checked);

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
        var targetFileName = string.IsNullOrWhiteSpace(result.TargetFilePath)
            ? string.Empty
            : Path.GetFileName(result.TargetFilePath);
        var renameAction = result.RenameSkippedDueToDryRun
            ? "Skipped (Dry Run)"
            : result.Renamed
                ? "Renamed"
                : "Not Renamed";

        resultsGrid.Rows.Add(
            Path.GetFileName(result.SourceFilePath),
            result.Status.ToString(),
            result.DetectionSource.ToString(),
            result.InvoiceNumber ?? string.Empty,
            targetFileName,
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

    private void InitializeStartupFolder()
    {
        var userStateLoaded = TryLoadUserState(out var userState);
        _folderHistory = BuildFolderHistory(userState);
        RefreshFolderHistoryDropdown();

        if (userStateLoaded
            && !string.IsNullOrWhiteSpace(userState.LastUsedFolder)
            && Directory.Exists(userState.LastUsedFolder))
        {
            ApplySelectedFolder(
                userState.LastUsedFolder,
                logMessage: $"Using last used folder: {userState.LastUsedFolder}");
            return;
        }

        if (!TryLoadStartupSettings(out var settings))
        {
            folderPathComboBox.Text = string.Empty;
            UpdateProcessFolderButtonState();
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultFolder))
        {
            AppendLog($"The settings file '{SettingsFileName}' does not define a usable DefaultFolder value.");
            folderPathComboBox.Text = string.Empty;
            UpdateProcessFolderButtonState();
            return;
        }

        if (!Directory.Exists(settings.DefaultFolder))
        {
            AppendLog($"The configured DefaultFolder does not exist: {settings.DefaultFolder}");
            folderPathComboBox.Text = string.Empty;
            UpdateProcessFolderButtonState();
            return;
        }

        ApplySelectedFolder(
            settings.DefaultFolder,
            logMessage: $"Using default folder: {settings.DefaultFolder}");
    }

    private void ApplySelectedFolder(string folderPath, string? logMessage = null)
    {
        folderPathComboBox.Text = folderPath;
        AppendLog(logMessage ?? $"Folder selected: {folderPath}");
    }

    private void SaveFolderUsage(string folderPath)
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

        TrySaveUserState(new UserState
        {
            LastUsedFolder = normalizedFolderPath,
            FolderHistory = _folderHistory.ToArray()
        });
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
        processFolderButton.Enabled = enabled && Directory.Exists(folderPathComboBox.Text.Trim());
        processSingleFileButton.Enabled = enabled;
        folderPathComboBox.Enabled = enabled;
        dryRunCheckBox.Enabled = enabled;
    }

    private sealed class StartupSettings
    {
        public string? DefaultFolder { get; init; }
    }

    private sealed class UserState
    {
        public string? LastUsedFolder { get; init; }

        public string[] FolderHistory { get; init; } = [];
    }
}
