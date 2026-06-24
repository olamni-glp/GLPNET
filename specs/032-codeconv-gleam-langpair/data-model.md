# Phase 1 Data Model: codeconv Gleam langpair (Dart→Gleam)

This feature adds no database schema, no migration, and no new persisted entity.
The "data model" here is the **hook-value table** for the `dart_gleam` pair (the
concrete realization of the 016 `LangPair` protocol) plus the normalization
rule. All values are pure functions of their inputs (FR-009).

## Entities (from spec § Key Entities)

| Entity | Realization | Persistence |
|---|---|---|
| Dart→Gleam language pair | `DartGleam(LangPair)` in `langpairs/dart_gleam/__init__.py` | In-memory registry singleton (process-wide) |
| Language-pair registry | existing `langpairs._REGISTRY` gains `("dart","gleam")` | In-memory; populated via `_PRODUCTION_PAIR_MODULES` auto-import |
| Workspace pair binding | existing `codeconv.workspace_settings` rows `source_lang`/`target_lang` | PGLite `.pgdb/` (unchanged schema) |
| Target tree artifacts | per source: `.gleam` target + verbatim-preserved source + 9 companions + root tracker | Filesystem (produced by the unchanged scaffold/mirror stages) |

## `dart_gleam` hook-value table

`key()` → `("dart", "gleam")`

### Source side (identical-in-result to `dart_csharp`; delegates to `tools/discover`)

| Hook | Value / behavior |
|---|---|
| `source_extensions()` | `(".dart",)` |
| `tool_exclusion_globs()` | derived from `discover.walker` `_EXCLUDED_SEGMENTS` + `_GENERATED_SUFFIXES` (same as `dart_csharp`) |
| `read_package_name(root)` | delegate → `discover.pubspec.read_package_name(root, repo_root=None)` |
| `extract_imports(f, root, pkg)` | delegate → `discover.parse.extract_imports` |
| `extract_leading_doc(f)` | delegate → `discover.parse.extract_leading_doc` |

### Target side (the Gleam-specific part)

| Hook | Value / behavior |
|---|---|
| `target_extension()` | `".gleam"` |
| `target_for(source_rel)` | POSIX-normalize separators; split into segments; swap `.dart`→`.gleam` on the basename; **normalize each segment** to a legal Gleam module segment (rule below); rejoin with `/`. Directory structure mirrored verbatim — no `src/glp/` prefix (F3 owns layout). |
| `workdir_name(source_rel)` | `"__" + basename-without-ext` (D2NET parity; same as `dart_csharp`) |

### Mirror side

| Hook | Value |
|---|---|
| `mirror_prune_segments()` | `(".dart_tool","build","archive","backup",".git",".idea",".vscode")` |
| `preserved_source_suffix()` | `""` (verbatim — `discover` is `.dart`-based) |
| `companion_extensions()` | `(".gleam",".ana",".tst",".con",".dep",".cgn",".iss",".sta",".ver")` (order fixed — FR-010) |
| `companion_stub_comment(ext, base)` | `f"// TODO: {category} ({ext}) — port from {base}"` where `category` maps `gleam→"Gleam source"` and the other eight as in `dart_csharp` (`ana→analysis`, …). Gleam `//` line comment. |
| `tracker_filename()` | `"codeconv-gleam-tracker.json"` (pair-defined) |

## Gleam module-segment normalization rule (FR-003 / FR-008 / SC-004)

`_GLEAM_SEGMENT_RE = re.compile(r"^[a-z][a-z0-9_]*$")`
`_GLEAM_RESERVED` = pinned Gleam 1.17.0 keyword set (see research.md R-002).

`normalize_segment(seg) -> str`:
1. If `_GLEAM_SEGMENT_RE.match(seg)` **and** `seg not in _GLEAM_RESERVED` → return
   `seg` unchanged (FR-003 AS-2: legal names preserved).
2. Else: `s = "".join(c.lower() if c.isascii() and c.isalnum() else "_" for c in seg)`
   (1:1 char map: ASCII letters→lower, digits kept, everything else → `_`).
3. If `s == "" or not s[0:1].isalpha()` (i.e. not a `[a-z]` start) → `s = "g_" + s`.
4. If `s in _GLEAM_RESERVED` → `s = s + "_"`.
5. Return `s`.

Properties: deterministic, pure, output always matches `^[a-z][a-z0-9_]*$` and is
non-reserved (SC-004 — 100% legal segments). **Not injective** (see research.md
R-003 pigeonhole) — collision handling is the R-003 owner decision.

### Worked examples

| source_rel | target_for → |
|---|---|
| `lib/runtime/heap_fcp.dart` | `lib/runtime/heap_fcp.gleam` (all legal — identity + ext swap) |
| `bin/main.dart` | `bin/main.gleam` |
| `lib/Foo.dart` | `lib/foo.gleam` (uppercase normalized) |
| `lib/2d_grid.dart` | `lib/g_2d_grid.gleam` (leading digit → `g_` prefix) |
| `lib/my-mod.dart` | `lib/my_mod.gleam` (hyphen → `_`) |
| `lib/type.dart` | `lib/type_.gleam` (reserved word → suffix `_`) |
| `lib\\a.dart` | `lib/a.gleam` (Windows sep → POSIX) |
| `README` | `readme.gleam` (no source ext → ext appended; basename normalized) |
| **collision (R-003)** `lib/Runner.dart` + `lib/runner.dart` | both → `lib/runner.gleam` — detection per the R-003 decision |

## State transitions

None. The pair is stateless; the workspace binding state machine
(unset → bound → mismatch-refused) is the existing 016 `resolve_workspace_pair`
behavior, unchanged.
