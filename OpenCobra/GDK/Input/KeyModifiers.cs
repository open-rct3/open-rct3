// Keyboard Key Modifiers
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK.Input;

/// <summary>Modifier keys a <see cref="KeyboardBinding"/> can additionally require to be held.</summary>
[Flags]
public enum KeyModifiers {
  None = 0,
  Control = 1 << 0,
  Shift = 1 << 1,
  Alt = 1 << 2,
}
