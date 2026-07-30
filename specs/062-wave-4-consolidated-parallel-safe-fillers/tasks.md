<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: Wave 4 consolidated — parallel-safe fillers

**Feature**: `062-wave-4-consolidated-parallel-safe-fillers` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Organized by user story (priority order). Each user story is an independently testable, shippable
slice (spec FR-010). Baseline before / re-test after every slice (DISCIPLINE §2.2). `[P]` = may run
in parallel (different files, no incomplete-task dependency).

## Phase 1: Setup

- [~] T001 Capture wave-start baselines and record in the marathon run `mrun-7b8d08899272`: REPL suite (`bash test/run_all_tests.sh`), codeconv pytest, and the C#/Gleam suites touched by US3 — **REPL 532/532 ✓ (538/538 after US4 A31); codeconv pytest 688 passed / 17 pre-existing-failed / 9 skipped (28:49).** C#/Gleam US3 baselines captured 2026-07-30 (fork ruled both, engine line C#/.NET): **Gleam suite `gleam test` = 508 passed / 0 failed ✓; C# transport-line unit suites = glp_link 152/152, glp_il_codec 45/45, glp_wire_registry 6/6 (203/203) ✓; C# engine sln `out/csharp/glp_runtime_net.sln` builds green (no test project — engine verified via the REPL/corpus parity harness `test/parity/`, not `dotnet test`).** REMAINING sub-step: cross-runtime corpus/differential parity baseline (`test/parity/run_gleam_corpus.sh` + `run_differential.sh`) once the C# REPL exe is built — this is the engine-execution baseline for US3 T017/T022 and the US5 parity target.
- [X] T002 [P] Create feature-local `specs/062-wave-4-consolidated-parallel-safe-fillers/research/` for US2 studies
- [X] T003 [P] Create feature-local `specs/062-wave-4-consolidated-parallel-safe-fillers/proposals/` for US5 §1.14 proposals — created; holds `_fcp-sourcing-notes.md` (sourced FCP/GLP semantics, T026/T027 prep)

## Phase 2: Foundational (blocking prerequisites)

