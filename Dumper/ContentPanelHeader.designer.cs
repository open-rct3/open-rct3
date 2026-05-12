using System.Windows.Forms;
using System.Xml;

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
      nameLabel.AutoSize = true;
      nameLabel.Name = "nameLabel";
      nameLabel.Padding = new System.Windows.Forms.Padding(0, 7, 0, 0);
      nameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
      //
      // viewerCombo
      //
      viewerCombo.Enabled = false;
      viewerCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      viewerCombo.Margin = new System.Windows.Forms.Padding(0, 4, 4, 4);
      viewerCombo.Name = "viewerCombo";
      viewerCombo.Size = new System.Drawing.Size(125, 23);
      viewerCombo.MaximumSize = new System.Drawing.Size(175, 23);
      //
      // ContentPanelHeader
      //
      BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
      ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130F));
      RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      Controls.Add(nameLabel, 0, 0);
      Controls.Add(viewerCombo, 1, 0);
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
