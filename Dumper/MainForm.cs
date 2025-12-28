// Main Form
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025 OpenRCT3 Contributors. All rights reserved.
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

  private async void openToolStripMenuItem_Click(object sender, EventArgs e) {
    statusLabel.Text = openingArchive;
    progressBar.Visible = true;

    switch (openDialog.ShowDialog()) {
      case DialogResult.OK:
        var ovl = await Task.Run(() => Ovl.Open(openDialog.FileName));
        break;
    }

    statusLabel.Text = ready;
    progressBar.Visible = false;
  }
}
