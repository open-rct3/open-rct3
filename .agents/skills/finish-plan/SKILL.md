---
name: finish-plan
description: Close a completed prototype plan by archiving its findings into code, docs, and Roadmap. Use when a plan in .agents/plans/ reaches completion, or when a PR implements a plan (archived or not). Accepts the plan filename OR PR URL as an argument to avoid losing implementation notes, out-of-scope learnings, or other context.
argument-hint: "<plan-file> | <pr-url>"
todo: |
  ## Sorting Future Work

  - new architecture decisions and notes go in ./.agents/summaries/Architecture.md
---

# Finish Plan

When a prototype plan reaches completion, archive its findings into the codebase, update the todo list, and delete the plan file (if it still
exists). This preserves implementation notes, out-of-scope decisions, and other
context that would otherwise be siloed in the now-obsolete plan document.

## When to Apply This Skill

- **Plan file exists**: The traditional case. Read the plan, extract findings,
  update todo list/docs, delete the plan file.
- **Plan already archived, PR is open**: The plan file was already deleted
  during development, but a PR now contains all the implementation. Use the PR
  as the source of truth: read the PR description (overview, what was
  implemented, known limitations) and review commits to understand what files
  changed and why. Apply steps 2–4 (todo list updates, <remarks> documentation,
  todo list extraction) without step 6 (no plan file to delete).

## Procedure

### 1. Parse the plan file or PR

**If the plan file exists:** Read it thoroughly and identify the sections below.

**If the plan file is archived but a PR exists:** Read the PR description (the
comment at the top of the PR thread, not individual commits). It should include:

- Overview of what was implemented
- Architecture decisions and rationale
- Known limitations
- Commit history and the files that changed

You can also use `git log <branch>...main` or review the PR diff
(`git diff main...<branch>`) to enumerate all touched files.

For either source, identify and organize:

- **What was implemented**: Phase structure, scope, files touched
- **What was deferred**: Out-of-scope items and Future Work section
- **Key decisions**: Trade-offs, design rationale, constraints that shaped the
  work
- **Implementation notes**: Gotchas, edge cases, cross-file interactions, known
  limitations that aren't bugs but will matter to future work
- **Testing approach**: Which test files validate which behaviors
- **Dependencies and blockers**: What's waiting on what, which systems are
  needed for next steps

### 2. Integrate implementation notes into source files

**CRITICAL: Extract <remarks> from EVERY file the plan touched, not just a few.**
Enumerate all files (test files, implementation files, integration files) and
add remarks to at least the core ones. A plan touching 15 files needs remarks in
at least 8–10 of them (skip trivial test stubs, but do not skip integration
files like store.ts or scene.ts).

For each file modified by this plan, add `<remarks>` comments capturing
non-obvious behavior, invariants, or gotchas. These remarks become the permanent
record once the plan document is deleted.

**Where to place `<remarks>`:**

- At the top of a file (below imports, above the first function/class) if the
  note applies broadly
- On the specific function/variable where the note matters most
- In a comment block scoped to a particular section if it's a cross-function
  invariant

**Checklist before moving to step 4:**

- [ ] Scanned plan file for ALL files it touched (grep for filenames, check
      tasks for file lists)
- [ ] Added <remarks> to core implementation files (data structures, algorithms,
      GPU code)
- [ ] Added <remarks> to integration files (store.ts, scene.ts, render pipeline
      entry points)
- [ ] Added <remarks> to files with cross-file invariants or floating-origin/GPU
      precision concerns
- [ ] Verified no file has a "TODO" or "FIXME" that wasn't captured in a remark
      or todo list note

**What to write in `<remarks>`:**

- The actual _fact_ or _decision_, self-contained and not referencing the plan
  file (which will be deleted). No "See the plan for details" — the remark must
  stand alone.
- Not the history (no "this used to be", "the original approach", "we tried X
  but it failed"; see AGENTS.md's Documentation rules)
- Example:
  `<remarks>
  <see cref="Calendar.DayPhase"/> is derived from ticks via cosine curve to
  keep brightness smooth at boundaries, not via linear ramp.
  </remarks>`
  (states the design) rather than "we got visual artifacts with linear so
  switched to cosine" (states the failure)
- Include constraints that would surprise a reader, e.g.
  `<remarks>
  <see cref="Scrub"/> must clamp phase to <c>[0, 1-1e-6]</c> to avoid rounding into
  the next day from user scrubber interaction.
  </remarks>`

**Follow Documentation rules:**

- No changelog/history narratives
- No commentary on past bugs or workarounds unless the workaround is _still in
  effect_ (then explain why it's needed, not how we discovered it)
- Prefer existing comments over redundant new ones; if the code is already
  clear, the remark might go in a more complex upstream function instead
- **Most importantly**: Do not reference the plan file in remarks — remarks are
  meant to survive the plan's deletion and stand as permanent documentation

### 3. Extract out-of-scope work into `TODO.md`

Find the plan's Future Work / Out-of-Scope section. For each item:

- **Check if it's in the todo list already**: Search
  `TODO.md` for mentions. If the todo list already covers
  it, no action needed.
- **If not already in the todo list**: Add a section or subsection to the
  todo list capturing the out-of-scope work and its dependencies. Example: if
  the plan deferred "real sun/sky lighting blocked on lighting model",
  ensure `TODO.md` includes mention of that.
- **Link back**: `TODO.md` is the long-term home for this context, not the plan file.

### 4. Delete the plan file (if it still exists)

**If the plan file exists:** Delete it:

```bash
rm .agents/plans/<plan-file>
```

Then commit the changes (todo list updates, source code remarks, todo list
changes, and the plan deletion) in a single commit with a clear message.
Example:

```
Archive time-weather prototype plan

- Update todo list: time-weather Phase 0–1 complete
- Integrate calendar and clock implementation notes into source files
- Extract Future Work dependencies into todo list
- Delete .agents/plans/time-weather.md
```

**If the plan file is already archived:** Commit just the todo list updates,
source code remarks, and todo list changes. Example:

```
Integrate terrain prototype documentation into source code

- 💅 Add @remarks to core terrain files (mesh, culling, grading)
- 💅 Add @remarks to integration files (toGpu, EaseScheduler)
- Improve slope visualization logic for edge cases
```

## Anti-patterns (things that wasted time before)

- **Extracting remarks from only one file**: A plan may touch 15+ files
  (implementation, tests, integration, shaders). Extracting remarks from only
  the "main" file and skipping integration files like `store.ts` or `scene.ts`
  loses critical context about how pieces fit together. **Enumerate all files
  the plan touched before starting step 3.** Check task file lists, grep the
  plan for filenames, scan commits. Add remarks to at least 8–10 files for a
  substantial plan.
- **Losing context**: Deleting the plan file without first extracting its
  findings. Six months later, a developer asks "why is scrub() clamped to [0,
  1-1e-6]?" and you can't answer because the reasoning was in the plan you
  deleted.
- **Over-commenting**: Adding multi-paragraph remarks for every function the
  plan touched. A single `<remarks>` per file pointing to the key invariant is
  enough; don't duplicate the plan's entire narrative into inline comments.
- **Leaving stale `TODO.md` entries**: Updating the Status for the plan's own doc
  but forgetting to update the Tier rankings if Future Work shifted or
  dependencies changed. Keep the todo list internally consistent.
- **Skipping the PR description**: When a plan is archived, the PR comment (the
  initial description at the top of the PR, not individual commit messages) is
  the source of truth for "what was implemented" and "what was deferred". Read
  it thoroughly before drilling into commits. Commit messages are granular and
  task-oriented; the PR description gives you the big picture.
