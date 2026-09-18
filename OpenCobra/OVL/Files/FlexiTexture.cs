// FlexiTexture
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace OpenCobra.OVL.Files;

public record struct FlexiTexture(string Name, Recolorable Recolorable, Image<Rgba32> Texture) {
  public FlexiTexture(Recolorable recolorable, Image<Rgba32> texture)
    : this(string.Empty, recolorable, texture) { }

  public uint Width => Convert.ToUInt32(Texture.Width);
  public uint Height => Convert.ToUInt32(Texture.Height);

  public static implicit operator Texture(FlexiTexture frame) {
    var texture = new Texture(
      frame.Name, TextureFormat.A8R8G8B8, frame.Width, frame.Height, recolorable: frame.Recolorable);
    texture.MipLevels[0] = frame.Texture;
    return texture;
  }
}

internal readonly record struct FlexiFrameData(
  Recolorable Recolorable,
  ReadOnlyMemory<byte> Palette,
  ReadOnlyMemory<byte> Texture,
  ReadOnlyMemory<byte> Alpha
);

public record struct FlexiTextureList(uint Fps, FlexiTexture[] Frames) {
  private const int HeaderSize = 36;
  private const int FrameStructSize = 28; // scale, width, height, Recolorable, palette*, texture*, alpha*
  private const int PaletteSize = 256 * 4; // 256 × 4 (BGRA)

  private readonly record struct FrameData(
    uint Width,
    uint Height,
    Recolorable Recolorable,
    ReadOnlyMemory<byte> Palette,
    ReadOnlyMemory<byte> Texture,
    ReadOnlyMemory<byte>? Alpha
  );

  public readonly int Width => Frames[0].Texture.Width;
  public readonly int Height => Frames[0].Texture.Height;
  public readonly Recolorable Recolorable => Frames[0].Recolorable;
  public readonly int Length => Frames.Length;
  public readonly int Count => Frames.Length;
  public readonly FlexiTexture this[int index] => Frames[index];

  // See FlexiTextureInfoStruct/FlexiTextureStruct in flexitexture.h and ManagerFTX.cpp.
  public static FlexiTextureList Load(Ovl ovl, OvlFile file) {
    var bytes = ovl.ReadResource(file) ??
      throw new InvalidOperationException($"Resource '{file.Name}' not found in OVL.");
    if (!ovl.TryGetDataPointer(file, out var resourceAddress))
      throw new InvalidDataException($"Could not resolve FlexiTexture header for '{file.Name}'.");

    ReadOnlyMemory<byte>? ResolveData(uint address, int length) {
      if (!ovl.TryResolveRelocation(address, out var block, out var offset)) return null;
      if (offset > Convert.ToUInt32(block.Length)) return null;

      var start = Convert.ToInt32(offset);
      if (length > block.Length - start) return null;
      return new ReadOnlyMemory<byte>(block, start, length);
    }

    bool IsRelocatedPointer(uint sourceAddress, uint targetAddress) =>
      ovl.TryGetRelocationSource(sourceAddress, out var rawValue) && rawValue == targetAddress;

    return Decode(file.Name, bytes, resourceAddress, ResolveData, IsRelocatedPointer);
  }

