using System;
using System.Runtime.InteropServices;

namespace OpenRCT3.OpenGL;

public interface IPlatformGLContext : IDisposable {
  nint GetProcAddress(string procName);
}
