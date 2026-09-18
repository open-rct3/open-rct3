// Texture
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using OpenCobra.GDK.Game;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace OpenCobra.GDK.Materials;

/// <summary>
/// One frame's mip chain. A static texture has a single <see cref="MipChain"/> with every mip
/// level. An animated (flexi) texture has one single-resolution <see cref="MipChain"/> per frame.
/// </summary>
public record struct MipChain(IReadOnlyList<Image<Rgba32>> Mips);

/// <summary>
/// Animation metadata for a multi-frame <see cref="Texture"/>. <c>FrameCount == 1</c> and
/// <c>Fps == 0</c> for static textures; <c>FrameCount == Frames.Count</c> for animated ones.
/// </summary>
public record struct Animation(uint Fps, int FrameWidth, int FrameHeight, int FrameCount);

public class Texture : IResource, IDisposable {
  public static readonly string UniformName = "u_Texture";
  private readonly object lifetimeLock = new();
  private readonly Rgba32[] uploadPixels;
  private bool disposed;
  private bool ownerReleased;
  private int leaseCount;
  private uint handle;

  [Category("Design")]
  public string Name { get; private set; }
  [Category("Appearance")]
  public int Width { get; }
  [Category("Appearance")]
  public int Height { get; }
  [Category("Appearance")]
  public Recolorable Recolorable { get; }
  [Category("Appearance")]
  public TextureFormat Format { get; init; } = TextureFormat.A8R8G8B8;

  /// <summary>
  /// Every frame's mip chain. Static textures hold one frame with every mip; animated (flexi)
  /// textures hold one single-resolution frame per animation frame.
  /// </summary>
  [Browsable(false)]
  public IReadOnlyList<MipChain> Frames { get; init; }

  /// <summary>
  /// Animation metadata, or <c>null</c> for a static texture. The renderer reads this together
  /// with <see cref="Frames"/> - it never iterates separate <see cref="Texture"/> instances.
  /// </summary>
  [Category("Appearance")]
  public Animation? Animation { get; init; }

  /// <summary>
  /// Convenience alias for <c>Frames[0].Mips[0]</c>, the existing public surface.
  /// </summary>
  [Category("Appearance")]
  public Image<Rgba32> Pixels => Frames[0].Mips[0];

  [Browsable(false)]
  public TextureCacheKey CacheKey { get; }

  public Texture(
    string name,
    int width,
    int height,
    [TakesOwnership] Image<Rgba32> texture,
    Recolorable recolorable = 0
  ) {
    Name = name;
    Width = width;
    Height = height;
    Recolorable = recolorable;
    Frames = [new MipChain([texture])];
    uploadPixels = new Rgba32[texture.Width * texture.Height];
    texture.CopyPixelDataTo(uploadPixels);
    CacheKey = TextureCacheKey.Create(name, width, height, recolorable, uploadPixels);
  }

  /// <summary>
  /// Whether this texture is recolorable.
  /// </summary>
  [Category("Appearance")]
  public bool IsRecolorable => Recolorable != Recolorable.None;

  [Category("GPU")]
  public State State {
    get {
      lock (lifetimeLock)
        return disposed ? State.Disposed : (handle == 0 ? State.Uninitialized : State.Ready);
    }
  }

  [Browsable(false)]
  public uint Handle {
    get {
      lock (lifetimeLock) return handle;
    }
  }

  public void Upload() {
    ObjectDisposedException.ThrowIf(State == State.Disposed, this);
    if (State == State.Ready) return;
    Upload(new SilkTextureGpuApi(IGame.IoC.Resolve<GL>()));
  }

  /// <summary>
  /// Uploads the immutable pixel snapshot through a renderer-provided GPU API.
  /// </summary>
  public void Upload(IGpuApi gpu) => EnsureUploaded(pixels => {
    var uploadedHandle = gpu.CreateTexture();
    if (uploadedHandle == 0)
      throw new InvalidOperationException("A texture upload must allocate a non-zero GPU handle.");
    try {
      // FIXME: SAFELY upload texture pixels to GPU!
      gpu.UploadTexture(uploadedHandle, Width, Height, pixels);
      return uploadedHandle;
    } catch (Exception uploadError) {
      try {
        gpu.DeleteTexture(uploadedHandle);
      } catch (Exception cleanupError) {
        throw new AggregateException(uploadError, cleanupError);
      }
      throw;
    }
  });

