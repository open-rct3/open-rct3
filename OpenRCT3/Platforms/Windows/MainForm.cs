// MainForm
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025 OpenRCT3 Contributors. All rights reserved.

using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace OpenRCT3.Platforms.Windows;

internal partial class MainForm : Form, IWindow {
  private readonly HashSet<IObserver<Surface>> _observers = new();
  private readonly WebGPU gpu;
  private Surface? _surface = null;

  public MainForm(WebGPU gpu) {
    InitializeComponent();

    this.gpu = gpu;
    RecreateSurface();
  }

  public string Title { get => this.Text; set => this.Text = value; }
  public Dpi Dpi {
    get {
      using Graphics g = this.CreateGraphics();
      return new Dpi(g.DpiX, g.DpiY);
    }
  }
  public Size FrameBufferSize => new(
    Convert.ToInt32(Math.Round(ClientSize.Width * Dpi.X)),
    Convert.ToInt32(Math.Round(ClientSize.Height * Dpi.Y))
  );

  public IDisposable Subscribe(IObserver<Surface> observer) {
    _observers.Add(observer);
    return new Unsubscriber<Surface>(_observers, observer);
  }

  private void RecreateSurface() {
    //_surface = WebGPU.
  }

  private void MainForm_DpiChanged(object sender, DpiChangedEventArgs e) {
    RecreateSurface();
  }

  private void MainForm_Resize(object sender, EventArgs e) {
    RecreateSurface();
  }
}
