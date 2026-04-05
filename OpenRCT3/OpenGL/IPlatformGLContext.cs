using System;
using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace OpenRCT3.OpenGL;

public interface IPlatformGLContext : IDisposable {
  nint GetProcAddress(string procName);
}
