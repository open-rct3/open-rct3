# Style Guide

Code style conventions OpenRCT3 follows, briefly.

The guide is intentionally short. The goal is to provide consistency, not exhaustive rules. When something isn't covered here, match the style of the surrounding code.

## Table of Contents

- [File Headers](#file-headers)
- [Namespaces](#namespaces)
- [XML Doc Comments](#xml-doc-comments)
- [Commit Messages](#commit-messages)

## File Headers

Start each file with a descriptive one-liner about what the file does and the project's license boilerplate.

```cs
// Decodes static shape (`shs`) entries.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Concurrent;

// ...
```

> [!NOTE]
> **Rationale:** This is the only place a file-level comment is useful. C# `<summary>` doc comments cannot be attached to a file or namespace.

### File Description

Describe what the file does, not the file's class name.

Avoid:

```cs
// StaticShapes
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
```

The first line tells the reader nothing.

### Attribution

New files should **not** contain an author line.

**If you make significant changes to a file with an `Authors:` line, delete it.**

> [!NOTE]
> **Rationale:** source control already provides authorship and change history, e.g. `git blame` and pull request histories. A per-file
> author list goes stale immediately and duplicates that.

Avoid:

```cs
// Decodes static shape (`shs`) entries.
//
// Author: Chance Snow <git@chancesnow.me>
```

Current `Authors:` lines are kept for historical attribution, for example:

```cs
// Decodes static shape (`shs`) entries.
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
```

## Namespaces

Use **file-scoped namespaces**.

Using the file-scoped form keeps namespace declarations simple and avoids the extra indentation that block-scoped namespaces force on every type.

```cs
namespace OpenRCT3.Simulation;

public class Park {
  // ...
}
```

Do not use block-scoped namespaces:

```cs
// Avoid:
namespace OpenRCT3.Simulation {
  public class Park { ... }
}
```

Do not nest namespaces inside each other:

```cs
// Avoid:
namespace OpenRCT3 {
  namespace Simulation {
    public class Park { ... }
  }
}
```

Auto-generated `.designer.cs` files are exempt. They are owned by their generator (WinForms/Xamarin), and editing them by hand just creates merge conflicts the next time the designer runs.

## XML Doc Comments

Use `/// <summary>` on every public type and member.

Summarize in a single sentence what the type or member *does*, in present tense, starting with a verb. Use `<param>` to document parameters and `<see cref="…"/>` to cross-reference other types by name:

```cs
/// <summary>Create a flat quad on the XY plane (Z-up).</summary>
/// <param name="name">Name assigned to <see cref="Mesh.Name"/></param>
/// <param name="color">
/// Vertex color assigned to <see cref="Vertex.Color"/>. Defaults to <see cref="Colors.Transparent"/>.
/// </param>
public static Mesh Plane(string? name = null, Vector4 color = default);
```

### Remarks

Use `<remarks>` to document the *why* including, but not limited to, implementation rationale, edge cases, non-obvious invariants, and performance characteristics:

```cs
/// <summary>
/// Builds a world-space <see cref="Ray"/> for <paramref name="screenPos"/> analytically from
/// <paramref name="camera"/>'s eye, target, and field of view - not by inverting <see cref="Camera.Value"/>.
/// </summary>
/// <remarks>
/// The view-projection matrix's inverse is ill-conditioned in single-precision float at realistic
/// gameplay camera distances, since <see cref="Camera"/>'s far clip plane can sit hundreds of thousands
/// of times farther than its fixed 1cm near plane. Reconstructing the ray from the camera's own basis
/// vectors sidesteps that matrix inversion (and the near/far planes) entirely, so the ratio between them
/// never enters the computation.
/// </remarks>
public static Ray ToRay(this Camera camera, Vector2 screenPos, Vector2D<int> framebufferSize);
```

### See Also Links

For cross-project references (e.g. references to source files
in a related project), use `<seealso href="...">` with a permalink:

```cs
/// <summary>Decodes scenery item (`sid`) entries.</summary>
/// <seealso href="https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/include/sceneryrevised.h#L191">rct3-importer: SceneryItem_V</seealso>
public readonly record struct SceneryItem(...);
```

GitHub permalinks use the line fragment `#L<line>` for a single line, or `#L<start>-L<end>` for a range.

Do not nest `<seealso>` inside `<summary>`. Place it on the type or member declaration,
as a sibling of and after `<summary>` and `<remarks>`.

Use `href` (not `cref`) for external URLs, `cref` references code and doesn't create clickable links.

See the [Microsoft XML docs guidance](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags#seealso).

## Commit Messages

Follow the project's [`git-guidance`](../.agents/skills/git-guidance/SKILL.md) skill:

- Present-tense subject line, no periods, no trailing punctuation
- Limit the subject line to about 50 characters
- Prefix non-functional changes (formatting, comments, renaming) with `💅`
- Use Markdown bullets in the body
- Wrap the body at 72 characters

Example:

```md
💅 Link RCT3 Importer refs and standardize OpenCobra file headers

- Replace bare `rct3-importer` filename references with `<seealso href>` permalinks
  pinned to commit `431fbf2b5b5038c07ed197d29d12facdf319bc68`
- Standardize the file-level one-liner across all 16 files in `OpenCobra/OVL/Files/`
```
