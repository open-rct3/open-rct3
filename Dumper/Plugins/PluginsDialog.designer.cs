namespace Dumper.Plugins {
  partial class PluginsDialog {
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing) {
      if (disposing && (components != null)) {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent() {
      components = new System.ComponentModel.Container();
      splitView = new SplitContainer();
      toolStrip = new ToolStrip();
      install = new ToolStripSplitButton();
      installFromCatalog = new ToolStripMenuItem();
      installFromDisk = new ToolStripMenuItem();
      uninstall = new ToolStripButton();
      pluginList = new ListBox();
      metadata = new TableLayoutPanel();
      name = new Label();
      nameValue = new Label();
      enabled = new CheckBox();
      version = new Label();
      versionValue = new Label();
      fileTypes = new Label();
      fileTypesValue = new Label();
      close = new Button();
      openFolder = new Button();
      emptyLabel = new Label();
      toolTip = new ToolTip(components);
      ((System.ComponentModel.ISupportInitialize)splitView).BeginInit();
      splitView.Panel1.SuspendLayout();
      splitView.Panel2.SuspendLayout();
      splitView.SuspendLayout();
      toolStrip.SuspendLayout();
      metadata.SuspendLayout();
      SuspendLayout();
      // 
      // splitView
      // 
      splitView.Dock = DockStyle.Fill;
      splitView.FixedPanel = FixedPanel.Panel1;
      splitView.IsSplitterFixed = true;
      splitView.Location = new Point(0, 0);
      splitView.Name = "splitView";
      // 
      // splitView.Panel1
      // 
      splitView.Panel1.Controls.Add(toolStrip);
      splitView.Panel1.Controls.Add(pluginList);
      splitView.Panel1MinSize = 150;
      // 
      // splitView.Panel2
      // 
      splitView.Panel2.Controls.Add(metadata);
      splitView.Panel2.Controls.Add(emptyLabel);
      splitView.Panel2MinSize = 200;
      splitView.Size = new Size(559, 341);
      splitView.SplitterDistance = 150;
      splitView.SplitterWidth = 3;
      splitView.TabIndex = 0;
      // 
      // toolStrip
      // 
      toolStrip.CanOverflow = false;
      toolStrip.GripStyle = ToolStripGripStyle.Hidden;
      toolStrip.Items.AddRange(new ToolStripItem[] { install, uninstall });
      toolStrip.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
      toolStrip.Location = new Point(0, 0);
      toolStrip.Name = "toolStrip";
      toolStrip.Size = new Size(150, 25);
      toolStrip.Stretch = true;
      toolStrip.TabIndex = 1;
      toolStrip.Text = "toolStrip1";
      // 
      // install
      // 
      install.DropDownItems.AddRange(new ToolStripItem[] { installFromCatalog, installFromDisk });
      install.Name = "install";
      install.Size = new Size(16, 22);
      install.ToolTipText = "Install a plugin…";
      install.ButtonClick += InstallFromCatalog_Click;
      install.DropDownClosed += InstallSplitBtn_DropDownClosed;
      install.DropDownOpening += InstallSplitBtn_DropDownOpening;
      // 
      // installFromCatalog
      // 
      installFromCatalog.Name = "installFromCatalog";
      installFromCatalog.Size = new Size(185, 22);
      installFromCatalog.Text = "Install from catalog…";
      installFromCatalog.Click += InstallFromCatalog_Click;
      // 
      // installFromDisk
      // 
      installFromDisk.Name = "installFromDisk";
      installFromDisk.Size = new Size(185, 22);
      installFromDisk.Text = "Install from disk…";
      installFromDisk.Click += InstallFromDisk_Click;
      // 
      // uninstall
      // 
      uninstall.Alignment = ToolStripItemAlignment.Right;
      uninstall.DisplayStyle = ToolStripItemDisplayStyle.Image;
      uninstall.Enabled = false;
      uninstall.Name = "uninstall";
      uninstall.Overflow = ToolStripItemOverflow.Never;
      uninstall.Size = new Size(23, 22);
      uninstall.ToolTipText = "Uninstall this plugin";
      uninstall.Click += Uninstall_Click;
      // 
      // pluginList
      // 
      pluginList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
      pluginList.BorderStyle = BorderStyle.None;
      pluginList.DisplayMember = "Name";
      pluginList.FormattingEnabled = true;
      pluginList.IntegralHeight = false;
      pluginList.ItemHeight = 15;
      pluginList.Location = new Point(0, 28);
      pluginList.Name = "pluginList";
      pluginList.Size = new Size(150, 313);
      pluginList.TabIndex = 0;
      pluginList.SelectedIndexChanged += PluginListBox_SelectedIndexChanged;
      // 
      // metadata
      // 
      metadata.ColumnCount = 3;
      metadata.ColumnStyles.Add(new ColumnStyle());
      metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
      metadata.ColumnStyles.Add(new ColumnStyle());
      metadata.Controls.Add(name, 0, 0);
      metadata.Controls.Add(nameValue, 1, 0);
      metadata.Controls.Add(enabled, 2, 0);
      metadata.Controls.Add(version, 0, 1);
      metadata.Controls.Add(versionValue, 1, 1);
      metadata.Controls.Add(fileTypes, 0, 2);
      metadata.Controls.Add(fileTypesValue, 1, 2);
      metadata.Controls.Add(close, 2, 5);
      metadata.Controls.Add(openFolder, 0, 5);
      metadata.Dock = DockStyle.Fill;
      metadata.Location = new Point(0, 0);
      metadata.Name = "metadata";
      metadata.Padding = new Padding(12, 8, 12, 8);
      metadata.RowCount = 2;
      metadata.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
      metadata.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
      metadata.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
      metadata.RowStyles.Add(new RowStyle());
      metadata.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
      metadata.RowStyles.Add(new RowStyle());
      metadata.Size = new Size(406, 341);
      metadata.TabIndex = 0;
      metadata.Visible = false;
      // 
      // name
      // 
      name.AutoSize = true;
      name.ForeColor = SystemColors.GrayText;
      name.Location = new Point(15, 8);
      name.Name = "name";
      name.Size = new Size(42, 15);
      name.TabIndex = 0;
      name.Text = "Name:";
      // 
      // nameValue
      // 
      nameValue.Dock = DockStyle.Fill;
      nameValue.Location = new Point(80, 8);
      nameValue.Name = "nameValue";
      nameValue.Size = new Size(230, 24);
      nameValue.TabIndex = 1;
      nameValue.Text = "Flexi-Texture Viewer";
      // 
      // enabled
      // 
      enabled.Anchor = AnchorStyles.Top | AnchorStyles.Right;
      enabled.AutoSize = true;
      enabled.Location = new Point(323, 8);
      enabled.Margin = new Padding(0, 0, 3, 0);
      enabled.Name = "enabled";
      enabled.Size = new Size(68, 19);
      enabled.TabIndex = 9;
      enabled.Text = "Enabled";
      enabled.UseVisualStyleBackColor = true;
      // 
      // version
      // 
      version.AutoSize = true;
      version.ForeColor = SystemColors.GrayText;
      version.Location = new Point(15, 32);
      version.Name = "version";
      version.Size = new Size(48, 15);
      version.TabIndex = 2;
      version.Text = "Version:";
      // 
      // versionValue
      // 
      metadata.SetColumnSpan(versionValue, 2);
      versionValue.Dock = DockStyle.Fill;
      versionValue.Location = new Point(80, 32);
      versionValue.Name = "versionValue";
      versionValue.Size = new Size(311, 24);
      versionValue.TabIndex = 3;
      versionValue.Text = "v0.1.0";
      // 
      // fileTypes
      // 
      fileTypes.AutoSize = true;
      fileTypes.ForeColor = SystemColors.GrayText;
      fileTypes.Location = new Point(15, 56);
      fileTypes.Name = "fileTypes";
      fileTypes.Size = new Size(59, 15);
      fileTypes.TabIndex = 4;
      fileTypes.Text = "File types:";
      // 
      // fileTypesValue
      // 
      metadata.SetColumnSpan(fileTypesValue, 2);
      fileTypesValue.Dock = DockStyle.Fill;
      fileTypesValue.Location = new Point(80, 56);
      fileTypesValue.Name = "fileTypesValue";
      fileTypesValue.Size = new Size(311, 24);
      fileTypesValue.TabIndex = 5;
      fileTypesValue.Text = "*.ftx";
      // 
      // close
      // 
      close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
      close.DialogResult = DialogResult.Cancel;
      close.FlatStyle = FlatStyle.System;
      close.Location = new Point(316, 307);
      close.Name = "close";
      close.Size = new Size(75, 23);
      close.TabIndex = 1;
      close.Text = "Close";
      close.UseVisualStyleBackColor = true;
      close.Click += Close_Click;
      // 
      // openFolder
      // 
      openFolder.AutoSize = true;
      metadata.SetColumnSpan(openFolder, 2);
      openFolder.FlatStyle = FlatStyle.System;
      openFolder.Location = new Point(15, 306);
      openFolder.Name = "openFolder";
      openFolder.Size = new Size(135, 24);
      openFolder.TabIndex = 8;
      openFolder.Text = "Reveal in File Explorer";
      toolTip.SetToolTip(openFolder, "Open the folder containing  this plugin");
      openFolder.UseVisualStyleBackColor = true;
      openFolder.Click += OpenFolder_Click;
      // 
      // emptyLabel
      // 
      emptyLabel.Dock = DockStyle.Fill;
      emptyLabel.ForeColor = SystemColors.GrayText;
      emptyLabel.Location = new Point(0, 0);
      emptyLabel.Name = "emptyLabel";
      emptyLabel.Size = new Size(406, 341);
      emptyLabel.TabIndex = 1;
      emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
      emptyLabel.Visible = false;
      // 
      // PluginsDialog
      // 
      AcceptButton = close;
      AutoScaleDimensions = new SizeF(7F, 15F);
      AutoScaleMode = AutoScaleMode.Font;
      CancelButton = close;
      ClientSize = new Size(559, 341);
      Controls.Add(splitView);
      MaximizeBox = false;
      MinimizeBox = false;
      MinimumSize = new Size(575, 380);
      Name = "PluginsDialog";
      ShowInTaskbar = false;
      StartPosition = FormStartPosition.CenterParent;
      Text = "Manage Plugins";
      Load += PluginsDialog_Load;
      splitView.Panel1.ResumeLayout(false);
      splitView.Panel1.PerformLayout();
      splitView.Panel2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)splitView).EndInit();
      splitView.ResumeLayout(false);
      toolStrip.ResumeLayout(false);
      toolStrip.PerformLayout();
      metadata.ResumeLayout(false);
      metadata.PerformLayout();
      ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.SplitContainer splitView;
    private System.Windows.Forms.ListBox pluginList;
    private System.Windows.Forms.TableLayoutPanel metadata;
    private System.Windows.Forms.Label name;
    private System.Windows.Forms.Label nameValue;
    private System.Windows.Forms.Label version;
    private System.Windows.Forms.Label versionValue;
    private System.Windows.Forms.Label fileTypes;
    private System.Windows.Forms.Label fileTypesValue;
    private System.Windows.Forms.Label emptyLabel;
    private System.Windows.Forms.Button close;
    private System.Windows.Forms.ToolStrip toolStrip;
    private System.Windows.Forms.ToolStripSplitButton install;
    private System.Windows.Forms.ToolStripMenuItem installFromCatalog;
    private System.Windows.Forms.ToolStripMenuItem installFromDisk;
    private System.Windows.Forms.ToolStripButton uninstall;
    private System.Windows.Forms.Button openFolder;
    private System.Windows.Forms.ToolTip toolTip;
    private System.Windows.Forms.CheckBox enabled;
  }
}
