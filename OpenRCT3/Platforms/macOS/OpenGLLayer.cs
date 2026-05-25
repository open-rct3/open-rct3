using System.ComponentModel;
using Silk.NET.OpenGL;
using CoreAnimation;
using CoreVideo;
using OpenGL;
using OpenRCT3.OpenGL;
// ReSharper disable InconsistentNaming

namespace OpenRCT3.Platforms.macOS;

public class OpenGLLayer : CAOpenGLLayer, IGraphicsSurface {
  private bool initialized;
  private SurfaceSettings? settings;
  private GL? gl;
  private GLContext? glContext;
  private Renderer? renderer;

  public event SurfaceCreated? SurfaceCreated;
  public event SurfaceChanged? SurfaceChanged;

  [Browsable(false)]
  public IRenderer Renderer => renderer
    ?? throw new InvalidOperationException("Renderer has not been initialized.");

  [Browsable(false)]
  public SurfaceSettings Settings => settings!;

  [Browsable(false)]
  public bool IsValid => initialized;

  [Browsable(false)]
  public Handle<IntPtr> Surface {
    get => field ?? throw new InvalidOperationException("Current surface is invalid!");
    private set;
  }

  /// <summary>
  /// Whether the OpenGL layer is asynchronous.
  /// </summary>
  /// <remarks>
  /// The contents of this layer are updated only in response to receiving a <see cref="CALayer.SetNeedsDisplay"/> message.
  /// </remarks>
  /// <seealso cref="CAOpenGLLayer.Asynchronous"/>
  public new static bool Asynchronous => false;

  public override void DrawInCGLContext(CGLContext context, CGLPixelFormat pixelFormat, double timeInterval, ref CVTimeStamp timeStamp) {
    if (!initialized) InitializeRenderer(context);
    if (renderer != null) RenderScene();

    base.DrawInCGLContext(context, pixelFormat, timeInterval, ref timeStamp);
  }

  private void InitializeRenderer(CGLContext context) {
    settings = new SurfaceSettings();
    glContext = new GLContext();
    Surface = new OpenGLSurface(context.Handle.Handle, false);

    // Load Silk.NET OpenGL with the current context
    gl = GL.GetApi(glContext.GetProcAddress);

    renderer = new Renderer(gl);
    renderer.Initialize(this);
    renderer.ContextRequested += (_, _) => {
      // FIXME: CGLSetCurrentContext(context);
      NSOpenGLContext.CurrentContext.MakeCurrentContext();
    };
    renderer.Rendered += (_, _) => {
      // TODO: Swap buffers? Or no-op and let CoreAnimation handle it?
    };
    SurfaceCreated?.Invoke(this, renderer);
    SetNeedsDisplay();

    initialized = true;
  }

  private void RenderScene() {
    if (renderer == null || Game.Instance == null) return;
    renderer.SetViewport((int)Frame.Width, (int)Frame.Height);
    Game.Instance.Scene.Camera.Update((float)Frame.Width / (float)Frame.Height);
    renderer.Render(Game.Instance.Scene);
  }

  protected override void Dispose(bool disposing) {
    if (disposing) {
      Game.Instance?.Dispose();
      renderer?.Dispose();
      gl?.Dispose();
    }
    base.Dispose(disposing);
  }
}
