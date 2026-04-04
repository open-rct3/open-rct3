using System;
using System.Runtime.InteropServices;

namespace OpenRCT3.Platforms.Windows;

internal static class Win32 {
  public readonly struct HWND(IntPtr value) {
    public readonly IntPtr Value = value;

    public static implicit operator IntPtr(HWND h) => h.Value;
    public static implicit operator HWND(IntPtr p) => new HWND(p);
    public override string ToString() => Value.ToString();
  }

  const string USER32 = "user32.dll";
  const string GDI32 = "gdi32.dll";
  const string OPENGL32 = "opengl32.dll";

  [DllImport(USER32, SetLastError = true)]
  public static extern IntPtr GetDC(IntPtr hWnd);

  [DllImport(USER32, SetLastError = true)]
  public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

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

  [DllImport(GDI32, SetLastError = true)]
  public static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR pfd);

  [DllImport(GDI32, SetLastError = true)]
  public static extern bool SetPixelFormat(IntPtr hdc, int format, ref PIXELFORMATDESCRIPTOR pfd);

  [DllImport(OPENGL32, SetLastError = true)]
  public static extern IntPtr wglCreateContext(IntPtr hdc);

  [DllImport(OPENGL32, SetLastError = true)]
  public static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

  [DllImport(OPENGL32, SetLastError = true)]
  public static extern bool wglDeleteContext(IntPtr hglrc);

  [DllImport("gdi32.dll", SetLastError = true)]
  public static extern bool SwapBuffers(IntPtr hdc);

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
