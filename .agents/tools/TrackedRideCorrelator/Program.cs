using System.Text;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;

// Correlates every named tracked ride (a `trr` resource) under the production `tracks/` tree with
// the numbered TrackN / TrackBasedN archive that actually defines the `tks` track-segment symbols
// it references (via each archive's SymbolRefStruct table, exposed as Ovl.SymbolReferences).
//
// Writes:
//   .agents/summaries/ovl-trr-scan.csv   one row per tracked-ride OVL
//   .agents/summaries/track-rides.csv    inverted: one row per segment archive, with addon
//
// File paths in the CSVs use the literal token ${RCT3_PATH} for anything under the RCT3 install.

var repoRoot = Directory.GetCurrentDirectory();

while (!Directory.Exists(Path.Combine(repoRoot, "OpenCobra")) && repoRoot.Length > 3)
  repoRoot = Path.GetDirectoryName(repoRoot)!;

var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH");
var fixtureDir = Path.Combine(repoRoot, "OpenCobra", "Tests", "Fixtures", "OVL");

// Portable path for a CSV: ${RCT3_PATH}/... under the RCT3 install, else repo-relative.
string Rel(string fullPath) {
  var full = Path.GetFullPath(fullPath);
  if (!string.IsNullOrEmpty(rct3Path)) {
    var root = Path.GetFullPath(rct3Path);
    if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
      return "${RCT3_PATH}/" + full[root.Length..].TrimStart('/', '\\').Replace('\\', '/');
  }
  return Path.GetRelativePath(repoRoot, full).Replace('\\', '/');
}

var ovlFiles = new List<string>();
if (!string.IsNullOrEmpty(rct3Path) && Directory.Exists(Path.Combine(rct3Path, "tracks")))
  ovlFiles.AddRange(Directory.GetFiles(Path.Combine(rct3Path, "tracks"), "*.ovl", SearchOption.AllDirectories));
if (Directory.Exists(fixtureDir))
  ovlFiles.AddRange(Directory.GetFiles(fixtureDir, "*.ovl", SearchOption.AllDirectories));

if (ovlFiles.Count == 0) {
  Console.Error.WriteLine("No OVLs found. Set RCT3_PATH or add fixtures.");
  return 2;
}

Console.WriteLine($"Loading {ovlFiles.Count} OVLs...\n");

// Decode every OVL in parallel (PLINQ, one archive per core), then collapse each archive's
// .common / .unique halves - which share a stem (path minus the trailing suffix) - into one
// merged ArchiveInfo. Loading is CPU-bound and archive-independent, so this is embarrassingly
// parallel; the whole pass runs off the calling thread via Task.Run.
var stems = ovlFiles.ToDictionary(f => f, f => StripOvlSuffix(Rel(f)));

var archives = (await Task.Run(() => ovlFiles
    .AsParallel()
    .WithDegreeOfParallelism(Environment.ProcessorCount)
    .Select(file => LoadArchive(file, stems[file]))
    .Where(a => a is not null)
    .Select(a => a!)
    .GroupBy(a => a.Stem, StringComparer.OrdinalIgnoreCase)
    .Select(MergeArchives)
    .ToList()))
  .ToDictionary(a => a.Stem, StringComparer.OrdinalIgnoreCase);

var segmentArchives = archives.Values.Where(a => a.DefinedTks.Count > 0).ToList();
var rides = archives.Values.Where(a => a.TrrNames.Count > 0 && a.RefTks.Count > 0)
  .OrderBy(a => a.Stem, StringComparer.OrdinalIgnoreCase).ToList();

Console.WriteLine($"Archives (tks data): {segmentArchives.Count}");
Console.WriteLine($"Tracked rides (trr + tks refs): {rides.Count}\n");

// ride stem -> ordered list of (archive stem, matched count)
var archiveToRides = segmentArchives.ToDictionary(a => a.Stem, _ => new List<string>());

var rows = new List<string[]> { trrColumns };

