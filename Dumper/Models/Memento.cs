// Memento
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using System.ComponentModel;
using System.Diagnostics;

namespace Dumper.Models;

public class Memento<T> where T : INotifyPropertyChanging {
  private readonly T state;
  private long oldHash;

  public Memento(T state) {
    this.state = state;
    oldHash = Hash;
    // Setup change detection
    state.PropertyChanging += (_, _) => {
      oldHash = Hash;
    };
  }

  public T Value => state;
  public long Hash => state.GetHashCode();
  public bool HasChanges => state.GetHashCode() != oldHash;
}
