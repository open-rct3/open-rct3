using System.Runtime.InteropServices;

namespace OpenRCT3.Platforms.Windows;

internal static partial class Win32 {
  private const string USER32 = "user32.dll";
  private const string GDI32 = "gdi32.dll";

  public readonly struct HWND(IntPtr value) {
    public readonly IntPtr Value = value;

    public static implicit operator IntPtr(HWND h) => h.Value;
    public static implicit operator HWND(IntPtr p) => new HWND(p);
    public override string ToString() => Value.ToString();
  }

  [LibraryImport(USER32, SetLastError = true)]
  public static partial nint GetDC(nint hWnd);

  [LibraryImport(USER32, SetLastError = true)]
  public static partial int ReleaseDC(nint hWnd, nint hDC);

  [StructLayout(LayoutKind.Sequential)]
  public struct PIXELFORMATDESCRIPTOR {
    public ushort nSize;
    public ushort nVersion;
    public uint dwFlags;
    public byte iPixelType;
    public byte cColorBits;
    public byte cRedBits;
    public byte cRedShift;
    public byte cGreenBits;
    public byte cGreenShift;
    public byte cBlueBits;
    public byte cBlueShift;
    public byte cAlphaBits;
    public byte cAlphaShift;
    public byte cAccumBits;
    public byte cAccumRedBits;
    public byte cAccumGreenBits;
    public byte cAccumBlueBits;
    public byte cAccumAlphaBits;
    public byte cDepthBits;
    public byte cStencilBits;
    public byte cAuxBuffers;
    public sbyte iLayerType;
    public byte bReserved;
    public uint dwLayerMask;
    public uint dwVisibleMask;
    public uint dwDamageMask;
  }

  [LibraryImport(GDI32, SetLastError = true)]
  public static partial nint ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR pfd);

  [LibraryImport(GDI32, SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool SetPixelFormat(IntPtr hdc, nint format, ref PIXELFORMATDESCRIPTOR pfd);

  [LibraryImport("gdi32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool SwapBuffers(IntPtr hdc);

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

  [LibraryImport("user32.dll", SetLastError = true)]
  public static partial IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

  public static IntPtr GetWindowLongPtr(IntPtr hWnd, WindowLongs nIndex)
    => GetWindowLongPtr(hWnd, (int)nIndex);

  public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    => IntPtr.Size == 8
      ? GetWindowLongPtr64(hWnd, nIndex)
      : GetWindowLongPtr32(hWnd, nIndex);

  [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
  private static partial IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

  [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
  private static partial IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

  public static IntPtr SetWindowLongPtr(IntPtr hWnd, WindowLongs nIndex, IntPtr dwNewLong)
    => SetWindowLongPtr(hWnd, (int)nIndex, dwNewLong);

  public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    => IntPtr.Size == 8
      ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
      : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

  [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
  private static partial int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

  [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
  private static partial IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
