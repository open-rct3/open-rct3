// macOS GLContext
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;

namespace OpenRCT3.OpenGL;

public partial class GLContext : IGLContext, INativeContext, IDisposable {
  private const int RTLD_LAZY = 1;
  private readonly MacGlContextLifetime lifetime = new(
    dlopen("/System/Library/Frameworks/OpenGL.framework/OpenGL", RTLD_LAZY));

  public static int PreferredColorDepth => 32;
  public static int PreferredDepthBufferBits => 24;
  public static int PreferredStencilBufferBits => 8;

  public nint GetProcAddress(string procName) => dlsym(lifetime.LibraryHandle, procName);

  public nint GetProcAddress(string proc, int? slot = null) =>
    dlsym(lifetime.LibraryHandle, proc);

  public bool TryGetProcAddress(string proc, out nint addr, int? slot = null) {
    try {
      addr = dlsym(lifetime.LibraryHandle, proc);
      return addr != 0;
    } catch (ObjectDisposedException) {
      addr = nint.Zero;
      return false;
    }
  }

  public void SwapInterval(int interval) {
    lifetime.ThrowIfDisposed();
    const int kCGLCPSwapInterval = 222;
    var context = CGLGetCurrentContext();
    if (context == nint.Zero) return;
    CGLSetParameter(context, kCGLCPSwapInterval, ref interval);
  }

  public void SwapBuffers() {
    lifetime.ThrowIfDisposed();
    var context = lifetime.ContextHandle;
    if (context != nint.Zero) CGLFlushDrawable(context);
  }

  public void MakeCurrent() {
    lifetime.ThrowIfDisposed();
    CGLSetCurrentContext(lifetime.ContextHandle);
  }

  /// <summary>
  /// Updates the CGL context handle used by <see cref="MakeCurrent"/> and <see cref="SwapBuffers"/>.
  /// </summary>
  /// <remarks>
  /// <see cref="CAOpenGLLayer"/> hands us a fresh <c>CGLContextObj</c> on every draw callback rather than
  /// guaranteeing one stays current between frames, so the caller must supply it each frame.
  /// </remarks>
  public void SetCurrentContext(nint handle) => lifetime.SetCurrentContext(handle);

  public void Clear() {
    lifetime.ThrowIfDisposed();
    // glClear is called via Silk.NET GL after context is set
  }

  [System.ComponentModel.Browsable(false)]
  public nint Handle => lifetime.ContextHandle;

  [System.ComponentModel.Browsable(false)]
  public IGLContextSource? Source => null;

  public bool IsCurrent => lifetime.IsCurrent(CGLGetCurrentContext);

  public void Dispose() {
    GC.SuppressFinalize(this);
    var handles = lifetime.Release();
    try {
      if (handles.Context != nint.Zero && CGLGetCurrentContext() == handles.Context)
        CGLSetCurrentContext(nint.Zero);
    } finally {
      if (handles.Library != nint.Zero) dlclose(handles.Library);
    }
  }

  [LibraryImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
  private static partial nint CGLGetCurrentContext();

  [LibraryImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
  private static partial int CGLSetParameter(nint ctx, int parameter, ref int value);

  [LibraryImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
  private static partial void CGLFlushDrawable(nint ctx);

  [LibraryImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
  private static partial void CGLSetCurrentContext(nint ctx);

  [LibraryImport("libSystem.dylib", StringMarshalling = StringMarshalling.Utf8)]
  private static partial nint dlopen(string path, int mode);

  [LibraryImport("libSystem.dylib", StringMarshalling = StringMarshalling.Utf8)]
  private static partial nint dlsym(nint handle, string symbol);

  [LibraryImport("libSystem.dylib")]
  private static partial int dlclose(nint handle);
}
