// Immediate GUI Controller
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace OpenCobra.GDK;

/// <summary>
/// ImGui rendering abstraction, initialized once per scene.
/// </summary>
public class GuiController : IDisposable {
  private readonly ImGuiController? controller;
  private bool disposed;

  public void Render() {
    ImGui.Render();
    controller?.Render();
  }

  public void Dispose() {
    if (disposed) return;
    GC.SuppressFinalize(this);
    controller?.Dispose();
    disposed = true;
  }
}
