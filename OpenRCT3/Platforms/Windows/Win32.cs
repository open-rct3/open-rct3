using System;
using System.Runtime.InteropServices;

namespace OpenRCT3.Platforms.Windows;

internal static class Win32 {
  public enum WindowLongs : int {
    GWL_STYLE = -16,
    GWL_EXSTYLE = -20,
  }

  [Flags]
  public enum WindowStyles : uint {
    WS_CHILD = 0x40000000,
    WS_DISABLED = 0x08000000,
    WS_VISIBLE = 0x10000000,
    WS_CLIPSIBLINGS = 0x04000000,
    WS_CLIPCHILDREN = 0x02000000,
  }

  [Flags]
  public enum WindowStylesEx : uint {
    WS_EX_NOACTIVATE = 0x08000000,
  }

  [DllImport("user32.dll", SetLastError = true)]
  public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

  public static IntPtr GetWindowLongPtr(IntPtr hWnd, WindowLongs nIndex)
    => GetWindowLongPtr(hWnd, (int)nIndex);

  public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    => IntPtr.Size == 8
      ? GetWindowLongPtr64(hWnd, nIndex)
      : GetWindowLongPtr32(hWnd, nIndex);

  [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
  private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

  [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
  private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

  public static IntPtr SetWindowLongPtr(IntPtr hWnd, WindowLongs nIndex, IntPtr dwNewLong)
    => SetWindowLongPtr(hWnd, (int)nIndex, dwNewLong);

  public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    => IntPtr.Size == 8
      ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
      : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

  [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
  private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

  [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
  private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
