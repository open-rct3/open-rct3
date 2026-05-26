// IFolderPicker
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenRCT3.Platforms;

public interface IFolderPicker : IDisposable {
  /// <summary>
  /// Displays a folder picker dialog to the user.
  /// </summary>
  /// <returns>The selected path, or null if the user cancelled.</returns>
  string? PickFolder(string title, string? initialPath = null);
}
