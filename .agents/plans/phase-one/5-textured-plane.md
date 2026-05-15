# Phase 5: Render Textured Plane with `nullbmp`

**Goal**: Load `nullbmp.common.ovl`, decode the flexi-texture, and render the quad with it.

## 1. Prototype Palette Conversion

Verify the existing `PaletteConverter.cs` in `OpenCobra.GDK`.

- [x] Create `OpenCobra/Tests/GDK/PaletteConverterTests.cs`.
- [x] Test `ConvertIndexedToRgba` with a mock 256-color palette and indexed data.
- [x] Ensure alpha is correctly handled when `alphaPixels` is empty vs. provided.

## 2. Finish `TextureLoader`

Complete the implementation in `OpenCobra/GDK/Assets/TextureLoader.cs`.

- [x] Implement `LoadTextureFromOvl` to handle standard texture files.
- [x] Refine `LoadFlexiTextureFromOvl` to ensure it correctly reads the 768-byte palette (256 * RGB).
- [x] Fix `ConvertFlexiToGdkTexture` to use the correct stride (it currently allocates `width * height * 4` but passes the `Rgba32[]` directly).

## 3. Integrate Texture in `Scene`

- [x] Update `OpenCobra/GDK/Scene.cs`:
    - Ensure `LoadTexture(string path)` is called with the correct path to `nullbmp.common.ovl`.
    - Verify UV coordinates on the quad are `(0,0)` to `(1,1)`.

## 4. Update OpenGL Renderer

The `Renderer.cs` in `OpenRCT3.OpenGL` already has some texture logic, but it needs verification.

- [x] Ensure `SetupResources` correctly handles `scene.Model.Material.AlbedoTexture`.
- [x] Bind `u_Texture` uniform to texture unit 0.
- [x] Verify `TexImage2D` is using `PixelFormat.Rgba` and `PixelType.UnsignedByte`.

## 5. Verification

- [x] Run `make test` to ensure GDK logic is sound.
- [x] Launch the application.
- [x] **Milestone**: The ground plane should no longer be a solid color, but display the `nullbmp` texture.
