---
name: rct3-guidance
description: Guidance on RCT3 OVL structure, libOVL porting details, texture/bitmap relationships, and renderer control flow.
---

# RCT3 OVL and Renderer Guidance

This skill provides comprehensive guidance on the OVL archive format, the C# port of the `libOVL` parser, and the application's renderer control flow within the OpenRCT3 codebase.

---

## 1. OVL Archive & Resource Architecture

Detailed binary layout specifications is described in the [Archive Format](../../../docs/ovl/archive-format.md) documentation.

### Paired OVL Files
OVL archives in RollerCoaster Tycoon 3 are split into paired files:
*   **Common OVL (`.common.ovl`)**: Contains shared resources referenced by multiple archives (typically has string tables).
*   **Unique OVL (`.unique.ovl`)**: Contains archive-specific resources (often has an empty block 0 string table).

When loaded using `Ovl.Load()`, they merge into a **single combined virtual address space**. The unique file's addresses are offset by the common file's relocation base:
```csharp
uniqueReloBase = commonData.ReloOffset
```

### File Structure & Sections
As defined in [Archive Format](../../../docs/ovl/archive-format.md), an OVL binary consists of seven distinct components:
1.  **Header**: Core metadata identifying the magic number (`0x4B524746` / `"FGRK"`), version (1, 4, or 5), and reference count. Version 5 files contain an extended header containing sub-version flags and additional reference metrics.
2.  **External References**: A list of dependant OVL filenames containing a 16-bit length followed by the raw ASCII name.
3.  **Loader Metadata**: Describes the resource type handlers. V5 files include a mapping table of loader indices to their respective symbol counts.
4.  **Block Definitions**: Organizes data into up to 9 types of blocks (types 0 to 8), specifying counts and sizes for each instance.
5.  **Post-Block Metadata**: Version-specific trailing diagnostic or alignment bytes.
6.  **Raw Data**: Sequential data for all defined blocks. Relative offsets operate across the merged block data.
7.  **Relocation Table**: Address mapping references for pointer patching at runtime.

### Relocation Resolution Algorithm
Pointers are patched using a two-level virtual address space resolution:
1.  **File Identification**: System checks if the address is greater than or equal to `uniqueReloBase`. If so, the resource resides in the unique OVL; otherwise, it is in the common OVL.
2.  **Block Discovery**: Once the file is selected, the system scans the accumulated block offsets to find which block type (0–8) and block instance contains the address range `[relative offset, relative offset + size)`.
3.  **Address Calculation**: The absolute pointer is calculated via:
    ```csharp
    block.data + (relocationAddress - block.RelativeOffset)
    ```

### Resource Types
OVL files can store a variety of resource types, identified by their tags, including but not limited to:

| Tag | FileType | Description |
| :--- | :--- | :--- |
| `tex` | Texture | 2D Texture |
| `flic` | Flic | Compressed 2D Image (animated frames) |
| `ftx` / `flt` | FlexibleTexture | Flexi-Texture |
| `btbl` | BitmapTable | Bitmap Table |

### Texture & Bitmap Relationships
*   **Coexistence**: Textures can live in either the common or the unique OVL; there is no enforcement restricting textures to only one file.
*   **Loader Relocations**: Each loader entry uses a `DataAddress` pointing to the combined address space. The relocation system determines which file contains the target block:
    ```csharp
    private bool ResolveAddress(uint address, out OvlType fileType, out int file, out uint block, out uint offset) {
      if (uniqueData != null && address >= uniqueReloBase)
        fileType = OvlType.Unique;
      // ...
    }
    ```
*   **String Table Sharing**: The common OVL's string table (block 0) provides resource names for loaders across both files when loaded as a pair.
*   **Bitmap Tables (`btbl`)**: Bitmap tables are distinct resource types that organize or reference bitmap data rather than act as direct containers for textures.

---

## 2. libOVL C# Porting Reference

### Paired Archive Loading (`Ovl.Load`)
The loader mirrors the original `cOVLDump::Load` logic:
1.  Loads both common and unique OVLs via `Read()`.
2.  Resolves relocations across the virtual address space.
3.  Parses the string table (block 0).
4.  Parses the symbol table (block 2, sub-block 0).
5.  Parses the loader table (block 2, sub-block 1).

### Resource Discovery & Symbol Structure
Resources are registered through symbols in Block Type 2, following exact binary sizes described in [archive-format.md](../../../docs/ovl/archive-format.md):
*   **V1 Symbols (12 bytes)**: Contains `namePtr` (u32), `dataPtr` (u32), and `isPointer` (u32) flag.
*   **V4/V5 Symbols (16 bytes)**: Contains `namePtr` (u32), `dataPtr` (u32), `isPointer` (u16), an unknown field (u16), and a resource hash/size (u32).

To avoid the overhead of C# struct marshalling during high-frequency loads, `OVL.cs` parses these structures directly from the raw byte buffer using byte offsets and `BitConverter`:
```csharp
var namePtr = BitConverter.ToUInt32(symbolBlock.Data!, symOffset);
var dataPtr = BitConverter.ToUInt32(symbolBlock.Data!, symOffset + 4);
var size = symbolSize == 16 ? BitConverter.ToUInt32(symbolBlock.Data!, symOffset + 12) : 0u;
```

### Safe 32-bit Struct Equivalents & 64-bit Layout Safety
In C++, OVL resource pointers are stored as 32-bit virtual addresses. On 64-bit systems, raw pointers in C# expand to 64-bit structures, causing data alignment corruption. To ensure safe execution and precise layout mapping on 64-bit hosts, the following struct wrappers and marshaled structures are defined with explicit sequential or explicit layouts and fixed widths:

