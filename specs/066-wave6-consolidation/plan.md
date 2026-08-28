<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Implementation Plan: Wave-6 roadmap consolidation

**Branch**: `066-wave6-consolidation` | **Date**: 2026-08-03 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/066-wave6-consolidation/spec.md`

## Summary

Drive every item of the 18-item not-closed roadmap snapshot (20260803T150440Z) to a terminal
disposition through six prioritized stories: a gate ledger first (P1), then quick wins (P2),
promoted singletons (P3), the ANTLR4 spike (P4), the Full-Gleam chain in dependency order
consuming ariellas' receipts (P5), and captured-intake triage (P6). The wave is orchestration
over existing subsystems — it adds no new product subsystem of its own; its own artifacts are
the gate ledger, per-item dispositions on the roadmap, and the receipts trail. External
ownership (ariellas' 064-post-wave-gap-closure), the 064/065 tracks, and the engineer's open
rulings are modeled as gates that park stories, never as scope.

## Technical Context

**Language/Version**: mixed, per target item — Dart 3.x (glp_runtime REPL/engine), C#/.NET 10
(csharp/ + out/csharp/), Gleam/Erlang OTP 29 (glp_gleam), Python 3.14 (buildkit/codeconv
tooling), bash (test suites). The wave itself adds no new language surface.
**Primary Dependencies**: buildkit CLI (roadmap/pipeline/marathon/size/codexreview), the
fleet COOP protocol (receipts, sync rounds), per-item existing test suites.
**Storage**: buildkit PGlite catalog (roadmap_* rows, marathon rows) — additive only;
gate ledger as a git-tracked markdown artifact in this spec dir.
**Testing**: per-item: full REPL suite (`bash test/run_all_tests.sh`, 551/551 baseline),
`dotnet test` (glp_link.tests 172, glp_crdtmsg.tests 188), gleam test (569/569 baseline),
service-box drills; wave-level: ledger completeness checks (mechanical).
**Target Platform**: Windows 11 host (Gavriella) for this branch; cross-runtime items verify
against the C#↔Gleam suites as their own gates require.
**Project Type**: orchestration wave over an existing multi-runtime monorepo.
**Performance Goals**: N/A for the wave itself; per-item goals inherit from each item's
roadmap profile.
**Constraints**: never rebuild surfaces covered by ariellas' receipts (FR-004); ruling-blocked
stories park (FR-003); ships engineer-keystroke only (FR-008); additive-only persistence.
**Scale/Scope**: 18 items; 6 stories; expected multi-session (durable marathon run
mrun-6dc97a88c769 with per-story checkpoints).

## Constitution Check

*GATE: evaluated against constitution v1.1.0 before Phase 0; re-checked post-design.*

- **I Spec-First**: PASS — every story's build work proceeds only against the target item's
  existing spec (059 for full-scope Gleam, item profiles for quick wins/singletons); the wave
  spec itself governs orchestration. Stories finding spec gaps STOP per Bug-Protocol.
- **II Bug-Protocol**: PASS — FR-003/edge cases encode STOP-and-park; no workaround path exists
  in the design.
- **III SRSW**: PASS — no `skipSRSW` token in any wave artifact; GLP-touching stories inherit
  the invariant.
- **IV-a/IV-b Language Authority / Preserve Internals**: PASS — no language-surface change is
  in wave scope; any story that would touch one parks for owner approval (G3 mechanism).
- **V Claude-Only LM**: PASS — no external LM API anywhere in the wave design.
- **VI-a/VI-b Persistence**: PASS — only additive roadmap/marathon rows in the existing
  catalog; no new cluster; ledger is a git artifact.
- **VII Test-Gated, Commit-Scoped Shipping**: PASS — FR-006 requires green gates per story
  checkpoint; ships via buildkit GitFlow on engineer keystroke (FR-008).
- **VIII Single Source of Truth & Traceability**: PASS — the ledger references roadmap rows
  and receipts; it duplicates neither. Roadmap → pipeline → tasks traceability is the wave's
  own operating mechanism.

No violations; Complexity Tracking not required. Post-design re-check (after Phase 1):
unchanged — PASS.

## Project Structure

### Documentation (this feature)

```text
specs/066-wave6-consolidation/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── gate-ledger.md           # ledger format + completeness invariants
│   └── disposition-protocol.md  # terminal-disposition rules + evidence requirements
├── gate-ledger.md       # THE ledger instance (US1 deliverable; created by implement)
└── tasks.md             # Phase 2 output (/bk-tasks)
```

### Source Code (repository root)

No new source tree. Stories touch existing trees only, each under its item's own gates:

```text
glp_runtime/         # S2 glp-runtime-consol (Dart runtime features)
glp_gleam/           # S4 Full-Gleam chain (after ariellas receipts; second-lander rebases)
csharp/glp_link/     # S4 cross-runtime consumption boundary (ariellas touch-set — coordinate)
test/                # per-item suite wiring (existing conventions: Sections A..T)
codeconv/, .specify/ # S1 quick wins (toolchain/roadmap tooling) per their item profiles
```

**Structure Decision**: single-branch orchestration (wave-1..5 precedent). All wave stories
implement on `066-wave6-consolidation`; a story graduating into work larger than its profile
re-plans as its own follow-on feature rather than bloating the wave (the S6 triage path is the
model). Shared-file coordination with ariellas follows the agreed second-lander-rebases norm.

## Complexity Tracking

No constitution violations to justify.
