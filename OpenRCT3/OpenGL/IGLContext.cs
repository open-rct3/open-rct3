// OpenGL Interfaces
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

// ReSharper disable InconsistentNaming
using Silk.NET.Core.Contexts;

namespace OpenRCT3.OpenGL;

public interface IGLContext : INativeContext, IDisposable {
  nint GetProcAddress(string procName);
}
