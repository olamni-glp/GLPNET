# Full-scope Gleam GLP — Deduplicated Delivered-vs-Gaps Inventory

**Date**: 2026-07-19 · **Method**: 3-role task team (spec-051), run `20260719T130005Z-782b`, frozen method `method-20260719T130005Z-782b` (3 codex red-team passes) · **Marathon**: `mrun-8bda036d9e9b` step `phase1-3rtask-gap-analysis` · **Anchor feature**: `full-scope-gleam-glp-implementation`

Three blind builders on pairwise-disjoint corpora — b1 roadmap+specs+handovers (design promises), b2 glp_gleam+gleam_quic code (implementation truth), b3 Dart/C# runtimes + normative docs + programs corpus (full-scope reference) — 206 attributed claims, joined mechanically by detail_id (154 capabilities), adjudicated by the cross-provider codex Critic (201 CONFIRM / 5 ESCALATE / 0 REFUTE; all 40 near-miss key pairs judged distinct). Verdict: budget_stop after cycle 1 (600k budget; cycle-2 self-verification pass is the named residual). Every entry cites its source claim_ids; full claims live in `.specify/3rtask/runs/20260719T130005Z-782b/cycle01/claims-builder-*.json`.

**Reading rule**: `delivery` is b2 code testimony ONLY; `promise` is b1 specs testimony; `parity-required` means b3 found it in the reference. UNCONFIRMED-GAP = promised/required with no b2 testimony either way (candidate gap pending code-side verification — NOT confirmed delivered).

**yngenios embeddability** is REQUIREMENTS-LEVEL: no yngenios sources exist in-repo; `embeddability-api` appears below at its evidence level (in-process engine-value API delivered; host-embedding/yngenios surface absent) and the yngenios integration itself is gap-by-definition.

---

## 1. DELIVERED in Gleam — corroborated across corpora (17)

### `compiler-pipeline`  (interface)

- **Axes**: delivery=`delivered` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-001**: The single-entry Gleam load pipeline (parse→SRSW→partial-eval→type-check→compile-v2.16→load, staged diagnostics, no skippable stage) is recorded complete per FR-001 and the instance-surface contract.
  - evidence: `specs/050-full-gleam-combined/tasks.md:60 (T020) + contracts/gleam-instance-surface.md:5-11` · test: `specs/050-full-gleam-combined/tasks.md:60 (4 gleeunit staged-error tests; native gleam 336/336)`
- **b2-c1-022**: The full compiler pipeline is delivered as a single unskippable load entry (parse, SRSW, partial evaluation, type check, v2.16 codegen) with stage-attributed diagnostics and later-stages-do-not-run semantics.
  - evidence: `glp_gleam/src/glp/compiler/loader.gleam:60-87` · test: `glp_gleam/test/glp/compiler/loader_test.gleam:1-9`
- **b3-c1-019**: The full compiler pipeline — lexer, parser, defined-guard partial evaluation, type check, SRSW analysis with reduce/2 generation, and codegen to bytecode — is the delivered compilation surface a Gleam implementation must match.
  - evidence: `glp_runtime/lib/compiler/compiler.dart:56-133` · test: `no-test: unified REPL suite (test/run_all_tests.sh) outside slice; every load of programs/tests/ runs the full pipeline`

### `crc32-checksum`  (protocol)

- **Axes**: delivery=`delivered` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-029**: A pure-Gleam (FFI-free, AtomVM-portable) IEEE CRC-32 with the canonical cross-runtime check vector is recorded delivered for frame integrity.
  - evidence: `specs/050-full-gleam-combined/tasks.md:123 (T046 crc32.gleam)` · test: `specs/050-full-gleam-combined/tasks.md:124 (canonical vector compute("123456789")==0xCBF43926)`