*   `RelocationPointer` ([RelocationPointer.cs](../../../OpenCobra/OVL/RelocationPointer.cs)): A 4-byte sequential wrapper structure representing a 32-bit virtual address offset.
    ```csharp
    [StructLayout(LayoutKind.Sequential, Size = 4)]
    public struct RelocationPointer {
      public uint Value;
    }
    ```
*   `Tex` ([Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs)): Explictly aligned 60-byte structure mapping Frontier's `icontexture.h` layout. Uses `RelocationPointer` fields to secure 32-bit address mappings for `TextureData`, `Flic`, and `ExtraData`.
*   `TexExtra` ([Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs)): Explicit 8-byte structure containing back-references (`Tex`) and pointer tables (`Flic`) as `RelocationPointer` structures.
*   `ExtraDataInfoV5` ([Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs)): A 14-byte sequential structure mapping the version 5 trailing header blocks.
*   `BitmapTable` ([Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs)): An 8-byte sequential structure parsing bitmap metadata.
*   `Flic` ([Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs)): A 12-byte sequential structure storing animation/frame pointer locations.
*   `FlicHeader` & `FlicMipHeader` ([Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs)): Explicitly sized 16-byte sequential structures ensuring proper mapping of image dimensions, pitch, block size, and formatting enumerations.

### File Type Tags (29 Supported Types)
The `FileType` enumeration spans all loader tags defined by the `Manager` classes:

| Tag | Type | Name |
| :--- | :--- | :--- |
| `txt` | Text | Text |
| `int` | Integer | Integer Number |
| `tex` | Texture | 2D Texture |
| `flic` | Flic | Compressed 2D Image |
| `ftx` | FlexibleTexture | Flexi-Texture (alias `flt`) |
| `gsi` | GuiSkinItem | GUI Skin Item |
| `sid` | SceneryItem | Scenery Item |
| `btbl` | BitmapTable | Bitmap Table |
| `anr` | AnimatedRide | Animated Ride |
| `ban` | BoneAnim | Bone Animation |
| `bsh` | BoneShape | Bone Shape |
| `ced` | CarriedItemExtra | Carried Item Extra |
| `chg` | ChangingRoom | Changing Room |
| `cid` | CarriedItem | Carried Item |
| `mam` | ManifoldMesh | Manifold Mesh |
| `ptd` | PathType | Path |
| `qtd` | QueueType | Queue |
| `ric` | RideCar | Ride Car |
| `rit` | RideTrain | Ride Train |
| `sat` | SpecialAttraction| Special Attraction |
| `shs` | StaticShape | Static Shape |
| `snd` | Sound | Sound |
| `spl` | Spline | Spline |
| `sta` | Stall | Stall |
| `svd` | SceneryItemVisual| Scenery Item Visual |
| `ter` | TerrainType | Terrain |
| `tks` | TrackSection | Track Section |
| `trr` | TrackedRide | Tracked Ride |
| `wai` | WildAnimalItem | Wild Animal Item |

---

## 3. Renderer Control Flow

The `Renderer.Render` method handles rendering for the `Scene` bound to the global `Game.Instance`.

### Windows paint loop
1.  **Entry Point**: `Program.windows.cs` -> Runs `MainForm`.
2.  **UI Layout**: `MainForm.Designer.cs` -> Instantiates `GLSurface` (OpenGL-enabled control).
3.  **Initialization**: `GLSurface.cs` -> Instantiates and initializes `Renderer` in `OnHandleCreated`.
4.  **Paint/Draw Execution**:
    *   `OnPaint` triggers `OnRenderFrame`.
    *   `OnRenderFrame` executes `_renderer.Render(Game.Instance.Scene)` and swaps buffers.
    *   `OnResize` triggers a repaint by calling `Invalidate()`.

```text
Program.Main()
└── MainForm (WinForms)
    └── GLSurface.OnPaint()
        └── GLSurface.OnRenderFrame()
            └── Renderer.Render(Scene)
```

### macOS paint loop
1.  **Paint/Draw Execution**: `OpenGLLayer.cs` handles Core Animation / AppKit draws.
2.  `DrawInContext` updates camera: `Game.Instance.Scene.UpdateCamera(...)`.
3.  `DrawInContext` calls `_renderer.Render(Game.Instance.Scene)`.

```text
GameViewController
└── OpenGLLayer.DrawInContext()
    └── Renderer.Render(Scene)
```

---

## References

*   [archive-format.md](../../../docs/ovl/archive-format.md): Full binary layout details.
*   [OVL.cs](../../../OpenCobra/OVL/OVL.cs): Core parser, relocations, loading, and table construction.
*   [RelocationPointer.cs](../../../OpenCobra/OVL/RelocationPointer.cs): Fixed-size relocation address wrapper.
*   [FileTypes.cs](../../../OpenCobra/OVL/Files/FileTypes.cs): Expanded `FileType` enum and mapping methods.
*   [ListResources.cs](../../../OpenCobra/OVL%20Tests/ListResources.cs): Contains OVL resource examination tests (e.g., `ExamineWaterOvlBinaries()`).
*   [ReadArchive.cs](../../../OpenCobra/OVL%20Tests/ReadArchive.cs): OVL loader test suite.
*   [GLSurface.cs](../../../OpenRCT3/Platforms/Windows/GLSurface.cs): Windows GLSurface paint/resize pipeline.
*   [OpenGLLayer.cs](../../../OpenRCT3/Platforms/macOS/OpenGLLayer.cs): macOS OpenGLLayer implementation.
