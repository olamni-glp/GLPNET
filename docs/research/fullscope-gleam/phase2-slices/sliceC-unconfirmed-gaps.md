# Full-scope Gleam GLP — Deduplicated Delivered-vs-Gaps Inventory

**Date**: 2026-07-19 · **Method**: 3-role task team (spec-051), run `20260719T130005Z-782b`, frozen method `method-20260719T130005Z-782b` (3 codex red-team passes) · **Marathon**: `mrun-8bda036d9e9b` step `phase1-3rtask-gap-analysis` · **Anchor feature**: `full-scope-gleam-glp-implementation`

Three blind builders on pairwise-disjoint corpora — b1 roadmap+specs+handovers (design promises), b2 glp_gleam+gleam_quic code (implementation truth), b3 Dart/C# runtimes + normative docs + programs corpus (full-scope reference) — 206 attributed claims, joined mechanically by detail_id (154 capabilities), adjudicated by the cross-provider codex Critic (201 CONFIRM / 5 ESCALATE / 0 REFUTE; all 40 near-miss key pairs judged distinct). Verdict: budget_stop after cycle 1 (600k budget; cycle-2 self-verification pass is the named residual). Every entry cites its source claim_ids; full claims live in `.specify/3rtask/runs/20260719T130005Z-782b/cycle01/claims-builder-*.json`.

**Reading rule**: `delivery` is b2 code testimony ONLY; `promise` is b1 specs testimony; `parity-required` means b3 found it in the reference. UNCONFIRMED-GAP = promised/required with no b2 testimony either way (candidate gap pending code-side verification — NOT confirmed delivered).

**yngenios embeddability** is REQUIREMENTS-LEVEL: no yngenios sources exist in-repo; `embeddability-api` appears below at its evidence level (in-process engine-value API delivered; host-embedding/yngenios surface absent) and the yngenios integration itself is gap-by-definition.

---

## 7. UNCONFIRMED GAPS — promised (specs) or required (reference) with no Gleam code testimony (97)

### `acceptance-sweep-and-polish`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-075**: The feature-closing acceptance sweep (SC-001..SC-009 evidence in acceptance.md, dossier obligation sweep, docs, final all-green regression) is designed but entirely unstarted.
  - evidence: `specs/050-full-gleam-combined/tasks.md:159-163 (T064-T068 all [ ])` · test: `no-test: T068 final regression run is itself the planned evidence`

### `antlr-shared-grammar-spike`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`deferred` · parity-required=no · builders=b1
- **b1-c1-050**: The ANTLR4 shared-grammar spike is recorded absorbed/superseded for the Gleam path (hand-ported parser, no BEAM ANTLR target), with the canonical Glp.g4 artifact remaining an open dossier-level concern.
  - evidence: `specs/050-full-gleam-combined/spec.md:12 + research.md:5-11 (R1) + roadmap-snapshot-2026-07-19.md:38 ([refined])` · test: `no-test: absorbed scope decision, no artifact`

### `arithmetic-expression`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-017**: Arithmetic expression trees (Exp) evaluated in guards and body kernels are part of the language's delivered numeric semantics.
  - evidence: `programs/self.glp:22 (Exp type) with glp_runtime/lib/runtime/body_kernels.dart:108-134` · test: `programs/tests/typed/arith_comparison.glp`

### `atomvm-compatibility-by-construction`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-053**: AtomVM/lightweight-platform compatibility is designed as preserved-by-construction (no gleam_otp, plain spawn + Subjects, enforced by a delivered dependency-policy test), while actual AtomVM execution is explicitly excluded from 050 acceptance.
  - evidence: `specs/050-full-gleam-combined/spec.md:130,185 (FR-007 + Assumptions) + tasks.md:41 (T012 dep-policy assertion [X])` · test: `specs/050-full-gleam-combined/tasks.md:41 (deps_policy_test fails if an OTP-abstraction package appears)`

### `baseline-program-dossier`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-065**: The 036 baseline-program dossier — corpus-verified faithfulness spec, D1–D16 decisions, obligation registers, outcome-equivalence theorem, and the two-epic (Optional/Full-Gleam) reconfiguration — is recorded delivered and is the authoritative decision record behind 050.
  - evidence: `specs/036-glp-gleam-baseline-program/spec.md:17-66 + specs/050-full-gleam-combined/spec.md:188 (authoritative decision record) + roadmap-snapshot:85` · test: `no-test: research/verification program; proofs recorded in the P4 INDEX it created`

### `build-test-topology-windows`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-066**: The recorded build/test topology — native Windows gleam build with WSL-hosted gleeunit runs (path-separator defect) and cross-environment build-artifact cleanup — is a delivered operational decision of the Gleam workstream.
  - evidence: `specs/050-full-gleam-combined/research.md:63-69 (R8) + baseline.md:30-35 (T005 + mixed-artifact note)` · test: `specs/050-full-gleam-combined/baseline.md:30 (WSL gleam test verified)`

### `bytecode-instruction-set`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-006**: The normative v2.16 bytecode instruction set (control, commit, head/put/unify, spawn/requeue/proceed, allocate/deallocate, guard instructions) is the conformance target for any bytecode-level Gleam runner.
  - evidence: `docs/glp-bytecode-v216-complete.md sections 2-14 (with glp_runtime/lib/bytecode/opcodes.dart)` · test: `csharp/glp_il_codec.tests/DiscriminantCompletenessTests.cs`

