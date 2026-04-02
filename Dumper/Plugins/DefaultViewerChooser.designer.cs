namespace Dumper {
  partial class DefaultViewerChooser {
    private System.ComponentModel.IContainer components = null;

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
      fileTypeLabel = new System.Windows.Forms.Label();
      fileTypeCombo = new System.Windows.Forms.ComboBox();
      currentDefaultLabel = new System.Windows.Forms.Label();
      setDefaultButton = new System.Windows.Forms.Button();
      cancelButton = new System.Windows.Forms.Button();
      SuspendLayout();
      //
      // fileTypeLabel
      //
      fileTypeLabel.AutoSize = true;
      fileTypeLabel.Location = new System.Drawing.Point(14, 18);
      fileTypeLabel.Name = "fileTypeLabel";
      fileTypeLabel.Size = new System.Drawing.Size(53, 15);
      fileTypeLabel.TabIndex = 0;
      fileTypeLabel.Text = "File type:";
      //
      // fileTypeCombo
      //
      fileTypeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      fileTypeCombo.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      fileTypeCombo.Location = new System.Drawing.Point(90, 15);
      fileTypeCombo.Name = "fileTypeCombo";
      fileTypeCombo.Size = new System.Drawing.Size(250, 23);
      fileTypeCombo.TabIndex = 1;
      //
      // currentDefaultLabel
      //
      currentDefaultLabel.ForeColor = System.Drawing.SystemColors.GrayText;
      currentDefaultLabel.Location = new System.Drawing.Point(14, 48);
      currentDefaultLabel.Name = "currentDefaultLabel";
      currentDefaultLabel.Size = new System.Drawing.Size(330, 20);
      currentDefaultLabel.TabIndex = 2;
      //
      // setDefaultButton
      //
      setDefaultButton.DialogResult = System.Windows.Forms.DialogResult.OK;
      setDefaultButton.Location = new System.Drawing.Point(186, 90);
      setDefaultButton.Name = "setDefaultButton";
      setDefaultButton.Size = new System.Drawing.Size(80, 28);
      setDefaultButton.TabIndex = 3;
      setDefaultButton.Text = "Set Default";
      setDefaultButton.UseVisualStyleBackColor = true;
      //
      // cancelButton
      //
      cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      cancelButton.Location = new System.Drawing.Point(270, 90);
      cancelButton.Name = "cancelButton";
      cancelButton.Size = new System.Drawing.Size(75, 28);
      cancelButton.TabIndex = 4;
      cancelButton.Text = "Cancel";
      cancelButton.UseVisualStyleBackColor = true;
      //
      // DefaultViewerChooser
      //
      AcceptButton = setDefaultButton;
      AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      CancelButton = cancelButton;
      ClientSize = new System.Drawing.Size(360, 140);
      Controls.Add(fileTypeLabel);
      Controls.Add(fileTypeCombo);
      Controls.Add(currentDefaultLabel);
      Controls.Add(setDefaultButton);
      Controls.Add(cancelButton);
      FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      MaximizeBox = false;
      MinimizeBox = false;
      Name = "DefaultViewerChooser";
      ShowInTaskbar = false;
      StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      Text = "Choose a Default Viewer";
      ResumeLayout(false);
      PerformLayout();
    }

    #endregion

    private System.Windows.Forms.Label fileTypeLabel;
    private System.Windows.Forms.ComboBox fileTypeCombo;
    private System.Windows.Forms.Label currentDefaultLabel;
    private System.Windows.Forms.Button setDefaultButton;
    private System.Windows.Forms.Button cancelButton;
  }
}
