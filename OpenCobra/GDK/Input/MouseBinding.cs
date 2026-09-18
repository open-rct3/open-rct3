// Mouse Binding
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using Silk.NET.Input;

namespace OpenCobra.GDK.Input;

/// <summary>Binds an <see cref="InputActionMap"/> action to a mouse button.</summary>
public sealed record MouseBinding(MouseButton Button) : IInputBinding {
  public bool IsActive(IInputContext context) {
    foreach (var mouse in context.Mice) {
      if (mouse.IsButtonPressed(Button)) return true;
    }
    return false;
  }
}
