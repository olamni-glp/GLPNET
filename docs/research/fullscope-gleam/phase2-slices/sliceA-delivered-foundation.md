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

