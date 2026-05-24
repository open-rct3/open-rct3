// IRenderer
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System;
using OpenCobra.GDK;

namespace OpenRCT3.Platforms;

public interface IRenderer : IResource, IDisposable {
  void Initialize(IGraphicsSurface surface);
  void Render(Scene scene);
  void SetViewport(int width, int height);
}
