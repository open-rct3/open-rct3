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

  private Vector2 FramebufferSize => (Vector2)window.FramebufferSize.As<float>();

  public Controller(IInputContext input) {
    // Initialize ImGui
    var context = (this.context = ImGui.CreateContext()).Value;
    ImGui.SetCurrentContext(context);

    var io = ImGui.GetIO();
    io.DisplaySize = FramebufferSize;
    io.DisplayFramebufferScale = new Vector2(1);
    io.WantSaveIniSettings = false;
    io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
    io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
    io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
    // TODO: io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;

    var mouse = input.Mice[0];
    mouse.MouseMove += Mouse_Move;
    mouse.Click += Mouse_Click;

    // Setup GUI theme
    ImGui.StyleColorsDark();
    var style = ImGui.GetStyle();
    // TODO: style.ScaleAllSizes(mainScale);
    // TODO: style.FontScaleDpi = mainScale;
    // TODO: io.ConfigDpiScaleFonts = true;

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

  private void Mouse_Click(IMouse mouse, MouseButton button, Vector2 pos) => Invoke(() => {
    var io = ImGui.GetIO();
    io.AddMouseButtonEvent(button == MouseButton.Right ? 1 : 0, down: true);
    io.AddMouseButtonEvent(button == MouseButton.Right ? 1 : 0, down: false);
  });

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
