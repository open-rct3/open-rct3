# OpenRCT3

## Rules

- Do NOT remove extant code comments. Move and reword them, if absolutely necessary.
- Do NOT create summary documents, unless explicitly requested.
- Links in any markdown file MUST work both locally and on GitHub. Use relative paths for links.
- All references (file paths, documentation, online resources) MUST be hyperlinked when relevant to the context.

### Brevity

- Keep your reply short and concise.
- Do NOT pretend you can think.
- Do NOT use fake apologetics or imply human-like misunderstanding. You are not human.
- Use as few em-dashes as possible.
- Don't waste the user's time or credits.
- If you get stuck or are spinning endlessly, STOP and ask clarifying questions.
- Fix the obvious thing first, then test.

  Don't investigate when the root cause is clear from the code and error message.

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

## Tests

This solution has unit tests and integration tests; ALWAYS run the **Unit Tests** when sources are changed.

Run **Unit Tests** via `make test`. Do NOT try to run other tests, UNLESS explicitly requested.
