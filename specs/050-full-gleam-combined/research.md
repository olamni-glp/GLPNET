# Phase 0 Research: 050-full-gleam-combined

All unknowns from the Technical Context resolved below. Format: Decision / Rationale / Alternatives considered. Sources: repo survey (2026-07-10), 036 baseline dossier (`docs/research/glp-gleam-baseline/pipelines/`), spec clarifications (2026-07-10).

## R1 — Parser conformance oracle (dossier fork D1, absorbed ANTLR spike)

**Decision**: Hand-port a recursive-descent lexer+parser from `glp_runtime/lib/compiler/` into `glp_gleam/src/glp/parser/`. Conformance is corpus-driven: the shared corpus + the Dart parser's accept/reject behaviour is the oracle. No `.g4` is authored or generated in this feature; the canonical-grammar artifact remains a dossier-level concern (D1 stays open at dossier level, closed operationally here).

**Rationale**: The only `.g4` in the repo is the single-clause spike (`spike/p5-il-merge/grammar/merge.g4`); a canonical `Glp.g4` does not exist as a shipped file. Authoring one is not needed to reach parity — the Dart parser IS the de-facto canonical grammar, and the spec (FR-002, Assumptions) already fixes conformance-by-corpus. An ANTLR BEAM target does not exist upstream.

**Alternatives considered**: (a) author canonical `Glp.g4` first, generate Dart/C# parsers, hand-port Gleam — rejected: multiplies scope, blocks M1 on a documentation artifact; (b) ANTLR-generated Erlang parser — rejected: no maintained ANTLR Erlang/Gleam target (spike finding).

## R2 — Engine concurrency model (dossier fork D8)

**Decision**: Scheduler-actor inside one BEAM process per GLP instance: the engine is a pure state-stepping function (goal queue + immutable heap + suspension table) driven by a scheduler loop; `erlang:spawn` + Subjects are used at the instance/link boundary (REPL process, link pumps, transport acceptors), not per GLP goal.

**Rationale**: Matches the constituent brief ("3-phase runner+scheduler over 034 immutable heap"; "value-copy port") and keeps determinism for corpus parity — process-per-goal would make scheduling nondeterministic and golden-output agreement unachievable. Immutable heap + value copying demands the generation-scoped activation dedup (FR-005) precisely because reactivation is message-like, not pointer-like.

**Alternatives considered**: process-per-goal (one BEAM process per GLP goal) — rejected: nondeterministic interleaving breaks golden parity, spawn cost per reduction, and monitors/links complexity; `gleam_otp` actors — rejected: dependency excluded by FR-007 (AtomVM path).

## R3 — Bytecode + registers

**Decision**: Implement the canonical v2.16 instruction set per `docs/glp-bytecode-v216-complete.md` (normative), preserving the positional X-register model (FR-006). Loader accepts the same program shape the Dart/C# assemblers emit (`asm.dart`/`asm.cs` are the porting references). No opcode additions/renumbering (respects dossier D4 ISA discipline).

**Rationale**: Spec-first — the bytecode doc is the single normative source; both existing runtimes conform to it; corpus parity is only meaningful over the same ISA.

**Alternatives considered**: compile to an intermediate Gleam-native form — rejected: breaks bytecode-level traceability and the D4 freeze discipline.

## R4 — Golden outputs + differential harness (MISS-04)

**Decision**: Create `test/parity/` as the ONE recording location: `record_dart_goldens.sh` runs each shared-corpus program on the Dart REPL and records stdout-normalized outcomes to `test/parity/goldens/`; the Gleam corpus runner diffs against those files; `run_differential.sh` runs one program on Dart+C#+Gleam and reports any divergence (closes MISS-04 / FR-012). Divergence handling: STOP, report per Bug Protocol (three-way evidence: Dart output, Gleam output, spec anchor) — never patch the port to "make it pass" without identifying which side violates the spec.

**Rationale**: `test/run_all_tests.sh` keeps expectations as inline assertion strings, which cannot be diffed cross-runtime; recorded goldens make 100%-agreement (SC-001) measurable and give the 10× wall-clock bound (SC-009) its reference timings from the same recording run.

**Alternatives considered**: (a) porting the inline-assertion bash suite verbatim to Gleam — rejected: duplicates expectations (violates single-source-of-truth), no cross-runtime diffing; (b) treating C# as truth — rejected: Dart is the mandated reference/source-of-truth runtime for glp_runtime semantics.

