using System.Runtime.InteropServices;

namespace OpenCobra.Textures;

public static unsafe class PaletteConverter {
    public static void Main() { }

    [UnmanagedCallersOnly(EntryPoint = "convert_indexed_to_rgba")]
    public static unsafe void ConvertIndexedToRgbaNative(byte* indexedPixels, int width, int height, byte* palette, byte* alphaPixels, byte* outputRgba) {
        int pixelCount = width * height;
        for (int i = 0; i < pixelCount; i++) {
            byte index = indexedPixels[i];
            int pixelOffset = i * 4;

            // Palette is expected to be 768 bytes (256 * 3 RGB)
            outputRgba[pixelOffset + 0] = palette[index * 3 + 0]; // R
            outputRgba[pixelOffset + 1] = palette[index * 3 + 1]; // G
            outputRgba[pixelOffset + 2] = palette[index * 3 + 2]; // B

            if (alphaPixels != null) {
                outputRgba[pixelOffset + 3] = alphaPixels[i];
            } else {
                outputRgba[pixelOffset + 3] = 255;
            }
        }
    }

    public static void ConvertIndexedToRgba(ReadOnlySpan<byte> indexedPixels, int width, int height, ReadOnlySpan<byte> palette, ReadOnlySpan<byte> alphaPixels, Span<byte> outputRgba) {
        unsafe {
            fixed (byte* pIndexed = indexedPixels)
            fixed (byte* pPalette = palette)
            fixed (byte* pAlpha = alphaPixels)
            fixed (byte* pOutput = outputRgba) {
                int pixelCount = width * height;
                for (int i = 0; i < pixelCount; i++) {
                    byte index = pIndexed[i];
                    int pixelOffset = i * 4;

                    pOutput[pixelOffset + 0] = pPalette[index * 3 + 0]; // R
                    pOutput[pixelOffset + 1] = pPalette[index * 3 + 1]; // G
                    pOutput[pixelOffset + 2] = pPalette[index * 3 + 2]; // B

                    if (alphaPixels.Length > 0) {
                        pOutput[pixelOffset + 3] = pAlpha[i];
                    } else {
                        pOutput[pixelOffset + 3] = 255;
                    }
                }
            }
        }
    }
}
