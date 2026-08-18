<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 57a8b9fc-2569-4312-9cb0-17b9be75ff62
-->

# Implementation Plan: Verification receipts and loud failure

**Branch**: `078-verification-receipts` | **Date**: 2026-08-18 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/078-verification-receipts/spec.md`

## Summary

Every check (test, review, gate, poll, import, status probe) must emit a **receipt**
proving it ran against its intended target, and no verdict without a conforming
receipt may be read as a pass. Outcomes are classified as exactly one of
PASS / EMPTY / UNREAD / UNSEARCHABLE / FAIL, and the three "nothing found" cases
(EMPTY = examined-in-full-nothing-there, UNREAD = existed-but-not-examined,
UNSEARCHABLE = could-not-examine) never collapse. The mechanism is proven by a
**fault-injection** suite that deliberately induces each silent-success mode and
asserts a loud refusal — the feature is subject to its own invariant.

**Technical approach (all engineer-ratified 2026-08-18):**

- **Contract representation** — the receipt contract is a **versioned, language-neutral
  JSON schema** (the sidecar file format), with a **Python reference emitter/validator**
  serving the Python areas and **thin per-area emitters** (a ~20-line bash helper for the
  test-harness, a documented convention + helper for coop) for the non-Python areas.
  Consumers validate against the schema, not a library binding.
- **Addressing (FR-022)** — a receipt is a **sidecar file at a conventional, documented
  path derived from area + run**, with a **pointer on the verdict**; "no receipt" is thus a
  determinate condition, not a judgement. (Not catalog rows — that would make a prove-it-ran
  mechanism depend on the fleet's worst silent-failure component.)
- **Ownership & sequencing (FR-024)** — the contract + conformance fixture are **owned by
  buildkit** (the repo that distributes to every host); **glpnet binds by version**.
  This 078 feature delivers the **glpnet consumer, the 4 glpnet-area adoptions, and runs the
  buildkit-shipped fixture**; the contract itself + the 2 buildkit-area adoptions (3rtask,
  codexreview) are a **companion buildkit change** delivered by the buildkit/coordination lane
  (gavriella), pinned here by version. The adoption manifest is **per-repo**.
- **Ship-gate boundary** — the **first SHIP-TOKEN ships the MVP mechanism only**
  (US1 receipt-primitive + US2 three-way distinction + US3 fault-injection harness, proven
  against a purpose-built **reference check**). **US4** then retrofits the 6 real areas
  incrementally, each reproducing its historical instance; SC-001 (13/13) and SC-002 (100% of
  areas) close over those subsequent increments, honestly reported via the adoption manifest.

## Technical Context

**Language/Version**: Python 3.13 (buildkit CLI + `codeconv`); Bash (test-harness); Dart 3.9+ (test-harness runtime, unchanged). Receipt format: JSON.
**Primary Dependencies**: a JSON-schema validator (Python `jsonschema`, already an fleet dependency); the existing `codeconv` package; the buildkit CLI + its versioned-artifact/deploy machinery (for the by-version binding); the informed-consent override mechanism already established in `bk-guardian` (reused per FR-012, not reinvented).
**Storage**: **sidecar JSON files** at a conventional per-area/per-run path (FR-022). **No new PGLite tables, no new migration** (catalog-rows explicitly rejected; Constitution VI-a/b untouched).
**Testing**: `pytest` (`codeconv/tests/`) for the Python reference lib + consumer; the fault-injection suite (US3) as a dedicated, self-verifying test target; the bash `test/run_all_tests.sh` for the test-harness adoption (US4, deferred).
**Target Platform**: fleet Windows hosts (OLAMNIT/GAVRI/ARIELLAS/GAVRIELLA); the receipt format itself is OS-neutral.
**Project Type**: cross-repo toolchain library + per-area adoptions (glpnet consumer side; buildkit contract side is a companion change).
**Performance Goals**: receipt emission is O(1) per check beyond the check's own work; receipts are size-bounded (FR-005) so they never overwhelm the signal.
**Constraints**: additive-only (existing verdicts keep their shape, gain a receipt beside them — Assumptions); no external LM API (Constitution V); receipts bounded per FR-005 (cap enumerations, always keep true totals, byte backstop, self-declared truncation).
**Scale/Scope**: 6 declared areas; 13 witnessed instances to reproduce as faults (SC-001); MVP mechanism first, then 4 glpnet-area + 2 buildkit-area retrofits.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0. Re-checked after Phase 1 — still clean.*

| Principle | MUST | Verdict |
|---|---|---|
| I. Spec-First | No impl without an identified, quoted, consistency-checked spec | **PASS** — spec.md specified + clarified + 6 decisions engineer-ratified 2026-08-18. |
| II. Bug-Protocol / No-Workarounds | STOP-and-report on bugs; no robustness-as-workaround | **PASS** — the feature *is* the loud-failure mechanism; instance 13 was retained, not routed around. |
| III. SRSW inviolable | zero `skipSRSW` in artifacts | **PASS** — toolchain feature; GLP semantics untouched (Assumptions); no `skipSRSW`. |
| IV-a. Language Authority | no GLP language change without owner approval | **PASS** — no §1.14 gate applies (Assumptions); no guards/kernels/types touched. |
| IV-b. Preserve Working Internals | no removal of `_ClauseVar`/`_TentativeStruct`/fallbacks | **PASS** — no GLP runtime internals touched. |
| V. Claude-only LM | no OpenAI/litellm/openai on any LM path | **PASS** — no LM path introduced; SC-003's blind reader is Claude/human, never an external API. |
| VI-a. Additive migrations | additive, idempotent, single head | **PASS** — sidecar files, **no migration** (catalog rows rejected). |
| VI-b. Single PGLite cluster | one working-data cluster | **PASS** — no cluster created; receipts are files. |
| VII. Test-gated shipping | baseline green; commit-scoped; GitFlow | **PASS (advisory)** — plan honors baseline-before/after and ships via buildkit GitFlow. |
| VIII. Single source of truth | one authoritative spec/contract; no duplication | **PASS** — FR-024 (one buildkit-owned contract, copying prohibited) *embodies* this principle; slug-drift is the advisory clause (instance 13, in-scope). |

**No violations → Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/078-verification-receipts/
├── plan.md                       # This file (/bk-plan output)
├── research.md                   # Phase 0 — decisions + rejected alternatives
├── data-model.md                 # Phase 1 — Receipt / Outcome / Manifest / Override entities
├── quickstart.md                 # Phase 1 — emit / consume / fault-inject a receipt
├── contracts/
│   ├── receipt-schema.design.md  # DESIGN HANDOFF for the buildkit-owned JSON schema (FR-024); glpnet never owns the runtime artifact
│   ├── consumer-refusal.md       # glpnet-side consumer contract (FR-008/009)
│   ├── conformance-fixture.md    # the fixture whose own output IS a receipt (FR-024)
│   └── manifest-and-expected.md  # per-repo adoption manifest (FR-019/020/021) + per-run expected-set (FR-023)
└── tasks.md                      # Phase 2 — /bk-tasks (NOT created by /bk-plan)
```

