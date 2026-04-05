using System;
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;

namespace OpenRCT3.OpenGL;

public class GLContext : IPlatformGLContext, INativeContext {
  private readonly nint _openglLib;

  public GLContext() {
    _openglLib = dlopen("/System/Library/Frameworks/OpenGL.framework/OpenGL", RTLD_LAZY);
  }

  public nint GetProcAddress(string procName) {
    return dlsym(_openglLib, procName);
  }

  public nint GetProcAddress(string proc, int? slot = null) {
    return dlsym(_openglLib, proc);
  }

  public bool TryGetProcAddress(string proc, out nint addr, int? slot = null) {
    addr = dlsym(_openglLib, proc);
    return addr != 0;
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
