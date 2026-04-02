using System;
using System.Drawing;
using System.Windows.Forms;

namespace OvlTestBench;

partial class OvlTestBenchForm {
    private System.ComponentModel.IContainer components = null;
    private FlowLayoutPanel mainColumn = null!;
    private Button startStopButton = null!;
    private Button diagButton = null!;
    private Label configLabel = null!;
    private ProgressBar progressBar = null!;
    private TreeView resultsTree = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel statusLabel = null!;
    private ToolStripStatusLabel timingLabel = null!;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing) {
        if (disposing && (components != null)) {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

  /// <summary>
  /// Required method for Designer support - do not modify
  /// the contents of this method with the code editor.
  /// </summary>
  private void InitializeComponent() {
    resultsTree = new TreeView();
    mainColumn = new FlowLayoutPanel();
    startStopButton = new Button();
    diagButton = new Button();
    configLabel = new Label();
    progressRow = new TableLayoutPanel();
    progressBar = new ProgressBar();
    statusStrip = new StatusStrip();
    statusLabel = new ToolStripStatusLabel();
    timingLabel = new ToolStripStatusLabel();
    mainColumn.SuspendLayout();
    progressRow.SuspendLayout();
    statusStrip.SuspendLayout();
    SuspendLayout();
    // 
    // resultsTree
    // 
    resultsTree.Font = new Font("Consolas", 9F);
    resultsTree.Location = new Point(3, 71);
    resultsTree.Name = "resultsTree";
    resultsTree.Size = new Size(399, 166);
    resultsTree.TabIndex = 0;
    // 
    // mainColumn
    // 
    mainColumn.AutoSize = true;
    mainColumn.Controls.Add(startStopButton);
    mainColumn.Controls.Add(diagButton);
    mainColumn.Controls.Add(configLabel);
    mainColumn.Controls.Add(progressRow);
    mainColumn.Controls.Add(resultsTree);
    mainColumn.Dock = DockStyle.Fill;
    mainColumn.Location = new Point(0, 0);
    mainColumn.Name = "mainColumn";
    mainColumn.Size = new Size(404, 241);
    mainColumn.TabIndex = 3;
    // 
    // startStopButton
    // 
    startStopButton.ImageAlign = ContentAlignment.MiddleLeft;
    startStopButton.Location = new Point(3, 3);
    startStopButton.Name = "startStopButton";
    startStopButton.Size = new Size(75, 26);
    startStopButton.TabIndex = 0;
    startStopButton.Text = "Start";
    startStopButton.TextImageRelation = TextImageRelation.ImageBeforeText;
    startStopButton.Click += StartStopButton_Click;
    // 
    // diagButton
    // 
    diagButton.ImageAlign = ContentAlignment.MiddleLeft;
    diagButton.Location = new Point(84, 3);
    diagButton.Name = "diagButton";
    diagButton.Size = new Size(130, 26);
    diagButton.TabIndex = 1;
    diagButton.Text = "Gather Diagnostics";
    diagButton.TextImageRelation = TextImageRelation.ImageBeforeText;
    diagButton.Click += GatherDiagnosticsButton_Click;
    // 
    // configLabel
    // 
    configLabel.AutoSize = true;
    mainColumn.SetFlowBreak(configLabel, true);
    configLabel.Location = new Point(222, 9);
    configLabel.Margin = new Padding(5, 9, 0, 0);
    configLabel.Name = "configLabel";
    configLabel.Size = new Size(46, 15);
    configLabel.TabIndex = 2;
    configLabel.Text = "Config:";
    configLabel.TextAlign = ContentAlignment.MiddleLeft;
    // 
    // progressRow
    // 
    progressRow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
    progressRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
    progressRow.ColumnCount = 1;
    progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
    progressRow.Controls.Add(progressBar, 0, 0);
    mainColumn.SetFlowBreak(progressRow, true);
    progressRow.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
    progressRow.Location = new Point(3, 35);
    progressRow.Name = "progressRow";
    progressRow.RowCount = 1;
    progressRow.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
    progressRow.Size = new Size(378, 30);
    progressRow.TabIndex = 4;
    // 
    // progressBar
    // 
    progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
    progressBar.Location = new Point(3, 3);
    progressBar.Name = "progressBar";
    progressBar.Size = new Size(372, 23);
    progressBar.TabIndex = 1;
    // 
    // statusStrip
    // 
    statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, timingLabel });
    statusStrip.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
    statusStrip.Location = new Point(0, 241);
    statusStrip.Name = "statusStrip";
    statusStrip.Size = new Size(404, 20);
    statusStrip.TabIndex = 5;
    // 
    // statusLabel
    // 
    statusLabel.Name = "statusLabel";
    statusLabel.Size = new Size(39, 15);
    statusLabel.Text = "Ready";
    statusLabel.Alignment = ToolStripItemAlignment.Left;
    // 
    // timingLabel
    // 
    timingLabel.Alignment = ToolStripItemAlignment.Right;
    timingLabel.Name = "timingLabel";
    timingLabel.Size = new Size(120, 15);
    timingLabel.Text = "ETA: About 5 minutes";
    // 
    // OvlTestBenchForm
    // 
    BackColor = SystemColors.Control;
    ClientSize = new Size(404, 261);
    Controls.Add(mainColumn);
    Controls.Add(statusStrip);
    Font = new Font("Segoe UI", 9F);
    ForeColor = SystemColors.ControlText;
    MinimumSize = new Size(420, 300);
    Name = "OvlTestBenchForm";
    StartPosition = FormStartPosition.CenterScreen;
    Text = "Frontier OVL Test Bench";
    Resize += OvlTestBenchForm_Resize;
    mainColumn.ResumeLayout(false);
    mainColumn.PerformLayout();
    progressRow.ResumeLayout(false);
    statusStrip.ResumeLayout(false);
    statusStrip.PerformLayout();
    ResumeLayout(false);
    PerformLayout();
  }

  private TableLayoutPanel progressRow;
}
