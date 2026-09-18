// LoaderTypeIndexDiagnostic
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
//
// Fix plan Step 1 (.agents/plans/fix/ovl-texture-decoding.md): empirically checks bug doc Part 6
// Finding 1 against Main.common.ovl before ExtractResources is rewritten to rely on it. Reads
// Ovl's private allFileTypeBlocks/allLoaderHeaders fields via reflection rather than
// InternalsVisibleTo, since this is a read-only diagnostic, not production code.
//
// Result (recorded here since it changes the fix plan's Step 3 priority): LoaderStruct.LoaderType
// IS a direct, correct index into loaderHeaders by array *position* - no desync, no out-of-range
// values. But "tex" and "fct" (and "gsi"/"shs") never appear as a LoaderType value anywhere in
// Main.common.ovl's LoaderStruct[] array, despite 84 tex-tagged and 6 fct-tagged symbols existing
// (per name-suffix classification, which already works and is unaffected). Only loaders that carry
// an attached "extra data" chunk stream (ftx/btbl/flic/txt/snd) get a LoaderStruct entry at all;
// tex's own data is the inline 60-byte TextureStruct read directly from the symbol table, and its
// link to pixel data is the FlicPtr double-relocation chase onto a *flic*-tagged LoaderStruct entry
// (Part 6 Finding 2), not its own LoaderStruct entry. So for this repro target, Step 3's
// "read LoaderType/Sym for classification" fallback replacement is a no-op - tex/fct symbols in
// Main.common.ovl already classify correctly via the primary name-suffix path and never hit the
// fallback. Finding 1 may still be real for some other archive (e.g. one without name-suffix tags),
// but it is not what blocks Main.common.ovl's 0/84 decode failure; Findings 2 (FlicPtr chase) and 3
// (relocation table) are. Also ruled out: LoaderHeader.Type (parsed at OVL.cs:354, currently
// unused) is NOT a usable index either - it's a small collide-prone category value (seen: 1/2/3
// across 9 headers), not a per-header key.
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DotNetEnv;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;

namespace OpenCobra.Tests.Integration;

[TestFixture]
public class LoaderTypeIndexDiagnostic {
  private const int LoaderStructSize = 20; // LoaderType(4), data(4), HasExtraData(4), Sym(4), SymbolsToResolve(4)
  private static string? Rct3Path() => Environment.GetEnvironmentVariable("RCT3_PATH");