### `bytecode-lint`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-070**: A bytecode-level linter is a delivered auxiliary tool of the Dart reference.
  - evidence: `glp_runtime/lib/lint/linter.dart:1-12` · test: `no-test: no in-slice test observed for the linter`

### `bytecode-mode-conversion`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-007**: Mode-aware argument loading (GetVariable/GetValue with isReader polarity) and the WxW no-writer-to-writer restriction are normative instruction-level semantics.
  - evidence: `docs/glp-bytecode-v216-complete.md sections 12 and 16` · test: `programs/tests/test_wxw.glp`

### `channel-convention`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-073**: The channel type convention and its creation/reception discipline are delivered normative behaviors of the reference language surface.
  - evidence: `docs/typed-glp-manual.md sections 4-5 and 10 (type at programs/self.glp:18)` · test: `programs/tests/test_new_channel_guard.glp; programs/tests/test_channel_route.glp`

### `clause-programming-idioms`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-071**: The canonical clause-programming idioms (bind/inject/handle/do and head-mode rules) are normative behaviors the corpus assumes of any conforming implementation.
  - evidence: `docs/glp-cheat-sheet.md sections 4-7` · test: `no-test: idioms are locked by the manual/cheat-sheet and pervade the programs/ corpus`

### `compile-mode-directive`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-030**: A system compile mode granting reserved-constant access and skipping reduce/2 generation is part of the delivered compiler directive surface.
  - evidence: `glp_runtime/lib/compiler/compiler.dart:111-122 (usage at glp_engine.dart:71-89; manual section 12)` · test: `programs/system/mad_predicates.glp`

### `compiled-il-on-the-wire`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-042**: Compiled-IL-on-the-wire with the compiler factored into the front-end is a designed, deliberately-deferred follow-up (roadmap refined, no spec dir).
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:37 ([refined])` · test: `no-test: no spec dir exists for this feature`

### `compiler-strict-mode`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-020**: Compilation offers a strict-types gate (report vs abort on type errors) as part of its public options.
  - evidence: `glp_runtime/lib/compiler/compiler.dart:19-30` · test: `programs/tests/type_errors/ (3 negative programs)`

### `crdt-convergence`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-064**: CRDT-based convergent messaging (rich-text ops over an op-WAL with dedup and reorder tolerance) is a delivered application capability of the C# reference.
  - evidence: `csharp/glp_crdtmsg/route/Mesh.cs:1-22 (CRDT core at csharp/glp_crdtmsg/crdt/)` · test: `csharp/glp_crdtmsg.tests/StoreConvergenceTests.cs; FugueTests.cs; PeritextTests.cs`

### `cross-runtime-pair-capstone`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-036**: The C#↔Gleam cross-runtime capstone (8 role-parameterized scenarios × 2 directions = 16/16 over TCP with QUIC-WS coverage, verdicts identical to single-runtime references) is a designed hard gate (M2 LOCK) with no delivery recorded.
  - evidence: `specs/050-full-gleam-combined/spec.md:90-102,151 (US5, FR-016) + contracts/link-parity.md:31-35 + tasks.md:147-151 (T059-T063 [ ])` · test: `no-test: run_link_tests_cross_gleam.sh is a planned artifact (tasks.md:147), unchecked`

### `differential-harness`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-021**: The cross-runtime differential harness (same program on Dart+C#+Gleam, three-column diff, exit = divergent pairs) is recorded delivered, closing MISS-04.
  - evidence: `specs/050-full-gleam-combined/contracts/corpus-parity.md:19-22 + tasks.md:107 (T041 [X]) + baseline.md:92-94` · test: `specs/050-full-gleam-combined/baseline.md:92-94 (validated on X:=2+3, primes(10), FORK-1)`

### `durable-mesh-messaging`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`partial` · parity-required=no · builders=b1
- **b1-c1-070**: Durable mesh messaging is partially recorded: a prototype is delivered while the full signal-then-fetch WAL/PGLite-tiered protocol remains a captured, unspecified ambition.
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:55 (prototype [closed, delivered]) + :105 (full protocol [captured])` · test: `no-test: roadmap rows only in this corpus`

