# Feature Specification: GLEAM implementation — combined Full-Gleam feature

**Feature Branch**: `050-full-gleam-combined`
**Created**: 2026-07-10
**Status**: Draft
**Input**: User description: "GLEAM implementation — combined Full-Gleam feature"

## Overview

The Full-Gleam chain is currently split across six interdependent refined roadmap features; building them piecemeal costs a session of coordination overhead per slice. This combined feature delivers a **complete standalone Gleam GLP instance end-to-end**: compiler + loader, bytecode runner/engine, standalone REPL, the shared test corpus ported with golden-output parity, the multi-protocol link layer, and C#↔Gleam cross-runtime distributed tests. When it ships, GLP has a third full runtime (after Dart and C#) that behaves observably identically on the shared corpus and interoperates with the shipped C# instance over the wire.

Folded constituent features (roadmap rows stay `refined`; advance/close them when this ships): `glp-gleam-compiler-and-loader`, `glp-gleam-bytecode-runner`, `glp-gleam-repl`, `glp-test-corpus-port-and-runner`, `glp-gleam-link-layer`, `cross-runtime-csharp-gleam-distributed-tests`. The `antlr4-shared-grammar-spike` scope decision is absorbed here (hand-ported recursive-descent parser conforming to the canonical shared grammar; no ANTLR-generated parser on BEAM — no hard dependency kept).

## Clarifications

### Session 2026-07-10

- Q: Gleam link-layer transport scope for this feature? → A: Full multi-protocol parity with the shipped C# layer — in-process loopback, TCP, and QUIC-WS/HTTP3 are all in scope and gating.
- Q: AtomVM/Profile-C acceptance scope? → A: BEAM-only acceptance; AtomVM compatibility preserved by construction (FR-007), not an acceptance gate.
- Q: Cross-runtime capstone acceptance bar? → A: Full suite required — all 16 pair scenarios split C#↔Gleam must pass (16/16); hard gate.
- Q: Performance expectation for the Gleam instance? → A: Sanity bound — the full shared corpus completes within 10× the Dart reference wall-clock; no stricter optimization goal.
- Q: Discharge form for the two OPEN proof obligations? → A: Both mechanized and written — Lean proofs AND prose proofs in the feature dossier, plus targeted adversarial tests, for writer-MGU-under-value-copy and distributed-dereference convergence.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Load and run GLP programs on a standalone Gleam instance (Priority: P1)

