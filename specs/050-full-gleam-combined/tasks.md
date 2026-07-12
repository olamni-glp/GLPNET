# Tasks: GLEAM implementation — combined Full-Gleam feature

**Input**: Design documents from `/specs/050-full-gleam-combined/`
**Prerequisites**: plan.md, spec.md (5 user stories), research.md (R1–R8), data-model.md, contracts/ (4), quickstart.md

**Tests**: INCLUDED — the spec's gates are test/proof artifacts (parity suites, adversarial suites, Lean proofs), so test tasks are integral, not optional.

**Organization**: grouped by user story; US1 (engine) is the MVP; US2 (REPL) and US3 (corpus) complete M1; US4 (links) and US5 (capstone) are M2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different files, no dependency on an incomplete task)
- Porting references: Dart `glp_runtime/lib/` (source of truth), C# `glp_runtime_net/lib/` + `csharp/glp_link/`; ISA: `docs/glp-bytecode-v216-complete.md` (normative)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: skeleton, baselines, toolchain checks — build stays green throughout

- [X] T001 Replace 033 placeholder modules with real subsystem skeletons (`glp_gleam/src/glp/{parser,analysis,compiler,bytecode,engine,repl,link}/` dirs + module stubs that compile); keep `gleam build --target erlang` green
- [X] T002 [P] Create `test/parity/` skeleton: `lib/normalize.sh` (shared normalization lib, empty rules to start), `goldens/` dir, runner stubs with host-requirement headers
- [X] T003 [P] Record baseline: run `bash test/run_all_tests.sh` (Dart) and `bash test/link/run_link_tests_cross.sh` (Dart↔C#), confirm green, note counts in `specs/050-full-gleam-combined/baseline.md`
- [X] T004 [P] Scaffold Lean Lake projects `glp_gleam/lean/WriterMguBindsOnlyWriters/` and `glp_gleam/lean/DistDerefConvergence/` (lakefile.lean + empty Basic.lean, `lake build` green) per repo convention
- [X] T005 Verify `gleam test` green under WSL on the skeleton (`cd /mnt/d/.../glp_gleam && gleam test`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: shared types every story consumes — MUST complete before user stories

**⚠️ CRITICAL**: No user story work until this phase is done

- [X] T006 Implement v2.16 opcode definitions in `glp_gleam/src/glp/bytecode/opcodes.gleam` per `docs/glp-bytecode-v216-complete.md` (no additions/renumbering — D4 discipline; port refs `glp_runtime/lib/bytecode/opcodes*.dart`)
- [X] T007 Implement bytecode program model + loader types in `glp_gleam/src/glp/bytecode/program.gleam` (procedure table, instruction stream, positional X-register file — FR-006; ref `asm.dart`/`asm.cs`)
- [X] T008 [P] Define AST types in `glp_gleam/src/glp/parser/ast.gleam` (SourceModule: types, procedure decls, clauses, directives — from Dart compiler AST)
- [X] T009 [P] Define engine core types in `glp_gleam/src/glp/engine/types.gleam` (Goal/Activation with goal_id + suspension_generation, run queue, states per data-model.md)
- [X] T010 Extend `glp_gleam/src/glp/runtime/suspension.gleam` (034 module) with generation-scoped wake: writer id → set of (goal_id, generation); atomic consume on bind (no double-wake)
- [X] T011 [P] Define staged-diagnostic type in `glp_gleam/src/glp/diagnostics.gleam` (stage name + location + reason; rejection classes matching reference: parse/SRSW/type/guard)
- [X] T012 Foundational gleeunit tests in `glp_gleam/test/glp/bytecode/opcodes_test.gleam` and `glp_gleam/test/glp/engine/dedup_key_test.gleam` (opcode table integrity; (goal_id, generation) dedup key drops stale wakes); plus dependency-policy assertion in `glp_gleam/test/glp/deps_policy_test.gleam` — fail if an OTP-abstraction package appears in `glp_gleam/gleam.toml`/`manifest.toml` (FR-007; analyze C1)

**Checkpoint**: foundation compiles + tests green (WSL) — user stories can begin

---

## Phase 3: User Story 1 — Load and run GLP programs on a standalone Gleam instance (Priority: P1) 🎯 MVP

**Goal**: full load pipeline (parse→SRSW→PE→typecheck→compile→load) + three-phase engine; corpus smoke programs run with reference-identical outcomes

**Independent Test**: via the engine-value API, load a known-good corpus program and run its goal — outcome matches Dart; load SRSW-violating and ill-typed programs — rejected at the same stage as Dart

- [X] T013 [US1] Port lexer to `glp_gleam/src/glp/parser/lexer.gleam` (from `glp_runtime/lib/compiler/` lexer; R1 hand-port, no parser generator)
- [X] T014 [US1] Port recursive-descent parser to `glp_gleam/src/glp/parser/parser.gleam` producing `ast.SourceModule` (Dart parser behaviour is the conformance oracle — R1)
- [X] T015 [P] [US1] Parser tests incl. corpus negative parse cases in `glp_gleam/test/glp/parser/parser_test.gleam`
- [X] T016 [US1] Port SRSW checker to `glp_gleam/src/glp/analysis/srsw.gleam` — unchanged semantics incl. constant-type and ground-guard relaxations (typed-glp-manual §3; ruling D6); no escape mechanism of any kind
- [X] T017 [US1] Port partial evaluator to `glp_gleam/src/glp/compiler/partial_eval.gleam` (unit-clause/defined-guard unfolding; SRSW-preserving per PI:13 — add conformance note referencing the dossier row) ◇ 2026-07-11: BOTH live Dart copies ported (engine copy w/ 049 guard admission + analyzer copy feeding codegen — observable surfaces differ); dead `unfoldReduceCalls` not ported; 28 gleeunit tests; all 5 error channels REPL-verified byte-identical
- [x] T018 [US1] Port type checker to `glp_gleam/src/glp/analysis/type_checker.gleam` (modes ↓/↑ with `?`-flip rule, parameterized types, type union — manual §2A/§17/§20)
- [X] T019 [US1] Port codegen to `glp_gleam/src/glp/compiler/codegen.gleam` emitting v2.16 bytecode (ref `glp_runtime/lib/compiler/` codegen; byte-comparable output on the P5 merge example as a smoke check) ◇ 2026-07-12: faithful port of Dart `CodeGenerator` (immutable `Ctx`); register-allocation prerequisite folded in per option (a) — additive `srsw.clause_register_map` reuses the SRSW-verified first-occurrence walk (Dart `_assignRegisters` order). Two-clause merge/3 golden pins exact v2.16 op stream (native gleam test 332/332). One escalated divergence (unused by merge, deferred to T030): Dart emits `UnifyConstant(<StructTerm>)` for ground body-arg list literals; Gleam `UnifyConstant` holds only a `Constant`, so those build structurally.
- [X] T020 [US1] Implement single-entry load pipeline in `glp_gleam/src/glp/compiler/loader.gleam` (fixed stage order, staged diagnostics, no stage skippable — contracts/gleam-instance-surface.md) ◇ 2026-07-12: `load(source, prelude_source) -> Result(LoadOutcome, StagedError)` wires parse→SRSW→PE(analyzer copy)→typecheck(with PE-transformed procs)→codegen→load per Dart `compiler.dart GlpCompiler.compileWithMetadata` sequencing; no-global-state (FR-009) → prelude source threaded as a param (engine T029 reads self.glp). Staged-error class mapping: parse→parse error; SRSW multiplicity/reserved-const→SRSW violation, guard-position→guard violation; PE defined-guard→guard violation; type→type error. 4 gleeunit tests (happy path + one negative per early stage asserting earliest-rejecting stage+class). native gleam test 336/336.
- [X] T021 [US1] Implement three-phase runner in `glp_gleam/src/glp/engine/runner.gleam` (HEAD tentative unification, pure GUARD eval, BODY commit; writer-MGU discipline over `runtime/heap.gleam`; suspension on unbound readers) ◇ 2026-07-12 SLICE 21a/b (IN PROGRESS, not closed): immutable recursive stepper porting Dart `runWithStatus`/`RunnerContext` — control spine (ClauseTry/ClauseNext/TryNextClause/Commit/Proceed/NoMoreClauses + `_findNextClauseTry` forward scan), HEAD-constant path (σ̂w writer-bind / Si reader-defer / ground compare), two-phase Si-resolution Commit applying σ̂w via heap.bind_writer(_to_var), suspend-or-fail. Adaptation: Si/U carry WRITER addrs (writer-keyed suspension foundation), documented in module header. Unported opcodes return `RunnerError(Unimplemented)` (surfaced). 3 gleeunit e2e tests: flip clause-1 bind, clause-2 via soft-fail, suspend-on-unbound-reader (native gleam 339/339). ◇ 2026-07-12 SLICE 21c DONE (HEAD structures — writer-MGU crux): ported `HeadStructure` (arg + clause-var ≥10 paths), unified `GetVariable`/`GetValue` (Dart opv2), `UnifyVariable` (WRITE-tentative + READ incl. reader×reader FAIL, writer×writer soft-fail), `UnifyConstant`/`UnifyVoid`, nested `Push`/`Pop`/`UnifyStructure`, and the Commit tentative→StructTerm conversion (+`_ClauseVar` resolution). Immutable adaptation: tentative struct kept in `current` and re-synced to `sigma_hat[current_writer]` per fill (`put_current`); σ̂w value is `SigmaVal{SVTerm|SVTentative}`; clauseVars is `CVar{CVAddr|CVTerm|CVTentative|CVState}`. Added additive read-only heap accessors `paired_reader`/`paired_writer` (Dart pairedReaderAddr/tryWriterForReader). One surfaced frozen-semantics gap (RunnerError, not guessed): a WRITE-mode void slot commits to Dart `ConstTerm(null)`, which Gleam's Constant model can't represent — escalate if the corpus hits it. 4 new e2e gleeunit (swap READ+WRITE writer-MGU, first_bit list READ+UnifyVoid+WRITE some(H?), empty-list 2nd clause, list-suspend); native gleam 343/343. ◇ 2026-07-12 SLICE 21d DONE (BODY construction + spawn) — T021 CLOSED: ported PutVariable/PutConstant/PutBoundConst/PutBoundNil/PutStructure, SetVariable/SetConstant + top-level UnifyVariable/UnifyConstant BODY-WRITE, structure completion with recursive parent-stack unwind (`complete_body_struct`/`unwind_body`), and Spawn. Runner↔scheduler seam: Spawn emits `SpawnReq(procedure, entry_pc, regs)` (runner does NOT own the goal-id counter — Dart rt.nextGoalId; the T022 scheduler mints ids); `Reduced(heap, woken, spawned)` splits commit/body reactivation signals (woken GoalRefs) from body-spawned goals (SpawnReqs). Added ctx fields arg_slots/build_writer/build_slot/parent_stack. Requeue/PutList/PutNil/PutConstant NOT emitted by the Gleam codegen (Spawn-for-all-body-goals); Distribute/Transmit/Allocate/Deallocate deferred (US4/unused). 1 new e2e gleeunit (start(zero) body builds g(X?) + spawns sink/1 → SpawnReq arg resolves to g(zero)); native gleam 344/344. NEXT: guard opcode dispatch = T023 (Guard/Ground/Known/Otherwise/NoReaders/GroundEqual/Unknown currently Unimplemented), T024 kernels, T022 scheduler loop (consume RunQueue, mint ids for SpawnReqs, register woken), T029 engine facade, T030 acceptance.
- [X] T022 [US1] Implement scheduler in `glp_gleam/src/glp/engine/scheduler.gleam` (R2 scheduler-actor: pure stepping, run queue, reduction budget, generation-scoped reactivation dedup — FR-005) ◇ 2026-07-12: immutable `Engine` (program+heap+RunQueue+goal-store+next_id) run loop over T021 `reduce`; Reduced→mint ids for SpawnReqs + reactivate woken GoalRefs + drop goal; Suspended→`heap.suspend_on_writer` per waited writer (heap-driven reactivation) + generation++ + keep in store; Failed→drop; Budget/Error surfaced. Reactivation dedup via `types.enqueue_wake` (FR-005). `boot`/`run(reduction_budget, fuel)`. 2 e2e gleeunit: run_flip (body spawns flip → binds query var, quiescent) + rendezvous (flip SUSPENDS on unbound X, set_zero binds X → WAKES flip → resumes → Out=one — first cross-goal suspend/reactivate). native gleam 346/346.
- [ ] T023 [US1] Implement guard library in `glp_gleam/src/glp/engine/guards.gleam` (`ground/1`, `known/1`, `otherwise`, `=?=`, arithmetic comparisons, `wait_until` — three-valued: succeed/suspend/fail; semantics frozen, any gap STOPs and escalates) ◇ 2026-07-12 STRUCTURAL GUARDS DONE (in `runner.gleam`, NOT a separate guards.gleam — they read the runner's σ̂w/clause-var state; a separate module would import-cycle with the runner dispatch; shared-state extraction to `state.gleam` is the follow-up if a split is wanted): `Ground`/`Known`/`Unknown`/`NoReaders`/`GroundEqual`(=?=)/`Otherwise` via a cycle-safe σ̂w-aware `collect_unbound` walker (writer→FAIL, unbound-readers→SUSPEND-on-terminal-writers, else SUCCEED; negation inverts; suspend unchanged) + `resolve_cvar` for =?= ground comparison. 4 e2e gleeunit (ground succeeds, otherwise-after-failure, =?= equal, =?= false→otherwise) w/ minimal prelude decls. native gleam 350/350, warning-free. DEFERRED to T024 arithmetic slice: the generic `Guard` opcode (arithmetic comparisons `< > =< >= =:= =\=`, term-order `@<`.., type-tests, `wait_until`) — routes to a shared arithmetic/`_evaluateGuard` evaluator that T024's `:=`/`=` kernels also need; currently Unimplemented (surfaced). ◇ 2026-07-12 CLOSED in T024: generic `Guard` opcode + shared `arith.gleam` evaluator landed; `wait`/`wait_until` surfaced as Unimplemented (effectful, out of pure-engine MVP). Guard library functionally complete for MVP.
- [X] T024 [US1] Implement body kernels in `glp_gleam/src/glp/engine/kernels.gleam` (`:=`/arithmetic, assignment `=`, remaining reference kernels needed by the corpus) ◇ 2026-07-12: TWO coupled deliverables + ONE shared numeric core. (A) **Generic `Guard` opcode** in `runner.gleam` (`guard_generic`/`g_walk`/`eval_guard`/`compare_terms`) — arithmetic comparisons `< > =< >= =:= =\=`, standard-order `@< @> @=< @>=` (Number<String<compound, FR-060), type tests `integer/atom/string/constant/number/list/is_list/compound/tuple/unknown/known/ground`, `=?=`; deref-with-tracking consults σ̂w on the deref TERMINAL writer (reader→paired-writer→σ̂w, Dart `_dereferenceWithTracking`); unbound reader (≠`unknown`) → SUSPEND, unbound writer → comparator FAILs; NEGATION inverts success↔fail; effectful `wait`/`wait_until` SURFACED as `Unimplemented` (need scheduler timers, out of pure-engine MVP — escalate, don't invent). (B) **Native body kernels** `kernels.gleam` (`_add`/`_sub`/`_mul`/`_div`/`_idiv`/`_mod`/`_neg`) via the `spawn()` label-miss dispatch (Dart runner.dart:3220-3256) — heap-only (post-Commit, no σ̂w → no runner import cycle), bind output writer + carry reactivations; abort surfaced as `RunnerError`. (C) **`arith.gleam`** shared numeric core (`NumV`, Dart `num` promotion: `+-*` int-or-float, `/` always float, `//` trunc-toward-zero int, `mod` euclidean int; `combine`/`compare`). FROZEN semantics ported 1:1 from Dart `_evaluateGuard`/`_evaluateArithmetic`/`_compareTerms`/`body_kernels.dart`. Findings: `:=` & `=` are self.glp GLP clauses (not kernels); `=` unfolds via PE unit-clause, `:=` needs prelude COMPILED into the program — a **T029 gap** (loader.load compiles only the source, not prelude clauses); direct kernel calls in a user body correctly reject (Dart parity, `builtinProcedures` omits `_add`). 15 gleeunit (5 generic-Guard via real pipeline + 10 direct kernel). native gleam **365/365**, warning-free. `:=` end-to-end deferred to T030 (needs T029 prelude thread). Guard opcode's deferred-from-T023 part CLOSED here.
- [ ] T025 [P] [US1] Engine semantics tests in `glp_gleam/test/glp/engine/runner_test.gleam` (three-phase ordering, suspend/reactivate-exactly-once, otherwise-after-failure-not-suspension)
- [ ] T026 [P] [US1] Adversarial writer-MGU suite in `glp_gleam/test/glp/engine/writer_mgu_adversarial_test.gleam` (reader/reader, writer/writer, nested structures, tentative-HEAD paths — contracts/proof-obligations.md)
- [X] T027 [US1] Author Lean proof PI:14 in `glp_gleam/lean/WriterMguBindsOnlyWriters/` (binding-step preserves writer-only invariant; `lake build` green)
- [ ] T028 [US1] Author prose proof `docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/writer_mgu_binds_only_writers/PROOF.md` + flip INDEX.md row OPEN→discharged with artifact links ◇ 2026-07-11: PROOF.md authored; INDEX flip deliberately deferred to the T026 four-artifact discharge commit (contracts/proof-obligations.md bookkeeping: Lean + prose + tests + INDEX in ONE checkpointed commit)
- [ ] T029 [US1] Implement engine-value facade in `glp_gleam/src/glp/engine.gleam` (`new/load/run/step` — opaque Engine, no global state; replaces the 033 placeholder)
- [ ] T030 [US1] US1 acceptance: smoke set of corpus programs (incl. one suspension case, one SRSW negative, one type negative) run via engine API with Dart-identical outcomes; record in `specs/050-full-gleam-combined/baseline.md`

**Checkpoint**: standalone engine functional — M1 gate PI:14 discharged; MVP demonstrable via gleeunit

---

## Phase 4: User Story 2 — Interactive REPL on the Gleam instance (Priority: P2)

**Goal**: user-facing REPL with `:trace`/`:limit`/`:quit`, reference-format outcomes, ED-1 envelope seam identical in-process and encoded

**Independent Test**: pipe `load <corpus file>` + goal into `gleam run`; outcomes match reference; `:limit` stops long-running goals the reference way

- [ ] T031 [US2] Implement REPL loop + entry point in `glp_gleam/src/glp/repl/repl.gleam` and `glp_gleam/src/glp_gleam.gleam` (gleam run main; scripted newline-fed stdin mode, non-interactive exit)
- [ ] T032 [US2] Implement REPL commands in `glp_gleam/src/glp/repl/commands.gleam` (`load`, bare-path load, goal execution, `:trace`, `:limit <n>`, `:quit` — reference command semantics per contracts/gleam-instance-surface.md)
- [ ] T033 [US2] Bind results through the 038 envelope builder in `glp_gleam/src/glp/repl/results.gleam` (deep-resolve bindings, outcome classification success/suspension/failure — ED-1 in-process binding)
- [ ] T034 [US2] Implement output-capture seam in `glp_gleam/src/glp/engine/output_capture.gleam` (captured program output flows into the ResultEnvelope — greenfield; folds constituent #10 scope)
- [ ] T035 [P] [US2] REPL scripted-mode tests in `glp_gleam/test/glp/repl/repl_test.gleam` (piped session: load, goal, :trace shape, :limit exhaustion, :quit exit 0)
- [ ] T036 [US2] Envelope-identity test in `glp_gleam/test/glp/repl/envelope_identity_test.gleam` (same computation in-process vs TLV-encoded → byte-identical envelopes — FR-009/SC-004 corollary)

**Checkpoint**: a person can use the Gleam GLP instance — M1 user surface complete

---

## Phase 5: User Story 3 — Shared test corpus green with recorded-output parity (Priority: P2) — M1 LOCK

**Goal**: 100% agreement vs recorded Dart goldens; GAP/FORK cases explicit; differential harness (MISS-04) built; 10× bound checked

**Independent Test**: `bash test/parity/run_gleam_corpus.sh` exits 0 with 100% agreement and wall-clock summary

- [ ] T037 [US3] Implement `test/parity/record_dart_goldens.sh` (runs Dart REPL per corpus case, records normalized outcome + wall-clock into `test/parity/goldens/`; explicit re-record only — contracts/corpus-parity.md). FIRST emit a reviewed `test/parity/corpus-manifest.md` pinning the case list (per-section include/exclude rationale over `test/run_all_tests.sh` sections A–K); goldens and parity (SC-001) are measured against the manifest (analyze A1)
- [ ] T038 [US3] Implement shared normalization rules in `test/parity/lib/normalize.sh` (strip prompts/timing noise, stabilize variable numbering; sourced by recorder AND comparator)
- [ ] T039 [US3] Implement `test/parity/run_gleam_corpus.sh` (drives Gleam REPL over the same case list, diffs vs goldens, asserts suite-level `gleam ≤ 10× dart` wall-clock, prints agreement summary)
- [ ] T040 [P] [US3] Author GAP-G1/G2/G3/G8 + FORK-1 cases as named programs in `programs/tests/typed/` (per register definitions in `docs/research/glp-gleam-baseline/pipelines/P2-concerns/REGISTER.md`; single corpus home — no copies)
- [ ] T041 [US3] Implement `test/parity/run_differential.sh <program> <goal>` (Dart+C#+Gleam, three-column diff on divergence, exit = divergent pairs — closes MISS-04/FR-012)
- [ ] T042 [US3] Drive full corpus to 100% agreement (iterate port fixes; divergences handled per Bug Protocol three-way classification — never adjust a golden to pass)
- [ ] T043 [US3] Verify the 10× wall-clock bound (SC-009) and record both sums in the runner summary + `specs/050-full-gleam-combined/baseline.md`
- [ ] T044 [US3] Regression guard (SC-007): `bash test/run_all_tests.sh` + C# suites + `bash test/link/run_link_tests_cross.sh` all green after corpus work (shared-file changes in `programs/tests/`)

**Checkpoint**: M1 LOCK — parity declared (goldens + GAP/FORK green + PI:14 discharged)

---

## Phase 6: User Story 4 — Gleam instance joins peer-to-peer network links (Priority: P3)

**Goal**: link primitives + FrameCodec parity + dist-unify over loopback, TCP, and QUIC-WS/HTTP3 (all gating); quiescence oracle; PI:17 discharged

**Independent Test**: two Gleam instances exchange terms over each transport; received terms equivalent; wire bytes match reference codec; hostile frames rejected as fault terms

- [ ] T045 [US4] Define transport seam in `glp_gleam/src/glp/link/seam/transport.gleam` (port of `i_link_transport`: connect/accept/send/recv/close; scheme-agnostic layer above — contracts/link-parity.md)
- [ ] T046 [US4] Port FrameCodec + CRC32 to `glp_gleam/src/glp/link/reliability/frame_codec.gleam` (byte parity with `frame_codec.dart`/`FrameCodec.cs`: header/flags/sequence/CRC32)
- [ ] T047 [P] [US4] Frame-codec parity tests vs golden vectors in `glp_gleam/test/glp/link/frame_codec_test.gleam` (vectors from `specs/038-result-codec-and-framecodec-ride/contracts/golden/`)
- [ ] T048 [US4] Implement loopback transport in `glp_gleam/src/glp/link/transports/loopback.gleam` (in-BEAM message passing behind the seam)
- [ ] T049 [US4] Implement TCP transport in `glp_gleam/src/glp/link/transports/tcp.gleam` (gen_tcp via FFI/gleam_erlang; interop peer: C# `TcpTransport`, Dart `tcp_transport`)
- [ ] T050 [US4] Port link primitives to `glp_gleam/src/glp/link/primitives/` (link_send/link_recv, establish/listen/accept/request/setup/close/monitor kernels, registry, pump, egress — 025 contracts verbatim; GLP-visible semantics unchanged, deviations STOP and escalate)
- [ ] T051 [US4] Implement distributed unification in `glp_gleam/src/glp/link/dist_unify.gleam` (deferred-local-assignment; globalize/localize on `known/1`; RemoteVarRef per data-model.md)
- [ ] T052 [US4] Implement fault-as-data + untrusted-frame hardening in `glp_gleam/src/glp/link/faults.gleam` (length→CRC→type validation before decode; violations → fault term, never crash — FR-015, D11/D12)
- [ ] T053 [P] [US4] Adversarial untrusted-input tests in `glp_gleam/test/glp/link/untrusted_frame_test.gleam` (malformed/truncated/oversized/type-confused frames)
- [ ] T054 [US4] Implement quiescence oracle (GAP-G6) in `glp_gleam/src/glp/link/quiescence.gleam` + test `glp_gleam/test/glp/link/quiescence_test.gleam` (quiescent vs deadlocked vs running: no runnable goals + no in-flight frames)
- [ ] T055 [US4] Implement QUIC-WS transport in `glp_gleam/src/glp/link/transports/quic_ws.gleam` via `gleam_quic` Profile-C FFI (quicer/MsQuic; RFC-6455-over-QUIC-bidi framing parity with C# `WebSocketOverQuic.cs`; certs from `glpquick-cert/`; runtime WSL-only)
- [ ] T056 [P] [US4] Gleam↔Gleam link round-trip tests in `glp_gleam/test/glp/link/link_roundtrip_test.gleam` (loopback + TCP native/WSL; QUIC-WS under WSL)
- [ ] T057 [US4] Adversarial dist-deref suite in `glp_gleam/test/glp/link/dist_deref_adversarial_test.gleam` (FORK-1 circular cross-instance shapes, interleaved bind/deref races, fault-mid-deref)
- [ ] T058 [US4] Author Lean proof PI:17 in `glp_gleam/lean/DistDerefConvergence/` + prose `.../PROOFS/dist_deref_convergence/PROOF.md` + INDEX flip (recorded deviation: Lean replaces dossier's SPIN plan, owner-directed 2026-07-10)

**Checkpoint**: Gleam instance is distributed-capable; PI:17 + GAP-G6 discharged — M2 gates open

---

## Phase 7: User Story 5 — Cross-runtime C#↔Gleam distributed test pairs (Priority: P3) — M2 LOCK

**Goal**: 8 scenarios × 2 directions = 16/16 split C#↔Gleam, verdicts identical to single-runtime reference, across in-scope transports

**Independent Test**: `bash test/link/run_link_tests_cross_gleam.sh` exits 0 (16/16)

- [ ] T059 [US5] Implement `test/link/run_link_tests_cross_gleam.sh` (extends the 025 rig: boots the Gleam REPL as a role host alongside `out/csharp/glp_repl`; result rows under `test/link/results/`)
- [ ] T060 [US5] Verify the 8 pair programs (`programs/tests/link/{pc,sr,bidi,pathb,mon}.glp`) load clean on Gleam via the corpus pipeline; report (never patch around) any load divergence
- [ ] T061 [US5] Drive C#↔Gleam 16/16 green over TCP (both directions per scenario — FR-016 hard gate)
- [ ] T062 [US5] Run the suite over QUIC-WS under WSL vs the C# peer (+ loopback where the rig supports same-host pairs); assert identical per-scenario outcomes across transports (SC-008)
- [ ] T063 [US5] Integrate the quiescence oracle into the rig's completion detection and compare all verdicts vs single-runtime reference (SC-005)

**Checkpoint**: M2 LOCK — capstone proven

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T064 [P] Update docs: `docs/known-issues.md` (new instance quirks), `glp_gleam/README` usage; run quickstart.md end-to-end as written and fix drift
- [ ] T065 [P] Dossier bookkeeping in `docs/research/glp-gleam-baseline/pipelines/`: PROOFS/INDEX.md rows final, FB-M1-*/FB-M2-* obligations swept against PARITY-BAR.md, delivered D-refs annotated in RECONFIGURATION.md
- [ ] T066 Full acceptance sweep: verify SC-001..SC-009 one by one; record evidence in `specs/050-full-gleam-combined/acceptance.md` (record SC-008 interpretation: loopback applies to same-instance links (US4); cross-runtime coverage is TCP + QUIC-WS — analyze I1)
- [ ] T067 [P] Cleanup: remove dead 033 placeholder remnants, `gleam format`, lint pass across `glp_gleam/`
- [ ] T068 Final regression: Dart REPL suite, C# suites + Dart↔C# rig, `gleam test` (WSL), corpus parity, C#↔Gleam rig, both `lake build`s — all green in one recorded run

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)** → **Foundational (P2)** → user stories
- **US1 (Phase 3)**: only Foundational — the MVP
- **US2 (Phase 4)**: needs US1 T029 (engine facade); independent of US3+
- **US3 (Phase 5)**: needs US2 (REPL is the corpus-runner surface); T037/T038/T040/T041 can start once US1 is underway (recorder + goldens are Dart-side)
- **US4 (Phase 6)**: needs US1 (engine) + 038 codecs (exist); independent of US3 except shared Bug-Protocol discipline; T045–T049 need no REPL
- **US5 (Phase 7)**: needs US4 complete + US3's load-clean guarantee (T060)
- **Polish (Phase 8)**: last

### Key cross-task dependencies

- T020 (loader) needs T013–T019; T021–T024 need T006–T010; T027/T028 need T021 semantics stable; T030 needs T029
- T039 needs T031–T033 (REPL) + T037/T038; T042 needs T039–T041
- T050 needs T045–T046; T051 needs T050; T055 needs T045 (seam) — parallel to T050/T051; T058 needs T051+T057
- T061 needs T059+T060; T062 needs T055+T061

### Parallel Opportunities

- Setup: T002/T003/T004 together
- Foundational: T008/T009/T011 together (after T006–T007 start)
- US1: T015 ∥ T016; T025/T026 ∥ T027 authoring
- US3 recorder/goldens (T037/T038/T040) ∥ late US2 tasks
- US4: T047/T053 ∥ implementation neighbours; T048/T049 after T045; T055 ∥ T050–T052
- Polish: T064/T065/T067 together

## Parallel Example: User Story 1

```bash
# After T014 lands:
Task: "T015 parser negative tests in glp_gleam/test/glp/parser/parser_test.gleam"
Task: "T016 SRSW checker port in glp_gleam/src/glp/analysis/srsw.gleam"
# After T021/T022 land:
Task: "T025 engine semantics tests"
Task: "T026 adversarial writer-MGU suite"
Task: "T027 Lean PI:14 authoring"
```

## Implementation Strategy

**MVP first**: Setup → Foundational → US1 (engine + PI:14) — stop and validate via the smoke set (T030). Then US2 (REPL) → US3 (corpus = **M1 LOCK**) as the first shippable milestone; US4 (links + PI:17 + GAP-G6) → US5 (capstone = **M2 LOCK**). Each checkpoint is a marathon scoped-commit; baseline suites re-run before any checkpoint touching shared files (`programs/tests/`, `test/`).

## Notes

- 68 tasks: Setup 5, Foundational 7, US1 18, US2 6, US3 8, US4 14, US5 5, Polish 5
- Dart is the semantic oracle; C# is the QUIC-WS wire peer; the bytecode doc is the ISA authority
- Language semantics are frozen — any gap found mid-port STOPs and escalates (Constitution IV-a)
- Commit after each task or logical group via marathon checkpoints (scoped paths, never -A)
