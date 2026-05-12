// WindowsFolderPicker
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Windows.Forms;

namespace OpenRCT3.Platforms.Windows;

public class FolderPicker : IFolderPicker {
    public string? PickFolder(string title, string? initialPath = null) {
        using var dialog = new FolderBrowserDialog {
            Description = title,
            UseDescriptionForTitle = true,
            SelectedPath = initialPath ?? string.Empty,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == DialogResult.OK) {
            return dialog.SelectedPath;
        }
        return null;
    }
}
