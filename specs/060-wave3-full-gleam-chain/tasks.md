<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: Wave 3 consolidated — Full Gleam chain

**Feature**: `060-wave3-full-gleam-chain` | **Date**: 2026-07-27
**Input**: [spec.md](./spec.md), [plan.md](./plan.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**MVP scope**: Phase 1 + Phase 2 + Phase 3 (User Story 1). That alone delivers a Gleam runtime that loads and runs a multi-module GLP program — the wave's floor.

Tests are included for US3 and US5 because the spec makes conformance and cross-runtime proof the acceptance instruments (FR-016…FR-019, FR-026…FR-030); elsewhere tests follow the existing `gleeunit` pattern.

---

## Phase 1: Setup

- [x] T001 Capture the non-regression baseline: run `gleam test` in `glp_gleam/` and record the green count (expected 465) in `specs/060-wave3-full-gleam-chain/research.md` under a new "Baseline captured" line
- [x] T002 Capture the reference-suite baseline: run `bash test/run_all_tests.sh` from repo root and record pass/total in the same "Baseline captured" line
- [x] T003 [P] Record the corpus starting state: run `bash test/parity/run_gleam_corpus.sh` and record total / pass / fail / missing-golden counts in `specs/060-wave3-full-gleam-chain/research.md`
- [x] T004 Commit the captured baseline as the wave's checkpoint-zero (scoped commit, `research.md` only)

**Gate**: T001 and T002 must be green before any code change. A change made against a red baseline cannot be attributed (Constitution VII).

---

## Phase 2: Foundational (blocking prerequisites)

- [x] T005 Add the `_copy/2` builtin required by the `reduce` metainterpreter in `glp_gleam/src/glp/engine/kernels.gleam` (gap G2)
- [x] T006 [P] Add a `gleeunit` test for `_copy/2` term-copy semantics in `glp_gleam/test/glp/engine/kernels_test.gleam`
- [x] T007 Introduce the transport-injection seam on the engine composition root in `glp_gleam/src/glp/engine.gleam` so an instance can be constructed with a transport rather than compiled-in kernels only (gap G6)
- [x] T008 [P] Add a `gleeunit` test constructing an engine with an injected loopback transport in `glp_gleam/test/glp/engine_test.gleam`

**Gate**: T005 and T007 unblock US1 and US4 respectively. Nothing in Phase 3+ may proceed past its dependency on them.

---

## Phase 3: User Story 1 — Run a GLP program on the Gleam runtime (P1) 🎯 MVP

**Goal**: A `.glp` file loads and its goal runs, matching the reference runtime's outcome.
**Independent test**: Run a representative corpus subset on both runtimes and compare outcomes (quickstart §3).

- [x] T009 [US1] Implement module static linking ~~in `glp_gleam/src/glp/compiler/loader.gleam`~~ (locus: new `compiler/project_linker.gleam` + `loader.compile_linked` + facade `load_project`, per the T009 dossier) — resolve cross-module procedure references at load time (gap G1, FR-008). **Delivered** (commit `ba8b69f9`): faithful port of Dart `loadProject` — discovery, ancestor `self.glp` type-scoping, per-module type check, `M:p` rename + goal resolution, mode-aware entry aliases, linked compile with the reference's SRSW elision; Section F oracle (CSSG plays 1–7 + fplay1/2/4 tagged output) green
- [x] T010 [US1] Implement dynamic dispatch for module-qualified calls ~~in `glp_gleam/src/glp/compiler/loader.gleam`~~ (locus corrected by the G1 dossier: runner+scheduler+kernels+engine), replacing the `Unimplemented distribute` path (gap G1, FR-009). **Delivered as dispatch B1–B3** (commits `2dda06ea`, `89796927`, `1378fdd7`): scheduler module registry + channels, `_activate/2`→`RemoteSpawn`, embedded `serve/2`, auto-activation on exports, `Distribute`/`Transmit` data-threaded channel sends; Section L oracle L1–L3 green through the engine facade
- [x] T011 [US1] Support late resolution: a module referenced before it is loaded must resolve when it arrives, or yield a structured error at first call (FR-008). **Spec determination (2026-07-28)**: FR-008 verbatim — "resolve references across module boundaries, including modules loaded after the referring module" — is satisfied by CALL-time resolution, the exact Dart semantics: activations replay at every goal boot, so a module loaded after the referring module (any order, before the run) resolves; a module never loaded yields the reference's structured error naming it at the first dispatched call. Both arms proven by `module_resolution_test.gleam`; no new mechanism invented
- [x] T012 [P] [US1] Implement re-load replacement semantics in `glp_gleam/src/glp/compiler/loader.gleam` so stale procedures and link-table entries become unreachable (FR-015)
- [x] T013 [US1] ~~Implement the bytecode lint~~ **DISPOSITION RATIFIED (2026-07-27, marathon trace `T013-lint-disposition`)**: the Dart source of truth `glp_runtime/lib/lint/linter.dart` is (a) wired into nothing — referenced only by its own 3 tests, never by the load pipeline/engine/REPL — and (b) written against a superseded instruction set (`SuspendEnd`, `UnionSiAndGoto`, `BodySet*` — absent from the v2.16 inventory both runtimes execute). A verbatim port is impossible; a fresh lint over v2.16 has no spec (Language Authority / spec-first). Disposition: keep the placeholder, defer lint to its own specified feature. This IS the "lint disposition" 059's close-activation asked for (gap G3)
- [x] T014 [P] [US1] Ensure load-time SRSW rejection names the offending variable and clause in `glp_gleam/src/glp/analysis/srsw.gleam` diagnostics (FR-005)
- [x] T015 [P] [US1] Ensure every load failure yields a structured `LoadError{file, clause, reason}` and leaves the runtime usable in `glp_gleam/src/glp/diagnostics.gleam` (FR-003)
- [x] T016 [P] [US1] Add `gleeunit` tests for multi-module load, late resolution, and duplicate-procedure resolution ~~in `glp_gleam/test/glp/compiler/loader_test.gleam`~~ (locus: `glp_gleam/test/glp/engine/module_resolution_test.gleam` — the cases are engine-facade semantics, not single-file loader stages): Section-A-shaped co-load with goals against both files, late resolution both arms (T011), duplicate-procedure first-load-wins + same-name re-load replacement
- [ ] T017 [P] [US1] Add `gleeunit` tests for the bytecode lint in `glp_gleam/test/glp/lint_test.gleam`
- [x] T018 [US1] Verify suspension is reported distinctly from failure end-to-end in `glp_gleam/src/glp/engine/runner.gleam` (FR-006) and add a `gleeunit` case in `glp_gleam/test/glp/engine/runner_test.gleam`
- [x] T018a [US1] Verify the writer-MGU invariant survives module linking and the injected-transport engine seam — only writers bind, never readers, never writer-to-writer — with a `gleeunit` test in `glp_gleam/test/glp/runtime/unify_test.gleam` exercising cross-module calls (FR-007; closes analyze finding C1)

**Checkpoint**: `gleam test` ≥ baseline; a multi-module program loads and runs; writer-MGU verified across the changed paths.

---

## Phase 4: User Story 2 — Standalone interactive instance (P2)

**Goal**: A person can load, pose goals, trace, bound, and inspect without leaving the instance.
**Independent test**: Drive the scripted session in quickstart §4 and compare the transcript to the reference runtime.

- [x] T019 [US2] Implement `:bytecode <name>/<arity>` disassembly in `glp_gleam/src/glp/repl/commands.gleam` per `contracts/repl-commands.md` (gap G4, FR-014)
- [x] T020 [US2] ~~Implement `:boot <module>`~~ Amended to the Dart reference (contract amendment 2026-07-27): `:boot` runs a multi-isolate play via IsolateManager = the multiagent boot loader (gap G9); this instance parses `:boot` and reports the deferral instead of misreading it as a goal. Full `:boot` lands with the dispatch subsystem
- [x] T021 [P] [US2] Ensure `UnknownProcedure` / `UnknownModule` errors leave the session usable in `glp_gleam/src/glp/repl/repl.gleam` (contract invariant 1)
- [x] T022 [P] [US2] Ensure a `:limit`-stopped run returns `Bounded(steps)` and never `Failure` in `glp_gleam/src/glp/repl/results.gleam` (FR-013)
- [x] T023 [P] [US2] Add `gleeunit` tests for the full command surface in `glp_gleam/test/glp/repl/commands_test.gleam`
- [x] T024 [US2] Verify `:bytecode` is read-only — no heap or program mutation — with a test in `glp_gleam/test/glp/repl/commands_test.gleam` (contract invariant 6)

**Checkpoint**: every row of the `contracts/repl-commands.md` table is implemented and tested.

---

## Phase 5: User Story 3 — Conformance against the shared corpus (P2)

**Goal**: A single command yields a complete, deterministic, per-case verdict set.
**Independent test**: Run the corpus and confirm `pass + fail + out_of_scope == total`.

- [x] T025 [US3] Emit the three-verdict model (`pass` / `fail` / `out_of_scope{reason}`) from `test/parity/run_gleam_corpus.sh` per `contracts/corpus-report.md` (FR-017). Every attempted case emits exactly one `verdict:` line; fails carry expected/observed (first differing outcome lines); blocked / gap / fork / missing-golden are `out_of_scope` with their reasons (missing-golden per contract invariant 3 — no longer a bare divergence)
- [x] T026 [US3] Emit the aggregate block and assert the completeness invariant `P + F + O == N` in `test/parity/run_gleam_corpus.sh` (SC-002): contract-shape `total/pass/fail/out_of_scope` block; an attempted-vs-verdicts shortfall reports COMPLETENESS VIOLATION and exits 3 (a runner defect, never silence). Full run 2026-07-28: total 206 / pass 206 / fail 0 / out_of_scope 0, invariant holds
- [x] T027 [US3] ~~Classify the 44 golden-less cases as out_of_scope~~ **Superseded by Bug Protocol ruling 2026-07-27**: the 44 cases were never golden-less — CR-tolerant parsing added to `test/parity/run_gleam_corpus.sh` (both read loops) and `test/parity/record_dart_goldens.sh`; `test/parity` inputs/goldens/scripts pinned LF in `.gitattributes` (revised FR-018a/FR-018b)
- [ ] T028 [P] [US3] Emit named divergences (case id, expected, observed) for every `fail` from `test/parity/run_differential.sh` (FR-017)
- [x] T028a [US3] **GATE — Bug-Protocol triage before any regeneration.** DONE 2026-07-27: all 44 cases triaged in one pass — neither class (a) *absent* nor class (b) *behavioural divergence*, but a third class the gate existed to catch: **(c) harness defect** (CRLF-corrupted block ids; hex-verified; recorded in `research.md` T003 finding + marathon `mitem-019fa481`). STOPped and reported per Constitution II; owner ruling received (closes analyze finding B1)
- [x] T029 [US3] ~~Regenerate reference goldens~~ **Dissolved by the T028a outcome**: nothing was missing, so nothing is regenerated — regenerating would have canonised the harness defect. Replaced by the CR-tolerance + LF-pin fix under T027 (revised FR-018a/FR-018b, SC-010)
- [ ] T030 [US3] Verify determinism: run the corpus twice over unchanged code and assert identical verdicts and counts (FR-019, SC-008)
- [ ] T031 [P] [US3] **GATE — enforce SC-001.** Compute the in-scope pass rate, record it with every exception named in `specs/060-wave3-full-gleam-chain/research.md`, and **fail the phase if it is below 95%**, escalating rather than proceeding (SC-001; closes analyze finding A1)

**Checkpoint**: completeness invariant holds; golden-less count is 0 or individually reasoned; in-scope pass rate ≥95% or escalated.

**Dependency**: T028a strictly precedes T029. Skipping the triage risks baking a drifted reference into a golden — the exact failure the Bug-Protocol exists to prevent.

---

## Phase 6: User Story 4 — Connect two Gleam instances (P3)

**Goal**: Two instances link, exchange ordered messages, and fail cleanly on peer loss.
**Independent test**: quickstart §6 — join, round-trip, kill, observe.

- [ ] T032 [US4] Implement the inbound pump so an instance accepts inbound link attempts in `glp_gleam/src/glp/link.gleam` (gap G7, FR-023)
- [ ] T033 [US4] Implement the capability/version handshake per `contracts/link-handshake.md` in `glp_gleam/src/glp/link/seam/link_options.gleam` (FR-022)
- [ ] T034 [US4] Implement `Refuse{reason}` on version or capability mismatch — never best-effort continuation — in `glp_gleam/src/glp/link/seam/link_fault.gleam` (FR-022, FR-029)
- [ ] T035 [US4] Implement instance network join in `glp_gleam/src/glp/link.gleam` (gap G7)
- [ ] T036 [US4] Establish per-link ordering guarantees in `glp_gleam/src/glp/link/reliability/frame_codec.gleam` above the current CRC floor (gap G8, FR-021)
- [ ] T037 [P] [US4] Ensure a partially-received or CRC-failing frame is never delivered as complete in `glp_gleam/src/glp/link/reliability/frame_codec.gleam` (contract rule 5)
- [ ] T038 [US4] Implement bounded peer-loss detection (≤30 s) with fault propagation to programs holding cross-link references in `glp_gleam/src/glp/link/seam/link_fault.gleam` (FR-024, SC-007)
- [ ] T039 [P] [US4] Implement the multiagent boot loader in `glp_gleam/src/glp/mad/mad_engine.gleam` (gap G9)
- [ ] T040 [P] [US4] Add `gleeunit` link tests over loopback in `glp_gleam/test/glp/link_test.gleam`
- [ ] T041 [US4] Add link tests over TCP in `glp_gleam/test/glp/link/transports/tcp_test.gleam` (FR-025 acceptance surface)
- [ ] T042 [P] [US4] Assert `zmq`, `quic`, and `ws` remain **reachable** through the seam without link-layer changes: for each scheme, instantiate it via `link_scheme` and assert construction succeeds (not merely that the variant is selectable) in `glp_gleam/test/glp/link/seam/link_scheme_test.gleam` (FR-025; closes analyze finding U1)

**Checkpoint**: quickstart §6 checks 1–4 pass over both loopback and TCP.

**Note on G9**: 059 recorded the malformed named-reference plays (`|` type-alt) as failing on **both** runtimes. That is a shared defect — report and specify it before fixing it on the Gleam side (Constitution II).

---

## Phase 7: User Story 5 — C# ↔ Gleam interoperation (P3)

**Goal**: Two independently-written runtimes proven interoperable.
**Independent test**: every suite scenario passes in both directions.

- [ ] T043 [US5] Create the cross-runtime distributed test suite scaffold in `test/parity/cross_runtime/` with C#-initiates and Gleam-initiates variants (gap G10, FR-028)
- [ ] T044 [US5] Implement the term round-trip scenario covering nested structures, lists, and unbound variables with reader/writer polarity preserved in `test/parity/cross_runtime/round_trip.sh` (FR-027, SC-006)
- [ ] T045 [P] [US5] Implement the capability-mismatch scenario asserting explicit refusal, not silent misinterpretation, in `test/parity/cross_runtime/mismatch.sh` (FR-029)
- [ ] T046 [US5] Implement the bidirectional link-establishment scenario in `test/parity/cross_runtime/link_both_ways.sh` (FR-026)
- [ ] T047 [US5] Wire the cross-runtime suite into the project's regular test invocation in `test/run_all_tests.sh` so its results report alongside the others (FR-030)
- [ ] T048 [US5] Verify no scenario leaves an instance blocked indefinitely (SC-007) in `test/parity/cross_runtime/round_trip.sh`

**Checkpoint**: 100% of suite scenarios pass in both directions (SC-005).

---

## Phase 8: Polish & cross-cutting

- [ ] T049 [P] Verify the AtomVM-compatibility constraint: no BEAM-only construct introduced without a recorded reason, checked via `glp_gleam/src/atomvm_gated_probe.gleam` (FR-032)
- [ ] T050 [P] Re-run the full non-regression set — `gleam test` and `bash test/run_all_tests.sh` — and confirm both at or above the T001/T002 baseline (SC-009)
- [ ] T051 [P] Time the quickstart cold path and confirm first-goal-answer under 5 minutes (SC-003)
- [ ] T052 Update `specs/060-wave3-full-gleam-chain/quickstart.md` to remove any troubleshooting row whose gap has been closed

---

## Dependencies

```text
Phase 1 (T001-T004)  ─┬─▶ Phase 2 (T005-T008) ─┬─▶ Phase 3 US1 (T009-T018a) ─┬─▶ Phase 5 US3 (T025-T031, incl. T028a)
   baseline gate      │      T005 ─────────────┘                             │
                      │      T007 ──────────────────▶ Phase 6 US4 (T032-T042)┤
                      │                                                       ├─▶ Phase 7 US5 (T043-T048)
                      └─────────────────────────────▶ Phase 4 US2 (T019-T024)┘
                                                                              │
                                                                Phase 8 (T049-T052) ◀┘
```

- **US1 depends on** T005 (`_copy/2`).
- **US2 depends on** US1 (nothing to inspect or boot without a loaded program).
- **US3 depends on** US1 (programs must run to be compared).
- **US4 depends on** T007 (transport injection seam).
- **US5 depends on** US4 **and** US3, plus a runnable C# instance.
- **Phase 8** depends on everything.

## Parallel opportunities

| Phase | Parallelisable |
|---|---|
| 1 | T003 alongside T001/T002 |
| 2 | T006 ∥ T008 |
| 3 | T012, T014, T015, T016, T017 (distinct files); T018a after T009–T011 |
| 4 | T021, T022, T023 |
| 5 | T028 ∥ T028a; T031 after T029/T030 |
| 6 | T037, T039, T040, T042 |
| 7 | T045 alongside T044 |
| 8 | T049, T050, T051 all parallel |

## Implementation strategy

1. **Baseline first** (Phase 1) — non-negotiable under Constitution VII.
2. **MVP** = Phases 1–3. Stop here and the wave has already delivered a working second GLP runtime.
3. **Incremental delivery** — US2 and US3 can land in either order once US1 is green; US4 can start in parallel with them as soon as T007 lands.
4. **US5 last** — it consumes everything and needs an external C# instance.

## Task count

| Phase | Tasks |
|---|---|
| 1 Setup | 4 |
| 2 Foundational | 4 |
| 3 US1 (MVP) | 11 |
| 4 US2 | 6 |
| 5 US3 | 8 |
| 6 US4 | 11 |
| 7 US5 | 6 |
| 8 Polish | 4 |
| **Total** | **54** |

Two tasks (`T018a`, `T028a`) were added after `/bk-analyze` to close findings C1 and B1; `T031` and `T042`
were tightened to close A1 and U1. Suffixed IDs keep the original numbering stable so
`analysis-findings.md` and any in-flight references stay valid.
