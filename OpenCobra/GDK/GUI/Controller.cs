// Immediate GUI Controller
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.OpenGL3;
using NLog;
using OpenCobra.GDK.Game;
using OpenCobra.GDK.Threading;
using Silk.NET.Input;
using System.Numerics;

namespace OpenCobra.GDK.GUI;

/// <summary>
/// ImGui rendering abstraction, initialized once per scene.
/// </summary>
public class Controller : ThreadAffine, IDisposable {
  private readonly Logger logger = LogManager.GetCurrentClassLogger();
  private readonly Platform.IWindow window = IGame.IoC.Resolve<Platform.IWindow>();
  private ImGuiContextPtr? context;
  private bool disposed;

  public static bool CaptureMouse => ImGui.GetIO().WantCaptureMouse;
  public static bool CaptureKeyboard => ImGui.GetIO().WantCaptureKeyboard;
  /// <summary>
  /// Whether a text-entry widget (e.g. an <c>InputText</c>) currently wants literal keyboard characters.
  /// Unlike <see cref="CaptureKeyboard"/> - which stays true for any focused ImGui window, including one
  /// with no text field at all, since ImGui may also want keys for nav/shortcuts - this is only true while
  /// something is actually editable, which is the narrower condition game keyboard shortcuts should be
  /// gated on.
  /// </summary>
  public static bool WantTextInput => ImGui.GetIO().WantTextInput;

  private Vector2 FramebufferSize => (Vector2)window.FramebufferSize.As<float>();

  public Controller(IInputContext input) {
    // Initialize ImGui
    var context = (this.context = ImGui.CreateContext()).Value;
    ImGui.SetCurrentContext(context);

    var io = ImGui.GetIO();
    io.DisplaySize = FramebufferSize;
    io.DisplayFramebufferScale = new Vector2(1);
    unsafe { io.IniFilename = null; }
    io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
    io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
    io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
    // TODO: io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;

    var mouse = input.Mice[0];
    mouse.MouseMove += Mouse_Move;
    mouse.MouseDown += Mouse_Down;
    mouse.MouseUp += Mouse_Up;
    mouse.Scroll += Mouse_Scroll;

    // Setup GUI theme
    ImGui.StyleColorsDark();
    var style = ImGui.GetStyle();
    var mainScale = window.Dpi.X;
    style.ScaleAllSizes(mainScale);
    style.FontScaleDpi = mainScale;
    io.ConfigDpiScaleFonts = true;

    // Initialize GUI renderer
    ImGuiImplOpenGL3.SetCurrentContext(context);
    // TODO: Extract this magic number to the Materials namespace
    var glslVersion = "#version 150";
    if (!ImGuiImplOpenGL3.Init(glslVersion)) {
      var message = string.Format("Failed to initialize ImGui OpenGL implementation with GLSL v{0}", glslVersion.Split(' ')[1]);
      logger.Fatal(message);
      throw new PlatformNotSupportedException(message);
    }
  }

  private void Mouse_Move(IMouse mouse, Vector2 pos) => Invoke(() => ImGui.GetIO().AddMousePosEvent(pos.X, pos.Y));

  private void Mouse_Down(IMouse mouse, MouseButton button) => Invoke(() => {
    var index = ToImGuiButton(button);
    if (index is int i) ImGui.GetIO().AddMouseButtonEvent(i, down: true);
  });

  private void Mouse_Up(IMouse mouse, MouseButton button) => Invoke(() => {
    var index = ToImGuiButton(button);
    if (index is int i) ImGui.GetIO().AddMouseButtonEvent(i, down: false);
  });

  private void Mouse_Scroll(IMouse mouse, ScrollWheel wheel) => Invoke(() => ImGui.GetIO().AddMouseWheelEvent(wheel.X, wheel.Y));

  private static int? ToImGuiButton(MouseButton button) => button switch {
    MouseButton.Left => 0,
    MouseButton.Right => 1,
    MouseButton.Middle => 2,
    _ => null,
  };

  public void Update(double deltaSeconds) => Invoke(() => {
    Debug.Assert(!disposed);
    Debug.Assert(context is not null);

    var io = ImGui.GetIO();
    io.DeltaTime = Convert.ToSingle(deltaSeconds);
    var oldSize = io.DisplaySize;
    if (oldSize != FramebufferSize) logger.Trace(FramebufferSize);
    io.DisplaySize = FramebufferSize;
    io.DisplayFramebufferScale = new Vector2(1);
  });

  public void StartFrame() => Invoke(() => {
    Debug.Assert(!disposed);
    Debug.Assert(context is not null);

    ImGuiImplOpenGL3.NewFrame();
    ImGui.NewFrame();
  });

  public void Render() => Invoke(() => {
    Debug.Assert(!disposed);
    Debug.Assert(context is not null);

    ImGui.Render();
    ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());
  });

  public void Dispose() => Invoke(() => {
    if (disposed) return;
    Debug.Assert(context is not null);
    GC.SuppressFinalize(this);

    ImGuiImplOpenGL3.Shutdown();
    ImGuiImplOpenGL3.SetCurrentContext(null);
    ImGui.DestroyContext(context.Value);
    context = null;
    disposed = true;
  });
}
