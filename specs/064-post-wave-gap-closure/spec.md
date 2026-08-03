<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: Post-wave consolidation — verified gap closure (REPL/engine + Full-Gleam)

**Feature Branch**: `064-post-wave-gap-closure`
**Created**: 2026-08-03
**Status**: Draft
**Input**: User description: "Close the residual gaps confirmed by 3rtask run 20260803T133715Z-20ac (curator report at .specify/3rtask/runs/20260803T133715Z-20ac/curator_report.md) against develop @ 14c28169: Gleam link tail; 059 close-out block; C# serve path; IL-on-the-wire completion; small residuals. Spike follow-ons explicitly OUT. Any new GLP language surface is Section-1.14 gated (propose-only)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Gleam link reaches functional parity with the C# link (Priority: P1)

A distributed-GLP developer running two Gleam instances (or a Gleam×C# pair) needs the Gleam link to support the same distributed semantics the C# link already has: non-ground terms unify across the link (distributed unification), a quiescence oracle reports when a distributed computation has settled, a listener accepts more than one concurrent link, and the QUIC-WS transport leaf exists so Gleam peers can join QUIC meshes. Today the Gleam link is ground-relay only over loopback/TCP/ZMQ with one link per listen (050 T050–T058 unchecked).

**Why this priority**: This is the largest verified semantic gap; every other distributed-Gleam scenario (multiagent plays, cross-runtime meshes) is capped by it, and it blocks the 059 umbrella's acceptance sweep.

**Independent Test**: Run the existing cross-runtime and Gleam link scenario suites extended with non-ground-term, quiescence, multi-link, and QUIC-WS cases; each new case passes on the Gleam side with results equivalent to the C# reference.

**Acceptance Scenarios**:

1. **Given** two linked Gleam instances, **When** a goal on instance A shares a non-ground variable with a goal on instance B and B binds it, **Then** A observes the binding and the computation completes with the same result as the single-instance run.
2. **Given** a distributed computation across two linked instances, **When** all goals have succeeded or suspended with no in-flight messages, **Then** the quiescence oracle reports quiescent, and never reports quiescent while a message is in flight.
3. **Given** one Gleam listener, **When** two peers dial it concurrently, **Then** both links establish and exchange terms without either being dropped.
4. **Given** a Gleam peer with the QUIC-WS leaf configured, **When** it dials a C# QUIC-WS endpoint, **Then** the link establishes and the standard scenario set passes both directions.

---

### User Story 2 - C# engine host serves multiple real clients through the GLP control program (Priority: P2)

An operator running the split REPL/engine stack connects several thin clients to one engine host. The host's transport accepts clients continuously (multi-accept loop in the TCP transport), and the shipped multi-client GLP control program (regression block A31) is wired to real client channels — commands from any client are merged, dispatched, and answered, instead of the current loud refusal of the second client.

**Why this priority**: The delivered multi-accept helper and the delivered GLP control program exist but are not joined; this story turns two shipped artifacts into the user-facing capability the roadmap intended.

**Independent Test**: Start one engine host, connect three clients, drive interleaved goals from all three; every client receives its own results and the engine-host suite plus REPL suite stay green.

**Acceptance Scenarios**:

1. **Given** a running engine host, **When** a second and third client connect, **Then** each is accepted and served (no refusal), and disconnecting one client does not disturb the others.
2. **Given** three connected clients issuing goals concurrently, **When** the GLP control program merges their command streams, **Then** each reply reaches only the issuing client and the merged ordering is a legal interleaving.

---

### User Story 3 - Thin client ships compiled IL; the compiler leaves the engine path (Priority: P3)

A REPL user on the thin client loads a program; the client compiles it and sends compiled IL over the split protocol (a dedicated IL request kind), and the engine executes IL without ever seeing source text. The delivered IL codec/envelope (062) becomes the actual REPL transport payload, completing the factor-out that the split protocol still lacks (LOAD_SOURCE/RUN_GOAL text-only today).

**Why this priority**: Completes the REPL/engine separation programme's stated architecture; depends on nothing in P1 and can ride the existing engine-host suites.

**Independent Test**: Load and run the standard typed test programs through the IL request kind; results are byte-identical to the text path; the engine host builds and runs with no compiler reference in the execute path.

**Acceptance Scenarios**:

1. **Given** the thin client with a program file, **When** it loads via the IL request kind, **Then** the engine executes the shipped IL and the goal results equal the text-path results for the whole regression corpus.
2. **Given** a malformed or version-skewed IL envelope, **When** the engine receives it, **Then** it refuses loudly with the recorded error taxonomy and stays serving.

---

### User Story 4 - 059 umbrella close-out: acceptance sweep and process-split builds (Priority: P4)

The Full-Gleam programme owner completes the 059 umbrella's open close-out block: the FE/BE process split builds, the embeddability build (G3-A), the QUIC gate, the full-scope regression accept (T094), and the SC-sweep/discharge tasks — so the umbrella feature can be verified DONE against its own spec rather than remaining a 59/98 partial.

**Why this priority**: Mostly verification and build wiring over work delivered by P1–P3; it is the umbrella's bookkeeping-with-teeth and lands last.

**Independent Test**: 059's tasks.md acceptance tasks check off against recorded evidence; the full-scope regression accept runs green across Dart/C#/Gleam suites.

**Acceptance Scenarios**:

1. **Given** P1–P3 complete, **When** the 059 acceptance sweep runs, **Then** every SC row has recorded evidence and the umbrella's open tasks reduce to zero or to explicitly recorded deferrals.

---

### User Story 5 - Small residuals closed (Priority: P5)

A REPL user gets the `:boot` command on the Gleam REPL (G9 deferral), the bytecode-lint placeholder becomes a working check, and the recorded `param_arity` panic is fixed — three small, independently testable residuals from the verified inventory.

**Why this priority**: Low-risk cleanups; valuable but not blocking.

**Independent Test**: Each residual has its own test: a `:boot` scenario play, a lint invocation on a known-bad program, a regression case for the panic input.

**Acceptance Scenarios**:

1. **Given** the Gleam REPL, **When** `:boot` runs a multi-isolate play, **Then** the play completes with the documented outcome.
2. **Given** a program triggering the former `param_arity` panic, **When** it loads, **Then** a proper error is reported and the REPL survives.

### Edge Cases

- Distributed unification when both instances bind the same variable concurrently — the writer-MGU rule must hold across the link (never bind writer to writer; a well-formed program cannot race, a malformed one must fail loudly, not corrupt).
- Quiescence oracle vs. link failure: a dropped peer must surface as a fault-lattice event, never as a false quiescent verdict.
- Multi-accept under peer half-close at establishment: the D-9 ruling and `{exit_on_close, false}` requirement apply to every new BEAM socket path.
- IL request kind version skew between an old client and a new engine (and vice versa): explicit refusal with the envelope's version taxonomy, never silent fallback to text.
- A client crashing mid-goal in the multi-client host: its pending replies are discarded without wedging the control program's merge loop.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Gleam link MUST support distributed unification of non-ground terms with semantics identical to the C# link (writer-MGU preserved across the link).
- **FR-002**: The Gleam link MUST provide a quiescence oracle that reports quiescent exactly when no goal can advance and no message is in flight, and integrates with the existing fault lattice.
- **FR-003**: The Gleam link MUST accept multiple concurrent inbound links on one listener (multi-accept) with none dropped.
- **FR-004**: The Gleam link MUST provide a QUIC-WS transport leaf interoperable with the existing C# QUIC-WS endpoint.
- **FR-005**: The C# TCP transport MUST accept clients continuously (multi-accept loop), and the engine host MUST serve multiple concurrent clients through the shipped GLP control program, with per-client reply routing.
- **FR-006**: The split protocol MUST define an IL request kind carrying the existing CompiledIlEnvelope; the thin client MUST compile locally and ship IL; the engine execute path MUST NOT reference the compiler.
- **FR-007**: IL-path results MUST be equivalent to text-path results over the full regression corpus; malformed/skewed envelopes MUST be refused with the recorded error taxonomy.
- **FR-008**: The 059 umbrella's open acceptance tasks MUST be discharged with recorded evidence or explicit recorded deferrals (FE/BE split builds, embeddability build, QUIC gate, T094 regression accept, SC-sweep).
- **FR-009**: The Gleam REPL MUST support `:boot` for multi-isolate plays; bytecode-lint MUST perform its documented checks; the `param_arity` panic MUST become a reported error with a regression test.
- **FR-010**: All existing suites (REPL, Dart, C#, Gleam, parity corpus, cross-runtime Section I) MUST remain green at every checkpoint; zero regression is a hard gate.
- **FR-011**: Any change requiring new GLP language surface (guards, kernels, directives, types) MUST be raised as a Section-1.14 proposal and implemented only after explicit engineer approval; absent approval, the affected sub-scope is delivered host-side or recorded as a gated deferral.
- **FR-012**: Every new BEAM TCP/socket path MUST set `{exit_on_close, false}` and honor the D-9 run-termination barrier and dial-retry norms.

### Key Entities

- **Distributed binding**: a variable binding whose writer and reader live on different instances; carries origin, target, and the term payload.
- **Quiescence verdict**: a per-computation state (active | quiescent | faulted) derived from goal states and in-flight message counts.
- **IL request**: a split-protocol message kind wrapping a CompiledIlEnvelope (il_version, digest, source metadata, IL body).
- **Client session**: a per-client channel pair in the multi-client host, joined to the control program's merged command stream.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The extended Gleam link scenario suite (non-ground unification, quiescence, multi-link, QUIC-WS) passes 100% on the Gleam side with results equivalent to the C# reference, and the cross-runtime Section I suite stays green ×10 consecutive loops.
- **SC-002**: Three concurrent clients complete interleaved goal batches against one engine host with zero refusals, zero cross-delivered replies, and zero regression in the engine-host and REPL suites.
- **SC-003**: The full regression corpus produces identical results via the IL request kind and the text path (100% agreement), and the engine executes IL with no compiler reference on the execute path.
- **SC-004**: 059's open acceptance tasks reduce to zero or recorded deferrals, with the full-scope regression accept recorded green across all three runtimes.
- **SC-005**: The three residuals each close with a dedicated passing test; total suite counts increase and none decrease.
- **SC-006**: Zero regression at every checkpoint across all suites (REPL, Dart unit, C# suites, Gleam suite, parity corpus 206/206, cross-runtime 18/18).

## Assumptions

- The 3rtask curator report (run 20260803T133715Z-20ac) is the authoritative gap inventory; verified-delivered items are not re-opened here.
- The three decision-gated spike follow-ons (C++ executor spike, ORCv2 accelerator spike, many-instances BEAM experiment) are OUT of scope — separate engineer-gated roadmap items.
- Distributed unification and the quiescence oracle can be implemented against the existing frozen link primitives and FCP reference semantics without new GLP language surface; if that assumption fails, FR-011's Section-1.14 gate applies (propose-only).
- The C# link remains the reference implementation for link semantics; the Dart runtime remains the execution oracle for corpus parity.
- 050/059/060 spec task lists remain the authoritative enumeration of the open tail; this feature discharges them rather than re-specifying them.
- Wave numbering continues under the 2026-07 consolidated-waves epic; this is the post-wave consolidation feature (064).
