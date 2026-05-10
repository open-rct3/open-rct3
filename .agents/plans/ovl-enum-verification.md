# Plan: Resolve OVL Enum TODOs (Items 1 & 2)

## Context

Two outstanding TODOs in `OpenCobra/OVL/Enums.cs` required cross-referencing the original C++ source in the
rct3-importer repo at `rct3constants.h`. Both are now resolved by research.

---

## Item 1: SvdType and LODs (`Enums.cs:36`)

### Finding

**The `SvdType` enum is misnamed.** In the original C++ source (`rct3constants.h`, lines 352–376), this enum is defined
as `SVD::LOD_Type` — the mesh type discriminator for individual LOD structs within an SVD, not a property of the SVD
itself.

```cpp
struct SVD {
    struct LOD_Type {
        enum { Static = 0, Animated = 3, Billboard = 4 };
    };
};
```

Each `SceneryItemVisualLOD` struct has a `meshtype` field using these values:

- `0` → `StaticShape` (static 3D mesh)
- `3` → `BoneShape` (animated/bone-driven mesh)
- `4` → `Billboard` (screen-facing flat sprite)

A single SVD can contain multiple LOD entries, each with its own `meshtype`. So this enum does relate to LODs — it is
the per-LOD mesh type, not a property of the parent SVD.

The `.opencode/plans/OVL Decoding/ovl-scenery-item-visuals.md` plan proposes `MeshType` as the name.

### Change

**Rename** `SvdType` → `SvdLodType` to align with the C++ `SVD::LOD_Type` name.

Update the member names to reflect what they actually reference:

- `Static` (0) → `StaticShape`
- `Animated` (3) → `BoneShape`
- `Billboard` (4) → `Billboard` (unchanged)

Replace the `TODO` remark with a doc comment explaining the relationship.

**File:** `OpenCobra/OVL/Enums.cs`

**Also check for usages** of the old `SvdType` name to update call sites (grep the repo before editing).

---

## Item 2: NoShadow/Flower Duplicate (`Enums.cs:49`)

### Finding

**The duplication is intentional.** In `rct3constants.h` (lines 360–376), both `No_Shadow` and `Flower` are explicitly
defined as `0x00000002`:

```cpp
struct Flags {
    enum {
        Greenery  = 0x00000001,
        No_Shadow = 0x00000002,
        Flower    = 0x00000002,
        Rotation  = 0x00000004,
        // ...
    };
};
```

The bit has dual semantics depending on object type: on flower-type objects (`SidType.Flowers`), it marks them as
flowers (and incidentally suppresses shadow casting, as flowers have no meaningful shadows); on all other object types,
the same bit is interpreted as `NoShadow` alone.

### Change

**Replace the `FIXME` remark** with a proper `<remarks>` that explains the intentional dual semantics and cites the
original C++ source. Do not change the values.

**File:** `OpenCobra/OVL/Enums.cs`

---

## Implementation

### 1. Grep for existing usages of `SvdType`

Before renaming, find all references:

```
grep -r "SvdType" --include="*.cs"
```

### 2. Update `Enums.cs`

**Change 1 — rename `SvdType` and its members:**

```csharp
// Before:
/// <summary>...</summary>
/// <remarks>TODO: Confirm whether this relates to LODs.</remarks>
public enum SvdType {
    Static = 0,
    Animated = 3,
    Billboard = 4
}

// After:
/// <summary>Mesh type for a single LOD entry within a Scenery Item Visual (SVD).</summary>
/// <remarks>
/// Corresponds to <c>SVD::LOD_Type</c> in the original C++ source (rct3constants.h).
/// Each <c>SceneryItemVisualLOD</c> struct contains a <c>meshtype</c> field using these values.
/// A single SVD may contain multiple LODs, each with a different mesh type.
/// </remarks>
public enum SvdLodType {
    StaticShape = 0,
    BoneShape = 3,
    Billboard = 4
}
```

**Change 2 — update `NoShadow`/`Flower` FIXME remark:**

```csharp
// Before:
NoShadow = 0x00000002,
/// <remarks>FIXME: Has the same value as <see cref="NoShadow"/>.</remarks>
Flower = 0x00000002,

// After:
NoShadow = 0x00000002,
/// <remarks>
/// Alias for <see cref="NoShadow"/>. Both are explicitly <c>0x00000002</c> in the original
/// C++ source (rct3constants.h). On flower-type objects (<see cref="OVL.SidType.Flowers"/>),
/// this bit identifies the object as a flower; on all other object types, it suppresses
/// shadow casting. The functional effect (no shadow) is the same in both contexts.
/// </remarks>
Flower = 0x00000002,
```

### 3. Update any `SvdType` call sites

Update usages found in step 1 (rename `SvdType` → `SvdLodType`, `Static` → `StaticShape`, `Animated` → `BoneShape`).

### 4. Update the OVL Decoding plan

Update `.opencode/plans/OVL Decoding/ovl-scenery-item-visuals.md` status if relevant (this is a doc change, not a
decoder implementation).

---

## Verification

- `dotnet build OpenCobra/OVL/OVL.csproj` — no errors
- `dotnet build OpenRCT3/OpenRCT3.csproj` — no errors
- Confirm no remaining references to old `SvdType`/`Static`/`Animated` names via grep
