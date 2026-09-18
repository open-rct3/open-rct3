// Textures
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Concurrent;
using NLog;

namespace OpenCobra.OVL.Files;

public static class Textures {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  public static bool IsDecodable(TextureFormat _) => false;

  // Extract all textures from an OVL
  public static TextureCollection Extract(Ovl ovl) {
    // Find all tex, flic, and btbl files and read their data, in parallel
    var textureTypes = new[] { FileType.Texture, FileType.Flic, FileType.BitmapTable };
    // QUESTION: What will disk resource contention look like here?
    var textureFilesData =
      from type in textureTypes
      from file in ovl.Keys.Where(file => file.Type == type).AsParallel()
        .WithDegreeOfParallelism(Environment.ProcessorCount)
        .WithMergeOptions(ParallelMergeOptions.NotBuffered)
      let data = ovl.ReadResource(file)
      where data != null
      select new OvlData(ovl.Name, file, data);

    // Decode texture data in parallel
    var bag = new ConcurrentBag<Texture>();
    var failures = new ConcurrentBag<OvlFile>();

    // Read bitmap tables FIRST, because other flics in the archive may depend on it
    // If an archive contains a bitmap table, we cannot collect texture references until it is read
    var textureFiles = textureFilesData as OvlData[] ?? [.. textureFilesData];
    var bitmapTableData = textureFiles.Where(fileData => fileData.File.Type == FileType.BitmapTable);
    var otherTextureData = textureFiles.Where(fileData => fileData.File.Type != FileType.BitmapTable);

    // Read bitmap tables first
    // Association state is order-sensitive, so tables are decoded before the parallel texture pass.
    var bitmapTables = new Dictionary<string, Texture[]>(StringComparer.OrdinalIgnoreCase);
    var rawBitmapTables = new List<Texture[]>();
    foreach (var fileData in bitmapTableData) {
      try {
        var name = fileData.File.ToString();

        var table = TextureDecoding.ReadBitmapTable(name, ovl, fileData.File, fileData.Data);
        bitmapTables[fileData.File.Path] = table;
        rawBitmapTables.Add(table);
      } catch (Exception ex) {
        logger.Error(ex, "Failed to decode {FileName}", fileData.File.ToString());
        failures.Add(fileData.File);
      }
    }

    // Walk every loader instance in on-disk order (Part 6 Finding 4): "btbl"/"flic" are
    // loader-category tags, not classified symbols (Finding 5), so they're invisible to ovl.Keys -
    // this is the only way to discover and decode them. Each "btbl" loader becomes "current" until
    // the next one; each "flic" loader's single extra-data chunk is a 4-byte index into whichever
    // table was current when it was encountered.
    Texture[]? currentTable = null;
    string? currentSourcePath = null;
    var bitmapTablesByFlicAddress = new Dictionary<uint, Texture[]>();
    foreach (var entry in ovl.LoaderEntriesInOrder) {
      if (!string.Equals(currentSourcePath, entry.SourcePath, StringComparison.OrdinalIgnoreCase)) {
        currentSourcePath = entry.SourcePath;
        currentTable = null;
      }

      switch (entry.Tag) {
        case "btbl":
          try {
            currentTable = TextureDecoding.ReadBitmapTableAt(
              $"{ovl.Name}:btbl@{entry.DataAddress:X}", ovl, entry.DataAddress);
            rawBitmapTables.Add(currentTable);
          } catch (Exception ex) {
            logger.Error(
              ex, "Failed to decode bitmap table at {Address:X} in {OvlName}",
              entry.DataAddress, ovl.Name);
            currentTable = null;
          }
          break;
        case "flic" when currentTable != null:
          bitmapTablesByFlicAddress[entry.DataAddress] = currentTable;
          break;
      }
    }

    // Read other textures in parallel
    try {
      Parallel.ForEach(otherTextureData, fileData => {
        try {
          var name = fileData.File.ToString();

          // ReSharper disable once ConvertIfStatementToSwitchStatement
          if (fileData.File.Type == FileType.Texture) {
            if (!ovl.TryGetDataPointer(fileData.File, out var texAddress))
              throw new InvalidOperationException($"Failed to resolve data pointer for {name}");
            var texture = TextureDecoding.ReadTexture(
              name, ovl, texAddress, fileData.Data, bitmapTablesByFlicAddress);
            if (texture != null) bag.Add(texture);
          } else if (fileData.File.Type == FileType.Flic) {
            if (!ovl.TryGetDataPointer(fileData.File, out var flicAddress))
              throw new InvalidOperationException($"Failed to resolve data pointer for {name}");
            var bitmapTable = bitmapTablesByFlicAddress.GetValueOrDefault(flicAddress)
                              ?? bitmapTables.GetValueOrDefault(fileData.File.Path);
            if (!ovl.TryReadExtraData(fileData.File, out var chunks) || chunks.Count == 0)
              throw new InvalidOperationException($"Failed to resolve flic data for {name}");
            bag.Add(TextureDecoding.ReadFlic(name, chunks[0], bitmapTable));
          }
        } catch (Exception ex) {
          logger.Error(ex, "Failed to decode {FileName}", fileData.File.ToString());
          failures.Add(fileData.File);
        }
      });
    } finally {
      foreach (var table in rawBitmapTables)
      foreach (var texture in table)
        texture.Dispose();
    }

    foreach (var file in ovl.Keys.Where(file => file.Type == FileType.FlexibleTexture)) {
      try {
        var frames = FlexiTextureList.Load(ovl, file);
        foreach (var frame in frames.Frames) bag.Add(frame);
      } catch (Exception ex) {
        logger.Error(ex, "Failed to decode {FileName}", file.ToString());
        failures.Add(file);
      }
    }

    if (!failures.IsEmpty)
      logger.Error(
        "Failed to decode {count} textures from {x} OVL{suffix}.",
        failures.Count,
        textureFiles.Length,
        textureFiles.Length != 1 ? "s" : string.Empty
      );

    return new TextureCollection().AddRange(bag);
  }
}
