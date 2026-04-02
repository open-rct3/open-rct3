namespace Dumper {
  partial class ContentPanel {
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
      header = new Dumper.ContentPanelHeader();
      webView = new Microsoft.Web.WebView2.WinForms.WebView2();
      emptyLabel = new System.Windows.Forms.Label();
      SuspendLayout();
      //
      // header
      //
      header.Dock = System.Windows.Forms.DockStyle.Top;
      header.Name = "header";
      //
      // webView
      //
      webView.DefaultBackgroundColor = System.Drawing.Color.White;
      webView.Dock = System.Windows.Forms.DockStyle.Fill;
      webView.Name = "webView";
      webView.Visible = false;
      //
      // emptyLabel
      //
      emptyLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      emptyLabel.Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont.FontFamily, 11F);
      emptyLabel.ForeColor = System.Drawing.SystemColors.GrayText;
      emptyLabel.Name = "emptyLabel";
      emptyLabel.Text = "Select a file to view";
      emptyLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
      //
      // ContentPanel
      //
      AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      Controls.Add(webView);
      Controls.Add(emptyLabel);
      Controls.Add(header);
      Name = "ContentPanel";
      Size = new System.Drawing.Size(400, 300);
      ResumeLayout(false);
    }

    #endregion

    private Dumper.ContentPanelHeader header;
    private Microsoft.Web.WebView2.WinForms.WebView2 webView;
    private System.Windows.Forms.Label emptyLabel;
  }
}
