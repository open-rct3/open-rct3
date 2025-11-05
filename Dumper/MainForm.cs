// Main
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025 OpenRCT3 Contributors. All rights reserved.
using System;
using System.Windows.Forms;

namespace Dumper;

public partial class MainForm : Form {
  public MainForm() {
    // TODO: Initialize component in the designer
    // InitializeComponent();

    Width = 800;
    Height = 600;
    Text = "OVL Dumper";
    StartPosition = FormStartPosition.CenterScreen;
  }
}
