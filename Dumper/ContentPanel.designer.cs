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
      header = new ContentPanelHeader();
      webView = new Microsoft.Web.WebView2.WinForms.WebView2();
      ((System.ComponentModel.ISupportInitialize)webView).BeginInit();
      SuspendLayout();
      // 
      // header
      //
      header.Dock = System.Windows.Forms.DockStyle.Top;
      header.Location = new System.Drawing.Point(0, 0);
      header.Name = "header";
      header.Size = new System.Drawing.Size(400, 32);
      header.TabIndex = 1;
      // 
      // webView
      // 
      webView.AllowExternalDrop = true;
      webView.CreationProperties = null;
      webView.DefaultBackgroundColor = System.Drawing.Color.White;
      webView.Dock = System.Windows.Forms.DockStyle.Fill;
      webView.Location = new System.Drawing.Point(0, 32);
      webView.Name = "webView";
      webView.Size = new System.Drawing.Size(400, 268);
      webView.TabIndex = 0;
      webView.ZoomFactor = 1D;
      // 
      // ContentPanel
      // 
      AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      Controls.Add(webView);
      Controls.Add(header);
      Name = "ContentPanel";
      Size = new System.Drawing.Size(400, 300);
      ((System.ComponentModel.ISupportInitialize)webView).EndInit();
      ResumeLayout(false);
    }

    #endregion

    private Dumper.ContentPanelHeader header;
    private Microsoft.Web.WebView2.WinForms.WebView2 webView;
  }
}
