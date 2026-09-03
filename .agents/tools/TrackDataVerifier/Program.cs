using System.Globalization;
using System.Numerics;
using System.Text;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;

// Verifies the OpenCobra track-data decoder against the OVL archives named in an spl/tks scan CSV.
//
// Accepts either scan CSV layout:
//   - combined : file,spl_count,tks_count,spl_samples,tks_samples   (ovl-spl-tks-scan.csv)
//   - per-type : file,type,count,samples                            (ovl-spl-scan.csv / ovl-tks-scan.csv)
//                (rows for the same file are merged)
//
// For every archive it:
//   - re-counts spl/tks resources and compares against the CSV counts
//   - for each NAMED tks sample: resolves that TrackSection's SplineRefs to real decoded splines,
//     requires both left and right rails, and sanity-checks each resolved rail's geometry
//   - for each NAMED spl sample: sanity-checks that spline's geometry
//   - additionally sanity-checks every spline in the archive, and reports TrackSection.IsValid
//
// A row FAILS on: count mismatch, an unresolved sample name, a named tks whose left/right rails
// don't both resolve, or any geometry issue on a named/archive spline. Exits non-zero on any fail.

var csvArg = args.FirstOrDefault(a => !a.StartsWith("--"));
var limit = int.MaxValue;
var filter = args.FirstOrDefault(a => a.StartsWith("--filter="))?["--filter=".Length..];
var limitArg = args.FirstOrDefault(a => a.StartsWith("--limit="));
if (limitArg != null) limit = int.Parse(limitArg["--limit=".Length..], CultureInfo.InvariantCulture);

var repoRoot = Directory.GetCurrentDirectory();
while (!Directory.Exists(Path.Combine(repoRoot, "OpenCobra")) && repoRoot.Length > 3)
  repoRoot = Path.GetDirectoryName(repoRoot)!;

var csvPath = csvArg is null
  ? Path.Combine(repoRoot, ".agents", "summaries", "ovl-spl-tks-scan.csv")
  : Path.GetFullPath(csvArg);
var fixtureDir = Path.Combine(repoRoot, "OpenCobra", "Tests", "Fixtures", "OVL");
var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH");
var outputPath = Path.Combine(
  repoRoot, ".agents", "summaries",
  Path.GetFileNameWithoutExtension(csvPath).Replace("-scan", "") + "-verify.csv");

if (!File.Exists(csvPath)) {
  Console.Error.WriteLine($"Scan CSV not found: {csvPath}");
  return 2;
}

var rows = ParseCsv(csvPath).ToList();

Console.WriteLine("Track Data Decoder Verifier\n");
Console.WriteLine($"  scan CSV   : {Path.GetRelativePath(repoRoot, csvPath)}  ({rows.Count} archives)");
Console.WriteLine($"  fixtures   : {(Directory.Exists(fixtureDir) ? fixtureDir : "(missing)")}");
Console.WriteLine($"  RCT3_PATH  : {(string.IsNullOrEmpty(rct3Path) ? "(unset)" : rct3Path)}\n");

string? ResolvePath(string file) {
  var rel = file.Trim();
  foreach (var token in new[] { "${RCT3_PATH}", "$(RCT3_PATH)", "%RCT3_PATH%",
                                "${RCT3_INSTALL}", "$(RCT3_INSTALL)", "%RCT3_INSTALL%" })
    if (rel.StartsWith(token, StringComparison.Ordinal))
      rel = rel[token.Length..];
  rel = rel.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)
    .TrimStart(Path.DirectorySeparatorChar);

  var candidates = new List<string>();
  if (Path.IsPathRooted(file)) candidates.Add(file);
  candidates.Add(Path.GetFullPath(Path.Combine(repoRoot, file)));
  if (!string.IsNullOrEmpty(rct3Path)) candidates.Add(Path.Combine(rct3Path, rel));
  if (Directory.Exists(fixtureDir)) {
    candidates.Add(Path.Combine(fixtureDir, rel));
    candidates.Add(Path.Combine(fixtureDir, Path.GetFileName(rel)));
  }
  candidates.Add(Path.Combine(repoRoot, rel));
  return candidates.FirstOrDefault(File.Exists);
}

