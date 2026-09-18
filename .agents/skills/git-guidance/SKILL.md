---
name: git-guidance
description: Generate concise commit messages for staged changes. Use this whenever the user asks to draft, rewrite, or polish a git commit message.
---

# Git Guidance

Use this skill when drafting Git commits for this repository.

## Commit Messages

### Required formatting rules

- Describe each change beginning with a descriptive _present tense_ verb
- Limit the subject line to about 50 characters
- Do NOT use periods in subject lines nor for bullet points
- Use GitHub Flavored Markdown (GFM) for bullets and other formatting in commit bodies
- Use backticks when referencing named code constructs, e.g. `paramName` or `SymbolName`
- Prefix purely non-functional changes with `💅` in bullet lists

### Scope and usage

- Summarize only what is staged
- Keep the subject line concise and action-oriented
- Write a body ONLY when the changes are extensive, i.e. an adaquate description would _not_ fit as a single-line summary
- Use bullets in the body when multiple change groups exist
- Use `💅` only when a bullet is strictly non-functional (formatting, comments, naming polish, docs wording with no behavior change)
- Do not use `💅` for bullets that include any functional behavior change

### Examples

#### Single-Line Messages

- Add `DrawNode` traversal for scene rendering
- `💅` Reformat `ShaderProgram` constants for consistent alignment
- `💅` Reformat sources
  (i.e. when _many_ of the repo's sources are reformatted)
- Rename `BuildMesh` to `BuildIndexedMesh` for clarity

#### Long Messages

```md
Enhance rendering pipeline, Scaffold scenario editor, ...

- Add initial Scenario Editor scaffold, including supporting project, platform, and config updates
- Update Scenario Editor plan

## Rendering Engine Refactor

Rework OpenGL renderer, context, and surface flow around draw nodes, matrix/color helpers, and explicit OpenGL error handling.
```
