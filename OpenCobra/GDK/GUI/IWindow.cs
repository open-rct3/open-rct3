// GUI Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK.GUI;

public interface IWindow {
  bool Open { get; }

  void Render();
}
