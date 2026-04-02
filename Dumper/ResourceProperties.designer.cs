namespace Dumper {
  partial class ResourceProperties {
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
      table = new System.Windows.Forms.TableLayoutPanel();
      symbolLabel = new System.Windows.Forms.Label();
      symbolValue = new System.Windows.Forms.Label();
      fileTypeLabel = new System.Windows.Forms.Label();
      fileTypeValue = new System.Windows.Forms.Label();
      loaderLabel = new System.Windows.Forms.Label();
      loaderValue = new System.Windows.Forms.Label();
      dataAddressLabel = new System.Windows.Forms.Label();
      dataAddressValue = new System.Windows.Forms.Label();
      sourceFileLabel = new System.Windows.Forms.Label();
      sourceFileValue = new System.Windows.Forms.Label();
      viewerLabel = new System.Windows.Forms.Label();
      viewerValue = new System.Windows.Forms.Label();
      buttonPanel = new System.Windows.Forms.Panel();
      okButton = new System.Windows.Forms.Button();
      table.SuspendLayout();
      buttonPanel.SuspendLayout();
      SuspendLayout();
      //
      // table
      //
      table.ColumnCount = 2;
      table.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130F));
      table.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      table.Controls.Add(symbolLabel, 0, 0);
      table.Controls.Add(symbolValue, 1, 0);
      table.Controls.Add(fileTypeLabel, 0, 1);
      table.Controls.Add(fileTypeValue, 1, 1);
      table.Controls.Add(loaderLabel, 0, 2);
      table.Controls.Add(loaderValue, 1, 2);
      table.Controls.Add(dataAddressLabel, 0, 3);
      table.Controls.Add(dataAddressValue, 1, 3);
      table.Controls.Add(sourceFileLabel, 0, 4);
      table.Controls.Add(sourceFileValue, 1, 4);
      table.Controls.Add(viewerLabel, 0, 5);
      table.Controls.Add(viewerValue, 1, 5);
      table.Dock = System.Windows.Forms.DockStyle.Fill;
      table.Location = new System.Drawing.Point(0, 0);
      table.Name = "table";
      table.RowCount = 6;
      table.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
      table.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
      table.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
      table.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
      table.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
      table.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
      table.Padding = new System.Windows.Forms.Padding(16, 12, 16, 6);
      table.AutoSize = true;
      table.TabIndex = 0;
      //
      // symbolLabel
      //
      symbolLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
      symbolLabel.AutoSize = true;
      symbolLabel.Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont.FontFamily, System.Drawing.SystemFonts.DefaultFont.Size, System.Drawing.FontStyle.Bold);
      symbolLabel.Text = "Symbol:";
      //
      // symbolValue
      //
      symbolValue.Anchor = System.Windows.Forms.AnchorStyles.Left;
      symbolValue.AutoSize = true;
      //
      // fileTypeLabel
      //
      fileTypeLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
      fileTypeLabel.AutoSize = true;
      fileTypeLabel.Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont.FontFamily, System.Drawing.SystemFonts.DefaultFont.Size, System.Drawing.FontStyle.Bold);
      fileTypeLabel.Text = "File Type:";
      //
      // fileTypeValue
      //
      fileTypeValue.Anchor = System.Windows.Forms.AnchorStyles.Left;
      fileTypeValue.AutoSize = true;
      //
      // loaderLabel
      //
      loaderLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
      loaderLabel.AutoSize = true;
      loaderLabel.Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont.FontFamily, System.Drawing.SystemFonts.DefaultFont.Size, System.Drawing.FontStyle.Bold);
      loaderLabel.Text = "Loader:";
      //
      // loaderValue
      //
      loaderValue.Anchor = System.Windows.Forms.AnchorStyles.Left;
      loaderValue.AutoSize = true;
      //
      // dataAddressLabel
      //
      dataAddressLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
      dataAddressLabel.AutoSize = true;
      dataAddressLabel.Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont.FontFamily, System.Drawing.SystemFonts.DefaultFont.Size, System.Drawing.FontStyle.Bold);
      dataAddressLabel.Text = "Data Address:";
      //
      // dataAddressValue
      //
      dataAddressValue.Anchor = System.Windows.Forms.AnchorStyles.Left;
      dataAddressValue.AutoSize = true;
      //
      // sourceFileLabel
      //
      sourceFileLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
      sourceFileLabel.AutoSize = true;
      sourceFileLabel.Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont.FontFamily, System.Drawing.SystemFonts.DefaultFont.Size, System.Drawing.FontStyle.Bold);
      sourceFileLabel.Text = "Source File:";
      //
      // sourceFileValue
      //
      sourceFileValue.Anchor = System.Windows.Forms.AnchorStyles.Left;
      sourceFileValue.AutoSize = true;
      //
      // viewerLabel
      //
      viewerLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
      viewerLabel.AutoSize = true;
      viewerLabel.Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont.FontFamily, System.Drawing.SystemFonts.DefaultFont.Size, System.Drawing.FontStyle.Bold);
      viewerLabel.Text = "Viewer Available:";
      //
      // viewerValue
      //
      viewerValue.Anchor = System.Windows.Forms.AnchorStyles.Left;
      viewerValue.AutoSize = true;
      //
      // buttonPanel
      //
      buttonPanel.Controls.Add(okButton);
      buttonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
      buttonPanel.Height = 44;
      buttonPanel.Padding = new System.Windows.Forms.Padding(0, 0, 16, 12);
      buttonPanel.Name = "buttonPanel";
      //
      // okButton
      //
      okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
      okButton.Name = "okButton";
      okButton.Size = new System.Drawing.Size(75, 28);
      okButton.TabIndex = 0;
      okButton.Text = "OK";
      okButton.UseVisualStyleBackColor = true;
      okButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
      //
      // ResourceProperties
      //
      AcceptButton = okButton;
      AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      CancelButton = okButton;
      ClientSize = new System.Drawing.Size(380, 210);
      Controls.Add(table);
      Controls.Add(buttonPanel);
      FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      MaximizeBox = false;
      MinimizeBox = false;
      ShowInTaskbar = false;
      StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      Name = "ResourceProperties";
      table.ResumeLayout(false);
      table.PerformLayout();
      buttonPanel.ResumeLayout(false);
      ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel table;
    private System.Windows.Forms.Label symbolLabel;
    private System.Windows.Forms.Label symbolValue;
    private System.Windows.Forms.Label fileTypeLabel;
    private System.Windows.Forms.Label fileTypeValue;
    private System.Windows.Forms.Label loaderLabel;
    private System.Windows.Forms.Label loaderValue;
    private System.Windows.Forms.Label dataAddressLabel;
    private System.Windows.Forms.Label dataAddressValue;
    private System.Windows.Forms.Label sourceFileLabel;
    private System.Windows.Forms.Label sourceFileValue;
    private System.Windows.Forms.Label viewerLabel;
    private System.Windows.Forms.Label viewerValue;
    private System.Windows.Forms.Panel buttonPanel;
    private System.Windows.Forms.Button okButton;
  }
}
