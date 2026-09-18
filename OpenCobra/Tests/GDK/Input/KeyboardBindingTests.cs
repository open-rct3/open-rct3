// KeyboardBindingTests
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
public class KeyboardBindingTests {
  [Test]
  public void IsActive_ReturnsFalse_WhenKeyIsNotPressed() {
    var context = new FakeInputContext();
    var binding = new KeyboardBinding(Key.Q);

    Assert.That(binding.IsActive(context), Is.False);
  }

  [Test]
  public void IsActive_ReturnsTrue_WhenBoundKeyIsPressed() {
    var context = new FakeInputContext();
    var binding = new KeyboardBinding(Key.Q);

    context.Keyboard.Press(Key.Q);

    Assert.That(binding.IsActive(context), Is.True);
  }

  [Test]
  public void IsActive_ReturnsFalse_WhenADifferentKeyIsPressed() {
    var context = new FakeInputContext();
    var binding = new KeyboardBinding(Key.Q);

    context.Keyboard.Press(Key.E);

    Assert.That(binding.IsActive(context), Is.False);
  }

  [Test]
  public void IsActive_ReturnsFalse_WhenRequiredModifierIsNotHeld() {
    var context = new FakeInputContext();
    var binding = new KeyboardBinding(Key.F11, KeyModifiers.Control | KeyModifiers.Shift);

    context.Keyboard.Press(Key.F11);
    context.Keyboard.Press(Key.ShiftLeft);
    // Control is not held.

    Assert.That(binding.IsActive(context), Is.False);
  }

  [Test]
  public void IsActive_ReturnsTrue_WhenKeyAndAllRequiredModifiersAreHeld() {
    var context = new FakeInputContext();
    var binding = new KeyboardBinding(Key.F11, KeyModifiers.Control | KeyModifiers.Shift);

    context.Keyboard.Press(Key.F11);
    context.Keyboard.Press(Key.ControlLeft);
    context.Keyboard.Press(Key.ShiftRight);

    Assert.That(binding.IsActive(context), Is.True);
  }

  [Test]
  public void IsActive_AcceptsEitherLeftOrRightVariantOfAModifier() {
    var context = new FakeInputContext();
    var binding = new KeyboardBinding(Key.C, KeyModifiers.Control);

    context.Keyboard.Press(Key.C);
    context.Keyboard.Press(Key.ControlRight);

    Assert.That(binding.IsActive(context), Is.True);
  }
}
