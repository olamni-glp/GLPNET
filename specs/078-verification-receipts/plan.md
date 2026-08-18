<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 57a8b9fc-2569-4312-9cb0-17b9be75ff62
-->

# Implementation Plan: Verification receipts and loud failure

**Branch**: `078-verification-receipts` | **Date**: 2026-08-18 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/078-verification-receipts/spec.md`

## Summary

No check may report success without emitting proof it ran against its intended target. The mechanism
is a **receipt**: a bounded, machine-readable document written to a conventional location beside every
verdict, recording the *resolved* target identity, examined and skipped counts, an outcome drawn from
a five-value enumeration, and the time it ran. Consumers refuse a verdict whose receipt is absent or
non-conforming, wherever the producing area has declared adoption.

The technical approach follows directly from FR-024: **one authoritative contract, owned by buildkit,
consumed by glpnet by version, proven by a conformance fixture whose own output is a receipt.** The
single hardest constraint is that the six declared areas are not one runtime — three are Python CLI
capabilities, one is a bash test harness, one is a filesystem protocol across three hosts. The
contract must therefore be *data*, not a class hierarchy, with a thin emitter per runtime.

## Technical Context

**Language/Version**: Python 3.11+ (buildkit capabilities, the contract, the conformance runner) and
POSIX bash (the glpnet REPL test harness, `test/run_all_tests.sh`). No third language is introduced.
**Primary Dependencies**: stdlib only for emit and verify — `json`, `pathlib`, `datetime`. A JSON
Schema document is the normative contract; validation uses the schema already vendored for buildkit's
existing envelopes. Deliberately no new runtime dependency: an emitter that cannot run because a
package is missing is itself a silent-success vector.
**Storage**: Files. Receipts are written to `.specify/receipts/<area>/<run-id>.json` (FR-022). No
database. Rationale recorded in research.md and in the spec's Clarifications: the catalog is the
component with this fleet's worst measured silent-failure record, and a mechanism built to prove
things ran must not depend on it.
**Testing**: `pytest` for the Python contract, emitters, consumers and the fault-injection suite;
bash assertions inside `test/run_all_tests.sh` for the harness emitter. The fault-injection suite
(US3) is the acceptance vehicle and is itself subject to FR-016.
**Target Platform**: Windows (GAVRIELLA, OLAMNIT), macOS/Linux (peer lanes). Path handling must be
platform-neutral; receipts record POSIX-normalised paths so a receipt is comparable across hosts.
**Project Type**: Cross-repo tooling contract + per-runtime emitters + consumers.
**Performance Goals**: Emitting a receipt must not measurably change a check's runtime — target
< 5 ms per receipt, which a bounded JSON write satisfies with room to spare.
**Constraints**: Receipts bounded per FR-005 — enumerations capped, **totals always retained**, byte
backstop per field, truncation self-declared. Additive per the Assumptions: existing verdicts keep
their current shape.
**Scale/Scope**: Six declared areas, 13 witnessed instances to fault-inject, 24 functional
requirements, 8 success criteria.

## Constitution Check

*GATE: must pass before Phase 0. Re-checked after Phase 1.*

| Principle | Assessment |
|---|---|
| **I. Spec-First** | PASS. This plan derives from spec.md alone. Where the spec was ambiguous, the ambiguity was resolved *in the spec* (Clarifications, Session 2026-08-18) before planning — not decided here. |
| **II. Bug-Protocol / No-Workarounds** | PASS, and load-bearing. This feature *is* the anti-workaround principle mechanised: it forbids the "handle it quietly" response to an unverifiable result. |
| **III. SRSW inviolable** | **N/A.** No GLP source is touched. The glpnet side changes `test/run_all_tests.sh` only. |
| **IV-a. Language Authority** | **N/A.** No guard, system predicate, body kernel, directive, type-system feature or primitive type is added. Confirmed against the Assumptions ("GLP semantics are untouched, so no §1.14 language-authority gate applies"). |
| **IV-b. Preserve Working Internals** | PASS. Emitters are additive; no existing verdict shape changes; nothing is removed. |
| **V. Claude-Only LM / No External API** | PASS. SC-003's blind reader runs in-toolchain; no external service is called. |
| **VI-a. Additive-Only, Idempotent, Single-Head** | PASS. Receipts are write-once per `(area, run-id)`; re-running a check writes a new run-id rather than mutating a prior receipt. |
| **VI-b. Single OS-lock-guarded PGLite cluster** | PASS **by avoidance** — no cluster is touched. See Storage above; this is a deliberate rejection recorded in research.md, not an oversight. |
| **VII. Test-Gated, Commit-Scoped Shipping** | PASS. US3's fault-injection suite gates the feature, and SC-007 requires the suite to be *proven able to go red*. |
| **VIII. Single Source of Truth & Traceability** | PASS, and directly encoded: FR-024 forbids copying the contract between repositories. Every FR traces to a witnessed instance in the spec's evidence table. |

**No violations. No justification table required.**

## Project Structure

### Documentation (this feature)

```text
specs/078-verification-receipts/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions + rejected alternatives
├── data-model.md        # Phase 1 — Receipt, Outcome, the two manifests, Override
├── quickstart.md        # Phase 1 — emit a receipt, consume one, inject a fault
├── contracts/
│   ├── receipt.schema.json          # THE normative contract (FR-004, FR-024)
│   ├── adoption-manifest.schema.json  # FR-019/020/021
│   ├── expected-checks.schema.json    # FR-023
│   └── conformance/                 # the fixture whose own output is a receipt
├── checklists/requirements.md       # Iterations 1–3 (existing)
└── tasks.md             # Phase 2 output — NOT created by /bk-plan
```

### Source code

**buildkit** (contract owner, FR-024) — worktree `glpnet-lane/toolchain-integrity-fixes`:

```text
src/buildkit_cli/receipts/
├── __init__.py          # public surface: emit(), read(), verify(), Outcome
├── model.py             # Receipt, Outcome, Override — dataclasses, no I/O
├── emit.py              # write a bounded receipt to the conventional location
├── verify.py            # FR-008 consumer gate; FR-009 aggregate propagation
├── manifest.py          # adoption (FR-019/020/021) + expected-checks (FR-023)
├── bound.py             # FR-005: cap enumerations, keep totals, declare truncation
└── conformance.py       # runs the fixture; its output IS a receipt
tests/receipts/          # unit + the fault-injection suite (US3)
```

**glpnet** (consumer, by version):

```text
test/receipts/
├── emit.sh              # bash emitter for the REPL harness (FR-022 location)
└── assert.sh            # harness-side assertions
test/run_all_tests.sh    # per-section receipts (see the Section I collision below)
.specify/receipts/       # receipt output root (gitignored; evidence, not source)
```

**Structure decision.** The contract is a schema plus two small pure modules, deliberately *not* a
framework. Two runtimes must emit it and the bash one cannot import Python, so the shared artifact
has to be the document format. `bound.py` and `manifest.py` are pure functions over data so the
conformance fixture can exercise them identically from either side.

## Phase 0 — research

Output: [research.md](./research.md). Every NEEDS CLARIFICATION from Technical Context is resolved
there; there are none outstanding, because the six that existed were closed in the spec at clarify.

Open questions research must settle (none block Phase 1):

1. **Run-id derivation** so `(area, run-id)` is unique and stable, without a clock the harness lacks.
2. **The `Section I` collision.** `test/run_all_tests.sh` declares `Section I` twice (lines 1653 and
   2219, verified 2026-08-18). Per-section receipts keyed on the letter alone would collide and one
   would silently overwrite the other — the exact failure this feature prevents. Register block 06
   recommends keying on `(letter, slugified-title)`; research.md confirms and records it.
3. **Where the 13 witnessed instances sit** relative to the six declared areas, so US4's retrofit is
   a checklist rather than a search.

## Phase 1 — design & contracts

Outputs: `data-model.md`, `contracts/*.schema.json`, `contracts/conformance/`, `quickstart.md`.

1. **`receipt.schema.json`** — the normative contract. Fields per FR-002/003/005/006/010, plus the
   truncation self-declaration and the optional override block (FR-012).
2. **`adoption-manifest.schema.json`** — enumerates all six FR-017 areas; absence of an entry is an
   error, never non-adoption (FR-019/020/021).
3. **`expected-checks.schema.json`** — the per-run declared set that gives FR-013 its meaning
   (FR-023).
4. **`contracts/conformance/`** — the fixture both repositories run. Its output is a receipt, so
   conformance is demonstrated under this feature's own invariant rather than asserted (FR-024).
5. **Agent context** — update the `<!-- BUILDKIT -->` block in `CLAUDE.md` to point here.

**Post-design constitution re-check: PASS.** No principle is affected by the design above; VI-b
remains satisfied by avoidance and the avoidance is now explicit in the artifact list.

## Complexity tracking

*No constitutional violations, so this table is empty by design rather than by omission.*

| Violation | Why needed | Simpler alternative rejected because |
|---|---|---|
| — | — | — |

The one place complexity was deliberately *added* is the bash emitter: a second implementation of the
same contract. It is justified because the alternative — excluding the test harness from the declared
areas — would leave the largest single source of witnessed instances (5 of 13) outside the mechanism,
and the conformance fixture is what keeps the two implementations honest.