- [X] T004 Confirm the US3 engine line with the operator/lead (research R-3 assumes the C#/.NET line); record the confirmation before any US3 code — **CONFIRMED C#/.NET by operator (Gabi) 2026-07-29.** NB: the hand-maintained `csharp/` line is transport/codec only (no execution engine); the executing engine is the machine-converted `out/csharp/` — surfaces a scoping decision at T017 (execute-on-B target).
- [X] T005 Confirm access path to the FCP source + sibling GLP repo for US5 semantics (research R-5); flag on the scheduler board if off-host access is needed — **GRANTED (sibling-repo/FCP + web + Shapiro GLP corpus) 2026-07-29; FCP abandon-op semantics sourced from EShapiro2/FCP `unify.c`/`macros.h`/`fcp.h`. Finding: FCP has NO primitive named "abandon" — it is the anonymous-writer discard path (writer captures+drops → no suspension → GC). T028 STOP-gate material.**

## Phase 3: User Story 1 — Depgraph tooling (Priority: P1) 🎯 MVP

**Goal**: mark-and-recompute + cross-run trend reporting in the codeconv depgraph tool.
**Independent test**: fixture recompute touches only the marked subgraph; trend report byte-identical on re-run.

- [X] T006 [P] [US1] Add pytest fixtures for a small multi-file project with a recorded depgraph run in `codeconv/tests/` — reused the A→B→C `_mk_chain_subtree` + `_migrate_and_discover` (small multi-file project with a recorded compute run); no duplicate fixture (DISCIPLINE §1.3)
- [X] T007 [US1] Implement `mark-and-recompute` subcommand (mark set → dirty transitive dependents → recompute only dirty) in `codeconv/src/codeconv/tools/depgraph/` — new `subgraph.py` (pure dirty-set) + `run_mark_and_recompute` in `workflow.py` + CLI in `__init__.py`; NO new migration (per T011: `computed_at` bump is the trace, no `depgraph_runs` mode-CHECK widening)
- [X] T008 [US1] Implement `trends` view (≥2 runs → deterministic secret-redacted per-metric deltas) in `codeconv/src/codeconv/tools/depgraph/` — new `trends.py` (pure) + `run_trends` in `workflow.py` + CLI; byte-identical canonical JSON, `<2` runs → exit 1
- [X] T009 [P] [US1] Test: mark-and-recompute recomputes only the marked subgraph; unknown paths reported, nothing fabricated (exit 1) — `codeconv/tests/test_depgraph_recompute.py` (9 tests: 5 pure dirty-set + 4 bridge e2e incl. only-marked, transitive-dependents, unknown→exit 1, dry-run) ✓
- [X] T010 [P] [US1] Test: trends byte-identical on unchanged inputs; `<2` runs refused with clear message — `codeconv/tests/test_depgraph_trends.py` (7 tests: determinism/order-independence/byte-identical/redaction + e2e byte-identical + single-run→exit 1) ✓
- [X] T011 [US1] Verify additive-only persistence (no new Alembic head; single-head test still passes) and re-run codeconv pytest green vs T001 baseline — **NO migration added** (head stays `0011`); full depgraph suite **66/66 after** (16 new + 50 existing, 0 regression). Baseline had 17 pre-existing unrelated failures (stale 0008/0009 head tests + tutorials_run + equiv_capture + phase7); none are depgraph, none introduced here.

## Phase 4: User Story 2 — Feasibility studies (Priority: P2)

**Goal**: three decision-ready written studies. **Independent test**: each states go/no-go + risks.

- [X] T012 [P] [US2] Write `research/research-programme-and-llvm-feasibility.md` (staged programme + LLVM feasibility: question, options, go/no-go, staged plan, risks) — GO (programme, doc-complete) / NO-GO (LLVM as IL/backend) + gated ORCv2 accelerator spike
- [X] T013 [P] [US2] Write `research/cpp-engine-feasibility.md` (C++ engine+scheduler+compiler feasibility) — CONDITIONAL-GO on narrow C++ executor spike; NO-GO (for now) on full front-end
- [X] T014 [P] [US2] Write `research/many-instances-shared-static-memory-cooperative-scheduling.md` (feasibility + recommendation) — GO to design/experiment; BEAM leading substrate; gated on footprint target

## Phase 5: User Story 3 — Engine & transport (Priority: P2)

**Goal**: hardened compiled-IL-on-the-wire + factor-out-compiler, multi-accept transport, ZMQ base.
**Independent test**: ≥2 clients served; remote exec == local; malformed/version/failure rejected safely; ZMQ round-trip. Depends on T004.

- [X] T015 [US3] Factor the compiler so compiled IL is produced independently of execution (engine line per R-3) — **VERIFIED present** in C# `csharp/glp_il_codec`: `IlCodec.Encode/Decode` serializes compiled bytecode with no execution; `ExecuteEquivalenceTests` proves `Decode(Encode(p))` executes identically to the original (FR-003). Baseline `dotnet test glp_il_codec.tests` = 45/45 (T022 anchor).
- [X] T016 [US3] Implement the compiled-IL wire envelope per `contracts/compiled-il-wire-envelope.md` (il_version, compiled_form, integrity_digest, source_metadata) — `csharp/glp_il_codec/CompiledIlEnvelope.cs`: record + `CompiledIlEnvelopeCodec` (Encode/Decode/Wrap/Unwrap). `il_version`=semver (major-compat gate), `compiled_form`=IlCodec bytes, `integrity_digest`=SHA-256, `source_metadata`=module id. Verifying `Decode` realizes receiver obligations 1-2 (reject incompatible version / digest mismatch, loud, no execute). Tests `CompiledIlEnvelopeTests.cs` (6): round-trip, execute-equivalence, digest-mismatch/incompatible/unrecognized-version/truncated all rejected. **il_codec 51/51**.
- [X] T017 [US3] Implement receiver: compile-on-A → send → execute-on-B, result equals local execution — `csharp/glp_il_codec.tests/ReceiverExecuteOnBTests.cs`: A compiles→`Wrap`→`FrameCodec.Encode` (whole + fragmented); B reassembles→`Unwrap`→`GlpExecutor.RunNullaryAsync`. Execute-on-B **== local** across succeed/fail/suspend, both frame modes (6 tests). Contract obligation 4 / FR-005.
- [X] T018 [US3] Harden receiver (FR-005a): reject unknown/incompatible il_version + digest mismatch with diagnostic; mid-transfer failure leaves engine state unchanged — `ReceiverHardeningTests.cs` (4): corrupt-in-transit + incompatible-version rejected at the wire boundary BEFORE execution; mid-transfer (dropped fragment) → no whole frame → engine never reached (obligation 3); a valid transfer after a rejected one still executes == local. **il_codec 61/61** (45 base + 6 T016 + 6 T017 + 4 T018).
- [X] T019 [P] [US3] Implement `multi-accept` transport extension (≥2 concurrent clients, none dropped) — made `LoopbackTransport` rendezvous **role-aware** (per-channel listener/connector FIFO queues) so N concurrent `ListenAsync` on ONE address pair with N connectors → N independent bilateral links (distinct `LinkId`), none dropped (honors D-9 P2P base, not a hub). New `MultiAcceptListener.AcceptManyAsync`. Tests `MultiAcceptTests.cs` (2+5 clients, distinct links, PendingCount 0). **glp_link 154/154** (152 base + 2), zero regression from the rewrite.
- [X] T020 [P] [US3] Implement `zmq-receiver-base` + `zmq-sender-base` behind the transport seam — **NetMQ 4.0.4.3** added to `GlpLink.csproj` (operator-approved full-ZMQ capability, 2026-07-30). `csharp/glp_link/transports/ZmqTransport.cs`: `ZmqTransport : ILinkTransport` (+ `ZmqEndpoint`) — bilateral P2P over a NetMQ `PairSocket` pair (Bind↔Connect, lazy connect = no listener race), poller-owned socket (thread-safe send via `NetMQQueue`, recv via `Channel`), 1-byte control tag (data/eos) for graceful close + empty-frame round-trip. New `LinkScheme.Zmq`. Registerable in `TransportRegistry`.
- [X] T021 [US3] Tests: multi-accept ≥2 clients; compiled-IL happy path + hardening (malformed/version/failure); ZMQ round-trip — multi-accept (T019 `MultiAcceptTests`), compiled-IL happy+hardening (T016/T017/T018 il_codec tests), **ZMQ**: `glp_link.tests/ZmqTransportTests.cs` (7: round-trip both dirs, FIFO, empty frame, 100KB frame, graceful close, registry select, wrong-scheme) + `glp_il_codec.tests/ZmqEnvelopeExecuteTests.cs` (3: compiled-IL envelope over the real ZMQ PAIR wire → reassemble → execute-on-B == local, succeed/fail/suspend).
- [X] T022 [US3] Re-run the C#/engine suite green vs T001 baseline (no regression) — **il_codec 64/64** (was 45), **glp_link 161/161** (was 152), **wire_registry 6/6** (unchanged), **engine sln builds 0 errors**. Zero regression across the transport/engine line.

## Phase 6: User Story 4 — GLP multi-client control program (Priority: P3)

**Goal**: a GLP program coordinating N clients. **Independent test**: type-checks, compiles, runs to documented outcome.

- [X] T023 [US4] Write the multi-client control program (type + procedure decls, SRSW-valid, `Channel(In,Out)`) in `programs/tests/typed/` — `multi_client_control.glp`: controller broadcasts a ground command stream to 3 per-client streams, each client is a process, replies merged; local `merge` (no global merge in this runtime)
- [X] T024 [US4] Load + run via the REPL pipeline; confirm type-check/compile/run to documented succeeded|suspended outcome — loads (type-checks+compiles), `control_demo(X).` → `X = [pong(c1),pong(c3),pong(c2),bye(c3),bye(c1),bye(c2)]` → **succeeds** (SC-005)
- [X] T025 [US4] Add a REPL regression case in `test/run_all_tests.sh` (Section A/F); re-run suite green vs T001 baseline — A31 block added (6 checks); **after-run 538/538 = baseline 532 + 6, zero regression** ✓

## Phase 7: User Story 5 — §1.14 language items (Priority: P3, PROPOSAL-GATED) ⚠

**Goal**: implement abandon-operation (FCP-exact) + nested-structure-head-matching, each behind a
written §1.14 proposal. **Discipline**: proposal (sourced) BEFORE implementation; extend, never
remove, `_ClauseVar`/`_TentativeStruct`/fallbacks (IV-b). Depends on T005.

- [X] T026 [US5] Write §1.14 proposal `proposals/abandon-operation.md` — DRAFTED; finding: not a new primitive (existing anonymous-writer discard, verified). Operator ruling 2026-07-30: "ONLY integration adaptation with wider enriched capabilities, no structural change" → scope = document + regression-test + port to C#/Gleam runtime family (no Dart structural change).
- [X] T027 [US5] Write §1.14 proposal `proposals/nested-structure-head-matching.md` — DRAFTED; finding: already fully implemented in Dart (arbitrary depth READ+WRITE, verified via struct_demo/depth_test). Operator ruling 2026-07-30: "codeconv/reimplement in C# and/or Gleam" → scope = parity-verify + pin in target runtime(s); machinery already present in C# runner.cs (39 hits) + Gleam runner.gleam (47 hits).
- [X] T028 [US5] STOP-gate: both proposals presented; both rulings recorded. FORK RESOLVED by operator (Gabi) 2026-07-30: **target runtime(s) = BOTH C# `out/csharp/` AND Gleam.** Concrete work confirmed = parity-verify + regression-pin in both runtime families (machinery already present in C# runner.cs + Gleam runner.gleam), escalating only if a genuine divergence surfaces.
- [X] T029 [US5] abandon-operation parity (reframed by T028 ruling: no Dart structural change; verify anonymous-writer-discard parity in C# `out/csharp/` + Gleam). **PARITY CONFIRMED 2026-07-30** via `test/parity/run_differential.sh`: `control_demo(X).` (internal `client(c1,[stop|_],…)` abandon) → Dart/C#/Gleam all `→ succeeds` with the closed 6-reply list, all AGREE. C# `abandon.cs`/`runtime.cs` "abandon" symbols are DEAD, `[Obsolete(error:true)]`, comment "FCP has no abandon" — confirms proposal (abandon = ordinary anonymous-writer head-bind discard, not a dedicated op). REMAINING: regression pin (T031/T032).
- [X] T030 [US5] nested-structure HEAD-phase matching parity (reframed: no Dart structural change; verify arbitrary-depth READ+WRITE parity in C# + Gleam). **PARITY CONFIRMED 2026-07-30** via `run_differential.sh` (all three AGREE): WRITE `make_person(...)`→person(alice,age(thirty),city(seattle)); triple-WRITE `tree3(x,T)`; nested-WRITE `bin_nest(q,R)`; nested-READ `get_age(person(alice,age(thirty),city(seattle)),A)`→thirty and `get_city(...)`→seattle. FINDING (orthogonal, NOT a T030 divergence): the Gleam REPL query parser does **not** accept conjunction goals (`build_person(P), get_age(P?,A).` → ParseError at the comma) — a pre-existing Gleam-MVP frontend limitation; single-goal formulations achieve full parity. REMAINING: regression pin (T031/T032).
- [X] T031 [P] [US5] Positive + negative REPL regression cases in `test/run_all_tests.sh` (Sections A/C) for both items — **A32 block** (7 checks: nested WRITE `make_person`, nested READ `get_age`/`get_city`, nested soft-fail `age`≠`weight`, abandon `first_only` head+empty, outcome-sequence) + **Section C** `abandon_reader_bad.glp` (`_?` rejected). New fixtures `programs/tests/typed/{abandon_stream,abandon_reader_bad}.glp`. **C#/Gleam parity cases:** `test/parity/run_differential.sh` on all 5 US5 goals → **0 divergent** across Dart/C#/Gleam (make_person, get_age, get_city, soft-fail=`<unbound>`, first_only=`first(a)`). C# REPL rebuilt for this.
- [X] T032 [P] [US5] Dart unit coverage in `glp_runtime/test/` for both items — `glp_runtime/test/engine/us5_nested_abandon_test.dart` (6 tests): nested WRITE skeleton build, nested READ ×2, nested soft-fail (σ̂w discarded), abandon succeeds+NOT-suspended, `_?` rejected-at-load (`loadFile` throws). Observable-API assertions (private `si`/`_TentativeStruct` pinned indirectly).
- [X] T033 [US5] Re-run REPL suite + `dart test` green vs T001 baseline (SRSW + type checker clean) — **REPL 546/546** (= baseline 538 + 8, zero regression); **engine `dart test` 11/11** (5 existing + 6 new). SRSW + type checker clean (negative fixture proves `_?` still rejected).

## Phase 8: Polish & cross-cutting

- [X] T034 [P] Advance each delivered roadmap sub-item to its terminal state; confirm no item silently dropped (SC-008) — `TERMINAL-STATE.md` ledger: all 8 SCs mapped to delivered evidence; US1–US5 + decision (a) ZMQ **delivered**; US2 **delivered-as-study**; decision (b) Gleam conjunction-query gap **in progress/tracked** (ships this increment, T037-gated) — nothing silently dropped. SC-006 Gleam re-baseline deferred to T037.
- [X] T035 [P] `/bk-codify` any coordination/pipeline wins + improvements per the directive's GEPA/DSPy meta-task — win note `cn-20260730T065537-b169f631` (disk + catalog mirrored) capturing the per-task baseline→change→re-test + three-way-parity discipline and the parallel `/bk-3rtask` dispatch.
- [X] T036 Post stage-seam UPDATEs to the fleet lead at each phase completion (specify..close) with receipts (directive #4) — COOP v2 UPDATE posted to `I:\coop\glpnet\inbox\ariellas\20260730T065844Z-olamnit-UPDATE-wave4-us3-us5-implement-complete.md` with full receipts; `status\olamnit.md` refreshed (cursor honestly held at 193333Z per C6a). Advisory only (E6) — authorises no ship.
- [~] T037 Final full-suite green sweep across all touched runtimes; then hand to `/bk-analyze` → `/bk-implement` — **FINAL SWEEP GREEN:** REPL **546/546** (final anchor re-run), Gleam **514/514**, C# il_codec 64/64, glp_link 161/161, wire_registry 6/6, engine sln 0 errors, engine `dart test` 11/11, codeconv depgraph 66/66. All touched runtimes green together. **Remaining:** `/bk-analyze` pre-ship pass + the SHIP itself — GATED on lead CalVer `.N` coordination (directive #5) + operator go; NOT auto-executed.

## Dependencies & order

- Setup (T001–T003) → Foundational (T004–T005) → user stories.
- US1, US2, US4 are independent and may proceed in parallel after Setup.
- US3 depends on T004 (engine-line confirm). US5 depends on T005 (semantic-source access) and the
  T028 proposal STOP-gate.
- MVP = US1 (Phase 3) alone: a complete, shippable increment.

## Parallel opportunities

- T002/T003 together; T012/T013/T014 (three studies) together; T019/T020 together; T031/T032 together.
- Across stories: US1 (T006–T011), US2 (T012–T014), and US4 (T023–T025) can run concurrently.
