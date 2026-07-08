# RCT3 Advanced Fireworks Editor (AFE): Particle System Research for OpenRCT3

## 1. General particle system foundations (the baseline)

Before getting into AFE specifics, here's the standard game-particle-system model AFE is built on:

- **Emitter**: a spawn source with a rate, an origin/shape, and an initial velocity distribution. Classic CPU particle systems (id Tech, early Unreal, Cocos2D-style) all reduce to: spawn rate → per-particle initial state → per-frame integration → death when `age > lifetime`.
- **Particle state**: position, velocity, age/lifetime, size (often start/end interpolated), color (often a gradient/keyframe track), rotation, and a handle back to its emitter.
- **Forces/motion modifiers**: gravity, drag (velocity-proportional decel), and turbulence are the three universal ones. Most engines apply them as `velocity += (gravity - drag*velocity) * dt` each tick — this is exactly the exponential-slowdown behavior the AFE guide describes for "Drag."
- **Rendering**: essentially always camera-facing billboarded quads/sprites with additive or alpha blending, sorted or drawn back-to-front, sizes driven by start/end size interpolation.
- **Hierarchical/nested emitters**: the more advanced pattern (used in Unreal Cascade/Niagara, Houdini POPs, and AFE) is emitters-that-spawn-emitters — i.e., a particle's death or a time event triggers a *new* emitter using the dying particle's transform/velocity as its parent frame. That's the "shell explodes into stars" pattern.

AFE is a fairly pure implementation of that last pattern, with no shader/GPU compute — it's era-appropriate (2004) CPU-side, sprite-based, tree-structured particle scripting exposed directly to end users instead of hidden in engine code.

## 2. AFE's specific data model

Sourced from the Culcraft AFE guide (originally written 2006, mirrored from a 2008 Wayback Machine capture) and corroborating community tutorials (DRP's AFE tutorial on Atari Forums, etc.).

### Structure

A firework is a **tree** of emitter nodes. The root ("ancestor") holds no particles itself; you attach "child emitters" beneath it, and children can have their own children indefinitely, forming Parent → Child chains (e.g. Shell → Tail-particle-emitter, Shell → Star-emitter, Star → its own tail).

### Per-emitter parameter blocks

- **Emitter Rate**: `startTime`, `endTime` (both as % of parent particle lifetime, 0–1), `random` (jitter on those), `particleCount`, and a `useStrengthModifier` flag letting a show-level "strength" scalar scale count down.
- **Emitter Speed**: `speed` (scalar), `posSphere`/`posCircle` (initial spawn-shape), `sphere`/`circle` (initial velocity direction shaping), and `parentSpeedInheritance` (0–1 fraction of parent's velocity vector inherited — the key parent-child coupling term).
- **Emitter Rotation**: `particleRelative` flag (whether the emitter's facing is in particle-local space or world/park space), plus spin offset and spin rate per axis (stored in radians).
- **Particle Life**: `lifetime` (0.05–20s), `timeRandom` (0–1 as % of lifetime), plus `soundTime` and sound selection/min-max distance for audio culling.
- **Particle Basics (appearance)**: a color-over-life curve (multiple color stops → fade/glitter), a sprite/shape index into a fixed texture atlas (stars, smoke, streaks, etc.), `startSize`/`endSize` (0–50) with random variance, and a `useColorModifier` flag that exposes a single recolor slot to the MixMaster/show-builder UI (this is how "recolorable fireworks" work).
- **Special Effects**: `burnLength`/`burnSize` (an automatic trailing-tail generator — essentially an implicit extra child chain rather than a manually built one), `stretch` (velocity-aligned scale, used for laser/streak effects — scales with *speed*, not lifetime), and `spinRate` (perpendicular roll).
- **Particle Motion**: `drag` (0–5, exponential-style velocity damping), `gravity` (–2 to 2, signed so negative values simulate buoyant/rising smoke), `upscale` (a bounded orbital/spiral term in the X/Z plane — noted by the community as breaking above ~0.7, suggesting an unstable or clipped integration), and per-axis spin rates.

### File format (`.frw`)

`.frw` files use RCT3's generic `.dat`-style binary chunk format (same family as saved-park/prefab data, distinct from `.ovl` scenery objects). Community reverse-engineering (Coaster-Games.org forum thread) found:

- A header region with data that doesn't appear to affect the tree structure (likely fixed boilerplate/version info).
- The actual payload is the emitter tree itself, serialized node-by-node with parent/child links.
- A trailing 32-bit value functions as a checksum/footer (suspected CRC32-family, not confirmed) — files with a missing or incorrect footer load fine in MixMaster, but the *in-game AFE editor itself* rejects them as "file might be corrupted." This matters if your reimplementation needs to write files editable in original RCT3, but is irrelevant if you're purely round-tripping within OpenRCT3.
- No one has publicly finished a from-scratch `.frw` writer with a correct checksum; existing community tools only *edit* an existing valid file's tree rather than synthesizing the footer from nothing. This remains a genuine open problem if byte-for-byte legacy compatibility matters.

## 3. Design implications for an OpenRCT3 reimplementation

A clean modern re-architecture, translating AFE's model directly:

```txt
EmitterNode {
  children: [EmitterNode]
  rate: { startPct, endPct, randomPct, count, strengthScaled }
  spawnShape: { posSphere, posCircle }
  velocity: { speed, sphereSpread, circleSpread, parentInherit }
  rotation: { relative, spinOffset[3], spinRate[3] }
  life: { lifetime, lifeRandomPct, sound: {time, clip, minDist, maxDist} }
  appearance: { colorStops[], spriteIndex, sizeStart, sizeEnd, sizeRandomPct, colorModifierSlot? }
  fx: { burnLength, burnSize, stretch, spinRate }
  motion: { drag, gravity, upscale, spinRateX, spinRateY }
}
```

Simulation step, per particle:
1. Integrate velocity (apply gravity, drag, upscale/orbital term).
2. Advance age.
3. When `age/lifetime` crosses a child's `startPct`/`endPct` window, spawn that child's particles, inheriting `parentInherit * parentVelocity` plus the child's own shape/speed terms.

This lets the "burn" auto-tail be implemented as sugar over an implicit child emitter rather than a special-cased renderer path.

**Legacy content support**: separate concerns — (1) an internal, saner data model like the one above for the actual sim/editor, and (2) a `.frw` importer that parses the known chunk layout and ignores the footer checksum on read. Only attempt to regenerate a valid checksum if round-trip compatibility with real RCT3 is a hard requirement — the algorithm is currently unknown/unconfirmed, so this may not be worth pursuing.

## 4. Sourcing notes / caveats

- The Culcraft guide is the most complete extant technical writeup, itself preserved from a 2008 Wayback Machine capture of an original 2006 document — the primary source (vpyro.com) is long defunct.
- I was not able to pull a live Wayback Machine capture of vpyro's own technical forum threads directly; the `.frw` file-format details above come from a **secondary** reconstruction (Coaster-Games.org thread discussing someone's unreleased "Xtreme Firework Editor" from vpyro-era discussions), not vpyro's original archived text.
- If byte-level file format accuracy is critical, it's worth trying direct Wayback Machine snapshot URLs for vpyro.com and rct3-archive.org rather than relying on search-indexed excerpts — happy to do that pass if useful.
