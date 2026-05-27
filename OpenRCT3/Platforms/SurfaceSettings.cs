// SurfaceSettings
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Platform;
using Silk.NET.OpenGL;
using System.ComponentModel;

// ReSharper disable InconsistentNaming
namespace OpenRCT3.Platforms;

public class SurfaceSettings : ISurfaceSettings {
  public readonly static Version DefaultVersion = new(4, 1);

  [Category("OpenGL")]
  public GraphicsAPI API { get; set; } = GraphicsAPI.OpenGL;
  [Category("OpenGL")]
  public ContextProfileMask Profile { get; set; } = ContextProfileMask.CoreProfileBit;
  [Category("OpenGL")]
  public ContextFlagMask Flags { get; set; } = ContextFlagMask.ForwardCompatibleBit;
  [Category("OpenGL")]
  public Version Version { get; set; } = DefaultVersion;

  public SurfaceSettings Clone() => MemberwiseClone() as SurfaceSettings ??
    throw new Exception("Could not clone surface settings");
  ISurfaceSettings ISurfaceSettings.Clone() => Clone();

  public override string ToString() => string.Format("{0} v{1} {2}", API, Version, Profile switch {
    ContextProfileMask.CoreProfileBit or ContextProfileMask.ContextCoreProfileBit
      => "Core profile",
    ContextProfileMask.CompatibilityProfileBit or ContextProfileMask.ContextCompatibilityProfileBit
      => "Compatibility profile",
    _ => throw new InvalidOperationException()
  });
}
