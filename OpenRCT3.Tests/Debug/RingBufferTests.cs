// Unit tests for generic ring buffer telemetry storage.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NUnit.Framework;
using OpenRCT3.Debug;

namespace OpenRCT3.Tests.Debug;

[TestFixture]
public class RingBufferTests {
  [Test]
  public void Push_UnderCapacity_AppendsItemsInChronologicalOrder() {
    var buffer = new RingBuffer<int>(3);
    buffer.Push(10);
    buffer.Push(20);

    Assert.That(buffer.Count, Is.EqualTo(2));
    Assert.That(buffer[0], Is.EqualTo(10));
    Assert.That(buffer[1], Is.EqualTo(20));
    Assert.That(buffer.IsFull, Is.False);
  }

  [Test]
  public void Push_OverCapacity_EvictsOldestItems() {
    var buffer = new RingBuffer<int>(3);
    buffer.Push(1);
    buffer.Push(2);
    buffer.Push(3);
    buffer.Push(4);

    Assert.That(buffer.Count, Is.EqualTo(3));
    Assert.That(buffer.IsFull, Is.True);
    Assert.That(buffer[0], Is.EqualTo(2));
    Assert.That(buffer[1], Is.EqualTo(3));
    Assert.That(buffer[2], Is.EqualTo(4));
  }

  [Test]
  public void CopyTo_ChronologicalSpan_PopulatesChronologically() {
    var buffer = new RingBuffer<float>(3);
    buffer.Push(1.5f);
    buffer.Push(2.5f);
    buffer.Push(3.5f);
    buffer.Push(4.5f);

    Span<float> dest = stackalloc float[3];
    buffer.CopyTo(dest);

    Assert.That(dest[0], Is.EqualTo(2.5f));
    Assert.That(dest[1], Is.EqualTo(3.5f));
    Assert.That(dest[2], Is.EqualTo(4.5f));
  }

  [Test]
  public void Clear_ResetsCountAndState() {
    var buffer = new RingBuffer<int>(3);
    buffer.Push(1);
    buffer.Push(2);
    buffer.Clear();

    Assert.That(buffer.Count, Is.EqualTo(0));
    Assert.That(buffer.IsFull, Is.False);
  }

  [Test]
  public void Indexer_OutOfRange_ThrowsArgumentOutOfRangeException() {
    var buffer = new RingBuffer<int>(3);
    buffer.Push(10);
    Assert.Throws<ArgumentOutOfRangeException>(new Action(() => {
      _ = buffer[1];
    }));
    Assert.Throws<ArgumentOutOfRangeException>(new Action(() => {
      _ = buffer[-1];
    }));
  }
}
