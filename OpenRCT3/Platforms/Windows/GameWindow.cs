// Game Window
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025-2026 OpenRCT3 Contributors. All rights reserved.

using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenRCT3.Platforms.Windows;

internal partial class GameWindow : Form, IWindow {
  public GameWindow() {
    InitializeComponent();

    // Start the game
    glSurface.SurfaceCreated += (_, renderer) =>
      Task.Run(() => new Game(renderer).Run());
  }

  public string Title { get => base.Text; set => Text = value; }

  public Dpi Dpi {
    get {
      using var g = CreateGraphics();
      return new Dpi(g.DpiX / 96f, g.DpiY / 96f);
    }
  }

  public Size FrameBufferSize => ClientSize;
}
