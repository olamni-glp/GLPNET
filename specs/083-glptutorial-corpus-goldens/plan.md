<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Implementation Plan: glptutorial corpus-golden reconciliation (stale goldens + drift-guard vendoring)

**Branch**: `083-glptutorial-corpus-goldens` | **Date**: 2026-08-24 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/083-glptutorial-corpus-goldens/spec.md`
**Lane**: `gavriella` · **Marathon**: `mrun-20d9230f767b` · **Z-series step**: `Z01`

## Summary

The tutorial corpus is a regression oracle whose truth artefacts have stopped telling the truth.
Four divergences are measured and unchanged as of 2026-08-24. Two are **false goldens** (ch04/07
asserts a spec-invalid program loads; ch04/08 predates the C# `is_list` fix); two are **guard gaps**
(ch07's substrate is unvendored, and its exercise→run mapping is unrecorded).

The plan restores the oracle in the spec's two independently-deliverable stories: **US1 makes the
corpus stop asserting falsehoods**, **US2 makes the drift guard able to see ch07**. FR-002 is ruled
**(b) record the rejection** — the ch04/07 exercise stays byte-exact from book §4.3.1 p 37 and its
*golden* changes — which puts **FR-009** (a golden must be able to express a correct refusal) in
scope as the load-bearing schema change.

🔴 **Phase 0 surfaced one thing the spec did not anticipate: the drift guard is saturated.**
`tutorials sync --check` exits 1 with **67 drift lines across all 13 chapters** on an *unmodified*
tree. FR-004's "a modification causes it to fail" is therefore an **unconditioned signal**, and
SC-003's "unmodified reports OK" is 0% true today. This plan adds per-chapter scoping as the
minimum that makes the ch07 signal real, and raises the SC wording to the engineer rather than
quietly redefining it.

## Technical Context

**Language/Version**: Python 3.14 (`codeconv` CLI); GLP corpus artefacts; Dart + C# runtimes as oracles
**Primary Dependencies**: `codeconv.cli tutorials` (`list · sync · preview · run · explain · propose`); the GLP REPL
**Storage**: the tutorial corpus on disk (goldens, tutorial `.md`, vendored substrate + digest manifest)
**Testing**: `bash test/run_all_tests.sh` (REPL suite) + `codeconv/tests` (pytest)
**Target Platform**: Windows (host GAVRIELLA); corpus is platform-neutral
**Project Type**: CLI tool + data corpus (single project)
**Performance Goals**: N/A — correctness feature, no throughput requirement
**Constraints**: ch04/07 source is **immutable** (FR-002 ruling (b)); no GLP language change (Constitution IV-a); ch04 + ch07 only
**Scale/Scope**: 4 measured divergences; 2 chapters; 1 vendored substrate (`programs/cssg_modules/`, 5 files)

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Verdict |
|---|---|---|
| **I. Spec-First** | Every task traces to an FR; FR-002 ruled before planning; FR-009's condition discharged. | ✅ PASS |
| **II. Bug-Protocol / No-Workarounds** | The ch04/07 rejection is **reported** (B10 to Udi), not silently fixed. The saturated guard is reported as a defect, not worked around. | ✅ PASS |
| **III. SRSW** | No GLP source is authored or modified. | ✅ N/A |
| **IV-a. Language Authority** | 🔴 Explicitly respected: option (a) was rejected *because* it would need new guard semantics — Udi's call. No language change here. | ✅ PASS |
| **IV-b. Preserve Working Internals** | No runtime internals touched; the C# `is_list` fix is not re-litigated. | ✅ PASS |
| **V. Claude-Only LM** | No external API. | ✅ PASS |
| **VI-a. Additive, Idempotent** | Repairs go through the approval-gated flow; `propose` is read-only and recomputed. | ✅ PASS |
| **VI-b. Single PGLite cluster** | Not touched. | ✅ N/A |
| **VII. Test-Gated, Commit-Scoped** | Gate re-run per slice against the **re-based** 561/559/2/0 baseline (R-5). Commits scoped to this feature. | ✅ PASS |
| **VIII. Single Source of Truth** | The corpus stays the single source for recorded outcomes; provenance is recorded per change. | ✅ PASS |

**Post-Phase-1 re-check**: unchanged — the design adds no new mechanism (existing
`--apply --approve --rationale` flow) and introduces no language or runtime change. ✅ PASS

## Project Structure

### Documentation (this feature)

```
specs/083-glptutorial-corpus-goldens/
├── spec.md            # ruled: FR-002 (b), FR-009 in scope
├── plan.md            # this file
├── research.md        # Phase 0 — R-1..R-7, all re-measured 2026-08-24
├── data-model.md      # Phase 1 — E1..E6, outcome_kind, change provenance
├── quickstart.md      # Phase 1 — runnable, with today's real outputs
├── contracts/
│   └── tutorials-cli.md   # Phase 1 — CLI surface, exit codes, report shape
└── checklists/
```

### Source Code (repository root)

```
codeconv/src/codeconv/tools/tutorials/   # the propose / sync / run surface
codeconv/tests/                          # pytest
programs/cssg_modules/                   # ch07 substrate (5 files) — to be vendored
<corpus root>/ch04/, ch07/               # goldens, tutorial .md, vendored copies
test/run_all_tests.sh                    # REPL regression gate
```

**Structure Decision**: single project. The change is confined to the `tutorials` tool surface and
the corpus data it governs. No new module, no new service, no new approval mechanism.

## Phase 0 — Research

Complete. See [research.md](./research.md). Findings that bind the plan:

| id | finding |
|---|---|
| R-1 | Baseline **re-confirmed**: exactly the same 4 proposals on 2026-08-24 |
| **R-2** | 🔴 **Guard saturated** — 67 drift lines, all 13 chapters, unmodified tree, exit 1 |
| R-3 | ch07 substrate **confirmed** `programs/cssg_modules/` (5 files), not `_v2` (6) |
| R-4 | `propose`'s "vendor **or** manifest" text contradicts C2 — both are MUSTs |
| **R-5** | 🔴 **SC-007 baseline stale**: spec says 546/0/1; measured **561/559/2/0** |
| R-6 | Existing approval-gated flow is the mechanism; no new one |
| R-7 | FR-009 needs an `outcome_kind` discriminator; `rejected` ≠ `error` |

**No blocking unknowns remain.** Two spec amendments are raised below rather than assumed.

## Phase 1 — Design & Contracts

Complete: [data-model.md](./data-model.md), [contracts/tutorials-cli.md](./contracts/tutorials-cli.md),
[quickstart.md](./quickstart.md).

**The load-bearing design decision** is `Golden.outcome_kind ∈ {loaded, rejected, error}` with
`rejected` carrying the refusal's **mechanical identity**, never prose. `rejected` and `error` are
kept distinct so a genuine runtime breakage can never be recorded as an expected refusal.

**The second** is mechanising FR-008's discriminator (C4): `change_class = recapture` is permitted
**only** with a non-null `cited_cause` naming a specific runtime change; absent a citation it is a
`repair`. This makes SC-006 checkable without reading anyone's intent.

## Delivery slices

| slice | story | scope | gate |
|---|---|---|---|
| **S1** | US1 | `outcome_kind` schema + ch04/07 golden re-recorded as `rejected` (source untouched) | proposal count 4 → 3 |
| **S2** | US1 | ch04/08 golden re-captured for **both** backends, `change_class=recapture`, cause cited | 3 → 2 |
| **S3** | US2 | per-chapter scoping for `sync --check` (**R-2 prerequisite**) | ch07 scope reports a real verdict |
| **S4** | US2 | vendor `programs/cssg_modules/` + digest manifest | modify substrate ⇒ named failure |
| **S5** | US2 | ch07 run manifest — one `(program, play, limit)` per exercise | 2 → 0 |
| **S6** | US3 | provenance recoverability + `propose` remedy text fix (C-1.5) + FR-010 doc correction | SC-005, SC-006 |
| **S7** | — | **B10 report to Udi** (book §4.3.1 vs manual §8) | reported, not fixed |

**US1 (S1+S2) is the MVP** — it restores trust in the oracle without touching the guard, exactly as
the spec's priority says.

## Complexity Tracking

Two items where the spec, as written, cannot be satisfied. **Raised, not silently redefined.**

| # | Item | Why it cannot stand as written | Recommendation |
|---|---|---|---|
| **A-1** | **SC-003 / FR-004** — "unmodified tree reports OK 100% of the time" | The unmodified tree reports **67 drifts, exit 1**, across all 13 chapters. 57 lines are outside this feature's declared scope (ch04 + ch07 only). The criterion is 0% true today and cannot be made true without a 13-chapter sweep this feature explicitly excludes. | Amend SC-003 to be **per-chapter for the in-scope chapters**, delivered via C-2.3 scoping. The whole-tree sweep becomes its own feature. |
| **A-2** | **SC-007** — "remains green (baseline 546 pass / 0 fail / 1 skip)" | Measured today: **561 total / 559 pass / 2 fail / 0 skip**. The suite grew 15 tests and lost its skip; the 2 failures are pre-existing `Section T` 064 drills, out of scope. Taken literally SC-007 can never be met. | Re-base SC-007 to **"introduces no new failure against 561/559/2/0"**. |

🔴 **Neither is a licence to proceed on assumption.** A-1 and A-2 are recorded as engineer
amendments; tasks may be generated, but **S3/S4 must not claim SC-003 until A-1 is ruled**, and no
slice may claim SC-007 until A-2 is ruled. Advancing past an open gate is the failure mode this
lane has already had to withdraw once.

## Progress

- [x] Phase 0 — research.md (R-1..R-7, all re-measured 2026-08-24)
- [x] Phase 1 — data-model.md, contracts/, quickstart.md
- [x] Constitution Check — pre-research PASS, post-design PASS
- [ ] Phase 2 — `/bk-tasks` (next)
