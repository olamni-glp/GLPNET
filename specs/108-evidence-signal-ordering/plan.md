<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Implementation Plan: Evidence-signal ordering (the complement of 078)

**Branch**: `108-evidence-signal-ordering` | **Date**: 2026-09-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/108-evidence-signal-ordering/spec.md`

## Summary

Deliver the invariant *"a signal a caller treats as evidence must not be observable before the work it
reports, and must survive the next restart"* as **three artefacts that can fail**, not as a document:

1. **A declared manifest** (`.specify/evidence-signals/manifest.json`) enumerating this lane's
   evidence-bearing signal surfaces. It is the denominator for SC-002 (FR-014a).
2. **A mechanical scan + cross-check** (`scripts/evidence_signal_audit.py`) that finds signal surfaces
   in the tree and reports any disagreement with the manifest **in either direction** as an error
   (FR-014b), emits a 078-conforming receipt (FR-017), and separates examined from unexamined
   regions (FR-020).
3. **A conformance harness with negative controls** (`scripts/tests/test_evidence_signals.py`) that
   exercises the four mechanisms — early wait, exit-status-only, size-as-evidence, non-durable
   completion — and, for each, is **demonstrated to fail** against the defect it governs (FR-018a).
   A check never shown capable of failing scores zero (SC-003, SC-005).

The invariant is additionally published for fleet adoption as `docs/evidence-signal-invariant.md`,
cross-referenced to 078 in both directions and to nothing else.

**Anti-goal, stated first because it is the likeliest failure**: this feature must not grow into
078, must not fix another lane's signal in that lane's tree, and must not mint a rival client,
election, transport or override mechanism. Every one of those has a measured precedent in the last
72 hours.

## Technical Context

**Language/Version**: Python 3.13 (`codeconv/.venv/Scripts/python.exe` and the buildkit `.venv313`);
the audited surfaces additionally span C# `net11.0` (`csharp/ynet_transport`, `csharp/ynet_client`),
Dart 3.9+ (`glp_runtime`), and Bash (`test/run_all_tests.sh`, `scripts/*.sh`).
**Primary Dependencies**: stdlib only for the audit and harness. No new third-party dependency — an
audit that cannot run because a dependency is missing is the failure mode it exists to prevent.
**Storage**: the manifest and the conformance report are checked-in files. No database. The 078
receipt is written to the conventional receipt location the existing 078 implementation already uses.
**Testing**: `pytest` for the harness (`codeconv/.venv/Scripts/python.exe -m pytest`); the existing
`test/run_all_tests.sh` REPL suite is the repo's primary signal and MUST stay green.
**Target Platform**: Windows (OLAMNIT) primary; the audit is path-portable and MUST NOT hard-code a
host path — a sibling host's hard-coded dart path has already produced 16/16 false failures in this
fleet.
**Project Type**: tooling + governance artefact inside an existing multi-language repo.
**Performance Goals**: the audit completes in under 60 s on this repo; the conformance harness's
40-iteration contention runs complete in under 120 s total. Slower than that and the suite gets
skipped, which is its own evidence failure.
**Constraints**: additive only. No change to feature 078, to the GLP language, to `heap_fcp.dart`,
or to any file under `csharp/ynet_client` beyond adding tests — the canonical client is qhstate's.
**Scale/Scope**: this lane's surfaces plus the eight measured instances (seven in the spec, plus the
one root-caused during planning — see Phase 0).

## Constitution Check

| principle | gate | verdict |
|---|---|---|
| **I. Spec-First** | judgement | **PASS** — `spec.md` written and clarified before any code; every artefact below traces to a numbered FR. |
| **II. Bug-Protocol / No-Workarounds** | judgement | **PASS, and load-bearing.** The instance-7 root cause found during planning is **reported, not patched** — it lives in a peer's canonical client. This plan adds a *failing* conformance test that names it, which is the reporting mechanism, not a workaround. |
| **III. SRSW** | machine | **PASS** — zero occurrences of `skipSRSW` in this feature's artefacts. No GLP clause is authored. |
| **IV-a. Language Authority** | judgement | **PASS** — no guard, kernel, directive, primitive type or type-system feature is proposed. |
| **IV-b. Preserve Working Internals** | judgement | **PASS** — nothing removed; `_ClauseVar`, `_TentativeStruct` and every fallback branch untouched. |
| **V. Claude-only LM** | machine | **PASS** — zero occurrences of `OPENAI_API_KEY` / `litellm` / `openai` on any path. The audit runs no LM at all. |
| **VI-a. Additive, single-head migrations** | machine | **PASS** — no migration. Head stays `0010`. |
| **VI-b. Single PGLite cluster** | judgement | **PASS** — no new cluster; the audit touches no database. |
| **VII. Test-gated, commit-scoped shipping** | advisory | **PASS** — baseline recorded before change, re-run after; staged by name; shipped via GitFlow. |
| **VIII. Single source of truth** | judgement | **PASS** — 078 remains the sole authority for verdict-bearing checks; this feature is the sole authority for non-verdict evidence signals; the boundary table in `spec.md` is the one place it is stated and both documents point at it. |

**No violations. Complexity Tracking is empty and stays empty.**

## Project Structure

### Documentation (this feature)

```
specs/108-evidence-signal-ordering/
├── spec.md              # the invariant, 7+1 measured instances, FR-001..FR-020
├── plan.md              # this file
├── research.md          # Phase 0 — instance 8 root cause, prior-art survey, rejected designs
├── data-model.md        # manifest + report + receipt schemas
├── contracts/
│   ├── manifest.schema.json         # the declared enumeration (FR-014a)
│   ├── conformance-report.schema.json # classifications + regions (FR-014, FR-020)
│   └── audit-cli.md                 # the audit's command contract and exit-code semantics
├── quickstart.md        # how a lane adopts the invariant in ten minutes
├── checklists/requirements.md
└── tasks.md             # Phase 2 output, produced by /bk-tasks
```

### Source Code (repository root)

```
.specify/evidence-signals/
└── manifest.json                    # NEW — the declared denominator

scripts/
├── evidence_signal_audit.py         # NEW — scan, cross-check, classify, receipt
└── tests/
    ├── test_evidence_signal_audit.py    # NEW — the audit's own tests
    └── test_evidence_signal_conformance.py  # NEW — the 4 mechanisms + negative controls

docs/
└── evidence-signal-invariant.md     # NEW — the published invariant, for fleet adoption

csharp/ynet_transport.tests/         # EXISTING — one added regression only
csharp/ynet_client.tests/            # EXISTING — one added failing test naming instance 8
```

**Structure Decision**: additive files under `scripts/`, `.specify/` and `docs/`, following the
existing repo convention (`scripts/roadmap_open_table.py`, `scripts/marathon_sitrep.py`,
`scripts/l0-consumers.py` are the direct precedents — stdlib-only Python audit scripts run by hand
and by the suite). No new package, no new build target, no new dependency. The two C# test additions
sit in the existing test projects rather than a new one.

## Phase 0 — Research

See [research.md](./research.md). Two things came out of it that changed the plan:

1. **An eighth instance was measured while planning, and its root cause localised.** It is the
   strongest available demonstration of FR-012 and it corroborates a peer's P1 on a *second host*
   with a *newer build*, while correcting that P1's stated mechanism. It is reported, not patched.
2. **The negative-control requirement (FR-018a) was strengthened from "recommended" to "the run
   scores zero without it"** after the survey found that every one of the eight instances would have
   passed a harness that lacked one.

## Phase 1 — Design

See [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md).

Design decisions, each with the alternative that was rejected and why:

- **The manifest is JSON, not YAML or a Python literal.** Rejected YAML: the repo has no YAML parser
  in the stdlib, and adding one to an audit whose whole point is to run everywhere is self-defeating.
- **The scan is pattern-based over source text, not AST-based.** Rejected per-language AST parsing:
  five languages, five parsers, and the parsers become the blind spot. A deliberately crude scan whose
  misses are *caught by the manifest cross-check* is more honest than a sophisticated scan whose
  misses are invisible. This is FR-014b's entire rationale.
- **The audit exits 0 only when the report is clean, and exits non-zero with a distinct code per
  failure class.** Rejected always-exit-0-with-a-report: that is instance 4 exactly (`reject` exiting
  0 while refusing). The audit must not commit the defect it audits for.
- **The receipt reuses 078's existing location and shape.** Rejected a new receipt format: a second
  receipt format is a second thing to keep conforming, and FR-006b already forbids a second override
  mechanism for the same reason.
- **Conformance evidence is a pytest test, not a manifest field.** Rejected recording conformance as
  a boolean in the manifest: FR-016 — a note is not evidence. The manifest records *where the check
  lives*; the check itself decides pass or fail.

## Complexity Tracking

Empty. No constitutional principle is bent, no exemption is claimed, and no dependency is added.
