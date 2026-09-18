// ParticleEffects
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
//
// Decodes particle sprite textures tagged "psi" (FileType.ParticleSpriteItem) - e.g. the
// FWFlares02_* firework/particle sprite symbols in Particles/Particles.ovl. Like CharacterSkins,
// these reuse the exact same tex/flic/btbl loader shapes Textures.cs decodes (confirmed: the
// install's one Particles.ovl carries both a "psi"-tagged btbl and "psi"-tagged flic entries), but
// classifying by tag suffix alone loses which on-disk shape a given symbol actually has, so this
// module detects it per-symbol at decode time - see CharacterSkins.cs for the identical approach
// and the reasoning behind it.
//
// NOTE: an underlying mms/prt/psi/fct symbol-resolution bug is still open. This module exists so
// decoding "just works" once it is; it is not yet verified to produce real pixel data against the
// actual install.
using System.Collections.Concurrent;
using NLog;

namespace OpenCobra.OVL.Files;

public static class ParticleEffects {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  // Extract all particle sprite textures from an OVL
  public static TextureCollection Extract(Ovl ovl) {
    var filesData =
      from file in ovl.Keys.Where(file => file.Type == FileType.ParticleSpriteItem)
      let data = ovl.ReadResource(file)
      where data != null
      select new OvlData(ovl.Name, file, data);

    var allFiles = filesData as OvlData[] ?? [.. filesData];
    var bag = new ConcurrentBag<Texture>();
    var failures = new ConcurrentBag<OvlFile>();

    // Phase 1: pull out anything shaped like a bitmap table first, since sibling flic-shaped
    // symbols in the same archive may reference it by index (mirrors Textures.Extract/CharacterSkins).
    var bitmapTables = new ConcurrentDictionary<string, Texture[]>();
    var remaining = new ConcurrentBag<OvlData>();
    Parallel.ForEach(allFiles, fileData => {
      var name = fileData.File.ToString();
      try {
        var table = BitmapTables.Read(name, ovl, fileData.File, fileData.Data);
        bitmapTables[fileData.OvlName] = table;
        foreach (var texture in table) bag.Add(texture);
        return;
      } catch {
        // Not bitmap-table shaped (or its bitmap table data didn't resolve) - fall through and try
        // the other on-disk shapes below.
      }
      remaining.Add(fileData);
    });

    // Phase 2: everything left over - try the flic shape (a loader with its own extra data,
    // either a standalone flic or a 4-byte bitmap-table index), then fall back to the tex shape.
    Parallel.ForEach(remaining, fileData => {
      try {
        var name = fileData.File.ToString();
        var bitmapTable = bitmapTables.GetValueOrDefault(fileData.OvlName);

        if (ovl.TryReadExtraData(fileData.File, out var chunks) && chunks.Count > 0) {
          bag.Add(Flic.Read(name, chunks[0], bitmapTable));
          return;
        }

        if (!ovl.TryGetDataPointer(fileData.File, out var texAddress))
          throw new InvalidOperationException($"Failed to resolve data pointer for {name}");
        var texture = TextureDecoding.ReadTexture(name, ovl, texAddress, fileData.Data, null);
        if (texture != null) bag.Add(texture);
      } catch (Exception ex) {
        logger.Error(ex, "Failed to decode {FileName}", fileData.File.ToString());
        failures.Add(fileData.File);
      }
    });

    if (!failures.IsEmpty)
      logger.Error(
        "Failed to decode {count} particle sprite textures from {x} OVL{suffix}.",
        failures.Count,
        allFiles.Length,
        allFiles.Length != 1 ? "s" : string.Empty
      );

    return [.. bag];
  }
}
