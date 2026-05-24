// Safe Cast Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Runtime.CompilerServices;

namespace OpenCobra.GDK.Memory;

public struct CastFrom<From> where From : unmanaged {
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static To To<To>(From value) where To : unmanaged => Unsafe.As<From, To>(ref value);
}
