// RelocationDebug - scratch diagnostic, not part of the fix plan; delete once Steps 2+4 are verified.
using System.Collections.Generic;
using System.Reflection;
using DotNetEnv;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;

namespace OpenCobra.Tests.Integration;

[TestFixture]
public class RelocationDebug {
  private static string? Rct3Path() => Environment.GetEnvironmentVariable("RCT3_PATH");

  [SetUp]
  public void Setup() {
    if (File.Exists(Constants.EnvFilePath))
      Env.NoClobber().Load(Constants.EnvFilePath);
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH", "Cannot find RCT3. Skipping integration test.")]
  public void Debug_Main() {
    var rct3 = Rct3Path()!;
    var mainPath = Path.Combine(rct3, "Main.common.ovl");
    using var ovl = Ovl.Load(mainPath);

    var ovlType = typeof(Ovl);
    var nonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    var relocations = (System.Collections.IDictionary)ovlType.GetField("relocations", nonPublicInstance)!.GetValue(ovl)!;
    TestContext.Out.WriteLine($"relocations count: {relocations.Count}");

    var texEntry = ovl.Keys.First(k => k.Type == FileType.Texture);
    Assert.That(ovl.TryGetDataPointer(texEntry, out var texAddress), Is.True);
    TestContext.Out.WriteLine($"tex entry: {texEntry.Name}, texAddress: {texAddress} (0x{texAddress:X})");

    var bytes = ovl.ReadResource(texEntry);
    TestContext.Out.WriteLine($"resource bytes length: {bytes?.Length}");
    if (bytes != null && bytes.Length >= 56) {
      var flicPtrRaw = BitConverter.ToUInt32(bytes, 52);
      TestContext.Out.WriteLine($"raw FlicPtr (offset 52 in resource bytes): {flicPtrRaw} (0x{flicPtrRaw:X})");
    }

    var fieldAddr = texAddress + 52;
    var gated = ovl.TryGetRelocationSource(fieldAddr, out var flicSlot);
    TestContext.Out.WriteLine($"TryGetRelocationSource(texAddress+52={fieldAddr}): {gated}, flicSlot={flicSlot}");

    if (gated) {
      var gated2 = ovl.TryGetRelocationSource(flicSlot, out var flicAddr);
      TestContext.Out.WriteLine($"TryGetRelocationSource(flicSlot={flicSlot}): {gated2}, flicAddr={flicAddr}");

      if (gated2) {
        var found = ovl.TryReadExtraData(flicAddr, out var chunks);
        TestContext.Out.WriteLine($"TryReadExtraData(flicAddr={flicAddr}): {found}, chunkCount={chunks?.Count}");

        // Build the real loader-order btbl/flic index (mirrors Textures.Extract) and check whether
        // flicAddr resolves through it.
        var ovlType2 = typeof(Ovl);
        var loaderEntriesProp = ovlType2.GetProperty("LoaderEntriesInOrder", nonPublicInstance)!;
        var loaderEntries = (System.Collections.IEnumerable)loaderEntriesProp.GetValue(ovl)!;
        var btblCount = 0;
        var flicCount = 0;
        var flicAddresses = new List<uint>();
        foreach (var entry in loaderEntries) {
          var entryType = entry.GetType();
          var tag = (string)entryType.GetField("Item1")!.GetValue(entry)!;
          var addr = (uint)entryType.GetField("Item2")!.GetValue(entry)!;
          if (tag == "btbl") btblCount++;
          if (tag == "flic") { flicCount++; flicAddresses.Add(addr); }
        }
        TestContext.Out.WriteLine($"loader-order: {btblCount} btbl, {flicCount} flic; flicAddr={flicAddr} in flic list: {flicAddresses.Contains(flicAddr)}");
        TestContext.Out.WriteLine($"sample flic addresses: {string.Join(", ", flicAddresses.Take(10))}");

        // Build the real btbl/flic loader-order index (mirrors Textures.Extract exactly).
        var textureDecodingType = ovl.GetType().Assembly.GetType("OpenCobra.OVL.Files.TextureDecoding")!;
        var readBitmapTableAtMethod = textureDecodingType.GetMethod("ReadBitmapTableAt", BindingFlags.NonPublic | BindingFlags.Static)!;
        object? currentTable = null;
        var bitmapTablesByFlicAddress = new Dictionary<uint, object>();
        foreach (var entry in loaderEntries) {
          var entryType = entry.GetType();
          var tag = (string)entryType.GetField("Item1")!.GetValue(entry)!;
          var addr = (uint)entryType.GetField("Item2")!.GetValue(entry)!;
          if (tag == "btbl") {
            try {
              currentTable = readBitmapTableAtMethod.Invoke(null, [$"btbl@{addr:X}", ovl, addr]);
              var arr = (Array)currentTable!;
              TestContext.Out.WriteLine($"btbl@{addr:X} decoded {arr.Length} entries");
            } catch (Exception ex) {
              TestContext.Out.WriteLine($"btbl@{addr:X} FAILED: {ex.InnerException?.Message ?? ex.Message}");
              currentTable = null;
            }
          } else if (tag == "flic" && currentTable != null) {
            bitmapTablesByFlicAddress[addr] = currentTable;
          }
        }
        TestContext.Out.WriteLine($"bitmapTablesByFlicAddress has {bitmapTablesByFlicAddress.Count} entries; contains flicAddr={flicAddr}: {bitmapTablesByFlicAddress.ContainsKey(flicAddr)}");

        var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(uint), textureDecodingType.Assembly.GetType("OpenCobra.OVL.Files.Texture")!.MakeArrayType());
        var typedDict = Activator.CreateInstance(dictType)!;
        var addMethod = dictType.GetMethod("Add")!;
        foreach (var kv in bitmapTablesByFlicAddress)
          addMethod.Invoke(typedDict, [kv.Key, kv.Value]);

        try {
          var readTextureMethod = textureDecodingType.GetMethod("ReadTexture", BindingFlags.Public | BindingFlags.Static)!;
          var texture = readTextureMethod.Invoke(null, [texEntry.Name, ovl, texAddress, (ReadOnlyMemory<byte>)bytes!, typedDict]);
          TestContext.Out.WriteLine($"ReadTexture result: {(texture == null ? "null" : texture.ToString())}");
        } catch (Exception ex) {
          TestContext.Out.WriteLine($"ReadTexture threw: {ex.InnerException?.Message ?? ex.Message}");
        }
      }
    }

    // Check a btbl symbol specifically: does its symbol-table dataPtr (what TryReadExtraData(file,)
    // looks up via entryDataPtrs) match a key in the now-relocation-resolved allExtraData dict?
    var btblEntry = ovl.Keys.FirstOrDefault(k => k.Type == FileType.BitmapTable);
    if (btblEntry != null) {
      Assert.That(ovl.TryGetDataPointer(btblEntry, out var btblDataPtr), Is.True);
      TestContext.Out.WriteLine($"btbl entry: {btblEntry.Name}, symbol dataPtr: {btblDataPtr}");
      var btblFound = ovl.TryReadExtraData(btblEntry, out var btblChunks);
      TestContext.Out.WriteLine($"TryReadExtraData(btbl file): {btblFound}, chunkCount={btblChunks?.Count}");
    } else {
      TestContext.Out.WriteLine("No BitmapTable-typed symbol found via ovl.Keys");
    }

    // Compare against every flic-tagged loader entry's raw `data` field (what ReadLoaderExtraData
    // keys allExtraData by) to see whether flicAddr matches any of them, directly or off by a
    // small amount.
    var allExtraDataField = ovlType.GetField("allExtraData", nonPublicInstance)!;
    var allExtraData = (System.Collections.IEnumerable)allExtraDataField.GetValue(ovl)!;
    foreach (var extraDataObj in allExtraData) {
      var dict = (System.Collections.IDictionary)extraDataObj;
      var keys = dict.Keys.Cast<uint>().OrderBy(k => k).ToList();
      TestContext.Out.WriteLine($"allExtraData dict has {keys.Count} keys; sample: {string.Join(", ", keys.Take(10))}");
      var closest = keys.OrderBy(k => Math.Abs((long)k - (long)flicSlot)).FirstOrDefault();
      TestContext.Out.WriteLine($"closest key to flicSlot({flicSlot}): {closest} (diff {(long)closest - (long)flicSlot})");
    }

    // Print a sample of relocation source addresses to see what range they're in.
    var sample = relocations.Keys.Cast<uint>().OrderBy(k => k).Take(10).ToList();
    TestContext.Out.WriteLine($"sample relocation source addresses: {string.Join(", ", sample)}");
    var allAddrs = relocations.Keys.Cast<uint>().ToList();
    var maxAddr = allAddrs.Count > 0 ? allAddrs.Max() : 0;
    var minAddr = allAddrs.Count > 0 ? allAddrs.Min() : 0;
    TestContext.Out.WriteLine($"relocation address range: {minAddr} .. {maxAddr}");
  }
}
