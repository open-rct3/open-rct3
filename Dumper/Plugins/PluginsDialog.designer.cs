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
      splitView = new System.Windows.Forms.SplitContainer();
      toolStrip = new System.Windows.Forms.ToolStrip();
      install = new System.Windows.Forms.ToolStripSplitButton();
      installFromCatalog = new System.Windows.Forms.ToolStripMenuItem();
      installFromDisk = new System.Windows.Forms.ToolStripMenuItem();
      uninstall = new System.Windows.Forms.ToolStripButton();
      pluginList = new System.Windows.Forms.ListBox();
      metadata = new System.Windows.Forms.TableLayoutPanel();
      name = new System.Windows.Forms.Label();
      nameValue = new System.Windows.Forms.Label();
      enabled = new System.Windows.Forms.CheckBox();
      version = new System.Windows.Forms.Label();
      versionValue = new System.Windows.Forms.Label();
      fileTypes = new System.Windows.Forms.Label();
      fileTypesValue = new System.Windows.Forms.Label();
      location = new System.Windows.Forms.Label();
      locationValue = new TruncatedLabel();
      openFolder = new System.Windows.Forms.Button();
      closeButton = new System.Windows.Forms.Button();
      emptyLabel = new System.Windows.Forms.Label();
      toolTip = new System.Windows.Forms.ToolTip(components);
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
      splitView.Dock = System.Windows.Forms.DockStyle.Fill;
      splitView.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
      splitView.IsSplitterFixed = true;
      splitView.Location = new System.Drawing.Point(0, 0);
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
      splitView.Size = new System.Drawing.Size(559, 341);
      splitView.SplitterDistance = 150;
      splitView.SplitterWidth = 3;
      splitView.TabIndex = 0;
      // 
      // toolStrip
      // 
      toolStrip.CanOverflow = false;
      toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
      toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { install, uninstall });
      toolStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
      toolStrip.Location = new System.Drawing.Point(0, 0);
      toolStrip.Name = "toolStrip";
      toolStrip.Size = new System.Drawing.Size(150, 25);
      toolStrip.Stretch = true;
      toolStrip.TabIndex = 1;
      toolStrip.Text = "toolStrip1";
      // 
      // install
      // 
      install.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { installFromCatalog, installFromDisk });
      install.Name = "install";
      install.Size = new System.Drawing.Size(16, 22);
      install.ToolTipText = "Install a plugin…";
      install.ButtonClick += InstallFromCatalog_Click;
      // 
      // installFromCatalog
      // 
      installFromCatalog.Name = "installFromCatalog";
      installFromCatalog.Size = new System.Drawing.Size(185, 22);
      installFromCatalog.Text = "Install from catalog…";
      installFromCatalog.Click += InstallFromCatalog_Click;
      // 
      // installFromDisk
      // 
      installFromDisk.Name = "installFromDisk";
      installFromDisk.Size = new System.Drawing.Size(185, 22);
      installFromDisk.Text = "Install from disk…";
      installFromDisk.Click += InstallFromDisk_Click;
      // 
      // uninstall
      // 
      uninstall.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
      uninstall.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
      uninstall.Enabled = false;
      uninstall.Name = "uninstall";
      uninstall.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
      uninstall.Size = new System.Drawing.Size(23, 22);
      uninstall.ToolTipText = "Uninstall this plugin";
      uninstall.Click += Uninstall_Click;
      // 
      // pluginList
      // 
      pluginList.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
      pluginList.BorderStyle = System.Windows.Forms.BorderStyle.None;
      pluginList.FormattingEnabled = true;
      pluginList.IntegralHeight = false;
      pluginList.ItemHeight = 15;
      pluginList.Location = new System.Drawing.Point(0, 28);
      pluginList.Name = "pluginList";
      pluginList.Size = new System.Drawing.Size(150, 313);
      pluginList.TabIndex = 0;
      pluginList.SelectedIndexChanged += PluginListBox_SelectedIndexChanged;
      // 
      // metadata
      // 
      metadata.ColumnCount = 3;
      metadata.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      metadata.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      metadata.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      metadata.Controls.Add(name, 0, 0);
      metadata.Controls.Add(nameValue, 1, 0);
      metadata.Controls.Add(enabled, 2, 0);
      metadata.Controls.Add(version, 0, 1);
      metadata.Controls.Add(versionValue, 1, 1);
      metadata.Controls.Add(fileTypes, 0, 2);
      metadata.Controls.Add(fileTypesValue, 1, 2);
      metadata.Controls.Add(location, 0, 3);
      metadata.Controls.Add(closeButton, 2, 5);
      metadata.Controls.Add(locationValue, 1, 3);
      metadata.Controls.Add(openFolder, 2, 3);
      metadata.Dock = System.Windows.Forms.DockStyle.Fill;
      metadata.Location = new System.Drawing.Point(0, 0);
      metadata.Name = "metadata";
      metadata.Padding = new System.Windows.Forms.Padding(12, 8, 12, 8);
      metadata.RowCount = 2;
      metadata.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
      metadata.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
      metadata.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
      metadata.RowStyles.Add(new System.Windows.Forms.RowStyle());
      metadata.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      metadata.RowStyles.Add(new System.Windows.Forms.RowStyle());
      metadata.Size = new System.Drawing.Size(406, 341);
      metadata.TabIndex = 0;
      metadata.Visible = false;
      // 
      // name
      // 
      name.AutoSize = true;
      name.ForeColor = System.Drawing.SystemColors.GrayText;
      name.Location = new System.Drawing.Point(15, 8);
      name.Name = "name";
      name.Size = new System.Drawing.Size(42, 15);
      name.TabIndex = 0;
      name.Text = "Name:";
      // 
      // nameValue
      // 
      nameValue.Dock = System.Windows.Forms.DockStyle.Fill;
      nameValue.Location = new System.Drawing.Point(80, 8);
      nameValue.Name = "nameValue";
      nameValue.Size = new System.Drawing.Size(230, 24);
      nameValue.TabIndex = 1;
      nameValue.Text = "Flex-Texture Viewer";
      // 
      // enabled
      // 
      enabled.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
      enabled.AutoSize = true;
      enabled.Location = new System.Drawing.Point(323, 8);
      enabled.Margin = new System.Windows.Forms.Padding(0, 0, 3, 0);
      enabled.Name = "enabled";
      enabled.Size = new System.Drawing.Size(68, 19);
      enabled.TabIndex = 9;
      enabled.Text = "Enabled";
      enabled.UseVisualStyleBackColor = true;
      // 
      // version
      // 
      version.AutoSize = true;
      version.ForeColor = System.Drawing.SystemColors.GrayText;
      version.Location = new System.Drawing.Point(15, 32);
      version.Name = "version";
      version.Size = new System.Drawing.Size(48, 15);
      version.TabIndex = 2;
      version.Text = "Version:";
      // 
      // versionValue
      // 
      metadata.SetColumnSpan(versionValue, 2);
      versionValue.Dock = System.Windows.Forms.DockStyle.Fill;
      versionValue.Location = new System.Drawing.Point(80, 32);
      versionValue.Name = "versionValue";
      versionValue.Size = new System.Drawing.Size(311, 24);
      versionValue.TabIndex = 3;
      versionValue.Text = "v0.1.0";
      // 
      // fileTypes
      // 
      fileTypes.AutoSize = true;
      fileTypes.ForeColor = System.Drawing.SystemColors.GrayText;
      fileTypes.Location = new System.Drawing.Point(15, 56);
      fileTypes.Name = "fileTypes";
      fileTypes.Size = new System.Drawing.Size(59, 15);
      fileTypes.TabIndex = 4;
      fileTypes.Text = "File types:";
      // 
      // fileTypesValue
      // 
      metadata.SetColumnSpan(fileTypesValue, 2);
      fileTypesValue.Dock = System.Windows.Forms.DockStyle.Fill;
      fileTypesValue.Location = new System.Drawing.Point(80, 56);
      fileTypesValue.Name = "fileTypesValue";
      fileTypesValue.Size = new System.Drawing.Size(311, 24);
      fileTypesValue.TabIndex = 5;
      fileTypesValue.Text = "*.ftx";
      // 
      // location
      // 
      location.AutoSize = true;
      location.ForeColor = System.Drawing.SystemColors.GrayText;
      location.Location = new System.Drawing.Point(15, 80);
      location.Name = "location";
      location.Size = new System.Drawing.Size(34, 15);
      location.TabIndex = 6;
      location.Text = "Path:";
      // 
      // locationValue
      // 
      locationValue.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
      locationValue.Ellipsis = "...";
      locationValue.Location = new System.Drawing.Point(80, 80);
      locationValue.Name = "locationValue";
      locationValue.PreserveRatio = 0.3F;
      locationValue.Size = new System.Drawing.Size(230, 24);
      locationValue.SmartBreak = true;
      locationValue.TabIndex = 7;
      locationValue.Text = "C:\\OpenRCT3\\Plugins\\Dumper\\ftx-viewer.wasm";
      // 
      // openFolder
      // 
      openFolder.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
      openFolder.AutoSize = true;
      openFolder.FlatStyle = System.Windows.Forms.FlatStyle.System;
      openFolder.Location = new System.Drawing.Point(361, 80);
      openFolder.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
      openFolder.Name = "openFolder";
      openFolder.Size = new System.Drawing.Size(30, 24);
      openFolder.TabIndex = 8;
      openFolder.Text = "…";
      toolTip.SetToolTip(openFolder, "Open the folder containing  this plugin");
      openFolder.UseVisualStyleBackColor = true;
      // 
      // closeButton
      // 
      closeButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
      closeButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      closeButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
      closeButton.Location = new System.Drawing.Point(316, 307);
      closeButton.Name = "closeButton";
      closeButton.Size = new System.Drawing.Size(75, 23);
      closeButton.TabIndex = 1;
      closeButton.Text = "Close";
      closeButton.UseVisualStyleBackColor = true;
      // 
      // emptyLabel
      // 
      emptyLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      emptyLabel.ForeColor = System.Drawing.SystemColors.GrayText;
      emptyLabel.Location = new System.Drawing.Point(0, 0);
      emptyLabel.Name = "emptyLabel";
      emptyLabel.Size = new System.Drawing.Size(406, 341);
      emptyLabel.TabIndex = 1;
      emptyLabel.Text = "No plugins installed.";
      emptyLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
      emptyLabel.Visible = false;
      // 
      // PluginsDialog
      // 
      AcceptButton = closeButton;
      AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      CancelButton = closeButton;
      ClientSize = new System.Drawing.Size(559, 341);
      Controls.Add(splitView);
      MaximizeBox = false;
      MinimizeBox = false;
      MinimumSize = new System.Drawing.Size(575, 380);
      Name = "PluginsDialog";
      ShowInTaskbar = false;
      StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      Text = "Plugins";
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
    private System.Windows.Forms.Label location;
    private TruncatedLabel locationValue;
    private System.Windows.Forms.Label emptyLabel;
    private System.Windows.Forms.Button closeButton;
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
