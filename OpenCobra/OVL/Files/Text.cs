// Text
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Concurrent;
using System.Text;
using NLog;

namespace OpenCobra.OVL.Files;

/// <summary>Decodes "txt" (Text) entries - see <see cref="ReadText"/> for the on-disk layout.</summary>
public static class Text {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  /// <summary>Extracts every <c>txt</c> symbol in the archive, keyed by symbol name.</summary>
  public static IReadOnlyDictionary<string, string> Extract(Ovl ovl) {
    var txtFiles = ovl.Keys.Where(file => file.Type == FileType.Text).ToList();
    var bag = new ConcurrentDictionary<string, string>();
    var failures = new ConcurrentBag<OvlFile>();

    Parallel.ForEach(txtFiles, file => {
      var value = TryExtractOne(ovl, file);
      if (value == null) {
        failures.Add(file);
        return;
      }
      bag[file.Name] = value;
    });

    if (!failures.IsEmpty)
      logger.Error(
        "Failed to decode {count} text entries from {x} OVL{suffix}.",
        failures.Count,
        txtFiles.Count,
        txtFiles.Count != 1 ? "s" : string.Empty
      );

    return bag;
  }

  /// <summary>
  /// Decodes a single <c>txt</c>-tagged symbol without scanning the rest of the archive - for
  /// callers (e.g. <see cref="SceneryItems"/> resolving <c>Listing.Name</c>) that need one string on
  /// demand rather than every string up front. Returns <c>null</c> instead of throwing on decode
  /// failure, mirroring <see cref="StaticShapes.TryExtractOne"/>'s per-symbol failure handling.
  /// </summary>
  public static string? TryExtractOne(Ovl ovl, OvlFile file) {
    try {
      return ReadText(ovl, file);
    } catch (Exception ex) {
      logger.Error(ex, "Failed to decode {FileName}", file.ToString());
      return null;
    }
  }

  /// <summary>
  /// Reads a <c>txt</c> symbol's raw content: per rct3-importer's ManagerTXT.cpp, a bare
  /// null-terminated UTF-16LE string stored directly at the resolved data pointer, with no
  /// header struct of its own.
  /// </summary>
  private static string ReadText(Ovl ovl, OvlFile file) {
    if (!ovl.TryGetDataPointer(file, out var address))
      throw new InvalidOperationException($"Failed to resolve data pointer for {file.Name}");
    if (!ovl.TryResolveRelocation(address, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve Text block for {file.Name}");

    var o = Convert.ToInt32(offset);
    var end = o;
    while (end + 1 < block.Length && (block[end] != 0 || block[end + 1] != 0)) end += 2;

    return Encoding.Unicode.GetString(block, o, end - o);
  }
}
