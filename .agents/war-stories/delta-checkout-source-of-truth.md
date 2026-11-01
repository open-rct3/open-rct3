# Case Study: Delta Checkout Source of Truth

## Failure
Changes were made in a Delta worktree and then reported as present in the user's main checkout. `merge_worktrees` merged within the Delta thread workspace; it did not update the user's local checkout. The user's `git status` showed only the unrelated `codebook.toml` modification.

## Durable Rule
When Delta identifies a specific local checkout, use that checkout directly for every read, write, status check, and verification. The Delta worktree is not the user's working set.

## Correct Recovery
1. Probe the identified checkout with read-only `git status`.
2. Copy or surgically apply only the requested files into that checkout.
3. Re-read the changed files and run `git status` there.
4. Report the exact checkout path and observed status. Preserve unrelated changes.

## Additional Incident

Plan and AGENTS.md edits were first made in the Delta worktree. A worktree merge was reported as if it changed the user's main checkout, but the user's git status showed only the unrelated codebook.toml edit.

The same rule applies to plans, project rules, memory, and war stories. Verify each result in the identified checkout before reporting it as saved.

## Full Failure Sequence

1. The plan was written in `docs/superpowers/plans/` instead of the repository convention `.agents/plans/`.
2. The plan used a date-prefixed filename even though existing plans use `<feature-name>.md`.
3. Edits were made in the Delta worktree despite the direct-checkout rule and the Open Code rule forbidding worktrees.
4. `merge_worktrees` was used and then incorrectly treated as a merge into the user's main checkout.
5. The assistant first reported that no merge occurred, then reported that the changes were already in the main checkout, then reported that the merge had completed there.
6. After the user supplied `git status`, the assistant incorrectly refused to edit the main checkout even though the identified checkout was accessible through the terminal.
7. The assistant created a second war story duplicating `delta-checkout-source-of-truth.md` instead of extending the existing case study.
8. The user had to branch the session while recovering from the repeated incorrect state reports.

## Required Guard

Before any edit, identify the checkout path. Run the first probe there, not in a Delta worktree. Use the checkout for every read, write, copy, move, status check, and verification. Treat `merge_worktrees` as a Delta-workspace operation, never as evidence that the user's checkout changed. Read the existing destination directory before creating a plan, memory, or war story.
