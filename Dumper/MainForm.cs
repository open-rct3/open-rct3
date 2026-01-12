// Main Form
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025 OpenRCT3 Contributors. All rights reserved.
using Dumper.Models;
using OVL;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Dumper;

public partial class MainForm : Form {
  static readonly string ready = "Ready";
  static readonly string openingArchive = "Opening archive…";

  public MainForm() {
    InitializeComponent();
  }

  private void LoadOvl(Ovl ovl) {
    MessageBox.Show(ovl.Files.Length.ToString());
  }

  private async void openToolStripMenuItem_Click(object sender, EventArgs e) {
    statusLabel.Text = openingArchive;
    progressBar.Visible = true;

    switch (openDialog.ShowDialog()) {
      case DialogResult.OK:
        this.Cursor = Cursors.WaitCursor;
        LoadOvl(await Task.Run(() => Ovl.Open(openDialog.FileName)));
        this.Cursor = Cursors.Default;
        break;
    }

    statusLabel.Text = ready;
    progressBar.Visible = false;
  }
}