A GLP user (or the port effort's CI) loads a `.glp` program into a standalone Gleam-hosted GLP instance and runs goals, obtaining the same observable results as the Dart reference instance. Loading a file runs the full pipeline — SRSW check → partial evaluation → type check → compile → load — exactly as on the existing instances: if it loads, it passed every stage.

**Why this priority**: This is the M1 anchor. Without compile+load+execute there is no Gleam instance at all; every other story builds on it.

**Independent Test**: Load a known-good corpus program, run its goal, compare the result against the recorded Dart reference output. Load a known-bad program (SRSW violation, type error) and verify it is rejected at the same stage as on the reference.

**Acceptance Scenarios**:

1. **Given** a well-typed `.glp` program from the shared corpus, **When** it is loaded into the Gleam instance, **Then** it passes SRSW → partial evaluation → type check → compile → load and its goals execute with three-phase (HEAD/GUARD/BODY) semantics including suspension and reactivation.
2. **Given** a `.glp` program with an SRSW violation or type error, **When** it is loaded, **Then** loading fails at the corresponding stage with a diagnostic equivalent in class to the Dart/C# reference behaviour.
3. **Given** a goal that suspends on an unbound reader, **When** the paired writer is later bound, **Then** the goal reactivates exactly once (no duplicate activations).

---

### User Story 2 - Interactive REPL on the Gleam instance (Priority: P2)

A GLP user starts the standalone Gleam REPL, loads `.glp` files, runs goals interactively, and uses `:trace` and `:limit` — the same working surface they know from the Dart and C# REPLs. Results are produced through the same result-envelope seam whether consumed in-process or over the wire.

**Why this priority**: The REPL is the user-facing entry point that makes the instance usable and testable by a person; reaching it constitutes milestone M1.

**Independent Test**: Start the REPL, load a corpus file, run a goal, observe the result; exercise `:trace` and `:limit` on a long-running goal.

**Acceptance Scenarios**:

1. **Given** a running Gleam REPL, **When** the user loads a corpus `.glp` file and runs a goal, **Then** the goal's outcome (success / suspension / failure and bindings) matches the reference instance's outcome for the same program and goal.
2. **Given** a goal under `:limit N`, **When** the reduction budget is exhausted, **Then** execution stops and reports in the same way the reference REPLs do.
3. **Given** the same goal executed in-process and via the wire seam, **When** results are delivered, **Then** the result envelope contents are identical.

---

### User Story 3 - Shared test corpus runs green on Gleam with recorded-output parity (Priority: P2)

The port effort runs one shared cross-runtime test corpus against the Gleam instance and gets green, with 100% agreement against the recorded Dart golden outputs. The corpus explicitly includes the known gap/fork cases so parity cannot be declared while any of them diverges.

**Why this priority**: The corpus is the objective evidence of behavioural parity (outcome-equivalence); it is the M1 LOCK criterion.

**Independent Test**: Execute the ported corpus runner against the Gleam instance and diff every output against the recorded golden outputs.

**Acceptance Scenarios**:

1. **Given** the ported shared corpus and runner, **When** the suite runs on the Gleam instance, **Then** every case agrees with the recorded Dart reference output (100% agreement, zero unexplained divergences).
2. **Given** the gap/fork cases (GAP-G1, GAP-G2, GAP-G3, GAP-G8, FORK-1), **When** the suite runs, **Then** each is present as an explicit test and passes, as a precondition for declaring parity.
3. **Given** the cross-runtime differential harness, **When** the same case is run on two runtimes, **Then** any output divergence is detected and reported automatically.

---

### User Story 4 - Gleam instance joins peer-to-peer network links (Priority: P3)

A distributed-GLP operator connects a Gleam instance to a peer instance using the multi-protocol link layer (link primitives such as `link_send`/`link_recv`), with byte-identical wire encodings so any conforming peer can interoperate.

**Why this priority**: Networking (M2) extends the standalone instance to distributed use; it depends on the instance existing (P1/P2) and proven (P2 corpus).

**Independent Test**: Bring up two instances on one host, establish a link over each in-scope transport (loopback, TCP, QUIC-WS/HTTP3), exchange terms, and verify the received terms and the on-wire bytes.

**Acceptance Scenarios**:

1. **Given** two linked instances, **When** a term is sent from one and received on the other, **Then** the received term is equivalent and the on-wire encoding is byte-for-byte identical to the shipped codec's encoding for that term.
2. **Given** a distributed unification touching a remote variable, **When** it resolves, **Then** assignment is deferred to the owning side (deferred-local-assignment) and both sides converge on the same binding.
3. **Given** a malformed or hostile incoming frame, **When** it is received, **Then** it is rejected safely and surfaced as data (fault-as-data), never crashing the instance.

---

### User Story 5 - Cross-runtime C#↔Gleam distributed test pairs (Priority: P3)

GLP test/CI runs one side of a role-parameterized pair program on the shipped C# instance and the other side on the Gleam instance, connected via the link layer, and gets verdicts identical to the single-runtime reference runs — mirroring the existing Dart↔C# 16/16 result.

**Why this priority**: This is the capstone (M2 LOCK): it proves two independently implemented runtimes interoperate correctly under the adversarial corpus. It is inherently cross-runtime and cannot be collapsed into the other stories.

**Independent Test**: Run the pair suite with roles split C#/Gleam and compare every verdict with the recorded reference verdicts.

**Acceptance Scenarios**:

1. **Given** a role-parameterized pair program, **When** one role runs on C# and the other on Gleam over the link layer, **Then** the run completes with verdicts identical to the single-runtime reference.
2. **Given** the adversarial corpus, **When** the split-pair suite runs, **Then** all verdicts agree with the reference (target: mirror the Dart↔C# 16/16 suite).

---

### Edge Cases

- **Duplicate reactivation**: a goal suspended on several variables must reactivate exactly once per suspension generation — deduplication must key on (goal identity, suspension generation), not bare goal identity (known prior defect class).
- **Writer-MGU safety under value-copy semantics**: the port copies values rather than sharing mutable cells; the writer-MGU discipline (only writers bind, never readers, never writer-to-writer) must be shown to hold — an OPEN proof obligation gating M1.
- **Distributed dereference**: chains that cross instance boundaries must terminate and converge — an OPEN proof obligation gating M2.
- **Quiescence detection**: distributed tests need an oracle for "the computation is done" (deadlock/quiescence distinction) before distributed acceptance can be judged.
- **Untrusted wire input**: frames from the network are untrusted input; oversized, truncated, or type-confused frames must be rejected without memory unsafety or crash loops.
- **Goals that legitimately suspend forever**: the REPL must distinguish suspension from failure and honour `:limit`.
- **Host-platform quirk**: Gleam unit-test discovery is broken on native Windows (path-separator defect in the test framework); test runs execute under WSL while builds remain native.

## Requirements *(mandatory)*

### Functional Requirements

**Compiler + loader**

- **FR-001**: The Gleam instance MUST load `.glp` source through the full pipeline — SRSW check, partial evaluation, type check, compile to the canonical bytecode, load — with a single entry point, such that a file that loads has passed every stage.
- **FR-002**: The parser MUST be a hand-ported recursive-descent parser conforming to the canonical shared grammar used by the Dart/C# instances; no parser-generator target on BEAM is introduced (absorbed spike decision).
- **FR-003**: Partial evaluation MUST preserve the SRSW property (existing proof obligation PI:13 applies to the port), and the type checker MUST implement the approved language-authority ruling on the `ground/1` SRSW relaxation (D6) — no new language semantics may be introduced by the port.

**Bytecode runner / engine**

- **FR-004**: The engine MUST execute the canonical bytecode with three-phase HEAD/GUARD/BODY semantics — tentative head unification, pure guards, body mutations — including suspension on unbound readers and reactivation on writer binding, over the delivered immutable core-terms/heap layer.
- **FR-005**: Reactivation MUST be deduplicated by (goal identity, suspension generation) so that multi-variable suspensions reactivate exactly once per generation.
- **FR-006**: The runner MUST preserve the positional X-register model of the canonical bytecode.
- **FR-007**: Concurrency MUST use plain process spawning and message subjects without depending on the OTP-abstraction package (keeps the lightweight-platform profile viable).

**Standalone REPL**

- **FR-008**: The instance MUST provide a standalone REPL that loads `.glp` files, runs goals, and supports `:trace`, `:limit`, and `:quit`, with outcome reporting (success / suspension / failure, bindings) equivalent to the reference REPLs.
- **FR-009**: The engine MUST be exposed as a typed in-process value, and results MUST flow through the same result-envelope + deep-resolve + output-capture seam in-process as over-the-wire, producing identical envelopes for identical computations (absorbs the envelope and output-capture constituent scopes).

**Shared test corpus + runner**

- **FR-010**: The shared cross-runtime test corpus and its runner MUST be ported to run against the Gleam instance and pass (green) on BEAM.
- **FR-011**: Corpus parity MUST be measured as agreement with the recorded Dart golden outputs, and MUST NOT be declared until the gap/fork cases GAP-G1, GAP-G2, GAP-G3, GAP-G8, and FORK-1 exist as explicit tests and pass.
- **FR-012**: A cross-runtime differential harness MUST be delivered that runs the same case across runtimes and reports any output divergence automatically (closes the known harness gap MISS-04).

**Multi-protocol link layer**

- **FR-013**: The link primitives (`link_send`, `link_recv`, and companions) MUST be ported so a Gleam instance can hold peer-to-peer links, using the shipped byte-for-byte TLV term codec and frame envelope (codec parity with the delivered codec feature).
- **FR-014**: Distributed unification MUST use deferred-local-assignment; variables MUST globalize/localize on `known/1`; link faults MUST surface as data to the program rather than crashing the instance; transports MUST reach full multi-protocol parity with the shipped C#/Dart layer — in-process loopback, TCP, and the QUIC-WS/HTTP3 channel protocol (clarified 2026-07-10: all gating, none deferred).
- **FR-015**: Incoming frames MUST be treated as untrusted input per the owner wire-framing and deserialization-threat decisions (D11/D12): malformed input is rejected safely and reported.

**Cross-runtime distributed tests**

- **FR-016**: Role-parameterized pair programs MUST run split across the shipped C# instance and the Gleam instance over the link layer, and the adversarial-corpus verdicts MUST be identical to single-runtime reference runs across the **full** pair suite — all 16 scenarios (16/16), a hard acceptance gate (clarified 2026-07-10).

**Proof and gate obligations**

- **FR-017**: The two OPEN proof obligations — writer-MGU safety under value-copy semantics (gates M1) and distributed-dereference convergence (gates M2) — MUST be discharged and recorded within this feature before the corresponding milestone is declared. Discharge form (clarified 2026-07-10): a mechanized Lean proof AND a written prose proof in the feature dossier, plus targeted adversarial tests, for each obligation. The quiescence oracle (GAP-G6) MUST exist before distributed acceptance is judged.

### Key Entities

- **GLP source module** (`.glp` file): the unit of loading; passes the five pipeline stages or is rejected with a staged diagnostic.
- **Bytecode program**: the canonical compiled form shared across runtimes; input to the engine.
- **Goal / process**: a running or suspended unit of GLP execution; carries identity and suspension generation.
- **Suspension record**: links an unbound reader to the goals waiting on it; drives reactivation.
- **Term**: the immutable heap value exchanged between goals and, encoded, between instances.
- **Wire frame**: the TLV-encoded, envelope-framed representation of terms/messages on a link; untrusted on receipt.
- **Link**: a peer-to-peer connection between two GLP instances over a transport (loopback, socket).
- **Corpus case + golden output**: a shared test program plus its recorded reference output; the unit of parity measurement.
- **Result envelope**: the uniform result container (outcome, bindings, captured output) used both in-process and over the wire.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every shared-corpus program produces output on the Gleam instance identical to the recorded Dart reference — 100% agreement, zero unexplained divergences.
- **SC-002**: The ported corpus (including GAP-G1/G2/G3/G8 and FORK-1 as explicit cases) runs green on the Gleam instance.
- **SC-003**: A user can load a corpus program and run a goal end-to-end in the standalone REPL, including `:trace` and `:limit`, with outcome reporting equivalent to the reference REPLs.
- **SC-004**: For the codec test corpus, term encodings produced by the Gleam instance are byte-for-byte identical to the shipped reference encodings.
- **SC-005**: The split-pair suite (one role on C#, one on Gleam) passes in full — all 16 pair scenarios (16/16) with verdicts identical to the single-runtime reference, matching the existing Dart↔C# result.
- **SC-008**: Link-layer scenarios pass over each in-scope transport — loopback, TCP, and QUIC-WS/HTTP3 — with identical outcomes per scenario across transports.
- **SC-009**: The full shared corpus run on the Gleam instance completes within 10× the recorded Dart reference wall-clock time (pathological-slowdown sanity bound, not an optimization target).
- **SC-006**: Both OPEN proof obligations (writer-MGU under value-copy; distributed-dereference convergence) are recorded as discharged, and the quiescence oracle exists, before their gated milestones are declared.
- **SC-007**: All existing Dart and C# suites remain green after this feature lands (no regressions to shipped runtimes).

## Assumptions

- **Platform** (confirmed in clarification 2026-07-10): BEAM (Erlang/OTP) is the acceptance platform for this feature. Lightweight-platform (AtomVM / Profile-C) compatibility is preserved by construction (FR-007: no OTP-abstraction dependency; plain spawn), but AtomVM execution is not an acceptance gate here — the Profile-C runtime remains WSL-only as established by feature 049.
- **Toolchain**: Windows-native builds work (`gleam build --target erlang`; OTP 29 / Gleam 1.17 / rebar3 3.27 on user PATH); Gleam unit tests run under WSL due to the known test-framework path-separator defect on Windows.
- **Delivered prerequisites** (in place, relied upon, not re-delivered): Gleam subtree scaffold (specs/033), core terms + immutable heap (specs/034), codeconv Gleam language pair (specs/032), result codec + frame codec (specs/038), result envelope + deep-resolve, structured output-capture seam, monitor-primitive verification (specs/039, M2-0), multi-protocol link layer on C#/Dart (specs/025), HTTP3/QUIC-WS channel link proto.
- **Authoritative decision record**: the 036 baseline-program dossier (decisions D1–D16, obligation sets FB-M1-*/FB-M2-*, outcome-equivalence Thm 3.34 / Rem 3.35) backs this spec; obligations cited here (PI:13, RISK-PROOF-writerMGU, RISK-PROOF-distDeref, GAP-G*, FORK-1, MISS-04, M2-0) are defined there and in specs/030-era contracts.
- **Grammar conformance reference**: the canonical shared grammar file is the parser-conformance reference; conformance is by corpus-driven testing, not by generated code.
- **QUIC groundwork**: QUIC-WS/HTTP3 transport parity on BEAM builds on the existing `gleam_quic` groundwork in this repo (quicer/msquic), delivered with the QUIC-WS channel link proto prerequisite.
- **Roadmap reconciliation**: the six constituent roadmap rows remain `refined` during this feature and are advanced/closed when it ships; the `wave-3-consolidated-full-gleam-chain` ordering row is superseded.
- **Parity definition**: M2 parity means observable outcome/wire parity, not instruction-set identity between runtimes.
