namespace Dumper.Settings;

partial class SettingsDialog {
  /// <summary>
  /// Required designer variable.
  /// </summary>
  private System.ComponentModel.IContainer components = null;

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

  #region Windows Form Designer generated code

  /// <summary>
  /// Required method for Designer support - do not modify
  /// the contents of this method with the code editor.
  /// </summary>
  private void InitializeComponent() {
    applyBtn = new Button();
    cancelBtn = new Button();
    okayBtn = new Button();
    rct3Group = new GroupBox();
    browseBtn = new Button();
    rct3Path = new TextBox();
    pathLbl = new Label();
    openFolder = new FolderBrowserDialog();
    rct3Group.SuspendLayout();
    SuspendLayout();
    // 
    // applyBtn
    // 
    applyBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
    applyBtn.Enabled = false;
    applyBtn.FlatStyle = FlatStyle.System;
    applyBtn.Location = new Point(447, 81);
    applyBtn.Name = "applyBtn";
    applyBtn.Size = new Size(75, 23);
    applyBtn.TabIndex = 0;
    applyBtn.Text = "Apply";
    applyBtn.UseVisualStyleBackColor = true;
    applyBtn.Click += ApplyBtn_Click;
    // 
    // cancelBtn
    // 
    cancelBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
    cancelBtn.FlatStyle = FlatStyle.System;
    cancelBtn.Location = new Point(366, 81);
    cancelBtn.Name = "cancelBtn";
    cancelBtn.Size = new Size(75, 23);
    cancelBtn.TabIndex = 2;
    cancelBtn.Text = "Cancel";
    cancelBtn.UseVisualStyleBackColor = true;
    cancelBtn.Click += CancelBtn_Click;
    // 
    // okayBtn
    // 
    okayBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
    okayBtn.Location = new Point(285, 81);
    okayBtn.Name = "okayBtn";
    okayBtn.Size = new Size(75, 23);
    okayBtn.TabIndex = 3;
    okayBtn.Text = "Okay";
    okayBtn.UseVisualStyleBackColor = true;
    okayBtn.Click += OkayBtn_Click;
    // 
    // rct3Group
    // 
    rct3Group.Controls.Add(browseBtn);
    rct3Group.Controls.Add(rct3Path);
    rct3Group.Controls.Add(pathLbl);
    rct3Group.Location = new Point(12, 12);
    rct3Group.Name = "rct3Group";
    rct3Group.Size = new Size(510, 60);
    rct3Group.TabIndex = 4;
    rct3Group.TabStop = false;
    rct3Group.Text = "RollerCoaster Tycoon 3";
    // 
    // browseBtn
    // 
    browseBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
    browseBtn.Location = new Point(429, 22);
    browseBtn.Name = "browseBtn";
    browseBtn.Size = new Size(75, 23);
    browseBtn.TabIndex = 2;
    browseBtn.Text = "Browse…";
    browseBtn.UseVisualStyleBackColor = true;
    browseBtn.Click += BrowseBtn_Click;
    // 
    // rct3Path
    // 
    rct3Path.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
    rct3Path.Location = new Point(126, 22);
    rct3Path.Name = "rct3Path";
    rct3Path.PlaceholderText = "Enter the path containing RCT3.exe here";
    rct3Path.Size = new Size(297, 23);
    rct3Path.TabIndex = 1;
    rct3Path.TextChanged += Rct3Path_TextChanged;
    // 
    // pathLbl
    // 
    pathLbl.AutoSize = true;
    pathLbl.Location = new Point(6, 26);
    pathLbl.Name = "pathLbl";
    pathLbl.Size = new Size(114, 15);
    pathLbl.TabIndex = 0;
    pathLbl.Text = "Installation location:";
    // 
    // openFolder
    // 
    openFolder.Description = "Select RCT3 Installation Folder";
    openFolder.RootFolder = Environment.SpecialFolder.CommonProgramFiles;
    openFolder.ShowHiddenFiles = true;
    openFolder.ShowNewFolderButton = false;
    openFolder.UseDescriptionForTitle = true;
    // 
    // SettingsDialog
    // 
    AcceptButton = okayBtn;
    AutoScaleDimensions = new SizeF(7F, 15F);
    AutoScaleMode = AutoScaleMode.Font;
    CancelButton = cancelBtn;
    ClientSize = new Size(534, 116);
    Controls.Add(rct3Group);
    Controls.Add(okayBtn);
    Controls.Add(cancelBtn);
    Controls.Add(applyBtn);
    FormBorderStyle = FormBorderStyle.FixedSingle;
    MaximizeBox = false;
    MinimumSize = new Size(550, 155);
    Name = "SettingsDialog";
    ShowInTaskbar = false;
    StartPosition = FormStartPosition.CenterParent;
    Text = "Settings…";
    rct3Group.ResumeLayout(false);
    rct3Group.PerformLayout();
    ResumeLayout(false);
  }

  #endregion

  private Button applyBtn;
  private Button cancelBtn;
  private Button okayBtn;
  private GroupBox rct3Group;
  private Button browseBtn;
  private TextBox rct3Path;
  private Label pathLbl;
  private FolderBrowserDialog openFolder;
}
