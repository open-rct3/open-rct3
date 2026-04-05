using System;
using System.Runtime.InteropServices;

namespace OpenRCT3.OpenGL;

public static class GLContextFactory {
  public static IPlatformGLContext Create() {
    return new GLContext();
  }
}
