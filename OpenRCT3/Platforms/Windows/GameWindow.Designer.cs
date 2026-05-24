namespace OpenRCT3.Platforms.Windows {
  partial class GameWindow {
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing) {
      if (disposing && (components != null)) {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent() {
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GameWindow));
      glSurface = new GLSurface();
      SuspendLayout();
      // 
      // glSurface
      // 
      glSurface.Dock = System.Windows.Forms.DockStyle.Fill;
      glSurface.Location = new System.Drawing.Point(0, 0);
      glSurface.Name = "glSurface";
      glSurface.Size = new System.Drawing.Size(624, 381);
      glSurface.TabIndex = 0;
      // 
      // MainForm
      // 
      AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      ClientSize = new System.Drawing.Size(624, 381);
      Controls.Add(glSurface);
      Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
      MinimumSize = new System.Drawing.Size(640, 420);
      Name = "MainForm";
      StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      Text = "OpenRCT3";
      ResumeLayout(false);
    }

    #endregion

    private GLSurface glSurface;
  }
}