### Source Code (repository root)

This feature's glpnet-side deliverables (the buildkit-side contract is a companion change, out of this tree):

```text
codeconv/src/codeconv/receipts/      # Python reference emitter + validator + consumer (binds buildkit schema by version)
├── __init__.py
├── receipt.py                       # emit(): build + write the sidecar JSON; classify outcome
├── outcome.py                       # PASS / EMPTY / UNREAD / UNSEARCHABLE / FAIL + the non-collapse rules
├── consumer.py                      # refuse a verdict lacking a conforming receipt (FR-008/009)
├── manifest.py                      # per-repo adoption manifest + per-run expected-set loader (FR-019..023)
├── override.py                      # informed-consent override binding (FR-012, reuses guardian shape)
└── bind.py                          # resolve + pin the buildkit contract version (FR-024)

codeconv/tests/
├── test_receipt_*.py                # reference-lib unit tests
├── test_consumer_refusal_*.py       # consumer refusal / aggregate propagation
└── faultinj/                        # US3 fault-injection suite (self-verifying — FR-016)
    ├── reference_check.py           # the purpose-built reference check MVP is proven against
    └── test_faults_*.py             # one deliberate silent-success mode per file (FR-014/015)

test/receipts/emit_receipt.sh        # thin bash emitter for the test-harness area (US4, deferred)
docs/receipts/                       # human-facing convention docs (path scheme, coop helper) (US4, deferred)
.specify/receipts/adoption.json      # the per-repo adoption manifest (FR-019) — checked in

MANIFEST/expected-set carrier         # per-run expected-check declaration (FR-023) — location set in Phase 1 contract
```

**Structure Decision**: The reference implementation lives under `codeconv/` because
codeconv is glpnet's established Python home with a `pytest` suite and the tools registry;
placing receipts there gives the Python areas (build-gate, roadmap-sync) a direct import and
gives the whole feature a single test target. Non-Python areas (test-harness bash, coop
markdown) get thin emitters that write the **same** schema-conforming JSON, validated by the
buildkit-shipped conformance fixture. The authoritative schema is **not** copied here
(FR-024) — `bind.py` resolves it from the pinned buildkit version and `receipt-schema.design.md`
is the one-time design handoff to the buildkit companion change.

## Complexity Tracking

> No Constitution violations. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
