// MainForm
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025-2026 OpenRCT3 Contributors. All rights reserved.

using Silk.NET.OpenGL;
using System;
using System.Windows.Forms;

namespace OpenRCT3.Platforms.Windows;

internal partial class MainForm : Form {
  public MainForm() {
    InitializeComponent();
    glSurface.RenderFrame += RenderFrame;
    glSurface.Resize += ResizeContext;
  }

  private void RenderFrame(object? sender, EventArgs e) {
    glSurface.MakeCurrent();
    glSurface.GL.Clear(ClearBufferMask.ColorBufferBit);
    glSurface.SwapBuffers();
  }

  private void ResizeContext(object? sender, EventArgs e) {
    if (!glSurface.HasValidContext) return;
    glSurface.MakeCurrent();
    glSurface.GL.Viewport(
      0, 0, Convert.ToUInt32(glSurface.ClientSize.Width), Convert.ToUInt32(glSurface.ClientSize.Height
    ));
    glSurface.Invalidate();
  }
}
