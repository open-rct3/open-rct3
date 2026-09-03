// TrackSection Decoding Unit Tests
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class TrackSectionsTests {
  private static byte[] CreateSyntheticTrackSection(
    uint sceneryItemRef = 42,
    uint direction = 0b0000_1001,
    uint entrySlope = 2,
    uint exitSlope = 3,
    uint entryBank = 1,
    uint exitBank = 5,
    uint leftSpline = 1001,
    uint rightSpline = 1002
  ) {
    var data = new byte[140];
    Buffer.BlockCopy(BitConverter.GetBytes(sceneryItemRef), 0, data, 4, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(direction), 0, data, 20, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(leftSpline), 0, data, 32, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(rightSpline), 0, data, 36, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(entrySlope), 0, data, 72, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(entryBank), 0, data, 76, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(exitSlope), 0, data, 100, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(exitBank), 0, data, 104, 4);
    return data;
  }

  [Test]
  public void ParseTrackSection_ValidBinary_DecodesEnumsAndMetadata() {
    var bytes = CreateSyntheticTrackSection(
      sceneryItemRef: 123,
      direction: 9,
      entrySlope: 2,
      exitSlope: 6,
      entryBank: 1,
      exitBank: 7
    );

    var section = TrackData.ParseTrackSection(bytes, "sec_1", "Section 1");

    using (Assert.EnterMultipleScope()) {
      Assert.That(section.Id, Is.EqualTo("sec_1"));
      Assert.That(section.InternalName, Is.EqualTo("Section 1"));
      Assert.That(section.SceneryItemRef, Is.EqualTo(123u));
      Assert.That(section.EntrySlope, Is.EqualTo(TrackSlope.MediumUp));
      Assert.That(section.ExitSlope, Is.EqualTo(TrackSlope.SteepDown));
      Assert.That(section.EntryBank, Is.EqualTo(TrackBank.Left));
      Assert.That(section.ExitBank, Is.EqualTo(TrackBank.BankRight));
      Assert.That(section.EntryDirection, Is.EqualTo(TrackDirection.Left));
      Assert.That(section.ExitDirection, Is.EqualTo(TrackDirection.Right));
    }
  }

  [Test]
  public void ParseTrackSection_DataTooShort_ThrowsInvalidDataException() {
    var truncated = new byte[100];
    Assert.Throws<InvalidDataException>(new Action(() => {
      TrackData.ParseTrackSection(truncated);
    }));
  }

  [Test]
  public void ParseTrackSection_MissingSplines_SetsIsValidFalse() {
    var bytes = CreateSyntheticTrackSection(leftSpline: 1001, rightSpline: 1002);
    var available = new HashSet<string> { "spline_0x3E9" };

    var section = TrackData.ParseTrackSection(bytes, availableSplines: available);
    Assert.That(section.IsValid, Is.False);
  }

  [Test]
  public void ParseTrackSection_AllSplinesPresent_SetsIsValidTrue() {
    var bytes = CreateSyntheticTrackSection(leftSpline: 1001, rightSpline: 1002);
    var available = new HashSet<string> { "spline_0x3E9", "spline_0x3EA" };

    var section = TrackData.ParseTrackSection(bytes, availableSplines: available);
    Assert.That(section.IsValid, Is.True);
  }
}
