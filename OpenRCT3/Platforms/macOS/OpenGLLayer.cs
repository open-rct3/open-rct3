using Silk.NET.OpenGL;
using System.Drawing;
using CoreAnimation;
using CoreVideo;
using OpenGL;
using OpenRCT3.OpenGL;
// ReSharper disable InconsistentNaming

namespace OpenRCT3.Platforms.macOS;

public class OpenGLLayer : CAOpenGLLayer {
  private bool _initialized;
  private GL? _gl;
  private GLContext? _glContext;

  public override void DrawInCGLContext(CGLContext glContext, CGLPixelFormat pixelFormat, double timeInterval, ref CVTimeStamp timeStamp) {
    if (!_initialized) {
      _initialized = true;
      _glContext = new GLContext();
      _gl = GL.GetApi(_glContext.GetProcAddress);
      _gl.ClearColor(Color.CornflowerBlue);
    }

    _gl!.Clear(ClearBufferMask.ColorBufferBit);

    base.DrawInCGLContext(glContext, pixelFormat, timeInterval, ref timeStamp);
  }
}
