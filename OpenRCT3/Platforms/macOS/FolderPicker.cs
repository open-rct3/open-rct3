// MacFolderPicker
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using AppKit;
using Foundation;

namespace OpenRCT3.Platforms.macOS;

public class FolderPicker : IFolderPicker {
    public string? PickFolder(string title, string? initialPath = null) {
        var panel = new NSOpenPanel {
            Title = title,
            CanChooseDirectories = true,
            CanChooseFiles = false,
            AllowsMultipleSelection = false
        };

        if (!string.IsNullOrEmpty(initialPath)) {
            panel.DirectoryUrl = NSUrl.FromFilename(initialPath);
        }

        if (panel.RunModal() == (long)NSModalResponse.OK) {
            return panel.Url?.Path;
        }
        return null;
    }
}