  internal static FlexiTextureList Decode(
    string name,
    ReadOnlyMemory<byte> bytes,
    uint resourceAddress,
    Func<uint, int, ReadOnlyMemory<byte>?> resolveData,
    Func<uint, uint, bool> isRelocatedPointer
  ) {
    ArgumentNullException.ThrowIfNull(resolveData);
    ArgumentNullException.ThrowIfNull(isRelocatedPointer);
    if (bytes.Length < HeaderSize)
      throw new InvalidDataException($"FlexiTexture '{name}' header is truncated.");

    var header = bytes.Span;
    var scale = ReadUInt32(header, 0);
    var width = ReadUInt32(header, 4);
    var height = ReadUInt32(header, 8);
    var fps = ReadUInt32(header, 12);
    var recolorable = ReadRecolorable(name, "header", ReadUInt32(header, 16));
    var offsetCount = ReadUInt32(header, 20);
    // `offset1` and `fts2` are relocated pointers, not inline data: the animation frame order and
    // the per-frame FlexiTextureStruct array both live elsewhere in the archive's block data.
    var offset1Ptr = ReadUInt32(header, 24);
    var frameCount = ReadUInt32(header, 28);
    var fts2Ptr = ReadUInt32(header, 32);

    _ = recolorable;
    _ = ValidateDimensions(name, "header", scale, width, height);
    if (frameCount == 0)
      throw new InvalidDataException($"FlexiTexture '{name}' contains no frames.");

    var frameBytesLength = GetByteLength(name, "frame count", frameCount, FrameStructSize);
    RequireRelocatedPointer(
      name, "frame array", AddAddress(name, resourceAddress, 32), fts2Ptr, isRelocatedPointer);
    var frameBytes = ResolveRequired(
      name, "frame array", fts2Ptr, frameBytesLength, resolveData);

    var frameData = new FrameData[Convert.ToInt32(frameCount)];
    foreach (var frameIndex in Enumerable.Range(0, frameData.Length)) {
      var frameOffset = frameIndex * FrameStructSize;
      var frame = frameBytes.Span.Slice(frameOffset, FrameStructSize);
      var frameScale = ReadUInt32(frame, 0);
      var frameWidth = ReadUInt32(frame, 4);
      var frameHeight = ReadUInt32(frame, 8);
      var frameRecolorable = ReadRecolorable(
        name, $"frame {frameIndex}", ReadUInt32(frame, 12));
      var palettePtr = ReadUInt32(frame, 16);
      var texturePtr = ReadUInt32(frame, 20);
      var alphaPtr = ReadUInt32(frame, 24);

      var framePixelCount = ValidateDimensions(
        name, $"frame {frameIndex}", frameScale, frameWidth, frameHeight);
      var frameAddress = AddAddress(name, fts2Ptr, Convert.ToUInt32(frameOffset));
      RequireRelocatedPointer(
        name, $"frame {frameIndex} palette", AddAddress(name, frameAddress, 16), palettePtr,
        isRelocatedPointer);
      RequireRelocatedPointer(
        name, $"frame {frameIndex} pixel data", AddAddress(name, frameAddress, 20), texturePtr,
        isRelocatedPointer);

      var palette = ResolveRequired(
        name, $"frame {frameIndex} palette", palettePtr, PaletteSize, resolveData);
      var texture = ResolveRequired(
        name, $"frame {frameIndex} pixel data", texturePtr, framePixelCount, resolveData);
      ReadOnlyMemory<byte>? alpha = null;
      if (alphaPtr != 0) {
        RequireRelocatedPointer(
          name, $"frame {frameIndex} alpha", AddAddress(name, frameAddress, 24), alphaPtr,
          isRelocatedPointer);
        alpha = ResolveRequired(
          name, $"frame {frameIndex} alpha", alphaPtr, framePixelCount, resolveData);
      }
      // A null alpha pointer means the frame has no alpha mask: fully opaque.

      frameData[frameIndex] = new FrameData(
        frameWidth, frameHeight, frameRecolorable, palette, texture, alpha);
    }

    var frameOrder = ReadFrameOrder(
      name, offsetCount, offset1Ptr, frameCount, resourceAddress, resolveData,
      isRelocatedPointer);
    var frames = new FlexiTexture[frameOrder.Length];
    foreach (var outputIndex in Enumerable.Range(0, frameOrder.Length)) {
      var data = frameData[frameOrder[outputIndex]];
      var alpha = data.Alpha.HasValue ? data.Alpha.Value.Span : ReadOnlySpan<byte>.Empty;
      var rgbaTexture = PaletteConverter.ConvertIndexedBgraToRgba(
        data.Width, data.Height, data.Palette.Span, data.Texture.Span, alpha);
      var image = Image.LoadPixelData<Rgba32>(
        rgbaTexture, Convert.ToInt32(data.Width), Convert.ToInt32(data.Height));
      image.Mutate(context => context.Flip(FlipMode.Vertical));
      frames[outputIndex] = new FlexiTexture(
        frameOrder.Length == 1 ? name : $"{name}#{outputIndex}", data.Recolorable, image);
    }

    return new FlexiTextureList(fps, frames);
  }

  internal static TextureCollection Parse(
    string name, uint fps, uint width, uint height, IReadOnlyList<FlexiFrameData> frameData
  ) {
    var frames = new Texture[frameData.Count];
    foreach (var index in Enumerable.Range(0, frameData.Count)) {
      var frame = frameData[index];
      var rgba = PaletteConverter.ConvertIndexedBgraToRgba(
        width, height, frame.Palette.Span, frame.Texture.Span, frame.Alpha.Span);
      var image = Image.LoadPixelData<Rgba32>(rgba, Convert.ToInt32(width), Convert.ToInt32(height));
      var frameName = frameData.Count == 1 ? name : $"{name}#{index}";
      frames[index] = new Texture(
        frameName, TextureFormat.A8R8G8B8, width, height, mipCount: 1,
        recolorable: frame.Recolorable) {
        MipLevels = { [0] = image },
      };
    }

    return new TextureCollection(frames, fps);
  }

