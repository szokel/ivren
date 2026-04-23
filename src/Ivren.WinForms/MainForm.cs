using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.WinForms;

public partial class MainForm : Form
{
    private const string DefaultSampleFolderPath = @"C:\repo\ivren\mintaszamla";
    private readonly IInvoiceFileProcessor _invoiceFileProcessor;

    public MainForm(IInvoiceFileProcessor invoiceFileProcessor)
    {
        _invoiceFileProcessor = invoiceFileProcessor ?? throw new ArgumentNullException(nameof(invoiceFileProcessor));

        InitializeComponent();
        ConfigureResultsGrid();
        folderPathTextBox.TextChanged += (_, _) => UpdateProcessFolderButtonState();
        InitializeDefaultFolder();
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
            InitialDirectory = Directory.Exists(folderPathTextBox.Text) ? folderPathTextBox.Text : string.Empty,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ApplySelectedFolder(dialog.SelectedPath);
        }
    }

    private async void processFolderButton_Click(object sender, EventArgs e)
    {
        var folderPath = folderPathTextBox.Text.Trim();
        if (!Directory.Exists(folderPath))
        {
            MessageBox.Show(this, "Please select an existing folder.", "Folder Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

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

        if (Directory.Exists(folderPathTextBox.Text))
        {
            dialog.InitialDirectory = folderPathTextBox.Text;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        folderPathTextBox.Text = Path.GetDirectoryName(dialog.FileName) ?? folderPathTextBox.Text;
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

    private void InitializeDefaultFolder()
    {
        if (!Directory.Exists(DefaultSampleFolderPath))
        {
            folderPathTextBox.Clear();
            UpdateProcessFolderButtonState();
            return;
        }

        ApplySelectedFolder(DefaultSampleFolderPath);
    }

    private void ApplySelectedFolder(string folderPath)
    {
        folderPathTextBox.Text = folderPath;
        AppendLog($"Folder selected: {folderPath}");
    }

    private void UpdateProcessFolderButtonState()
    {
        processFolderButton.Enabled = Directory.Exists(folderPathTextBox.Text.Trim());
    }

    private void ToggleUi(bool enabled)
    {
        browseFolderButton.Enabled = enabled;
        processFolderButton.Enabled = enabled && Directory.Exists(folderPathTextBox.Text.Trim());
        processSingleFileButton.Enabled = enabled;
        folderPathTextBox.Enabled = enabled;
        dryRunCheckBox.Enabled = enabled;
    }
}
