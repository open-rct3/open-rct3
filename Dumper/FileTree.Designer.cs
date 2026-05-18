namespace Dumper {
  partial class FileTree {
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing) {
      if (disposing && (components != null)) {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Component Designer generated code

    private void InitializeComponent() {
      treeView = new System.Windows.Forms.TreeView();
      SuspendLayout();
      // 
      // treeView
      // 
      treeView.Dock = System.Windows.Forms.DockStyle.Fill;
      treeView.Location = new System.Drawing.Point(0, 0);
      treeView.Name = "treeView";
      treeView.ShowNodeToolTips = true;
      treeView.Size = new System.Drawing.Size(225, 312);
      treeView.TabIndex = 0;
      treeView.AfterSelect += treeView_AfterSelect;
      treeView.NodeMouseClick += treeView_NodeMouseClick;
      // 
      // FileTree
      // 
      AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      Controls.Add(treeView);
      Name = "FileTree";
      Size = new System.Drawing.Size(225, 312);
      ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.TreeView treeView;
  }
}
