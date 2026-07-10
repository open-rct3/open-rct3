// Safe Cast Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace OpenCobra.GDK.Memory;

/// <summary>
/// Converts <paramref name="From"/> to another integer type by value, not by reinterpreting its
/// bytes.
/// </summary>
public readonly struct CastFrom<From> where From : IBinaryInteger<From> {
  /// <summary>
  /// Converts <paramref name="value"/> to <typeparamref name="To"/> by value, not by reinterpreting its
  /// bytes.
  /// </summary>
  /// <remarks>
  /// <c>Unsafe.As</c> would read/write past the source's storage whenever the two types differ in
  /// size (e.g. widening a 4-byte <see cref="int"/> into an 8-byte <see cref="nint"/>), silently producing
  /// garbage instead of throwing.
  /// </remarks>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static To To<To>(From value) where To : IBinaryInteger<To> => To.CreateChecked(value);
}
