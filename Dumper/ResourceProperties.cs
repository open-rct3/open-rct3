// ResourceProperties
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Windows.Forms;
using OVL;
using OVL.Files;

namespace Dumper;

/// <summary>A simple dialog showing properties of an OVL resource entry.</summary>
sealed partial class ResourceProperties : Form {

  public ResourceProperties(OvlLoaderEntry entry, FileType fileType, bool hasViewer) {
    InitializeComponent();

    Text = $"{entry.SymbolName} Properties";
    symbolValue.Text = entry.SymbolName;
    fileTypeValue.Text = fileType.ToDisplayName();
    loaderValue.Text = entry.Name;
    dataAddressValue.Text = $"0x{entry.DataAddress:X8}";
    sourceFileValue.Text = entry.SourceFile;
    viewerValue.Text = hasViewer ? "Yes" : "No";

    // Position okButton within buttonPanel
    okButton.Location = new System.Drawing.Point(
      buttonPanel.ClientSize.Width - okButton.Width - 16, 8);
  }
}
