// MouseScrollBindingTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework;
using OpenCobra.GDK.Input;
using Silk.NET.Input;

namespace OpenCobra.Tests.GDK.Input;

[TestFixture]
public class MouseScrollBindingTests {
  [Test]
  public void IsActive_AlwaysReturnsFalse_RegardlessOfDeviceState() {
    // A scroll tick is a discrete event, not a "held" state - IsActive must never report active, even
    // while the mouse is otherwise being interacted with; InputActionMap.Scrolled is the only signal
    // for this binding kind (see InputActionMapTests).
    var context = new FakeInputContext();
    var binding = new MouseScrollBinding(ScrollDirection.Up);

    context.Mouse.Press(MouseButton.Left);

    Assert.That(binding.IsActive(context), Is.False);
  }
}
