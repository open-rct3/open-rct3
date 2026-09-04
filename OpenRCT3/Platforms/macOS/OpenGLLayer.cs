// OpenGLLayer
//
// Copyright © 2025-2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using CoreAnimation;
using CoreVideo;
using OpenCobra.GDK.Platform;
using OpenCobra.GDK.GUI;
using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using DryIoc;
using OpenGL;
using OpenRCT3.OpenGL;
// ReSharper disable InconsistentNaming

namespace OpenRCT3.Platforms.macOS;

public class OpenGLLayer : CAOpenGLLayer, IGraphicsSurface {
  private const CGLPixelFormatAttribute CglPfaVersion_4_1 = (CGLPixelFormatAttribute)0x4100;
  private const CGLPixelFormatAttribute CglPfaOpenGLProfile = (CGLPixelFormatAttribute)0x63;

  public OpenGLLayer() {
    Surface = new Handle<IntPtr>(IntPtr.Zero, false);
    NeedsDisplayOnBoundsChange = true;
  }

  /// <summary>
  /// Prevent Core Animation from animating geometry changes for this layer.
  /// </summary>
  /// <remarks>
  /// Returning NSNull.Null explicitly halts Core Animation's action search chain,
  /// whereas returning null would fall back to defaultActionForKey (creating a default CABasicAnimation).
  /// </remarks>
  public override NSObject? ActionForKey(string eventKey) =>
    eventKey is "position" or "bounds" or "frame" ? NSNull.Null : base.ActionForKey(eventKey);

  private bool initialized;
  private bool faulted;
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
  public SurfaceSettings Settings => settings
    ?? throw new InvalidOperationException("Surface settings have not been initialized.");
  ISurfaceSettings IGraphicsSurface.Settings => Settings;

  [Browsable(false)]
  public bool IsValid => initialized;

  /// <summary>
  /// The native GL context backing this surface.
  /// </summary>
  [Browsable(false)]
  public IGLContext GLContext => glContext;

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
    if (!faulted) {
      try {
        // CoreAnimation can schedule draw callbacks before AppKit finishes attaching the view
        // to the window hierarchy during scene startup. Defer renderer creation until the host
        // window has been registered so downstream subsystems can resolve IWindow.
        if (!Game.IoC.IsRegistered<OpenCobra.GDK.Platform.IWindow>()) {
          SetNeedsDisplay();
          return;
        }

        glContext.SetCurrentContext(context.Handle.Handle);

        if (!initialized) InitializeRenderer(context);
        if (renderer != null) RenderScene();
      }
      catch (Exception e) {
        // Latch so a failure during init/render doesn't re-throw (and re-alert) on every
        // subsequent frame while the CVDisplayLink keeps calling us at up to 60fps.
        faulted = true;
        Program.HandleException(e);
      }
    }

    base.DrawInCGLContext(context, pixelFormat, timeInterval, ref timeStamp);
  }

  private void InitializeRenderer(CGLContext context) {
    // Load Silk.NET OpenGL with the current context
    gl = GL.GetApi(glContext.GetProcAddress);

    // Determine the current OpenGL version
    CGLContext.CurrentContext = context;
    Diagnostics.Assert(Version.TryParse(gl.GetStringS(StringName.Version).Split(' ')[0], out var version));
    settings = new SurfaceSettings {
      Profile = ContextProfileMask.CoreProfileBit,
      Version = version
    };

    // Register surface and GL context so other services can resolve them
    Game.IoC.RegisterInstance<IGraphicsSurface>(this);
    Game.IoC.RegisterInstance(gl);
    Game.IoC.RegisterInstance<IGLContext>(glContext);

    // Provide a minimal input context and GUI controller used by the renderer
    var input = new MacInputContext(context.Handle.Handle);
    Game.IoC.RegisterInstance<IInputContext>(input);
    Game.IoC.RegisterInstance(new Controller(input));

    // Create and initialize the scene renderer
    renderer = new Renderer { FramebufferSize = new((int)Frame.Width, (int)Frame.Height) };
    renderer.Initialize();
    Game.IoC.RegisterInstance<IRenderer>(renderer);

    // Create a platform surface handle (opaque) for consumers
    Surface = new Handle<IntPtr>(context.Handle.Handle, false);

    initialized = true;
    SurfaceCreated?.Invoke(this, renderer);
    SetNeedsDisplay();
  }

  private void RenderScene() {
    if (renderer == null || Game.Instance == null) return;
    renderer.FramebufferSize = new((int)Frame.Width, (int)Frame.Height);
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
