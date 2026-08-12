using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;

// Parse command-line arguments for file types to scan
var requestedTypes = args.Length > 0
  ? args.Select(arg => arg.ToFileType()).Where(t => t != FileType.Unknown).ToList()
  : [];

if (requestedTypes.Count == 0) {
  Console.WriteLine("Usage: dotnet run --project .agents/tools/OvlScanner/OvlScanner.csproj -- <tag1> [tag2] [tag3] ...");
  Console.WriteLine("\nExamples:");
  Console.WriteLine("  dotnet run --project ... -- spl tks       # Scan for Spline and TrackSection");
  Console.WriteLine("  dotnet run --project ... -- tex           # Scan for Textures");
  Console.WriteLine("  dotnet run --project ... -- shs sid       # Scan for Static Shapes and Scenery Items");
  Console.WriteLine("\nSupported tags (29 types):");
  Console.WriteLine("  txt, int, tex, flic, ftx, gsi, sid, btbl, anr, ban, bsh, ced, chg, cid,");
  Console.WriteLine("  mam, ptd, qtd, ric, rit, sat, shs, snd, spl, sta, svd, ter, tks, trr, wai, mms, prt, psi, fct");
  return;
}

var repoRoot = Directory.GetCurrentDirectory();
while (!Directory.Exists(Path.Combine(repoRoot, "OpenCobra")) && repoRoot.Length > 3) {
  repoRoot = Path.GetDirectoryName(repoRoot)!;
}
var fixtureDir = Path.Combine(repoRoot, "OpenCobra", "Tests", "Fixtures", "OVL");

// Generate output filename based on requested types
var typeNames = string.Join("-", requestedTypes.Select(t => t.ToTagString()).OrderBy(t => t));
var outputFile = Path.Combine(repoRoot, ".agents", "summaries", $"ovl-{typeNames}-scan.csv");

Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

var results = new List<(string File, string Type, int Count, string Samples)>();
var typeCounts = new Dictionary<FileType, int>();
foreach (var type in requestedTypes) {
  typeCounts[type] = 0;
}

Console.WriteLine($"OVL Resource Scanner\n");
Console.WriteLine($"Scanning for: {string.Join(", ", requestedTypes.Select(t => $"{t.ToDisplayName()} ({t.ToTagString()})"))}");
Console.WriteLine();

var allOvlFiles = new List<string>();

// Add fixture OVLs
Console.WriteLine("=== FIXTURES ===");
if (Directory.Exists(fixtureDir)) {
  var fixtureOvls = Directory.GetFiles(fixtureDir, "*.ovl", SearchOption.AllDirectories);
  allOvlFiles.AddRange(fixtureOvls);
  Console.WriteLine($"Found {fixtureOvls.Length} fixture OVLs\n");
} else {
  Console.WriteLine("Fixture directory not found\n");
}

// Add production OVLs if RCT3_PATH is available
var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH");
if (!string.IsNullOrEmpty(rct3Path) && Directory.Exists(rct3Path)) {
  Console.WriteLine("=== PRODUCTION OVLs (RCT3_PATH) ===");
  Console.WriteLine($"Scanning: {rct3Path}\n");

  // Scan common RCT3 OVL locations
  var searchPaths = new[] {
    Path.Combine(rct3Path, "Rides"),
    Path.Combine(rct3Path, "tracks"),
  };

  var foundCount = 0;
  foreach (var searchPath in searchPaths) {
    if (!Directory.Exists(searchPath)) continue;

    try {
      var pathOvls = Directory.GetFiles(searchPath, "*.ovl", SearchOption.AllDirectories);
      foundCount += pathOvls.Length;
      Console.WriteLine($"  {Path.GetRelativePath(rct3Path, searchPath),-40} {pathOvls.Length,5} OVLs");
      allOvlFiles.AddRange(pathOvls);
    } catch (UnauthorizedAccessException) {
      Console.WriteLine($"  {Path.GetRelativePath(rct3Path, searchPath),-40} [ACCESS DENIED]");
    }
  }
  Console.WriteLine($"\nTotal production OVLs found: {foundCount}\n");
} else {
  Console.WriteLine("=== PRODUCTION OVLs ===");
  Console.WriteLine("RCT3_PATH environment variable not set - skipping production OVL scan\n");
}

Console.WriteLine($"=== SCANNING ===\n");

int fileCount = 0;
int filesWithMatches = 0;

foreach (var ovlFile in allOvlFiles.OrderBy(f => f)) {
  fileCount++;
  if (fileCount % 100 == 0) Console.WriteLine($"  Progress: {fileCount}/{allOvlFiles.Count}...");

  try {
    using var ovl = Ovl.Load(ovlFile);

    var matchesByType = new Dictionary<FileType, List<string>>();
    foreach (var type in requestedTypes) {
      var matches = ovl.Where(kvp => kvp.Key.Type == type).Select(kvp => kvp.Key.Name).ToList();
      if (matches.Count > 0) {
        matchesByType[type] = matches;
        typeCounts[type] += matches.Count;
      }
    }

    if (matchesByType.Count == 0) continue;

    filesWithMatches++;

    foreach (var (type, matches) in matchesByType) {
      var samples = string.Join("; ", matches.Take(3));
      results.Add((
        Path.GetRelativePath(repoRoot, ovlFile),
        type.ToTagString(),
        matches.Count,
        samples
      ));
    }

    // Display result line
    var displayParts = matchesByType.OrderBy(kvp => kvp.Key.ToTagString())
      .Select(kvp => $"{kvp.Value.Count,3} {kvp.Key.ToTagString()}");
    Console.WriteLine($"✓ {Path.GetFileName(ovlFile),-45} {string.Join("  ", displayParts)}");
  } catch {
    // Silently skip files that can't be parsed
  }
}

Console.WriteLine($"\n{'=',-70}");
Console.WriteLine($"Scanned: {fileCount} OVL files");
Console.WriteLine($"Found matches in: {filesWithMatches} files");
Console.WriteLine($"\nSummary by type:");
foreach (var type in requestedTypes.OrderBy(t => t.ToTagString())) {
  Console.WriteLine($"  {type.ToDisplayName(),30} ({type.ToTagString()}): {typeCounts[type],6} entries");
}

// Write CSV
var csvHeader = string.Join(",", new[] { "file", "type", "count", "samples" });
var csvLines = new List<string> { csvHeader };
foreach (var (file, type, count, samples) in results.OrderBy(r => r.File).ThenBy(r => r.Type)) {
  var escapedSamples = samples.Replace("\"", "\"\"");
  csvLines.Add($"{file},{type},{count},\"{escapedSamples}\"");
}
File.WriteAllLines(outputFile, csvLines, Encoding.UTF8);

Console.WriteLine($"\n✓ Results written to {Path.GetRelativePath(repoRoot, outputFile)}");
