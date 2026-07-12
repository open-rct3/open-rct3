#!/usr/bin/env -S deno run --allow-read --allow-net --allow-env
//
// rank-plans.ts
//
// Ranks open plans under .agents/plans by an "impact" heuristic and prints a
// table. Path handling uses Deno's std `@std/path` module (via JSR) rather
// than hand-rolled string splicing — battle-tested, cross-platform (handles
// Windows drive letters/backslashes correctly).
//
// Usage:
//   deno run --allow-read --allow-net --allow-env .agents/scripts/rank-plans.ts [--top=N] [--json] [--dir=<path>]
//
// (--allow-net/--allow-env are needed to resolve/cache the jsr:@std/path
// import AND, on every run, to fetch the project roadmap wiki page. If the
// roadmap fetch fails — offline, rate-limited, etc. — the script warns and
// falls back to scoring without the roadmap-item-match term.)
//
// Flags:
//   --top=N     Only show the top N plans (default: all).
//   --json      Print machine-readable JSON instead of a table.
//   --dir=path  Override the plans directory (default: <repoRoot>/.agents/plans).
//
// Scoring (see `computeScore` for the authoritative formula):
//   score = inboundLinks * 10        // how many OTHER .agents docs reference this plan
//         + roadmapWeight * 4        // Phase 1 > Phase 2 > Phase 3 > unspecified (manual **Roadmap** annotation)
//         + roadmapItemWeight        // match against a live, undone item on the project roadmap wiki
//         + resolvedBullets * 1      // design maturity: settled decisions ready to implement
//         + (inProgress ? 5 : 0)     // momentum bonus for work already underway
//
// A plan is "closed" (excluded from ranking) if its Status text contains any
// of: implemented, complete, done, superseded — these are treated as shipped
// or replaced, not open work. Closed plans are still listed in a footer for
// transparency.

import { basename, dirname, extname, fromFileUrl, join, relative, resolve } from "jsr:@std/path@^1.0.0";

// ---------------------------------------------------------------------------
// Repo/filesystem discovery
// ---------------------------------------------------------------------------

/** Case-insensitive comparison key, robust on Windows filesystems. */
function normalizeForCompare(p: string): string {
  return p.replaceAll("\\", "/").toLowerCase();
}

function repoRootFromScriptUrl(): string {
  const scriptPath = fromFileUrl(import.meta.url);
  // Script lives at <repoRoot>/.agents/scripts/rank-plans.ts
  return resolve(dirname(scriptPath), "..", "..");
}

interface WalkOptions {
  extensions?: string[];
}

/** Recursively collects file paths under `root`, tolerating unreadable entries. */
async function walkFiles(root: string, opts: WalkOptions = {}): Promise<string[]> {
  const results: string[] = [];
  const extensions = opts.extensions;

  async function recurse(dir: string) {
    let entries: Deno.DirEntry[];
    try {
      entries = [...Deno.readDirSync(dir)];
    } catch (err) {
      console.error(`warn: could not read directory ${dir}: ${(err as Error).message}`);
      return;
    }
    for (const entry of entries) {
      const full = join(dir, entry.name);
      if (entry.isDirectory) {
        await recurse(full);
      } else if (entry.isFile) {
        if (!extensions || extensions.includes(extname(entry.name).toLowerCase())) {
          results.push(full);
        }
      }
      // Symlinks are skipped deliberately — no cycle-detection needed and no
      // surprise traversal outside the intended tree.
    }
  }

  await recurse(root);
  return results;
}

// ---------------------------------------------------------------------------
// Plan parsing
// ---------------------------------------------------------------------------

interface Plan {
  path: string; // absolute, posix-style
  relPath: string; // relative to repo root, for display
  title: string;
  statusText: string;
  closed: boolean;
  closedReason?: string;
  roadmapWeight: number;
  roadmapLabel: string;
  resolvedBullets: number;
  inProgress: boolean;
  inboundLinks: number;
  inboundFrom: string[];
  roadmapItemWeight: number;
  roadmapItemMatch?: string; // matched roadmap wiki item text, for display
  roadmapItemPhase?: string;
  score: number;
}

