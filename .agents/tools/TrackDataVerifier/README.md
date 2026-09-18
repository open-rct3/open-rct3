# Track Data Decoder Verifier

Checks that `OpenCobra.OVL.Files.TrackData` can extract **usable rail geometry from the named
track sections** in an `spl`/`tks` scan CSV — not just that symbol names decode.

## What it verifies, per archive

- `spl` / `tks` resource counts match the CSV.
- **For each named `tks` sample**: the `TrackSection` decodes, its `SplineRefs` resolve to real
  decoded splines, **both left and right rails are present**, and every resolved rail
  (left/right/join/extra) passes the spline sanity checks below.
- **For each named `spl` sample**: that spline passes the sanity checks.
- Every spline in the archive passes the sanity checks (secondary sweep).
- Reports `TrackSection.IsValid` counts.

Spline sanity checks: `NodeCount` vs `Nodes.Length`, at least one node, finite node positions and
control points, non-negative finite `TotalLength` (non-zero for multi-node splines),
`SegmentLengths` / `Segments` lengths against the open/cyclic expectation, 14 samples per segment.

A row **FAILS** on: a count mismatch, an unresolved sample name, a named `tks` whose left/right
rails don't both resolve, or any geometry issue on a named or archive spline.

## Usage

```bash
# Combined scan CSV (file,spl_count,tks_count,spl_samples,tks_samples)
dotnet run --project .agents/tools/TrackDataVerifier/TrackDataVerifier.csproj -c Release

# Per-type scan CSV (file,type,count,samples) — rows sharing a file are merged
dotnet run --project .agents/tools/TrackDataVerifier/TrackDataVerifier.csproj -- .agents/summaries/ovl-tks-scan.csv

# Subset / cap
dotnet run --project .agents/tools/TrackDataVerifier/TrackDataVerifier.csproj -- --filter=Yoshi
dotnet run --project .agents/tools/TrackDataVerifier/TrackDataVerifier.csproj -- --limit=20
```

Archive paths may be repo-relative, absolute, a fixture name, or `${RCT3_INSTALL}\`-prefixed;
each is resolved against the repo root, `RCT3_PATH`, and the fixture directory.

## Output

- Console: one line per archive (`ok` / `FAIL` / `ERR` / `miss`) plus a summary.
- `.agents/summaries/<csv-name>-verify.csv`: per-archive report, columns `file`, `status`,
  `spl_found`, `spl_expected`, `tks_found`, `tks_expected`, `named_tks_ok`, `named_tks_total`,
  `named_spl_ok`, `named_spl_total`, `archive_spline_issues`, `tks_valid`, `tks_total`, `notes`.
- Exit code `1` if any archive fails, `2` if the CSV is missing, `0` otherwise.