### `embeddability-service-box`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`absent` · parity-required=no · builders=b1
- **b1-c1-051**: Yngenios-oriented embeddability (durable-listener-service-box, service/box boundary, store kernels) is absent from the design corpus beyond a promoted-but-unspecified roadmap row — a gap-by-definition at requirements level.
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:133 ([promoted], no spec reference)` · test: `no-test: no spec dir or contract exists anywhere under specs/ (repo-wide grep for yngenios/embeddab/service-box/store_put returned no spec hits)`

### `embedded-switch-role-framing`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-074**: The engine's embedded-switch role (routing between external connectivity and internal OS/actor QHSM/HSM actions) is a recorded design framing in the verification framework, with no implementing feature in the corpus.
  - evidence: `specs/027-refinement-verification-framework/spec.md:178 (FR-051)` · test: `no-test: framing requirement inside a delivered verification framework`

### `engine-composition-root`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-038**: Host-side composition-root wiring (kernels and transports injected onto a live engine, never referenced by it) is the delivered extension discipline.
  - evidence: `glp_runtime/bin/glp_repl.dart:51-101 (seam contract at glp_runtime/lib/link/primitives/link_kernels.dart:12-22)` · test: `csharp/glp_link.tests/LinkInfraTests.cs`

### `engine-csharp-parity`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-039**: A second, C# reference runtime exists (engine, bytecode runner, scheduler) and is exercised through a public seam by the in-slice codec equivalence tests, establishing cross-runtime parity as a full-scope requirement.
  - evidence: `csharp/glp_il_codec.tests/GlpExecutor.cs:1-25 (project reference at csharp/glp_il_codec.tests/GlpIlCodec.Tests.csproj:24)` · test: `csharp/glp_il_codec.tests/ExecuteEquivalenceTests.cs`

### `engine-instances-scaling-research`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-073**: The separation epic's scaling/alternative-backend research ambitions (C++ engine feasibility, many-instances shared-static-memory cooperative scheduling, staged LLVM programme) are designed-unstarted roadmap promises.
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:40-42 (all [refined])` · test: `no-test: research/feasibility rows, no spec dirs`

### `engine-review-dossier`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-039**: The FE/BE separation design dossier — covering the seam contract, binary wire shapes, control-program/client model, liveness/crash/restart, persistence and restore-and-resume, the mailbox decision, and the MVP slice — is recorded delivered as the epic's authoritative design.
  - evidence: `specs/026-engine-review-dossier/spec.md:32-68 (US1 design areas a-g) + roadmap-snapshot-2026-07-19.md:28 ([closed, delivered])` · test: `no-test: documentation/design deliverable by definition (spec.md:22-23)`

### `engine-state-snapshot-persistence`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-043**: The engine-state snapshot + persistence API is a designed roadmap promise whose open design forks were surfaced by the 026 dossier, still blocked behind the unstarted process split.
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:33 ([refined], blocked-by repl-engine-process-split-mvp)` · test: `no-test: no spec dir exists for this feature`

### `external-io`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-018**: A host I/O bridge (inject input terms, observe output streams) is a delivered runtime seam required for any embedded or interactive use.
  - evidence: `glp_runtime/lib/runtime/external_io.dart:1-15` · test: `no-test: exercised through multiagent plays at programs/multiagent/`

### `full-scope-gleam-anchor`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-059**: The full-scope-gleam-glp-implementation anchor is a captured, unspecified roadmap ambition blocked behind the in-progress combined Full-Gleam feature — its scope beyond 050 (including any FE/BE split or embeddability content) is not yet written down.
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:69 ([captured], blocked-by gleam-implementation-combined-full-gleam-feature)` · test: `no-test: captured row, no spec dir`

### `gap-fork-case-corpus`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-020**: The GAP-G1/G2/G3/G8 + FORK-1 parity-precondition cases are recorded delivered, with FORK-1's circular-deref discriminator resolved owner-directed to <circular> cycle detection matching Dart/C#.
  - evidence: `specs/050-full-gleam-combined/spec.md:140 (FR-011) + baseline.md:87-91 + docs/handover/050-full-gleam-M2-restart-2026-07-13.md:14` · test: `specs/050-full-gleam-combined/baseline.md:87-91 (T040 record)`

### `guard-defined`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-012**: Runtime-defined guards (user-declared test-only guard procedures compiled to a side table and evaluated three-valued) are part of the delivered guard system.
  - evidence: `glp_runtime/lib/bytecode/guard_defs.dart:1-10 (with runner.dart:56-66)` · test: `programs/tests/test_defined_guards.glp`

### `guard-purity`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-013**: Guard purity (guards may test but never bind or mutate) is a normative behavioral requirement.
  - evidence: `docs/glp-bytecode-v216-complete.md section 5; docs/typed-glp-manual.md section 6` · test: `no-test: negative-guard suite (unified section E) harness is outside this slice`

### `heap-value-copy-semantics`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-013**: The value-copy/immutable re-expression of the WAM mutable heap is the recorded, delivered design basis for the Gleam runtime, with lookups copying rather than aliasing.
  - evidence: `specs/034-glp-gleam-core-terms-and-heap/spec.md:14 (dossier §4.1 re-expression) + specs/050-full-gleam-combined/data-model.md:10-11` · test: `specs/050-full-gleam-combined/tasks.md:66 (T026 asserts post-heap state invariants)`

### `il-codec`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-059**: A self-describing bytecode wire codec with proven round-trip identity and execute-equivalence gating is the delivered program-shipping protocol.
  - evidence: `csharp/glp_il_codec/IlCodec.cs:1-25 (Lean proof at csharp/glp_il_codec/lean/IlCodecRoundTrip)` · test: `csharp/glp_il_codec.tests/RoundTripIdentityTests.cs; ExecuteEquivalenceTests.cs`

### `il-codec-round-trip`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-041**: The BytecodeProgram↔bytes round-trip codec (structural-identity equivalence, execute-equivalence gate, Lean decode∘encode=id) that de-risks persistence and compiled-IL-on-the-wire is recorded delivered.
  - evidence: `specs/029-il-codec-spike/spec.md:8-36 + roadmap-snapshot-2026-07-19.md:31 ([released, delivered])` · test: `specs/029-il-codec-spike/spec.md:34 (sorry-free Lean decode∘encode=id in scope)`

