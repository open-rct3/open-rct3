// DrawNode
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Numerics;

namespace OpenRCT3.OpenGL;

/// <summary>
/// A single, GPU-ready draw command.
/// </summary>
internal record struct DrawNode(
  string Name,
  uint Vao,
  uint Vbo,
  uint? TextureHandle,
  uint ShaderHandle,
  uint IndexCount,
  Matrix4x4 ModelTransform);
