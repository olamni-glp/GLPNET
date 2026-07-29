# Full-scope Gleam GLP — Feature Outline Plan (Phase 2)

> **STATUS: SUPERSEDED 2026-07-20.** Cycle 2 completed per the recorded G1 resume ruling; the FINAL plan is `feature-outline-plan-FINAL-2026-07-20.md` (88 CONFIRM / 0 blocked / 2 open escalations, dangling deps zero). This file is the cycle-1 historical record. Original banner follows.
>
> **STATUS: NON-FINAL (frozen-method E9).** The 3rtask plan run `20260719T134320Z-544f` hit its 350k token cap after cycle 1 (359k spent; budget gate = warn_confirm, run stopped — never a silent overrun). Per the red-teamed method, this synthesis MUST NOT be accepted as the feature plan until EITHER (i) a resumed session completes cycle 2 (repairing the BLOCKED section from persisted state in `.specify/3rtask/runs/20260719T134320Z-544f/`) OR (ii) the engineer records an explicit written waiver accepting it as-is. Decision is Gabi's.

**Date**: 2026-07-19 · **Feature**: `full-scope-gleam-glp-implementation` (roadmap, epic full-gleam, hard-dep on `gleam-implementation-combined-full-gleam-feature`) · **Marathon**: `mrun-8bda036d9e9b` step `phase2-3rtask-outline-plan` · **Input**: committed Phase-1 inventory `gap-inventory-2026-07-19.md` (154 capabilities) · **Method**: `method-20260719T134320Z-544f` (10 elements, 3 codex red-team passes) · **Adjudication**: codex, all 79 WPs (single-slice provisional by construction): 66 CONFIRM · 10 NOT-ACCEPTED (BLOCKED) · 3 ESCALATE.

**Coverage**: 154/154 inventory capabilities accounted (100% union, 0 uncovered, 0 status conflicts, 0 dependency cycles). 8 out-of-scope PROPOSALS (each carried by a rule-request — nothing leaves scope without a ruling).