  private static int[] ReadFrameOrder(
    string name,
    uint offsetCount,
    uint offset1Ptr,
    uint frameCount,
    uint resourceAddress,
    Func<uint, int, ReadOnlyMemory<byte>?> resolveData,
    Func<uint, uint, bool> isRelocatedPointer
  ) {
    if (offsetCount == 0)
      return Enumerable.Range(0, Convert.ToInt32(frameCount)).ToArray();

    var offsetsLength = GetByteLength(name, "animation offset count", offsetCount, sizeof(uint));
    RequireRelocatedPointer(
      name, "animation offsets", AddAddress(name, resourceAddress, 24), offset1Ptr,
      isRelocatedPointer);
    var offsets = ResolveRequired(
      name, "animation offsets", offset1Ptr, offsetsLength, resolveData);
    var order = new int[Convert.ToInt32(offsetCount)];
    foreach (var index in Enumerable.Range(0, order.Length)) {
      var frameIndex = ReadUInt32(offsets.Span, index * sizeof(uint));
      if (frameIndex >= frameCount)
        throw new InvalidDataException(
          $"FlexiTexture '{name}' animation offset {index} references missing frame {frameIndex}.");
      order[index] = Convert.ToInt32(frameIndex);
    }
    return order;
  }

  private static int ValidateDimensions(
    string name, string location, uint scale, uint width, uint height
  ) {
    if (width == 0 || height == 0)
      throw new InvalidDataException(
        $"FlexiTexture '{name}' {location} has zero dimensions ({width}x{height}).");
    if (width != height || (width & (width - 1)) != 0)
      throw new InvalidDataException(
        $"FlexiTexture '{name}' {location} dimensions must be a square power of two, got {width}x{height}.");
    if (scale >= 32 || (1u << Convert.ToInt32(scale)) != width)
      throw new InvalidDataException(
        $"FlexiTexture '{name}' {location} scale {scale} does not match dimension {width}.");

    var pixelCount = Convert.ToUInt64(width) * Convert.ToUInt64(height);
    if (pixelCount > int.MaxValue)
      throw new InvalidDataException($"FlexiTexture '{name}' {location} pixel count overflows.");
    return Convert.ToInt32(pixelCount);
  }

  private static Recolorable ReadRecolorable(string name, string location, uint value) {
    var validFlags = Convert.ToUInt32(
      Recolorable.First | Recolorable.Second | Recolorable.Third);
    if ((value & ~validFlags) != 0)
      throw new InvalidDataException(
        $"FlexiTexture '{name}' {location} has invalid recolorable flags {value}.");
    return (Recolorable)value;
  }

  private static int GetByteLength(
    string name, string location, uint count, int elementSize
  ) {
    var length = Convert.ToUInt64(count) * Convert.ToUInt64(elementSize);
    if (length > int.MaxValue)
      throw new InvalidDataException($"FlexiTexture '{name}' {location} overflows.");
    return Convert.ToInt32(length);
  }

  private static ReadOnlyMemory<byte> ResolveRequired(
    string name,
    string location,
    uint pointer,
    int length,
    Func<uint, int, ReadOnlyMemory<byte>?> resolveData
  ) {
    if (pointer == 0)
      throw new InvalidDataException($"FlexiTexture '{name}' {location} pointer is null.");

    var data = resolveData(pointer, length);
    if (!data.HasValue || data.Value.Length < length)
      throw new InvalidDataException(
        $"FlexiTexture '{name}' {location} is unresolved or truncated.");
    return data.Value[..length];
  }

  private static void RequireRelocatedPointer(
    string name,
    string location,
    uint sourceAddress,
    uint targetAddress,
    Func<uint, uint, bool> isRelocatedPointer
  ) {
    if (targetAddress == 0 || !isRelocatedPointer(sourceAddress, targetAddress))
      throw new InvalidDataException(
        $"FlexiTexture '{name}' {location} pointer is not a valid relocation.");
  }

  private static uint AddAddress(string name, uint address, uint offset) {
    var result = Convert.ToUInt64(address) + Convert.ToUInt64(offset);
    if (result > uint.MaxValue)
      throw new InvalidDataException($"FlexiTexture '{name}' pointer address overflows.");
    return Convert.ToUInt32(result);
  }

  private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset) =>
    BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
