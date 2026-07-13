// Fonts
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
//
// Symbols tagged "fct" (FileType.FontCharacterTable) - e.g. GUIFontSmallNumbers, GUIFontSmall,
// GUIFontTiny in Main.ovl. Unlike CharacterSkins/ParticleEffects, these are NOT tex/flic/btbl
// shaped: an "fct" resource's bytes were found to decode as a Win32 LOGFONT-like structure
// containing readable ASCII font-family name strings ("Tahoma", "Verdana", "Arial", "Lucida", ...).
// The exact struct layout - field offsets, string encoding/length prefix, how multiple font entries
// are delimited - has not been reverse-engineered yet.
//
// This module deliberately does not attempt to decode that structure yet: guessing a wrong layout
// here would be worse than not decoding at all. It only enumerates "fct" resources and exposes
// their raw bytes, so a real decoder can be dropped in once the format is confirmed.
namespace OpenCobra.OVL.Files;

/// <summary>
/// A single "fct" (Font Character Table) resource, not yet decoded - see remarks on <see cref="Fonts"/>.
/// </summary>
public sealed record FontCharacterTableEntry(string Name, byte[] Data);

public static class Fonts {
  // Extract all font character table resources from an OVL, as raw (undecoded) bytes.
  public static IReadOnlyList<FontCharacterTableEntry> Extract(Ovl ovl) =>
    [.. ovl.Keys
      .Where(file => file.Type == FileType.FontCharacterTable)
      .Select(file => (file, data: ovl.ReadResource(file)))
      .Where(x => x.data != null)
      .Select(x => new FontCharacterTableEntry(x.file.ToString(), x.data!))];
}
