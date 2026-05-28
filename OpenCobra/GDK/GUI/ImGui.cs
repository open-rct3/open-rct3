// Immediate GUI Controller
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using ImGuiNET;
using OpenCobra.GDK.Services;
using Silk.NET.Input;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace OpenCobra.GDK.GUI;

/// <summary>
/// ImGui rendering abstraction, initialized once per scene.
/// </summary>
public class Controller : IDisposable {
  private readonly IResolverContext scope = Scene.IoC.OpenScope(typeof(Controller).FullName, trackInParent: true);
  private readonly IInputContext input;
  private readonly ImGuiController controller;

  public static bool CaptureMouse => ImGui.GetIO().WantCaptureMouse;
  public static bool CaptureKeyboard => ImGui.GetIO().WantCaptureKeyboard;

  public Controller() {
    input = scope.Resolve<IInputContext>();
    controller = new(
      scope.Resolve<IContextSource>().Context,
      scope.Resolve<Platform.IWindow>(),
      input
    );
  }

  public void Update(float deltaSeconds) {
    Debug.Assert(!scope.IsDisposed);
    // FIXME: Why does this sometimes throw an AccessViolatonException? Bug in the ImGui wrapper?
    controller.Update(deltaSeconds);
  }

  public void Render() {
    Debug.Assert(!scope.IsDisposed);
    // FIXME: Why does this sometimes throw an AccessViolatonException? Bug in the ImGui wrapper?
    controller.Render();
  }

  public void Dispose() {
    if (scope.IsDisposed) return;
    GC.SuppressFinalize(this);
    controller.Dispose();
    scope.Dispose();
  }
}
