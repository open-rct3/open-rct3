# OVL Enum Verification

Resolved two `TODO` comments in `OpenCobra/OVL/Enums.cs` by cross-referencing the original C++ source (`rct3constants.h`).

## Changes Required

### 1. Rename `SvdType` → `SvdLodType`

Corresponds to `SVD::LOD_Type` in C++. Each `SceneryItemVisualLOD` has its own `meshtype` field.

- `Static` (0) → `StaticShape`
- `Animated` (3) → `BoneShape`
- `Billboard` (4) → `Billboard`

### 2. Document `NoShadow`/`Flower` Intentional Duplicate

Both are `0x00000002`. On `SidType.Flowers` objects it marks a flower; on all others it suppresses shadows. The functional effect is the same.
