// ISurfaceSettings
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK.Platform;

public interface ISurfaceSettings {
  GraphicsAPI API { get; }
  ISurfaceSettings Clone();
  string ToString();
}
