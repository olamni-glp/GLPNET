# Tasks: Wave 1 Consolidated — GLP Policy-Guard + HTTP3/QUIC-WS Link Full Acceptance

**Input**: Design documents from `/specs/049-wave1-guard-link-acceptance/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: REQUIRED by the spec (FR-007 regression programs, FR-005 parity tests, FR-015 regression coverage). Test tasks precede implementation within each story.

**Organization**: grouped by user story (US1 guard, US2 Profile C, US3 two-host, US4 marathon durability), then carried fixes (FR-015) and close-out. Ship gate = ALL FOUR stories pass (SC-010).

**Host legend**: tasks marked *(gavri)* execute on the gavri host per `contracts/gavri-delegation.md` on its own branch off this feature branch; all other tasks execute on this host (Olamnit).

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 Create evidence tree `specs/049-wave1-guard-link-acceptance/evidence/{guard,gavri,two-host,marathon}/` with `.gitkeep` files, per contracts/acceptance-evidence.md
- [X] T002 Record pre-change baselines (constitution VII): run `bash test/run_all_tests.sh` (expect 524/525 — the 1 failure is the pre-existing AOT-smoke case), `dotnet test csharp/glp_crdtmsg.tests`, `pytest glp_quick`; write results to `specs/049-wave1-guard-link-acceptance/evidence/baseline.md`; commit as baseline checkpoint

---

## Phase 2: Foundational (gates noted per task)

**⚠️ T003 is the §1.14 HARD GATE for ALL US1 guard work (no guard implementation/compile/run before its addendum is recorded). T004 gates US2+US3 (delegation artifact). US4 has no foundational dependency.**

- [X] T003 R1 realization checkpoint with Gabi: present research.md R1 evidence (typed-glp-manual §8 + `glp_runtime/lib/compiler/partial_evaluator.dart` compile-time-only defined guards) with candidates (a1) compiler-extension vs (a3) re-stage-direct-to-form-(b), PLUS the empty-targets outcome for vectors v05/v12 (C# matcher delivers vacuously; proposal text fails); record his answer as a Clarifications addendum in `specs/049-wave1-guard-link-acceptance/spec.md`. STOP-and-wait task — no guard code before the addendum exists (FR-001, SC-001)
- [X] T004 [P] Produce the FR-016 delegation artifact `specs/049-wave1-guard-link-acceptance/gavri-task-prompt.md` per contracts/gavri-delegation.md (self-contained: branch off `049-wave1-guard-link-acceptance`, push-only-own-branch, environment discovery, quicer provisioning per `gleam_quic/profile_c/README.md`, in-process conformance, two-host pairing with Olamnit server at 192.168.0.143, evidence to `evidence/gavri/` pushed early + continuously, BLOCKED-record protocol)

**Checkpoint**: T003 addendum recorded → US1 unlocked; T004 handed to Gabi → US2/US3 delegation can start; US4 can start any time.

---

## Phase 3: User Story 1 — Native GLP routing guard behind the language-authority gate (Priority: P1) 🎯 MVP

**Goal**: `satisfiable(Policy?, Reachable?)` with three-valued semantics, staged form (a) → form (b), 100% parity with the shipped C# matcher, (a) ≡ (b) equivalence proven.

**Independent Test**: load the guard in the REPL and reproduce the four proposal worked-example outcomes (Success / Fail / Fail / Suspend), independent of Deliverable B (spec US1).

- [X] T005 [US1] (v05/v12 per recorded T003 rulings) Create `specs/049-wave1-guard-link-acceptance/contracts/vectors.json` from contracts/decision-vectors.md seed table with the T003-ruled values for v05/v12 (schema per data-model DecisionVector; wx1–wx4 outcomes fixed by SC-002)
- [X] T006 [P] [US1] (124/124 green, matcher untouched) Add `csharp/glp_crdtmsg.tests/PolicyVectorParityTests.cs` reading vectors.json and driving `PolicyMatcher.Evaluate` on all `guard_only=false` vectors (characterization — green against the shipped matcher; `csharp/glp_crdtmsg/route/PolicyMatcher.cs` is READ-ONLY per FR-006); run `dotnet test csharp/glp_crdtmsg.tests`
- [X] T007 [P] [US1] (written first; loads only after the T009 a1 seat) Write worked-example regression programs (typed, with `procedure` declarations) in `programs/tests/typed/policy_guard_worked.glp` asserting wx1 `→ succeeds`, wx2/wx3 `→ failed`, wx4 `→ suspended` under the REPL step limit (written FIRST; expected not to load until T009)
- [X] T008 [US1] (all 12 vectors incl. guard_only) Write vector-driven guard programs in `programs/tests/typed/policy_guard_vectors.glp` covering every vectors.json entry (guard side runs ALL vectors incl. guard_only)
- [X] T009 [US1] (gavri `7884fbbb`, 2026-07-09 — a1 seat: PE test-only pass-through + codegen `definedGuards` side table + runner three-valued evaluator + engine merge; `policy_guard.glp` loads; wx1–wx4 = S/F/F/Susp) Implement form (a) per the T003-ruled mechanism: guard + types in `programs/crdtmsg/policy_guard.glp` (+ the ruled compiler seat in `glp_runtime/lib/compiler/partial_evaluator.dart`/codegen ONLY if (a1) was ruled — additive, preserve working internals); follow typed-glp-manual §0.2/§0.3: show the proposed GLP and the goal before any load/run, using pre-approved suite invocations where available
- [X] T010 [US1] (A29 block; full suite 526/527 = baseline 524/525 preserved + 2 new green; evidence/guard/form-a.md) Wire T007/T008 programs into `test/run_all_tests.sh` (Section A runtime; suspend cases distinguished from hangs by step limit per research R6); run full REPL suite — baseline 524/525 preserved + new tests green; record form-(a) EquivalenceRun in `specs/049-wave1-guard-link-acceptance/evidence/guard/form-a.md`
- [X] T011 [US1] (gavri `06aaec6e` — native systemDefinedGuards clause-spec table in runner + analyzer non-negatable + builtinProcedures registration; pure-(b) declaration-only caller proven S/F/Susp; no kernel blocker) Evolve to form (b) system guard primitive per the T003/base ruling: additive guard registration in `glp_runtime/lib/compiler/analyzer.dart` guard tables + three-valued evaluation in `glp_runtime/lib/bytecode/runner.dart`; kernel-level blocker ⇒ record + escalate, ship gate stays closed (FR-008)
- [X] T012 [US1] (full suite 529/528-pass @ 06aaec6e; SC-009 12/12+4/4 identical maps both forms, form-(a) env reference wired permanently; evidence/guard/form-b.md) Re-run the ENTIRE guard suite (worked examples + all vectors) under form (b); record form-(b) EquivalenceRun in `evidence/guard/form-b.md`; SC-009: outcome maps identical to form-(a) — any divergence is a defect in form (b), bug protocol BEFORE any fix (form (a) is reference)
- [X] T013 [US1] (evidence/guard/audit.md — SC-003 PASS via shared vectors.json both suites green, FR-006 PolicyMatcher diff EMPTY since baseline, SC-001 addenda ancestry verified) Final parity + gate audit: 100% guard-vs-matcher agreement on shared vectors (SC-003), `git diff` of `csharp/glp_crdtmsg/route/PolicyMatcher.cs` empty since baseline (FR-006), feature history shows zero guard code preceding the T003 addendum (SC-001); record verdicts in `evidence/guard/audit.md`

**Checkpoint**: US1 fully functional and independently verified — MVP delivered.

---

## Phase 4: User Story 2 — In-process QUIC on the full BEAM / 036 Profile C (Priority: P2)

**Goal**: 036 conformance flow passes with the BEAM client terminating QUIC in-process via quicer (T032 of 036), pass criteria equal to the Profile A baseline.

**Independent Test**: run the 036 conformance demo with the BEAM client in-process against the shipped QUIC host; same pass criteria as Profile A (spec US2).

**⚠️ Execution semantics**: a local (Olamnit) `/bk-implement` session NEVER executes *(gavri)* tasks (T015–T017) — its local actions are delegation tracking (T014) and evidence integration (T018). *(gavri)* tasks complete only via evidence pushed on the gavri branch; while waiting, proceed with other lanes.

- [X] T014 [US2] Hand `gavri-task-prompt.md` to Gabi for posting in a gavri session; record delegation start (gavri branch name, date) in `specs/049-wave1-guard-link-acceptance/evidence/gavri/delegation.md`
- [X] T015 [US2] *(gavri — done, `72127ce4` on 049a-gavri-us2-us3)* Environment discovery FIRST commit: OS, Erlang/OTP, rebar3, gleam, cmake, C/C++ toolchain, LAN address → `specs/049-wave1-guard-link-acceptance/evidence/gavri/environment.md` (contract D2.1)
- [X] T016 [US2] *(gavri — done, quicer 0.2.15/msquic 2.5.7 WSL, `ef2c6981`+`6bb9a891`)* Provision Profile C reproducibly (FR-010): build quicer NIF per `gleam_quic/profile_c/README.md`, wire `quic_link` module mirroring the C# QuicTransport contract, flip `GleamStackAdapter(profile="c")` capabilities to `in_process`; document every step; on exhausted provisioning → BLOCKED record + escalate (contract D4, FR-010)
- [X] T017 [US2] *(gavri — done, demo --profile c PASS = Profile A baseline, `6bb9a891`)* Run in-process conformance (connect, TLS pin verify, full-duplex) with pass criteria equal to Profile A baseline; per-criterion records to `evidence/gavri/` (acceptance-evidence format), pushed continuously
- [X] T018 [US2] (evidence/gavri/us2-verdict.md — PASS SC-005; branch merge deferred to US3 close) Integrate on Olamnit: pull the gavri branch, verify every US2 criterion has a PASS (or BLOCKED-escalated) record per contracts/acceptance-evidence.md; record SC-005 verdict in `evidence/gavri/us2-verdict.md`

**Checkpoint**: US2 independently verified (or BLOCKED-escalated with gate closed).

---

## Phase 5: User Story 3 — Two-host LAN end-to-end acceptance / 036 T040 (Priority: P3)

**Goal**: full 036 quickstart criteria (incl. ≥4-client mesh) across two physical hosts (Olamnit + gavri), 036 cert trust model unchanged.

**Independent Test**: 036 quickstart §7 with server on Olamnit, client(s) on gavri; full-duplex + mesh criteria pass across the wire (spec US3).

- [X] T019 [US3] Prepare Olamnit server side (addr corrected to 192.168.0.136 — see evidence/two-host/prep.md; firewall rule + server start are engineer-held) per 036 quickstart §7: cert material present (`glpquick-cert`, distributed to gavri out-of-band — `.pfx` never committed), firewall UDP 8443 open, server command verified: `glp-quick --server --addr 192.168.0.143 --port 8443 --cert ./glpquick-cert --max-clients 4`; readiness note to `specs/049-wave1-guard-link-acceptance/evidence/two-host/prep.md`
- [X] T020 [US3] (run executed 2026-07-08 roles-flipped, gavri=server; records `evidence/two-host/run.md` + `evidence/gavri/20-two-host.md`; packet-capture line staged-not-taken, non-loopback proven by two-machine consoles — deviation recorded in run.md) Execute the two-host run paired with gavri: connect, pin-verify, full-duplex, ≥4-client mesh; capture on-wire confirmation (UDP/QUIC capture note — not loopback); Olamnit-side records to `evidence/two-host/`, gavri-side records to `evidence/gavri/` (contract D2.4)
- [X] T021 [US3] (evidence/two-host/us3-verdict.md — SC-006 PASS, all five quickstart criteria per-criterion records; scenario 2 never arose) Verify every 036 quickstart criterion has a per-criterion record; second-host-unavailable attempts recorded + rescheduled with gate closed (US3 scenario 2); record SC-006 verdict in `evidence/two-host/us3-verdict.md`

**Checkpoint**: US3 independently verified across physical hosts.

---

## Phase 6: User Story 4 — Marathon durability verification / 036 T003+T036 (Priority: P4)

**Goal**: a real persisted marathon run survives a mid-flight process kill and resumes purely from durable rows; re-drive path completes a withheld scoped commit without duplication.

**Independent Test**: create/use a persisted run, kill the owning process, resume fresh, verify position derives from durable rows alone (spec US4).

- [X] T022 [US4] (evidence/marathon/run.md — run mrun-9724364d684a) Create (or adopt) a REAL marathon run for this wave via the installed buildkit (`D:\bstdev\research\buildkit\.venv313\Scripts\`, Python 3.13 venv — system 3.14 breaks DBOS); checkpoint ≥2 steps; record run id + durable-store location in `specs/049-wave1-guard-link-acceptance/evidence/marathon/run.md`
- [X] T023 [US4] (evidence/marathon/kill-resume.md — PASS) Kill the owning process mid-flight (taskkill; record pid + timestamp), resume from a FRESH session, assert: reported position == durable rows, completed checkpoints NOT re-executed, zero recorded state lost (US4 scenario 1, SC-007); full MarathonDurabilityRecord to `evidence/marathon/kill-resume.md`
- [X] T024 [US4] (evidence/marathon/redrive.md — PASS) Exercise the durable-first/commit re-drive path: checkpoint written durable-first with its scoped commit withheld, resume, verify the re-drive completes the commit exactly once (US4 scenario 2, FR-012); record to `evidence/marathon/redrive.md`

**Checkpoint**: US4 independently verified — all four stories done.

---

## Phase 7: Carried codexreview fixes (FR-015 — Deliverable B, no story label)

**Purpose**: the four lower-severity 036 findings, each fix + regression test (constitution II: spec'd defects, not workarounds).

- [X] T025 [P] Fix #3 duplicate `endpoint_id` eviction — VERIFIED PRE-EXISTING: fix + regression landed 036 commit `bdab8585` (`test_mesh.py::test_duplicate_announced_id_never_evicts_the_incumbent`; end-to-end pytest — the host has no xUnit project); `dotnet test` 114/114 + pytest green at baseline (evidence/baseline.md)
- [X] T026 [P] Fix #5 (pre-existing, 036 commit `d0acab2f`) + pytest regression ADDED: `test_review_regressions.py::test_demo_handshake_timeout_records_sc001_fail_not_attributeerror` — suite 180 passed
- [X] T027 [P] Fix #6 (pre-existing, 036 commit `b8c474b1`) + pytest regression ADDED: `test_review_regressions.py::test_spawn_handle_drains_stdout_before_readiness_wait` (real 300 KB pre-READY flood child) — suite 180 passed
- [X] T028 Fix #7 (pre-existing, 036 commit `28db9e5b` — noeol/eol fragment REASSEMBLY rather than the parenthetical length-framed read; outcome equivalent, deviation recorded for Gabi) + Erlang regression ADDED: `gleam_quic/test/run_glpq_ffi_reassembly_test.sh` (2 MiB envelope whole on stdout, control on stderr) — PASS locally on OTP 29 full paths, no gavri delegation needed

---

## Phase 8: Close-out & ship gate

- [X] T029 (2026-07-09; reference table below) Evidence completeness sweep (FR-013): every FR-009..FR-012 criterion and every SC has at least one record under `specs/049-wave1-guard-link-acceptance/evidence/`; add the record-path references into this tasks.md against T010/T012/T013/T017/T018/T020/T021/T022–T024
- [X] T030 (evidence/final-baselines.md — REPL 528/529 baseline-preserved, dotnet 124/124, quick-host builds, pytest 181+6skip) Final baselines (SC-004): `bash test/run_all_tests.sh` (baseline 524/525 + new guard tests green; delete stale `glp_runtime/.dart_tool/repl.dill` if unexpected failures), `dotnet test` (glp_crdtmsg.tests + glp_quick_host), `pytest glp_quick`; results to `evidence/final-baselines.md`
- [X] T031 (evidence/ship-gate.md — ALL FOUR US PASS; sole BLOCKED record = MSVC-native quicer, non-gating with SC-005 met via WSL path, surfaced for Gabi's express nod at ship) Ship-gate audit (SC-010, Clarifications hard-gate): ALL FOUR user stories PASS their acceptance scenarios with zero deferred gate items; any BLOCKED record ⇒ ship stays closed pending Gabi's express re-ruling; audit note to `evidence/ship-gate.md`
- [X] T032 (2026-07-27, primary session, marathon run `mrun-b0fabf3f8f44` — SHIP-HANDOFF discharged: PR #97 merged at `ab9ec6df`; **ship half was already complete** — `git log origin/develop..049-wave1-guard-link-acceptance` and `origin/main..` are both EMPTY, PR #100 "ship(049): merge full wave-1 implementation into develop (corrects hollow v2026.07.09.1)" merged 2026-07-09, tags `v2026.07.09.1`/`v2026.07.09.2` cut; so `buildkit ship` was a no-op and was NOT re-run. Roadmap half executed here: `glp-policy-guard`, `http3-quic-ws-link-full-acceptance`, and `wave-1-consolidated-glp-policy-guard-http3-quic-ws-link-full-acceptance` each advanced promoted → shipped → released via `buildkit-roadmap advance`) At wave close (with ship): advance roadmap features `glp-policy-guard` and `http3-quic-ws-link-full-acceptance` to shipped/closed with traceability to this feature recorded (FR-014, SC-008) via `buildkit-roadmap`; then ship via `buildkit ship --skip-preflight` (suites already run in T030) — **runs from the primary session on the canonical branch after PR #97 merges (SHIP-HANDOFF)**

### Evidence record references (T029 / FR-013)

| Criterion | Record |
|---|---|
| FR-009 / SC-005 (Profile C) | `evidence/gavri/10-profile-c.md`, `evidence/gavri/us2-verdict.md` (T017/T018) |
| FR-010 (provisioning + BLOCKED escalation) | `evidence/gavri/10-profile-c.md`, `evidence/gavri/90-summary.md` |
| FR-011 / SC-006 (two-host) | `evidence/two-host/run.md`, `evidence/two-host/us3-verdict.md`, `evidence/gavri/20-two-host.md` (T020/T021) |
| FR-012 / SC-007 (marathon) | `evidence/marathon/run.md`, `evidence/marathon/kill-resume.md`, `evidence/marathon/redrive.md` (T022–T024) |
| SC-001 / SC-003 / FR-006 | `evidence/guard/audit.md` (T013) |
| SC-002 / form (a) | `evidence/guard/form-a.md` (T010) |
| SC-009 / form (b) | `evidence/guard/form-b.md` (T012) |
| SC-004 | `evidence/baseline.md` (T002), `evidence/final-baselines.md` (T030) |
| SC-010 | `evidence/ship-gate.md` (T031) |
| SC-008 | recorded at T032 (roadmap advance at wave close) |

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2**: T001/T002 first (baselines gate every code change).
- **T003 (§1.14 addendum) BLOCKS all of US1 (T005–T013)**; nothing else waits on it.
- **T004 → T014 → T015–T017 (gavri)**; **T018** needs T017 evidence pushed.
- **US3 (T019–T021)** needs T004/T014 delegation live + T019 prep; the run itself (T020) needs gavri present (US2 provisioning complete or at least the gavri session active).
- **US4 (T022–T024)**: independent — can start immediately after Phase 1.
- **Within US1**: T005 → {T006, T007} → T008 → T009 → T010 → T011 → T012 → T013 (tests written before implementation; form (b) only after form (a) green).
- **Phase 7 (T025–T028)**: independent of all stories; only Phase 1 baselines required. T028 may pair with the gavri delegation.
- **Phase 8**: T029–T031 after all stories + fixes; T032 last (ship).

### Parallel opportunities

- After T002: **T003 prep, T004, US4 (T022–T024), and T025–T027 can all proceed in parallel.**
- T006 ∥ T007 (different files, different suites).
- The whole gavri lane (T015–T017) runs in parallel with everything on Olamnit.
- T025 ∥ T026 ∥ T027 (three different files/suites).

## Parallel Example: after baselines

```text
Lane A (gate):    T003 checkpoint with Gabi → US1 chain T005..T013
Lane B (gavri):   T004 → T014 → T015 → T016 → T017 → (T018 integrate) → T019..T021
Lane C (local):   T022 → T023 → T024 (marathon)
Lane D (fixes):   T025 ∥ T026 ∥ T027, then T028
```

## Implementation Strategy

**MVP = US1** (guard through form (b) + equivalence). But because T003 is a wait-on-Gabi gate, start Lanes B/C/D immediately after baselines so the wait costs nothing. Ship only at SC-010 (all four stories) — there is no partial ship for this wave.
