using System;
using System.Runtime.InteropServices;

namespace OpenRCT3.OpenGL;

public class MacOSGLContext : IPlatformGLContext {
  private readonly nint _openglLib;

  public MacOSGLContext() {
    _openglLib = dlopen("/System/Library/Frameworks/OpenGL.framework/OpenGL", RTLD_LAZY);
  }

  public nint GetProcAddress(string procName) {
    return dlsym(_openglLib, procName);
  }

  public void Dispose() {
    if (_openglLib != nint.Zero) dlclose(_openglLib);
  }

  private const int RTLD_LAZY = 1;

  [DllImport("libSystem.dylib")]
  private static extern nint dlopen(string path, int mode);

  [DllImport("libSystem.dylib")]
  private static extern nint dlsym(nint handle, string symbol);

  [DllImport("libSystem.dylib")]
  private static extern int dlclose(nint handle);
}
