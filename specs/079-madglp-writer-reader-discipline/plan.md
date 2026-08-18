<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Implementation Plan: madGLP writer-reader address-discipline closure (N/N+1 audit + residuals)

**Branch**: `079-madglp-writer-reader-discipline` | **Date**: 2026-08-16 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/079-madglp-writer-reader-discipline/spec.md`

## Summary

Close the last three residuals of the N/N+1 address-discipline defect class. **Plan-time audit (research.md R-1) revised the approach:** the `writerAddr+1` fallback in `pairedReaderAddr` is NOT dead code — it is the **bound-writer reader path** (`readerForWriter` returns null for bound writers), used at 11+ `runner.dart` call sites. So the fix is not a blind removal + fail-loud; it is: add a bound-aware cross-pointer reader accessor **then** drop the convention (R-1a), or STOP-and-report if that needs a heap-format change (R-1b). Residuals R-2 (false-positive verdict) and R-3 (field rename) are clean audit-closes.

## Technical Context

**Language/Version**: Dart (glp_runtime; SDK ^3.9.4, host dart at `C:\src\flutter\bin\cache\dart-sdk\bin`)
**Primary Dependencies**: glp_runtime multiagent + FCP heap (`heap_fcp.dart`); no new deps
**Storage**: N/A (in-memory heap)
**Testing**: `dart test test/multiagent/` + REPL suite `test/run_all_tests.sh` (baseline before/after, FR-003)
**Target Platform**: Windows (olamnit); runtime is cross-platform Dart
**Project Type**: compiler/runtime (madGLP multiagent + FCP heap)
**Performance Goals**: behaviour-preserving; no measurable regression vs baseline
**Constraints**: 🔴 audit-first, behaviour-preserving with cross-pointers intact; core file `heap_fcp.dart` touched → surface diff explicitly; NEVER remove `_ClauseVar`/`_TentativeStruct`/fallback branches without approval (STOP-and-report)
**Scale/Scope**: 3 residuals; ~1 core file (`heap_fcp.dart`) + `lib/multiagent/mad_helpers.dart` + 1 doc + tests

## Constitution Check

*GATE: Must pass before Phase 0. Re-checked post-design.*

- **I. Spec-First** — PASS. Plan follows spec; the R-1 scope discovery is fed back to the spec (FR-009 anticipated it), not worked around.
- **II. Bug-Protocol / No-Workarounds** — PASS, and actively honored: R-1 is surfaced as a split with an explicit STOP-and-report branch (R-1b) rather than a workaround. The whole feature *removes* a workaround (the +1 convention).
- **III. SRSW** — N/A to this heap-addressing audit (no clause-level variable-mode changes).
- **IV-a. Language Authority (§1.14)** — PASS. NOT a language change; no guard/predicate/kernel/type-system change. Heap-addressing implementation only.
- **IV-b. Preserve Working Internals** — 🔴 THE governing gate. `pairedReaderAddr` fallback, `_ClauseVar`, `_TentativeStruct` are protected internals. Plan does NOT remove the fallback blindly: R-1a adds a proper accessor first (behaviour-preserving); R-1b STOP-and-reports if core invariants would be touched. PASS by construction.
- **V. Claude-Only LM** — PASS (no external API).
- **VI-a/b. Persistence** — N/A (no PGLite/catalog change).
- **VII. Test-Gated Shipping** — PASS: baseline recorded, re-test after, commit-scoped, SHIP-TOKEN before ship.
- **VIII. Single Source of Truth** — PASS; `docs/bug-send-globalise-localise.md` is the authority for R-2's verdict.

No violations → Complexity Tracking empty.

## Project Structure

### Documentation (this feature)
```text
specs/079-madglp-writer-reader-discipline/
├── plan.md              # this file
├── research.md          # Phase 0 — R-1..R-4 (R-1 scope split)
├── data-model.md        # Phase 1 — heap cell/cross-pointer entities
├── quickstart.md        # Phase 1 — how to run the audit + tests
├── contracts/
│   └── reader-resolution.md   # the pairedReaderAddr behavioural contract
└── tasks.md             # /bk-tasks output (NOT this command)
```

### Source Code (repository root)
```text
glp_runtime/
├── lib/runtime/heap_fcp.dart          # R-1: readerForWriter (:199), pairedReaderAddr (:236, +1 at :242)
├── lib/multiagent/mad_helpers.dart    # R-3: GlobalSendSpawn.readerAddr (:61-64) rename/re-doc
├── lib/bytecode/runner.dart           # R-1 call sites (11+: :411,1059,1724,1869,1880,1938,1949,2009,2036,2047,2065)
└── test/multiagent/                   # baseline + R-2 three_agent_pipeline_boot verdict
docs/bug-send-globalise-localise.md    # R-2 verdict authority; R-7 Issue-1 header/body fix
```

**Structure Decision**: In-place edits to the existing runtime; no new modules. Core touch confined to `heap_fcp.dart` reader-resolution; multiagent-only edits in `lib/multiagent/`.

## Phasing (for /bk-tasks)

1. **P1 — baseline** (FR-003): record `dart test test/multiagent/` + REPL suite counts.
2. **P2 — R-3 field rename** (clean, `lib/multiagent/` only): rename/re-doc `GlobalSendSpawn.readerAddr` + all refs; Issue-1 doc header fix (R-7). Lowest risk, do first for a quick green.
3. **P3 — R-2 false-positive verdict**: run three_agent_pipeline_boot deterministically → file repro OR retire + update `docs/bug-send-globalise-localise.md`.
4. **P4 — R-1 audit + fix**: audit all 11 `pairedReaderAddr` call sites (which pass bound vs unbound writers); implement R-1a (bound-aware cross-pointer reader accessor, then drop the +1 convention) with the core diff surfaced explicitly; **if R-1a needs a heap-format/core-invariant change → STOP-and-report (R-1b), do not proceed**. MVP alt: assert-on-unbound-fallback to close the silent-guess hazard if R-1a is deferred.
5. **P5 — verify**: re-run both suites at baseline count (SC-002); fault-injection proof for SC-001; probe both bound + unbound paths (SC-003).

**MVP = P1+P2+P3 + P4's audit + (R-1a OR the documented STOP-report).** SHIP-TOKEN acquired before /bk-ship.

## Complexity Tracking

*No constitution violations — table intentionally empty.*
