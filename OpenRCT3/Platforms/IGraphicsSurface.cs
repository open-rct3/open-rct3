using Silk.NET.OpenGL;
using System;

namespace OpenRCT3.Platforms {
  internal interface IGraphicsSurface {
    SurfaceSettings Settings { get; }
  }

  public enum GraphicsAPI {
    Unsupported,
    OpenGL,
    /// <remarks>
    /// The macOS Metal backend is a pipe dream.
    /// </remarks>
    Metal,
    /// <remarks>
    /// The WebGPU backend is used in the web client.
    /// </remarks>
    WebGPU
  }

  public class SurfaceSettings {
    public static Version DefaultVersion = new(4, 0);

    public GraphicsAPI API { get; set; } = GraphicsAPI.OpenGL;
    public ContextProfileMask Profile { get; set; } = ContextProfileMask.CompatibilityProfileBit;
    public ContextFlagMask Flags { get; set; } = ContextFlagMask.ForwardCompatibleBit;
    public Version Version { get; set; } = DefaultVersion;

    public SurfaceSettings Clone() => (SurfaceSettings)MemberwiseClone();
  }
}