  /// <summary>
  /// Attaches the cached GPU handle returned by <paramref name="upload"/> exactly once.
  /// Render backends can use this seam to share one upload across equivalent texture instances.
  /// </summary>
  public void EnsureUploaded(Func<uint> upload) {
    EnsureUploaded(_ => upload());
  }

  /// <summary>
  /// Attaches a handle created from the immutable pixel snapshot exactly once.
  /// </summary>
  public void EnsureUploaded(TextureUpload upload) {
    lock (lifetimeLock) {
      ObjectDisposedException.ThrowIf(disposed, this);
      if (handle != 0) return;

      var uploadedHandle = upload(uploadPixels);
      if (uploadedHandle == 0)
        throw new InvalidOperationException("A texture upload must return a non-zero GPU handle.");
      handle = uploadedHandle;
    }
  }

  /// <summary>
  /// Detaches this texture from a renderer-owned GPU handle without disposing its pixels.
  /// </summary>
  public void ResetUpload() {
    lock (lifetimeLock) handle = 0;
  }

  public void Dispose() {
    lock (lifetimeLock) {
      if (ownerReleased) return;
      ownerReleased = true;
      if (leaseCount > 0) return;
      CompleteDispose();
    }
  }

  /// <summary>
  /// Acquires a non-owning lease that keeps this texture alive until the lease is disposed.
  /// The creator remains the owner and releases ownership through <see cref="Dispose"/>.
  /// </summary>
  public IDisposable AcquireLease() {
    lock (lifetimeLock) {
      ObjectDisposedException.ThrowIf(ownerReleased || disposed, this);
      leaseCount++;
      return new TextureLease(this);
    }
  }

  private void ReleaseLease() {
    lock (lifetimeLock) {
      if (leaseCount <= 0)
        throw new InvalidOperationException("Texture ownership was released more than once.");
      leaseCount--;
      if (leaseCount == 0 && ownerReleased) CompleteDispose();
    }
  }

  private void CompleteDispose() {
    if (disposed) return;
    GC.SuppressFinalize(this);
    foreach (var frame in Frames)
      foreach (var mip in frame.Mips)
        mip.Dispose();
    disposed = true;
  }

  public delegate uint TextureUpload(ReadOnlySpan<Rgba32> pixels);

  public interface IGpuApi {
    uint CreateTexture();
    void UploadTexture(uint handle, int width, int height, ReadOnlySpan<Rgba32> pixels);
    void DeleteTexture(uint handle);
  }

  private sealed class TextureLease(Texture texture) : IDisposable {
    private Texture? resource = texture;

    public void Dispose() {
      var owned = Interlocked.Exchange(ref resource, null);
      owned?.ReleaseLease();
    }
  }

  private sealed class SilkTextureGpuApi(GL gl) : IGpuApi {
    public uint CreateTexture() => gl.GenTexture();

    public void UploadTexture(
      uint handle,
      int width,
      int height,
      ReadOnlySpan<Rgba32> pixels
    ) {
      try {
        gl.BindTexture(TextureTarget.Texture2D, handle);
        gl.TexImage2D(
          TextureTarget.Texture2D,
          0,
          InternalFormat.Rgba,
          Convert.ToUInt32(width),
          Convert.ToUInt32(height),
          0,
          PixelFormat.Rgba,
          PixelType.UnsignedByte,
          pixels
        );

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
      } finally {
        gl.BindTexture(TextureTarget.Texture2D, 0);
      }
    }

    public void DeleteTexture(uint handle) => gl.DeleteTexture(handle);
  }
}

public readonly record struct TextureCacheKey(
  string Name,
  int Width,
  int Height,
  Recolorable Recolorable,
  string PixelHash
) {
  internal static TextureCacheKey Create(
    string name,
    int width,
    int height,
    Recolorable recolorable,
    ReadOnlySpan<Rgba32> pixels
  ) {
    var bytes = MemoryMarshal.AsBytes(pixels);
    var hash = Convert.ToHexString(SHA256.HashData(bytes));
    return new(name, width, height, recolorable, hash);
  }
}
