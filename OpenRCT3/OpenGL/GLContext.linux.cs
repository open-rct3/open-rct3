// GLContext (Linux)
//
// Authors:
//   - Nicolas Vyčas Nery <vycasnicolas@gmail.com>
//   - Chance Snow <git@chancesnow.me>
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System;
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;

namespace OpenRCT3.OpenGL;

/// <summary>
/// Loads OpenGL symbols on Linux.
/// </summary>
/// <remarks>
/// Prefers glXGetProcAddress (which can resolve modern, context-specific entry
/// points exposed by the driver) and falls back to plain
/// <c>dlsym(libGL.so.1, ...)</c> for the legacy <c>1.x</c> exports.
/// </remarks>
public partial class GLContext : INativeContext {
  private readonly nint _openglLib;

  public GLContext() {
    _openglLib = dlopen("libGL.so.1", RTLD_NOW | RTLD_GLOBAL);
    if (_openglLib == nint.Zero)
      _openglLib = dlopen("libGL.so", RTLD_NOW | RTLD_GLOBAL);
  }

  public nint GetProcAddress(string procName) {
    try {
      var addr = glXGetProcAddress(procName);
      if (addr != nint.Zero) return addr;
    } catch (DllNotFoundException) {
      // libGL.so.1 not available; fall through to dlsym.
    } catch (EntryPointNotFoundException) {
      // Older drivers may only expose glXGetProcAddressARB; ignore and fall back.
    }
    return _openglLib != nint.Zero ? dlsym(_openglLib, procName) : nint.Zero;
  }

  public nint GetProcAddress(string proc, int? slot = null) => GetProcAddress(proc);

  public bool TryGetProcAddress(string proc, out nint addr, int? slot = null) {
    addr = GetProcAddress(proc);
    return addr != 0;
  }

  public void Dispose() {
    if (_openglLib != nint.Zero) dlclose(_openglLib);
    GC.SuppressFinalize(this);
  }

  private const int RTLD_NOW = 2;
  private const int RTLD_GLOBAL = 0x100;

  [LibraryImport("libdl.so.2", StringMarshalling = StringMarshalling.Utf8)]
  private static partial nint dlopen(string path, int mode);

  [LibraryImport("libdl.so.2", StringMarshalling = StringMarshalling.Utf8)]
  private static partial nint dlsym(nint handle, string symbol);

  [LibraryImport("libdl.so.2")]
  private static partial int dlclose(nint handle);

  [LibraryImport("libGL.so.1", EntryPoint = "glXGetProcAddress", StringMarshalling = StringMarshalling.Utf8)]
  private static partial nint glXGetProcAddress(string proc);
}
