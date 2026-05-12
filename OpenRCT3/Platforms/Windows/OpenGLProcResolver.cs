using System;
using System.Runtime.InteropServices;

namespace OpenRCT3.Platforms.Windows;

internal static class OpenGLProcResolver {
  private const string OPENGL32 = "opengl32.dll";

  [DllImport(OPENGL32, EntryPoint = "wglGetProcAddress", CharSet = CharSet.Ansi)]
  private static extern IntPtr WglGetProcAddress(string proc);

  [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
  private static extern IntPtr GetProcAddress(IntPtr hModule, string proc);

  private static readonly IntPtr OpenGLModule = NativeLibrary.Load(OPENGL32);

  public static IntPtr GetProc(string name, Version? glVersion = null) {
    glVersion ??= SurfaceSettings.DefaultVersion;

    // First try wglGetProcAddress (modern/context-specific functions)
    var addr = WglGetProcAddress(name);
    if (addr != IntPtr.Zero) return addr;

    // Fallback to opengl32.dll exports
    addr = GetProcAddress(OpenGLModule, name);
    if (addr != IntPtr.Zero) return addr;

    // Optionally add version-specific logic here
    return IntPtr.Zero;
  }
}
