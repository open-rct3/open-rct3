// Win32 System Menu Customization
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Linq;
using System.Windows.Forms;
using static OpenRCT3.Platforms.Windows.Win32;

namespace OpenRCT3.Platforms.Windows;

/// <summary>
/// System command menu item IDs.
/// </summary>
/// <remarks>
/// Values <i>below</i> <c>0xF000</c> are acceptable for application use.
/// </remarks>
/// <seealso href="https://learn.microsoft.com/en-us/windows/win32/menurc/wm-syscommand#parameters"><c>WM_SYSCOMMAND</c> Parameters</seealso>
internal enum Command : uint {
  Unknown = 0,
  OpenLog = 0x0001,
  /// <summary>
  /// Sizes the window.
  /// </summary>
  Size = 0xF000,
  /// <summary>
  /// Moves the window.
  /// </summary>
  Move = 0xF010,
  /// <summary>
  /// Minimizes the window.
  /// </summary>
  Minimize = 0xF020,
  /// <summary>
  /// Maximizes the window.
  /// </summary>
  Maximize = 0xF030,
  /// <summary>
  /// Restores the window to its normal position and size.
  /// </summary>
  Restore = 0xF120,
  /// <summary>
  /// Activates the Start menu.
  /// </summary>
  TaskList = 0xF130,
  /// <summary>
  /// Changes the cursor to a question mark with a pointer. If the user then clicks a control in the window, the
  /// control receives a <c>WM_HELP</c> message.
  /// </summary>
  ContextualHelp = 0xF180,
  /// <summary>
  /// Closes the window.
  /// </summary>
  Close = 0xF060,
}

internal static class SystemMenu {
  private static readonly Command[] commands = Enum.GetValues<Command>();

  // Menu flags
  private const uint MF_SEPARATOR = 0x00000800;
  private const uint MF_STRING = 0x00000000;
  private const uint MF_BYPOSITION = 0x00000400;

  public const int WM_SYSCOMMAND = 0x112;

  public static void AddItems(Form form) {
    var hMenu = GetSystemMenu(form.Handle, false);
    if (hMenu == IntPtr.Zero) return;

    // Get the position before the Close item (which is last)
    int itemCount = GetMenuItemCount(hMenu);
    uint insertPos = (uint)(itemCount - 1);

    // Insert custom menu items before Close
    InsertMenu(hMenu, insertPos, MF_BYPOSITION | MF_SEPARATOR, 0, nint.Zero);
    InsertMenu(hMenu, insertPos, MF_BYPOSITION | MF_STRING, (uint)Command.OpenLog, "Open Log");
  }

  public static bool TryGetCommand(ref Message m, out Command command) {
    command = Command.Unknown;
    if (m.Msg != WM_SYSCOMMAND) return false;

    int cmd = m.WParam.ToInt32();
    if (commands.Any(c => (uint)c == cmd)) {
      command = (Command)cmd;
      return true;
    }

    return false;
  }
}
