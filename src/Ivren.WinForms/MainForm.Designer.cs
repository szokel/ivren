namespace Ivren.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel layoutPanel;
    private FlowLayoutPanel commandPanel;
    private Label folderLabel;
    private ComboBox folderPathComboBox;
    private Button browseFolderButton;
    private Button processFolderButton;
    private Button processSingleFileButton;
    private CheckBox dryRunCheckBox;
    private DataGridView resultsGrid;
    private TextBox logTextBox;
    private Label resultsLabel;
    private Label logLabel;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        layoutPanel = new TableLayoutPanel();
        commandPanel = new FlowLayoutPanel();
        folderLabel = new Label();
        folderPathComboBox = new ComboBox();
        browseFolderButton = new Button();
        processFolderButton = new Button();
        processSingleFileButton = new Button();
        dryRunCheckBox = new CheckBox();
        resultsLabel = new Label();
        resultsGrid = new DataGridView();
        logLabel = new Label();
        logTextBox = new TextBox();
        layoutPanel.SuspendLayout();
        commandPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)resultsGrid).BeginInit();
        SuspendLayout();
        // 
        // layoutPanel
        // 
        layoutPanel.ColumnCount = 1;
        layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutPanel.Controls.Add(commandPanel, 0, 0);
        layoutPanel.Controls.Add(resultsLabel, 0, 1);
        layoutPanel.Controls.Add(resultsGrid, 0, 2);
        layoutPanel.Controls.Add(logLabel, 0, 3);
        layoutPanel.Controls.Add(logTextBox, 0, 4);
        layoutPanel.Dock = DockStyle.Fill;
        layoutPanel.Location = new Point(0, 0);
        layoutPanel.Name = "layoutPanel";
        layoutPanel.Padding = new Padding(12);
        layoutPanel.RowCount = 5;
        layoutPanel.RowStyles.Add(new RowStyle());
        layoutPanel.RowStyles.Add(new RowStyle());
        layoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
        layoutPanel.RowStyles.Add(new RowStyle());
        layoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
        layoutPanel.Size = new Size(1184, 661);
        layoutPanel.TabIndex = 0;
        // 
        // commandPanel
        // 
        commandPanel.AutoSize = true;
        commandPanel.Controls.Add(folderLabel);
        commandPanel.Controls.Add(folderPathComboBox);
        commandPanel.Controls.Add(browseFolderButton);
        commandPanel.Controls.Add(processFolderButton);
        commandPanel.Controls.Add(processSingleFileButton);
        commandPanel.Controls.Add(dryRunCheckBox);
        commandPanel.Dock = DockStyle.Fill;
        commandPanel.Location = new Point(15, 15);
        commandPanel.Name = "commandPanel";
        commandPanel.Size = new Size(1154, 38);
        commandPanel.TabIndex = 0;
        commandPanel.WrapContents = false;
        // 
        // folderLabel
        // 
        folderLabel.Anchor = AnchorStyles.Left;
        folderLabel.AutoSize = true;
        folderLabel.Location = new Point(3, 10);
        folderLabel.Margin = new Padding(3, 10, 8, 0);
        folderLabel.Name = "folderLabel";
        folderLabel.Size = new Size(42, 15);
        folderLabel.TabIndex = 0;
        folderLabel.Text = "Folder:";
        // 
        // folderPathComboBox
        // 
        folderPathComboBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        folderPathComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        folderPathComboBox.FormattingEnabled = true;
        folderPathComboBox.Location = new Point(56, 5);
        folderPathComboBox.Margin = new Padding(3, 5, 8, 3);
        folderPathComboBox.Name = "folderPathComboBox";
        folderPathComboBox.Size = new Size(520, 23);
        folderPathComboBox.TabIndex = 1;
        // 
        // browseFolderButton
        // 
        browseFolderButton.Location = new Point(587, 3);
        browseFolderButton.Name = "browseFolderButton";
        browseFolderButton.Size = new Size(110, 29);
        browseFolderButton.TabIndex = 2;
        browseFolderButton.Text = "Browse Folder";
        browseFolderButton.UseVisualStyleBackColor = true;
        browseFolderButton.Click += browseFolderButton_Click;
        // 
        // processFolderButton
        // 
        processFolderButton.Location = new Point(703, 3);
        processFolderButton.Name = "processFolderButton";
        processFolderButton.Size = new Size(140, 29);
        processFolderButton.TabIndex = 3;
        processFolderButton.Text = "Process Folder";
        processFolderButton.UseVisualStyleBackColor = true;
        processFolderButton.Click += processFolderButton_Click;
        // 
        // processSingleFileButton
        // 
        processSingleFileButton.Location = new Point(849, 3);
        processSingleFileButton.Name = "processSingleFileButton";
        processSingleFileButton.Size = new Size(150, 29);
        processSingleFileButton.TabIndex = 4;
        processSingleFileButton.Text = "Process Single PDF";
        processSingleFileButton.UseVisualStyleBackColor = true;
        processSingleFileButton.Click += processSingleFileButton_Click;
        // 
        // dryRunCheckBox
        // 
        dryRunCheckBox.Anchor = AnchorStyles.Left;
        dryRunCheckBox.AutoSize = true;
        dryRunCheckBox.Checked = true;
        dryRunCheckBox.CheckState = CheckState.Checked;
        dryRunCheckBox.Location = new Point(1005, 8);
        dryRunCheckBox.Name = "dryRunCheckBox";
        dryRunCheckBox.Size = new Size(69, 19);
        dryRunCheckBox.TabIndex = 5;
        dryRunCheckBox.Text = "Dry Run";
        dryRunCheckBox.UseVisualStyleBackColor = true;
        // 
        // resultsLabel
        // 
        resultsLabel.AutoSize = true;
        resultsLabel.Location = new Point(15, 56);
        resultsLabel.Name = "resultsLabel";
        resultsLabel.Padding = new Padding(0, 8, 0, 4);
        resultsLabel.Size = new Size(45, 27);
        resultsLabel.TabIndex = 1;
        resultsLabel.Text = "Results";
        // 
        // resultsGrid
        // 
        resultsGrid.AllowUserToAddRows = false;
        resultsGrid.AllowUserToDeleteRows = false;
        resultsGrid.AllowUserToResizeRows = false;
        resultsGrid.BackgroundColor = SystemColors.Window;
        resultsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        resultsGrid.Dock = DockStyle.Fill;
        resultsGrid.Location = new Point(15, 86);
        resultsGrid.MultiSelect = false;
        resultsGrid.Name = "resultsGrid";
        resultsGrid.ReadOnly = true;
        resultsGrid.RowHeadersVisible = false;
        resultsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        resultsGrid.Size = new Size(1154, 324);
        resultsGrid.TabIndex = 2;
        // 
        // logLabel
        // 
        logLabel.AutoSize = true;
        logLabel.Location = new Point(15, 413);
        logLabel.Name = "logLabel";
        logLabel.Padding = new Padding(0, 8, 0, 4);
        logLabel.Size = new Size(28, 27);
        logLabel.TabIndex = 3;
        logLabel.Text = "Log";
        // 
        // logTextBox
        // 
        logTextBox.Dock = DockStyle.Fill;
        logTextBox.Location = new Point(15, 443);
        logTextBox.Multiline = true;
        logTextBox.Name = "logTextBox";
        logTextBox.ReadOnly = true;
        logTextBox.ScrollBars = ScrollBars.Both;
        logTextBox.Size = new Size(1154, 203);
        logTextBox.TabIndex = 4;
        logTextBox.WordWrap = false;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1184, 661);
        Controls.Add(layoutPanel);
        MinimumSize = new Size(1000, 620);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Ivren Invoice PDF Renamer";
        layoutPanel.ResumeLayout(false);
        layoutPanel.PerformLayout();
        commandPanel.ResumeLayout(false);
        commandPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)resultsGrid).EndInit();
        ResumeLayout(false);
    }
}
