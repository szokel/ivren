namespace Ivren.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel layoutPanel;
    private TableLayoutPanel commandPanel;
    private FlowLayoutPanel actionPanel;
    private Label folderLabel;
    private ComboBox folderPathComboBox;
    private Button browseFolderButton;
    private Label renamedFolderLabel;
    private ComboBox renamedFolderComboBox;
    private Button browseRenamedFolderButton;
    private Label failedFolderLabel;
    private ComboBox failedFolderComboBox;
    private Button browseFailedFolderButton;
    private Label auditLogFolderLabel;
    private ComboBox auditLogFolderComboBox;
    private Button browseAuditLogFolderButton;
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
        commandPanel = new TableLayoutPanel();
        actionPanel = new FlowLayoutPanel();
        folderLabel = new Label();
        folderPathComboBox = new ComboBox();
        browseFolderButton = new Button();
        renamedFolderLabel = new Label();
        renamedFolderComboBox = new ComboBox();
        browseRenamedFolderButton = new Button();
        failedFolderLabel = new Label();
        failedFolderComboBox = new ComboBox();
        browseFailedFolderButton = new Button();
        auditLogFolderLabel = new Label();
        auditLogFolderComboBox = new ComboBox();
        browseAuditLogFolderButton = new Button();
        processFolderButton = new Button();
        processSingleFileButton = new Button();
        dryRunCheckBox = new CheckBox();
        resultsLabel = new Label();
        resultsGrid = new DataGridView();
        logLabel = new Label();
        logTextBox = new TextBox();
        layoutPanel.SuspendLayout();
        commandPanel.SuspendLayout();
        actionPanel.SuspendLayout();
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
        commandPanel.ColumnCount = 4;
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135F));
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118F));
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380F));
        commandPanel.Controls.Add(folderLabel, 0, 0);
        commandPanel.Controls.Add(folderPathComboBox, 1, 0);
        commandPanel.Controls.Add(browseFolderButton, 2, 0);
        commandPanel.Controls.Add(actionPanel, 3, 0);
        commandPanel.Controls.Add(renamedFolderLabel, 0, 1);
        commandPanel.Controls.Add(renamedFolderComboBox, 1, 1);
        commandPanel.Controls.Add(browseRenamedFolderButton, 2, 1);
        commandPanel.Controls.Add(failedFolderLabel, 0, 2);
        commandPanel.Controls.Add(failedFolderComboBox, 1, 2);
        commandPanel.Controls.Add(browseFailedFolderButton, 2, 2);
        commandPanel.Controls.Add(auditLogFolderLabel, 0, 3);
        commandPanel.Controls.Add(auditLogFolderComboBox, 1, 3);
        commandPanel.Controls.Add(browseAuditLogFolderButton, 2, 3);
        commandPanel.Dock = DockStyle.Fill;
        commandPanel.Location = new Point(15, 15);
        commandPanel.Name = "commandPanel";
        commandPanel.RowCount = 4;
        commandPanel.RowStyles.Add(new RowStyle());
        commandPanel.RowStyles.Add(new RowStyle());
        commandPanel.RowStyles.Add(new RowStyle());
        commandPanel.RowStyles.Add(new RowStyle());
        commandPanel.Size = new Size(1154, 140);
        commandPanel.TabIndex = 0;
        // 
        // actionPanel
        // 
        actionPanel.AutoSize = true;
        actionPanel.Controls.Add(processFolderButton);
        actionPanel.Controls.Add(processSingleFileButton);
        actionPanel.Controls.Add(dryRunCheckBox);
        actionPanel.Dock = DockStyle.Fill;
        actionPanel.Location = new Point(777, 0);
        actionPanel.Margin = new Padding(0);
        actionPanel.Name = "actionPanel";
        actionPanel.Size = new Size(377, 35);
        actionPanel.TabIndex = 3;
        actionPanel.WrapContents = false;
        // 
        // folderLabel
        // 
        folderLabel.Anchor = AnchorStyles.Left;
        folderLabel.AutoSize = true;
        folderLabel.Location = new Point(3, 10);
        folderLabel.Margin = new Padding(3, 8, 8, 0);
        folderLabel.Name = "folderLabel";
        folderLabel.Size = new Size(72, 15);
        folderLabel.TabIndex = 0;
        folderLabel.Text = "Input folder:";
        // 
        // folderPathComboBox
        // 
        folderPathComboBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        folderPathComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        folderPathComboBox.FormattingEnabled = true;
        folderPathComboBox.Location = new Point(138, 5);
        folderPathComboBox.Margin = new Padding(3, 5, 8, 3);
        folderPathComboBox.Name = "folderPathComboBox";
        folderPathComboBox.Size = new Size(513, 23);
        folderPathComboBox.TabIndex = 1;
        // 
        // browseFolderButton
        // 
        browseFolderButton.Location = new Point(659, 3);
        browseFolderButton.Name = "browseFolderButton";
        browseFolderButton.Size = new Size(110, 29);
        browseFolderButton.TabIndex = 2;
        browseFolderButton.Text = "Browse";
        browseFolderButton.UseVisualStyleBackColor = true;
        browseFolderButton.Click += browseFolderButton_Click;
        // 
        // renamedFolderLabel
        // 
        renamedFolderLabel.Anchor = AnchorStyles.Left;
        renamedFolderLabel.AutoSize = true;
        renamedFolderLabel.Location = new Point(3, 45);
        renamedFolderLabel.Margin = new Padding(3, 8, 8, 0);
        renamedFolderLabel.Name = "renamedFolderLabel";
        renamedFolderLabel.Size = new Size(120, 15);
        renamedFolderLabel.TabIndex = 4;
        renamedFolderLabel.Text = "Renamed PDFs folder:";
        // 
        // renamedFolderComboBox
        // 
        renamedFolderComboBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        renamedFolderComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        renamedFolderComboBox.FormattingEnabled = true;
        renamedFolderComboBox.Location = new Point(138, 40);
        renamedFolderComboBox.Margin = new Padding(3, 5, 8, 3);
        renamedFolderComboBox.Name = "renamedFolderComboBox";
        renamedFolderComboBox.Size = new Size(513, 23);
        renamedFolderComboBox.TabIndex = 5;
        // 
        // browseRenamedFolderButton
        // 
        browseRenamedFolderButton.Location = new Point(659, 38);
        browseRenamedFolderButton.Name = "browseRenamedFolderButton";
        browseRenamedFolderButton.Size = new Size(110, 29);
        browseRenamedFolderButton.TabIndex = 6;
        browseRenamedFolderButton.Text = "Browse";
        browseRenamedFolderButton.UseVisualStyleBackColor = true;
        browseRenamedFolderButton.Click += browseRenamedFolderButton_Click;
        // 
        // failedFolderLabel
        // 
        failedFolderLabel.Anchor = AnchorStyles.Left;
        failedFolderLabel.AutoSize = true;
        failedFolderLabel.Location = new Point(3, 80);
        failedFolderLabel.Margin = new Padding(3, 8, 8, 0);
        failedFolderLabel.Name = "failedFolderLabel";
        failedFolderLabel.Size = new Size(105, 15);
        failedFolderLabel.TabIndex = 7;
        failedFolderLabel.Text = "Failed PDFs folder:";
        // 
        // failedFolderComboBox
        // 
        failedFolderComboBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        failedFolderComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        failedFolderComboBox.FormattingEnabled = true;
        failedFolderComboBox.Location = new Point(138, 75);
        failedFolderComboBox.Margin = new Padding(3, 5, 8, 3);
        failedFolderComboBox.Name = "failedFolderComboBox";
        failedFolderComboBox.Size = new Size(513, 23);
        failedFolderComboBox.TabIndex = 8;
        // 
        // browseFailedFolderButton
        // 
        browseFailedFolderButton.Location = new Point(659, 73);
        browseFailedFolderButton.Name = "browseFailedFolderButton";
        browseFailedFolderButton.Size = new Size(110, 29);
        browseFailedFolderButton.TabIndex = 9;
        browseFailedFolderButton.Text = "Browse";
        browseFailedFolderButton.UseVisualStyleBackColor = true;
        browseFailedFolderButton.Click += browseFailedFolderButton_Click;
        // 
        // auditLogFolderLabel
        // 
        auditLogFolderLabel.Anchor = AnchorStyles.Left;
        auditLogFolderLabel.AutoSize = true;
        auditLogFolderLabel.Location = new Point(3, 115);
        auditLogFolderLabel.Margin = new Padding(3, 8, 8, 0);
        auditLogFolderLabel.Name = "auditLogFolderLabel";
        auditLogFolderLabel.Size = new Size(94, 15);
        auditLogFolderLabel.TabIndex = 10;
        auditLogFolderLabel.Text = "Audit log folder:";
        // 
        // auditLogFolderComboBox
        // 
        auditLogFolderComboBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        auditLogFolderComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        auditLogFolderComboBox.FormattingEnabled = true;
        auditLogFolderComboBox.Location = new Point(138, 110);
        auditLogFolderComboBox.Margin = new Padding(3, 5, 8, 3);
        auditLogFolderComboBox.Name = "auditLogFolderComboBox";
        auditLogFolderComboBox.Size = new Size(513, 23);
        auditLogFolderComboBox.TabIndex = 11;
        // 
        // browseAuditLogFolderButton
        // 
        browseAuditLogFolderButton.Location = new Point(659, 108);
        browseAuditLogFolderButton.Name = "browseAuditLogFolderButton";
        browseAuditLogFolderButton.Size = new Size(110, 29);
        browseAuditLogFolderButton.TabIndex = 12;
        browseAuditLogFolderButton.Text = "Browse";
        browseAuditLogFolderButton.UseVisualStyleBackColor = true;
        browseAuditLogFolderButton.Click += browseAuditLogFolderButton_Click;
        // 
        // processFolderButton
        // 
        processFolderButton.Location = new Point(3, 3);
        processFolderButton.Name = "processFolderButton";
        processFolderButton.Size = new Size(120, 29);
        processFolderButton.TabIndex = 0;
        processFolderButton.Text = "Process Folder";
        processFolderButton.UseVisualStyleBackColor = true;
        processFolderButton.Click += processFolderButton_Click;
        // 
        // processSingleFileButton
        // 
        processSingleFileButton.Location = new Point(129, 3);
        processSingleFileButton.Name = "processSingleFileButton";
        processSingleFileButton.Size = new Size(140, 29);
        processSingleFileButton.TabIndex = 1;
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
        dryRunCheckBox.Location = new Point(275, 8);
        dryRunCheckBox.Name = "dryRunCheckBox";
        dryRunCheckBox.Size = new Size(69, 19);
        dryRunCheckBox.TabIndex = 2;
        dryRunCheckBox.Text = "Dry Run";
        dryRunCheckBox.UseVisualStyleBackColor = true;
        // 
        // resultsLabel
        // 
        resultsLabel.AutoSize = true;
        resultsLabel.Location = new Point(15, 158);
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
        resultsGrid.Location = new Point(15, 188);
        resultsGrid.MultiSelect = false;
        resultsGrid.Name = "resultsGrid";
        resultsGrid.ReadOnly = true;
        resultsGrid.RowHeadersVisible = false;
        resultsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        resultsGrid.Size = new Size(1154, 263);
        resultsGrid.TabIndex = 2;
        // 
        // logLabel
        // 
        logLabel.AutoSize = true;
        logLabel.Location = new Point(15, 454);
        logLabel.Name = "logLabel";
        logLabel.Padding = new Padding(0, 8, 0, 4);
        logLabel.Size = new Size(28, 27);
        logLabel.TabIndex = 3;
        logLabel.Text = "Log";
        // 
        // logTextBox
        // 
        logTextBox.Dock = DockStyle.Fill;
        logTextBox.Location = new Point(15, 484);
        logTextBox.Multiline = true;
        logTextBox.Name = "logTextBox";
        logTextBox.ReadOnly = true;
        logTextBox.ScrollBars = ScrollBars.Both;
        logTextBox.Size = new Size(1154, 162);
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
        actionPanel.ResumeLayout(false);
        actionPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)resultsGrid).EndInit();
        ResumeLayout(false);
    }
}
