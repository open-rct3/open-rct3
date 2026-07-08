// CharacterSkins
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
//
// Decodes character skin/body-part textures tagged "mms" (FileType.CharacterSkinSet) and "prt"
// (FileType.CharacterSkinPart) - e.g. the SkinBody_*/Body_*/OptionalBody_* symbols in
// Characters/*/*_Main.ovl. On disk these reuse the exact same tex/flic/btbl loader shapes
// Textures.cs decodes (confirmed via an ovl.Keys dump taken before "mms"/"prt" were added to
// FileTypeExtensions.ToFileType, which showed the very same symbols individually typed as
// Texture/Flic/BitmapTable). Classifying them by tag suffix instead means the on-disk *shape*
// is no longer visible on OvlFile.Type - every "mms"-tagged symbol in an archive now lands in the
// one CharacterSkinSet bucket regardless of whether it's tex-, flic-, or btbl-shaped. This module
// detects the shape per-symbol at decode time instead (see DecodeSymbol below), reusing the exact
// same TextureDecoding routines Textures.cs uses.
//
// NOTE: the underlying mms/prt symbol-resolution bug documented in
// .agents/bugs/ovl-texture-decoding.md is still open - real archives have been observed resolving
// a claimed 192-entry bitmap table to a 40-byte block. Expect most real archives to still fail to
// decode until that is fixed; this module exists so decoding "just works" once it is.
using System.Collections.Concurrent;
using NLog;

namespace OpenCobra.OVL.Files;

public static class CharacterSkins {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  private static readonly FileType[] SkinTypes = [FileType.CharacterSkinSet, FileType.CharacterSkinPart];

  // Extract all character skin textures from an OVL
  public static TextureCollection Extract(Ovl ovl) {
    var filesData =
      from type in SkinTypes
      from file in ovl.Keys.Where(file => file.Type == type)
      let data = ovl.ReadResource(file)
      where data != null
      select new OvlData(ovl.Name, file, data);

    var allFiles = filesData as OvlData[] ?? [.. filesData];
    var bag = new ConcurrentBag<Texture>();
    var failures = new ConcurrentBag<OvlFile>();

    // Phase 1: pull out anything shaped like a bitmap table first, since sibling flic-shaped
    // symbols in the same archive may reference it by index (mirrors Textures.Extract's two-pass
    // approach). Two archives commonly carry *both* an "mms" and a "prt" bitmap table side by
    // side (e.g. SkinBody_AF01_L2:mms and SkinBody_AF01_L2:prt) - keying by (OvlName, FileType)
    // rather than just OvlName keeps them from clobbering each other.
    var bitmapTables = new ConcurrentDictionary<(string OvlName, FileType Type), Texture[]>();
    var remaining = new ConcurrentBag<OvlData>();
    Parallel.ForEach(allFiles, fileData => {
      var name = fileData.File.ToString();
      try {
        var table = TextureDecoding.ReadBitmapTable(name, ovl, fileData.File, fileData.Data);
        bitmapTables[(fileData.OvlName, fileData.File.Type)] = table;
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
        var bitmapTable = bitmapTables.GetValueOrDefault((fileData.OvlName, fileData.File.Type));

        if (ovl.TryReadExtraData(fileData.File, out var chunks) && chunks.Count > 0) {
          bag.Add(TextureDecoding.ReadFlic(name, chunks[0], bitmapTable));
          return;
        }

        var texture = TextureDecoding.ReadTexture(name, ovl, fileData.Data, bitmapTable);
        if (texture != null) bag.Add(texture);
      } catch (Exception ex) {
        logger.Error(ex, "Failed to decode {FileName}", fileData.File.ToString());
        failures.Add(fileData.File);
      }
    });

    if (!failures.IsEmpty)
      logger.Error(
        "Failed to decode {count} character skin textures from {x} OVL{suffix}.",
        failures.Count,
        allFiles.Length,
        allFiles.Length != 1 ? "s" : string.Empty
      );

    return [.. bag];
  }
}
