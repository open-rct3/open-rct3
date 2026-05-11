# OVL Archive Format

The OVL format is an archive used by the Cobra engine (RollerCoaster Tycoon 3, Elite Dangerous, etc.) to store game
resources.

OVL archives often come in **pairs**:

- **Common OVL** (`.common.ovl`): shared resources used across multiple archives
- **Unique OVL** (`.unique.ovl`): archive-specific resources

When loaded together via `Ovl.Load()`, they form a **single combined virtual address space** where the unique file's
addresses are offset by the common file's relocation base.

## File Structure

An OVL file consists of a header, an external reference list, loader metadata, block definitions, raw data, and a
relocation table.

### 1. Header

| Offset | Type  | Description                                     |
| :----- | :---- | :---------------------------------------------- |
| 0x00   | `u32` | Magic: `0x4B524746` ("FGRK")                    |
| 0x04   | `u32` | Header Reference Count (v1) or Reserved (v4/v5) |
| 0x08   | `u32` | Version (1, 4, or 5)                            |
| 0x0C   | `u32` | Reference Count (v4) or Header Data (v1)        |

#### Version 5 Extended Header

If version is 5, the following fields follow immediately:

- `subVersionFlag` (u32): Usually 0 or 1.
- If `subVersionFlag != 0`:
  - 3x `u32` unknown fields (usually 0).
  - Null-terminated string (unknown bytes), padded to 4-byte boundary.
- `referenceCount` (u32): The actual number of external OVL references.

### 2. External References

A list of other OVL files this archive depends on.

- Each entry: `length` (u16) followed by `name` (ASCII string, no null terminator).

### 3. Loader Metadata (v4 and v5 only)

Metadata describing the "loaders" (resource types) used in this archive.

- `loaderCount` (u32): Number of loader headers.

Each **Loader Header**:

| Type     | Description                                |
| :------- | :----------------------------------------- |
| `u16`    | Loader name length                         |
| `string` | Loader name (e.g., "flexi_texture_loader") |
| `u16`    | Display name length                        |
| `string` | Display name                               |
| `u32`    | Loader type ID                             |
| `u16`    | Tag length                                 |
| `string` | Extension/Tag (e.g., "ftx", "tex", "sid")  |

#### Version 5 Symbol Counts

If version is 5, a table follows the loader headers mapping loaders to symbol counts:

- For each loader: `loaderIdx` (u32), `symbolCount` (u32).

### 4. Block Definitions

OVL data is organized into up to 10 types of blocks (Type 0 to Type 9).

For each type (0-9):

- `count` (u32): Number of blocks of this type.
- If `version > 1`:
  - `unknown` (u32)
  - If `version == 5` and `subVersionFlag != 0`: `extra` (u32)
  - `sizes` (count * u32): The size of each individual block.

### 5. Post-Block Metadata

Version-specific unknown data chunks:

- **v4**: 2x `u32` (usually 0).
- **v5**:
  - `bytesCount` (u32) followed by `bytesCount` bytes.
  - `longCount` (u32) followed by `longCount * u32` values.

### 6. Raw Data

The data for all blocks defined in section 4, stored sequentially.

- **Relative Offsets**: OVLs use a global relative offset space. A pointer (u32) in the data refers to a location across
  all blocks combined.
  - `Offset 0` usually starts at the beginning of Type 0 Block 0.
- **Type 0 Block**: Usually contains the **String Table** (null-terminated ASCII strings).
- **Type 2 Block**: Usually contains the **Symbol Table** (resource definitions).

### 7. Relocation Table

A list of addresses within the archive's relative offset space that contain pointers which must be patched/resolved.

- `relocCount` (u32)
- `offsets` (relocCount * u32): Relative offsets where 32-bit pointers are located.

#### Relocation Resolution

Crudially, a unified address space among the common and unique files is created. When a relocation address is processed,
the system determines its origin file (common or unique) and the specific block within that file to correctly translate
relative addresses. Data structures map these relative addresses to actual file offsets, effectively combining the data
from both files into a single address space.

---

## Resource Discovery (Symbols)

Resources are identified by symbols, typically found in **Block Type 2**.

### Symbol Structure (v1)

Each symbol is 12 bytes:

| Offset | Type  | Description                                  |
| :----- | :---- | :------------------------------------------- |
| 0x00   | `u32` | `namePtr`: Relative offset to resource name. |
| 0x04   | `u32` | `isPointer`: Flag.                           |
| 0x08   | `u32` | `dataPtr`: Relative offset to data.          |

### Symbol Structure (v4/v5)

Each symbol is 24 bytes:

| Offset | Type  | Description                                                                          |
| :----- | :---- | :----------------------------------------------------------------------------------- |
| 0x00   | `u32` | `namePtr`: Relative offset to resource name in Type 0 block                          |
| 0x04   | `u32` | `loaderIdx`: Index into the [Loader Metadata](#loader-metadata-v4-and-v5-only) table |
| 0x08   | `u32` | `dataPtr`: Relative offset to the actual resource data                               |
| 0x0C   | `u32` | `isPointer`: Flag indicating if dataPtr is a pointer or immediate value              |
| 0x10   | `u32` | `unknown`                                                                            |
| 0x14   | `u32` | `hash` / `size`: Often contains a hash or the size of the resource                   |

## Sources

- [`libOVLDump`](https://github.com/chances/rct3-importer/tree/main/RCT3%20Importer/src/libOVLDump) reference implementation
