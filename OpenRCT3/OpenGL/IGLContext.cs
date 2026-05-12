using System;
// ReSharper disable InconsistentNaming

namespace OpenRCT3.OpenGL;

public interface IGLContext : IDisposable {
  nint GetProcAddress(string procName);
}
