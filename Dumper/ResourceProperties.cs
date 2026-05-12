// ResourceProperties
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Windows.Forms;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;

namespace Dumper;

/// <summary>A simple dialog showing properties of an OVL resource entry.</summary>
sealed partial class ResourceProperties : Form {

  public ResourceProperties(OvlFile file, OvlEntry entry, bool hasViewer) {
    InitializeComponent();

    Text = $@"{file.Name} Properties";
    symbolValue.Text = file.Name;
    fileTypeValue.Text = file.Type.ToDisplayName();
    loaderValue.Text = file.Type.ToTagString();
    dataAddressValue.Text = $@"0x{entry.Offset:X8}";
    sourceFileValue.Text = file.Path;
    viewerValue.Text = hasViewer ? "Yes" : "No";

    // Position okButton within buttonPanel
    okButton.Location = new System.Drawing.Point(
      buttonPanel.ClientSize.Width - okButton.Width - 16, 8);
  }
}
