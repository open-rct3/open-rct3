# OpenRCT3

## Rules

- NEVER run any `git` command — including read-only or seemingly-harmless ones like `git stash`, `git stash pop`, `git add`, `git status`, `git diff` — without first stopping and asking the user for explicit permission for that specific command. This applies even mid-task, even to verify something, even if it seems reversible. Ask first, every time, no exceptions.
- Do NOT remove extant code comments. Move and reword them, if absolutely necessary.
- Do NOT create summary documents, unless explicitly requested.
- Adapt working examples end-to-end, rather than inferring or deriving file formats from scratch. Reference implementations (e.g. `rct3-importer`'s `libOVLng`, `rct3tex.cpp`) already solve these formats correctly - port their logic directly instead of reverse-engineering it from raw byte dumps.
- Read reference implementations FIRST. Do NOT guess at clumsy changes or trial-and-error before understanding a problem. If a reference implementation exists for the code you're touching, read the relevant source before writing a single line - not after a guess fails.
- Links in any markdown file MUST work both locally and on GitHub. Use relative paths for links.
- All references (file paths, documentation, online resources) MUST be hyperlinked when relevant to the context.
- When you need to know a third-party library's API (e.g. an enum member or method overload), look it up via its docs/source on the web (WebFetch/WebSearch) FIRST. Do NOT dig through decompiled DLLs, `strings`, or reflection/MetadataLoadContext gymnastics to reverse-engineer an API that's already documented online.
- Always increase test coverage, not just maintain it. Any plan or change that touches untested code — especially in `OpenCobra/OVL` and `OpenCobra/GDK` — MUST add unit tests for the code paths it touches, not just for newly-added code.

### Brevity

- Keep your reply short and concise.
- Do NOT pretend you can think.
- Do NOT use fake apologetics or imply human-like misunderstanding. You are not human.
- Use as few em-dashes as possible.
- Don't waste the user's time or credits.
- If you get stuck or are spinning endlessly, STOP and ask clarifying questions.
- Fix the obvious thing first, then test.

  Don't investigate when the root cause is clear from the code and error message.

### Planning Workflow

For any task requiring multiple steps:

1. **Write the plan first**: Decompose the full task into concrete steps before touching any code. Include file paths, dependencies between steps, and estimated complexity.
2. **Get approval before executing**: Present the plan in a markdown checklist format and wait for feedback. Only proceed after human confirmation, or modify the plan based on feedback.
3. **Execute step-by-step**: Mark tasks as you complete them. If you discover the plan is wrong mid-execution, pause and ask for clarification rather than improvising.
4. **When stuck**: If a step blocks or you're uncertain, ask a clarifying question. Do not guess at architecture or deviate from the plan without explicit permission.

### C#

- Prefer `var` declarations over those with explicit types
- Use `Convert.*` instead of raw number casts
- Prefer single-line clauses with `foreach` and `if` statements, e.g.
  `if (!File.Exists(ConfigPath)) return new AppConfig();`
  - If the line is longer than 90 characters, wrap simple clauses like this:
    ```csharp
    if (!File.Exists(ConfigPath))
      return new AppConfig();
    ```
- In `OpenCobra/OVL/OVL.cs`, do not use potentially unbounded `while` loops or plain `for` loops; prefer `foreach`, LINQ, or bounded helper methods.
- In `OpenCobra/OVL/OVL.cs`, do not rewind `BaseStream.Position`; parse forward only.
- Do NOT use hex literals in `Color.FromArgb`; use whole ints, e.g. `Color.FromArgb(25, 118, 210)`.
- In NUnit tests, use `Assert.Throws<T>(new Action(() => ...))` instead of the obsolete `TestDelegate`.

## Tests

This solution has unit tests and integration tests; ALWAYS run the **Unit Tests** when sources are changed.

Run **Unit Tests** via `make test`. Do NOT try to run other tests, UNLESS explicitly requested.
