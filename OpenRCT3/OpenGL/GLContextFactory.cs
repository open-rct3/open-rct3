using System;
using System.Runtime.InteropServices;

namespace OpenRCT3.OpenGL;

public static class GLContextFactory {
  public static IPlatformGLContext Create() {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return new WindowsGLContext();
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return new MacOSGLContext();
    throw new PlatformNotSupportedException();
  }
}
