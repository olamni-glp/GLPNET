# Contract: tombstone file format

Source: spec FR-021, FR-022; clarification Q6; research R7.

## File location

```
.codeconv/tombstones/<rel-path>.dart.md
```

Where `<rel-path>` is the file's path relative to `glp_runtime_net/`, with `.dart` replaced by `.dart.md`. Subdirectory structure mirrors the source tree exactly.

Examples:

| Source `.dart` file | Tombstone path |
|---|---|
| `glp_runtime_net/heap_fcp.dart` | `.codeconv/tombstones/heap_fcp.dart.md` |
| `glp_runtime_net/runtime/cell.dart` | `.codeconv/tombstones/runtime/cell.dart.md` |
| `glp_runtime_net/multiagent/process.dart` | `.codeconv/tombstones/multiagent/process.dart.md` |

Orphaned tombstones live under `.codeconv/tombstones/.orphaned/<rel-path>.dart.md` (FR-025; checked in per FR-029).

## File structure

A tombstone is a Markdown document with YAML frontmatter delimited by `---`:

```markdown
---
<YAML frontmatter — see § Frontmatter schema>
---

<verbatim leading doc-comment block from the .dart file, or empty>
```

## Frontmatter schema

```yaml
path: <string>           # POSIX path relative to glp_runtime_net/. Required.
name: <string>           # Basename. Required.
purpose: <string>        # Verbatim doc-comment block, or "". Required (may be empty).
key_idea: <string>       # Same as purpose when single block, "" otherwise. Required.
dependencies: <list>     # Sorted POSIX paths. Required (may be empty list).
callers: <list>          # Sorted POSIX paths. Required (may be empty list).
mtime: <ISO-8601 UTC>    # File mtime at last discover. Required.
sha256: <lowercase hex>  # Content SHA-256 at last discover. Required.
```

### Field rules

- **`path`**: ALWAYS POSIX-style (forward slashes), even on Windows. Per R7.
- **`purpose` / `key_idea`**: YAML block scalar (`|`) for multi-line content. Empty string `""` when no doc-comment.
- **`dependencies` / `callers`**: YAML lists, each entry on its own line with `- ` prefix. Sorted lexically (stable diffs across runs).
- **`mtime`**: ISO-8601 with millisecond precision, suffixed `Z` (UTC). MUST match the format produced by PostgreSQL's default `timestamptz` text output via the `pglite_compat_loaders` patch.
- **`sha256`**: 64 hex characters, lowercase.

## Body

Everything after the second `---` is the body. The body is the verbatim leading doc-comment block from the `.dart` file with `///` markers stripped (or `/** */` block markers stripped per Dart convention), preserving internal whitespace and newlines.

If the source file has no leading doc-comment, the body is empty (the second `---` is followed by a single trailing newline).

## Round-trip invariant (SC-007)

A tombstone reading by `codeconv discover --from-tombstones` MUST reconstruct the same `dart_files`, `dart_imports`, `dart_callers` rows as a fresh-source `codeconv discover` run on identical source state. This requires:

1. Frontmatter is the SOURCE of truth for `--from-tombstones`. The body is informational only.
2. Tombstone writes by discover are deterministic (same inputs → byte-identical output). Specifically: list ordering is sorted lexically; YAML emitter settings (indent, quoting style, line width) are pinned.

## Diff stability

Tombstones live in git (FR-029). To keep diffs minimal across discover runs:

- Stable field ordering: `path, name, purpose, key_idea, dependencies, callers, mtime, sha256`.
- `dependencies` and `callers` lists sorted lexically.
- YAML emission options pinned (recommended: PyYAML `default_flow_style=False, sort_keys=False, allow_unicode=True, width=10000`).
- `mtime` is the noisy field — every source mtime change touches one tombstone. This is acceptable.

## Hand-edits

Per Edge Cases in spec.md: hand-edits to `purpose`, `key_idea`, or the body are NOT preserved across re-runs. Discover overwrites them based on the current source. Engineer-curated semantic content is reserved for the future enrichment tool (out of scope).

## Worked example

Source file `glp_runtime_net/runtime/cell.dart`:

```dart
/// FCP-style heap cell representation.
///
/// Each cell carries a tag (WrtTag, RoTag, etc.) plus payload.
/// See docs/heap/heap-pointer-architecture-spec.md.

import 'tag.dart';
import '../bytecode/opcode.dart';

class Cell { ... }
```

Generated tombstone `.codeconv/tombstones/runtime/cell.dart.md`:

```markdown
---
path: runtime/cell.dart
name: cell.dart
purpose: |
  FCP-style heap cell representation.

  Each cell carries a tag (WrtTag, RoTag, etc.) plus payload.
  See docs/heap/heap-pointer-architecture-spec.md.
key_idea: |
  FCP-style heap cell representation.

  Each cell carries a tag (WrtTag, RoTag, etc.) plus payload.
  See docs/heap/heap-pointer-architecture-spec.md.
dependencies:
  - bytecode/opcode.dart
  - runtime/tag.dart
callers:
  - runtime/heap_fcp.dart
  - runtime/runner.dart
mtime: '2026-04-30T11:14:22.000Z'
sha256: 7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b
---

FCP-style heap cell representation.

Each cell carries a tag (WrtTag, RoTag, etc.) plus payload.
See docs/heap/heap-pointer-architecture-spec.md.
```
