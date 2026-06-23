# Implementation Plan: codeconv Gleam langpair (Dart→Gleam)

**Branch**: `032-codeconv-gleam-langpair` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/032-codeconv-gleam-langpair/spec.md`

## Summary

Add a second production language pair `(source="dart", target="gleam")` to the
codeconv toolchain, alongside the existing `(dart, csharp)` pair, so the
inventory/structure stages (`discover`, `scaffold`, `mirror`) can target Gleam.
The pair satisfies the existing language-pair plugin contract
(`specs/016-codeconv-init-scaffold-langpair/contracts/langpair_plugin_contract.md`)
in full. Technical approach: a new `codeconv/src/codeconv/langpairs/dart_gleam/`
package (identity + source/target/mirror modules) plus exactly one auto-import
line in `langpairs/__init__.py`. The **source side reuses the proven Dart
delegation** (byte-faithful to `tools/discover`, identical in result to
`dart_csharp`); the **target side** swaps the extension to `.gleam` and mirrors
the Dart directory structure verbatim while normalizing each path segment to a
legal Gleam module segment; the **mirror side** reuses the Dart prune set and
verbatim-preserved source, with the `.gleam` companion replacing `.cs`, Gleam
`//` comment stubs, and a pair-defined tracker filename. No inventory/structure
stage-tool source is modified (Extensibility proof, FR-005/SC-003).

## Technical Context

**Language/Version**: Python 3.14 (codeconv harness; runs under `codeconv/.venv`).
**Primary Dependencies**: stdlib only for the pair hooks (`pathlib`, `re`,
`typing`); no new runtime dependency. The pair plugs into the existing
`codeconv.langpairs` registry + the `tools/{discover,scaffold,mirror}` stages
(unchanged). Tests use `pytest` (no bridge — hooks are pure).
**Storage**: N/A for the pair hooks (pure, filesystem-read at most — FR-009). The
stages they feed read/write the shared PGLite `.pgdb/` cluster, but the pair adds
no schema and no migration.
**Testing**: `pytest` in `codeconv/tests/` (`--test-concurrency=1`); new
`test_langpair_dart_gleam.py` (pure unit, no `@needs_bridge`). The existing
`test_langpair_registry.py` is the regression oracle for zero stage-tool drift.
**Target Platform**: developer/CI host (Windows/Linux) running the codeconv CLI.
**Project Type**: single project — a pluggable extension to the existing Python
`codeconv` toolchain (the language-pair plugin boundary from feature 016).
**Performance Goals**: N/A (per-file pure string mapping; the structure stages'
performance is unchanged — the pair adds O(segments) normalization per path).
**Constraints**: hooks MUST be pure / side-effect-free (FR-009); structure-stage
output MUST be deterministic / stable-ordered (FR-010); zero stage-tool edits
(FR-005/SC-003); default `(dart, csharp)` behavior unchanged (FR-006/SC-002);
only Gleam-legal module segments emitted (FR-003/FR-008/SC-004).
**Scale/Scope**: one new ~4-module package + one registry line + one test module.
The authoritative source tree is Dart `glp_runtime/` (per F1 dossier §6); the C#
path must remain byte-for-byte unaffected.

**Resolved unknowns** (see `research.md`):
- **R-001 source-side reuse strategy** — RESOLVED: `dart_gleam/source_dart.py`
  independently delegates to the same `codeconv.tools.discover.{parse,pubspec,walker}`
  single-source-of-truth (mirrors how `dart_csharp/source_dart.py` is itself a
  thin delegate), rather than importing `dart_csharp.source_dart` (avoids
  inter-pair coupling) or copying parser logic (DISCIPLINE §1.3 forbids).
- **R-002 Gleam target conventions** — RESOLVED: extension `.gleam`; module
  segment rule `[a-z][a-z0-9_]*` minus reserved words; companion comment syntax
  `//` (Gleam line comment — no block comments in Gleam); tracker filename
  `codeconv-gleam-tracker.json` (pair-defined; the C# pair keeps the legacy
  `d2net-tracker.json` literal for fidelity).
- **R-003 NORMALIZATION + COLLISION (the load-bearing decision)** — see
  research.md R-003 and **Complexity Tracking** below. Recorded as a NEEDS-OWNER
  decision surfaced to `/bk-analyze`; default carried into tasks is the
  zero-stage-edit option (corpus-level collision guarantee), with a recommended
  alternative (a generic planner uniqueness seam) requiring a narrow SC-003
  carve-out.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*
Constitution `v1.1.0` (`.specify/memory/constitution.md`).

| Principle | Verdict | Note |
|---|---|---|
| I. Spec-First | PASS | Plan quotes spec FR-001..FR-011 + the 016 contract verbatim; no code without spec. The FR-005↔FR-008↔FR-003 tension is surfaced (not worked around) per the Bug-Protocol. |
| II. Bug-Protocol / No-Workarounds | PASS (with flag) | The collision tension is reported, not patched with try/except. No robustness-as-workaround introduced. |
| III. SRSW | N/A | No GLP clauses; pure Python plumbing. Scan: zero `skipSRSW` in artifacts. |
| IV-a. Language Authority | PASS | No GLP language change (spec Assumption: "No new language primitives or GLP semantics"). Gleam is a conversion *target*, not a GLP extension. |
| IV-b. Preserve Working Internals | PASS | `dart_csharp`, the registry, and all stage tools are untouched. |
| V. Claude-Only LM / No External API | PASS | No LM in the loop; pure deterministic string mapping. Scan: zero `OPENAI_API_KEY`/`litellm`/`openai` in artifacts. |
| VI-a. Additive/Idempotent Migrations | PASS | No migration added; head stays `0010`. |
| VI-b. Single PGLite Cluster | PASS | No new cluster; the pair touches no DB. |
| VII. Test-Gated, Commit-Scoped | PASS | Baseline-green-before/after discipline in tasks; commit only `langpairs/dart_gleam/`, the one `__init__.py` line, the new test, and `specs/032/`. |
| VIII. Single Source of Truth | PASS | Source side delegates to `tools/discover` (no parser copy); the 016 contract remains authoritative; this plan references, does not duplicate. |

