// Memento
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using System.Diagnostics;

namespace Dumper.Models;

public class Memento<T>(T state) {
  public long Hash {
    get {
      Debug.Assert(state != null, nameof(state) + " != null");
      return state.GetHashCode();
    }
  }

  public bool HasChanges => state.GetHashCode() != Hash;
}
