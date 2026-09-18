# Rain Ambient Sound Effect

**Roadmap**: Phase 1, Engine & Rendering Scaffolding — first audio playback in the engine (no prior
`TODO.md` item covers audio; this plan establishes the OVL `snd` decoder and the GDK audio subsystem from
scratch)

**Tracks**: [open-rct3/open-rct3#8](https://github.com/open-rct3/open-rct3/issues/8) — "Extract and play the
looping rain sound effect."

**See also**:
- [`.agents/plans/features/ovl/README.md`](ovl/README.md) — OVL decoder plan conventions (Dumper plugin
  requirement, testing approach) this plan follows for its `snd` half.
- [`plugins/snd-viewer/index.ts`](../../../plugins/snd-viewer/index.ts) — existing AssemblyScript Dumper
  plugin that already parses RCT3's `.snd` payload and reconstructs a playable WAV; the direct porting
  reference for the C# decoder below.

## Context

`FileType.Sound` already exists in [`FileTypes.cs`](../../../OpenCobra/OVL/Files/FileTypes.cs) (tag `"snd"`,
extension `"snd"`, icon `"VolumeHigh"`), but no decoder consumes it — there is no `Sound.cs` under
`OpenCobra/OVL/Files/`, matching the pattern of `TerrainTypes.cs`/`StaticShapes.cs`/etc. for other tags.

There is also **no audio playback system anywhere in the engine**: no `Audio` folder under
`OpenCobra/GDK/` (which has `Assets`, `Game`, `GUI`, `Input`, `Materials`, `Meshes`, `Platform`, `Shaders`,
`Streaming`, but nothing audio-related), no audio package reference in `GDK.csproj` (which pulls in
`Silk.NET.Input.Common`/`Silk.NET.OpenGL`/`Silk.NET.Windowing.*` but no `Silk.NET.OpenAL`), and no prior
plan or research doc mentions sound or audio. This plan is green-field on both halves: decode `snd` OVL
entries into raw PCM, and build the minimal GDK subsystem needed to loop-play one.

`rct3-importer` has no rain-specific or weather-specific code — the rain sound is just one `.snd`-typed
symbol inside a game OVL, decoded generically like any other `snd` entry. Its exact symbol name and owning
archive are not yet known and need to be found by scanning (see Open Questions).

## Goals

- **`OpenCobra/OVL/Files/Sound.cs`** decodes a `snd` resource into a `SoundEffect` record, porting
  `snd-viewer`'s already-validated parsing logic (not re-deriving the format from scratch, per
  `AGENTS.md`'s port-don't-reverse-engineer rule):
  - Read the 18-byte `WAVEFORMATEX` header directly from the resource bytes (`formatTag`, `channels`,
    `sampleRate`, `avgBytesPerSec`, `blockAlign`, `bitsPerSample`, `extraSize`), no relocation/pointer
    chasing needed — `snd-viewer` confirms the whole resource is header + raw audio data, not a
    pointer-heavy struct like `sid`/`svd`.
  - Header size is `18 + extraSize`; everything after it up to the resource's length is raw audio data
    (PCM for `formatTag == 1`, ADPCM/IMA ADPCM for `2`/`0x0011` — decode format-tag-agnostically, i.e.
    store `FormatTag` + raw bytes rather than assuming PCM, since `snd-viewer` already found both PCM and
    ADPCM entries exist).
  - `Name` comes from the symbol's own OVL name, same convention as `TerrainTypeEntry`/`Textures.cs`.
  - Shape:
    ```csharp
    public readonly record struct SoundEffect(
      string Name,
      ushort FormatTag,
      ushort Channels,
      uint SampleRate,
      uint AvgBytesPerSec,
      ushort BlockAlign,
      ushort BitsPerSample,
      byte[] AudioData
    );

    public static class Sound {
      public static IReadOnlyList<SoundEffect> Extract(Ovl ovl);
    }
    ```
  - Mirrors `TerrainTypes.cs`'s `Extract(Ovl)` static entry point and `ConcurrentBag`/`Parallel.ForEach`
    style, same as every other `OpenCobra/OVL/Files/*.cs` decoder.
