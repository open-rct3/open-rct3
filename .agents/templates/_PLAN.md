# <Feature or Change Name>

<!--
Bare-bones plan template. Modeled on the recurring shape of existing plans in .agents/plans/
(e.g. features/terrain-heightmap.md, features/path-network.md, features/ein.md,
features/terrain/water-tool.md, features/scenery-placement-registry.md).

Delete this comment block and any sections that don't apply. Not every plan needs every
section below — a small plan can be Context + Goals + Status; a large one grows the rest as
decisions get made. Keep headings in this order when present, since readers skim in this order.
-->

## Context

Why this plan exists: the current state, the gap it closes, and links to anything it depends on
or was researched from (prior plans, `.agents/research/`, `.agents/summaries/`).

## Goals

What this plan actually decides, as a bulleted list of concrete choices — not aspirations. Each
bullet should be a decision someone could implement from directly, including the reasoning behind
non-obvious calls (why this shape and not the more obvious one). Confirmed facts from the user or
in-game observation are worth flagging inline (`**Confirmed (user, ...)**:`) so later edits know
which parts are load-bearing vs. inferred.

<!--
If Goals sketches type/struct shapes in pseudo-code, that pseudo-code is the contract only —
don't write out full XML doc summaries/remarks inline. Note once, near the pseudo-code, that the
implementation step adds real XML doc comments on every public type and member (per the
codebase's existing convention, e.g. StaticShapes.cs, TerrainTypes.cs) as a given, not something
that needs to be spelled out signature-by-signature in the plan.
-->

## Gaps and Risks

<!-- Optional: only for plans substantial enough to warrant a pre-execution review pass. -->

Problems found reviewing the plan before execution — wrong assumptions, missing pieces, unverified
guesses. Number them; mark each resolved/open as it's addressed, rather than deleting the entry,
so the plan keeps a record of what almost went wrong.

## Open Questions

Things this plan deliberately does not resolve, with enough context that a future reader
understands why the question is still open rather than just that it exists.

## Deferred

Work that depends on data models or systems that don't exist yet. State what this plan does to
avoid foreclosing that future work (e.g. "the API shape leaves room for X"), not just that it's
deferred. Also notes relevant work that is out-of-scope for this plan.

## Testing

What this plan will cover with unit/integration tests, called out per untested area it touches —
not just the new code it adds. Existing untested code this plan modifies (especially in
`OpenCobra/OVL` and `OpenCobra/GDK`) needs coverage added alongside the change, per
[`AGENTS.md`](../../AGENTS.md). List concrete cases (known-good, edge, failure) rather than
"add tests for X".

## Implementation Notes

<!-- Fill in during/after implementation, not during initial planning. -->

Where this landed in the codebase (file links), and any deviations from the Goals above — each
deviation stated plainly with the reason, not silently reconciled.

## Status

One paragraph: what's implemented, what's tested (link the test file / test count), what's
explicitly not implemented yet. Update this as the plan progresses rather than leaving it stale —
it's the section a future reader checks first to know whether the plan is still live.
