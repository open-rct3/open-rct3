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
using Silk.NET.Input;
using System.Numerics;

namespace OpenCobra.GDK.GUI;

/// <summary>
/// ImGui rendering abstraction, initialized once per scene.
/// </summary>
public class Controller : IDisposable {
  private readonly Logger logger = LogManager.GetCurrentClassLogger();
  private readonly IResolverContext scope = Scene.IoC.OpenScope(typeof(Controller).FullName, trackInParent: true);
  private readonly Platform.IWindow window = Scene.IoC.Resolve<Platform.IWindow>();
  private ImGuiContextPtr? context;

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
    // TODO: io.ConfigDpiScaleViewports = true;

    if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable)) {
      style.WindowRounding = 0.0f;
      style.Colors[(int)ImGuiCol.WindowBg].W = 1.0f;
    }

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

  private void Mouse_Move(IMouse mouse, Vector2 pos) => ImGui.GetIO().AddMousePosEvent(pos.X, pos.Y);

  private void Mouse_Click(IMouse mouse, MouseButton button, Vector2 pos) {
    var io = ImGui.GetIO();
    io.AddMouseButtonEvent(button == MouseButton.Right ? 1 : 0, down: true);
    io.AddMouseButtonEvent(button == MouseButton.Right ? 1 : 0, down: false);
  }

  public void Update(double deltaSeconds) {
    Debug.Assert(!scope.IsDisposed);
    Debug.Assert(context is not null);

    var io = ImGui.GetIO();
    io.DeltaTime = Convert.ToSingle(deltaSeconds);
    io.DisplaySize = FramebufferSize;
    io.DisplayFramebufferScale = new Vector2(1);
  }

  public void StartFrame() {
    Debug.Assert(!scope.IsDisposed);
    Debug.Assert(context is not null);

    ImGuiImplOpenGL3.NewFrame();
    ImGui.NewFrame();
  }

  public void Render() {
    Debug.Assert(!scope.IsDisposed);
    Debug.Assert(context is not null);

    var open = true;
    ImGui.Begin("Test", ref open, ImGuiWindowFlags.AlwaysAutoResize);
    ImGui.Text("Resize test");
    ImGui.End();

    ImGui.Render();
    ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

    var io = ImGui.GetIO();
    if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0) {
      ImGui.UpdatePlatformWindows();
      ImGui.RenderPlatformWindowsDefault();
    }
  }

  public void Dispose() {
    if (scope.IsDisposed) return;
    Debug.Assert(context is not null);
    GC.SuppressFinalize(this);

    ImGuiImplOpenGL3.Shutdown();
    ImGuiImplOpenGL3.SetCurrentContext(null);
    ImGui.DestroyContext(context.Value);
    context = null;

    scope.Dispose();
  }
}
