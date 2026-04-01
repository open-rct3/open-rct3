// Main Form
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025-2026 OpenRCT3 Contributors. All rights reserved.
using Dumper.Models;
using OVL;
using OVL.Files;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Dumper;

public partial class MainForm : Form {
  static readonly string ready = "Ready";
  static readonly string openingArchive = "Opening archive…";

  public MainForm() {
    InitializeComponent();
    InitializeComponentIcons();
  }

  private async Task OpenOvl() {
    switch (openDialog.ShowDialog()) {
      case DialogResult.OK:
        this.Cursor = Cursors.WaitCursor;
        LoadOvl(await Task.Run(() => Ovl.Open(openDialog.FileName)));
        this.Cursor = Cursors.Default;
        break;
    }
  }

  private void LoadOvl(Ovl ovl) {
    treeView.BeginUpdate();
    treeView.Nodes.Clear();

    var root = treeView.Nodes.Add(ovl.FileName, Path.GetFileName(ovl.FileName));

    // Convert each tag to its FileType and group by FileType
    var groupsByFileType = ovl.LoaderHeaders
      .GroupBy(h => h.tag.ToFileType())
      .ToDictionary(g => g.Key, g => g.GroupBy(h => h.tag).ToList());

    // Order by FileType enum value; known types first, Unknown last
    var orderedFileTypes = Enum.GetValues<FileType>();

    foreach (var fileType in orderedFileTypes) {
      if (!groupsByFileType.TryGetValue(fileType, out var tagGroups))
        continue;

      var groupNode = root.Nodes.Add(fileType.ToString(), fileType.ToDisplayName());
      foreach (var tagGroup in tagGroups) {
        foreach (var header in tagGroup)
          groupNode.Nodes.Add(header.name);
      }
    }

    root.Expand();
    treeView.EndUpdate();
  }

  private async void openToolStripMenuItem_Click(object sender, EventArgs e) {
    statusLabel.Text = openingArchive;
    progressBar.Visible = true;
    await OpenOvl();
    statusLabel.Text = ready;
    progressBar.Visible = false;
  }

  private async void openArchiveToolStripButton_Click(object sender, EventArgs e) {
    statusLabel.Text = openingArchive;
    progressBar.Visible = true;
    await OpenOvl();
    statusLabel.Text = ready;
    progressBar.Visible = false;
  }

  private void exitToolStripMenuItem_Click(object sender, EventArgs e) {
    Application.Exit();
  }
}
