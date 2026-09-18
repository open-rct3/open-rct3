# Parsing and Ingesting Texture Data

This document describes the binary layouts, relationships, and ingestion workflows for RollerCoaster Tycoon 3 (Cobra engine) textures, based on the `libOVLng` reference implementation and the C# parser port in `OpenCobra`.

---

## 1. Texture Resource Types & Relationships

Textures in the Cobra engine are managed via three cooperating loader/symbol types:

| Tag | Name | Type ID | C++ Manager | C# Representation | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `tex` | `Texture` | `2` | `ovlTEXManager` | [Tex](../../OpenCobra/OVL/Files/Textures.cs) | Holds texture style mappings, counts, and pointers to flic definitions. |
| `flic` | `Flic` | `2` or `3` | `ovlFLICManager` | [Flic](../../OpenCobra/OVL/Files/Textures.cs) | Contains image dimensions, format, mip headers, and raw pixel payloads. |
| `btbl` | `BmpTbl` | `1` | `ovlBTBLManager` | [BitmapTable](../../OpenCobra/OVL/Files/Textures.cs) | A table aggregating multiple shared textures into a single continuous block. |

### Relationships and Load Ordering
* **Bitmap Table Dependency**: If an OVL archive contains a `btbl` resource, all `flic` files in that archive are stored inside it. The `flic` resources themselves contain only a 4-byte table index. Therefore, **`btbl` resources must be processed first** to build the texture pool before resolving dependent `flic` index references.
* **Coexistence**: Textures and standalone flics can reside in either the common or the unique OVL archive. The virtual address space offsets relocations across both files transparently.

---

## 2. Binary Layouts and Structure Alignments

To secure safe execution and precise layout mapping on 64-bit hosts, OpenCobra maps original C++ structs to explicitly sized sequential or explicit layout structures.

### The Tex Struct (`tex` Main Data)
Maps to `TextureStruct` (`t1`) in Frontier's `icontexture.h`.
* **Size**: 60 bytes.
* **Fields**:
  - `Count` (offset 32): Number of textures.
  - `TextureData` (offset 44): Relocation pointer to texture style reference (TXS).
  - `CountAndAddon` (offset 48): Lo-word stores texture count; Hi-word represents the expansion pack addon ID (`Addon`).
  - `Flic` (offset 52): Relocation pointer to `FlicStruct` array (`fl`).
  - `ExtraData` (offset 56): Relocation pointer to `TexExtra` struct (`ts2`).

### The TexExtra Struct
Maps to `TextureStruct2` (`t2`) in Frontier's `icontexture.h`.
* **Size**: 8 bytes.
* **Fields**:
  - `Tex` (offset 0): Relocation pointer back to `Tex` (`t1`).
  - `Flic` (offset 4): Relocation pointer to `FlicStruct` (`fl2`).

---

## 3. Ingesting Standalone vs. Table-Indexed FLICs

Flics are modeled as a 12-byte `FlicStruct` (`Flic` in C#) followed by either index references or image payloads in their extra data chunk.

### Binary Layouts on Disk

#### A. Standalone FLICs
```text
[Main Data Block]
0x00: FlicStruct fl2 (12 bytes)
      - FlicDataPtr (4 bytes, always 0 on disk)
      - unk1 (4 bytes, always 1)
      - unk2 (4 bytes float, always 1.0)

[Extra Data Chunk]
0x0C: FlicHeader (16 bytes)
      - Format (4 bytes)
      - Width (4 bytes)
      - Height (4 bytes)
      - MipCount (4 bytes)
0x1C: FlicMipHeader (16 bytes) for Mip 0
      - Width, Height, Pitch, Blocks
0x2C: Mip 0 Pixel Data (Pitch * Blocks bytes)
... (repeat for other mips)
0xXX: Zeroed FlicMipHeader (16 bytes) [End Marker]
0xXX: ExtraDataInfoV5 (14 bytes) [Version 5 Only]
```

#### B. Table-Indexed FLICs (When BTBL Exists)
```text
[Main Data Block]
0x00: FlicStruct fl2 (12 bytes)

[Extra Data Chunk]
0x0C: Table Index (4 bytes)
0x10: ExtraDataInfoV5 (14 bytes) [Version 5 Only]
```

---

## References
* [`icontexture.h`](https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/include/icontexture.h): Original C++ structure layouts.
* [`ManagerTEX.cpp`](https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/src/libOVLng/ManagerTEX.cpp): Texture manager.
* [`ManagerFLIC.cpp`](https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/src/libOVLng/ManagerFLIC.cpp): Standalone and indexed FLIC writer.
* [`ManagerBTBL.cpp`](https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/src/libOVLng/ManagerBTBL.cpp): Bitmap table layout generation.
