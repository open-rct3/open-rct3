// GamePresentationOptionsTests
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenRCT3.Tests;

[TestFixture]
public class GamePresentationOptionsTests {
  [TestCase(null, true)]
  [TestCase("", true)]
  [TestCase("0", true)]
  [TestCase("true", true)]
  [TestCase("1", false)]
  public void ShouldShowUserInterface_OnlyHidesForExplicitOne(string? value, bool expected) {
    Assert.That(GamePresentationOptions.ShouldShowUserInterface(value), Is.EqualTo(expected));
  }
}
