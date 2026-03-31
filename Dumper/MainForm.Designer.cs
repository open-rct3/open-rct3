namespace Dumper
{
  partial class MainForm
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent() {
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
      menuStrip = new System.Windows.Forms.MenuStrip();
      fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      toolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
      exportToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
      exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      undoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      redoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
      cutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      pasteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
      selectAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      contentsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      indexToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      searchToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
      aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      statusStrip = new System.Windows.Forms.StatusStrip();
      statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
      progressBar = new System.Windows.Forms.ToolStripProgressBar();
      openDialog = new System.Windows.Forms.OpenFileDialog();
      splitView = new System.Windows.Forms.SplitContainer();
      toolStrip = new System.Windows.Forms.ToolStrip();
      helpToolStripButton = new System.Windows.Forms.ToolStripButton();
      openArchiveToolStripButton = new System.Windows.Forms.ToolStripButton();
      treeView = new System.Windows.Forms.TreeView();
      menuStrip.SuspendLayout();
      statusStrip.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)splitView).BeginInit();
      splitView.Panel1.SuspendLayout();
      splitView.SuspendLayout();
      toolStrip.SuspendLayout();
      SuspendLayout();
      // 
      // menuStrip
      // 
      menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem, toolsToolStripMenuItem, helpToolStripMenuItem });
      menuStrip.Location = new System.Drawing.Point(0, 0);
      menuStrip.Name = "menuStrip";
      menuStrip.Size = new System.Drawing.Size(584, 24);
      menuStrip.TabIndex = 0;
      menuStrip.Text = "menuStrip";
      // 
      // fileToolStripMenuItem
      // 
      fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { openToolStripMenuItem, toolStripSeparator, exportToolStripMenuItem, toolStripSeparator1, exitToolStripMenuItem });
      fileToolStripMenuItem.Name = "fileToolStripMenuItem";
      fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
      fileToolStripMenuItem.Text = "&File";
      // 
      // openToolStripMenuItem
      // 
      openToolStripMenuItem.Image = (System.Drawing.Image)resources.GetObject("openToolStripMenuItem.Image");
      openToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Magenta;
      openToolStripMenuItem.Name = "openToolStripMenuItem";
      openToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O;
      openToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
      openToolStripMenuItem.Text = "&Open…";
      openToolStripMenuItem.Click += openToolStripMenuItem_Click;
      // 
      // toolStripSeparator
      // 
      toolStripSeparator.Name = "toolStripSeparator";
      toolStripSeparator.Size = new System.Drawing.Size(177, 6);
      // 
      // exportToolStripMenuItem
      // 
      exportToolStripMenuItem.Name = "exportToolStripMenuItem";
      exportToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
      exportToolStripMenuItem.Text = "&Export";
      // 
      // toolStripSeparator1
      // 
      toolStripSeparator1.Name = "toolStripSeparator1";
      toolStripSeparator1.Size = new System.Drawing.Size(177, 6);
      // 
      // exitToolStripMenuItem
      // 
      exitToolStripMenuItem.Name = "exitToolStripMenuItem";
      exitToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.F4;
      exitToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
      exitToolStripMenuItem.Text = "E&xit";
      // 
      // editToolStripMenuItem
      // 
      editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { undoToolStripMenuItem, redoToolStripMenuItem, toolStripSeparator3, cutToolStripMenuItem, copyToolStripMenuItem, pasteToolStripMenuItem, toolStripSeparator4, selectAllToolStripMenuItem });
      editToolStripMenuItem.Name = "editToolStripMenuItem";
      editToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
      editToolStripMenuItem.Text = "&Edit";
      // 
      // undoToolStripMenuItem
      // 
      undoToolStripMenuItem.Name = "undoToolStripMenuItem";
      undoToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z;
      undoToolStripMenuItem.Size = new System.Drawing.Size(164, 22);
      undoToolStripMenuItem.Text = "&Undo";
      // 
      // redoToolStripMenuItem
      // 
      redoToolStripMenuItem.Name = "redoToolStripMenuItem";
      redoToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Y;
      redoToolStripMenuItem.Size = new System.Drawing.Size(164, 22);
      redoToolStripMenuItem.Text = "&Redo";
      // 
      // toolStripSeparator3
      // 
      toolStripSeparator3.Name = "toolStripSeparator3";
      toolStripSeparator3.Size = new System.Drawing.Size(161, 6);
      // 
      // cutToolStripMenuItem
      // 
      cutToolStripMenuItem.Image = (System.Drawing.Image)resources.GetObject("cutToolStripMenuItem.Image");
      cutToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Magenta;
      cutToolStripMenuItem.Name = "cutToolStripMenuItem";
      cutToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.X;
      cutToolStripMenuItem.Size = new System.Drawing.Size(164, 22);
      cutToolStripMenuItem.Text = "Cu&t";
      // 
      // copyToolStripMenuItem
      // 
      copyToolStripMenuItem.Image = (System.Drawing.Image)resources.GetObject("copyToolStripMenuItem.Image");
      copyToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Magenta;
      copyToolStripMenuItem.Name = "copyToolStripMenuItem";
      copyToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C;
      copyToolStripMenuItem.Size = new System.Drawing.Size(164, 22);
      copyToolStripMenuItem.Text = "&Copy";
      // 
      // pasteToolStripMenuItem
      // 
      pasteToolStripMenuItem.Image = (System.Drawing.Image)resources.GetObject("pasteToolStripMenuItem.Image");
      pasteToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Magenta;
      pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
      pasteToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.V;
      pasteToolStripMenuItem.Size = new System.Drawing.Size(164, 22);
      pasteToolStripMenuItem.Text = "&Paste";
      // 
      // toolStripSeparator4
      // 
      toolStripSeparator4.Name = "toolStripSeparator4";
      toolStripSeparator4.Size = new System.Drawing.Size(161, 6);
      // 
      // selectAllToolStripMenuItem
      // 
      selectAllToolStripMenuItem.Name = "selectAllToolStripMenuItem";
      selectAllToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.A;
      selectAllToolStripMenuItem.Size = new System.Drawing.Size(164, 22);
      selectAllToolStripMenuItem.Text = "Select &All";
      // 
      // toolsToolStripMenuItem
      // 
      toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { optionsToolStripMenuItem });
      toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
      toolsToolStripMenuItem.Size = new System.Drawing.Size(46, 20);
      toolsToolStripMenuItem.Text = "&Tools";
      // 
      // optionsToolStripMenuItem
      // 
      optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
      optionsToolStripMenuItem.Size = new System.Drawing.Size(125, 22);
      optionsToolStripMenuItem.Text = "&Options…";
      // 
      // helpToolStripMenuItem
      // 
      helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { contentsToolStripMenuItem, indexToolStripMenuItem, searchToolStripMenuItem, toolStripSeparator5, aboutToolStripMenuItem });
      helpToolStripMenuItem.Name = "helpToolStripMenuItem";
      helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
      helpToolStripMenuItem.Text = "&Help";
      // 
      // contentsToolStripMenuItem
      // 
      contentsToolStripMenuItem.Name = "contentsToolStripMenuItem";
      contentsToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
      contentsToolStripMenuItem.Text = "&Contents";
      // 
      // indexToolStripMenuItem
      // 
      indexToolStripMenuItem.Name = "indexToolStripMenuItem";
      indexToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
      indexToolStripMenuItem.Text = "&Index";
      // 
      // searchToolStripMenuItem
      // 
      searchToolStripMenuItem.Name = "searchToolStripMenuItem";
      searchToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
      searchToolStripMenuItem.Text = "&Search";
      // 
      // toolStripSeparator5
      // 
      toolStripSeparator5.Name = "toolStripSeparator5";
      toolStripSeparator5.Size = new System.Drawing.Size(119, 6);
      // 
      // aboutToolStripMenuItem
      // 
      aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
      aboutToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
      aboutToolStripMenuItem.Text = "&About…";
      // 
      // statusStrip
      // 
      statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { statusLabel, progressBar });
      statusStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
      statusStrip.Location = new System.Drawing.Point(0, 339);
      statusStrip.Name = "statusStrip";
      statusStrip.Size = new System.Drawing.Size(584, 22);
      statusStrip.TabIndex = 1;
      statusStrip.Text = "statusStrip";
      // 
      // statusLabel
      // 
      statusLabel.Name = "statusLabel";
      statusLabel.Size = new System.Drawing.Size(39, 17);
      statusLabel.Text = "Ready";
      // 
      // progressBar
      // 
      progressBar.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
      progressBar.MarqueeAnimationSpeed = 25;
      progressBar.Name = "progressBar";
      progressBar.Size = new System.Drawing.Size(100, 16);
      progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
      progressBar.Visible = false;
      // 
      // openDialog
      // 
      openDialog.Filter = "OVL Archives|*.ovl";
      openDialog.OkRequiresInteraction = true;
      openDialog.ShowHiddenFiles = true;
      openDialog.Title = "Open OVL Archive…";
      // 
      // splitView
      // 
      splitView.Dock = System.Windows.Forms.DockStyle.Fill;
      splitView.Location = new System.Drawing.Point(0, 24);
      splitView.Name = "splitView";
      // 
      // splitView.Panel1
      // 
      splitView.Panel1.Controls.Add(toolStrip);
      splitView.Panel1.Controls.Add(treeView);
      splitView.Panel1MinSize = 175;
      splitView.Size = new System.Drawing.Size(584, 315);
      splitView.SplitterDistance = 175;
      splitView.TabIndex = 2;
      // 
      // toolStrip
      // 
      toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
      toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { helpToolStripButton, openArchiveToolStripButton });
      toolStrip.Location = new System.Drawing.Point(0, 0);
      toolStrip.Name = "toolStrip";
      toolStrip.Size = new System.Drawing.Size(175, 25);
      toolStrip.TabIndex = 1;
      toolStrip.Text = "toolStrip";
      // 
      // helpToolStripButton
      // 
      helpToolStripButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
      helpToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
      helpToolStripButton.Image = (System.Drawing.Image)resources.GetObject("helpToolStripButton.Image");
      helpToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
      helpToolStripButton.Name = "helpToolStripButton";
      helpToolStripButton.Size = new System.Drawing.Size(23, 22);
      helpToolStripButton.Text = "He&lp";
      // 
      // openArchiveToolStripButton
      // 
      openArchiveToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
      openArchiveToolStripButton.Image = (System.Drawing.Image)resources.GetObject("openArchiveToolStripButton.Image");
      openArchiveToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
      openArchiveToolStripButton.Name = "openArchiveToolStripButton";
      openArchiveToolStripButton.Size = new System.Drawing.Size(23, 22);
      openArchiveToolStripButton.Text = "&Open";
      openArchiveToolStripButton.Click += openArchiveToolStripButton_Click;
      // 
      // treeView
      // 
      treeView.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
      treeView.Location = new System.Drawing.Point(0, 28);
      treeView.Name = "treeView";
      treeView.Size = new System.Drawing.Size(175, 287);
      treeView.TabIndex = 0;
      // 
      // MainForm
      // 
      AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      ClientSize = new System.Drawing.Size(584, 361);
      Controls.Add(splitView);
      Controls.Add(statusStrip);
      Controls.Add(menuStrip);
      Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
      MainMenuStrip = menuStrip;
      MinimumSize = new System.Drawing.Size(600, 400);
      Name = "MainForm";
      StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      Text = "OVL Dumper";
      menuStrip.ResumeLayout(false);
      menuStrip.PerformLayout();
      statusStrip.ResumeLayout(false);
      statusStrip.PerformLayout();
      splitView.Panel1.ResumeLayout(false);
      splitView.Panel1.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)splitView).EndInit();
      splitView.ResumeLayout(false);
      toolStrip.ResumeLayout(false);
      toolStrip.PerformLayout();
      ResumeLayout(false);
      PerformLayout();
    }

    #endregion

    private System.Windows.Forms.MenuStrip menuStrip;
    private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
    private System.Windows.Forms.ToolStripSeparator toolStripSeparator;
    private System.Windows.Forms.ToolStripMenuItem exportToolStripMenuItem;
    private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
    private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem undoToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem redoToolStripMenuItem;
    private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
    private System.Windows.Forms.ToolStripMenuItem cutToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem;
    private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
    private System.Windows.Forms.ToolStripMenuItem selectAllToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem contentsToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem indexToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem searchToolStripMenuItem;
    private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
    private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
    private System.Windows.Forms.StatusStrip statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel statusLabel;
    private System.Windows.Forms.ToolStripProgressBar progressBar;
    private System.Windows.Forms.OpenFileDialog openDialog;
    private System.Windows.Forms.SplitContainer splitView;
    private System.Windows.Forms.TreeView treeView;
    private System.Windows.Forms.ToolStrip toolStrip;
    private System.Windows.Forms.ToolStripButton helpToolStripButton;
    private System.Windows.Forms.ToolStripButton openArchiveToolStripButton;
  }
}
