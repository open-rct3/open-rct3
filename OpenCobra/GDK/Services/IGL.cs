// Scene
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using Silk.NET.OpenGL;

namespace OpenCobra.GDK.Services;

/// <summary>
/// Current OpenGL context IoC service.
/// </summary>
public interface IGL {
  GL Context { get; }
}

/// <summary>
/// Current OpenGL context.
/// </summary>
public record struct GLContext(GL Context) : IGL;