var report = new List<string[]> {
  new[] { "file", "status", "spl_found", "spl_expected", "tks_found", "tks_expected",
    "named_tks_ok", "named_tks_total", "named_spl_ok", "named_spl_total",
    "archive_spline_issues", "tks_valid", "tks_total", "notes" },
};

int checkedCount = 0, okCount = 0, missingCount = 0, failCount = 0;
int totalCountMismatch = 0, totalNamedTksFail = 0, totalNamedSplFail = 0, totalArchiveSplineIssues = 0, totalErrored = 0;

foreach (var row in rows) {
  if (filter != null && !row.File.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
  if (checkedCount >= limit) break;

  var path = ResolvePath(row.File);
  if (path is null) {
    missingCount++;
    report.Add([row.File, "missing", "", Str(row.SplCount), "", Str(row.TksCount), "", "", "", "", "", "", "", "archive not found"]);
    Console.WriteLine($"miss {row.File}");
    continue;
  }

  checkedCount++;
  var notes = new List<string>();
  try {
    using var ovl = Ovl.Load(path);
    var splines = TrackData.ExtractSplines(ovl).ToDictionary(s => s.Id);
    var sections = TrackData.ExtractTrackSections(ovl).ToDictionary(s => s.Id);

    var countOk = true;
    if (row.SplCount is { } sc && splines.Count != sc) {
      countOk = false; notes.Add($"spl count: csv {sc}, decoded {splines.Count}");
    }
    if (row.TksCount is { } tc && sections.Count != tc) {
      countOk = false; notes.Add($"tks count: csv {tc}, decoded {sections.Count}");
    }
    if (!countOk) totalCountMismatch++;

    // Named track sections: walk each to its rail splines and check the geometry is extractable.
    var namedTksOk = 0;
    foreach (var name in row.TksSamples) {
      if (!sections.TryGetValue(name, out var section)) {
        notes.Add($"tks '{name}' not decoded");
        continue;
      }
      var issues = InspectNamedSection(name, section, splines).ToList();
      if (issues.Count == 0) namedTksOk++;
      else notes.AddRange(issues.Take(4));
    }
    if (namedTksOk != row.TksSamples.Length) totalNamedTksFail++;

    // Named splines: check the geometry directly.
    var namedSplOk = 0;
    foreach (var name in row.SplSamples) {
      if (!splines.TryGetValue(name, out var spline)) {
        notes.Add($"spl '{name}' not decoded");
        continue;
      }
      var issues = InspectSpline(spline).ToList();
      if (issues.Count == 0) namedSplOk++;
      else notes.Add($"spl '{name}': {string.Join(", ", issues)}");
    }
    if (namedSplOk != row.SplSamples.Length) totalNamedSplFail++;

    // Whole-archive spline sweep (secondary signal).
    var archiveSplineIssues = 0;
    foreach (var spline in splines.Values)
      foreach (var issue in InspectSpline(spline)) {
        archiveSplineIssues++;
        if (archiveSplineIssues <= 3) notes.Add($"spline '{spline.Id}': {issue}");
      }
    totalArchiveSplineIssues += archiveSplineIssues;

    var tksValid = sections.Values.Count(s => s.IsValid);
    if (sections.Count > 0 && tksValid != sections.Count)
      notes.Add($"{sections.Count - tksValid}/{sections.Count} sections IsValid=false");

    var pass = countOk
      && namedTksOk == row.TksSamples.Length
      && namedSplOk == row.SplSamples.Length
      && archiveSplineIssues == 0;
    if (pass) okCount++; else failCount++;

    report.Add([
      row.File, pass ? "ok" : "FAIL",
      splines.Count.ToString(), Str(row.SplCount),
      sections.Count.ToString(), Str(row.TksCount),
      namedTksOk.ToString(), row.TksSamples.Length.ToString(),
      namedSplOk.ToString(), row.SplSamples.Length.ToString(),
      archiveSplineIssues.ToString(), tksValid.ToString(), sections.Count.ToString(),
      string.Join("; ", notes),
    ]);

    Console.WriteLine($"{(pass ? "ok  " : "FAIL")} {row.File}"
      + (notes.Count > 0 ? $"\n      {string.Join("\n      ", notes)}" : ""));
  } catch (Exception ex) {
    failCount++;
    totalErrored++;
    report.Add([row.File, "ERROR", "", Str(row.SplCount), "", Str(row.TksCount), "", "", "", "", "", "", "", $"{ex.GetType().Name}: {ex.Message}"]);
    Console.WriteLine($"ERR  {row.File}\n      {ex.GetType().Name}: {ex.Message}");
  }
}

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllLines(outputPath, report.Select(FormatCsvRow), new UTF8Encoding(false));

Console.WriteLine($"\n{new string('=', 60)}");
Console.WriteLine($"archives in CSV          : {rows.Count}");
Console.WriteLine($"checked                  : {checkedCount}");
Console.WriteLine($"  passed                 : {okCount}");
Console.WriteLine($"  failed                 : {failCount}");
Console.WriteLine($"not found on disk        : {missingCount}");
Console.WriteLine($"count mismatches         : {totalCountMismatch}");
Console.WriteLine($"named-tks rail failures  : {totalNamedTksFail}");
Console.WriteLine($"named-spl geom failures  : {totalNamedSplFail}");
Console.WriteLine($"archives w/ spline issues: {totalArchiveSplineIssues}");
Console.WriteLine($"archives errored         : {totalErrored}");
Console.WriteLine($"\nreport: {Path.GetRelativePath(repoRoot, outputPath)}");

return failCount > 0 ? 1 : 0;

static string Str(int? n) => n?.ToString() ?? "";

// Resolves a named TrackSection's rail references to decoded splines and checks the geometry is
// usable: both left (slot 0) and right (slot 1) rails must resolve, and every resolved rail
// (left/right/join/extra) must pass the spline sanity checks.
static IEnumerable<string> InspectNamedSection(string name, TrackSection section, IReadOnlyDictionary<string, Spline> splines) {
  string[] slotNames = ["left", "right", "join-left", "join-right", "extra-left", "extra-right"];
  var resolved = new Spline?[section.SplineRefs.Length];

  for (var i = 0; i < section.SplineRefs.Length; i++) {
    var reference = section.SplineRefs[i];
    if (string.IsNullOrEmpty(reference)) continue;
    if (reference.StartsWith("<unresolved", StringComparison.Ordinal)) {
      if (i < 2) yield return $"tks '{name}': {slotNames[i]} rail ref {reference}";
      continue;
    }
    if (!splines.TryGetValue(reference, out var spline)) {
      yield return $"tks '{name}': {Slot(slotNames, i)} rail '{reference}' not among decoded splines";
      continue;
    }
    resolved[i] = spline;
  }

  if (resolved[0] is null) yield return $"tks '{name}': no left rail spline resolved";
  if (section.SplineRefs.Length > 1 && resolved[1] is null) yield return $"tks '{name}': no right rail spline resolved";

  for (var i = 0; i < resolved.Length; i++) {
    if (resolved[i] is not { } rail) continue;
    foreach (var issue in InspectSpline(rail))
      yield return $"tks '{name}': {Slot(slotNames, i)} rail '{rail.Id}': {issue}";
  }

  static string Slot(string[] names, int i) => i < names.Length ? names[i] : $"slot {i}";
}

static IEnumerable<string> InspectSpline(Spline spline) {
  if (spline.Nodes.Length != spline.NodeCount)
    yield return $"NodeCount={spline.NodeCount} but Nodes.Length={spline.Nodes.Length}";
  if (spline.NodeCount == 0)
    yield return "spline has no nodes";
  for (var i = 0; i < spline.Nodes.Length; i++) {
    if (!IsFinite(spline.Nodes[i]) || !IsFinite(spline.ControlPoint1[i]) || !IsFinite(spline.ControlPoint2[i])) {
      yield return $"non-finite geometry at node {i}";
      break;
    }
  }
  if (!float.IsFinite(spline.TotalLength) || spline.TotalLength < 0)
    yield return $"invalid TotalLength {spline.TotalLength}";
  if (spline.NodeCount >= 2 && spline.TotalLength == 0)
    yield return "TotalLength is zero for a multi-node spline";

  var expectedSegments = spline.Cyclic ? (int)spline.NodeCount : Math.Max(0, (int)spline.NodeCount - 1);
  if (spline.SegmentLengths.Length != expectedSegments)
    yield return $"SegmentLengths.Length={spline.SegmentLengths.Length}, expected {expectedSegments}";
  if (spline.Segments.Length != expectedSegments)
    yield return $"Segments.Length={spline.Segments.Length}, expected {expectedSegments}";
  for (var i = 0; i < spline.Segments.Length; i++) {
    if (spline.Segments[i].Samples is not { Length: 14 }) {
      yield return $"segment {i} does not have 14 samples";
      break;
    }
  }
}

static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

static IEnumerable<ScanRow> ParseCsv(string path) {
  using var reader = new StreamReader(path);
  var header = reader.ReadLine();
  if (header is null) yield break;
  var cols = SplitCsv(header).Select(c => c.Trim().ToLowerInvariant()).ToArray();
  var combined = cols.Contains("spl_samples") || cols.Contains("tks_samples");

  if (combined) {
    string? line;
    while ((line = reader.ReadLine()) is not null) {
      if (string.IsNullOrWhiteSpace(line)) continue;
      var f = SplitCsv(line);
      if (f.Count < 5) continue;
      yield return new ScanRow(
        f[0],
        int.TryParse(f[1], out var spl) ? spl : null,
        int.TryParse(f[2], out var tks) ? tks : null,
        SplitSamples(f[3]),
        SplitSamples(f[4]));
    }
    yield break;
  }

  // Per-type layout: file,type,count,samples — merge rows sharing a file, preserving order.
  var order = new List<string>();
  var acc = new Dictionary<string, (int? spl, int? tks, string[] splS, string[] tksS)>();
  string? row;
  while ((row = reader.ReadLine()) is not null) {
    if (string.IsNullOrWhiteSpace(row)) continue;
    var f = SplitCsv(row);
    if (f.Count < 4) continue;
    var file = f[0];
    var type = f[1].Trim().ToLowerInvariant();
    var count = int.TryParse(f[2], out var c) ? c : (int?)null;
    var samples = SplitSamples(f[3]);
    if (!acc.ContainsKey(file)) { acc[file] = (null, null, [], []); order.Add(file); }
    var cur = acc[file];
    if (type == "spl") cur = (count, cur.tks, samples, cur.tksS);
    else if (type == "tks") cur = (cur.spl, count, cur.splS, samples);
    acc[file] = cur;
  }
  foreach (var file in order) {
    var (spl, tks, splS, tksS) = acc[file];
    yield return new ScanRow(file, spl, tks, splS, tksS);
  }
}

static string[] SplitSamples(string field) => field
  .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
  .ToArray();

static List<string> SplitCsv(string line) {
  var fields = new List<string>();
  var sb = new StringBuilder();
  var inQuotes = false;
  for (var i = 0; i < line.Length; i++) {
    var c = line[i];
    if (inQuotes) {
      if (c == '"') {
        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
        else inQuotes = false;
      } else sb.Append(c);
    } else if (c == '"') inQuotes = true;
    else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
    else sb.Append(c);
  }
  fields.Add(sb.ToString());
  return fields;
}

static string FormatCsvRow(string[] fields) => string.Join(',', fields.Select(f =>
  f.Contains(',') || f.Contains('"') || f.Contains('\n')
    ? $"\"{f.Replace("\"", "\"\"")}\""
    : f));

internal readonly record struct ScanRow(string File, int? SplCount, int? TksCount, string[] SplSamples, string[] TksSamples);
