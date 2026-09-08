// Input
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Generic;
using Silk.NET.Input;

namespace OpenRCT3.Platforms.Input;

public abstract class InputContext(nint handle) : IInputContext {
  protected List<IGamepad> gamepads = [];
  protected List<IJoystick> joysticks = [];
  protected List<IKeyboard> keyboards = [];
  protected List<IMouse> mice = [];
  protected List<IInputDevice> otherDevices = [];

  public event Action<IInputDevice, bool>? ConnectionChanged;

  public nint Handle { get; } = handle;

  public IReadOnlyList<IGamepad> Gamepads => gamepads;
  public IReadOnlyList<IJoystick> Joysticks => joysticks;
  public IReadOnlyList<IKeyboard> Keyboards => keyboards;
  public IReadOnlyList<IMouse> Mice => mice;
  public IReadOnlyList<IInputDevice> OtherDevices => otherDevices;

  public void Dispose() {
    GC.SuppressFinalize(this);
    keyboards.Clear();
    mice.Clear();
    gamepads.Clear();
    joysticks.Clear();
    otherDevices.Clear();
  }
}
