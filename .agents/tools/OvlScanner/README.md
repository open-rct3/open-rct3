# OVL Resource Scanner

A generic console tool for discovering and enumerating any OVL resource type across OVL archives. Useful for:

- **Production OVL discovery**: Locate Splines, TrackSections, Textures, or any other resource type in RCT3 game files
- **Fixture validation**: Verify what resources are present in test fixtures
- **Pre-implementation analysis**: Survey what data is available before implementing decoders
- **Resource type surveys**: Get statistics on distribution of any resource type across the game

## Usage

From the repository root, specify resource types via command-line arguments:

```bash
# Scan for Spline and TrackSection (original Phase 0 discovery)
dotnet run --project .agents/tools/OvlScanner/OvlScanner.csproj -- spl tks

# Scan for Textures
dotnet run --project .agents/tools/OvlScanner/OvlScanner.csproj -- tex

# Scan multiple types
dotnet run --project .agents/tools/OvlScanner/OvlScanner.csproj -- shs sid svd

# Scan for everything in a category
dotnet run --project .agents/tools/OvlScanner/OvlScanner.csproj -- tex flic ftx btbl
```

### Supported File Types (29 types)

| Tag | Type | Description |
| :-- | :--- | :--- |
| `txt` | Text | Text |
| `int` | Integer | Integer Number |
| `tex` | Texture | 2D Texture |
| `flic` | Flic | Compressed 2D Image |
| `ftx` | FlexibleTexture | Flexi-Texture |
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
| `sat` | SpecialAttraction | Special Attraction |
| `shs` | StaticShape | Static Shape |
| `snd` | Sound | Sound |
| `spl` | Spline | Spline |
| `sta` | Stall | Stall |
| `svd` | SceneryItemVisual | Scenery Item Visual |
| `ter` | TerrainType | Terrain |
| `tks` | TrackSection | Track Section |
| `trr` | TrackedRide | Tracked Ride |
| `wai` | WildAnimalItem | Wild Animal Item |
| `mms` | CharacterSkinSet | Character Skin Texture Set |
| `prt` | CharacterSkinPart | Character Skin Part Texture |
| `psi` | ParticleSpriteItem | Particle Sprite Item |
| `fct` | FontCharacterTable | Font Character Table |

## Output

**Console output** shows progress and results:
- Files found in each search directory
- Processing progress (every 100 files)
- Summary statistics grouped by type

**CSV report** (`.agents/summaries/ovl-{types}-scan.csv`) contains:
- `file`: Relative path to OVL archive
- `type`: OVL file type tag (e.g., "spl", "tks")
- `count`: Number of resources of this type in the file
- `samples`: Comma-separated resource names (first 3)

Only rows with at least one matching resource are included. Filename reflects the types scanned (e.g., `ovl-spl-tks-scan.csv` for Spline + TrackSection).

## Scan Locations

The scanner looks in these locations:

**Fixtures** (always scanned):
- `OpenCobra/Tests/Fixtures/OVL/**/*.ovl`

**Production OVLs** (scanned if `RCT3_PATH` environment variable is set):
- `{RCT3_PATH}/Rides/**/*.ovl`
- `{RCT3_PATH}/tracks/**/*.ovl`

## Design Notes

- **Generic**: Accepts any FileType via command-line arguments; no hardcoding
- **Robust**: Silently skips unparseable OVL files (corrupted or unsupported versions)
- **Safe**: Uses `OpenCobra.OVL` which properly handles 32-bit relocation addresses on 64-bit systems
- **Efficient**: Processes files one at a time, minimal memory footprint
- **CSV-friendly**: Properly escapes sample names containing quotes and commas