**Initial gate: PASS.** One judgement-gate-able item is *flagged* (the FR-005↔FR-008
tension under Principle I/II) — recorded in Complexity Tracking and routed to
`/bk-analyze` + the owner decision gate before `/bk-implement`. No principle is
violated by the plan itself.

**Post-Phase-1 re-check: PASS** (no new violation introduced by the design
artifacts; the flagged tension is unchanged and documented).

## Project Structure

### Documentation (this feature)

```text
specs/032-codeconv-gleam-langpair/
├── plan.md              # This file (/bk-plan)
├── research.md          # Phase 0 — R-001..R-003 decisions
├── data-model.md        # Phase 1 — dart_gleam hook-value table + normalization rules
├── quickstart.md        # Phase 1 — bind workspace to (dart,gleam) + run/verify
├── contracts/
│   └── dart_gleam_hooks.md   # Phase 1 — concrete hook values (delta over the 016 contract)
├── checklists/
│   └── requirements.md  # (pre-existing, from /bk-checklist)
└── tasks.md             # Phase 2 (/bk-tasks — NOT created here)
```

### Source Code (repository root)

```text
codeconv/src/codeconv/langpairs/
├── __init__.py                 # +1 auto-import line: "codeconv.langpairs.dart_gleam"
│                               #   (the ONLY edit outside the new package — FR-005)
├── base.py                     # UNCHANGED (LangPair protocol + UnknownLangPair)
├── dart_csharp/                # UNCHANGED (regression oracle for FR-006/SC-002)
└── dart_gleam/                 # NEW package (all pair logic lives here)
    ├── __init__.py             #   class DartGleam(LangPair) + register(DartGleam())
    ├── source_dart.py          #   Dart source side — delegates to tools/discover (R-001)
    ├── target_gleam.py         #   .gleam ext + target_for w/ Gleam-segment normalization
    └── mirror_gleam.py         #   prune set, "" suffix, companions, // stubs, tracker name

codeconv/src/codeconv/tools/   # UNCHANGED — init / discover / depgraph / scaffold / mirror
                               #   (Extensibility proof — SC-003 verified by diff)

codeconv/tests/
└── test_langpair_dart_gleam.py # NEW pure-unit tests (target+mirror hooks, registry,
                                #   selectability, segment-normalization, corpus no-collision)
```

**Structure Decision**: Single-project pluggable extension. The pair is wholly
contained in `langpairs/dart_gleam/`; the registry's `_PRODUCTION_PAIR_MODULES`
gains one tuple entry (`"codeconv.langpairs.dart_gleam"`). This is the seam the
016 contract designates ("Extensibility proof obligation"): adding a pair =
adding `langpairs/<source>_<target>/` + one auto-import line, zero stage-tool
edits. The source side mirrors `dart_csharp/source_dart.py`'s thin-delegate
structure so both pairs route to the single proven Dart parser in `tools/discover`.

## Complexity Tracking

> Filled because the Constitution Check **flags** a judgement-gate-able tension
> that the plan deliberately does not resolve unilaterally (Principle I/II:
> report, do not work around) — it is routed to `/bk-analyze` + an owner gate.

| Violation / Tension | Why it exists | Simpler alternative & why deferred |
|---|---|---|
| **FR-008 "detect & surface collision" cannot be met at runtime without editing a stage tool, which FR-005/SC-003 forbid.** Identity-preserving normalization (FR-003 AS-2) + illegal-segment normalization (FR-008) ⇒ collisions are provably possible (an illegal segment can normalize onto an already-legal sibling — pigeonhole). The only places that aggregate `target_for` outputs are the scaffold planner/workflow (stage tools), and neither detects duplicate targets today; the per-file pure `target_for` (FR-009, contract behaviour 2) cannot see cross-file collisions without becoming stateful (breaking purity/idempotency). | The three requirements are individually sound but jointly unsatisfiable as written. The pair-plugin protocol has no aggregate-validation hook, by design (all hooks per-file/constant). | **Deferred to owner decision (top `/bk-analyze` remediation), NOT chosen in-plan.** Default carried into tasks = **R3-a (zero stage edit):** identity-preserving normalization + a unit test asserting the authoritative `glp_runtime/` corpus normalizes collision-free (FR-008 "never silently overwritten" guaranteed for the production corpus; runtime erroring documented as out of reach without a seam). **Recommended alternative = R3-b:** one *generic* target-uniqueness assertion in `scaffold/planner.py` (helps any normalizing pair) — strongest correctness, but a stage-tool diff that needs a narrow SC-003 carve-out the owner must bless. **R3-c** (new protocol aggregate hook + planner call) is heavier and edits the 016 contract. |

> **OWNER RULING (2026-06-23): R3-b.** The owner blessed the SC-003 carve-out.
> Implemented as `scaffold/planner.py::TargetCollisionError` + a generic,
> pair-agnostic uniqueness check appended to `plan_target_tree` (raises before any
> staging write). SC-003/FR-005/FR-008 reconciled in `spec.md`; the diff now
> legitimately touches `tools/scaffold/planner.py` (one generic seam) in addition
> to the new pair package + the one registry line.
