// Input State
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Linq;
using System.Numerics;
using Silk.NET.Input;

namespace OpenRCT3.Platforms.Input;

readonly record struct PressedKey(Key Key, int ScanCode);

readonly record struct MouseState {
  public readonly MouseButton[] PressedButtons;
  public readonly Vector2 Position;
  public readonly ICursor? Cursor;

  public MouseState() : this([], Vector2.Zero) { }
  public MouseState(MouseButton[] pressedButtons, Vector2 position, ICursor? cursor = null) {
    PressedButtons = pressedButtons;
    Position = position;
    Cursor = cursor;
  }

  public MouseState WithButton(MouseButton button, bool pressed = true) => new(
    pressed
      ? [.. PressedButtons.Concat([button]).Distinct()]
      : [.. PressedButtons.Except([button])],
    Position,
    Cursor
  );

  public MouseState WithPosition(Vector2 position) => new(
    PressedButtons,
    position,
    Cursor
  );

  public MouseState WithCursor(ICursor cursor) => new(
    PressedButtons,
    Position,
    cursor
  );
}
