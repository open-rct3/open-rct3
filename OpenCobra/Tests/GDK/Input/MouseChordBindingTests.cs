// MouseChordBindingTests
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
public class MouseChordBindingTests {
  [Test]
  public void IsActive_ReturnsFalse_WhenNeitherButtonIsPressed() {
    var context = new FakeInputContext();
    var binding = new MouseChordBinding(MouseButton.Right, MouseButton.Left);

    Assert.That(binding.IsActive(context), Is.False);
  }

  [Test]
  public void IsActive_ReturnsFalse_WhenOnlyThePrimaryButtonIsPressed() {
    var context = new FakeInputContext();
    var binding = new MouseChordBinding(MouseButton.Right, MouseButton.Left);

    context.Mouse.Press(MouseButton.Right);

    Assert.That(binding.IsActive(context), Is.False);
  }

  [Test]
  public void IsActive_ReturnsFalse_WhenOnlyTheSecondaryButtonIsPressed() {
    var context = new FakeInputContext();
    var binding = new MouseChordBinding(MouseButton.Right, MouseButton.Left);

    context.Mouse.Press(MouseButton.Left);

    Assert.That(binding.IsActive(context), Is.False);
  }

  [Test]
  public void IsActive_ReturnsTrue_WhenBothButtonsArePressed() {
    var context = new FakeInputContext();
    var binding = new MouseChordBinding(MouseButton.Right, MouseButton.Left);

    context.Mouse.Press(MouseButton.Right);
    context.Mouse.Press(MouseButton.Left);

    Assert.That(binding.IsActive(context), Is.True);
  }

  [Test]
  public void IsActive_ReturnsFalse_AfterOneButtonOfTheChordIsReleased() {
    var context = new FakeInputContext();
    var binding = new MouseChordBinding(MouseButton.Right, MouseButton.Left);

    context.Mouse.Press(MouseButton.Right);
    context.Mouse.Press(MouseButton.Left);
    context.Mouse.Release(MouseButton.Left);

    Assert.That(binding.IsActive(context), Is.False);
  }
}