- **b2-c1-037**: CRC-32 (reflected 0xEDB88320, byte-identical to the Dart/C# implementations) is delivered in pure AtomVM-portable Gleam with the canonical cross-runtime check vector pinned.
  - evidence: `glp_gleam/src/glp/link/reliability/crc32.gleam:17-24` · test: `glp_gleam/test/glp/link/frame_codec_test.gleam:9-11`
- **b3-c1-050**: CRC32 checksum validation of link frames is a delivered protocol requirement in both reference runtimes.
  - evidence: `glp_runtime/lib/link/reliability/crc32.dart (C# at csharp/glp_link/reliability/Crc32.cs)` · test: `csharp/glp_link.tests/FrameCodecTests.cs`

### `frame-codec`  (protocol)

- **Axes**: delivery=`delivered` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-028**: The FrameCodec port (fixed 22-byte big-endian header, 1:1 fragmentation math, loud-fail-as-data error variants checked in reference order) is recorded delivered with byte-parity evidence, while the deep adversarial frame matrix remains deferred to T053.
  - evidence: `specs/050-full-gleam-combined/tasks.md:123 (T046 [X]) + contracts/link-parity.md:9-13` · test: `specs/050-full-gleam-combined/tasks.md:124 (T047 13 parity tests incl. exact wire-layout byte pin)`
- **b2-c1-038**: The link frame codec — fixed 22-byte big-endian header, Whole/Fragment kinds, 64 MiB cap, per-chunk CRC-32, MTU fragmentation, errors as data — is delivered with byte-parity and rejection tests against the Dart/C# reference.
  - evidence: `glp_gleam/src/glp/link/reliability/frame_codec.gleam:29-92` · test: `glp_gleam/test/glp/link/frame_codec_test.gleam:1-21`
- **b3-c1-049**: The link frame wire format with fragmentation and reassembly is a delivered cross-runtime protocol artifact.
  - evidence: `glp_runtime/lib/link/reliability/frame_codec.dart:1-30 (C# at csharp/glp_link/reliability/FrameCodec.cs)` · test: `csharp/glp_link.tests/FrameCodecTests.cs`

### `loopback-transport`  (interface)

- **Axes**: delivery=`delivered` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-030**: The in-BEAM loopback transport (hub+channel process mapping of the Dart/C# design, FIFO/exactly-once, close-drain and fault-on-send-after-close semantics, no gleam_otp) is recorded delivered behind the seam.
  - evidence: `specs/050-full-gleam-combined/tasks.md:125 (T048 [X])` · test: `specs/050-full-gleam-combined/tasks.md:125 (4 smoke tests; full matrix deferred to T056)`
- **b2-c1-040**: The deterministic in-process loopback transport (hub+channel process rendezvous, FIFO exactly-once delivery, synchronous seam) is delivered and test-locked as the hermetic round-trip substrate.
  - evidence: `glp_gleam/src/glp/link/transports/loopback.gleam:1-28` · test: `glp_gleam/test/glp/link/loopback_test.gleam:1-8`
- **b3-c1-052**: A loopback transport for in-process link testing is delivered in both reference runtimes.
  - evidence: `glp_runtime/lib/link/transports/loopback_transport.dart (registered at glp_repl.dart:93-98; C# at csharp/glp_link/transports/LoopbackTransport.cs)` · test: `csharp/glp_link.tests/LoopbackTests.cs`

### `partial-evaluator`  (interface/pattern)

- **Axes**: delivery=`delivered` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-003**: The partial evaluator is recorded complete as a port of both live Dart copies (SRSW-preserving per PI:13), with the dead unfoldReduceCalls path deliberately omitted.
  - evidence: `specs/050-full-gleam-combined/tasks.md:57 (T017)` · test: `specs/050-full-gleam-combined/tasks.md:57 (28 gleeunit tests; 5 error channels REPL-verified byte-identical)`
- **b2-c1-019**: The partial evaluator (defined-guard unfolding in both its engine and analyzer variants, unit-clause collection) is delivered with byte-identical error-message conformance tests.
  - evidence: `glp_gleam/src/glp/compiler/partial_eval.gleam:64-254` · test: `glp_gleam/test/glp/compiler/partial_eval_test.gleam:1-6`
- **b3-c1-021**: A partial evaluator that unfolds defined guards and reduce/2 calls as a pre-typecheck source transform is a delivered compiler stage.
  - evidence: `glp_runtime/lib/compiler/partial_evaluator.dart:1-20` · test: `programs/tests/test_defined_guards_correct.glp`

### `prelude-library`  (interface/user-story)

- **Axes**: delivery=`delivered` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-014**: Prelude loading (on-disk self.glp compiled and merged with user code, making := and = work as prelude clauses) is recorded complete in the engine facade.
  - evidence: `specs/050-full-gleam-combined/tasks.md:69 (T029 Slice 1) + tasks.md:64 (T024 finding := and = are self.glp clauses)` · test: `specs/050-full-gleam-combined/baseline.md:49 (X := 2+3 via real on-disk self.glp)`
- **b2-c1-024**: Prelude-library support is delivered — the engine boots the root self.glp untype-checked and merges it into every runnable program so prelude arithmetic works with no user load — though the prelude source file itself is outside the Gleam subtree.
  - evidence: `glp_gleam/src/glp/engine.gleam:42-45,103-115 + glp_gleam/src/glp/compiler/loader.gleam:106-110` · test: `glp_gleam/test/glp/engine_test.gleam:1-7`
- **b3-c1-026**: A root prelude (programs/self.glp) supplying core types and builtin declarations via the scope chain is a delivered, load-bearing library every conforming runtime must serve.
  - evidence: `programs/self.glp:1-40 (loading noted at glp_runtime/lib/analysis/type_checker/prelude.dart:10-12)` · test: `no-test: every REPL/engine boot loads it (glp_repl.dart:80-102)`

### `repl-loop`  (interface/user-story)

- **Axes**: delivery=`delivered` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-015**: The standalone Gleam REPL user story (load, goals, :trace with reference-shape trace lines, :limit, :quit, scripted stdin) is recorded complete as the M1 user surface.
  - evidence: `specs/050-full-gleam-combined/tasks.md:82-91 (T031-T036 + US2 polish) + spec.md:42-55 (US2) + contracts/gleam-instance-surface.md:13-23` · test: `specs/050-full-gleam-combined/tasks.md:90 (T035 16 scripted-mode tests)`
- **b2-c1-027**: The REPL loop is delivered — a scripted, EOF-terminating stdin loop over a threaded Session, entered via gleam run through glp_gleam.main.
  - evidence: `glp_gleam/src/glp/repl/repl.gleam:22-47` · test: `glp_gleam/test/glp/repl/repl_test.gleam:1-7`
- **b3-c1-032**: The interactive REPL — the single unified compile/typecheck/run tool with its full command set and binding/status display — is the primary delivered user-facing surface.
  - evidence: `glp_runtime/bin/glp_repl.dart:62-318` · test: `no-test: unified REPL suite harness (test/run_all_tests.sh) outside slice; driven by programs/tests/ corpus`

### `result-envelope`  (interface/protocol)

- **Axes**: delivery=`delivered` · promise=`partial` · parity-required=no · builders=b1,b2
- **b1-c1-016**: The ED-1 result-envelope seam (deep-resolve, canonical ordering, in-process/wire byte-identity) is recorded complete, while the envelope captured-output field remains deferred-by-owner-approval (excluded from parity, always empty).
  - evidence: `specs/050-full-gleam-combined/tasks.md:84,91 (T033/T036) + spec.md:135 (FR-009) + specs/038-result-codec-and-framecodec-ride/spec.md:10` · test: `specs/050-full-gleam-combined/tasks.md:91 (T036 envelope-identity tests)`
- **b2-c1-032**: The result-envelope wire protocol (status, bindings, var-to-writer, suspended, captured, error under a 0x01/0x11 header, order-preserving) is delivered with round-trip, loud-fail, and canonical-order tests.
  - evidence: `glp_gleam/src/glp/codec/result_envelope.gleam:31-77` · test: `glp_gleam/test/glp/codec/result_envelope_codec_test.gleam:1-12`

### `srsw-check`  (interface/pattern)

- **Axes**: delivery=`delivered` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-002**: The SRSW checker port with unchanged semantics (incl. the approved ground/1 relaxation D6, no escape mechanism) is recorded complete.
  - evidence: `specs/050-full-gleam-combined/tasks.md:56 (T016) + spec.md:123 (FR-003)` · test: `specs/050-full-gleam-combined/baseline.md:51 (SRSW negative rejects at SrswStage)`
- **b2-c1-018**: The SRSW check is delivered as a mandatory, unskippable pipeline stage with Dart-conformant violation messages and both sanctioned relaxations.
  - evidence: `glp_gleam/src/glp/analysis/srsw.gleam:1-73` · test: `glp_gleam/test/glp/analysis/srsw_test.gleam:1-4`
- **b3-c1-022**: Compile-time SRSW checking with guard-groundedness and type-declaration relaxations is a mandatory delivered analysis.
  - evidence: `glp_runtime/lib/compiler/pmt/checker.dart:1-14 (relaxation via compiler.dart:114-122)` · test: `programs/tests/srsw_multi_error.glp`

### `suspension-scheduler`  (interface/pattern)

- **Axes**: delivery=`delivered` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-008**: The scheduler-actor design (pure stepping, run queue, reduction budget, generation-scoped reactivation dedup keyed on (goal_id, suspension_generation)) is recorded complete including the drainWithStatus-parity refinement.
  - evidence: `specs/050-full-gleam-combined/tasks.md:39,62,69 (T010/T022/T029-slice-0) + research.md:14-19 (R2) + spec.md:128 (FR-005)` · test: `specs/050-full-gleam-combined/tasks.md:65 (T025 exactly-once rendezvous test)`
- **b2-c1-006**: The suspension-aware scheduler run loop (run queue, goal store, blocking-reader table mirroring Dart rt.suspended, faithful terminal statuses, heap-driven reactivation) is delivered and locked by multi-goal end-to-end tests.
  - evidence: `glp_gleam/src/glp/engine/scheduler.gleam:61-136` · test: `glp_gleam/test/glp/engine/scheduler_test.gleam:1-5`
- **b3-c1-003**: Goal suspension/reactivation semantics — one suspension record per goal on writer cells, wake-and-retry from the procedure entry point kappa, and suspension forwarding when a writer binds to another variable — are a locked conformance target.
  - evidence: `docs/glp-runtime-spec.txt:227-249 (with lib/runtime/suspend.dart, suspension.dart)` · test: `programs/tests/test_nested_suspend.glp`

### `tcp-transport`  (interface/protocol)

- **Axes**: delivery=`delivered` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-031**: The TCP transport (gen_tcp passive-mode FFI, byte-identical 4-byte big-endian length-prefix framing with the C#/Dart interop peers, half-close and retry-based role independence) is recorded delivered.
  - evidence: `specs/050-full-gleam-combined/tasks.md:126 (T049 [X]) + contracts/link-parity.md:26` · test: `specs/050-full-gleam-combined/tasks.md:126 (3 real-socket smoke tests)`
- **b2-c1-041**: The raw-TCP transport (passive-mode gen_tcp FFI, one persistent duplex socket per bilateral link, role-order-independent connect retry) is delivered and locked by real-socket smoke tests.
  - evidence: `glp_gleam/src/glp/link/transports/tcp.gleam:1-19,71-77` · test: `glp_gleam/test/glp/link/tcp_test.gleam:1-7`
- **b3-c1-053**: A TCP transport is delivered in both reference runtimes as a required real-network leaf.
  - evidence: `glp_runtime/lib/link/transports/tcp_transport.dart (C# at csharp/glp_link/transports/TcpTransport.cs)` · test: `csharp/glp_link.tests/TcpTransportTests.cs`

### `term-codec`  (protocol)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=yes · builders=b2,b3
- **b2-c1-031**: The cross-runtime term wire codec — byte-identical to the Dart reference with global variable identity replacing heap addresses — is delivered with golden-corpus byte-identity and cross-decode tests.
  - evidence: `glp_gleam/src/glp/codec/term_codec.gleam:36-98` · test: `glp_gleam/test/glp/codec/golden_corpus_test.gleam:1-9`
- **b3-c1-062**: A byte-parity term sub-codec for wire terms is a delivered cross-runtime protocol component.
  - evidence: `glp_runtime/lib/codec/term_codec.dart (C# at csharp/glp_result_codec/TermCodec.cs)` · test: `csharp/glp_result_codec/tests/GoldenVectorTests.cs; OracleConsistencyTests.cs`

### `term-heap-unification`  (interface/pattern)

- **Axes**: delivery=`delivered` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-012**: Writer-MGU three-valued unification over the immutable heap, with parity measured on observable outcomes only (not heap layout), is recorded delivered by feature 034.
  - evidence: `specs/034-glp-gleam-core-terms-and-heap/spec.md:50-64 (US2) + roadmap-snapshot-2026-07-19.md:52` · test: `specs/034-glp-gleam-core-terms-and-heap/parity-evidence.md (parity evidence file present in spec dir)`
- **b2-c1-002**: The immutable-threaded GLP heap (paired writer/reader allocation, deref with path compression, bind_writer, writer-writer violation as error, suspend_on_writer) is delivered and test-locked against the Dart heap_fcp source of truth.
  - evidence: `glp_gleam/src/glp/runtime/heap.gleam:69-313` · test: `glp_gleam/test/glp/runtime/heap_test.gleam:1-3`
- **b3-c1-002**: Unification runs over an FCP two-cell heap where each variable is a paired writer+reader cell, suspensions live on writer cells, and binding fires observation callbacks.
  - evidence: `glp_runtime/lib/runtime/heap_fcp.dart:1-80` · test: `programs/tests/test_reader_binding.glp`

### `term-representation`  (interface/pattern)

- **Axes**: delivery=`delivered` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-011**: The immutable Gleam term ADT (constants, structures, lists, writer/reader refs) promised by feature 034 is recorded delivered and is the reused foundation of the 050 instance.
  - evidence: `specs/034-glp-gleam-core-terms-and-heap/spec.md:33-47 (US1) + docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:52` · test: `specs/050-full-gleam-combined/data-model.md:7-8 (marked EXISTS, 034)`
- **b2-c1-001**: The full GLP term representation (atom, int, real, string, compound, nil, cons list, nested struct, var ref) is delivered as glp/runtime/terms with a 9-kind construction/inspection/equality test.
  - evidence: `glp_gleam/src/glp/runtime/terms.gleam:13-38` · test: `glp_gleam/test/glp/runtime/terms_test.gleam:1-3`
- **b3-c1-001**: The reference runtime represents all terms heap-only via tagged cells (writer/reader/value) per the normative ISA convention, which any full-scope Gleam runtime must reproduce.
  - evidence: `glp_runtime/lib/runtime/heap_fcp.dart:15-65` · test: `programs/tests/test_struct.glp`

### `test-harness`  (interface/pattern)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=yes · builders=b2,b3
- **b2-c1-054**: The test harness is delivered — a 49-module gleeunit suite spanning all delivered subsystems, anchored by a build-and-run smoke test.
  - evidence: `glp_gleam/test/glp_gleam_test.gleam:5-17` · test: `glp_gleam/test/ (49 gleeunit test modules spanning runtime, engine, analysis, parser, compiler, bytecode, codec, repl, link)`
- **b3-c1-068**: Reference behavior is locked by a large REPL program corpus plus per-package C# test suites with coverage gates, defining the acceptance bar for any Gleam parity claim.
  - evidence: `programs/tests/ (228 .glp: typed/ 65, type_errors/ 3, modules, link, quic, dynamic_dispatch) and csharp/*.tests/ (7 test projects)` · test: `csharp/glp_il_codec.tests/CoverageGateTests.cs`

### `three-phase-execution`  (pattern)

- **Axes**: delivery=`delivered` · promise=`partial` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-007**: Three-phase HEAD/GUARD/BODY execution with writer-MGU discipline is recorded complete and test-pinned, except one surfaced-unimplemented frozen-semantics gap (WRITE-mode void slot → ConstTerm(null)) left as escalate-if-hit.
  - evidence: `specs/050-full-gleam-combined/tasks.md:61 (T021 slices 21a-21d) + spec.md:127 (FR-004)` · test: `specs/050-full-gleam-combined/tasks.md:65 (T025 phase-ordering tests [X])`
- **b2-c1-011**: Three-phase HEAD/GUARD/BODY clause execution with tentative-structure and clause-variable state (the Dart BytecodeRunner semantics re-expressed immutably) is delivered and test-locked.
  - evidence: `glp_gleam/src/glp/engine/runner.gleam:1-37` · test: `glp_gleam/test/glp/engine/runner_test.gleam:1-6`
- **b3-c1-004**: Three-phase clause execution (HEAD tentative bindings, pure GUARDs, BODY mutations after commit, with Si unioned into U on clause failure) is the core execution model any Gleam runtime must match.
  - evidence: `docs/glp-bytecode-v216-complete.md sections 3-4 (with glp_runtime/lib/bytecode/opcodes.dart:11-43)` · test: `programs/tests/test_toplevel.glp`

### `type-checker`  (interface/pattern)

- **Axes**: delivery=`delivered` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-004**: The Gleam type-checker port (modes with ?-flip, parameterized types, unions per manual §2A/§17/§20) is recorded complete.
  - evidence: `specs/050-full-gleam-combined/tasks.md:58 (T018) + docs/handover/050-full-gleam-T018-handover-2026-07-12.md` · test: `specs/050-full-gleam-combined/baseline.md:52 (type negative rejects at TypeCheckStage)`
- **b2-c1-020**: The complete GLP moded type checker — mode algebra, subtyping, complement-pair program DFA, well-typed term/clause checking, parameterized-type expansion, environment building, covariance and input-coverage contravariance — is delivered with byte-identical-message oracle tests.
  - evidence: `glp_gleam/src/glp/analysis/type_checker/type_checker.gleam:146-190` · test: `glp_gleam/test/glp/analysis/type_checker_test.gleam:1-5`
- **b3-c1-023**: A full well-typed-program checker (covariance plus input-path coverage contravariance, with modes, subtyping, and parameterized-type expansion) is delivered and locked by the typed corpus.
  - evidence: `glp_runtime/lib/analysis/type_checker/type_checker.dart:1-30 (14-file package at glp_runtime/lib/analysis/type_checker/)` · test: `programs/tests/typed/ (65 positive programs) and programs/tests/type_errors/`

## 2. DELIVERED in Gleam — implementation-corpus testimony only (singletons, kept visible) (27)

### `atomvm-conformance-probe`  (pattern)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-053**: An AtomVM conformance probe for the gated codec entries (float and 64-bit-int edges) is delivered as a manually-run artifact, with those entries deliberately excluded from byte-final goldens.
  - evidence: `glp_gleam/src/atomvm_gated_probe.gleam:1-38` · test: `no-test: the probe is run manually via the Node AtomVM wrapper, not by gleam test (which runs on full OTP and is explicitly not an AtomVM-faithfulness signal)`

### `atomvm-dependency-policy`  (pattern)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-052**: The AtomVM-subset dependency policy — no OTP-abstraction package anywhere in the tree, plain spawn and Subjects only — is delivered as an enforced test tripwire.
  - evidence: `glp_gleam/src/glp/link/transports/loopback.gleam:22-24` · test: `glp_gleam/test/glp/deps_policy_test.gleam:1-9`

### `bytecode-opcode-table`  (protocol)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-013**: The complete v2.16 bytecode instruction inventory — including reference-live spec-gap opcodes — is delivered as one Gleam union with a table-integrity test over mnemonics and reader/writer flips.
  - evidence: `glp_gleam/src/glp/bytecode/opcodes.gleam:38-164` · test: `glp_gleam/test/glp/bytecode/opcodes_test.gleam:1-5`

### `bytecode-program-model`  (interface)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-014**: The bytecode program model (label indexing, prelude-in-front merge, guard-spec table, disassembly, X registers) is delivered and test-locked against Dart BytecodeProgram semantics.
  - evidence: `glp_gleam/src/glp/bytecode/program.gleam:24-166` · test: `glp_gleam/test/glp/bytecode/opcodes_test.gleam:1-5`

### `codec-loud-fail`  (pattern)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-033**: The loud-fail codec discipline — every malformed byte sequence rejected as data, never silently accepted — is delivered and fuzz-locked (SC-004).
  - evidence: `glp_gleam/src/glp/codec/term_codec.gleam:58-80` · test: `glp_gleam/test/glp/codec/loud_fail_fuzz_test.gleam:1-4`

### `compiler-codegen`  (interface)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-023**: v2.16 bytecode code generation is delivered as a faithful Dart-parity port with the exact merge/3 instruction stream pinned, carrying one documented semantically-neutral divergence on ground list literals.
  - evidence: `glp_gleam/src/glp/compiler/codegen.gleam:114` · test: `glp_gleam/test/glp/compiler/codegen_test.gleam:1-11`

### `deep-resolve-cycle-detection`  (pattern)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-035**: Cyclic-term deep resolve is delivered — a self-referential term yields the circular marker at the revisit point (never loops) matching the Dart/C# REPL behavior.
  - evidence: `glp_gleam/src/glp/codec/result_envelope_builder.gleam:54` · test: `glp_gleam/test/glp/codec/cyclic_term_test.gleam:1-7`

### `diagnostics-staged-error`  (interface)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-025**: Stage-attributed load diagnostics (which pipeline stage rejected, with what class, where) are delivered and locked by the loader negatives.
  - evidence: `glp_gleam/src/glp/diagnostics.gleam:17-66` · test: `glp_gleam/test/glp/compiler/loader_test.gleam:1-9`

### `engine-facade`  (interface)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-026**: The engine-as-typed-value facade (construct, load with prelude merge, one-shot run to a ResultEnvelope, interactive start/step, zero global state) is delivered and test-locked.
  - evidence: `glp_gleam/src/glp/engine.gleam:65-115,142-310` · test: `glp_gleam/test/glp/engine_test.gleam:1-7`

### `envelope-builder`  (interface)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-034**: The heap-to-envelope builder (depth-bounded deep resolve with explicit truncation and circular markers, global var identity) is delivered and locked by deref-fidelity and builder tests.
  - evidence: `glp_gleam/src/glp/codec/result_envelope_builder.gleam:36-147` · test: `glp_gleam/test/glp/codec/deref_fidelity_test.gleam:1-5`

### `output-capture`  (user-story)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-017**: Program output via _output/1 is delivered end-to-end: the ground argument renders like the Dart formatGroundTerm and flows out as captured data rather than a side effect.
  - evidence: `glp_gleam/src/glp/engine/output_capture.gleam:32` · test: `glp_gleam/test/glp/engine/output_capture_test.gleam:1-7`

### `parity-oracle-testing`  (pattern)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-055**: Dart-oracle parity testing — hermetic observable-outcome corpora, golden byte vectors, and byte-identical error-string assertions — is the delivered verification methodology across the Gleam port.
  - evidence: `glp_gleam/test/glp/runtime/parity_test.gleam:1-8` · test: `glp_gleam/test/glp/runtime/parity_test.gleam:1-8`

### `parser-conformance`  (interface)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-021**: The GLP source lexer and parser (clauses, guards, reader marks, module and procedure declarations) are delivered with Dart-identical error text and position conformance tests.
  - evidence: `glp_gleam/src/glp/parser/parser.gleam:251` · test: `glp_gleam/test/glp/parser/parser_test.gleam:1-6`

### `placeholder-module-scaffold`  (pattern)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-056**: An empty-but-building placeholder scaffold is delivered for the unported subsystems (multiagent, link, lint, plus the analysis/compiler/bytecode facades), keeping the gap inventory visible and the build green.
  - evidence: `glp_gleam/src/glp/link.gleam:1-9 (same shape as multiagent/lint/analysis/compiler/bytecode top-level modules)` · test: `glp_gleam/test/glp_gleam_test.gleam:9-17`

### `quic-sideprocess-relay`  (pattern)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-044**: The Profile A QUIC delegation pattern — a BEAM OS-port line relay keeping observable stdio byte-identical to the C# stack, with long-line reassembly guarding against silent data loss — is delivered in code with no in-corpus test.
  - evidence: `gleam_quic/src/glpq_ffi.erl:1-40` · test: `no-test: gleam_quic/test is empty; behavior verified only by out-of-repo stack adapters`

### `repl-command-surface`  (user-story)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-028**: A user can drive scripted REPL sessions with the reference command set — load, bare .glp paths, dotted goals, :trace, :limit (including exhaustion behavior), :quit — with Dart-parity parse semantics.
  - evidence: `glp_gleam/src/glp/repl/commands.gleam:21-119` · test: `glp_gleam/test/glp/repl/repl_test.gleam:1-7`

### `repl-result-rendering`  (interface)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-030**: Reference-shape REPL result rendering (bindings and goal status lines from the ResultEnvelope) is delivered and test-pinned.
  - evidence: `glp_gleam/src/glp/repl/results.gleam:32-69` · test: `glp_gleam/test/glp/repl/results_test.gleam:1-3`

### `repl-trace-mode`  (user-story)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-029**: REPL :trace mode is delivered — reduction traces render in the Dart reference shape (arity-stripped, reader-marked) and are emitted only when tracing is enabled.
  - evidence: `glp_gleam/src/glp/engine/goal_format.gleam:28-69` · test: `glp_gleam/test/glp/engine/goal_format_test.gleam:1-6`

### `result-seam-identity`  (pattern)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-036**: The ED-1 seam guarantee — a goal's result is byte-identical whether consumed in-process by the REPL or over the wire by a link peer — is delivered and test-locked.
  - evidence: `glp_gleam/src/glp/engine.gleam:183-218` · test: `glp_gleam/test/glp/repl/envelope_identity_test.gleam:1-8`

### `scheduler-single-step`  (interface)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-007**: A real single-reduction step seam (StepOutcome: idle/reduced/suspended/failed/errored) is delivered as a capability beyond the Dart reference, which only exposes the bounded drain.
  - evidence: `glp_gleam/src/glp/engine/scheduler.gleam:99-121,260` · test: `glp_gleam/test/glp/engine/step_test.gleam:1-6`

### `suspension-result-reporting`  (user-story)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-010**: A user running a goal that suspends receives a suspended-status envelope naming the blocking readers as global variable ids (never heap addresses) — delivered and acceptance-tested.
  - evidence: `glp_gleam/src/glp/codec/result_envelope_builder.gleam:147` · test: `glp_gleam/test/glp/codec/suspended_acceptance_test.gleam:1-4`

### `suspension-table`  (interface)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-005**: Heap-level suspension storage, activation, and forwarding (FR-008) is delivered as an opaque SuspensionTable cross-validated against the Dart suspension-pointer tests.
  - evidence: `glp_gleam/src/glp/runtime/suspension.gleam:20-84` · test: `glp_gleam/test/glp/runtime/suspension_test.gleam:1-5`

### `suspension-wake-dedup`  (pattern)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-008**: Reactivation dedup keyed by (goal id, suspension generation) with stale-wake dropping and atomic suspension consumption is delivered and test-locked (FR-005).
  - evidence: `glp_gleam/src/glp/engine/types.gleam:48-102` · test: `glp_gleam/test/glp/engine/dedup_key_test.gleam:1-4`

### `suspension-writer-address-model`  (pattern)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-009**: The Gleam suspension model deliberately carries writer addresses in Si/U (a documented adaptation of Dart's reader-address bookkeeping), reactivating on writer binding — behaviorally equivalent and test-locked.
  - evidence: `glp_gleam/src/glp/engine/runner.gleam:26-31` · test: `glp_gleam/test/glp/runtime/suspension_test.gleam:1-5`

### `tcp-length-prefix-framing`  (protocol)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-042**: The 4-byte big-endian length-prefix TCP wire framing with FrameCodec payloads riding opaquely is delivered at Dart/C# byte parity and test-locked.
  - evidence: `glp_gleam/src/glp/link/transports/tcp.gleam:10-13` · test: `glp_gleam/test/glp/link/tcp_test.gleam:3-4`

### `unification-three-valued-verdict`  (protocol)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-003**: Three-valued unification (Success/Suspend/Fail, unbound reader suspends, writer-writer is a structural error, no occurs-check) is delivered with the full SC-003 verdict-table test.
  - evidence: `glp_gleam/src/glp/runtime/unify.gleam:19-40` · test: `glp_gleam/test/glp/runtime/unify_test.gleam:1-4`

### `unification-writer-mgu`  (pattern)

- **Axes**: delivery=`delivered` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-004**: The writer-MGU discipline (binds only writers, never readers, never writer-writer, sigma-hat applied atomically at Commit) is delivered and adversarially test-locked alongside a Lean proof artifact.
  - evidence: `glp_gleam/src/glp/engine/runner.gleam:1-13` · test: `glp_gleam/test/glp/engine/writer_mgu_adversarial_test.gleam:1-11`

## 3. PARTIAL in Gleam — present part + named missing part (9)

### `body-kernel`  (interface)

- **Axes**: delivery=`partial` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-010**: The body-kernel set (arithmetic with Dart num-promotion parity, math/type-conversion, univ, _output, and the mwm mutable-ref subsystem made immutable-safe) is recorded complete against corpus needs.
  - evidence: `specs/050-full-gleam-combined/tasks.md:64,85 (T024/T034) + baseline.md:79-82 (T042 math/univ/mwm fixes)` · test: `specs/050-full-gleam-combined/tasks.md:64 (15 gleeunit) + baseline.md:66-69 (corpus parity)`
- **b2-c1-016**: Native body kernels are delivered for arithmetic, math, conversion, univ, mutual-reference stream append, and _output, while the effectful _now and _send kernels remain unregistered in the standalone engine.
  - evidence: `glp_gleam/src/glp/engine/kernels.gleam:20-97` · test: `glp_gleam/test/glp/engine/arith_guards_kernels_test.gleam:1-7`
- **b3-c1-014**: The reference delivers a body-kernel registry of two-valued (success/abort) inline kernels covering arithmetic, math, conversion, structure, stream-append, messaging, output, and module-activation operations.
  - evidence: `glp_runtime/lib/runtime/body_kernels.dart:52-106` · test: `programs/tests/test_mutual_ref.glp`

### `bytecode-runner`  (interface)

- **Axes**: delivery=`partial` · promise=`partial` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-006**: The v2.16 bytecode engine with positional X-registers is recorded complete for the M1 corpus, but Distribute/Transmit/Allocate/Deallocate opcodes are recorded deferred (US4/unused) and a UnifyConstant ground-struct-literal divergence was escalated rather than resolved.
  - evidence: `specs/050-full-gleam-combined/tasks.md:35-36,59,61 (T006/T007/T019/T021) + spec.md:129 (FR-006)` · test: `specs/050-full-gleam-combined/tasks.md:41 (T012 opcode-table tests) + tasks.md:59 (merge/3 golden pin)`
- **b2-c1-012**: The bytecode runner is delivered for the production-emitted opcode set (HEAD/GUARD/BODY families, Commit, Spawn) but Requeue tail-calls, environment frames (Allocate/Deallocate), and module-RPC opcodes (Distribute/Transmit) surface as Unimplemented runner faults.
  - evidence: `glp_gleam/src/glp/engine/runner.gleam:247,300-374` · test: `glp_gleam/test/glp/engine/runner_test.gleam:1-6`
- **b3-c1-005**: The reference delivers a bytecode interpreter whose public surface is BytecodeProgram (ops + labels + definedGuards) executed to a four-valued RunResult.
  - evidence: `glp_runtime/lib/bytecode/runner.dart:17-70` · test: `no-test: Dart unit tests live at glp_runtime/test/ outside this slice; behavior exercised by programs/tests/ corpus`

### `distribution-protocol`  (protocol)

- **Axes**: delivery=`partial` · promise=`designed` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-034**: Distributed unification for the Gleam instance (deferred-local-assignment, globalize/localize on known/1, RemoteVarRef as (instance id, writer id)) is designed and proof-gated but not recorded delivered.
  - evidence: `specs/050-full-gleam-combined/data-model.md:49-50 + contracts/link-parity.md:15-19 + tasks.md:128 (T051 [ ])` · test: `no-test: T057 adversarial dist-deref suite (tasks.md:134) unchecked`
- **b2-c1-051**: The distribution protocol is partial — every wire layer up to transports is delivered and tested, but engine-to-engine session establishment and remote goal/result routing above the seam do not exist in code.
  - evidence: `glp_gleam/src/glp/link.gleam:1-9 (link subsystem placeholder) + glp_gleam/src/glp/link/seam/transport.gleam:39-51` · test: `no-test: no test connects two engines or routes a goal/result across a link`
- **b3-c1-045**: A term-to-bytes distribution serialization with globally identified variables is the delivered cross-agent wire discipline.
  - evidence: `glp_runtime/lib/multiagent/payload_serializer.dart:1-18` · test: `no-test: serializer unit tests outside slice; exercised by multiagent plays`

### `embeddability-api`  (interface)

- **Axes**: delivery=`partial` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-018**: An in-process embeddability surface exists as the recorded-complete engine-value API (opaque Engine, new/load/run/step/start/Event, no process-dictionary or ETS state), which is the sole host-embedding hook the design corpus records for the Gleam instance.
  - evidence: `specs/050-full-gleam-combined/contracts/gleam-instance-surface.md:25-30 + tasks.md:69,89 (T029 + facade step/Event polish)` · test: `specs/050-full-gleam-combined/tasks.md:89 (step envelope proven == run envelope; 4 tests)`
- **b2-c1-050**: Embeddability is partial — the engine is an embeddable pure value with an explicit prelude-injection seam, but no dedicated host-embedding API (and no yngenios integration surface) exists in code.
  - evidence: `glp_gleam/src/glp/engine.gleam:107-110` · test: `glp_gleam/test/glp/engine_test.gleam:1-7`
- **b3-c1-037**: An embeddable engine API (host-callable load/run/configure surface decoupled from any UI) is delivered in the Dart reference and is the embeddability baseline a Gleam implementation must provide.
  - evidence: `glp_runtime/lib/engine/glp_engine.dart:1-48,115-120` · test: `no-test: engine unit tests at glp_runtime/test/ outside slice; consumed by REPL, IsolateManager, and C# equivalence harness`

### `guard-kernel`  (interface)

- **Axes**: delivery=`partial` · promise=`delivered` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-009**: The three-valued guard library (ground/known/otherwise/=?=, arithmetic and standard-order comparisons, type tests, wait guards, and the 049 satisfiable/2 defined-guard interpreter) is recorded functionally complete for parity.
  - evidence: `specs/050-full-gleam-combined/tasks.md:63-64 (T023/T024) + baseline.md:83-84 (defined-guard interpreter + wait guards in T042 fixes)` · test: `specs/050-full-gleam-combined/tasks.md:63-64 (guard gleeunit) + baseline.md:66-69 (206-case parity)`
- **b2-c1-015**: Guard-kernel evaluation is delivered for the full pure three-valued guard set (with arith_guards_kernels_test also locking comparisons, type tests, =?=, @<), while timer guards wait/wait_until remain unimplemented faults.
  - evidence: `glp_gleam/src/glp/engine/runner.gleam:353-371,2229` · test: `glp_gleam/test/glp/engine/guards_test.gleam:1-4`
- **b3-c1-011**: The builtin guard set — ground/known/unknown/otherwise, arithmetic comparisons, type guards, guard negation, and arithmetic expression evaluation in guards — is a required kernel surface.
  - evidence: `docs/glp-bytecode-v216-complete.md sections 11 and 20 (with glp_runtime/lib/bytecode/opcodes.dart:217-262)` · test: `programs/tests/test_defined_guards_all.glp; programs/tests/test_guard_negation.glp`

### `link-layer`  (interface)

- **Axes**: delivery=`partial` · promise=`designed` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-033**: The Gleam port of the 025 link primitives (link_send/link_recv, establish/listen/accept/request/setup/close/monitor kernels, registry, pump, egress) is contractually designed (025 contracts verbatim, deviations escalate) but not recorded delivered.
  - evidence: `specs/050-full-gleam-combined/tasks.md:127 (T050 [ ]) + contracts/link-parity.md:5-7 + specs/025-multi-protocol-link-layer/contracts/link-primitives.md` · test: `no-test: T056 link round-trip tests (tasks.md:133) unchecked`
- **b2-c1-039**: The link-layer seam below GLP (endpoint vtable, transport constructor surface, scheme/address/id/options/fault types) is delivered with two serving transports, while the GLP-facing link primitives, fault-as-data decoration, sequence/dedup, and QUIC leaf remain gaps.
  - evidence: `glp_gleam/src/glp/link/seam/endpoint.gleam:39-47` · test: `glp_gleam/test/glp/link/loopback_test.gleam:1-8`
- **b3-c1-046**: The GLP-visible link-layer kernel surface (setup, send, request/listen/accept handshake, monitor, close) is delivered identically in both reference runtimes.
  - evidence: `glp_runtime/lib/link/primitives/link_kernels.dart:23-57 (C# parity tree at csharp/glp_link/primitives/)` · test: `programs/tests/link/ (6 GLP programs); csharp/glp_link.tests/LinkSetupKernelTests.cs`

### `module-system`  (interface/pattern)

- **Axes**: delivery=`partial` · promise=`absent` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-057**: A Gleam-side module system is absent from the 050 design promises (spec and tasks are silent), with module-system delivery recorded only in Dart-era handovers — a potential parity scope gap versus the reference runtimes.
  - evidence: `specs/050-full-gleam-combined/spec.md (silent on modules) + docs/handover/module-phase1-handover.md:1-5 (Dart-side, Completed 2026-02)` · test: `no-test: no Gleam module-system task exists in tasks.md`
- **b2-c1-046**: The module system is partial — declaration parsing, export/import flags, and Distribute/Transmit code generation are delivered, but runtime execution of module RPC is unimplemented so cross-module calls fault.
  - evidence: `glp_gleam/src/glp/parser/parser.gleam:2198-2254 + glp_gleam/src/glp/compiler/codegen.gleam:460-464` · test: `no-test: no test exercises a module-qualified remote call end-to-end; runner.gleam:374 returns Unimplemented for Distribute/Transmit`
- **b3-c1-027**: The directory-based self.glp module scope chain (ancestor scoping, shadowing, sibling isolation) is a delivered module-system capability.
  - evidence: `glp_runtime/lib/runtime/module_hierarchy.dart:1-9` · test: `programs/tests/module_self_shadow/; programs/tests/module_self_local_shadow/; programs/tests/module_self_type_error/`

### `quic-client-inprocess`  (user-story)

- **Axes**: delivery=`partial` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-045**: In-process QUIC on the BEAM is partial — a client-only Profile C implementation mirroring the C# wire contract exists (server role explicitly unsupported), gated on the external quicer build and untested in the corpus.
  - evidence: `gleam_quic/src/glpq_quic.erl:1-50` · test: `no-test: gleam_quic/test is empty`

### `quic-transport`  (interface/protocol)

- **Axes**: delivery=`partial` · promise=`designed` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-032**: The Gleam QUIC-WS/HTTP3 transport (gleam_quic Profile-C quicer/MsQuic FFI, RFC-6455-over-QUIC-bidi parity with the C# reference peer, WSL-only runtime) is designed and gating but not recorded delivered, with a recorded host-block risk.
  - evidence: `specs/050-full-gleam-combined/tasks.md:132 (T055 [ ]) + research.md:37-45 (R5) + contracts/link-parity.md:27` · test: `no-test: QUIC round-trip is the QUIC part of T056 (tasks.md:133), unchecked`
- **b2-c1-043**: QUIC transport is partial — glp_gleam declares only the quic link-scheme token with no transport leaf, and the gleam_quic package supplies Profile A (C#-side-process relay) and Profile C (in-process quicer client) data planes without any in-corpus tests.
  - evidence: `glp_gleam/src/glp/link/seam/link_scheme.gleam:33-36 + gleam_quic/src/glp_quick_gleam.gleam:23-49` · test: `no-test: gleam_quic/test contains no test files and no glp_gleam test exercises the quic scheme`
- **b3-c1-054**: QUIC transport is delivered in the C# reference only (real-QUIC-only with cert pinning), with no Dart counterpart, making it a single-runtime reference capability.
  - evidence: `csharp/glp_link/transports/QuicTransport.cs:17-25 (Dart transports dir contains only loopback_transport.dart and tcp_transport.dart)` · test: `csharp/glp_link.tests/QuicTransportTests.cs; QuicReliabilityTests.cs; QuicTeardownTests.cs`

## 4. RESOLVED GAPS — promised/required AND corroborated absent-after-sweep (1)

### `fe-be-process-split`  (pattern/user-story)

- **Axes**: delivery=`absent-after-sweep` · promise=`designed` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-038**: The REPL/engine two-process split MVP (separate front-end and engine processes over TCP loopback) remains a designed-but-unstarted roadmap promise — its declared blocker is delivered, yet no spec or delivery is recorded.
  - evidence: `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md:32 (repl-engine-process-split-mvp [refined], blocked-by result-codec-and-framecodec-ride) + :119 (wave-2 [captured])` · test: `no-test: no spec dir for the process-split MVP exists under specs/`
- **b2-c1-049**: Front-end/back-end process separation is absent — the designed ED-1 envelope seam exists and is byte-identity-tested, but no code actually splits a front end from an engine back end across processes.
  - evidence: `searched glp_gleam/src/** for a REPL/engine process boundary: the REPL holds the engine value in-process (glp_gleam/src/glp/repl/commands.gleam:37-39); the only processes spawned are link channel/hub and test helpers` · test: `no-test: envelope_identity_test locks the seam's byte-identity, not a process split`
- **b3-c1-040**: The reference delivers engine/UI separation and a stdio process bridge but no true front-end/back-end process split of the runtime itself, so that roadmap ambition has only an in-process precedent to match.
  - evidence: `glp_runtime/bin/glp_repl.dart:1-5 (with csharp/glp_quick_host/Program.cs:13-19 and glp_runtime/lib/multiagent/agent_runtime.dart:1-11)` · test: `no-test: architectural property; no dedicated in-slice test`

## 5. GAPS by code testimony only (absent-after-sweep, no promise/reference row yet) (1)

### `link-reliability-sequencing`  (protocol)

- **Axes**: delivery=`absent-after-sweep` · promise=`not-promised` · parity-required=no · builders=b2
- **b2-c1-057**: The link reliability sequence/dedup sublayer is absent — the reliability directory holds only the frame codec and CRC-32, with no sequencing, ack, or dedup protocol implemented.
  - evidence: `searched glp_gleam/src/glp/link/reliability/ (only crc32.gleam and frame_codec.gleam exist) and glp_gleam/src/** for sequence/ack/dedup reliability symbols: none; endpoint.gleam:5-7 names "sequence/dedup" as sublayer scope` · test: `no-test: no sequencing/dedup reliability test exists`

## 6. OPEN ESCALATIONS — cross-corpus conflicts (ENGINEER to resolve) (2)

### `mesh-ring`  (protocol/user-story)

- **Axes**: delivery=`absent-after-sweep` · promise=`not-promised` · parity-required=yes · builders=b2,b3
- **b2-c1-048**: Mesh/ring topology support is absent from the Gleam implementation corpus — no module, type, or test references a mesh or ring.
  - evidence: `searched glp_gleam/src/** and gleam_quic/src/** for word-boundary mesh/Mesh/ring/Ring: zero matches` · test: `no-test: no mesh or ring test exists in the corpus`
- **b3-c1-057**: Multi-client mesh messaging over QUIC, driven from GLP source programs, is a delivered reference user story.
  - evidence: `programs/tests/quic/quic_mesh.glp (volume variants quic_mesh_t043.glp, quic_mesh_local_t043.glp)` · test: `csharp/glp_link.tests/QuicMeshTests.cs`

### `multiagent-runtime`  (interface/pattern)

- **Axes**: delivery=`absent-after-sweep` · promise=`deferred` · parity-required=yes · builders=b1,b2,b3
- **b1-c1-058**: The Gleam multiagent runtime exists in the design corpus only as a promised scaffold placeholder with cross-agent imported-variable support explicitly deferred to link-layer successors, and no delivery is recorded.
  - evidence: `specs/033-glp-gleam-subtree-scaffold/spec.md:24 (all-8-subsystems incl. multiagent) + specs/034-glp-gleam-core-terms-and-heap/spec.md:23 (imported variables out of scope, lands with link features F9+)` · test: `no-test: no multiagent port task exists in 050 tasks.md`
- **b2-c1-047**: The multiagent runtime is absent from the Gleam implementation — only an explicit empty-but-building placeholder module exists, deferring the port of glp_runtime/lib/multiagent/.
  - evidence: `glp_gleam/src/glp/multiagent.gleam:1-9 (explicit empty placeholder; also searched glp_gleam/src/** for agent/actor/mailbox/multiagent symbols beyond it: none)` · test: `no-test: no multiagent test exists in glp_gleam/test`
- **b3-c1-041**: A multiagent runtime wrapping the engine per agent with messaging and host callbacks is a delivered reference capability.
  - evidence: `glp_runtime/lib/multiagent/agent_runtime.dart:1-67` · test: `no-test: multiagent unit tests at glp_runtime/test/multiagent/ outside slice; plays at programs/multiagent/`

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

## Open items (E10 hard-stop record — nothing silently dropped)

- **Residual cycle**: cycle-2 self-verification (weakest-citation tightening + unswept-area sweeps per E7) not run — budget_stop at 574k/600k after cycle 1. Re-open by resuming run `20260719T130005Z-782b` with a fresh budget.
- **Open escalations (engineer to resolve)**: `mesh-ring`, `multiagent-runtime` — reference/spec testimony conflicts with b2 absent-after-sweep; resolve by ruling whether these are in-scope gaps for the full-scope feature.
- **Merge candidates**: 40 near-miss key pairs were all judged DISTINCT by the Critic; the pair list is preserved in `phase1-detail-join.json` (merge_candidates) for cycle-2 re-check.
- **Unswept areas**: per-builder coverage manifests (in the claims files) mark partially-swept/not-swept areas — notably b1: non-Gleam spec bodies (380 files, roadmap-rows only); b3: programs corpus breadth-only (1183 files), C# engine source at out/csharp (outside slice).
