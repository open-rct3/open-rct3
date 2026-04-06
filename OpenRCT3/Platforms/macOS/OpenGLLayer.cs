using Silk.NET.OpenGL;
using System.Drawing;
using CoreAnimation;
using CoreVideo;
using OpenGL;
using OpenRCT3.OpenGL;
using OpenRCT3.Platforms;
// ReSharper disable InconsistentNaming

namespace OpenRCT3.Platforms.macOS;

public class OpenGLLayer : CAOpenGLLayer, IGraphicsSurface {
  private bool _initialized;
  private SurfaceSettings _settings;
  private GL? _gl;
  private GLContext? _glContext;
  private Renderer? _renderer;

  [Browsable(false)]
  public IRenderer Renderer => _renderer
    ?? throw new InvalidOperationException("Renderer has not been initialized.");

  [Browsable(false)]
  public SurfaceSettings Settings => _settings;

  public override void DrawInCGLContext(CGLContext glContext, CGLPixelFormat pixelFormat, double timeInterval, ref CVTimeStamp timeStamp) {
    if (!_initialized) {
      _settings = new SurfaceSettings();
      _glContext = new GLContext();
      _gl = GL.GetApi(_glContext.GetProcAddress);
      _renderer = new Renderer(_gl);
      _renderer.Initialize(this);
      _initialized = true;
    }

    if (_renderer != null) {
      _renderer.SetViewport((int)Frame.Width, (int)Frame.Height);
      if (Game.Instance != null) {
        Game.Instance.Scene.UpdateCamera((float)Frame.Width / (float)Frame.Height);
        _renderer.Render(Game.Instance.Scene);
      }
    }

    base.DrawInCGLContext(glContext, pixelFormat, timeInterval, ref timeStamp);
  }

  protected override void Dispose(bool disposing) {
    if (disposing) {
      _renderer?.Dispose();
      _gl?.Dispose();
    }
    base.Dispose(disposing);
  }
}
