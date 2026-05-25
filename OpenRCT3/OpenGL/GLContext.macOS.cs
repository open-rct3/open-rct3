using System;
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;

namespace OpenRCT3.OpenGL;

public class GLContext : IGLContext, INativeContext {
  private const int RTLD_LAZY = 1;
  private readonly nint openglLib = dlopen("/System/Library/Frameworks/OpenGL.framework/OpenGL", RTLD_LAZY);

  public nint GetProcAddress(string procName) => dlsym(openglLib, procName);

  public nint GetProcAddress(string proc, int? slot = null) => dlsym(openglLib, proc);

  public bool TryGetProcAddress(string proc, out nint addr, int? slot = null) {
    addr = dlsym(openglLib, proc);
    return addr != 0;
  }

  public void Dispose() {
    if (openglLib != nint.Zero) dlclose(openglLib);
  }

  [DllImport("libSystem.dylib")]
  private extern static nint dlopen(string path, int mode);

  [DllImport("libSystem.dylib")]
  private extern static nint dlsym(nint handle, string symbol);

  [DllImport("libSystem.dylib")]
  private extern static int dlclose(nint handle);
}
