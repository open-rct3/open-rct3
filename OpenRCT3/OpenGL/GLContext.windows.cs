// OpenGL Context
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Platforms;
using System.Runtime.InteropServices;

namespace OpenRCT3.OpenGL;

public class GLContext(SurfaceSettings settings) : IGLContext {
  private const string OPENGL32 = "opengl32.dll";
  private readonly nint openglLib = LoadLibrary(OPENGL32);

  public SurfaceSettings Settings { get; } = settings;

  public void Dispose() {
    if (openglLib == nint.Zero) return;
    GC.SuppressFinalize(this);
    FreeLibrary(openglLib);
  }

  public nint GetProcAddress(string procName) => GetProcAddress(procName, null);

  public nint GetProcAddress(string proc, int? slot = null) {
    var addr = GetProcAddress(openglLib, proc);
    if (addr != nint.Zero) return addr;
    // Fallback to extern DLL import
    return WglGetProcAddress(proc);
  }

  public bool TryGetProcAddress(string proc, out nint addr, int? slot = null) {
    try {
      addr = GetProcAddress(proc, null);
      if (addr == nint.Zero) return false;
      return true;
    } catch {
      addr = nint.Zero;
      return false;
    }
  }

  public override string ToString() => base.ToString() ?? nameof(GLContext);

  [DllImport(OPENGL32, EntryPoint = "wglGetProcAddress", CharSet = CharSet.Ansi)]
  private static extern nint WglGetProcAddress(string proc);

  [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
  private static extern nint GetProcAddress(nint lib, string proc);

  [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
  private static extern nint LoadLibrary(string lib);

  [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
  private static extern bool FreeLibrary(nint lib);
}
