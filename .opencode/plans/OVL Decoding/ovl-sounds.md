# Plan: Decode Sound (SND) Entries

## Problem

SND entries store uncompressed PCM audio data with format metadata. Each sound has a WAVEFORMATEX header, two optional audio channels (int16 samples), and playback parameters. The dumper should display sound metadata and optionally play or export the audio.

## Background Research

**SND Manager** (`ManagerSND.h/cpp`):
- Tag: `"snd"`, Loader: `"FGDK"`, Name: `"Sound"`
- Each sound = `Sound` struct with:
  - `format` — `WAVEFORMATEX` (standard Windows audio format header)
  - `unk6` through `unk15` — playback parameters (floats, mostly constants)
  - `loop` — 0 = one-shot, 1 = looping
  - `channel1_size`, `channel2_size` — byte sizes of audio data
  - `channel1[]` — int16 samples (relocated)
  - `channel2[]` — int16 samples (relocated, optional for stereo)
- Default format: 22050 Hz, 16-bit, mono, PCM (wFormatTag=1)
- All entries stored in common OVL only

**WAVEFORMATEX Structure** (18 bytes):
```
wFormatTag: uint16      // 1 = PCM
nChannels: uint16       // 1 = mono, 2 = stereo
nSamplesPerSec: uint32  // sample rate (default 22050)
nAvgBytesPerSec: uint32 // bytes per second (default 44100)
nBlockAlign: uint16     // block alignment (default 2)
wBitsPerSample: uint16  // bits per sample (default 16)
cbSize: uint16          // extra format info (default 0)
```

**Data Layout**:
- Common blob per sound: `Sound` struct → channel1 data → channel2 data
- Channel data is int16 (signed 16-bit PCM samples)
- Relocated pointers to channel arrays

## Solution Architecture

### New File: `OpenCobra/OVL/Files/Sounds.cs`

```csharp
public record SoundFormat {
  ushort FormatTag;       // 1 = PCM
  ushort Channels;        // 1 = mono, 2 = stereo
  uint SampleRate;        // e.g. 22050
  uint AvgBytesPerSec;
  ushort BlockAlign;
  ushort BitsPerSample;   // e.g. 16
  ushort ExtraSize;
}

public record SoundEntry {
  string Name;
  SoundFormat Format;
  float Duration => Channel1.Length > 0
    ? (float)Channel1.Length / (Format.SampleRate * Format.Channels * Format.BitsPerSample / 8)
    : 0;
  bool IsLooping;
  short[] Channel1;       // PCM samples
  short[]? Channel2;      // optional second channel
  // Playback parameters
  float Unk7, Unk8, Unk9, Unk10, Unk11, Unk12, Unk13, Unk14, Unk15;
}

public static class Sounds {
  public static IReadOnlyList<SoundEntry> Extract(Ovl ovl);
}
```

### Implementation Steps

1. Find loaders where `Tag == "snd"`
2. Parse `Sound` struct from loader data
3. Read `WAVEFORMATEX` format header
4. Read channel1 data (int16 array) from relocated pointer
5. Read channel2 data (int16 array) from relocated pointer (if channel2_size > 0)
6. Calculate duration from format and sample count
7. Return list of `SoundEntry`

### Files to Create/Modify

**Create:**
- `OpenCobra/OVL/Files/Sounds.cs`

### Dependencies

- No external audio libraries needed for parsing
- Optional: NAudio or similar for playback/export (future work)

### Regression Prevention

- No changes to `Ovl.cs`
- New test file: `OpenCobra/Tests/TestRunner/Tests/ReadSounds.cs`
- Run TestRunner before/after implementation

### Testing Strategy (TestRunner)

Create new file `OpenCobra/Tests/TestRunner/Tests/ReadSounds.cs`:

```csharp
using System;
using System.Linq;
using OVL;

namespace OvlTestBench.Tests;

public static class ReadSounds {
  public static readonly OvlTest[] All = [
    new("SoundEntriesDecoded", pair => {
      foreach (var file in pair.Files) {
        try {
          using var stream = System.IO.File.OpenRead(file.Path);
          var ovl = Ovl.Read(stream, file.Path);
          var sounds = Sounds.Extract(ovl);
          if (ovl.LoaderEntries.Any(e => e.Tag == "snd") && sounds.Count == 0) {
            Assert.That(false, $"{System.IO.Path.GetFileName(file.Path)}: expected sounds but got none");
          }
          foreach (var sound in sounds) {
            Assert.That(sound.Format.SampleRate > 0, $"{System.IO.Path.GetFileName(file.Path)}: sound '{sound.Name}' has invalid sample rate");
          }
        } catch (Exception ex) {
          Assert.That(false, $"{System.IO.Path.GetFileName(file.Path)}: {ex.Message}");
        }
      }
    }),
  ];
}
```

Add to `LoadOvls.All` array or create as separate test file following the existing pattern.

### Success Criteria

- All SND entries extracted with format metadata and PCM data
- Duration calculated correctly
- Looping flag parsed
- Zero regressions

## Production OVLs with Entries

> **Status**: Not yet identified

Production OVL archives containing sound entries (tag: `"snd"`) have not yet been catalogued. To identify:
1. Scan production OVLs for loader entries with `Tag == "snd"`
2. Document common vs unique archive distribution
3. Note sample symbol names for verification

**Known test files**: `style.common.ovl`, `style.unique.ovl` (no SND entries present)

## Post-Implementation Steps

When this decoder is implemented:

1. **Create results file**: Add `.opencode/results/ovl-sounds.md` with implementation summary
2. **Update README**: Change Status to `Done` in the Plans table and Summary Table
3. **Update this plan**: Change status in "Production OVLs with Entries" section

### Future Work

- WAV file export
- Audio playback in dumper
- Support for compressed audio formats (if any exist)