  [SetUp]
  public void Setup() {
    if (File.Exists(Constants.EnvFilePath))
      Env.NoClobber().Load(Constants.EnvFilePath);
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH", "Cannot find RCT3. Skipping integration test.")]
  public void MainCommonOvl_DirectLoaderTypeTally_HasNoTexOrFctEntries() {
    var rct3 = Rct3Path()!;
    var mainPath = Path.Combine(rct3, "Main.common.ovl");
    Assert.That(File.Exists(mainPath), Is.True, $"Main.common.ovl not found at: {mainPath}");

    using var ovl = Ovl.Load(mainPath);

    var (tags, directTally, outOfRangeCount, loaderCount) = TallyDirectLoaderTypes(ovl);
    var currentTally = TallyCurrentClassification(ovl);

    TestContext.Out.WriteLine("Direct-index LoaderType tally (LoaderStruct[].LoaderType -> loaderHeaders[i].Tag):");
    foreach (var (tag, count) in directTally.OrderByDescending(kv => kv.Value))
      TestContext.Out.WriteLine($"  {tag}: {count}");
    if (outOfRangeCount > 0)
      TestContext.Out.WriteLine($"  (out-of-range LoaderType, dropped: {outOfRangeCount})");
    TestContext.Out.WriteLine($"  known loader tags: {string.Join(", ", tags)}");

    TestContext.Out.WriteLine("Current ovl.Keys classification tally (name-suffix + positional SymbolCount walk):");
    foreach (var (tag, count) in currentTally.OrderByDescending(kv => kv.Value))
      TestContext.Out.WriteLine($"  {tag}: {count}");

    using (Assert.EnterMultipleScope()) {
      // Position-based LoaderType indexing is internally consistent for this file: every raw
      // LoaderType value that appears is in-range (see class doc for what this does and doesn't
      // confirm about Finding 1).
      Assert.That(outOfRangeCount, Is.EqualTo(0),
        "LoaderType values out of range - re-open bug doc Part 6 Finding 1 before proceeding");

      // The interesting (negative) result: tex/fct never appear as LoaderStruct.LoaderType values
      // in Main.common.ovl at all, despite 84/6 tex/fct-tagged symbols existing. If this ever
      // becomes nonzero, something about this file's loader-table shape changed and Step 3's
      // premise for Main.common.ovl should be re-examined.
      Assert.That(directTally.GetValueOrDefault("tex"), Is.EqualTo(0),
        "'tex' now appears in the LoaderStruct array - re-examine Step 3's scope for this repro");
      Assert.That(directTally.GetValueOrDefault("fct"), Is.EqualTo(0),
        "'fct' now appears in the LoaderStruct array - re-examine Step 3's scope for this repro");

      // Only loaders with an attached extra-data chunk stream get a LoaderStruct entry.
      Assert.That(directTally.Keys, Is.EquivalentTo(new[] { "txt", "snd", "flic", "btbl", "ftx" }));

      // Sanity: every LoaderStruct entry accounted for by the position-based tally.
      Assert.That(directTally.Values.Sum() + outOfRangeCount, Is.EqualTo(loaderCount));

      // Current (name-suffix-based) classification already reports the full 84/6 tex/fct counts -
      // confirms those symbols aren't blocked on classification at all for this file.
      Assert.That(currentTally.GetValueOrDefault("tex"), Is.EqualTo(84));
      Assert.That(currentTally.GetValueOrDefault("fct"), Is.EqualTo(6));
    }
  }

  private static (string[] Tags, Dictionary<string, int> Tally, int OutOfRangeCount, int LoaderCount) TallyDirectLoaderTypes(Ovl ovl) {
    var ovlType = typeof(Ovl);
    var nonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    var publicInstance = BindingFlags.Public | BindingFlags.Instance;

    var allFileTypeBlocks = (IEnumerable)ovlType.GetField("allFileTypeBlocks", nonPublicInstance)!.GetValue(ovl)!;
    var allLoaderHeaders = (IEnumerable)ovlType.GetField("allLoaderHeaders", nonPublicInstance)!.GetValue(ovl)!;

    // Main.common.ovl has no paired .unique.ovl, so the common file is the only (first) entry.
    var blocks = (Array)allFileTypeBlocks.Cast<object>().First();
    var loaderHeaders = (Array)allLoaderHeaders.Cast<object>().First();

    Assert.That(blocks.Length, Is.GreaterThan(2), "Expected at least 3 FileTypeBlocks");
    var block2 = blocks.GetValue(2)!;
    var block2Blocks = (IList)block2.GetType().GetField("Blocks", publicInstance)!.GetValue(block2)!;
    Assert.That(block2Blocks.Count, Is.GreaterThan(1), "Expected blocks[2].Blocks[1] (LoaderStruct array)");

    var loaderBlock = block2Blocks[1]!;
    var fileBlockType = loaderBlock.GetType();
    var data = (byte[]?)fileBlockType.GetField("Data", publicInstance)!.GetValue(loaderBlock);
    var size = (uint)fileBlockType.GetField("Size", publicInstance)!.GetValue(loaderBlock)!;
    Assert.That(data, Is.Not.Null, "LoaderStruct block has no data");

    var loaderHeaderType = loaderHeaders.GetType().GetElementType()!;
    var tagProp = loaderHeaderType.GetProperty("Tag", publicInstance)!;
    var typeProp = loaderHeaderType.GetProperty("Type", publicInstance)!;

    // Two candidate mappings from LoaderStruct.LoaderType -> tag: (a) direct array position (what
    // bug doc Part 6 Finding 1 assumes), (b) via each header's own on-disk Type field (currently
    // parsed but discarded at OVL.cs:354/361). Print both so a mismatch between them is visible.
    var tagsByPosition = new string[loaderHeaders.Length];
    var tagsByHeaderType = new Dictionary<uint, string>();
    TestContext.Out.WriteLine("loaderHeaders (position, Type field, Tag):");
    for (var i = 0; i < loaderHeaders.Length; i++) {
      var header = loaderHeaders.GetValue(i);
      var tag = (string)tagProp.GetValue(header)!;
      var headerType = (uint)typeProp.GetValue(header)!;
      tagsByPosition[i] = tag;
      tagsByHeaderType[headerType] = tag;
      TestContext.Out.WriteLine($"  [{i}] Type={headerType} Tag={tag}");
    }

    var loaderCount = (int)size / LoaderStructSize;
    var tally = new Dictionary<string, int>();
    var tallyByHeaderType = new Dictionary<string, int>();
    var outOfRangeCount = 0;
    for (var i = 0; i < loaderCount; i++) {
      var loaderType = BitConverter.ToUInt32(data!, i * LoaderStructSize);
      if (loaderType < tagsByPosition.Length) {
        var tag = tagsByPosition[loaderType];
        tally[tag] = tally.GetValueOrDefault(tag) + 1;
      } else {
        outOfRangeCount++;
      }

      if (tagsByHeaderType.TryGetValue(loaderType, out var tagViaType))
        tallyByHeaderType[tagViaType] = tallyByHeaderType.GetValueOrDefault(tagViaType) + 1;
    }

    TestContext.Out.WriteLine("Direct-index tally via header's own Type field (alternate mapping):");
    foreach (var (tag, count) in tallyByHeaderType.OrderByDescending(kv => kv.Value))
      TestContext.Out.WriteLine($"  {tag}: {count}");

    return (tagsByPosition, tally, outOfRangeCount, loaderCount);
  }

  private static Dictionary<string, int> TallyCurrentClassification(Ovl ovl) {
    var tally = new Dictionary<string, int>();
    foreach (var tag in ovl.Keys.Select(key => key.Type.ToTagString())) {
      tally[tag] = tally.GetValueOrDefault(tag) + 1;
    }
    return tally;
  }
}
