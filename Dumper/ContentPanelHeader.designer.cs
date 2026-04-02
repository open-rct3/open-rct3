namespace Dumper {
  partial class ContentPanelHeader {
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
      nameLabel = new System.Windows.Forms.Label();
      viewerCombo = new System.Windows.Forms.ComboBox();
      SuspendLayout();
      //
      // nameLabel
      //
      nameLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      nameLabel.Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont.FontFamily, System.Drawing.SystemFonts.DefaultFont.Size, System.Drawing.FontStyle.Bold);
      nameLabel.Name = "nameLabel";
      nameLabel.Padding = new System.Windows.Forms.Padding(0, 0, 100, 0);
      nameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
      //
      // viewerCombo
      //
      viewerCombo.Dock = System.Windows.Forms.DockStyle.Right;
      viewerCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      viewerCombo.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      viewerCombo.Margin = new System.Windows.Forms.Padding(0, 4, 4, 4);
      viewerCombo.Name = "viewerCombo";
      viewerCombo.Size = new System.Drawing.Size(180, 23);
      viewerCombo.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
      //
      // ContentPanelHeader
      //
      BackColor = System.Drawing.SystemColors.Control;
      BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
      Controls.Add(nameLabel);
      Controls.Add(viewerCombo);
      Dock = System.Windows.Forms.DockStyle.Top;
      Height = 32;
      Name = "ContentPanelHeader";
      ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.Label nameLabel;
    private System.Windows.Forms.ComboBox viewerCombo;
  }
}