## R5 — Transports (clarification Q1: full parity, gating)

**Decision**: Three transports in `glp_gleam/src/glp/link/transports/`: (1) loopback — in-BEAM message passing; (2) TCP — `gen_tcp` via `gleam_erlang`/FFI; (3) QUIC-WS/HTTP3 — FFI onto the existing `gleam_quic` Profile-C stack (`quicer`/MsQuic), speaking the same RFC-6455-over-QUIC-bidi framing as C# `WebSocketOverQuic.cs`. Wire behaviour (framing, handshake, close) must interop with `csharp/glp_link/transports/` — C# is the interop peer and the QUIC wire reference. QUIC runtime testing happens under WSL (Profile-C is WSL-only per 049); loopback/TCP test natively and under WSL.

**Rationale**: Owner clarification made all three gating. C# is the only existing QUIC-WS implementation, so parity is defined against it. `gleam_quic/profile_c` already vendors quicer+MsQuic with a Windows MSVC patch — groundwork exists, wiring is the work.

**Alternatives considered**: deferring QUIC-WS (constituent-brief scope "loopback→gen_tcp") — rejected by owner clarification 2026-07-10; implementing HTTP/3 stack natively in Gleam — rejected: quicer/MsQuic is the established Profile-C route.

**Risk (tracked)**: quicer/MsQuic on BEAM is the highest-variance item (build quirks, WSL-only runtime). Mitigation: transport seam (`i_link_transport` port) keeps QUIC isolated; loopback/TCP land first; QUIC-WS lands behind the same seam with the C# peer as test partner.

## R6 — Proof tooling (clarification Q5: Lean + prose + tests for BOTH)

**Decision**: Two Lake projects under `glp_gleam/lean/`: `WriterMguBindsOnlyWriters/` (PI:14) and `DistDerefConvergence/` (PI:17), following the repo convention (`lakefile.lean`, `<Name>/Basic.lean`, colocated with the code they verify — precedent: `csharp/glp_result_codec/lean/ResultTermRoundTrip/`). Prose proofs land in `docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/` and the INDEX status flips OPEN→discharged. Targeted adversarial test suites accompany each (engine tests for writer-MGU; link tests for dist-deref).

**Recorded deviation**: the P4 proof harness originally planned **SPIN** for dist-deref (PI:17). The 2026-07-10 clarification requires Lean for both; the existing SPIN handshake precedent (`docs/research/repl-engine-separation/spikes/spin/`) MAY be kept as supplementary evidence but the gating artifact is the Lean proof. This deviation is deliberate and owner-directed.

**Alternatives considered**: SPIN-only for dist-deref (dossier plan) — superseded by clarification; test-evidence-only — rejected by clarification.

## R7 — REPL + engine-as-value + envelope seam (ED-1)

**Decision**: `glp_gleam/src/glp/repl/` implements the REPL loop as the package entry point (`gleam run` / escript). The engine is a typed Gleam value (opaque `Engine` record: program store, heap, scheduler state) — no global state. Results flow through the existing 038 `result_envelope(+builder)` modules: the REPL binds the seam in-process; the link layer binds the identical envelope over-the-wire (FR-009). `:trace`, `:limit N`, `:quit` port the Dart REPL's command semantics; goal outcomes report success/suspension/failure + bindings in the reference format.

**Rationale**: The envelope/builder modules already exist in Gleam (038) with parity tests — reusing them satisfies the ED-1 "identical envelope" requirement by construction; folding #10/#11 scope per the combined brief.

**Alternatives considered**: separate REPL package — rejected: single-package instance is the constituent-brief shape ("single combined IN-PROCESS Gleam instance").

## R8 — Build/test topology on Windows

**Decision**: Build natively (`gleam build --target erlang`, OTP 29/Gleam 1.17/rebar3 3.27 on user PATH); run `gleeunit` suites under WSL (known gleeunit Windows path-separator defect in test discovery); run the corpus/differential/link bash harnesses under git-bash (native) except QUIC scenarios which run under WSL. CI-facing scripts declare their host requirement in a header comment.

**Rationale**: Established by 038/049 practice and the 2026-07-10 toolchain memory; fixing gleeunit upstream is out of scope.

**Alternatives considered**: patching gleeunit locally — rejected: out-of-scope upstream fix, drift risk; WSL-only development — rejected: native build works and keeps the Windows dev loop fast.
