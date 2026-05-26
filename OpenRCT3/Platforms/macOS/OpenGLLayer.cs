// OpenGLLayer
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025-2026 OpenRCT3 Contributors. All rights reserved.

using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using CoreAnimation;
using CoreVideo;
using OpenGL;
using OpenRCT3.OpenGL;
// ReSharper disable InconsistentNaming

namespace OpenRCT3.Platforms.macOS;

public class OpenGLLayer : CAOpenGLLayer, IGraphicsSurface {
  private const CGLPixelFormatAttribute CglPfaVersion_4_1 = (CGLPixelFormatAttribute) 0x4100;
  private const CGLPixelFormatAttribute CglPfaOpenGLProfile = (CGLPixelFormatAttribute) 0x63;

  private bool initialized;
  private SurfaceSettings? settings;
  private GL? gl;
  private readonly GLContext glContext = new();
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

  [Category("GPU")]
  // FIXME: This doesn't take the display's pixel density into account
  public Size FrameBufferSize => new((int)Frame.Width, (int)Frame.Height);

  [Category("Behavior")]
  public float AspectRatio => (float)Frame.Width / (float)Frame.Height;

  /// <summary>
  /// Whether the OpenGL layer is asynchronous.
  /// </summary>
  /// <remarks>
  /// The contents of this layer are updated only in response to receiving a <see cref="CALayer.SetNeedsDisplay"/> message.
  /// </remarks>
  /// <seealso cref="CAOpenGLLayer.Asynchronous"/>
  public new static bool Asynchronous => false;

  public override CGLPixelFormat CopyCGLPixelFormatForDisplayMask(uint mask) {
    var attrs = new CGLPixelFormatAttribute[] {
      CglPfaOpenGLProfile, CglPfaVersion_4_1,
      CGLPixelFormatAttribute.ScreenMask, (CGLPixelFormatAttribute)mask,
      CGLPixelFormatAttribute.Accelerated,
      // TODO: Maybe use OpenCL for particle effects?
      // CGLPixelFormatAttribute.AcceleratedCompute,
      CGLPixelFormatAttribute.DoubleBuffer,
      CGLPixelFormatAttribute.Supersample,
      0 // Null terminator
    };

    return new CGLPixelFormat(attrs, out _);
  }

  public override void DrawInCGLContext(CGLContext context, CGLPixelFormat pixelFormat, double timeInterval, ref CVTimeStamp timeStamp) {
    if (!initialized) InitializeRenderer(context);
    if (renderer != null) RenderScene();

    base.DrawInCGLContext(context, pixelFormat, timeInterval, ref timeStamp);
  }

  private void InitializeRenderer(CGLContext context) {
    // Load Silk.NET OpenGL with the current context
    gl = GL.GetApi(glContext.GetProcAddress);

    // Determine the current OpenGL version
    CGLContext.CurrentContext = context;
    Debug.Assert(Version.TryParse(gl.GetStringS(StringName.Version).Split(' ')[0], out var version));
    settings = new SurfaceSettings {
      Profile = ContextProfileMask.CoreProfileBit,
      Version = version
    };

    // Create the scene renderer
    renderer = new Renderer(this, gl);
    renderer.Initialize(this);
    renderer.ContextRequested += (_, _) => {
      CGLContext.CurrentContext = context;
    };
    renderer.Rendered += (_, _) => {
      // Execution gets here after DrawInCGLContext > RenderScene > Renderer.Render (raises this event)
      // Also via Game.Run > Renderer.Render (raises this event)
      CGLFlushDrawable(context.Handle.Handle);
    };
    Surface = new OpenGLSurface(context.Handle.Handle, false);
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

  [DllImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
  private extern static void CGLFlushDrawable(nint ctx);
}
