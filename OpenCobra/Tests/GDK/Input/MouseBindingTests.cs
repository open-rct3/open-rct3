// MouseBindingTests
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
public class MouseBindingTests {
  [Test]
  public void IsActive_ReturnsFalse_WhenButtonIsNotPressed() {
    var context = new FakeInputContext();
    var binding = new MouseBinding(MouseButton.Right);

    Assert.That(binding.IsActive(context), Is.False);
  }

  [Test]
  public void IsActive_ReturnsTrue_WhenBoundButtonIsPressed() {
    var context = new FakeInputContext();
    var binding = new MouseBinding(MouseButton.Right);

    context.Mouse.Press(MouseButton.Right);

    Assert.That(binding.IsActive(context), Is.True);
  }

  [Test]
  public void IsActive_ReturnsFalse_WhenADifferentButtonIsPressed() {
    var context = new FakeInputContext();
    var binding = new MouseBinding(MouseButton.Right);

    context.Mouse.Press(MouseButton.Left);

    Assert.That(binding.IsActive(context), Is.False);
  }
}
