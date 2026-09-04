// Stores a fixed-capacity circular sequence of recent samples.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections;

namespace OpenCobra.GDK.Numerics;

/// <summary>
/// A fixed-capacity circular buffer storing the most recent <typeparamref name="T"/> samples.
/// Index 0 always refers to the oldest recorded sample, and <c>Count - 1</c> refers to the newest.
/// </summary>
public class RingBuffer<T>(int capacity) : IReadOnlyList<T> {
  private readonly T[] buffer = capacity > 0
    ? new T[capacity]
    : throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
  private int head;
  private int tail;

  /// <summary>Gets the maximum number of items the buffer can hold.</summary>
  public int Capacity => buffer.Length;

  /// <summary>Gets the current number of items stored in the buffer.</summary>
  public int Count { get; private set; }

  /// <summary>Gets whether the buffer has reached maximum capacity.</summary>
  public bool IsFull => Count == Capacity;

  public void Push(T item) {
    buffer[tail] = item;
    tail = (tail + 1) % Capacity;
    if (Count < Capacity) {
      Count++;
    } else {
      head = (head + 1) % Capacity;
    }
  }

  public void Clear() {
    head = 0;
    tail = 0;
    Count = 0;
    Array.Clear(buffer, 0, buffer.Length);
  }

  public T this[int index] {
    get {
      if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
      return buffer[(head + index) % Capacity];
    }
  }

  public void CopyTo(Span<T> destination) {
    if (destination.Length < Count) throw new ArgumentException("Destination span is too small.", nameof(destination));
    for (var i = 0; i < Count; i++) {
      destination[i] = this[i];
    }
  }

  public IEnumerator<T> GetEnumerator() {
    for (var i = 0; i < Count; i++) {
      yield return this[i];
    }
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
