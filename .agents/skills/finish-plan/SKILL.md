---
name: finish-plan
description: Close a completed prototype plan by archiving its findings into code, docs, and Roadmap. Use when a plan in .agents/plans/ reaches completion, or when a PR implements a plan (archived or not). Accepts the plan filename OR PR URL as an argument to avoid losing implementation notes, out-of-scope learnings, or other context.
argument-hint: "<plan-file> | <pr-url>"
todo: |
  ## Sorting Future Work

  - new architecture decisions and notes go in ./.agents/summaries/Architecture.md
---

# Finish Plan

Archive a completed plan's findings into the codebase, update `TODO.md`, and delete the plan file. This
preserves implementation notes, deferred work, and design rationale that would otherwise be lost when the
now-obsolete plan document is deleted.

## When to Apply

- **Plan file exists**: read it, extract findings, update `TODO.md`/source, delete the file.
- **Plan already archived, PR open**: no file to delete. Use the PR description (top comment, not individual
  commits) as source of truth — overview, decisions, known limitations. Run steps 2, 3, and 5 only.

## Procedure

### 1. Parse the plan (or PR)

Read the whole plan (or PR description + `git diff main...<branch>` to enumerate touched files) and organize:

- **Implemented**: phase structure, scope, files touched
- **Deferred**: Out-of-scope items, Future Work section
- **Decisions**: trade-offs and rationale that shaped the work
- **Gotchas**: edge cases, cross-file invariants, known limitations that aren't bugs
- **Testing**: which test files cover which behaviors
- **Dependencies**: what's blocked on what

### 2. Integrate implementation notes as `<remarks>`

**Extract `<remarks>` from every file the plan touched, not just the "main" one** — a 15-file plan needs
remarks in at least 8–10 (core implementation + integration files like `store.ts`/`scene.ts`; skip trivial test
stubs). Place remarks at file top for broad notes, on the specific member for narrow ones.

What to write: the current fact/invariant, self-contained, no reference to the plan file (it's being deleted).
No history ("this used to be", "we tried X but it failed") — state the design, not the failure it replaced.
Example: `<see cref="Scrub"/> must clamp phase to [0, 1-1e-6] to avoid rounding into the next day.` Prefer
extending an existing comment over adding a redundant one.

Checklist:

- [ ] Enumerated every file the plan touched (grep filenames, check task lists)
- [ ] Remarks on core implementation files and on integration entry points
- [ ] Remarks on files with cross-file invariants (floating-origin, GPU precision, etc.)
- [ ] No file still has a TODO/FIXME that isn't captured in a remark or `TODO.md`

### 3. Extract deferred work into `TODO.md`

For each Future Work / Out-of-Scope item: search `TODO.md` first — if already covered, skip it; otherwise add
a section/subsection with the work and its dependencies. `TODO.md` is the long-term home for this context, not
the plan file.

### 4. Delete the plan file

```bash
rm .agents/plans/<plan-file>
```

Commit the todo list updates, source remarks, and deletion together:

```
Archive time-weather prototype plan

- Update todo list: time-weather Phase 0–1 complete
- Integrate calendar and clock implementation notes into source files
- Extract Future Work dependencies into todo list
- Delete .agents/plans/time-weather.md
```

If the plan was already archived (PR-only case), commit just the remarks and `TODO.md` updates.

### 5. Verify complete coverage (always run this)

Headers-only extraction misses future-work language that isn't under an "Out of Scope"/"Known Debt" heading —
a decision bullet that ends "...belongs to a separate plan," or a stated Goal that quietly never resurfaces in
the final status with no deferral note. Close the gap every time, even when steps 2–3 felt thorough:

1. Get the full original plan text: `git diff --cached -- <plan-file>` (after staging the deletion) or
   `git show HEAD:<plan-file>`.
2. Grep it case-insensitively: `future|deferred|out of scope|separate (feature|plan)|not built|not yet|schedule for|blocked on|TODO`.
3. Confirm every match is tracked in `TODO.md` or a sibling plan file; add whatever's missing.
4. Re-check the plan's original **Goals** section specifically — a goal absent from the final status with no
   explanation is a silent drop, not an intentional cut. Flag it and add it to `TODO.md`.
5. Report what was already covered vs. newly added, and call out silent drops explicitly for confirmation.

## Anti-patterns

- **Remarks in only one file**: skipping integration files (`store.ts`, `scene.ts`) loses how pieces fit
  together. Enumerate all touched files before step 3.
- **Deleting before extracting**: six months later nobody can answer "why is `scrub()` clamped to
  `[0, 1-1e-6]`" because the reasoning lived only in the deleted plan.
- **Over-commenting**: one `<remarks>` per file on the key invariant is enough — don't restate the plan's
  entire narrative inline.
- **Stale `TODO.md`**: updating an item's status but not its tier/dependencies when Future Work shifted.
- **Skipping the PR description**: it's the source of truth for "implemented" vs. "deferred," not the
  granular, task-oriented commit messages.
- **Trusting only the labeled sections**: future work hides in decision asides and silently-dropped Goals too
  — that's what step 5's grep pass is for. Don't skip it because step 3 felt thorough.
