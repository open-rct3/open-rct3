using System.Drawing;
using CoreAnimation;
using CoreVideo;
using OpenGL;

using Silk.NET.OpenGL;

using OpenRCT3;
using OpenRCT3.OpenGL;

namespace OpenRCT3.Platforms.macOS;

public class GameOpenGLLayer : CAOpenGLLayer {
  private bool _initialized;
  private GL? _gl;

  public override void DrawInCGLContext(CGLContext glContext, CGLPixelFormat pixelFormat, double timeInterval, ref CVTimeStamp timeStamp) {
    if (!_initialized) {
      _initialized = true;
      _gl = new GL(new MacOSGLContext());
      var clearColor = Color.CornflowerBlue.ToGl();
      _gl.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
    }

    _gl!.Clear(ClearBufferMask.ColorBufferBit);

    base.DrawInCGLContext(glContext, pixelFormat, timeInterval, ref timeStamp);
  }
}