const CLOSED_KEYWORDS = ["implemented", "complete", "done", "superseded"];
const IN_PROGRESS_KEYWORDS = ["in progress", "partially done", "partially implemented"];

function extractTitle(content: string, fallback: string): string {
  const match = content.match(/^#\s+(.+)$/m);
  return match ? match[1].trim() : fallback;
}

/** Pulls the text right after a "## Status" heading, or a "**Status**:" line, whichever exists. */
function extractStatusText(content: string): string {
  const headingMatch = content.match(/^##\s+Status\s*$/im);
  if (headingMatch) {
    const start = headingMatch.index! + headingMatch[0].length;
    const rest = content.slice(start);
    const nextHeading = rest.search(/^##\s+/m);
    return (nextHeading === -1 ? rest : rest.slice(0, nextHeading)).trim().slice(0, 600);
  }
  const inlineMatch = content.match(/\*\*Status\*\*:?\s*(.+)/i);
  if (inlineMatch) return inlineMatch[1].trim().slice(0, 600);
  return "";
}

function classifyStatus(statusText: string, fullContent: string): { closed: boolean; reason?: string; inProgress: boolean } {
  const haystack = statusText.toLowerCase() || fullContent.slice(0, 1200).toLowerCase();
  for (const kw of CLOSED_KEYWORDS) {
    if (haystack.includes(kw)) return { closed: true, reason: kw, inProgress: false };
  }
  const inProgress = IN_PROGRESS_KEYWORDS.some((kw) => haystack.includes(kw));
  return { closed: false, inProgress };
}

function extractRoadmapWeight(content: string): { weight: number; label: string } {
  const roadmapLine = content.match(/\*\*Roadmap\*\*:?\s*(.+)/i);
  const haystack = (roadmapLine ? roadmapLine[1] : content.slice(0, 2000));
  if (/distant future/i.test(haystack)) return { weight: 0, label: "Distant Future" };
  if (/phase\s*1/i.test(haystack)) return { weight: 3, label: "Phase 1" };
  if (/phase\s*2/i.test(haystack)) return { weight: 2, label: "Phase 2" };
  if (/phase\s*3/i.test(haystack)) return { weight: 1, label: "Phase 3" };
  return { weight: 1, label: "Unspecified" };
}

/** Counts top-level bullets under any "Resolved" heading — a proxy for design maturity. */
function countResolvedBullets(content: string): number {
  const headingRegex = /^##\s+Resolved.*$/gim;
  let total = 0;
  let match: RegExpExecArray | null;
  while ((match = headingRegex.exec(content)) !== null) {
    const start = match.index + match[0].length;
    const rest = content.slice(start);
    const nextHeading = rest.search(/^##\s+/m);
    const section = nextHeading === -1 ? rest : rest.slice(0, nextHeading);
    const bullets = section.match(/^-\s+\*\*/gm);
    total += bullets ? bullets.length : 0;
  }
  return total;
}

/** Extracts every markdown-link target from `content`, e.g. `[text](path.md)`. */
function extractLinkTargets(content: string): string[] {
  const linkRegex = /\[[^\]]*\]\(([^)]+)\)/g;
  const targets: string[] = [];
  let match: RegExpExecArray | null;
  while ((match = linkRegex.exec(content)) !== null) {
    let target = match[1].trim();
    // Strip a trailing markdown "title" in quotes, and any #fragment.
    target = target.split(/\s+"/)[0].split("#")[0].trim();
    if (!target || target.startsWith("http://") || target.startsWith("https://")) continue;
    if (extname(target).toLowerCase() !== ".md") continue;
    targets.push(decodeURIComponent(target));
  }
  return targets;
}

function computeScore(plan: Omit<Plan, "score">): number {
  return (
    plan.inboundLinks * 10 +
    plan.roadmapWeight * 4 +
    plan.roadmapItemWeight +
    plan.resolvedBullets * 1 +
    (plan.inProgress ? 5 : 0)
  );
}

// ---------------------------------------------------------------------------
// Roadmap wiki integration
// ---------------------------------------------------------------------------

const ROADMAP_WIKI_URL = "https://raw.githubusercontent.com/wiki/open-rct3/open-rct3/Roadmap.md";

interface RoadmapItem {
  phaseName: string;
  phaseWeight: number; // same scale as roadmapWeight: Phase 1 = 3 ... Distant Future = 0
  index: number; // position within phase, 0-based; earlier = higher priority
  itemCount: number; // total items in this phase, for position scaling
  text: string;
  done: boolean;
}

function phaseWeightFor(phaseHeading: string): number {
  if (/distant future/i.test(phaseHeading)) return 0;
  if (/phase\s*1/i.test(phaseHeading)) return 3;
  if (/phase\s*2/i.test(phaseHeading)) return 2;
  if (/phase\s*3/i.test(phaseHeading)) return 1;
  return 1;
}

/** Strips markdown links/checkboxes/issue refs down to plain item text. */
function cleanRoadmapItemText(raw: string): string {
  return raw
    .replace(/\[([xX ])\]\s*/g, "") // checkbox markers
    .replace(/\[([^\]]*)\]\([^)]*\)/g, "$1") // [text](url) -> text
    .replace(/\(#\d+\)/g, "") // issue refs like (#3)
    .replace(/\s+/g, " ")
    .trim();
}

/** Fetches and parses the project roadmap wiki page into a flat, ordered item list. */
async function fetchRoadmapItems(): Promise<RoadmapItem[]> {
  const res = await fetch(ROADMAP_WIKI_URL);
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  const content = await res.text();

  const items: RoadmapItem[] = [];
  const headingRegex = /^#\s+(.+)$/gm;
  const headings: { title: string; start: number }[] = [];
  let hMatch: RegExpExecArray | null;
  while ((hMatch = headingRegex.exec(content)) !== null) {
    headings.push({ title: hMatch[1].trim(), start: hMatch.index + hMatch[0].length });
  }

  for (let i = 0; i < headings.length; i++) {
    const title = cleanRoadmapItemText(headings[i].title);
    const { start } = headings[i];
    const end = i + 1 < headings.length ? headings[i + 1].start : content.length;
    // Cut back to before the next heading's "# " line.
    const sectionEnd = i + 1 < headings.length ? content.lastIndexOf("\n#", end) : end;
    const section = content.slice(start, sectionEnd === -1 ? end : sectionEnd);

    const phaseWeight = phaseWeightFor(title);
    const lineRegex = /^\s*(?:\d+\.|-)\s+(.+)$/gm;
    const rawItems: { text: string; done: boolean }[] = [];
    let lMatch: RegExpExecArray | null;
    while ((lMatch = lineRegex.exec(section)) !== null) {
      const raw = lMatch[1];
      const done = /^\[x\]/i.test(raw.trim());
      const text = cleanRoadmapItemText(raw);
      if (text) rawItems.push({ text, done });
    }

    rawItems.forEach((item, index) => {
      items.push({
        phaseName: title,
        phaseWeight,
        index,
        itemCount: rawItems.length,
        text: item.text,
        done: item.done,
      });
    });
  }

  return items;
}

const STOPWORDS = new Set([
  "with", "that", "this", "from", "into", "shall", "then", "additional",
  "design", "features", "such", "using", "including", "than", "have",
  "also", "will", "each", "more", "over", "and", "the", "for",
]);

function tokenize(text: string): Set<string> {
  const words = text.toLowerCase().match(/[a-z]{4,}/g) ?? [];
  return new Set(words.filter((w) => !STOPWORDS.has(w)));
}

/** Overlap coefficient: |A ∩ B| / min(|A|, |B|) — forgiving when one side is short. */
function overlapCoefficient(a: Set<string>, b: Set<string>): { overlap: number; common: number } {
  if (a.size === 0 || b.size === 0) return { overlap: 0, common: 0 };
  let common = 0;
  for (const w of a) if (b.has(w)) common++;
  return { overlap: common / Math.min(a.size, b.size), common };
}

interface RoadmapMatch {
  weight: number;
  itemText: string;
  phaseName: string;
}

const ROADMAP_MATCH_THRESHOLD = 0.25;
const ROADMAP_MATCH_MIN_COMMON_WORDS = 2;

/** Finds the best-matching (undone) roadmap item for a plan's title+content, if any clears the threshold. */
function matchRoadmapItem(title: string, content: string, roadmapItems: RoadmapItem[]): RoadmapMatch | null {
  const docTokens = tokenize(`${title} ${content.slice(0, 3000)}`);
  let best: RoadmapMatch | null = null;
  let bestOverlap = 0;

  for (const item of roadmapItems) {
    if (item.done || item.phaseWeight <= 0) continue;
    const itemTokens = tokenize(item.text);
    const { overlap, common } = overlapCoefficient(itemTokens, docTokens);
    if (overlap < ROADMAP_MATCH_THRESHOLD || common < ROADMAP_MATCH_MIN_COMMON_WORDS || overlap <= bestOverlap) continue;

    // Earlier items within a phase carry more weight (up to 50% falloff by the phase's last item).
    const positionScale = 1 - (item.index / Math.max(item.itemCount, 1)) * 0.5;
    const itemWeight = item.phaseWeight * 4 * positionScale;

    bestOverlap = overlap;
    best = { weight: itemWeight * overlap, itemText: item.text, phaseName: item.phaseName };
  }

  return best;
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

function parseArgs(argv: string[]) {
  let top: number | null = null;
  let json = false;
  let dirOverride: string | null = null;
  for (const arg of argv) {
    if (arg === "--json") json = true;
    else if (arg.startsWith("--top=")) {
      const n = Number(arg.slice("--top=".length));
      if (!Number.isNaN(n) && n > 0) top = n;
    } else if (arg.startsWith("--dir=")) {
      dirOverride = arg.slice("--dir=".length);
    }
  }
  return { top, json, dirOverride };
}

async function main() {
  const { top, json, dirOverride } = parseArgs(Deno.args);
  const repoRoot = repoRootFromScriptUrl();
  const agentsDir = join(repoRoot, ".agents");
  const plansDir = dirOverride ? resolve(repoRoot, dirOverride) : join(agentsDir, "plans");

  let plansDirExists = true;
  try {
    const stat = Deno.statSync(plansDir);
    plansDirExists = stat.isDirectory;
  } catch {
    plansDirExists = false;
  }
  if (!plansDirExists) {
    console.error(`error: plans directory not found at ${plansDir}`);
    Deno.exit(1);
  }

  const planFiles = (await walkFiles(plansDir, { extensions: [".md"] }))
    .filter((p) => basename(p).toLowerCase() !== "readme.md");

  if (planFiles.length === 0) {
    console.error(`warn: no .md plan files found under ${plansDir}`);
    Deno.exit(0);
  }

  // Scan the whole .agents tree (plans, bugs, summaries, research, etc.) for
  // inbound links — a plan referenced from a bug report or another plan is
  // more load-bearing than one nobody points to.
  const allDocs = await walkFiles(agentsDir, { extensions: [".md"] });

  let roadmapItems: RoadmapItem[] = [];
  try {
    roadmapItems = await fetchRoadmapItems();
  } catch (err) {
    console.error(`warn: could not fetch roadmap wiki (${(err as Error).message}); scoring without roadmap-item-match term`);
  }

  const planPathSet = new Set(planFiles.map(normalizeForCompare));
  const inboundByPlan = new Map<string, Set<string>>();
  for (const p of planFiles) inboundByPlan.set(normalizeForCompare(p), new Set());

  for (const docPath of allDocs) {
    let content: string;
    try {
      content = await Deno.readTextFile(docPath);
    } catch (err) {
      console.error(`warn: could not read ${docPath}: ${(err as Error).message}`);
      continue;
    }
    const baseDir = dirname(docPath);
    for (const target of extractLinkTargets(content)) {
      const resolved = normalizeForCompare(resolve(baseDir, target));
      if (planPathSet.has(resolved) && resolved !== normalizeForCompare(docPath)) {
        inboundByPlan.get(resolved)!.add(docPath);
      }
    }
  }

  const plans: Plan[] = [];
  for (const filePath of planFiles) {
    let content: string;
    try {
      content = await Deno.readTextFile(filePath);
    } catch (err) {
      console.error(`warn: skipping unreadable file ${filePath}: ${(err as Error).message}`);
      continue;
    }

    const relPath = relative(repoRoot, filePath);
    const title = extractTitle(content, relPath);
    const statusText = extractStatusText(content);
    const { closed, reason, inProgress } = classifyStatus(statusText, content);
    const { weight: roadmapWeight, label: roadmapLabel } = extractRoadmapWeight(content);
    const resolvedBullets = countResolvedBullets(content);
    const inboundSet = inboundByPlan.get(normalizeForCompare(filePath)) ?? new Set<string>();
    const inboundFrom = [...inboundSet].map((p) => relative(repoRoot, p));
    const roadmapMatch = matchRoadmapItem(title, content, roadmapItems);

    const base = {
      path: filePath,
      relPath,
      title,
      statusText,
      closed,
      closedReason: reason,
      roadmapWeight,
      roadmapLabel,
      resolvedBullets,
      inProgress,
      inboundLinks: inboundFrom.length,
      inboundFrom,
      roadmapItemWeight: roadmapMatch?.weight ?? 0,
      roadmapItemMatch: roadmapMatch?.itemText,
      roadmapItemPhase: roadmapMatch?.phaseName,
    };
    plans.push({ ...base, score: computeScore(base) });
  }

  const open = plans.filter((p) => !p.closed).sort((a, b) => b.score - a.score);
  const closed = plans.filter((p) => p.closed).sort((a, b) => a.relPath.localeCompare(b.relPath));

  const shown = top ? open.slice(0, top) : open;

  if (json) {
    console.log(JSON.stringify({ repoRoot, plansDir, open: shown, closedCount: closed.length, closed }, null, 2));
    return;
  }

  console.log(`\nOpen plans ranked by impact (${open.length} open, ${closed.length} closed/excluded)`);
  console.log(
    `Formula: score = inboundLinks*10 + roadmapWeight*4 + roadmapItemWeight + resolvedBullets*1 + (inProgress ? 5 : 0)\n`,
  );
  if (roadmapItems.length === 0) {
    console.log(`(roadmap wiki unavailable this run — roadmapItemWeight is 0 for all plans)\n`);
  }

  const headers = ["#", "Score", "Roadmap", "RoadmapItem", "In", "Res.", "Prog", "Plan"];
  const rows = shown.map((p, i) => [
    String(i + 1),
    String(p.score),
    p.roadmapLabel,
    p.roadmapItemWeight > 0 ? p.roadmapItemWeight.toFixed(1) : "-",
    String(p.inboundLinks),
    String(p.resolvedBullets),
    p.inProgress ? "yes" : "-",
    `${p.title}  (${p.relPath})`,
  ]);

  const widths = headers.map((h, i) => Math.max(h.length, ...rows.map((r) => r[i].length)));
  const printRow = (cols: string[]) =>
    console.log(cols.map((c, i) => c.padEnd(widths[i])).join("  "));

  printRow(headers);
  printRow(widths.map((w) => "-".repeat(w)));
  for (const row of rows) printRow(row);

  if (shown.some((p) => p.roadmapItemWeight > 0)) {
    console.log(`\nMatched roadmap wiki items:`);
    for (const p of shown) {
      if (p.roadmapItemWeight > 0) {
        console.log(`  ${p.relPath}: [${p.roadmapItemPhase}] "${p.roadmapItemMatch}"`);
      }
    }
  }

  if (shown.some((p) => p.inboundLinks > 0)) {
    console.log(`\nInbound references (why a plan scored the way it did):`);
    for (const p of shown) {
      if (p.inboundLinks > 0) {
        console.log(`  ${p.relPath}:`);
        for (const from of p.inboundFrom) console.log(`    <- ${from}`);
      }
    }
  }

  if (closed.length > 0) {
    console.log(`\nExcluded as closed (status keyword matched): `);
    for (const p of closed) console.log(`  - ${p.relPath} [${p.closedReason}]`);
  }
}

if (import.meta.main) {
  await main();
}
