# Full-scope Gleam GLP — Deduplicated Delivered-vs-Gaps Inventory

**Date**: 2026-07-19 · **Method**: 3-role task team (spec-051), run `20260719T130005Z-782b`, frozen method `method-20260719T130005Z-782b` (3 codex red-team passes) · **Marathon**: `mrun-8bda036d9e9b` step `phase1-3rtask-gap-analysis` · **Anchor feature**: `full-scope-gleam-glp-implementation`

Three blind builders on pairwise-disjoint corpora — b1 roadmap+specs+handovers (design promises), b2 glp_gleam+gleam_quic code (implementation truth), b3 Dart/C# runtimes + normative docs + programs corpus (full-scope reference) — 206 attributed claims, joined mechanically by detail_id (154 capabilities), adjudicated by the cross-provider codex Critic (201 CONFIRM / 5 ESCALATE / 0 REFUTE; all 40 near-miss key pairs judged distinct). Verdict: budget_stop after cycle 1 (600k budget; cycle-2 self-verification pass is the named residual). Every entry cites its source claim_ids; full claims live in `.specify/3rtask/runs/20260719T130005Z-782b/cycle01/claims-builder-*.json`.

**Reading rule**: `delivery` is b2 code testimony ONLY; `promise` is b1 specs testimony; `parity-required` means b3 found it in the reference. UNCONFIRMED-GAP = promised/required with no b2 testimony either way (candidate gap pending code-side verification — NOT confirmed delivered).

**yngenios embeddability** is REQUIREMENTS-LEVEL: no yngenios sources exist in-repo; `embeddability-api` appears below at its evidence level (in-process engine-value API delivered; host-embedding/yngenios surface absent) and the yngenios integration itself is gap-by-definition.

---

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


## Open items (E10 hard-stop record — nothing silently dropped)

- **Residual cycle**: cycle-2 self-verification (weakest-citation tightening + unswept-area sweeps per E7) not run — budget_stop at 574k/600k after cycle 1. Re-open by resuming run `20260719T130005Z-782b` with a fresh budget.
- **Open escalations (engineer to resolve)**: `mesh-ring`, `multiagent-runtime` — reference/spec testimony conflicts with b2 absent-after-sweep; resolve by ruling whether these are in-scope gaps for the full-scope feature.
- **Merge candidates**: 40 near-miss key pairs were all judged DISTINCT by the Critic; the pair list is preserved in `phase1-detail-join.json` (merge_candidates) for cycle-2 re-check.
- **Unswept areas**: per-builder coverage manifests (in the claims files) mark partially-swept/not-swept areas — notably b1: non-Gleam spec bodies (380 files, roadmap-rows only); b3: programs corpus breadth-only (1183 files), C# engine source at out/csharp (outside slice).
