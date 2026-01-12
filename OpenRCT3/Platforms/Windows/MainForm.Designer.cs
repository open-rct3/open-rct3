namespace OpenRCT3.Platforms.Windows
{
  partial class MainForm
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
      SuspendLayout();
      // 
      // MainForm
      // 
      AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      ClientSize = new System.Drawing.Size(624, 381);
      Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
      MinimumSize = new System.Drawing.Size(640, 420);
      Name = "MainForm";
      StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      Text = "OpenRCT3";
      DpiChanged += MainForm_DpiChanged;
      Resize += MainForm_Resize;
      ResumeLayout(false);
    }

    #endregion

  }
}
