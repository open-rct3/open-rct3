namespace OpenCobra.Textures;

public static class PaletteConverter {
    public static void ConvertIndexedToRgba(ReadOnlySpan<byte> indexedPixels, int width, int height, ReadOnlySpan<byte> palette, ReadOnlySpan<byte> alphaPixels, Span<byte> outputRgba) {
        int pixelCount = width * height;
        bool hasAlpha = alphaPixels.Length > 0;
        for (int i = 0; i < pixelCount; i++) {
            byte index = indexedPixels[i];
            int dst = i * 4;
            outputRgba[dst + 0] = palette[index * 3 + 0]; // R
            outputRgba[dst + 1] = palette[index * 3 + 1]; // G
            outputRgba[dst + 2] = palette[index * 3 + 2]; // B
            outputRgba[dst + 3] = hasAlpha ? alphaPixels[i] : (byte)255;
        }
    }
}
