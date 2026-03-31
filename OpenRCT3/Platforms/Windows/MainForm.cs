// MainForm
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025-2026 OpenRCT3 Contributors. All rights reserved.

using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OpenRCT3.Platforms.Windows;

internal class MainForm : GameWindow, IWindow {
  private readonly HashSet<IObserver<OpenGLSurface>> _observers = new();

  public MainForm(NativeWindowSettings settings)
    : base(GameWindowSettings.Default, settings) { }

  public new string Title { get => base.Title; set => base.Title = value; }
  public Dpi Dpi {
    get {
      TryGetCurrentMonitorScale(out var x, out var y);
      return new Dpi(x, y);
    }
  }
  public Size FrameBufferSize => new(FramebufferSize.X, FramebufferSize.Y);

  public IDisposable Subscribe(IObserver<OpenGLSurface> observer) {
    _observers.Add(observer);
    return new Unsubscriber<OpenGLSurface>(_observers, observer);
  }

  protected override void OnLoad() {
    base.OnLoad();

    var clearColor = Color.CornflowerBlue.ToGl();
    GL.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
  }

  protected override void OnRenderFrame(FrameEventArgs args) {
    base.OnRenderFrame(args);

    GL.Clear(ClearBufferMask.ColorBufferBit);
    SwapBuffers();
  }

  protected override void OnFramebufferResize(FramebufferResizeEventArgs e) {
    base.OnFramebufferResize(e);

    GL.Viewport(0, 0, e.Width, e.Height);
  }
}
