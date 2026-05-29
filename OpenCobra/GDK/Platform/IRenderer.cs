// IRenderer
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.ComponentModel;

namespace OpenCobra.GDK.Platform;

public interface IRenderer : IResource, IDisposable {
  [Category("GPU")]
  int MsaaSamples { get; }

  void Initialize();
  void Render(Scene scene);
  void SetViewport(int width, int height);
}