**Drift controls carried by this plan**: frozen-interface register (wave 1) · grow-only pinned suites (Gleam 463/463 baseline, Dart unified suite, C# suites) · escalation register (below — never silently resolved) · every WP's acceptance evidence checkable from a fresh session · single feature, waves 1-5 internal · verify-before-close pairing on all 97 unconfirmed gaps.

---

## Wave 1 — FREEZE + GUARD (drift foundation) (14 accepted WPs)

### `freeze-bytecode-isa`  (freeze, S, builder-1)

- **Deliverable**: Frozen-interface register entry pinning the v2.16 bytecode ISA — the complete opcode union including reference-live spec-gap opcodes with mnemonic and reader/writer-flip table (glp_gleam/src/glp/bytecode/opcodes.gleam) and the BytecodeProgram model (label indexing, prelude-in-front merge, guard-spec table, disassembly, X registers; program.gleam) — which no build WP may extend or reorder without a rule-request.
- **Acceptance (restart-safe)**: docs/research/fullscope-gleam/phase2-plan/frozen-interface-register.md contains entry 'bytecode-isa'; WSL: cd glp_gleam && gleam test passes with glp_gleam/test/glp/bytecode/opcodes_test.gleam unmodified (git diff --exit-code vs freeze baseline).
- **Backing detail_ids**: `bytecode-opcode-table`, `bytecode-program-model`
- **Risk**: Singleton (b2-only) testimony — the ISA pin rides on the existing table-integrity test rather than cross-corpus corroboration, so a verify-wave cross-check against the normative v2.16 doc is a sensible other-builder complement.

### `freeze-codec-envelope`  (freeze, M, builder-1)

- **Deliverable**: Frozen-interface register entry pinning the ED-1 result seam as one wire contract — the byte-parity term codec with global variable identity replacing heap addresses, the 0x01/0x11 result envelope (status, bindings, var-to-writer, suspended, captured, error; canonical order-preserving), the depth-bounded deep-resolve builder with truncation and circular markers, the loud-fail rejection discipline, suspended-status reporting by global var ids, and in-process/wire byte-identity — this contract IS the FE/BE process-boundary payload for build-fe-be-process-split and the value surface for build-yngenios-embeddability, with the captured field frozen always-empty per the recorded owner-approved deferral.
- **Acceptance (restart-safe)**: docs/research/fullscope-gleam/phase2-plan/frozen-interface-register.md contains entry 'codec-envelope'; WSL: cd glp_gleam && gleam test passes with glp_gleam/test/glp/codec/result_envelope_codec_test.gleam, golden_corpus_test.gleam, loud_fail_fuzz_test.gleam, cyclic_term_test.gleam, deref_fidelity_test.gleam, suspended_acceptance_test.gleam and test/glp/repl/envelope_identity_test.gleam unmodified (git diff --exit-code vs freeze baseline).
- **Backing detail_ids**: `term-codec`, `result-envelope`, `envelope-builder`, `codec-loud-fail`, `deep-resolve-cycle-detection`, `result-seam-identity`, `suspension-result-reporting`
- **Risk**: Freezing captured-always-empty means any FE output-streaming need in the process split must arrive as a rule-request to unfreeze that field, never as a silent envelope extension.

### `freeze-compiler-pipeline`  (freeze, M, builder-1)

- **Deliverable**: Frozen-interface register entry pinning the single-entry unskippable load pipeline (parse -> SRSW -> partial-eval -> type-check -> v2.16 codegen via glp_gleam/src/glp/compiler/loader.gleam) with Dart-identical error text and positions, stage-attributed later-stages-do-not-run diagnostics, the sanctioned SRSW relaxations (incl. ground/1 D6, no escape mechanism), byte-identical-message moded type checking, and the pinned merge/3 codegen stream with its one documented semantically-neutral ground-list divergence — the BE-side load surface of the FE/BE process split.
- **Acceptance (restart-safe)**: docs/research/fullscope-gleam/phase2-plan/frozen-interface-register.md contains entry 'compiler-pipeline'; WSL: cd glp_gleam && gleam test passes with glp_gleam/test/glp/parser/parser_test.gleam, test/glp/analysis/srsw_test.gleam, test/glp/analysis/type_checker_test.gleam, test/glp/compiler/partial_eval_test.gleam, test/glp/compiler/codegen_test.gleam, test/glp/compiler/loader_test.gleam unmodified (git diff --exit-code vs freeze baseline).
- **Backing detail_ids**: `compiler-pipeline`, `parser-conformance`, `srsw-check`, `partial-evaluator`, `type-checker`, `compiler-codegen`, `diagnostics-staged-error`
- **Risk**: Byte-identical error freezes couple the pipeline to the Dart oracle's exact text, so an upstream oracle change surfaces as pinned-test failure — which is the intended loud signal, not an accident.

### `freeze-engine-execution`  (freeze, M, builder-1)

- **Deliverable**: Frozen-interface register entry pinning the engine execution contract — three-phase HEAD/GUARD/BODY clause execution with tentative-structure and clause-variable state (runner.gleam), the suspension-aware scheduler run loop with run queue, goal store, blocking-reader table and faithful terminal statuses (scheduler.gleam), (goal_id, suspension_generation) reactivation dedup with stale-wake dropping, the documented writer-address Si/U adaptation, and the StepOutcome single-reduction step seam (idle/reduced/suspended/failed/errored) — the step seam being the spine attachment for host-driven stepping in build-yngenios-embeddability.
- **Acceptance (restart-safe)**: docs/research/fullscope-gleam/phase2-plan/frozen-interface-register.md contains entry 'engine-execution'; WSL: cd glp_gleam && gleam test passes with glp_gleam/test/glp/engine/runner_test.gleam, scheduler_test.gleam, dedup_key_test.gleam, step_test.gleam unmodified (git diff --exit-code vs freeze baseline).
- **Depends on**: freeze-runtime-term-heap
- **Backing detail_ids**: `three-phase-execution`, `suspension-scheduler`, `suspension-wake-dedup`, `suspension-writer-address-model`, `scheduler-single-step`
- **Risk**: The one surfaced-unimplemented frozen-semantics gap (WRITE-mode void slot -> ConstTerm(null)) stays escalate-if-hit inside the freeze; a build WP hitting it must escalate, never patch ad hoc.

### `freeze-engine-facade`  (freeze, S, builder-1)

- **Deliverable**: Frozen-interface register entry pinning the engine-as-typed-value facade (construct, load with untype-checked self.glp prelude boot+merge, one-shot run to a ResultEnvelope, interactive start/step, zero global state; glp_gleam/src/glp/engine.gleam) plus _output/1 output-as-captured-data — this facade is the named yngenios-embeddability anchor (the delivered in-process engine-value API; host-embedding surface absent per inventory) on which build-yngenios-embeddability layers the host surface and behind which build-fe-be-process-split wraps the BE process.
- **Acceptance (restart-safe)**: docs/research/fullscope-gleam/phase2-plan/frozen-interface-register.md contains entry 'engine-facade'; WSL: cd glp_gleam && gleam test passes with glp_gleam/test/glp/engine_test.gleam and glp_gleam/test/glp/engine/output_capture_test.gleam unmodified (git diff --exit-code vs freeze baseline).
- **Depends on**: freeze-engine-execution
- **Backing detail_ids**: `engine-facade`, `output-capture`, `prelude-library`
- **Risk**: Any facade-shape change the embedding or split build WPs want becomes an explicit rule-request against this register entry rather than silent API drift on the spine's keystone.

### `freeze-link-transport-seam`  (freeze, S, builder-1)

- **Deliverable**: Frozen-interface register entry pinning the transport seam and its two delivered leaves — the in-BEAM loopback (hub+channel process rendezvous, FIFO exactly-once, close-drain, fault-on-send-after-close, no gleam_otp) and the raw-TCP transport (passive-mode gen_tcp FFI, one persistent duplex socket per bilateral link, role-order-independent connect retry) — the seam being the spine attachment point where build-fe-be-process-split adds its FE/BE process transport as a new peer leaf without touching the frozen ones, with loopback as the hermetic test substrate for that build.
- **Acceptance (restart-safe)**: docs/research/fullscope-gleam/phase2-plan/frozen-interface-register.md contains entry 'link-transport-seam'; WSL: cd glp_gleam && gleam test passes with glp_gleam/test/glp/link/loopback_test.gleam and tcp_test.gleam (incl. the 3 real-socket smoke tests) unmodified (git diff --exit-code vs freeze baseline).
- **Depends on**: freeze-link-wire
- **Backing detail_ids**: `loopback-transport`, `tcp-transport`
- **Risk**: The loopback full semantics matrix is deferred (T056 recorded), so the seam freeze holds only the observable semantics the existing smoke tests pin — a known-thin but honest guarantee.

### `freeze-link-wire`  (freeze, S, builder-1)

- **Deliverable**: Frozen-interface register entry pinning the link wire formats at Dart/C# byte parity — the fixed 22-byte big-endian frame header with Whole/Fragment kinds, 64 MiB cap, MTU fragmentation and errors-as-data (frame_codec.gleam), the pure-Gleam reflected-0xEDB88320 CRC-32 with canonical vector compute("123456789")==0xCBF43926 (crc32.gleam), and the 4-byte big-endian TCP length-prefix framing with FrameCodec payloads riding opaquely.
- **Acceptance (restart-safe)**: docs/research/fullscope-gleam/phase2-plan/frozen-interface-register.md contains entry 'link-wire'; WSL: cd glp_gleam && gleam test passes with glp_gleam/test/glp/link/frame_codec_test.gleam (incl. the exact wire-layout byte pin and canonical CRC vector) and tcp_test.gleam unmodified (git diff --exit-code vs freeze baseline).
- **Backing detail_ids**: `frame-codec`, `crc32-checksum`, `tcp-length-prefix-framing`
- **Risk**: The deep adversarial frame matrix remains deferred (T053 recorded in the inventory) — this freeze pins the format, not the missing matrix, which belongs to other-builder verify WPs.

### `freeze-platform-atomvm-policy`  (freeze, S, builder-1)

- **Deliverable**: Frozen-interface register entry elevating the delivered AtomVM-subset dependency policy — no OTP-abstraction package anywhere in the tree, plain spawn and Subjects only, enforced by the deps_policy test tripwire — to a feature-wide constraint binding every wave-4 build WP (the FE/BE process split on BEAM must be built inside this constraint).
- **Acceptance (restart-safe)**: docs/research/fullscope-gleam/phase2-plan/frozen-interface-register.md contains entry 'atomvm-policy'; WSL: cd glp_gleam && gleam test passes with glp_gleam/test/glp/deps_policy_test.gleam present and unmodified (git diff --exit-code vs freeze baseline).
- **Backing detail_ids**: `atomvm-dependency-policy`
- **Risk**: Process-split builders may be tempted toward gleam_otp conveniences; relaxing this policy requires a rule-request ruling, never a quiet dependency addition.

### `freeze-repl-surface`  (freeze, S, builder-1)

- **Deliverable**: Frozen-interface register entry pinning the test-visible REPL surface — the scripted EOF-terminating stdin loop over a threaded Session entered via gleam run, the reference command set (load, bare .glp paths, dotted goals, :trace, :limit incl. exhaustion behavior, :quit) with Dart-parity parse semantics, reference-shape bindings/status rendering from the ResultEnvelope, and arity-stripped reader-marked trace lines — the FE-parity reference whose output the split FE must stay byte-comparable to.
- **Acceptance (restart-safe)**: docs/research/fullscope-gleam/phase2-plan/frozen-interface-register.md contains entry 'repl-surface'; WSL: cd glp_gleam && gleam test passes with glp_gleam/test/glp/repl/repl_test.gleam, results_test.gleam and test/glp/engine/goal_format_test.gleam unmodified (git diff --exit-code vs freeze baseline).
- **Depends on**: freeze-codec-envelope
- **Backing detail_ids**: `repl-loop`, `repl-command-surface`, `repl-result-rendering`, `repl-trace-mode`
- **Risk**: build-fe-be-process-split will re-host this surface, so freezing the rendering shapes now is what keeps the future FE regression-comparable against the delivered REPL.

### `freeze-runtime-term-heap`  (freeze, S, builder-1)

- **Deliverable**: Frozen-interface register entry pinning the runtime term/heap contract — the 9-kind term ADT (glp_gleam/src/glp/runtime/terms.gleam), the immutable paired writer/reader heap with deref/bind_writer/suspend_on_writer (heap.gleam), the three-valued Success/Suspend/Fail unify verdict table (unify.gleam), the writer-MGU discipline (binds only writers, never readers, never writer-writer, sigma-hat atomic at Commit), and the opaque heap-level SuspensionTable (suspension.gleam) — as the bottom layer of the dependency spine that every downstream build WP (including build-fe-be-process-split and build-yngenios-embeddability) consumes unchanged.
- **Acceptance (restart-safe)**: docs/research/fullscope-gleam/phase2-plan/frozen-interface-register.md contains entry 'runtime-term-heap' listing the pinned public signatures of glp_gleam/src/glp/runtime/{terms,heap,unify,suspension}.gleam; WSL: cd glp_gleam && gleam test passes with glp_gleam/test/glp/runtime/terms_test.gleam, heap_test.gleam, unify_test.gleam, suspension_test.gleam and glp_gleam/test/glp/engine/writer_mgu_adversarial_test.gleam unmodified (git diff --exit-code on those test paths vs the freeze-baseline commit).
- **Backing detail_ids**: `term-representation`, `term-heap-unification`, `unification-three-valued-verdict`, `unification-writer-mgu`, `suspension-table`
- **Risk**: If a wave-4 build WP needs a heap or unify API extension, the freeze forces an explicit rule-request instead of silent drift — cheap now, possible friction later.

### `guard-atomvm-gated-probe`  (guard, S, builder-1)

- **Deliverable**: Dedicated guard for the one delivered capability that lives outside gleam test — the manually-run AtomVM conformance probe for the gated codec entries (float and 64-bit-int edges) — keeping the probe runnable and its gated-exclusion verdicts unchanged, since gleam test on full OTP is explicitly not an AtomVM-faithfulness signal.
- **Acceptance (restart-safe)**: Run glp_gleam/src/atomvm_gated_probe.gleam via the repo's Node AtomVM wrapper (its recorded manual procedure) and confirm the gated float/int64 entries remain excluded from byte-final goldens; git diff --exit-code glp_gleam/src/atomvm_gated_probe.gleam vs the freeze baseline is empty.
- **Backing detail_ids**: `atomvm-conformance-probe`
- **Risk**: Manual-only acceptance cannot ride the suite guard, so this guard depends on a human-in-the-loop run at feature checkpoints — the weakest drift control in the set, by the capability's own design.

### `guard-suite-csharp-reference`  (guard, S, builder-1)

- **Deliverable**: Guard pinning the C# reference suites green — the second cross-runtime parity oracle for the link wire formats, transports, and the term sub-codec golden vectors that the freeze-link-wire, freeze-link-transport-seam, and freeze-codec-envelope register entries are measured against.
- **Acceptance (restart-safe)**: dotnet test csharp/glp_link.tests (FrameCodecTests, TcpTransportTests, LoopbackTests) and dotnet test on the csharp/glp_result_codec test project (GoldenVectorTests, OracleConsistencyTests) -> all green.
- **Backing detail_ids**: `frame-codec`, `crc32-checksum`, `term-codec`, `tcp-transport`, `loopback-transport`
- **Risk**: Requires the .NET toolchain on the runner; if unavailable the guard must degrade loudly with a recorded gap, never be skipped silently.

### `guard-suite-dart-reference`  (guard, S, builder-1)

- **Deliverable**: Guard pinning the Dart reference oracle green — the unified REPL suite over the programs/tests/ corpus (incl. the 65-program typed corpus and type_errors negatives) that defines the byte-identical error text, prelude behavior, and REPL semantics the frozen Gleam pipeline and surfaces are measured against.
- **Acceptance (restart-safe)**: From the repo root: bash test/run_all_tests.sh -> unified REPL suite green (runtime + type-check sections over programs/tests/).
- **Backing detail_ids**: `compiler-pipeline`, `type-checker`, `prelude-library`, `repl-loop`, `three-phase-execution`
- **Risk**: If the Dart oracle drifts while only guard-suite-gleam is run, parity pins would be silently measured against a moved target — this guard must run alongside it at every checkpoint.

### `guard-suite-gleam`  (guard, S, builder-1)

- **Deliverable**: One pinned-suite guard holding the delivered 49-module gleeunit suite green for the whole feature — including the Dart-oracle parity methodology (hermetic corpora, golden byte vectors, byte-identical error strings), the build-and-run smoke anchor, and the empty-but-building placeholder scaffolds — answering Q7: this single suite guard covers every delivered capability that has an in-suite test, so no per-capability guards are needed beyond the three named exceptions (Dart oracle suite, C# reference suites, manual AtomVM probe).
- **Acceptance (restart-safe)**: WSL: cd glp_gleam && gleam test -> all tests pass (463/463 at freeze baseline; the count may only grow across waves, never shrink, and no test may be skipped or modified without a rule-request).
- **Backing detail_ids**: `test-harness`, `parity-oracle-testing`, `placeholder-module-scaffold`
- **Risk**: A single wide tripwire means one red test blocks all wave-4 build WPs — deliberate, since a shrinking or reddening suite is exactly the drift this guard exists to make loud.

## Wave 2 — VERIFY + RULE-REQUESTS (26 accepted WPs)

### `rule-request-codec-compiled-il-on-the-wire`  (rule-request, S, builder-3)

- **Deliverable**: An engineer ruling on the proposal to mark compiled-il-on-the-wire out-of-scope as post-feature-follow-on, per the owner-recorded 026 reconciliation (source text on the wire for the MVP, compiler relocation a deliberately-deferred follow-up with no spec dir).
- **Acceptance (restart-safe)**: Ruling recorded in docs/research/fullscope-gleam/phase2-verify/rulings.md citing the verify-codec-compiled-il-on-the-wire existence check and the b1-c1-040/b1-c1-042 deferral records
- **Depends on**: verify-codec-compiled-il-on-the-wire
- **Backing detail_ids**: `compiled-il-on-the-wire`
- **Risk**: The FE/BE build WP assumes source-text-on-the-wire, so a rejecting ruling changes the build-fe-be-process-split wire contract and must land before wave 4.

### `rule-request-compiler-antlr-shared-grammar-spike`  (rule-request, S, builder-3)

- **Deliverable**: An engineer ruling on the proposal to mark antlr-shared-grammar-spike out-of-scope as superseded (owner-recorded R1 absorption: hand-ported recursive-descent parser, no BEAM ANTLR target), with the canonical Glp.g4 concern re-anchored as a dossier follow-on.
- **Acceptance (restart-safe)**: Ruling (accept/reject with rationale) recorded in docs/research/fullscope-gleam/phase2-verify/rulings.md citing the verify-compiler-antlr-shared-grammar-spike existence-check output showing no ANTLR artifact on the Gleam path
- **Depends on**: verify-compiler-antlr-shared-grammar-spike
- **Backing detail_ids**: `antlr-shared-grammar-spike`
- **Risk**: If the ruling rejects, the spike re-enters close-compiler scope and its cost must be absorbed there.

### `rule-request-link-quic-relay`  (rule-request, S, builder-1)

- **Deliverable**: Engineer ruling requested on drift control for the sole delivered-but-untested capability — the Profile-A QUIC OS-port line relay (gleam_quic/src/glpq_ffi.erl, long-line reassembly, stdio byte-identity to the C# stack, gleam_quic/test empty): either freeze-by-file-pin as-is (git-hash pin, no behavioral guard) or require a minimal in-corpus relay smoke test before any wave-4 build WP may depend on it.
- **Acceptance (restart-safe)**: A recorded ruling for detail_id quic-sideprocess-relay in the run's rulings artifact (.specify/3rtask/runs/20260719T130005Z-782b/rulings.md or the engineer-designated equivalent) naming the chosen disposition and the WP that enforces it.
- **Depends on**: freeze-link-transport-seam
- **Backing detail_ids**: `quic-sideprocess-relay`
- **Risk**: Without a ruling this capability sits outside every guard — no test exists to pin — making it the single silent-drift hole in the delivered foundation; nothing may leave scope silently per E3e.

### `rule-request-process-engine-instances-scaling-research`  (rule-request, S, builder-3)

- **Deliverable**: An engineer ruling on the proposal to mark engine-instances-scaling-research out-of-scope as post-feature-follow-on: C++ engine feasibility, shared-static-memory many-instances scheduling, and the staged LLVM programme are research rows with no spec dirs, beyond the full-scope Gleam implementation.
- **Acceptance (restart-safe)**: Ruling recorded in docs/research/fullscope-gleam/phase2-verify/rulings.md citing the verify-process-baseline-program-dossier inspection of roadmap rows 40-42
- **Depends on**: verify-process-baseline-program-dossier
- **Backing detail_ids**: `engine-instances-scaling-research`
- **Risk**: If any scaling row is later ruled feature-relevant (e.g. many-instances for embeddability), the embeddability build WP inherits the requirement.

### `rule-request-quicws-mesh-full-mesh-native-quic`  (rule-request, S, builder-3)

- **Deliverable**: An engineer ruling on the proposal to mark mesh-full-mesh-native-quic out-of-scope as duplicate-of: it is its own promoted roadmap feature (glp-native-quic-link) with the C# REPL ruled as host and no recorded Gleam-instance role.
- **Acceptance (restart-safe)**: Ruling recorded in docs/research/fullscope-gleam/phase2-verify/rulings.md citing the verify-quicws-link-completion-live-repl-bridge scope check of specs/050-glp-native-quic-link/spec.md and the promoted roadmap row
- **Depends on**: verify-quicws-link-completion-live-repl-bridge
- **Backing detail_ids**: `mesh-full-mesh-native-quic`
- **Risk**: If the ruling instead assigns the Gleam instance a mesh role, close-quicws inherits a large new scope and the wave plan must be re-sized.

### `rule-request-transports-zmq-comm-base`  (rule-request, S, builder-3)

- **Deliverable**: An engineer ruling on the proposal to mark zmq-comm-base out-of-scope as external-dependency/post-feature-follow-on: it is blocked-by the reference-side multi-protocol-link-layer and explicitly absent from the Gleam transport contract (loopback/TCP/QUIC only).
- **Acceptance (restart-safe)**: Ruling recorded in docs/research/fullscope-gleam/phase2-verify/rulings.md citing the verify-transports-multi-accept-transport-extension check confirming ZMQ absence from both the Gleam contract and delivered reference code
- **Depends on**: verify-transports-multi-accept-transport-extension
- **Backing detail_ids**: `zmq-comm-base`
- **Risk**: Conflates two reasons (blocked upstream and contract-excluded), so the ruling must state which one governs for future reopening.

### `verify-acceptance-acceptance-sweep-and-polish`  (verify, S, builder-3)

- **Deliverable**: Confirmation that T059-T068 remain unstarted (no run_link_tests_cross_gleam.sh, no acceptance.md evidence) and a concrete work-scope statement for the 16/16 capstone and SC-001..SC-009 sweep feeding the close and accept WPs.
- **Acceptance (restart-safe)**: Run ls specs/050-full-gleam-combined/ and rg -l 'run_link_tests_cross_gleam' across the repo (expected absent); record tasks.md 147-163 checkbox state; scope statement at docs/research/fullscope-gleam/phase2-verify/verify-acceptance-acceptance-sweep-and-polish.md
- **Backing detail_ids**: `acceptance-sweep-and-polish`, `cross-runtime-pair-capstone`
- **Risk**: The capstone is an M2 LOCK hard gate, so under-scoping it here corrupts the whole wave-5 acceptance plan.

### `verify-bytecode-bytecode-instruction-set`  (verify, M, builder-3)

- **Deliverable**: Verdict on Gleam conformance to the v2.16 instruction set (discriminant-by-discriminant diff against docs/glp-bytecode-v216-complete.md sections 2-14), mode-aware GetVariable/GetValue polarity with the WxW restriction, and existence of any bytecode-linter port.
- **Acceptance (restart-safe)**: Diff the Gleam opcode enumeration against docs/glp-bytecode-v216-complete.md (mirroring csharp/glp_il_codec.tests/DiscriminantCompletenessTests.cs); execute programs/tests/test_wxw.glp via the differential harness; rg -n 'lint' glp_gleam/src; verdicts at docs/research/fullscope-gleam/phase2-verify/verify-bytecode-bytecode-instruction-set.md
- **Backing detail_ids**: `bytecode-instruction-set`, `bytecode-lint`, `bytecode-mode-conversion`
- **Risk**: The Gleam instance may be tree-walking rather than bytecode-level, in which case the verdict must state the conformance target is unmet by construction and escalate scope, not silently pass.

### `verify-codec-compiled-il-on-the-wire`  (verify, M, builder-3)

- **Deliverable**: Existence/scope verdict for the Gleam codec stack: Section-15 TLV term codec byte parity, result-envelope codec and deep-resolve builder, any Gleam IL codec (BytecodeProgram to bytes), and confirmation that compiled-IL-on-the-wire is absent by owner decision (source text on the wire for MVP).
- **Acceptance (restart-safe)**: Run the specs/038-result-codec-and-framecodec-ride/contracts/golden/ vectors against the Gleam codec via wsl -e bash -lc 'cd glp_gleam && gleam test'; rg -n 'il_codec|envelope|tlv' glp_gleam/src; record the b1-c1-042 deferral citation; verdicts at docs/research/fullscope-gleam/phase2-verify/verify-codec-compiled-il-on-the-wire.md
- **Backing detail_ids**: `compiled-il-on-the-wire`, `il-codec`, `il-codec-round-trip`, `result-envelope-builder`, `result-envelope-codec`, `term-codec-tlv`
- **Risk**: Byte-level parity claims require golden-vector reruns, not module presence, so a stale golden set would produce a false DELIVERED.

### `verify-compiler-antlr-shared-grammar-spike`  (verify, M, builder-3)

- **Deliverable**: Existence/scope verdict for the Gleam compiler chain: hand-ported recursive-descent parser, system compile-mode directive, strict-types gate, project static linking, dynamic _activate/_select dispatch, reduce/2 generation, and confirmation that no ANTLR artifact exists on the BEAM path (Glp.g4 status recorded).
- **Acceptance (restart-safe)**: Run rg -n 'strict|_activate|_select|reduce|linker|antlr' glp_gleam/src plus rg -l 'Glp.g4' across the repo; execute programs/tests/modules/, programs/tests/dynamic_dispatch/, programs/tests/tracing_meta.glp, programs/system/mad_predicates.glp and programs/tests/type_errors/ through the Gleam runner; verdicts at docs/research/fullscope-gleam/phase2-verify/verify-compiler-antlr-shared-grammar-spike.md
- **Backing detail_ids**: `antlr-shared-grammar-spike`, `compile-mode-directive`, `compiler-strict-mode`, `module-dynamic-dispatch`, `module-static-linking`, `parser-recursive-descent`, `reduce-metainterpreter`
- **Risk**: Compile-mode and reduce/2 generation are compiler-internal and may pass corpus runs while diverging internally, so negative programs (type_errors/) are required in the check.

### `verify-embed-embeddability-service-box`  (verify, S, builder-3)

- **Deliverable**: Repo-wide existence check confirming yngenios embeddability remains requirements-level (no spec dir, contract, or code) plus extraction of the P7 QHSM/YngeniOS packaging dossier into a concrete embeddability requirements checklist for build-yngenios-embeddability.
- **Acceptance (restart-safe)**: Run rg -in 'yngenios|embeddab|service-box|store_put|store_get' specs/ glp_gleam/ gleam_quic/ (expected: dossier-only hits); file-inspect specs/036-glp-gleam-baseline-program pipelines/P7-qhsm-yngenios/DOSSIER.md; requirements checklist committed at docs/research/fullscope-gleam/phase2-verify/verify-embed-embeddability-service-box.md
- **Backing detail_ids**: `embeddability-service-box`, `qhsm-yngenios-integration-design`
- **Risk**: Gap-by-definition entries tempt a no-op verdict, but the checklist extraction is the load-bearing output and must be concrete enough to build against.

### `verify-engine-engine-composition-root`  (verify, S, builder-3)

- **Deliverable**: Existence/scope verdict for the Gleam engine seam features: host-side composition-root injection of kernels/transports, the output-as-data capture flow (T034), and the ED-1 self-contained result envelope with server-side deep-resolve.
- **Acceptance (restart-safe)**: Run rg -n 'inject|kernel|capture|envelope' the Gleam engine facade modules in glp_gleam/src; re-run the T034 e2e emit(hello) check and the 8 recorded capture tests via wsl gleam test; verdicts at docs/research/fullscope-gleam/phase2-verify/verify-engine-engine-composition-root.md
- **Backing detail_ids**: `engine-composition-root`, `output-capture-seam`, `reference-envelope-and-capture-seam`
- **Risk**: Two of the three entries are recorded delivered only as roadmap/tasks rows, exactly the record-vs-code drift this feature's controls exist to catch.

### `verify-febe-embedded-switch-role-framing`  (verify, M, builder-3)

- **Deliverable**: Confirmation that no Gleam-side FE/BE process split exists plus a consolidated requirements baseline extracted from the 026 dossier, the two premise reconciliations, the C# split MVP, and the four designed-unstarted FE/BE promises (snapshot/persistence, liveness host, multi-client control program, restore-and-resume) as direct input to build-fe-be-process-split.
- **Acceptance (restart-safe)**: Run rg -n 'server|client|wire|socket|split' glp_gleam/src entry modules to confirm single-process shape; file-inspect specs/026-engine-review-dossier/spec.md US1/US2 and the roadmap rows for the four unstarted promises; requirements baseline committed at docs/research/fullscope-gleam/phase2-verify/verify-febe-embedded-switch-role-framing.md
- **Backing detail_ids**: `embedded-switch-role-framing`, `engine-review-dossier`, `engine-state-snapshot-persistence`, `liveness-crash-restart-host`, `multi-client-control-program`, `premise-reconciliation-compiler-location`, `repl-engine-split-binary-wire-mvp`, `restore-and-resume-link-reestablish`
- **Risk**: The dossier records open design forks (snapshot API) that the baseline must surface as decisions-needed, not resolve unilaterally.

### `verify-guards-guard-defined`  (verify, S, builder-3)

- **Deliverable**: Existence/scope verdict for runtime-defined guards (side-table compilation, three-valued evaluation) and guard purity enforcement in the Gleam runtime.
- **Acceptance (restart-safe)**: Run rg -n 'guard' glp_gleam/src; execute programs/tests/test_defined_guards.glp plus one negative guard-purity program through the Gleam runner and diff against Dart; verdicts at docs/research/fullscope-gleam/phase2-verify/verify-guards-guard-defined.md
- **Backing detail_ids**: `guard-defined`, `guard-purity`
- **Risk**: Guard purity is normative-doc-locked (manual section 6) with no in-slice negative harness, so an equivalent negative case must be run rather than assumed.

### `verify-langsurface-channel-convention`  (verify, M, builder-3)

- **Deliverable**: Verdict on whether the Gleam type-checker/parser honors the normative language surface: Channel convention, bind/inject/handle/do idioms, anonymous-writer SRSW exemption, the fixed self.glp guard vocabulary, and parameterized types.
- **Acceptance (restart-safe)**: Execute programs/tests/test_new_channel_guard.glp, test_channel_route.glp, test_passthrough.glp, test_ground.glp, test_channel_guards.glp and the programs/tests/typed/ corpus through the Gleam corpus runner against Dart goldens; per-detail_id verdicts at docs/research/fullscope-gleam/phase2-verify/verify-langsurface-channel-convention.md
- **Backing detail_ids**: `channel-convention`, `clause-programming-idioms`, `srsw-anonymous-writer`, `type-guard-set`, `type-parameterized`
- **Risk**: Idioms pervade the corpus rather than one test, so the check must sample enough typed programs to avoid a false DELIVERED from a narrow pass.

### `verify-link-inbound-pump`  (verify, M, builder-3)

- **Deliverable**: Code-vs-record verdict for the Gleam link stack: transport seam registry, T045 seam modules, inbound-pump ingress, link primitives, reliability sublayer (windowing/ordering/fencing/cycle-guard/reclaim), capability gate, and US4 open remainder (T050-T058) reconciled against actual glp_gleam code.
- **Acceptance (restart-safe)**: Run ls -R glp_gleam/src (link/seam/reliability directories) diffed against glp_runtime/lib/link/ and csharp/glp_link/ module lists; execute programs/tests/link/bidi.glp and mon.glp over the Gleam loopback and TCP transports; cross-check specs/050-full-gleam-combined/tasks.md 122-135 checkboxes against found code; verdicts at docs/research/fullscope-gleam/phase2-verify/verify-link-inbound-pump.md
- **Backing detail_ids**: `inbound-pump`, `instance-network-join`, `link-acceptance`, `link-capability-gate`, `link-reliability`, `link-seam`, `link-transport-seam`
- **Risk**: US4 is recorded partial, so the check must produce a per-sublayer boundary (delivered seam/codec/loopback/TCP vs open primitives/reliability/gate) instead of one batch verdict.

### `verify-multiagent-multiagent-boot-loader`  (verify, S, builder-3)

- **Deliverable**: Existence/scope verdict for the madGLP multiagent layer in Gleam: declarative boot files with @agent spawn directives, the global-send protocol over a globalized-writer table, and per-agent process isolation (BEAM processes standing in for Dart isolates).
- **Acceptance (restart-safe)**: Run rg -n 'boot|global|agent|spawn' glp_gleam/src; attempt programs/multiagent/play_alice_bob.glp and programs/tests/test_relay_send.glp on the Gleam instance and record outcomes; verdicts at docs/research/fullscope-gleam/phase2-verify/verify-multiagent-multiagent-boot-loader.md
- **Backing detail_ids**: `multiagent-boot-loader`, `multiagent-global-send`, `multiagent-isolate-manager`
- **Risk**: The multiagent layer is the most likely wholly-absent subsystem, so the check must scope the port cost honestly rather than fold it into a generic ABSENT.

### `verify-parity-differential-harness`  (verify, M, builder-3)

- **Deliverable**: Fresh-session re-execution of the recorded M1 parity evidence: three-runtime differential harness, C# engine seam, GAP/FORK case corpus, US1 load-and-run smoke, the 10x perf bound, the SC-007 suite counts, 206/206 corpus parity, and a scope statement of the full programs/ corpus against current Gleam coverage.
- **Acceptance (restart-safe)**: Re-run the T041 differential harness on X:=2+3, primes(10), FORK-1; re-run the test/parity/ recorder plus Gleam corpus runner and compare to the 206/206 record; re-run suite counts (Dart, C#, gleam) and the T043 timing; results table at docs/research/fullscope-gleam/phase2-verify/verify-parity-differential-harness.md
- **Backing detail_ids**: `differential-harness`, `engine-csharp-parity`, `gap-fork-case-corpus`, `instance-load-and-run`, `performance-sanity-bound`, `program-corpus`, `regression-guard`, `test-harness-corpus-parity`
- **Risk**: All eight entries are recorded-delivered without b2 testimony, so any re-run divergence is a drift finding that must halt and escalate rather than be patched inline.

### `verify-platform-atomvm-compatibility-by-construction`  (verify, S, builder-3)

- **Deliverable**: Fresh-session verification of the platform substrate: deps_policy_test still enforces no-OTP-abstraction, the Windows-build/WSL-test topology works from clean, all eight 033 scaffold placeholders are filled, and the 039 monitor probe, Dart-basis decision, and Dart-to-Gleam langpair records match reality.
- **Acceptance (restart-safe)**: Run wsl -e bash -lc 'cd glp_gleam && gleam build && gleam test' from a clean tree confirming deps_policy_test passes; ls glp_gleam/src against the 8 Dart subsystems; file-inspect specs/039-m2-0-verify-erlang-monitor-atomvm/spec.md, specs/031-gleam-port-spike/spec.md and specs/032-codeconv-gleam-langpair/spec.md; verdicts at docs/research/fullscope-gleam/phase2-verify/verify-platform-atomvm-compatibility-by-construction.md
- **Backing detail_ids**: `atomvm-compatibility-by-construction`, `build-test-topology-windows`, `langpair-dart-gleam`, `monitor-primitive-verification`, `port-source-basis-dart`, `subtree-scaffold`
- **Risk**: AtomVM execution is excluded from 050 acceptance, so the check must verify by-construction constraints only and flag any actual-AtomVM claims as out of this feature's evidence.

### `verify-process-baseline-program-dossier`  (verify, S, builder-3)

- **Deliverable**: File-inspection verdict that the six process/decision records (036 dossier plus P4 INDEX, scaling-research rows, full-scope anchor row, marathon M2 run gates, six-constituent reconciliation, runtime-gap rows) still match current repo state, with any drift flagged into the feature drift controls.
- **Acceptance (restart-safe)**: File-inspect specs/036-glp-gleam-baseline-program/spec.md and its P4 INDEX, docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md rows 33-42/62-69/105-133, and docs/handover/050-full-gleam-M2-restart-2026-07-13.md gate list; drift table at docs/research/fullscope-gleam/phase2-verify/verify-process-baseline-program-dossier.md
- **Backing detail_ids**: `baseline-program-dossier`, `engine-instances-scaling-research`, `full-scope-gleam-anchor`, `marathon-run-position`, `roadmap-constituent-reconciliation`, `runtime-gap-features-reference`
- **Risk**: Roadmap rows deliberately understate delivery per the reconciliation protocol, so naive row-reading would generate false drift findings.

### `verify-proofs-proof-dist-deref-convergence`  (verify, S, builder-3)

- **Deliverable**: Verification that the PI:14 discharge artifacts (Lean, prose, adversarial suite, INDEX per contracts/proof-obligations.md) exist and re-run green, and confirmation that PI:17 remains undischarged with T057/T058 unchecked, scoping the Lean discharge work for the close WP.
- **Acceptance (restart-safe)**: File-inspect specs/050-full-gleam-combined/contracts/proof-obligations.md 5-20 and the T026/T027/T028 artifacts; re-run the T026 10-test adversarial suite via wsl gleam test; confirm T057/T058 unchecked in tasks.md 134-135; verdicts at docs/research/fullscope-gleam/phase2-verify/verify-proofs-proof-dist-deref-convergence.md
- **Backing detail_ids**: `proof-dist-deref-convergence`, `proof-writer-mgu-value-copy`
- **Risk**: PI:17 is M2-gating with an owner-directed Lean-not-SPIN form, so mis-scoping the discharge here jeopardizes the distributed acceptance chain.

### `verify-quicws-link-completion-live-repl-bridge`  (verify, M, builder-3)

- **Deliverable**: Existence/scope verdict for the QUIC-WS line on the Gleam side: gleam_quic Profile-C transport state, RFC 6455 framing over QUIC, any Gleam role in the quic-host control plane, the live-repl-bridge residual, and the recorded scope of the native-QUIC full-mesh feature relative to the Gleam instance.
- **Acceptance (restart-safe)**: Run ls -R gleam_quic/ and rg -n 'websocket|rfc6455|quic' glp_gleam/src gleam_quic/; attempt one Gleam QUIC-WS loopback link in WSL (Profile-C per feature 049 ruling); record the mesh feature scope citation; verdicts at docs/research/fullscope-gleam/phase2-verify/verify-quicws-link-completion-live-repl-bridge.md
- **Backing detail_ids**: `link-completion-live-repl-bridge`, `mesh-full-mesh-native-quic`, `profile-c-quic-acceptance`, `quic-host`, `websocket-framing`
- **Risk**: Profile-C is WSL-only and environment-fragile, so an execution failure must be classified as environment versus absence before feeding SC-008 planning.

### `verify-repl-repl-boot-command`  (verify, S, builder-3)

- **Deliverable**: Existence/scope verdict on whether the Gleam instance front surface provides :boot, :bytecode disassembly, :limit and :trace equivalents, or whether these are host-level controls out of the Gleam surface.
- **Acceptance (restart-safe)**: Run rg -n 'boot|limit|trace|bytecode|command' over the Gleam frontend/entry modules in glp_gleam/src; drive the Gleam entry point with each command scripted and record observed behavior; verdicts at docs/research/fullscope-gleam/phase2-verify/verify-repl-repl-boot-command.md
- **Backing detail_ids**: `repl-boot-command`, `repl-bytecode-command`, `repl-limit-command`, `repl-trace-command`
- **Risk**: The Gleam instance may intentionally expose a narrower non-interactive surface, so the check must distinguish absent-capability from relocated-capability before any close work.

### `verify-runtime-arithmetic-expression`  (verify, M, builder-3)

- **Deliverable**: Existence/scope verdict (DELIVERED/ABSENT per detail_id) for the eight runtime-core capabilities in the Gleam runtime: arithmetic Exp evaluation, host I/O bridge, value-copy heap invariants, FIFO fairness plus bounded tail-recursion yield, O(1) mutual-reference stream append, suspension abandonment, drain-result suspension diagnostics, and the three-valued system-predicate registry.
- **Acceptance (restart-safe)**: Run rg -n 'abandon|fairness|external_io|system_predicate|mutual|drain' glp_gleam/src and wsl -e bash -lc 'cd glp_gleam && gleam test'; execute programs/tests/typed/arith_comparison.glp, programs/tests/test_mutual_ref.glp and programs/tests/test_time_guard.glp through the T041 differential harness; verdict table committed at docs/research/fullscope-gleam/phase2-verify/verify-runtime-arithmetic-expression.md
- **Backing detail_ids**: `arithmetic-expression`, `external-io`, `heap-value-copy-semantics`, `scheduler-fairness`, `stream-mutual-reference`, `suspension-abandonment`, `suspension-diagnostics`, `system-predicate-registry`
- **Risk**: Gleam module names may not mirror Dart file names, so a pure grep can miss delivered code and each ABSENT verdict must be confirmed by a failing corpus program.

### `verify-transports-multi-accept-transport-extension`  (verify, M, builder-3)

- **Deliverable**: Existence/scope verdict for transport hardening: multi-accept vs one-link-per-listen TCP, quiescence oracle presence (quiescence_test.gleam), the all-gating transport-parity matrix, the length-CRC-type fault-as-data boundary module with its adversarial suite, and confirmation of ZMQ absence from the Gleam transport contract.
- **Acceptance (restart-safe)**: Run rg -n 'accept|quiescen|zmq|crc' glp_gleam/src; check for quiescence_test.gleam and the T052/T053 boundary module; run the delivered frame-rejection-order test and record T050-T058 checkbox state from specs/050-full-gleam-combined/tasks.md; verdicts at docs/research/fullscope-gleam/phase2-verify/verify-transports-multi-accept-transport-extension.md
- **Backing detail_ids**: `multi-accept-transport-extension`, `quiescence-oracle`, `transport-parity-all-gating`, `untrusted-frame-hardening`, `zmq-comm-base`
- **Risk**: Transport parity is owner-clarified as all-gating, so any tempting per-transport deferral found here must route through a rule-request rather than a quiet verdict.

### `verify-wireproto-crdt-convergence`  (verify, M, builder-3)

- **Deliverable**: Existence/scope verdict for the application-layer wire stack in Gleam (CRDT convergent messaging, durable signal-then-fetch mesh protocol, router-opaque unified envelope, wire-schema toolchain, shared wire registry) including an explicit ruling-input on which items are full-scope-Gleam-required versus C#-host-side.
- **Acceptance (restart-safe)**: Run rg -n 'crdt|envelope|schema|registry|wal' glp_gleam/src gleam_quic/; diff against csharp/glp_crdtmsg/, csharp/glp_schema_lang/, csharp/glp_wire_registry/ component lists; per-item required-vs-host-side scope table at docs/research/fullscope-gleam/phase2-verify/verify-wireproto-crdt-convergence.md
- **Backing detail_ids**: `crdt-convergence`, `durable-mesh-messaging`, `message-envelope`, `schema-language`, `wire-registry`
- **Risk**: These are parity-required by b3 testimony but plausibly host-side by architecture, and misclassifying them either bloats the Gleam port or silently drops required parity.

## Wave 3 — CLOSE (partials + confirmed gaps) (23 accepted WPs)

### `close-acceptance-acceptance-sweep-and-polish`  (close, L, builder-3)

- **Deliverable**: The feature-closing build work lands: run_link_tests_cross_gleam.sh with the 8x2 role-parameterized capstone scenarios over TCP plus QUIC-WS coverage, and the SC-001..SC-009 evidence sweep assembled into acceptance.md.
- **Acceptance (restart-safe)**: run_link_tests_cross_gleam.sh exits 0 with 16/16 verdicts identical to single-runtime references; acceptance.md carries per-SC evidence rows; both committed under specs/050-full-gleam-combined/
- **Depends on**: verify-acceptance-acceptance-sweep-and-polish; close-link-inbound-pump; close-quicws-link-completion-live-repl-bridge
- **Backing detail_ids**: `acceptance-sweep-and-polish`, `cross-runtime-pair-capstone`
- **Risk**: The capstone is downstream of the entire link and transport chain, so it inherits every open defect found there.

### `close-bytecode-bytecode-instruction-set`  (close, M, builder-3)

- **Deliverable**: Gleam conformance to the v2.16 instruction set and mode-conversion semantics is confirmed or implemented, and the linter gap is either ported or explicitly recorded auxiliary-optional per the verify scope finding.
- **Acceptance (restart-safe)**: A Gleam discriminant-completeness test mirroring DiscriminantCompletenessTests.cs passes; test_wxw.glp agrees with Dart; linter disposition recorded in docs/research/fullscope-gleam/phase2-verify/close-bytecode-bytecode-instruction-set.md
- **Depends on**: verify-bytecode-bytecode-instruction-set
- **Backing detail_ids**: `bytecode-instruction-set`, `bytecode-lint`, `bytecode-mode-conversion`
- **Risk**: If the Gleam runner is not bytecode-level, this close escalates to an architecture decision rather than a fill-in port.

### `close-bytecode-runner-missing-opcodes`  (close, M, builder-2)

- **Deliverable**: Implement the Unimplemented runner faults for Requeue tail-calls and environment frames (Allocate/Deallocate) in glp_gleam/src/glp/engine/runner.gleam and apply the ruled UnifyConstant ground-struct-literal behavior in a golden pin, with Distribute/Transmit runtime explicitly owned by close-module-system-runtime-rpc to avoid double work.
- **Acceptance (restart-safe)**: `gleam test` in glp_gleam passes with new glp_gleam/test/glp/engine/runner_test.gleam cases executing Requeue and Allocate/Deallocate programs to completion (no Unimplemented fault) plus a golden opcode pin covering the ruled UnifyConstant ground-struct-literal case.
- **Depends on**: rule-bytecode-runner-unifyconstant-divergence; freeze-bytecode-isa (bound from freeze-bytecode-runner-interface)
- **Backing detail_ids**: `bytecode-runner`
- **Risk**: These opcodes are recorded deferred-as-unused (US4), so compiler emission paths may not yet produce them and compiler-side work could get dragged into this runner close.

### `close-codec-compiled-il-on-the-wire`  (close, M, builder-3)

- **Deliverable**: The Gleam codec stack closes per verdict: TLV and result-envelope byte parity confirmed against golden vectors, the IL codec ported or interop-consumed for program shipping, and compiled-IL-on-the-wire resolved per its rule-request.
- **Acceptance (restart-safe)**: All specs/038 golden vectors byte-identical on Gleam; a BytecodeProgram round-trips Gleam-encode to C#-decode (or the ruled alternative) in a committed test; record in docs/research/fullscope-gleam/phase2-verify/close-codec-compiled-il-on-the-wire.md
- **Depends on**: verify-codec-compiled-il-on-the-wire; rule-request-codec-compiled-il-on-the-wire
- **Backing detail_ids**: `compiled-il-on-the-wire`, `il-codec`, `il-codec-round-trip`, `result-envelope-builder`, `result-envelope-codec`, `term-codec-tlv`
- **Risk**: Byte-parity closes are brittle to endian/length-prefix drift, so goldens must be run from the shared contracts directory, never regenerated locally.

### `close-compiler-antlr-shared-grammar-spike`  (close, L, builder-3)

- **Deliverable**: Each compiler-chain gap is closed per verdict and ruling: confirmed with cited code, or implemented (strict gate, compile mode, linker, dispatch, reduce/2), with antlr-shared-grammar-spike resolved per its rule-request outcome.
- **Acceptance (restart-safe)**: programs/tests/modules/, dynamic_dispatch/, tracing_meta.glp, mad_predicates.glp pass and the three type_errors/ programs are rejected on Gleam identically to Dart; record in docs/research/fullscope-gleam/phase2-verify/close-compiler-antlr-shared-grammar-spike.md
- **Depends on**: verify-compiler-antlr-shared-grammar-spike; rule-request-compiler-antlr-shared-grammar-spike
- **Backing detail_ids**: `antlr-shared-grammar-spike`, `compile-mode-directive`, `compiler-strict-mode`, `module-dynamic-dispatch`, `module-static-linking`, `parser-recursive-descent`, `reduce-metainterpreter`
- **Risk**: reduce/2 generation plus the metainterpreter idiom couples compiler and runtime, so a partial close here can silently break meta-level corpus programs later.

### `close-embed-embeddability-service-box`  (close, S, builder-3)

- **Deliverable**: The embeddability gaps close at requirements level: the extracted P7 checklist is ratified into a service-box/store-kernel requirements contract (scope call: store_put/store_get kernels vs host-owned log carried to decision) handed to build-yngenios-embeddability.
- **Acceptance (restart-safe)**: A ratified embeddability requirements contract at docs/research/fullscope-gleam/phase2-verify/close-embed-embeddability-service-box.md with each requirement traced to the P7 dossier and marked build-bound
- **Depends on**: verify-embed-embeddability-service-box
- **Backing detail_ids**: `embeddability-service-box`, `qhsm-yngenios-integration-design`
- **Risk**: The open store-kernel scope call is Gabi-level and must be escalated, not resolved by the plan team.

### `close-engine-engine-composition-root`  (close, M, builder-3)

- **Deliverable**: The engine seam gaps close with code testimony: composition-root injection, output-as-data capture, and the deep-resolve result envelope confirmed or implemented in the Gleam engine facade with seam tests.
- **Acceptance (restart-safe)**: The T034 capture tests plus an injection test (kernel registered from the host, never referenced by the engine) green under wsl gleam test; record in docs/research/fullscope-gleam/phase2-verify/close-engine-engine-composition-root.md
- **Depends on**: verify-engine-engine-composition-root
- **Backing detail_ids**: `engine-composition-root`, `output-capture-seam`, `reference-envelope-and-capture-seam`
- **Risk**: The seam contract is the FE/BE and embeddability attachment point, so a weak close here propagates into both wave-4 builds.

### `close-febe-embedded-switch-role-framing`  (close, M, builder-3)

- **Deliverable**: The FE/BE gaps close at requirements level: the verified baseline is confirmed against the 026 dossier and C# split MVP, open design forks (snapshot API, BEAM-supervision-vs-liveness-host, mailbox) are carried to engineer decisions, and the implementation scope is handed to build-fe-be-process-split as a signed-off spec input.
- **Acceptance (restart-safe)**: A signed-off FE/BE requirements+decisions record at docs/research/fullscope-gleam/phase2-verify/close-febe-embedded-switch-role-framing.md enumerating each of the eight detail_ids with its disposition (design-confirmed / decision-taken / built-in-wave-4)
- **Depends on**: verify-febe-embedded-switch-role-framing
- **Backing detail_ids**: `embedded-switch-role-framing`, `engine-review-dossier`, `engine-state-snapshot-persistence`, `liveness-crash-restart-host`, `multi-client-control-program`, `premise-reconciliation-compiler-location`, `repl-engine-split-binary-wire-mvp`, `restore-and-resume-link-reestablish`
- **Risk**: Closing at requirements level defers real proof to wave 4, so the record must bind each detail_id to a named build acceptance to avoid a paper close.

### `close-guard-kernel-wait-guards`  (close, M, builder-2)

- **Deliverable**: Implement the timer guards wait/wait_until (currently unimplemented faults at runner.gleam:353-371,2229) with reference three-valued suspend semantics, completing the otherwise-delivered pure guard set.
- **Acceptance (restart-safe)**: `gleam test` in glp_gleam passes with new cases in glp_gleam/test/glp/engine/guards_test.gleam covering wait/wait_until suspend-then-reactivate and failure paths.
- **Depends on**: freeze-engine-execution (bound from freeze-guard-kernel-interface)
- **Backing detail_ids**: `guard-kernel`
- **Risk**: Timer guards need a time source inside the pure engine value, which may force a clock seam through the host API and couple this close to close-embeddability-host-api and the _now kernel.

### `close-guards-guard-defined`  (close, M, builder-3)

- **Deliverable**: Defined-guard support and purity enforcement are confirmed or implemented in the Gleam guard system with a positive and a negative test each.
- **Acceptance (restart-safe)**: programs/tests/test_defined_guards.glp passes on Gleam via the differential harness and a negative purity program is rejected identically to Dart; record in docs/research/fullscope-gleam/phase2-verify/close-guards-guard-defined.md
- **Depends on**: verify-guards-guard-defined
- **Backing detail_ids**: `guard-defined`, `guard-purity`
- **Risk**: Three-valued defined-guard evaluation interacts with suspension and may require runner changes beyond the guard table itself.

### `close-langsurface-channel-convention`  (close, M, builder-3)

- **Deliverable**: The five normative language-surface behaviors are confirmed or implemented so the named test programs and typed corpus produce Dart-identical verdicts on Gleam.
- **Acceptance (restart-safe)**: test_new_channel_guard.glp, test_channel_route.glp, test_passthrough.glp, test_ground.glp, test_channel_guards.glp and the programs/tests/typed/ sample all agree with Dart goldens via the corpus runner; record in docs/research/fullscope-gleam/phase2-verify/close-langsurface-channel-convention.md
- **Depends on**: verify-langsurface-channel-convention
- **Backing detail_ids**: `channel-convention`, `clause-programming-idioms`, `srsw-anonymous-writer`, `type-guard-set`, `type-parameterized`
- **Risk**: Parameterized-type expansion (param_expansion equivalent) is the deepest of the five and could dominate the batch if absent.

### `close-link-inbound-pump`  (close, L, builder-3)

- **Deliverable**: The open US4 remainder (link primitives, reliability sublayer, capability gate, distributed unification path) is implemented on the delivered seam/pump substrate until the link acceptance programs pass end-to-end on Gleam.
- **Acceptance (restart-safe)**: All programs/tests/link/ programs (bidi, pathb, mon, sr, pc, krepro) pass on Gleam over loopback and TCP, with reliability-sublayer tests mirroring SendWindow/Ordering/Fencing/CycleGuard/Reclaim green under wsl gleam test; record in docs/research/fullscope-gleam/phase2-verify/close-link-inbound-pump.md
- **Depends on**: verify-link-inbound-pump
- **Backing detail_ids**: `inbound-pump`, `instance-network-join`, `link-acceptance`, `link-capability-gate`, `link-reliability`, `link-seam`, `link-transport-seam`
- **Risk**: This is the largest single build surface in the plan and the T050-T058 remainder may itself hide sequencing dependencies (primitives before reliability before gate).

### `close-link-layer-fault-decoration`  (close, S, builder-2)

- **Deliverable**: Implement fault-as-data decoration so transport/endpoint faults (the seam's delivered fault types) arrive as data terms on GLP-visible link streams per the 025 contract, closing the second named link-layer missing part.
- **Acceptance (restart-safe)**: `gleam test` in glp_gleam passes a fault-injection case that closes/faults a loopback transport and observes the fault delivered as a data term through the link primitives.
- **Depends on**: close-link-layer-glp-primitives
- **Backing detail_ids**: `link-layer`
- **Risk**: The delivered seam fault taxonomy may not map one-to-one onto the 025 fault-as-data contract, which would trigger the deviations-escalate clause.

### `close-link-layer-glp-primitives`  (close, L, builder-2)

- **Deliverable**: Port the 025 GLP-facing link primitives — link_send/link_recv plus establish/listen/accept/request/setup/close/monitor kernels with registry, pump, and egress — onto the delivered seam (endpoint vtable + two serving transports) per the 025 contracts verbatim.
- **Acceptance (restart-safe)**: `gleam test` in glp_gleam passes T056-style link round-trip tests over loopback and TCP (tasks.md:133 currently unchecked), mirroring the six reference programs at programs/tests/link/.
- **Depends on**: freeze-link-transport-seam (bound from freeze-link-layer-interface)
- **Backing detail_ids**: `link-layer`
- **Risk**: The 025 contracts are verbatim-binding with deviations required to escalate, so any BEAM-process-model mismatch stops this WP rather than being silently adapted.

### `close-parity-differential-harness`  (close, M, builder-3)

- **Deliverable**: The parity-harness gaps close by re-establishing every recorded M1 evidence artifact as a fresh-session-reproducible check (scripts committed, goldens pinned) and extending corpus coverage per the program-corpus scope statement.
- **Acceptance (restart-safe)**: One command sequence documented in docs/research/fullscope-gleam/phase2-verify/close-parity-differential-harness.md reproduces harness diff, 206/206 parity, GAP/FORK verdicts, suite counts and the perf bound from a fresh clone
- **Depends on**: verify-parity-differential-harness
- **Backing detail_ids**: `differential-harness`, `engine-csharp-parity`, `gap-fork-case-corpus`, `instance-load-and-run`, `performance-sanity-bound`, `program-corpus`, `regression-guard`, `test-harness-corpus-parity`
- **Risk**: Pinned goldens can mask real regressions if regenerated during close, so golden updates must be change-controlled.

### `close-platform-atomvm-compatibility-by-construction`  (close, S, builder-3)

- **Deliverable**: The platform gaps close by recording fresh code/toolchain testimony for each (deps-policy green, clean-tree build/test reproduced, scaffold complete, decision records verified) with any drift escalated.
- **Acceptance (restart-safe)**: Fresh-clone wsl gleam build+test transcript including deps_policy_test, plus the six per-detail_id dispositions, committed at docs/research/fullscope-gleam/phase2-verify/close-platform-atomvm-compatibility-by-construction.md
- **Depends on**: verify-platform-atomvm-compatibility-by-construction
- **Backing detail_ids**: `atomvm-compatibility-by-construction`, `build-test-topology-windows`, `langpair-dart-gleam`, `monitor-primitive-verification`, `port-source-basis-dart`, `subtree-scaffold`
- **Risk**: Toolchain drift (OTP/gleam/rebar versions) is the likely failure mode and is environmental, needing a pinned-versions note rather than code fixes.

### `close-process-baseline-program-dossier`  (close, S, builder-3)

- **Deliverable**: The process/decision gaps close by reconciling each record with current state (anchor row re-scoped to this feature, marathon gates re-registered, constituent rows advanced per the reconciliation protocol, runtime-gap rows dispositioned) with scaling-research resolved per its rule-request.
- **Acceptance (restart-safe)**: Updated roadmap/marathon state plus a per-detail_id reconciliation table committed at docs/research/fullscope-gleam/phase2-verify/close-process-baseline-program-dossier.md
- **Depends on**: verify-process-baseline-program-dossier; rule-request-process-engine-instances-scaling-research
- **Backing detail_ids**: `baseline-program-dossier`, `engine-instances-scaling-research`, `full-scope-gleam-anchor`, `marathon-run-position`, `roadmap-constituent-reconciliation`, `runtime-gap-features-reference`
- **Risk**: Roadmap-row mutation is engineer-gated (advisory tools only), so this close records decisions rather than auto-advancing rows.

### `close-proofs-proof-dist-deref-convergence`  (close, L, builder-3)

- **Deliverable**: PI:14 is re-confirmed green and PI:17 is discharged in the owner-directed Lean-not-SPIN form (Lean proof, prose, T057 adversarial dist-deref suite, INDEX entry) unblocking the distributed acceptance chain.
- **Acceptance (restart-safe)**: Sorry-free Lean artifact for PI:17 plus the T057 suite green under wsl gleam test, both indexed per contracts/proof-obligations.md; record in docs/research/fullscope-gleam/phase2-verify/close-proofs-proof-dist-deref-convergence.md
- **Depends on**: verify-proofs-proof-dist-deref-convergence
- **Backing detail_ids**: `proof-dist-deref-convergence`, `proof-writer-mgu-value-copy`
- **Risk**: A Lean convergence proof over deferred-local-assignment is genuinely hard and its failure mode is a spec-level counterexample requiring owner discussion, not a workaround.

### `close-quicws-link-completion-live-repl-bridge`  (close, L, builder-3)

- **Deliverable**: The Gleam QUIC-WS transport is completed on the verified substrate (RFC 6455 framing over QUIC, Profile-C WSL runtime, interop with the reference quic-host), with the live-repl-bridge residual either delivered or ruled forward and the mesh item resolved per its rule-request.
- **Acceptance (restart-safe)**: One Gleam-to-C# link over QUIC-WS exchanges the link smoke suite in WSL against csharp/glp_quick_host, with WebSocketFramingTests-equivalent Gleam tests green; record in docs/research/fullscope-gleam/phase2-verify/close-quicws-link-completion-live-repl-bridge.md
- **Depends on**: verify-quicws-link-completion-live-repl-bridge; rule-request-quicws-mesh-full-mesh-native-quic
- **Backing detail_ids**: `link-completion-live-repl-bridge`, `profile-c-quic-acceptance`, `quic-host`, `websocket-framing`, `mesh-full-mesh-native-quic`
- **Risk**: Profile-C environment fragility (WSL-only quicer NIF) can stall the close on infrastructure rather than code.

### `close-repl-repl-boot-command`  (close, M, builder-3)

- **Deliverable**: The four REPL controls are confirmed, implemented on the Gleam front surface, or (per verify scope finding) recorded as host-level with the equivalent control path documented and tested.
- **Acceptance (restart-safe)**: A scripted session against the Gleam entry point exercises boot/limit/trace/bytecode equivalents with expected outputs; record in docs/research/fullscope-gleam/phase2-verify/close-repl-repl-boot-command.md
- **Depends on**: verify-repl-repl-boot-command
- **Backing detail_ids**: `repl-boot-command`, `repl-bytecode-command`, `repl-limit-command`, `repl-trace-command`
- **Risk**: The :boot command drags in the multiagent layer, so this close may be blocked-by close-multiagent for that one control.

### `close-runtime-arithmetic-expression`  (close, L, builder-3)

- **Deliverable**: Per the verify verdict, each runtime-core detail_id is closed by either recording confirming glp_gleam code testimony or porting the missing capability (kernel, seam, or invariant) with parity tests until all eight have code testimony.
- **Acceptance (restart-safe)**: wsl -e bash -lc 'cd glp_gleam && gleam test' all-green including new/located tests for each batch id; the eight inventory entries re-filed with glp_gleam evidence paths in docs/research/fullscope-gleam/phase2-verify/close-runtime-arithmetic-expression.md
- **Depends on**: verify-runtime-arithmetic-expression
- **Backing detail_ids**: `arithmetic-expression`, `external-io`, `heap-value-copy-semantics`, `scheduler-fairness`, `stream-mutual-reference`, `suspension-abandonment`, `suspension-diagnostics`, `system-predicate-registry`
- **Risk**: Fairness and abandonment semantics are spec-locked with no in-slice tests, so closing them requires authoring parity tests that could surface latent reference divergence.

### `close-transports-multi-accept-transport-extension`  (close, L, builder-3)

- **Deliverable**: Transport hardening closes per verdict and ruling: quiescence oracle and the length-CRC-type fault-as-data boundary with its adversarial suite implemented, the all-gating identical-outcomes matrix run across loopback/TCP/QUIC-WS, multi-accept either delivered or ruled follow-on, and ZMQ resolved per its rule-request.
- **Acceptance (restart-safe)**: quiescence_test.gleam and the T053 adversarial suite green under wsl gleam test; the transport matrix run shows identical outcomes across all three transports; record in docs/research/fullscope-gleam/phase2-verify/close-transports-multi-accept-transport-extension.md
- **Depends on**: verify-transports-multi-accept-transport-extension; rule-request-transports-zmq-comm-base
- **Backing detail_ids**: `multi-accept-transport-extension`, `quiescence-oracle`, `transport-parity-all-gating`, `untrusted-frame-hardening`, `zmq-comm-base`
- **Risk**: The all-gating clarification means any one transport lagging blocks this whole close, concentrating schedule risk.

### `close-wireproto-crdt-convergence`  (close, L, builder-3)

- **Deliverable**: Each application-layer wire item closes per the verify scope table: Gleam-required items implemented with reference-mirroring tests, host-side items recorded as satisfied-by-reference with the interop boundary tested from Gleam.
- **Acceptance (restart-safe)**: For each item either a Gleam test mirroring the named C# suite passes or an interop test proves the Gleam instance correctly produces/consumes the C#-hosted capability (envelope forwarding, registry lookup); record in docs/research/fullscope-gleam/phase2-verify/close-wireproto-crdt-convergence.md
- **Depends on**: verify-wireproto-crdt-convergence
- **Backing detail_ids**: `crdt-convergence`, `durable-mesh-messaging`, `message-envelope`, `schema-language`, `wire-registry`
- **Risk**: The required-vs-host-side scope table is itself a judgment surface, so this close must not start until that table has engineer sign-off.

## Wave 4 — BUILD (FE/BE split + embeddability) (2 accepted WPs)

### `build-fe-be-process-split`  (build, L, builder-3)

- **Deliverable**: The Gleam FE/BE process split is implemented per the closed requirements baseline: a BE engine process behind the frozen engine-facade and result-envelope interfaces (source text on the wire per the ruled MVP decision), an FE client process, BEAM-supervised liveness/crash-restart, an engine-state snapshot seam, and a GLP multi-client control program, with restore-and-resume staged per the dossier chain.
- **Acceptance (restart-safe)**: A committed e2e script under glp_gleam/test/febe/ that a fresh session runs via wsl gleam test: starts BE and FE as separate processes, loads and runs a program over the wire, kills and restarts the FE without engine loss, snapshots and restores engine state, and drives two concurrent clients through the control program
- **Depends on**: verify-febe-embedded-switch-role-framing; close-febe-embedded-switch-role-framing; close-engine-engine-composition-root; close-codec-compiled-il-on-the-wire; freeze-codec-envelope (bound from freeze-result-envelope-interface); freeze-engine-facade (bound from freeze-engine-facade-interface)
- **Backing detail_ids**: `repl-engine-split-binary-wire-mvp`, `engine-review-dossier`, `premise-reconciliation-compiler-location`, `embedded-switch-role-framing`, `engine-state-snapshot-persistence`, `liveness-crash-restart-host`, `multi-client-control-program`, `restore-and-resume-link-reestablish`
- **Risk**: The split rides frozen builder-1 interfaces and the ruled wire decision, so any late interface change forces rework across FE, BE and the embeddability build behind it.

### `build-yngenios-embeddability`  (build, L, builder-3)

- **Deliverable**: Yngenios embeddability is delivered at requirements level per the ratified contract: a durable-listener service-box API surface on the engine facade (store-kernel scope per the escalated Gabi decision), a QHSM packaging design instantiated against the split BE from build-fe-be-process-split, and a compiling Gleam API stub proving the boundary.
- **Acceptance (restart-safe)**: The embeddability contract plus a compiling service-box API stub under glp_gleam/ (wsl gleam build green) with a boundary test embedding the BE engine behind the frozen engine-facade interface, and engineer sign-off recorded in the contract document
- **Depends on**: verify-embed-embeddability-service-box; close-embed-embeddability-service-box; build-fe-be-process-split; freeze-engine-facade (bound from freeze-engine-facade-interface); freeze-codec-envelope (bound from freeze-result-envelope-interface)
- **Backing detail_ids**: `embeddability-service-box`, `qhsm-yngenios-integration-design`, `embedded-switch-role-framing`
- **Risk**: Requirements-level scope is easy to inflate into a full yngenios integration, which does not exist in-repo and must stay out of this feature.

## Wave 5 — ACCEPT (whole-feature integration) (1 accepted WPs)

### `accept-febe-embeddability`  (accept, M, builder-3)

- **Deliverable**: Integration acceptance of the FE/BE and embeddability deliverables: a fresh session executes the FE/BE kill-restart e2e, the snapshot/restore path, and the embeddability boundary stub build, and confirms the recorded engineer sign-offs.
- **Acceptance (restart-safe)**: Fresh-session run of glp_gleam/test/febe/ e2e plus the embeddability boundary test via wsl gleam test, with results and sign-off references appended to specs/050-full-gleam-combined/acceptance.md
- **Depends on**: build-fe-be-process-split; build-yngenios-embeddability
- **Backing detail_ids**: `embeddability-service-box`, `qhsm-yngenios-integration-design`, `repl-engine-split-binary-wire-mvp`, `restore-and-resume-link-reestablish`
- **Risk**: Acceptance of a requirements-level embeddability deliverable rests partly on sign-off records, which must be verified as present rather than assumed.

## ESCALATION REGISTER — engineer rulings required (3)

### `rule-bytecode-runner-unifyconstant-divergence` (builder-2)

- **What is asked**: Surface the recorded-but-unresolved UnifyConstant ground-struct-literal divergence (b1 testimony: 'escalated rather than resolved') for an explicit engineer ruling on which behavior is normative — reference v2.16 ground-struct-literal handling or the current Gleam emission — since E3c forbids silently resolving a recorded escalation inside a close WP.
- **Blocks**: Q6-adjacent: this recorded escalation blocks the golden-parity pin inside close-bytecode-runner-missing-opcodes; the minimal ruling is a one-line declaration of the normative ground-struct-literal behavior.
- **Critic**: Normative ground-struct literal behavior is engineer-only language decision.
- **Ruling evidence**: A recorded engineer ruling artifact (marathon decision row or a note in specs/050-full-gleam-combined/tasks.md against T006/T007/T019/T021) a fresh session can read, naming the normative UnifyConstant ground-struct-literal behavior for the golden pin.

### `rule-mesh-ring-escalation` (builder-2)

- **What is asked**: Carry the open mesh-ring escalation to the engineer with the exact conflict — b3: multi-client mesh messaging over QUIC driven from GLP programs is a delivered, parity-required reference user story (programs/tests/quic/quic_mesh.glp + QuicMeshTests.cs); b2: zero mesh/ring matches anywhere in glp_gleam or gleam_quic — and obtain a scope ruling.
- **Blocks**: Q6: this escalation blocks the multi-peer acceptance criteria of close-quic-transport-leaf, close-distribution-engine-sessions, and the wave-5 accept WPs; the minimal ruling is one line — mesh/ring parity in-scope or follow-on — naming the acceptance target and wave if in-scope.
- **Critic**: Mesh/ring peer semantics affect transport acceptance and require owner ruling.
- **Ruling evidence**: A recorded engineer ruling artifact (marathon decision row or specs/050-full-gleam-combined amendment) a fresh session can read, stating 'mesh/ring user-story parity in-scope with acceptance target = Gleam equivalent of programs/tests/quic/quic_mesh.glp passing, wave N' or 'follow-on feature <name>'.

### `rule-multiagent-runtime-escalation` (builder-2)

- **What is asked**: Carry the open multiagent-runtime escalation to the engineer with the exact cross-corpus conflict — b3: agent_runtime.dart is a delivered parity-required reference capability (engine-per-agent wrapper, messaging, host callbacks); b1: explicitly deferred to link-layer successors (F9+) with no 050 task; b2: only an empty placeholder module exists — and obtain a scope ruling.
- **Blocks**: Q6: this escalation blocks any wave-4 build-multiagent-runtime WP, blocks full-scope parity acceptance (reference multiagent plays at programs/multiagent/ cannot be claimed), and blocks the _send messaging-kernel scope in close-body-kernel-now-send; the minimal ruling is a one-line in-scope-vs-stays-deferred decision, plus the target wave if in-scope.
- **Critic**: Multiagent runtime and _send semantics require engineer ruling.
- **Ruling evidence**: A recorded engineer ruling artifact (marathon decision row or specs/050-full-gleam-combined amendment) that a fresh session can read, stating either 'in-scope: port glp_runtime/lib/multiagent/ in wave N' or 'stays deferred: named follow-on feature <name>'.

## BLOCKED — NOT-ACCEPTED at adjudication, cycle-2 repair required (10)

> Not counted as accepted plan rows (E10). NOTE: several rejections cite 'statement truncated' — an artifact of the adjudication input capping statements at 200 chars, not a builder defect; the persisted claims files carry full text. Cycle-2 (or the resumed session) must re-adjudicate these with full statements and repair genuine dependency defects.

- **`accept-full-scope-regression`** (accept, wave 5, builder-3): Depends on unresolved escalation-gated transport and multiagent scope but treats them as closable acceptance.
  - claim: The feature closes on one fresh-session all-green run of the full cross-runtime and cross-transport matrix.
  - backing: acceptance-sweep-and-polish, cross-runtime-pair-capstone, regression-guard, test-harness-corpus-parity, transport-parity-all-gating
- **`close-body-kernel-now-send`** (close, wave 3, builder-2): Depends on missing body-kernel freeze and unresolved multiagent runtime escalation.
  - claim: Q5: not on the FE/BE-split critical path — runs as parallel wave-3 work closing the only named body-kernel missing part (_now and _send unregistered in the standalone engine).
  - backing: body-kernel
- **`close-distribution-engine-sessions`** (close, wave 3, builder-2): Statement is truncated and dependency on unresolved mesh/ring escalation blocks closure.
  - claim: Q5: off the FE/BE-split critical path (FE-to-engine is the envelope seam, not engine-to-engine sessions) — the latest of the wave-3 closes, sequenced after the link-primitives close and the mesh-ring ruling.
  - backing: distribution-protocol
- **`close-embeddability-host-api`** (close, wave 3, builder-2): Truncated statement, missing dependency binding, and not wired into dependent build chain.
  - claim: Q5: this is THE PARTIAL missing part on the FE/BE-split critical path — the wave-4 engine back-end process wraps exactly this surface, so build-fe-be-process-split depends on this close plus guard-fe-be-envelope-seam, while all other closes run parallel or late.
  - backing: embeddability-api
- **`close-link-layer-sequence-dedup`** (close, wave 3, builder-2): Statement is truncated and dependency substrate is ambiguous between frame and codec contracts.
  - claim: Guard decision for resolved gap link-reliability-sequencing is NO separate guard WP — the other-builder freeze-frame-codec-interface (wave 1) already freezes the substrate it builds on, so the gap is closed by this wave-3 WP; Q5: parallel, not FE/BE-critical.
  - backing: link-reliability-sequencing, link-layer
- **`close-module-system-runtime-rpc`** (close, wave 3, builder-2): Requires missing module-system verify/close work with no emitted actual WP.
  - claim: Q5: not on the FE/BE-split critical path — parallel wave-3 closure of the named module-system missing part (runtime module-RPC execution).
  - backing: module-system
- **`close-multiagent-multiagent-boot-loader`** (close, wave 3, builder-3): Multiagent runtime escalation is carried elsewhere but not a dependency here.
  - claim: Multiagent capability closes when the plays themselves run green on Gleam, not before.
  - backing: multiagent-boot-loader, multiagent-global-send, multiagent-isolate-manager
- **`close-quic-client-inprocess-tests`** (close, wave 3, builder-2): No verify predecessor or rule request despite server-role gap.
  - claim: Q5: not on the FE/BE-split critical path — parallel wave-3 hardening of a b2-only partial whose named missing parts are its tests and its explicit server-role gap.
  - backing: quic-client-inprocess
- **`close-quic-transport-leaf`** (close, wave 3, builder-2): Depends on unverified client close and unresolved mesh/ring escalation affects acceptance breadth.
  - claim: Q5: late/parallel wave-3 — the FE/BE split rides TCP loopback per its roadmap promise, so the QUIC leaf never blocks it; multi-peer acceptance breadth additionally awaits rule-mesh-ring-escalation.
  - backing: quic-transport
- **`guard-fe-be-envelope-seam`** (guard, wave 1, builder-2): Statement is truncated and lacks dependency on freeze-codec-envelope.
  - claim: Guard decision for resolved gap fe-be-process-split is YES: the byte-identity envelope seam is the only delivered substrate of the unstarted two-process split, so it must be drift-locked in wave 1 as the head of the FE/BE critical path feeding the other-builder build-fe-be-process-split in wave 4.
  - backing: fe-be-process-split

## Named planning gaps (dangling deps with no emitted WP — cycle-2 must author these)

- `freeze-body-kernel-interface` — referenced as a dependency but never emitted by any builder.
- `freeze-module-system-interface` — referenced as a dependency but never emitted by any builder.
- `verify-module-system-scope-chain` — referenced as a dependency but never emitted by any builder.

## Dependency-name bindings applied at synthesis (blind naming misses resolved by the Critic)

- `freeze-bytecode-runner-interface` → `freeze-bytecode-isa`
- `freeze-embeddability-api-surface` → `close-embed-embeddability-service-box`
- `freeze-engine-facade-interface` → `freeze-engine-facade`
- `freeze-frame-codec-interface` → `freeze-link-wire`
- `freeze-guard-kernel-interface` → `freeze-engine-execution`
- `freeze-link-layer-interface` → `freeze-link-transport-seam`
- `freeze-result-envelope-interface` → `freeze-codec-envelope`

## Out-of-scope PROPOSALS (pending rulings — nothing leaves scope silently)

- `antlr-shared-grammar-spike` (builder-3, verify: `verify-compiler-antlr-shared-grammar-spike`): superseded — owner-recorded R1 absorption: hand-ported recursive-descent parser on BEAM, no ANTLR target; Glp.g4 remains a dossier-level follow-on; close WP is the fallback if the ruling rejects
- `compiled-il-on-the-wire` (builder-3, verify: `verify-codec-compiled-il-on-the-wire`): post-feature-follow-on — owner-recorded 026 reconciliation keeps source text on the wire for the MVP with compiler relocation a deliberately-deferred follow-up (roadmap [refined], no spec dir); close WP is the fallback if the ruling rejects
- `engine-instances-scaling-research` (builder-3, verify: `verify-process-baseline-program-dossier`): post-feature-follow-on — C++/LLVM/many-instances research rows are roadmap ambitions with no spec dirs, beyond the full-scope Gleam delivery feature; close WP is the fallback if the ruling rejects
- `mesh-full-mesh-native-quic` (builder-3, verify: `verify-quicws-link-completion-live-repl-bridge`): duplicate-of — the separately promoted glp-native-quic-link feature with the C# REPL ruled as host and no recorded Gleam-instance role; close WP is the fallback if the ruling rejects
- `open-items-cycle2-residual` (builder-2): Run-verification hygiene of the 3rtask run (budget_stop at 574k/600k), not feature scope; engineer-rulable by resuming run 20260719T130005Z-782b with a fresh budget (plausible other-builder verify WP territory, not a close/guard/rule in my slice mandate).
- `open-items-merge-candidates` (builder-2): All 40 near-miss key pairs were judged DISTINCT by the Critic and the pair list is preserved in phase1-detail-join.json for cycle-2 re-check; no feature WP is warranted unless cycle 2 overturns a DISTINCT verdict.
- `open-items-unswept-areas` (builder-2): Sweep-completeness residual of builders b1/b3 (non-Gleam spec bodies roadmap-rows-only; programs corpus breadth-only; out/csharp outside slice) — engineer-rulable as part of the cycle-2 sweep, not deliverable feature work.
- `zmq-comm-base` (builder-3, verify: `verify-transports-multi-accept-transport-extension`): external-dependency/post-feature-follow-on — blocked-by the reference-side multi-protocol-link-layer and explicitly absent from the Gleam transport contract (loopback/TCP/QUIC only); close WP is the fallback if the ruling rejects

## Traceability table (detail_id → WPs)

<details><summary>Full 154-row table</summary>

- `acceptance-sweep-and-polish`: covered → verify-acceptance-acceptance-sweep-and-polish, close-acceptance-acceptance-sweep-and-polish, accept-full-scope-regression
- `antlr-shared-grammar-spike`: out-of-scope-proposed → verify-compiler-antlr-shared-grammar-spike, rule-request-compiler-antlr-shared-grammar-spike, close-compiler-antlr-shared-grammar-spike
- `arithmetic-expression`: covered → verify-runtime-arithmetic-expression, close-runtime-arithmetic-expression
- `atomvm-compatibility-by-construction`: covered → verify-platform-atomvm-compatibility-by-construction, close-platform-atomvm-compatibility-by-construction
- `atomvm-conformance-probe`: covered → guard-atomvm-gated-probe
- `atomvm-dependency-policy`: covered → freeze-platform-atomvm-policy
- `baseline-program-dossier`: covered → verify-process-baseline-program-dossier, close-process-baseline-program-dossier
- `body-kernel`: covered → close-body-kernel-now-send
- `build-test-topology-windows`: covered → verify-platform-atomvm-compatibility-by-construction, close-platform-atomvm-compatibility-by-construction
- `bytecode-instruction-set`: covered → verify-bytecode-bytecode-instruction-set, close-bytecode-bytecode-instruction-set
- `bytecode-lint`: covered → verify-bytecode-bytecode-instruction-set, close-bytecode-bytecode-instruction-set
- `bytecode-mode-conversion`: covered → verify-bytecode-bytecode-instruction-set, close-bytecode-bytecode-instruction-set
- `bytecode-opcode-table`: covered → freeze-bytecode-isa
- `bytecode-program-model`: covered → freeze-bytecode-isa
- `bytecode-runner`: covered → close-bytecode-runner-missing-opcodes, rule-bytecode-runner-unifyconstant-divergence
- `channel-convention`: covered → verify-langsurface-channel-convention, close-langsurface-channel-convention
- `clause-programming-idioms`: covered → verify-langsurface-channel-convention, close-langsurface-channel-convention
- `codec-loud-fail`: covered → freeze-codec-envelope
- `compile-mode-directive`: covered → verify-compiler-antlr-shared-grammar-spike, close-compiler-antlr-shared-grammar-spike
- `compiled-il-on-the-wire`: out-of-scope-proposed → verify-codec-compiled-il-on-the-wire, rule-request-codec-compiled-il-on-the-wire, close-codec-compiled-il-on-the-wire
- `compiler-codegen`: covered → freeze-compiler-pipeline
- `compiler-pipeline`: covered → freeze-compiler-pipeline, guard-suite-dart-reference
- `compiler-strict-mode`: covered → verify-compiler-antlr-shared-grammar-spike, close-compiler-antlr-shared-grammar-spike
- `crc32-checksum`: covered → freeze-link-wire, guard-suite-csharp-reference
- `crdt-convergence`: covered → verify-wireproto-crdt-convergence, close-wireproto-crdt-convergence
- `cross-runtime-pair-capstone`: covered → verify-acceptance-acceptance-sweep-and-polish, close-acceptance-acceptance-sweep-and-polish, accept-full-scope-regression
- `deep-resolve-cycle-detection`: covered → freeze-codec-envelope
- `diagnostics-staged-error`: covered → freeze-compiler-pipeline
- `differential-harness`: covered → verify-parity-differential-harness, close-parity-differential-harness
- `distribution-protocol`: covered → close-distribution-engine-sessions
- `durable-mesh-messaging`: covered → verify-wireproto-crdt-convergence, close-wireproto-crdt-convergence
- `embeddability-api`: covered → close-embeddability-host-api
- `embeddability-service-box`: covered → verify-embed-embeddability-service-box, close-embed-embeddability-service-box, build-yngenios-embeddability, accept-febe-embeddability
- `embedded-switch-role-framing`: covered → verify-febe-embedded-switch-role-framing, close-febe-embedded-switch-role-framing, build-fe-be-process-split, build-yngenios-embeddability
- `engine-composition-root`: covered → verify-engine-engine-composition-root, close-engine-engine-composition-root
- `engine-csharp-parity`: covered → verify-parity-differential-harness, close-parity-differential-harness
- `engine-facade`: covered → freeze-engine-facade
- `engine-instances-scaling-research`: out-of-scope-proposed → verify-process-baseline-program-dossier, rule-request-process-engine-instances-scaling-research, close-process-baseline-program-dossier
- `engine-review-dossier`: covered → verify-febe-embedded-switch-role-framing, close-febe-embedded-switch-role-framing, build-fe-be-process-split
- `engine-state-snapshot-persistence`: covered → verify-febe-embedded-switch-role-framing, close-febe-embedded-switch-role-framing, build-fe-be-process-split
- `envelope-builder`: covered → freeze-codec-envelope
- `external-io`: covered → verify-runtime-arithmetic-expression, close-runtime-arithmetic-expression
- `fe-be-process-split`: covered → guard-fe-be-envelope-seam
- `frame-codec`: covered → freeze-link-wire, guard-suite-csharp-reference
- `full-scope-gleam-anchor`: covered → verify-process-baseline-program-dossier, close-process-baseline-program-dossier
- `gap-fork-case-corpus`: covered → verify-parity-differential-harness, close-parity-differential-harness
- `guard-defined`: covered → verify-guards-guard-defined, close-guards-guard-defined
- `guard-kernel`: covered → close-guard-kernel-wait-guards
- `guard-purity`: covered → verify-guards-guard-defined, close-guards-guard-defined
- `heap-value-copy-semantics`: covered → verify-runtime-arithmetic-expression, close-runtime-arithmetic-expression
- `il-codec`: covered → verify-codec-compiled-il-on-the-wire, close-codec-compiled-il-on-the-wire
- `il-codec-round-trip`: covered → verify-codec-compiled-il-on-the-wire, close-codec-compiled-il-on-the-wire
- `inbound-pump`: covered → verify-link-inbound-pump, close-link-inbound-pump
- `instance-load-and-run`: covered → verify-parity-differential-harness, close-parity-differential-harness
- `instance-network-join`: covered → verify-link-inbound-pump, close-link-inbound-pump
- `langpair-dart-gleam`: covered → verify-platform-atomvm-compatibility-by-construction, close-platform-atomvm-compatibility-by-construction
- `link-acceptance`: covered → verify-link-inbound-pump, close-link-inbound-pump
- `link-capability-gate`: covered → verify-link-inbound-pump, close-link-inbound-pump
- `link-completion-live-repl-bridge`: covered → verify-quicws-link-completion-live-repl-bridge, close-quicws-link-completion-live-repl-bridge
- `link-layer`: covered → close-link-layer-glp-primitives, close-link-layer-fault-decoration, close-link-layer-sequence-dedup
- `link-reliability`: covered → verify-link-inbound-pump, close-link-inbound-pump
- `link-reliability-sequencing`: covered → close-link-layer-sequence-dedup
- `link-seam`: covered → verify-link-inbound-pump, close-link-inbound-pump
- `link-transport-seam`: covered → verify-link-inbound-pump, close-link-inbound-pump
- `liveness-crash-restart-host`: covered → verify-febe-embedded-switch-role-framing, close-febe-embedded-switch-role-framing, build-fe-be-process-split
- `loopback-transport`: covered → freeze-link-transport-seam, guard-suite-csharp-reference
- `marathon-run-position`: covered → verify-process-baseline-program-dossier, close-process-baseline-program-dossier
- `mesh-full-mesh-native-quic`: out-of-scope-proposed → verify-quicws-link-completion-live-repl-bridge, rule-request-quicws-mesh-full-mesh-native-quic, close-quicws-link-completion-live-repl-bridge
- `mesh-ring`: covered → rule-mesh-ring-escalation
- `message-envelope`: covered → verify-wireproto-crdt-convergence, close-wireproto-crdt-convergence
- `module-dynamic-dispatch`: covered → verify-compiler-antlr-shared-grammar-spike, close-compiler-antlr-shared-grammar-spike
- `module-static-linking`: covered → verify-compiler-antlr-shared-grammar-spike, close-compiler-antlr-shared-grammar-spike
- `module-system`: covered → close-module-system-runtime-rpc
- `monitor-primitive-verification`: covered → verify-platform-atomvm-compatibility-by-construction, close-platform-atomvm-compatibility-by-construction
- `multi-accept-transport-extension`: covered → verify-transports-multi-accept-transport-extension, close-transports-multi-accept-transport-extension
- `multi-client-control-program`: covered → verify-febe-embedded-switch-role-framing, close-febe-embedded-switch-role-framing, build-fe-be-process-split
- `multiagent-boot-loader`: covered → verify-multiagent-multiagent-boot-loader, close-multiagent-multiagent-boot-loader
- `multiagent-global-send`: covered → verify-multiagent-multiagent-boot-loader, close-multiagent-multiagent-boot-loader
- `multiagent-isolate-manager`: covered → verify-multiagent-multiagent-boot-loader, close-multiagent-multiagent-boot-loader
- `multiagent-runtime`: covered → rule-multiagent-runtime-escalation
- `open-items-cycle2-residual`: out-of-scope-proposed → -
- `open-items-merge-candidates`: out-of-scope-proposed → -
- `open-items-unswept-areas`: out-of-scope-proposed → -
- `output-capture`: covered → freeze-engine-facade
- `output-capture-seam`: covered → verify-engine-engine-composition-root, close-engine-engine-composition-root
- `parity-oracle-testing`: covered → guard-suite-gleam
- `parser-conformance`: covered → freeze-compiler-pipeline
- `parser-recursive-descent`: covered → verify-compiler-antlr-shared-grammar-spike, close-compiler-antlr-shared-grammar-spike
- `partial-evaluator`: covered → freeze-compiler-pipeline
- `performance-sanity-bound`: covered → verify-parity-differential-harness, close-parity-differential-harness
- `placeholder-module-scaffold`: covered → guard-suite-gleam
- `port-source-basis-dart`: covered → verify-platform-atomvm-compatibility-by-construction, close-platform-atomvm-compatibility-by-construction
- `prelude-library`: covered → freeze-engine-facade, guard-suite-dart-reference
- `premise-reconciliation-compiler-location`: covered → verify-febe-embedded-switch-role-framing, close-febe-embedded-switch-role-framing, build-fe-be-process-split
- `profile-c-quic-acceptance`: covered → verify-quicws-link-completion-live-repl-bridge, close-quicws-link-completion-live-repl-bridge
- `program-corpus`: covered → verify-parity-differential-harness, close-parity-differential-harness
- `proof-dist-deref-convergence`: covered → verify-proofs-proof-dist-deref-convergence, close-proofs-proof-dist-deref-convergence
- `proof-writer-mgu-value-copy`: covered → verify-proofs-proof-dist-deref-convergence, close-proofs-proof-dist-deref-convergence
- `qhsm-yngenios-integration-design`: covered → verify-embed-embeddability-service-box, close-embed-embeddability-service-box, build-yngenios-embeddability, accept-febe-embeddability
- `quic-client-inprocess`: covered → close-quic-client-inprocess-tests
- `quic-host`: covered → verify-quicws-link-completion-live-repl-bridge, close-quicws-link-completion-live-repl-bridge
- `quic-sideprocess-relay`: covered → rule-request-link-quic-relay
- `quic-transport`: covered → close-quic-transport-leaf
- `quiescence-oracle`: covered → verify-transports-multi-accept-transport-extension, close-transports-multi-accept-transport-extension
- `reduce-metainterpreter`: covered → verify-compiler-antlr-shared-grammar-spike, close-compiler-antlr-shared-grammar-spike
- `reference-envelope-and-capture-seam`: covered → verify-engine-engine-composition-root, close-engine-engine-composition-root
- `regression-guard`: covered → verify-parity-differential-harness, close-parity-differential-harness, accept-full-scope-regression
- `repl-boot-command`: covered → verify-repl-repl-boot-command, close-repl-repl-boot-command
- `repl-bytecode-command`: covered → verify-repl-repl-boot-command, close-repl-repl-boot-command
- `repl-command-surface`: covered → freeze-repl-surface
- `repl-engine-split-binary-wire-mvp`: covered → verify-febe-embedded-switch-role-framing, close-febe-embedded-switch-role-framing, build-fe-be-process-split, accept-febe-embeddability
- `repl-limit-command`: covered → verify-repl-repl-boot-command, close-repl-repl-boot-command
- `repl-loop`: covered → freeze-repl-surface, guard-suite-dart-reference
- `repl-result-rendering`: covered → freeze-repl-surface
- `repl-trace-command`: covered → verify-repl-repl-boot-command, close-repl-repl-boot-command
- `repl-trace-mode`: covered → freeze-repl-surface
- `restore-and-resume-link-reestablish`: covered → verify-febe-embedded-switch-role-framing, close-febe-embedded-switch-role-framing, build-fe-be-process-split, accept-febe-embeddability
- `result-envelope`: covered → freeze-codec-envelope
- `result-envelope-builder`: covered → verify-codec-compiled-il-on-the-wire, close-codec-compiled-il-on-the-wire
- `result-envelope-codec`: covered → verify-codec-compiled-il-on-the-wire, close-codec-compiled-il-on-the-wire
- `result-seam-identity`: covered → freeze-codec-envelope
- `roadmap-constituent-reconciliation`: covered → verify-process-baseline-program-dossier, close-process-baseline-program-dossier
- `runtime-gap-features-reference`: covered → verify-process-baseline-program-dossier, close-process-baseline-program-dossier
- `scheduler-fairness`: covered → verify-runtime-arithmetic-expression, close-runtime-arithmetic-expression
- `scheduler-single-step`: covered → freeze-engine-execution
- `schema-language`: covered → verify-wireproto-crdt-convergence, close-wireproto-crdt-convergence
- `srsw-anonymous-writer`: covered → verify-langsurface-channel-convention, close-langsurface-channel-convention
- `srsw-check`: covered → freeze-compiler-pipeline
- `stream-mutual-reference`: covered → verify-runtime-arithmetic-expression, close-runtime-arithmetic-expression
- `subtree-scaffold`: covered → verify-platform-atomvm-compatibility-by-construction, close-platform-atomvm-compatibility-by-construction
- `suspension-abandonment`: covered → verify-runtime-arithmetic-expression, close-runtime-arithmetic-expression
- `suspension-diagnostics`: covered → verify-runtime-arithmetic-expression, close-runtime-arithmetic-expression
- `suspension-result-reporting`: covered → freeze-codec-envelope
- `suspension-scheduler`: covered → freeze-engine-execution
- `suspension-table`: covered → freeze-runtime-term-heap
- `suspension-wake-dedup`: covered → freeze-engine-execution
- `suspension-writer-address-model`: covered → freeze-engine-execution
- `system-predicate-registry`: covered → verify-runtime-arithmetic-expression, close-runtime-arithmetic-expression
- `tcp-length-prefix-framing`: covered → freeze-link-wire
- `tcp-transport`: covered → freeze-link-transport-seam, guard-suite-csharp-reference
- `term-codec`: covered → freeze-codec-envelope, guard-suite-csharp-reference
- `term-codec-tlv`: covered → verify-codec-compiled-il-on-the-wire, close-codec-compiled-il-on-the-wire
- `term-heap-unification`: covered → freeze-runtime-term-heap
- `term-representation`: covered → freeze-runtime-term-heap
- `test-harness`: covered → guard-suite-gleam
- `test-harness-corpus-parity`: covered → verify-parity-differential-harness, close-parity-differential-harness, accept-full-scope-regression
- `three-phase-execution`: covered → freeze-engine-execution, guard-suite-dart-reference
- `transport-parity-all-gating`: covered → verify-transports-multi-accept-transport-extension, close-transports-multi-accept-transport-extension, accept-full-scope-regression
- `type-checker`: covered → freeze-compiler-pipeline, guard-suite-dart-reference
- `type-guard-set`: covered → verify-langsurface-channel-convention, close-langsurface-channel-convention
- `type-parameterized`: covered → verify-langsurface-channel-convention, close-langsurface-channel-convention
- `unification-three-valued-verdict`: covered → freeze-runtime-term-heap
- `unification-writer-mgu`: covered → freeze-runtime-term-heap
- `untrusted-frame-hardening`: covered → verify-transports-multi-accept-transport-extension, close-transports-multi-accept-transport-extension
- `websocket-framing`: covered → verify-quicws-link-completion-live-repl-bridge, close-quicws-link-completion-live-repl-bridge
- `wire-registry`: covered → verify-wireproto-crdt-convergence, close-wireproto-crdt-convergence
- `zmq-comm-base`: out-of-scope-proposed → verify-transports-multi-accept-transport-extension, rule-request-transports-zmq-comm-base, close-transports-multi-accept-transport-extension

</details>