### `inbound-pump`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-048**: Asynchronous-transport ingress into the single-threaded runner via an inbound-pump seam is the delivered concurrency bridge for the link layer.
  - evidence: `glp_runtime/lib/runtime/inbound_pump.dart:1-20` · test: `csharp/glp_link.tests/LinkRecvIngressTests.cs`

### `instance-load-and-run`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-067**: US1 — load and run GLP programs on a standalone Gleam instance with staged rejection parity and suspension behaviour — is recorded delivered via the T030 smoke record and subsequent corpus lock.
  - evidence: `specs/050-full-gleam-combined/spec.md:26-39 (US1) + baseline.md:37-60 (T030 record)` · test: `specs/050-full-gleam-combined/baseline.md:45-53 (five-case smoke table, all agree)`

### `instance-network-join`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`partial` · parity-required=no · builders=b1
- **b1-c1-068**: US4 (Gleam instance joins peer-to-peer links) is partially recorded delivered: transport seam, frame codec, loopback, and TCP are done, while link primitives, distributed unification, fault hardening, quiescence, and QUIC-WS remain open.
  - evidence: `specs/050-full-gleam-combined/spec.md:74-87 (US4) + tasks.md:122-135 (T045-T049 [X], T050-T058 [ ]) + docs/handover/050-full-gleam-M2-restart-2026-07-13.md:3-5` · test: `specs/050-full-gleam-combined/tasks.md:124-126 (codec/transport smokes) vs 133-134 (round-trip/adversarial unchecked)`

### `langpair-dart-gleam`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-062**: The Dart→Gleam codeconv language pair (layout-agnostic mirror with the R3-b generic target-uniqueness assertion) is recorded delivered as the port's toolchain enabler.
  - evidence: `specs/032-codeconv-gleam-langpair/spec.md:1-27 + roadmap-snapshot-2026-07-19.md:50 ([released, delivered])` · test: `no-test: completion recorded as roadmap row state`

### `link-acceptance`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-058**: End-to-end link usage from GLP source (setup, handshake, monitored exchange, teardown) is a delivered, program-level user story a Gleam runtime must also pass.
  - evidence: `programs/tests/link/ (bidi.glp, pathb.glp, mon.glp, sr.glp, pc.glp, krepro.glp)` · test: `programs/tests/link/bidi.glp`

### `link-capability-gate`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-065**: Capability-gated link establishment (macaroon verify-before-act behind a gate seam) is a delivered security pattern of the reference link stack.
  - evidence: `csharp/glp_crdtmsg/bridge/MacaroonLinkGate.cs (gate seam at csharp/glp_link/seam/ICapabilityGate.cs and primitives/CapabilityGateRegistry.cs)` · test: `csharp/glp_link.tests/MacaroonGateTests.cs; csharp/glp_crdtmsg.tests/CapabilityTests.cs`

