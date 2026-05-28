// Immediate GUI Controller
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using ImGuiNET;
using OpenCobra.GDK.Game;
using OpenCobra.GDK.Services;
using Silk.NET.Input;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace OpenCobra.GDK;

/// <summary>
/// ImGui rendering abstraction, initialized once per scene.
/// </summary>
public class GuiController : IDisposable {
  private readonly IResolverContext scope = Scene.IoC.OpenScope(nameof(GuiController), trackInParent: true);
  private readonly IInputContext input;
  private readonly ImGuiController controller;

  public GuiController() {
    input = scope.Resolve<IInputContext>();
    controller = new(
      scope.Resolve<IContextSource>().Context,
      scope.Resolve<IWindow>(),
      input
    );
  }

  public void Update(float deltaSeconds) {
    Debug.Assert(!scope.IsDisposed);
    controller.Update(deltaSeconds);
  }

  public void Render() {
    Debug.Assert(!scope.IsDisposed);
    ImGui.Render();
    controller.Render();
  }

  public void Dispose() {
    if (scope.IsDisposed) return;
    GC.SuppressFinalize(this);
    controller.Dispose();
    scope.Dispose();
  }
}
