# Contract — Conversion-Subtree Recognition (FR-008, SC-006)

Acceptance surface for FR-008 ("recognized as a first-class subtree of the Dart→Gleam conversion
data flow WITHOUT modifying any inventory/structure stage-tool source") and SC-006 ("0 codeconv
stage-tool source files changed"). Grounded in research.md R-004.

## The recognition mechanism (already exists from F2)

| Concern | Carried by | NOT hardcoded in |
|---------|-----------|------------------|
| Active language pair | `codeconv.workspace_settings` → `resolve_workspace_pair(...)` | stage-tool source |
| Scaffold target root | `codeconv.workspace_settings` key `target_rel_root` (read by `tools/scaffold/workflow.py`) | stage-tool source |
| Mirror output root | `codeconv.workspace_settings` key `output_rel` (read by `tools/mirror/workflow.py`) | stage-tool source |
| Dart→Gleam hooks | the **registered** `dart_gleam` langpair package (F2) | — |

Because the pair and the conversion roots are **configuration** written by `codeconv init`, pointing
the pipeline at the Gleam data flow is an `init`/config action — **never** a code edit to
`init` / `discover` / `scaffold` / `mirror`.

## Obligations on F3

1. **Placement** — `glp_gleam/` exists at the repo root as a first-class sibling subtree (see
   project-layout.md), mirroring how `glp_runtime_net/` and `out/csharp/` sit for the C# pipeline.
2. **Recognized-level integration only** — F3 delivers "recognized + build/test green". It does NOT
   wire deep pipeline runs; those land with the heavy port features (spec Assumptions, narrowed
   2026-06-24).
3. **Committed-source boundary** — `glp_gleam/` is committed, hand-authored source. The codeconv
   Dart→Gleam mirror's companion/tracker tree (`.gleam`/`.ana`/… + `codeconv-gleam-tracker.json`)
   sits **alongside** and does **not** generate `glp_gleam/`. *(spec Clarification 2026-06-24)*

## Invariants

1. **Zero stage-tool source change** — `git diff --name-only <base>..HEAD` contains **no** path under
   `codeconv/src/codeconv/tools/{init,discover,scaffold,mirror}/`. *(SC-006)*
2. **No new langpair code** — F3 adds no file under `codeconv/src/codeconv/langpairs/` (the
   `dart_gleam` pair already exists from F2). Recognition is config + placement only.
3. **F2 boundary preserved** — the plugin boundary (new pair = package + one registry line + config,
   zero stage-tool edits) is unchanged. *(US3 AS-2)*

## Verification

- `git diff --name-only <merge-base>..HEAD -- codeconv/src/codeconv/tools/` → empty.
- `git diff --name-only <merge-base>..HEAD -- codeconv/src/codeconv/langpairs/` → empty.
- Any demonstration that the pipeline *sees* the subtree is performed via `codeconv init`/config,
  with the above diffs still empty. *(FR-008, SC-006)*