foreach (var ride in rides) {
  // Rank every segment archive by how many of this ride's referenced tks it defines. The primary
  // archive is the single best cover; a ride is assigned to exactly one archive.
  var ranked = segmentArchives
    .Select(a => (a.Stem, Matched: ride.RefTks.Count(a.DefinedTks.Contains)))
    .Where(x => x.Matched > 0)
    .OrderByDescending(x => x.Matched)
    .ThenBy(x => x.Stem, StringComparer.OrdinalIgnoreCase)
    .ToList();

  var primary = ranked.Count > 0 ? ranked[0].Stem : "";
  var primaryMatched = ranked.Count > 0 ? ranked[0].Matched : 0;
  var primaryCov = ride.RefTks.Count == 0 ? 0.0 : 100.0 * primaryMatched / ride.RefTks.Count;

  if (primary.Length > 0) archiveToRides[primary].Add(ride.Stem);

  rows.Add([
    ride.Stem,
    string.Join("|", ride.TrrNames.OrderBy(n => n)),
    ride.RefTks.Count.ToString(),
    ShortName(primary),
    primaryMatched.ToString(),
    primaryCov.ToString("0.#"),
    string.Join(";", ranked.Skip(1).Take(4).Select(c => $"{ShortName(c.Stem)}:{c.Matched}")),
    ride.RefSpl.Count.ToString(),
    ride.RefSid.Count.ToString(),
  ]);
}

WriteCsv(Path.Combine(repoRoot, ".agents", "summaries", "ovl-trr-scan.csv"), rows);

// Addon Classification
//
// Each segment archive's addon = the earliest RCT3 release among the rides that use it (a
// Vanilla archive can be reused by a later ride, but a Wild archive can't back a Vanilla ride).
// Base roster: .agents/summaries/rides.md (rct.fandom.com "The Complete Rides List").
// Soaked!/Wild! additions per rct3.fandom.com "List of Rides (expansion packs)" and rctgo,
// gathered via web search 2026-09. Only archives used *exclusively* by expansion rides are
// tagged; anything with a Vanilla ride, or with no ride at all, stays Vanilla / unknown.
var soakedRides = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
  "AlpineCoaster", "GiantFlume", "Halfpipe", "SuperSoaker", "SeaSerpent", "SkySwinger",
  "BoosterBikes", "WhiteWaterRapids", "HersheyTower", "Stormrunner", "BodySlide", "ProSlide",
  "MasterBlaster", "LazyRiver", "RingSlide", "GiantSlide", "SinkingShip",
};
var wildRides = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
  "BallCoaster", "RoboCoaster", "RotatingTowerCoaster", "SpinningSteel", "Seizmic", "TowerCoaster",
  "Flyturn", "Reverser", "SideFriction", "SpinningWild", "WildMouse", "ScreamingSquirrel",
  "VirginiaReel", "WoodenWildMine", "WoodenWildMouse", "Drifting", "FrequentFaller", "Aquarium",
  "InsectHouse", "ReptileHouse", "NocternalHouse", "MonsterTrucks", "CheshireCats", "SafariTrain",
  "SafariTransport", "ElephantTransport",
};
int Addon(string ride) => soakedRides.Contains(ride) ? 1 : wildRides.Contains(ride) ? 2 : 0;

string AddonFor(ArchiveInfo arch) {
  var used = archiveToRides[arch.Stem];
  return used.Count == 0
    ? ""
    : used.Min(r => Addon(ShortName(r))) switch { 1 => "Soaked", 2 => "Wild", _ => "Vanilla" };
}

var archiveRows = new List<string[]> { rideColumns };
foreach (var arch in segmentArchives.OrderBy(a => a.Stem, StringComparer.OrdinalIgnoreCase)) {
  var used = archiveToRides[arch.Stem].Distinct().OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
  archiveRows.Add([
    arch.Stem,
    AddonFor(arch),
    arch.DefinedTks.Count.ToString(),
    used.Count.ToString(),
    string.Join("|", used.Select(ShortName)),
  ]);
}
WriteCsv(Path.Combine(repoRoot, ".agents", "summaries", "track-rides.csv"), archiveRows);

