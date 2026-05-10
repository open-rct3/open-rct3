# OVL Texture and Bitmap Table Relationships

## Architecture Overview

OVL archives come in **paired files**:

- **Common OVL** (`.common.ovl`) — shared resources used across multiple archives
- **Unique OVL** (`.unique.ovl`) — archive-specific resources

When loaded together via `Ovl.Load()`, they form a **single combined virtual address space** where the unique file's
addresses are offset by the common file's relocation base:

```csharp
uniqueReloBase = commonData.ReloOffset
```

## Resource Types

Both common and unique OVLs can contain these texture-related resource types:

| Tag           | FileType        | Description                           |
| ------------- | --------------- | ------------------------------------- |
| `tex`         | Texture         | 2D Texture                            |
| `flic`        | Flic            | Compressed 2D Image (animated frames) |
| `ftx` / `flt` | FlexibleTexture | Flexi-Texture                         |
| `btbl`        | BitmapTable     | Bitmap Table                          |

## How Textures Relate Across Common/Unique

### 1. Both Files Can Have Textures

The codebase doesn't enforce that textures only live in one or the other. Both common and unique OVLs can contain `tex`,
`flic`, `btbl`, and other resource types.

### 2. Loader Entries Map to Data via Relocations

Each loader entry has a `DataAddress` (virtual address) that points into the combined address space. The relocation
system resolves whether that address falls in the common or unique portion:

```csharp
private bool ResolveAddress(uint address, out OvlType fileType, out int file, out uint block, out uint offset) {
  if (uniqueData != null && address >= uniqueReloBase)
    fileType = OvlType.Unique;
  // ...
}
```

### 3. String Table Sharing

The common OVL typically contains the string table (block 0). When loaded as a pair, the common's string table provides
names for loader entries in both files. The unique OVL often has an empty block 0.

### 4. Bitmap Tables (`btbl`)

Bitmap tables are a distinct resource type, not a container for textures. They appear to be tables that reference or
organize bitmap data, but the exact internal structure of bitmap table data isn't fully decoded in this codebase yet.

## Water OVLs Specifically

The test file at `OpenCobra/OVL Tests/ListResources.cs:150-239` examines the Water OVL pair. It loads both
`Water.common.ovl` and `Water.unique.ovl` together and inspects:

- How many entries come from common vs unique
- Which entries are named vs unnamed
- The loader headers and their tags

The code doesn't show a special hard-coded relationship between Water's textures and bitmap tables — they're just
different resource types that can exist in either file. The relationship is determined by the **relocation pointers** at
runtime: a texture or bitmap table entry's `DataAddress` will resolve to either the common or unique file block based on
the virtual address space layout.

## Key Files for Reference

| File                                   | Purpose                                                                                     |
| -------------------------------------- | ------------------------------------------------------------------------------------------- |
| `OpenCobra/OVL/OVL.cs`                 | Core OVL parsing (header structs, Read/Load/Open, relocations, string/symbol/loader tables) |
| `OpenCobra/OVL/Files/FileTypes.cs`     | FileType enum (29 types), tag-to-type mapping, display names, icon names                    |
| `OpenCobra/OVL Tests/ListResources.cs` | NUnit tests including `ExamineWaterOvlBinaries()`                                           |
| `docs/ai/Porting libOVL.md`            | Detailed documentation of the porting effort, binary format, bug fixes                      |