### `link-completion-live-repl-bridge`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-071**: HTTP3/QUIC+WS link completion (live glp_repl bridge, mesh fix, rebuild and re-verify) is a captured, unspecified residual ambition on the QUIC prototype line.
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:107 ([captured])` · test: `no-test: captured row, no spec dir`

### `link-reliability`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-051**: The link reliability sublayer (windowing, ordering, fencing, cycle-guard, reclaim) is a delivered cross-runtime capability set.
  - evidence: `glp_runtime/lib/link/reliability/ (11 files; C# mirror at csharp/glp_link/reliability/)` · test: `csharp/glp_link.tests/SendWindowTests.cs; OrderingTests.cs; FencingTests.cs; CycleGuardTests.cs; ReclaimTests.cs`

### `link-seam`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-047**: A transport-agnostic link seam with a registry for pluggable transports is the delivered extension point every new transport (including future Gleam ones) plugs into.
  - evidence: `glp_runtime/lib/link/seam/ (8 files; C# mirror at csharp/glp_link/seam/)` · test: `csharp/glp_link.tests/LinkInfraTests.cs`

### `link-transport-seam`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-027**: The scheme-agnostic link transport seam (port of i_link_transport as record-of-functions vtables with synchronous Results and out-of-band fault Subjects, QUIC token "quic") is recorded delivered.
  - evidence: `docs/handover/050-full-gleam-M2-restart-2026-07-13.md:6 (T045 landed, 7 seam modules) + specs/050-full-gleam-combined/tasks.md:122 (T045 [X])` · test: `no-test: seam is exercised by transport smoke tests (T048/T049 records)`

### `liveness-crash-restart-host`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-044**: The liveness + crash-signal + supervised-restart host is a designed-unstarted FE/BE promise, with the 039 monitor spike recording that BEAM supervision may supersede the C# liveness-host design.
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:34 ([refined])` · test: `no-test: no spec dir exists for this feature`

### `marathon-run-position`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-076**: The recorded work-position design for the Gleam feature is delivered-as-recorded: M1's marathon run discharged and an open M2 run with three registered M2-lock discharge gates (16/16 capstone, acceptance sweep, final regression).
  - evidence: `docs/handover/050-full-gleam-M2-restart-2026-07-13.md:5 (M2 run mrun-6bea075ec79e; M1 run discharged; 3 M2-lock gates registered)` · test: `no-test: process-state record`

### `mesh-full-mesh-native-quic`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-056**: The GLP-native true-QUIC full-mesh ambition (5 endpoints, 10 full-duplex QUIC links, crdtmsg envelopes with macaroons, GLP-program-driven setup and graceful termination) is designed/promoted with the C# REPL ruled as host — the Gleam instance has no recorded role in it.
  - evidence: `specs/050-glp-native-quic-link/spec.md:9-25 + roadmap-snapshot-2026-07-19.md:24 ([promoted])` · test: `no-test: cross-host mesh test is the feature's own planned acceptance`

### `message-envelope`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-063**: A router-opaque unified message envelope with verbatim-forwarded TLV payload sections is the delivered application-layer message protocol.
  - evidence: `csharp/glp_crdtmsg/header/UnifiedHeader.cs:1-27 (envelope primitives at csharp/glp_crdtmsg/envelope/)` · test: `csharp/glp_crdtmsg.tests/RouterTests.cs; FoundationTests.cs`

### `module-dynamic-dispatch`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-029**: Dynamic module activation (channel-served goal dispatch through '_activate' and _select/1) is a delivered runtime capability alongside static linking.
  - evidence: `glp_runtime/lib/engine/glp_engine.dart:66-82 (with glp_runtime/lib/runtime/glp_activation.dart:1-7 and body_kernels.dart:105)` · test: `programs/tests/dynamic_dispatch/ (4 programs)`

### `module-static-linking`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-028**: Static linking of multi-module projects into a single flat bytecode program (with per-module typecheck and renaming) is a delivered compiler capability.
  - evidence: `glp_runtime/lib/compiler/project_linker.dart:1-9` · test: `programs/tests/modules/ (8 module files)`

### `monitor-primitive-verification`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-054**: The M2-0 verification of erlang:monitor/'DOWN' behaviour on AtomVM 0.6.6 (grounding the fault-as-data model, with a D10 fallback inventory) is recorded delivered and relied upon by the link-layer design.
  - evidence: `specs/039-m2-0-verify-erlang-monitor-atomvm/spec.md:10-40 + roadmap-snapshot-2026-07-19.md:86 ([released, delivered])` · test: `specs/039-m2-0-verify-erlang-monitor-atomvm/spec.md:22 (minimal monitor/DOWN probe defined)`

### `multi-accept-transport-extension`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-046**: The multi-accept TCP transport extension is a designed-unstarted promise, and the delivered Gleam TCP transport is explicitly recorded as one-link-per-listen MVP.
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:36 ([refined, parallel-safe])` · test: `no-test: no spec dir exists for this feature`

### `multi-client-control-program`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-047**: A multi-client control program written in GLP is a designed-unstarted separation-epic promise whose design area is covered by the delivered 026 dossier.
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:39 ([refined])` · test: `no-test: no spec dir exists for this feature`

### `multiagent-boot-loader`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-043**: Declarative multiagent boot files (boot/0 with @agent spawn directives) are a delivered configuration surface.
  - evidence: `glp_runtime/lib/multiagent/boot_loader.dart:1-14` · test: `programs/multiagent/play_cold_call_test.glp`

### `multiagent-global-send`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-044**: The madGLP global-send protocol (stream-to-network predicates over a globalized-writer table) is the delivered inter-agent messaging contract.
  - evidence: `glp_runtime/lib/engine/glp_engine.dart:84-113 (table at glp_runtime/lib/multiagent/global_writers_table.dart:1-15)` · test: `programs/tests/test_relay_send.glp`

### `multiagent-isolate-manager`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-042**: Process-isolated agent execution (one isolate per agent, event-driven message routing) is the delivered multiagent concurrency model.
  - evidence: `glp_runtime/lib/multiagent/isolate_manager.dart:1-9` · test: `programs/multiagent/play_alice_bob.glp (via :boot)`

### `output-capture-seam`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-017**: The output-capture seam is recorded complete via the owner-chosen output-as-data design (captured program output flows through the runner/scheduler to the REPL ahead of the outcome block), folding the constituent structured-output-capture scope.
  - evidence: `specs/050-full-gleam-combined/tasks.md:85 (T034)` · test: `specs/050-full-gleam-combined/tasks.md:85 (8 tests; e2e emit(hello) verified)`

### `parser-recursive-descent`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-005**: A hand-ported recursive-descent lexer+parser conforming by corpus to the Dart grammar is recorded complete, with no ANTLR-generated parser introduced on BEAM.
  - evidence: `specs/050-full-gleam-combined/spec.md:122 (FR-002) + research.md:5-11 (R1) + tasks.md:53-54 (T013/T014)` · test: `specs/050-full-gleam-combined/tasks.md:55 (T015 parser negative tests [X])`

### `performance-sanity-bound`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-022**: The 10x wall-clock sanity bound (SC-009, explicitly not an optimization target) is recorded verified at roughly 1.35x of the Dart reference.
  - evidence: `specs/050-full-gleam-combined/spec.md:179 (SC-009) + baseline.md:66-69 (T043)` · test: `specs/050-full-gleam-combined/baseline.md:68 (gleam ~35.3s vs dart ~26.1s, ~1.35x)`

### `port-source-basis-dart`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-064**: The foundational port decision — Dart as the single source-of-truth basis for the Gleam port (overturning the C#-lean), with toolchain and AtomVM feasibility evidence — is recorded delivered.
  - evidence: `specs/031-gleam-port-spike/spec.md:9-30 + specs/034-glp-gleam-core-terms-and-heap/spec.md:11,21 (dossier §2.3 ratification) + roadmap-snapshot:49,84` · test: `no-test: decision-dossier deliverable (hello-GLP-term smoke recorded in dossier scope)`

### `premise-reconciliation-compiler-location`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-040**: Two FE/BE premise reconciliations are recorded decided: the parser/compiler stays engine-internal for the MVP (source text on the wire, compiler relocation a follow-up feature) and the engine does not generate IL at runtime (goal-term assembly over pre-compiled bytecode).
  - evidence: `specs/026-engine-review-dossier/spec.md:70-97 (US2)` · test: `no-test: design reconciliation, not code`

### `profile-c-quic-acceptance`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-055**: The 036-deferred HTTP3/QUIC-WS full-acceptance items (Erlang Profile-C via quicer NIF, two-host LAN e2e, marathon durability) are recorded delivered via wave 1, establishing the WSL-only Profile-C runtime ruling the Gleam QUIC transport builds on.
  - evidence: `specs/049-wave1-guard-link-acceptance/spec.md:1-35 + roadmap-snapshot-2026-07-19.md:22,118 (both rows [closed, delivered])` · test: `no-test: completion recorded as roadmap row states plus 050's Assumption that Profile-C runtime is WSL-only 'as established by feature 049'`

### `program-corpus`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-069**: A large canonical GLP program corpus (book, typed book, plays, paper examples) defines what a full-scope implementation must actually execute.
  - evidence: `programs/ (typed_book/ 223, book 2/ 147, multiagent/ 20, plays/play_coffee_shop/, paper/ 6, exercises/ 1)` · test: `no-test: the corpus itself is the acceptance material; harness outside slice`

### `proof-dist-deref-convergence`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-025**: The M2-gating proof obligation PI:17 (cross-instance deref chains terminate and converge under deferred-local-assignment) is fully designed with an owner-directed Lean-not-SPIN discharge form, but no discharge is recorded.
  - evidence: `specs/050-full-gleam-combined/contracts/proof-obligations.md:13-20 + tasks.md:135 (T058 [ ])` · test: `no-test: T057 adversarial dist-deref suite (tasks.md:134) unchecked`

### `proof-writer-mgu-value-copy`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-024**: The M1-gating proof obligation PI:14 (writer-MGU binds only writers under value-copy semantics) is recorded discharged in the contract-mandated Lean+prose+adversarial-tests+INDEX form.
  - evidence: `specs/050-full-gleam-combined/contracts/proof-obligations.md:5-11 + tasks.md:66-68 (T026/T027/T028)` · test: `specs/050-full-gleam-combined/tasks.md:66 (T026 10-test adversarial suite)`

### `qhsm-yngenios-integration-design`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-052**: A QHSM/YngeniOS(=NGENIUS) integration design — packaging the combined Gleam front+back instance as a QHSM inside the YngeniOS microkernel — is recorded delivered as a dossier (design-level only; no implementation feature exists in the corpus).
  - evidence: `specs/036-glp-gleam-baseline-program/spec.md:167-168 (FR-007) + tasks.md:44 (T010 [X] → pipelines/P7-qhsm-yngenios/DOSSIER.md)` · test: `no-test: design dossier deliverable; gate was 'concrete packaging design citing the sibling repos'`

### `quic-host`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-056**: A standalone QUIC+WS host process (client stdio bridge plus mesh-router server) is a delivered control-plane component of the reference stack.
  - evidence: `csharp/glp_quick_host/Program.cs:13-67` · test: `csharp/glp_link.tests/QuicMeshTests.cs`

### `quiescence-oracle`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-026**: The quiescence oracle (quiescent vs deadlocked vs running: no runnable goals + no in-flight frames) is designed as a hard precondition for distributed acceptance but not recorded delivered.
  - evidence: `specs/050-full-gleam-combined/data-model.md:66-67 + spec.md:155 (FR-017) + tasks.md:131,151 (T054/T063 [ ])` · test: `no-test: quiescence_test.gleam is a planned artifact (tasks.md:131), unchecked`

### `reduce-metainterpreter`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-031**: Auto-generated reduce/2 clauses plus the standard metainterpreter idiom are delivered and required for meta-level programs.
  - evidence: `docs/typed-glp-manual.md section 13 (generation at glp_runtime/lib/compiler/compiler.dart:111-122)` · test: `programs/tests/tracing_meta.glp`

### `reference-envelope-and-capture-seam`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-049**: The reference-side ED-1 seam features — self-contained result envelope with server-side deep-resolve and the structured output-capture seam — are recorded delivered and are relied-upon prerequisites of the Gleam instance.
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:29-30 ([released, delivered] x2)` · test: `no-test: completion recorded only as roadmap row state`

### `regression-guard`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-023**: The SC-007 regression guard is recorded satisfied at M1: Dart 530/531 (pre-existing Section-Q AOT failure documented), Dart↔C# rig 16/16, all seven C# suites 727/727, gleam 443/443.
  - evidence: `specs/050-full-gleam-combined/baseline.md:95-99 (T044) + docs/handover/050-full-gleam-M2-restart-2026-07-13.md:15` · test: `specs/050-full-gleam-combined/baseline.md:95-99 (530/531, 16/16, 727/727, 443/443)`

### `repl-boot-command`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-036**: Multiagent boot execution from the REPL (:boot with timeout) is a delivered interface.
  - evidence: `glp_runtime/bin/glp_repl.dart:189-201` · test: `programs/multiagent/play_alice_bob.glp`

### `repl-bytecode-command`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-034**: Bytecode disassembly of loaded programs is a delivered REPL inspection command.
  - evidence: `glp_runtime/bin/glp_repl.dart:203-217` · test: `no-test: inspection-only command`

### `repl-engine-split-binary-wire-mvp`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-048**: The C# REPL/engine split MVP with a binary wire-format intermediate language is recorded delivered on the roadmap (reference-runtime side of the FE/BE separation).
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:27 ([closed, delivered])` · test: `no-test: completion recorded only as roadmap row state`

### `repl-limit-command`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-035**: A settable goal-reduction limit is a delivered REPL/engine control.
  - evidence: `glp_runtime/bin/glp_repl.dart:157-171` · test: `no-test: exercised by long-running plays requiring :limit`

### `repl-trace-command`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-033**: Per-reduction tracing and debug-output toggles are delivered REPL commands.
  - evidence: `glp_runtime/bin/glp_repl.dart:133-143` · test: `programs/tests/test_tracing.glp`

### `restore-and-resume-link-reestablish`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-045**: Restore-and-resume with link re-establish is a designed-unstarted FE/BE promise at the end of the separation epic's dependency chain.
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:35 ([refined])` · test: `no-test: no spec dir exists for this feature`

### `result-envelope-builder`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-061**: Bridging live engine results into the heap-independent envelope (deep-resolve with explicit truncation) is a delivered builder interface in both runtimes.
  - evidence: `glp_runtime/lib/codec/result_envelope_builder.dart:1-14 (C# at csharp/glp_result_codec_builder/)` · test: `csharp/glp_result_codec_builder/tests/`

### `result-envelope-codec`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-060**: A cross-runtime result-envelope wire format for execution outcomes (status, bindings, suspensions, errors) is a delivered protocol with byte-level parity locks.
  - evidence: `glp_runtime/lib/codec/result_envelope_codec.dart:1-25 (C# at csharp/glp_result_codec/; Lean proof at csharp/glp_result_codec/lean/ResultTermRoundTrip)` · test: `csharp/glp_result_codec/tests/GoldenByteIdentityTests.cs; RoundTripTests.cs`

### `roadmap-constituent-reconciliation`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-060**: Per the recorded reconciliation protocol, the six constituent Full-Gleam roadmap rows deliberately remain 'refined' (and wave-3 superseded) until the combined feature ships, so roadmap row states currently understate recorded M1 delivery.
  - evidence: `specs/050-full-gleam-combined/spec.md:12,191 + roadmap-snapshot-2026-07-19.md:62-67 (six rows still [refined])` · test: `no-test: bookkeeping protocol`

### `runtime-gap-features-reference`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-072**: Of the language-level runtime-gap promises, comparison guards are recorded delivered while nested-structure HEAD-phase matching and the FCP-exact abandon operation remain designed-unstarted in the reference runtimes (and correspondingly unpromised for Gleam).
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:10-12 (comparison-guards [released]; nested-structure-head-matching, abandon-operation [refined])` · test: `no-test: refined rows have no spec dirs`

### `scheduler-fairness`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-009**: Goal scheduling requires FIFO fairness between concurrent goals and a bounded tail-recursion budget that yields to the host event loop to avoid starving I/O and timers.
  - evidence: `docs/glp-runtime-spec.txt:432-434 (with glp_runtime/lib/runtime/fairness.dart)` · test: `no-test: fairness locked by spec text; no in-slice test program`

### `schema-language`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-066**: A wire-schema language toolchain (parse, validate, evolve, CDDL interop) is a delivered C#-side capability of the reference stack.
  - evidence: `csharp/glp_schema_lang/parser/SchemaDslParser.cs:1-21 (full package at csharp/glp_schema_lang/)` · test: `csharp/glp_schema_lang.tests/`

### `srsw-anonymous-writer`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-072**: The anonymous-variable SRSW exemption is a normative language behavior locked by the manual.
  - evidence: `docs/typed-glp-manual.md section 9 (with docs/glp-cheat-sheet.md section 3)` · test: `programs/tests/test_passthrough.glp`

### `stream-mutual-reference`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-016**: O(1) stream append via mutual-reference kernels is a delivered runtime capability the stream-heavy corpus depends on.
  - evidence: `glp_runtime/lib/runtime/body_kernels.dart:93-96` · test: `programs/tests/test_mutual_ref.glp`

### `subtree-scaffold`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-063**: The glp_gleam subtree scaffold (committed hand-authored home, 1:1 placeholders for all 8 Dart subsystems, local WSL build+test smoke gate) is recorded delivered.
  - evidence: `specs/033-glp-gleam-subtree-scaffold/spec.md:14-24 + roadmap-snapshot-2026-07-19.md:51 ([released, delivered])` · test: `specs/033-glp-gleam-subtree-scaffold/contracts/build-test-smoke.md (local WSL smoke gate)`

### `suspension-abandonment`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-008**: Explicit suspension abandonment is a defined runtime operation alongside automatic reactivation.
  - evidence: `docs/glp-bytecode-v216-complete.md section 10.2 (with glp_runtime/lib/runtime/abandon.dart)` · test: `no-test: no in-slice test program identified for abandonment`

### `suspension-diagnostics`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-010**: The scheduler's drain result exposes three-valued execution status plus suspended-goal and blocking-reader diagnostics that the REPL and hosts report.
  - evidence: `glp_runtime/lib/runtime/scheduler.dart:6-21` · test: `no-test: consumed by engine/REPL status printing; unit tests outside slice`

### `system-predicate-registry`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-015**: Host-side system predicates with three-valued results (including suspension on unbound readers) and a standard I/O/arithmetic/module set are a required runtime extension surface.
  - evidence: `glp_runtime/lib/runtime/system_predicates.dart:16-79 (registration list at system_predicates_impl.dart:16-47)` · test: `programs/tests/test_time_guard.glp`

### `term-codec-tlv`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-061**: The Section-15 TLV term codec with cross-runtime byte-parity golden vectors is recorded delivered (feature 038) and reused unchanged as the Gleam wire and envelope payload foundation.
  - evidence: `specs/050-full-gleam-combined/data-model.md:38-39 (EXISTS, 038) + specs/038-result-codec-and-framecodec-ride/spec.md:37-48 + roadmap-snapshot:61 ([released, delivered])` · test: `specs/038-result-codec-and-framecodec-ride/contracts/golden/ (golden vectors referenced by 050 contracts)`

### `test-harness-corpus-parity`  (user-story)

- **Axes**: delivery=`no-code-testimony` · promise=`delivered` · parity-required=no · builders=b1
- **b1-c1-019**: US3 shared-corpus parity is recorded delivered at M1 LOCK: 206/206 (100%) agreement against recorded Dart goldens via the test/parity/ recorder, shared normalization lib, and Gleam corpus runner.
  - evidence: `docs/handover/050-full-gleam-M2-restart-2026-07-13.md:13 + specs/050-full-gleam-combined/baseline.md:64-69 + tasks.md:103-110 (T037-T044 [X])` · test: `specs/050-full-gleam-combined/baseline.md:66-69 (206/206 agree, 0 diverge)`

### `transport-parity-all-gating`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`partial` · parity-required=no · builders=b1
- **b1-c1-037**: Full multi-protocol transport parity is owner-clarified as all-gating (none deferred); of the three, loopback and TCP are recorded delivered while QUIC-WS and the identical-outcomes-across-transports matrix remain open.
  - evidence: `specs/050-full-gleam-combined/spec.md:18,146,178 (clarification + FR-014 + SC-008)` · test: `specs/050-full-gleam-combined/tasks.md:125-126 (loopback/TCP delivered) vs 132-133 (QUIC/matrix open)`

### `type-guard-set`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-025**: The builtin type/groundness guard vocabulary (integer, number, string, atom, constant, compound, tuple, list, is_list, module, ground, known) is fixed by the root prelude.
  - evidence: `programs/self.glp:28-40` · test: `programs/tests/test_ground.glp; programs/tests/test_channel_guards.glp`

### `type-parameterized`  (pattern)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-024**: Parameterized types (in definitions, procedure declarations, and modules) are a delivered and mandated type-system feature.
  - evidence: `docs/typed-glp-manual.md section 17 (with glp_runtime/lib/analysis/type_checker/param_expansion.dart)` · test: `programs/tests/typed/ corpus`

### `untrusted-frame-hardening`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`partial` · parity-required=no · builders=b1
- **b1-c1-035**: Untrusted-frame hardening is partially recorded: codec-level loud-fail rejection in reference order is delivered, while the length→CRC→type fault-as-data boundary module and its adversarial suite (FR-015, D11/D12) remain designed-only.
  - evidence: `specs/050-full-gleam-combined/spec.md:147 (FR-015) + contracts/link-parity.md:13 + tasks.md:123,129-130 (T046 [X], T052/T053 [ ])` · test: `specs/050-full-gleam-combined/tasks.md:124 (rejection-order parity delivered) vs tasks.md:130 (T053 adversarial suite unchecked)`

### `websocket-framing`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-055**: RFC 6455 WebSocket framing over a QUIC stream is a delivered wire protocol in the C# reference.
  - evidence: `csharp/glp_link/transports/WebSocketOverQuic.cs:1-20` · test: `csharp/glp_link.tests/WebSocketFramingTests.cs`

### `wire-registry`  (interface)

- **Axes**: delivery=`no-code-testimony` · promise=`not-promised` · parity-required=yes · builders=b3
- **b3-c1-067**: A shared wire registry binding payload types to schemas is a delivered protocol-infrastructure component.
  - evidence: `csharp/glp_wire_registry/WireRegistry.cs` · test: `csharp/glp_wire_registry.tests/`

### `zmq-comm-base`  (protocol)

- **Axes**: delivery=`no-code-testimony` · promise=`designed` · parity-required=no · builders=b1
- **b1-c1-069**: ZMQ base comm primitives remain a designed-unstarted transport ambition for the reference link layer, absent from the Gleam transport contract (loopback/TCP/QUIC only).
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:13 ([refined], blocked-by multi-protocol-link-layer)` · test: `no-test: no spec dir exists`

