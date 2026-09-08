# FLIC Decode Gaps — Reference vs. Current Implementation

Analysis of the ~320 texture-extract failures still produced after the
[fix to the standalone flic decoder](./../bugs/ovl-texture-decoding.md),
based on cross-referencing OpenCobra's `Textures.Extract` against
[`rct3tex.cpp`](file:///C:/Users/enigm/Applications/RCT3/Dumper/rct3tex.cpp) (Jonathan Wilson's working
RCT3 flic dumper). All six failure categories turn out to be the same root
cause manifesting in different places: **OpenCobra's `ReadTexture`/`ReadFlic` only
understands standalone flics; the reference uses a different code path for
tex/flic entries whose pixel data lives in a sibling `btbl` (bitmap table)**
— and OpenCobra's `ReadBitmapTable` is still buggy in the same ways the standalone
path used to be.

---

## The Reference's Three Code Paths

`rct3tex.cpp` has three distinct loaders for texture data, dispatched by the
loader type and version:

| Loader        | When                                              | Reads                                              | Source |
|---------------|---------------------------------------------------|----------------------------------------------------|--------|
| `FlicLoader` (l. 1588) | `flic` entry, `LoaderVersion == 2` (standalone) | 4-byte chunk size → `ReadTexture` (mip while-loop) | [rct3tex.cpp:1588](file:///C:/Users/enigm/Applications/RCT3/Dumper/rct3tex.cpp#L1588) |
| `FlicLoader2` (l. 1595) | every entry of a `btbl` (bitmap table)            | `ReadTextures` with a pre-read `FlicHeader`       | [rct3tex.cpp:1595](file:///C:/Users/enigm/Applications/RCT3/Dumper/rct3tex.cpp#L1595) |
| Inline (l. 1952) | `flic` entry, any other version (BTBL-backed)     | 4-byte chunk size + 4-byte BTBL index → look up `flicstr[index].Texture` | [rct3tex.cpp:1944](file:///C:/Users/enigm/Applications/RCT3/Dumper/rct3tex.cpp#L1944) |
| Inline (l. 1368) | `tex` entry, any version                          | Just uses `tex->Flic[0]->Texture` — **no I/O**   | [rct3tex.cpp:1368](file:///C:/Users/enigm/Applications/RCT3/Dumper/rct3tex.cpp#L1368) |

The critical observation: **for `tex` entries, the reference does not read any
extra data.** It assumes the `FlicStruct**` chain in the tex (`tex->Flic[0]`)
already points to a `FlicStruct` whose `Texture` field was populated earlier by
the `flic`/BTBL loader pass. The decode work happens once, in the BTBL/flic
loaders; `tex` is just a back-reference to a texture the rest of the archive
already loaded.

OpenCobra's `ReadTexture` ([`Textures.cs:140-164`](../../OpenCobra/OVL/Files/Textures.cs)) assumes
the opposite: that every `tex` has a `Tex.Ts2Ptr` → `TextureStruct2.Flic` chain
that points to the flic loader's own extra-data chunk. For BTBL-backed tex
entries that chain either doesn't exist, terminates at a `FlicStruct` with no
extra data, or terminates at a back-reference to a different flic loader — the
three sub-cases below.

---

## Failure Categories

### 1. "Failed to resolve flic data" (~161 entries) — `tex->Ts2Ptr` chain dead-ends

The `Tex.Ts2Ptr` resolves, the `TextureStruct2.Flic` field reads as a valid
pointer, `TryReadExtraData` is called on that pointer — and returns 0 chunks.
This is the "tex lives in a BTBL" case: the `Flic` field in `TextureStruct2`
points at the *back-reference* the tex holds to its flic, not at the flic
loader that owns the pixel data. No extra data is attached to that flic because
its pixel data is in the BTBL.

**Reference behavior** ([`rct3tex.cpp:1371-1385`](file:///C:/Users/enigm/Applications/RCT3/Dumper/rct3tex.cpp#L1371)):
`TextureLoader` checks `tex->Flic[0]->Texture` directly. The
texture was already decoded and stored in the global `flicstr` array by the
BTBL pass; the tex just looks it up.

**Fix in OpenCobra**: `ReadTexture` needs a `ReadTextureBTBLBacked` branch
that, when `TryReadExtraData(tex.Ts2Ptr → flicPtr)` returns 0 chunks and a
bitmap table is available, reads the 4-byte BTBL index from the flic's
*resolved block* (not its extra data), and returns `bitmapTable[index]`. The
flic's resolved block will be a `FlicStruct`-shaped 12 bytes; the
`DataRelocation` field (offset 0) is the same relocation value the standalone
path would use, but it points back to the flic, not to the pixel data.

This is roughly 161 tex entries — entries like `SkinBody_TF02_L1:mms.tex`
(peep/avatar textures) which live in BTBL-backed `mms.common.ovl` archives.

### 2. "Failed to resolve TextureStruct2" (~50 entries) — `tex->Ts2Ptr` itself doesn't resolve

`Tex.Ts2Ptr` is a relocated pointer that doesn't land in any block. The
"texture" the tex references lives in the *common* OVL, but the *symbol* (and
hence the `Ts2Ptr`) is in the *unique* OVL — and the existing
`TryResolveRelocation` only walks the currently-loaded file's blocks.

**Reference behavior** ([`rct3tex.cpp:1830-1842`](file:///C:/Users/enigm/Applications/RCT3/Dumper/rct3tex.cpp#L1830)):
`DoReloc` is called against the *merged* relocation address space across both
`common.ovl` and `unique.ovl`. The fallback `CurrentFile--; ... CurrentFile++;`
walks the other file's blocks if the first lookup fails.

**Fix in OpenCobra**: when `TryResolveRelocation(Ts2Ptr, ...)` returns false,
retry the lookup against the `Ovl.allFileTypeBlocks` set as a whole (i.e.
include the other file in the pair). This is the same change pattern
[`ovl-resource-relocation.md`](./../bugs/ovl-resource-relocation.md) identified
for Suspect 5; it just needs to be applied to the tex/flic decoders too.

### 3. "Failed to resolve bitmap table data" (~25 entries) — BTBL has fewer than 2 chunks

`ReadBitmapTable` ([`Textures.cs:256`](../../OpenCobra/OVL/Files/Textures.cs)) currently does:

```csharp
if (!ovl.TryReadExtraData(file, out var chunks) || chunks.Count < 2)
  throw new InvalidOperationException(...);
```

The reference ([`rct3tex.cpp:1895-1907`](file:///C:/Users/enigm/Applications/RCT3/Dumper/rct3tex.cpp#L1895))
reads the BTBL as:

```cpp
fread(&size, 4, 1, f);          // chunk 0 size (discarded)
fread(&val1, 4, 1, f);          // first 0 long
fread(&val2, 4, 1, f);          // second 0 long
... count FlicHeaders ...
fread(&size, 4, 1, f);          // chunk 1 size (discarded)
... count FlicStructs (ReadTextures for each) ...
```

That's exactly **two** `size` reads framing the rest. But in OpenCobra's
`ReadLoaderExtraData` ([`OVL.cs:300-318`](../../OpenCobra/OVL/OVL.cs)), each chunk
is its own `[size, data]` pair. For a 25-entry BTBL, the FlicHeader array is
`25 * 16 = 400` bytes — that alone fits comfortably in one chunk — and the
pixel-data chunk is a second chunk.

The 25 failing BTBLs have only 1 chunk. That suggests one of two bugs:
- (a) `ReadLoaderExtraData` is mis-counting chunks for these loaders, or
- (b) Some BTBLs only have 1 chunk on disk and the reference's two `size`
      reads are actually both reading from the *same* chunk, with the
      `val1`/`val2` "zero longs" being a 4-byte header that OpenCobra is
      missing.

The reference's quirk `if ((val1 != 0) || (val2 != 0)) val1 = val2;` is a
strong hint that the 8 bytes between chunk-size and first FlicHeader are not
*always* zero in the wild, and the code is papering over that. A 25-entry
failure cluster from a single pair (`mms.tex` entries) suggests option (a):
the BTBL loader for these specific archives attaches only one chunk (the
pixel data) and the FlicHeader array is read inline from the *main data
block* — which OpenCobra would currently read as the resource bytes and
throw away as the "header" chunk.

**Fix in OpenCobra**: investigate the BTBL `b->count` FlicHeaders against
`ReadLoaderExtraData`'s output for one of the failing archives (`mms.common.ovl`
or one of the peep-body archives) — is the FlicHeader array coming through
as part of `ReadResource(btbl file)`, or in a chunk, or both?

### 4. "references a bitmap table that failed to decode" (~80 entries) — cascading from #3

Once a BTBL in an archive fails to decode (category #3), every flic in that
archive that points to it via the 4-byte-index path throws
`'{name}' references a bitmap table that failed to decode`. This is the
expected cascade; it will resolve itself when #3 is fixed.

### 5. "Image cannot be loaded. Available decoders: ..." (4 entries) — BTBL pixel data hit the wrong decoder

The 4 surviving `UnknownImageFormatException` failures are in
`ReadBitmapTable` ([`Textures.cs:283`](../../OpenCobra/OVL/Files/Textures.cs)):

```csharp
textures[i].MipLevels[mip] = Image.Load<Rgba32>(data);
```

`Image.Load<Rgba32>` runs the raw bytes through ImageSharp's format
auto-detector (PNG, JPEG, GIF, BMP, …). The BTBL pixel data is *not* an
image format — it's raw A8R8G8B8/DXT pixel bytes. The 4 failing entries are
just the BTBLs whose pixel bytes happen to look enough like a known image
format header for ImageSharp to *try* and fail. The standalone `ReadFlic` path
was already fixed in the prior pass to use `Image.LoadPixelData` with explicit
dimensions, but `ReadBitmapTable` was missed.

**Fix in OpenCobra**: change `ReadBitmapTable` to use
`Image.LoadPixelData<Rgba32>(rgbaPixels, width, height)` with a pre-decoded
pixel buffer (and `DxtDecoder.DecodeDxtN` for the compressed formats) —
matching what `ReadFlic` now does.

### 6. 3 `ArgumentOutOfRangeException` + 3 `BitConverter.ToInt32` errors

Small handful, each likely a one-off resource-classification or boundary bug
worth investigating individually once the categories above are addressed. None
look systemic; probable causes include a flic with a `Mipcount` other than
`0` or `9` that makes `ComputeMipCount` return the wrong length, or a tex
entry whose inline `60 bytes` is shorter than the struct claims (e.g.
`mms.tex` body truncated to fit the in-place resource bytes).

---

## Priority Order

1. **#5 (4 entries)**: Trivial, ~5 lines. `ReadBitmapTable` should mirror
   `ReadFlic`'s decoder dispatch.
2. **#1 (~161 entries)**: The `tex` decoder needs a BTBL-backed branch. The
   chain is well-defined: when `TryReadExtraData(Ts2Ptr → flicPtr)` returns 0
   chunks and a `bitmapTable` is available, the flic's *inline* (12-byte
   FlicStruct) `DataRelocation` field is the BTBL index. Use
   `bitmapTable[index].WithName(name)`.
3. **#3 (~25 entries)**: Needs investigation against a real failing archive.
   OpenCobra's BTBL handling reads the FlicHeader array out of a chunk; the
   failing archives apparently don't have it in a chunk. Most likely fix:
   fall back to reading the FlicHeader array inline from `ReadResource(btbl file)`'s
   bytes if `chunks.Count == 1`.
4. **#2 (~50 entries)**: Cross-file relocation retry. Same fix pattern as
   `ovl-resource-relocation.md` Suspect 5.
5. **#4 (~80 entries)**: Resolves with #3.
6. **#6 (6 entries)**: Case-by-case.

#1 alone takes the decoded count from **51 → ~212**; #3+#5 take it to ~241.
Combined, all six should land the standalone + BTBL paths at parity with the
reference decoder.

---

## Key Source References

- [`rct3tex.cpp:1588`](file:///C:/Users/enigm/Applications/RCT3/Dumper/rct3tex.cpp#L1588) — `FlicLoader` (standalone, version 2)
- [`rct3tex.cpp:1595`](file:///C:/Users/enigm/Applications/RCT3/Dumper/rct3tex.cpp#L1595) — `FlicLoader2` (BTBL)
- [`rct3tex.cpp:1368`](file:///C:/Users/enigm/Applications/RCT3/Dumper/rct3tex.cpp#L1368) — `TextureLoader` (tex, no I/O)
- [`rct3tex.cpp:1891`](file:///C:/Users/enigm/Applications/RCT3/Dumper/rct3tex.cpp#L1891) — `btbl` inline read
- [`rct3tex.cpp:1944`](file:///C:/Users/enigm/Applications/RCT3/Dumper/rct3tex.cpp#L1944) — `flic` inline read (BTBL index path)
- [`Textures.cs:140`](../../OpenCobra/OVL/Files/Textures.cs#L140) — `ReadTexture` (tex chain)
- [`Textures.cs:170`](../../OpenCobra/OVL/Files/Textures.cs#L170) — `ReadFlic` (fixed in prior pass)
- [`Textures.cs:246`](../../OpenCobra/OVL/Files/Textures.cs#L246) — `ReadBitmapTable`
- [`OVL.cs:300`](../../OpenCobra/OVL/OVL.cs#L300) — `ReadLoaderExtraData` (chunk bookkeeping)
