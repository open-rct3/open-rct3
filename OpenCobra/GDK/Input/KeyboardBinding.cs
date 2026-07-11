// Keyboard Binding
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using Silk.NET.Input;

namespace OpenCobra.GDK.Input;

/// <summary>Binds an <see cref="InputActionMap"/> action to a keyboard key, plus optional modifiers.</summary>
public sealed record KeyboardBinding(Key Key, KeyModifiers Modifiers = KeyModifiers.None) : IInputBinding {
  public bool IsActive(IInputContext context) {
    foreach (var keyboard in context.Keyboards) {
      if (!keyboard.IsKeyPressed(Key)) continue;
      if (!HasModifiers(keyboard)) continue;
      return true;
    }
    return false;
  }

  private bool HasModifiers(IKeyboard keyboard) {
    if (Modifiers.HasFlag(KeyModifiers.Control) && !(keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight))) return false;
    if (Modifiers.HasFlag(KeyModifiers.Shift) && !(keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight))) return false;
    if (Modifiers.HasFlag(KeyModifiers.Alt) && !(keyboard.IsKeyPressed(Key.AltLeft) || keyboard.IsKeyPressed(Key.AltRight))) return false;
    return true;
  }
}
