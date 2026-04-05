using System;
using System.Runtime.InteropServices;

namespace OpenRCT3.OpenGL;

public class GLContext : IPlatformGLContext {
  private readonly nint _openglLib;

  public GLContext() {
    _openglLib = LoadLibrary("opengl32.dll");
  }

  public nint GetProcAddress(string procName) {
    var addr = wglGetProcAddress(procName);
    if (addr != nint.Zero) return addr;
    return GetProcAddress(_openglLib, procName);
  }

  public void Dispose() {
    if (_openglLib != nint.Zero) FreeLibrary(_openglLib);
  }

  [DllImport("opengl32.dll")]
  private static extern nint wglGetProcAddress(string proc);

  [DllImport("kernel32.dll")]
  private static extern nint LoadLibrary(string lib);

  [DllImport("kernel32.dll")]
  private static extern bool FreeLibrary(nint lib);

  [DllImport("kernel32.dll")]
  private static extern nint GetProcAddress(nint lib, string proc);
}
