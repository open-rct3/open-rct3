// SymbolReference
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Runtime.InteropServices;

namespace OpenCobra.OVL;

/// <summary>
/// A pointer to relocated data in an OVL archive.
/// </summary>
/// <remarks>
/// We assume this is a 32-bit pointer, given the era.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Size = 4)]
public struct RelocationPointer {
  public uint Value;
}
