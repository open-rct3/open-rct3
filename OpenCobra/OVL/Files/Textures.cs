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

    // Read bitmap tables in parallel
    var bitmapTables = new Dictionary<string, Texture[]>();
    Parallel.ForEach(bitmapTableData, fileData => {
      try {
        var name = fileData.File.ToString();

        var table = TextureDecoding.ReadBitmapTable(name, ovl, fileData.File, fileData.Data);
        bitmapTables[fileData.OvlName] = table;
        foreach (var texture in table) bag.Add(texture);
      } catch (Exception ex) {
        logger.Error(ex, "Failed to decode {FileName}", fileData.File.ToString());
        failures.Add(fileData.File);
      }
    });

    // Read other textures in parallel
    Parallel.ForEach(otherTextureData, fileData => {
      try {
        var name = fileData.File.ToString();
        var bitmapTable = bitmapTables.GetValueOrDefault(fileData.OvlName);

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (fileData.File.Type == FileType.Texture) {
          var texture = TextureDecoding.ReadTexture(name, ovl, fileData.Data, bitmapTable);
          if (texture != null) bag.Add(texture);
        } else if (fileData.File.Type == FileType.Flic) {
          if (!ovl.TryReadExtraData(fileData.File, out var chunks) || chunks.Count == 0)
            throw new InvalidOperationException($"Failed to resolve flic data for {name}");
          bag.Add(TextureDecoding.ReadFlic(name, chunks[0], bitmapTable));
        }
      } catch (Exception ex) {
        logger.Error(ex, "Failed to decode {FileName}", fileData.File.ToString());
        failures.Add(fileData.File);
      }
    });

    if (!failures.IsEmpty)
      logger.Error(
        "Failed to decode {count} textures from {x} OVL{suffix}.",
        failures.Count,
        textureFiles.Length,
        textureFiles.Length != 1 ? "s" : string.Empty
      );

    return [.. bag];
  }
}
