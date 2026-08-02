# Contract — §1.14 language-change proposal template (US5)

Every GLP language change in this wave MUST be captured with this structure BEFORE implementation
(DISCIPLINE §1.14 / Constitution IV-a). Two instances: `abandon-operation`,
`nested-structure-head-matching`. Files land under `specs/062-.../proposals/`.

## Required sections
1. **Item** — name + roadmap slug.
2. **Motivation** — why the language needs this; the gap it closes.
3. **Exact semantics** — precise behaviour, three-phase (HEAD/GUARD/BODY) impact where relevant,
   reader/writer + SRSW implications. NO ambiguity.
4. **Authoritative source** — the citation the semantics are drawn from:
   - `abandon-operation`: FCP source (`kernels.c` / `emulate.c` — abandon/reader-writer cell
     behaviour). DISCIPLINE §1.13 reference. **Off-host — must be read from the sibling GLP repo.**
   - `nested-structure-head-matching`: typed-GLP manual + sibling GLP runtime spec.
5. **Type-system impact** — declarations/checker changes, if any.
6. **Runtime impact** — which `glp_runtime/lib` internals change; explicit statement that
   `_ClauseVar` / `_TentativeStruct` / fallback branches are **extended, not removed** (IV-b).
7. **Test plan** — positive + negative REPL cases (Sections A/C of `run_all_tests.sh`) and Dart
   unit coverage.
8. **Approval reference** — operator approval recorded 2026-07-29 (clarify session).

## State
`sourced → drafted → operator-approved(recorded) → implemented`. A semantic problem discovered at
any point → STOP and report (Bug/Language protocol); do not implement around it.
