# Simplifying OVL API

## Flat: OVL data offset & size for each resource

```cs
public record OvlFile(string Name, FileType Type) {
  public override string ToString() => $"{Name}.{Type}";
  public override int GetHashCode() => HashCode.Combine(Name, Type);
}

public record OvlEntry(long Offset, uint Size);

public class Ovl {
  public static Dictionary<OvlFile, OvlEntry> Load(string ovlPath) {
    var basePath = Path.GetDirectoryName(ovlPath) ?? "";
    var fileName = Path.GetFileNameWithoutExtension(ovlPath).Split('.')[0];

    // Process OVL pairs
    // For example, if common and unique files exist, load them both, otherwise load the single file
    var commonPath = Path.Combine(basePath, $"{fileName}.common.ovl");
    var uniquePath = Path.Combine(basePath, $"{fileName}.unique.ovl");

    // ...
  }
}
```

## Invariants

```cs
Debug.Assert(magic == 0x4b524746 /* FGRK */, "Invalid OVL magic");
```

## References

- [rct3-importer](https://github.com/chances/rct3-importer) — RCT3 custom scenery importer, original libOVL
- [libOVLDump source](https://github.com/chances/rct3-importer/tree/main/RCT3%20Importer/src/libOVLDump) — Archive
  reading and dumping implementation
