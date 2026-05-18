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
      var resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
      menuStrip = new MenuStrip();
      fileToolStripMenuItem = new ToolStripMenuItem();
      openToolStripMenuItem = new ToolStripMenuItem();
      toolStripSeparator = new ToolStripSeparator();
      exportToolStripMenuItem = new ToolStripMenuItem();
      toolStripSeparator1 = new ToolStripSeparator();
      exitToolStripMenuItem = new ToolStripMenuItem();
      editToolStripMenuItem = new ToolStripMenuItem();
      undoToolStripMenuItem = new ToolStripMenuItem();
      redoToolStripMenuItem = new ToolStripMenuItem();
      toolStripSeparator3 = new ToolStripSeparator();
      cutToolStripMenuItem = new ToolStripMenuItem();
      copyToolStripMenuItem = new ToolStripMenuItem();
      pasteToolStripMenuItem = new ToolStripMenuItem();
      toolStripSeparator4 = new ToolStripSeparator();
      selectAllToolStripMenuItem = new ToolStripMenuItem();
      toolsToolStripMenuItem = new ToolStripMenuItem();
      pluginsToolStripMenuItem = new ToolStripMenuItem();
      optionsToolStripMenuItem = new ToolStripMenuItem();
      helpToolStripMenuItem = new ToolStripMenuItem();
      contentsToolStripMenuItem = new ToolStripMenuItem();
      indexToolStripMenuItem = new ToolStripMenuItem();
      searchToolStripMenuItem = new ToolStripMenuItem();
      toolStripSeparator5 = new ToolStripSeparator();
      aboutToolStripMenuItem = new ToolStripMenuItem();
      statusStrip = new StatusStrip();
      statusLabel = new ToolStripStatusLabel();
      ovlCountLabel = new ToolStripStatusLabel();
      resourceCountLabel = new ToolStripStatusLabel();
      progressBar = new ToolStripProgressBar();
      openDialog = new OpenFileDialog();
      splitView = new SplitContainer();
      toolStrip = new ToolStrip();
      helpToolStripButton = new ToolStripButton();
      openArchiveToolStripButton = new ToolStripButton();
      treeView = new FileTree();
      contentPanel = new ContentPanel();
      menuStrip.SuspendLayout();
      statusStrip.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)splitView).BeginInit();
      splitView.Panel1.SuspendLayout();
      splitView.Panel2.SuspendLayout();
      splitView.SuspendLayout();
      toolStrip.SuspendLayout();
      SuspendLayout();
      // 
      // menuStrip
      // 
      menuStrip.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem, toolsToolStripMenuItem, helpToolStripMenuItem });
      menuStrip.Location = new Point(0, 0);
      menuStrip.Name = "menuStrip";
      menuStrip.Size = new Size(659, 24);
      menuStrip.TabIndex = 0;
      menuStrip.Text = "menuStrip";
      // 
      // fileToolStripMenuItem
      // 
      fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openToolStripMenuItem, toolStripSeparator, exportToolStripMenuItem, toolStripSeparator1, exitToolStripMenuItem });
      fileToolStripMenuItem.Name = "fileToolStripMenuItem";
      fileToolStripMenuItem.Size = new Size(37, 20);
      fileToolStripMenuItem.Text = "&File";
      // 
      // openToolStripMenuItem
      // 
      openToolStripMenuItem.Image = (Image)resources.GetObject("openToolStripMenuItem.Image");
      openToolStripMenuItem.ImageTransparentColor = Color.Magenta;
      openToolStripMenuItem.Name = "openToolStripMenuItem";
      openToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
      openToolStripMenuItem.Size = new Size(155, 22);
      openToolStripMenuItem.Text = "&Open…";
      openToolStripMenuItem.Click += OpenMenuItem_Click;
      // 
      // toolStripSeparator
      // 
      toolStripSeparator.Name = "toolStripSeparator";
      toolStripSeparator.Size = new Size(152, 6);
      // 
      // exportToolStripMenuItem
      // 
      exportToolStripMenuItem.Name = "exportToolStripMenuItem";
      exportToolStripMenuItem.Size = new Size(155, 22);
      exportToolStripMenuItem.Text = "&Export";
      // 
      // toolStripSeparator1
      // 
      toolStripSeparator1.Name = "toolStripSeparator1";
      toolStripSeparator1.Size = new Size(152, 6);
      // 
      // exitToolStripMenuItem
      // 
      exitToolStripMenuItem.Name = "exitToolStripMenuItem";
      exitToolStripMenuItem.ShortcutKeys = Keys.Alt | Keys.F4;
      exitToolStripMenuItem.Size = new Size(155, 22);
      exitToolStripMenuItem.Text = "E&xit";
      exitToolStripMenuItem.Click += ExitMenuItem_Click;
      // 
      // editToolStripMenuItem
      // 
      editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { undoToolStripMenuItem, redoToolStripMenuItem, toolStripSeparator3, cutToolStripMenuItem, copyToolStripMenuItem, pasteToolStripMenuItem, toolStripSeparator4, selectAllToolStripMenuItem });
      editToolStripMenuItem.Name = "editToolStripMenuItem";
      editToolStripMenuItem.Size = new Size(39, 20);
      editToolStripMenuItem.Text = "&Edit";
      // 
      // undoToolStripMenuItem
      // 
      undoToolStripMenuItem.Name = "undoToolStripMenuItem";
      undoToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Z;
      undoToolStripMenuItem.Size = new Size(164, 22);
      undoToolStripMenuItem.Text = "&Undo";
      // 
      // redoToolStripMenuItem
      // 
      redoToolStripMenuItem.Name = "redoToolStripMenuItem";
      redoToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Y;
      redoToolStripMenuItem.Size = new Size(164, 22);
      redoToolStripMenuItem.Text = "&Redo";
      // 
      // toolStripSeparator3
      // 
      toolStripSeparator3.Name = "toolStripSeparator3";
      toolStripSeparator3.Size = new Size(161, 6);
      // 
      // cutToolStripMenuItem
      // 
      cutToolStripMenuItem.Image = (Image)resources.GetObject("cutToolStripMenuItem.Image");
      cutToolStripMenuItem.ImageTransparentColor = Color.Magenta;
      cutToolStripMenuItem.Name = "cutToolStripMenuItem";
      cutToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.X;
      cutToolStripMenuItem.Size = new Size(164, 22);
      cutToolStripMenuItem.Text = "Cu&t";
      // 
      // copyToolStripMenuItem
      // 
      copyToolStripMenuItem.Image = (Image)resources.GetObject("copyToolStripMenuItem.Image");
      copyToolStripMenuItem.ImageTransparentColor = Color.Magenta;
      copyToolStripMenuItem.Name = "copyToolStripMenuItem";
      copyToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.C;
      copyToolStripMenuItem.Size = new Size(164, 22);
      copyToolStripMenuItem.Text = "&Copy";
      // 
      // pasteToolStripMenuItem
      // 
      pasteToolStripMenuItem.Image = (Image)resources.GetObject("pasteToolStripMenuItem.Image");
      pasteToolStripMenuItem.ImageTransparentColor = Color.Magenta;
      pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
      pasteToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.V;
      pasteToolStripMenuItem.Size = new Size(164, 22);
      pasteToolStripMenuItem.Text = "&Paste";
      // 
      // toolStripSeparator4
      // 
      toolStripSeparator4.Name = "toolStripSeparator4";
      toolStripSeparator4.Size = new Size(161, 6);
      // 
      // selectAllToolStripMenuItem
      // 
      selectAllToolStripMenuItem.Name = "selectAllToolStripMenuItem";
      selectAllToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.A;
      selectAllToolStripMenuItem.Size = new Size(164, 22);
      selectAllToolStripMenuItem.Text = "Select &All";
      // 
      // toolsToolStripMenuItem
      // 
      toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { pluginsToolStripMenuItem, optionsToolStripMenuItem });
      toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
      toolsToolStripMenuItem.Size = new Size(46, 20);
      toolsToolStripMenuItem.Text = "&Tools";
      // 
      // pluginsToolStripMenuItem
      // 
      pluginsToolStripMenuItem.Name = "pluginsToolStripMenuItem";
      pluginsToolStripMenuItem.Size = new Size(180, 22);
      pluginsToolStripMenuItem.Text = "&Plugins…";
      pluginsToolStripMenuItem.Click += PluginsMenuItem_Click;
      // 
      // optionsToolStripMenuItem
      // 
      optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
      optionsToolStripMenuItem.Size = new Size(180, 22);
      optionsToolStripMenuItem.Text = "Settings…";
      // 
      // helpToolStripMenuItem
      // 
      helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { contentsToolStripMenuItem, indexToolStripMenuItem, searchToolStripMenuItem, toolStripSeparator5, aboutToolStripMenuItem });
      helpToolStripMenuItem.Name = "helpToolStripMenuItem";
      helpToolStripMenuItem.Size = new Size(44, 20);
      helpToolStripMenuItem.Text = "&Help";
      // 
      // contentsToolStripMenuItem
      // 
      contentsToolStripMenuItem.Name = "contentsToolStripMenuItem";
      contentsToolStripMenuItem.Size = new Size(122, 22);
      contentsToolStripMenuItem.Text = "&Contents";
      // 
      // indexToolStripMenuItem
      // 
      indexToolStripMenuItem.Name = "indexToolStripMenuItem";
      indexToolStripMenuItem.Size = new Size(122, 22);
      indexToolStripMenuItem.Text = "&Index";
      // 
      // searchToolStripMenuItem
      // 
      searchToolStripMenuItem.Name = "searchToolStripMenuItem";
      searchToolStripMenuItem.Size = new Size(122, 22);
      searchToolStripMenuItem.Text = "&Search";
      // 
      // toolStripSeparator5
      // 
      toolStripSeparator5.Name = "toolStripSeparator5";
      toolStripSeparator5.Size = new Size(119, 6);
      // 
      // aboutToolStripMenuItem
      // 
      aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
      aboutToolStripMenuItem.Size = new Size(122, 22);
      aboutToolStripMenuItem.Text = "&About…";
      // 
      // statusStrip
      // 
      statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, ovlCountLabel, resourceCountLabel, progressBar });
      statusStrip.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
      statusStrip.Location = new Point(0, 364);
      statusStrip.Name = "statusStrip";
      statusStrip.Size = new Size(659, 22);
      statusStrip.TabIndex = 1;
      statusStrip.Text = "statusStrip";
      // 
      // statusLabel
      // 
      statusLabel.Name = "statusLabel";
      statusLabel.Size = new Size(39, 17);
      statusLabel.Text = "Ready";
      // 
      // ovlCountLabel
      // 
      ovlCountLabel.Alignment = ToolStripItemAlignment.Right;
      ovlCountLabel.Name = "ovlCountLabel";
      ovlCountLabel.Size = new Size(0, 17);
      // 
      // resourceCountLabel
      // 
      resourceCountLabel.Alignment = ToolStripItemAlignment.Right;
      resourceCountLabel.Name = "resourceCountLabel";
      resourceCountLabel.Size = new Size(0, 17);
      // 
      // progressBar
      // 
      progressBar.Alignment = ToolStripItemAlignment.Right;
      progressBar.MarqueeAnimationSpeed = 25;
      progressBar.Name = "progressBar";
      progressBar.Size = new Size(100, 16);
      progressBar.Style = ProgressBarStyle.Marquee;
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
      splitView.Dock = DockStyle.Fill;
      splitView.FixedPanel = FixedPanel.Panel1;
      splitView.Location = new Point(0, 24);
      splitView.Name = "splitView";
      // 
      // splitView.Panel1
      // 
      splitView.Panel1.Controls.Add(toolStrip);
      splitView.Panel1.Controls.Add(treeView);
      splitView.Panel1MinSize = 175;
      // 
      // splitView.Panel2
      // 
      splitView.Panel2.Controls.Add(contentPanel);
      splitView.Size = new Size(659, 340);
      splitView.SplitterDistance = 225;
      splitView.TabIndex = 2;
      splitView.SplitterMoved += SplitView_SplitterMoved;
      splitView.SizeChanged += SplitView_SizeChanged;
      splitView.MouseDoubleClick += Splitter_MouseDoubleClick;
      // 
      // toolStrip
      // 
      toolStrip.CanOverflow = false;
      toolStrip.GripStyle = ToolStripGripStyle.Hidden;
      toolStrip.Items.AddRange(new ToolStripItem[] { helpToolStripButton, openArchiveToolStripButton });
      toolStrip.Location = new Point(0, 0);
      toolStrip.Name = "toolStrip";
      toolStrip.Size = new Size(225, 25);
      toolStrip.TabIndex = 1;
      toolStrip.Text = "toolStrip";
      // 
      // helpToolStripButton
      // 
      helpToolStripButton.Alignment = ToolStripItemAlignment.Right;
      helpToolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
      helpToolStripButton.Image = (Image)resources.GetObject("helpToolStripButton.Image");
      helpToolStripButton.ImageTransparentColor = Color.Magenta;
      helpToolStripButton.Name = "helpToolStripButton";
      helpToolStripButton.Size = new Size(23, 22);
      helpToolStripButton.Text = "He&lp";
      // 
      // openArchiveToolStripButton
      // 
      openArchiveToolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
      openArchiveToolStripButton.Image = (Image)resources.GetObject("openArchiveToolStripButton.Image");
      openArchiveToolStripButton.ImageTransparentColor = Color.Magenta;
      openArchiveToolStripButton.Name = "openArchiveToolStripButton";
      openArchiveToolStripButton.Size = new Size(23, 22);
      openArchiveToolStripButton.Text = "&Open";
      openArchiveToolStripButton.Click += OpenArchive_Click;
      // 
      // treeView
      // 
      treeView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
      treeView.Location = new Point(0, 28);
      treeView.Name = "treeView";
      treeView.Size = new Size(225, 312);
      treeView.TabIndex = 0;
      // 
      // contentPanel
      // 
      contentPanel.Dock = DockStyle.Fill;
      contentPanel.Location = new Point(0, 0);
      contentPanel.Name = "contentPanel";
      contentPanel.Size = new Size(430, 340);
      contentPanel.TabIndex = 1;
      contentPanel.Visible = false;
      // 
      // MainForm
      // 
      AutoScaleDimensions = new SizeF(7F, 15F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(659, 386);
      Controls.Add(splitView);
      Controls.Add(statusStrip);
      Controls.Add(menuStrip);
      Icon = (Icon)resources.GetObject("$this.Icon");
      MainMenuStrip = menuStrip;
      MinimumSize = new Size(675, 425);
      Name = "MainForm";
      StartPosition = FormStartPosition.CenterScreen;
      Text = "OVL Dumper";
      menuStrip.ResumeLayout(false);
      menuStrip.PerformLayout();
      statusStrip.ResumeLayout(false);
      statusStrip.PerformLayout();
      splitView.Panel1.ResumeLayout(false);
      splitView.Panel1.PerformLayout();
      splitView.Panel2.ResumeLayout(false);
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
    private System.Windows.Forms.ToolStripMenuItem pluginsToolStripMenuItem;
    private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
    private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem contentsToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem indexToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem searchToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
    private System.Windows.Forms.StatusStrip statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel statusLabel;
    private System.Windows.Forms.ToolStripStatusLabel ovlCountLabel;
    private System.Windows.Forms.ToolStripStatusLabel resourceCountLabel;
    private System.Windows.Forms.ToolStripProgressBar progressBar;
    private System.Windows.Forms.OpenFileDialog openDialog;
    private System.Windows.Forms.SplitContainer splitView;
    private Dumper.FileTree treeView;
    private System.Windows.Forms.ToolStrip toolStrip;
    private System.Windows.Forms.ToolStripButton helpToolStripButton;
    private System.Windows.Forms.ToolStripButton openArchiveToolStripButton;
    private Dumper.ContentPanel contentPanel;
  }
}
