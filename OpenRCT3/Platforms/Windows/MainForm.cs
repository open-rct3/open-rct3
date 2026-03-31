// MainForm
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025-2026 OpenRCT3 Contributors. All rights reserved.

using OpenTK.Graphics.OpenGL;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenRCT3.Platforms.Windows;

internal partial class MainForm : Form {
  public MainForm() {
    InitializeComponent();
    glSurface.RenderFrame += RenderFrame;
    glSurface.Resize += ResizeContext;
  }

  protected override void OnLoad(EventArgs e) {
    base.OnLoad(e);

    glSurface.MakeCurrent();
    var clearColor = Color.CornflowerBlue.ToGl();
    GL.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
  }

  private void RenderFrame(object? sender, EventArgs e) {
    glSurface.MakeCurrent();
    GL.Clear(ClearBufferMask.ColorBufferBit);
    glSurface.SwapBuffers();
  }

  private void ResizeContext(object? sender, EventArgs e) {
    if (!glSurface.HasValidContext) return;
    glSurface.MakeCurrent();
    GL.Viewport(0, 0, glSurface.ClientSize.Width, glSurface.ClientSize.Height);
    glSurface.Invalidate();
  }
}