- **`snd-viewer` Dumper plugin already exists and is not blocked by this plan** — no new plugin work is
  required for the OVL-decoding Goals section, unlike `ovl/README.md`'s usual Dumper Plugin Requirement
  (that requirement is about *creating* a plugin alongside a new decoder; here the plugin came first).
- **`OpenCobra/GDK/Audio/` subsystem** (new folder, sized only for "load a decoded PCM/ADPCM buffer and
  loop-play it" — not a general audio engine), following the sibling `Materials`/`Meshes`/`Shaders`
  folders' pattern of thin wrapper types around a Silk.NET binding:
  - Add `Silk.NET.OpenAL` (and `Silk.NET.OpenAL.Extensions.EXT` if needed for format edge cases) to
    `GDK.csproj`, matching the existing Silk.NET-for-everything convention (`Silk.NET.OpenGL` for
    rendering, `Silk.NET.Input.Common` for input) rather than pulling in an unrelated audio library.
  - `AudioDevice`/`AudioContext` thin wrappers around `ALContext`/device open+make-current, following
    `GLState.cs`'s pattern for the equivalent OpenGL context bring-up.
  - `AudioBuffer` wraps an OpenAL buffer object, uploaded once from a decoded `SoundEffect`'s PCM bytes
    (`AL.BufferData`) — ADPCM sources need decoding to PCM first (OpenAL core has no native ADPCM format);
    scope that conversion into `Sound.cs`'s decode step or a small `AdpcmDecoder` helper, not into
    `AudioBuffer` itself, keeping the GDK layer format-agnostic the same way `Texture` doesn't know about
    DXT decompression details.
  - `AudioSource` wraps an OpenAL source: `Play()`/`Stop()`/`Looping` (`AL.SourceProperty` maps directly to
    the "looping" ask in the issue — no custom loop-scheduling needed, OpenAL sources loop natively via
    `AL_LOOPING`), `Volume`/`Gain`.
- **Loading path**: a `SoundLoader` under `OpenCobra/GDK/Assets/` (sibling to `TextureLoader.cs`), following
  its exact shape — `LoadSound(string ovlPath, string name)` opens the OVL, finds the `FileType.Sound`
  symbol, decodes via `Sound.Extract`/a single-entry lookup, and returns a GDK `AudioBuffer` ready to
  attach to a source. Throws `AssetException` on failure, matching `TextureLoader`'s error-wrapping
  convention.
- **Playback wiring**: a rain ambience source is created once (e.g. as part of the weather/ambience system,
  or a minimal standalone hook if no weather system exists yet — see Open Questions) and calls
  `AudioSource.Play()` with `Looping = true` once the game world is loaded, `Stop()` on unload/scene teardown.
  This plan scopes only "loop-play one already-loaded buffer," not weather-state-driven volume
  fades/start-stop triggers — that's future weather-system work once one exists.

## Gaps and Risks

1. **ADPCM decoding is unimplemented anywhere in the codebase.** `snd-viewer` explicitly punts on this
   ("requires a decoder library for browser playback"). If the rain sound turns out to be ADPCM-encoded
   (not confirmed either way yet — see Open Questions), this plan needs either a small IMA ADPCM decoder
   ported into `Sound.cs`/a helper, or (lower effort) relying on OpenAL's `AL_EXT_MCFORMATS`/ADPCM
   extensions if the target platforms' OpenAL implementations support them. **Open** until the actual rain
   symbol's `formatTag` is confirmed by scanning.
2. **No existing GDK subsystem precedent for a non-rendering hardware context (audio device/context
   lifecycle, error handling, disposal ordering relative to the window/GL context).** `GLState.cs` is the
   closest analog but is GL-specific. Risk is mostly design-time (getting `IDisposable`/init-order right
   relative to `IGame`/`IWorld`'s existing lifecycle in `OpenCobra/GDK/Game/`), not technical difficulty.
   **Open** — resolve during implementation by following `GLState.cs`'s init/dispose shape as closely as
   sensible.

## Open Questions

- **Which OVL archive and symbol name holds the rain sound?** Not found in `rct3-importer` (no
  weather/rain-specific code there) or in any existing plan/research doc. Needs a scratch-scanner pass
  (per [`project_ovl_scratch_scanners`](../../research/) convention) over `RCT3_PATH` filtering
  `FileType.Sound` symbols, cross-checked by ear via `snd-viewer`'s rendered `<audio>` player in the Dumper
  UI, to identify the correct symbol (likely named something like `Rain`/`Ambient_Rain`, unconfirmed).
  Blocks writing the "Production OVLs with Entries" section this plan's decoder work is expected to fill in
  (per `ovl/README.md`'s Production OVLs Discovery convention) and blocks picking a concrete `LoadSound`
  call site.
- **Is there an existing or planned weather/ambience system to hook playback into**, or does this plan need
  to invent the minimal trigger point itself (e.g. directly in world-load code)? No weather system exists
  in the codebase today; scoping "loop once world loads" as the trigger (see Goals) avoids blocking on a
  system that doesn't exist yet, but a future weather system should take over start/stop/volume control
  rather than this plan's simple hook growing conditionals for it.
- **Multiple ambient loop layering / spatialization**: RCT3 likely plays rain as a flat 2D ambient loop, not
  a 3D-positioned OpenAL source — worth confirming (or just defaulting to non-positional, `AL_SOURCE_RELATIVE`)
  since building full 3D audio-source math is out of scope for a single looping ambience track.

## Deferred

- **General SFX playback** (ride/scenery one-shot sounds referenced by `sid` entries' sound fields, per
  `ovl-scenery-items.md`) — this plan only builds enough of `AudioBuffer`/`AudioSource`/`Sound.cs` to play
  one looping ambient track; a general SFX manager (pooling sources, distance attenuation, per-ride sound
  triggers) is real future work the `Audio/` subsystem's shape should not foreclose (e.g. `AudioBuffer`
  should not assume "the one rain buffer" as a singleton).
- **Weather-state-driven ambience** (fading rain in/out with actual weather simulation, switching between
  multiple ambient tracks) — no weather simulation exists yet; this plan's playback hook is a placeholder
  trigger point, not a weather system.
- **ADPCM decoding**, if the rain sound turns out not to be plain PCM — see Gaps and Risks #1. If deferred
  past this plan, `Sound.cs` should still decode the header/format-tag correctly and expose raw (still
  encoded) bytes, so playback is a follow-up rather than blocking the OVL-decoding half.

## Testing

- **`OpenCobra/Tests/OVL/SoundTests.cs`** (new, per `ovl/README.md`'s NUnit-first convention):
  - Synthetic `WAVEFORMATEX` header + PCM payload decodes to the expected `SoundEffect` fields (known-good).
  - `extraSize > 0` (non-PCM format tag) correctly offsets where audio data starts (edge case `snd-viewer`
    already handles — port the same offset math).
  - Resource shorter than 18 bytes throws/fails gracefully rather than reading out of bounds (failure case,
    mirrors `snd-viewer`'s own length guard).
  - A `RCT3_PATH`-gated real-archive check in `OpenCobra/Tests/Integration/` once the rain symbol is
    identified (see Open Questions): decodes without throwing and yields non-empty `AudioData`.
- **`OpenCobra/Tests/GDK/`** (existing folder, per `ovl/README.md`'s testing-approach reference to
  `OpenCobra/Tests/GDK`): cover `AudioBuffer` construction from a synthetic `SoundEffect` and
  `AudioSource.Looping` state, to the extent OpenAL objects are testable without a real audio device —
  device/context bring-up itself likely needs a manual/smoke check rather than a unit test, same as GL
  context creation isn't unit-tested today.

## Implementation Notes

<!-- Fill in during/after implementation. -->

## Status

Not started. This plan is green-field for both the `snd` OVL decoder and the GDK audio subsystem; no code
exists yet for either half. Blocked on identifying the rain sound's actual OVL symbol name/archive (see Open
Questions) before the "Production OVLs with Entries" section can be filled in and before playback wiring can
target a concrete call site.
