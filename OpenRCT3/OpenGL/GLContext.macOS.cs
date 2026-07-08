using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Silk.NET.Core.Contexts;

namespace OpenRCT3.OpenGL;

public partial class GLContext : IGLContext, INativeContext {
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

  public void SwapInterval(int interval) {
    const int kCGLCPSwapInterval = 222;
    var context = CGLGetCurrentContext();
    if (context == nint.Zero) return;
    CGLSetParameter(context, kCGLCPSwapInterval, ref interval);
  }

  [LibraryImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
  private static partial nint CGLGetCurrentContext();

  [LibraryImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
  private static partial int CGLSetParameter(nint ctx, int parameter, ref int value);

  [LibraryImport("libSystem.dylib", StringMarshalling = StringMarshalling.Utf8)]
  private static partial nint dlopen(string path, int mode);

  [LibraryImport("libSystem.dylib", StringMarshalling = StringMarshalling.Utf8)]
  private static partial nint dlsym(nint handle, string symbol);

  [LibraryImport("libSystem.dylib")]
  private static partial int dlclose(nint handle);
}