// Per-archive detail lives in track-rides.csv; here just the counts plus the archives no
// tracked ride references (so their addon can't be inferred).
var addonCounts = segmentArchives.GroupBy(AddonFor)
  .ToDictionary(g => g.Key.Length == 0 ? "unknown" : g.Key, g => g.Count());
Console.WriteLine($"By addon: {string.Join(", ", addonCounts.OrderBy(k =>
{
  // FIXME: Ordering: Vanilla, Soaked, Wild, unknown
    return k.Key;
}).Select(k => $"{k.Value} {k.Key}"))}");

var unused = segmentArchives.Where(a => archiveToRides[a.Stem].Count == 0)
  .OrderBy(a => a.Stem, StringComparer.OrdinalIgnoreCase).ToList();
Console.WriteLine($"\n{unused.Count} unreferenced archives (addon unknown):");
foreach (var arch in unused)
  Console.WriteLine($"  {ShortName(arch.Stem),-16} {arch.DefinedTks.Count,4} tks");

Console.WriteLine($"\nWrote ovl-trr-scan.csv and track-rides.csv");
return 0;

static string ShortName(string stem) => string.IsNullOrEmpty(stem) ? "" : stem.Split('/')[^1];

static string StripOvlSuffix(string rel) =>
  rel.EndsWith(".common.ovl") ? rel[..^".common.ovl".Length]
  : rel.EndsWith(".unique.ovl") ? rel[..^".unique.ovl".Length]
  : rel.EndsWith(".ovl") ? rel[..^".ovl".Length]
  : rel;

// Decodes one OVL into an ArchiveInfo; returns null for archives that fail to parse.
static ArchiveInfo? LoadArchive(string file, string stem) {
  try {
    using var ovl = Ovl.Load(file);
    var info = new ArchiveInfo(stem);
    foreach (var key in ovl.Keys) {
      if (key.Type == FileType.TrackSection) info.DefinedTks.Add(key.Name);
      else if (key.Type == FileType.TrackedRide) info.TrrNames.Add(key.Name);
    }
    foreach (var (name, type) in ovl.SymbolReferences) {
      if (type == FileType.TrackSection) info.RefTks.Add(name);
      else if (type == FileType.Spline) info.RefSpl.Add(name);
      else if (type == FileType.SceneryItem) info.RefSid.Add(name);
    }
    return info;
  } catch {
    return null;
  }
}

// Unions the per-file ArchiveInfos that share a stem (an archive's .common + .unique halves).
static ArchiveInfo MergeArchives(IGrouping<string, ArchiveInfo> parts) {
  var merged = new ArchiveInfo(parts.Key);
  foreach (var part in parts) {
    merged.DefinedTks.UnionWith(part.DefinedTks);
    merged.TrrNames.UnionWith(part.TrrNames);
    merged.RefTks.UnionWith(part.RefTks);
    merged.RefSpl.UnionWith(part.RefSpl);
    merged.RefSid.UnionWith(part.RefSid);
  }
  return merged;
}

static void WriteCsv(string path, List<string[]> rows) {
  Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  static string Esc(string s) => s.Contains(',') || s.Contains('"') || s.Contains('\n')
    ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
  File.WriteAllLines(path, rows.Select(r => string.Join(',', r.Select(Esc))), new UTF8Encoding(false));
}

internal sealed class ArchiveInfo(string stem) {
  public string Stem { get; } = stem;
  public HashSet<string> DefinedTks { get; } = new(StringComparer.OrdinalIgnoreCase);
  public HashSet<string> TrrNames { get; } = new(StringComparer.OrdinalIgnoreCase);
  public HashSet<string> RefTks { get; } = new(StringComparer.OrdinalIgnoreCase);
  public HashSet<string> RefSpl { get; } = new(StringComparer.OrdinalIgnoreCase);
  public HashSet<string> RefSid { get; } = new(StringComparer.OrdinalIgnoreCase);
}

partial class Program {
  private static readonly string[] trrColumns = ["ride_ovl", "trr", "tks_refs", "primary_archive", "primary_matched", "primary_coverage_pct", "other_archives", "spl_refs", "sid_refs"];
  private static readonly string[] rideColumns = [ "segment_archive", "addon", "defined_tks", "ride_count", "rides" ];
}
