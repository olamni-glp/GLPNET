<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: Wave 3 consolidated — Full Gleam chain

**Feature Branch**: `060-wave3-full-gleam-chain`  
**Created**: 2026-07-27  
**Status**: Draft  
**Input**: User description: "Wave 3 consolidated: Full Gleam chain — complete the Gleam GLP runtime through cross-runtime tests. Consolidates, in order: antlr4-shared-grammar-spike, glp-gleam-compiler-and-loader, glp-gleam-bytecode-runner, glp-gleam-repl, glp-test-corpus-port-and-runner, glp-gleam-link-layer, cross-runtime-csharp-gleam-distributed-tests. Value: a standalone Gleam GLP instance plus the C#<->Gleam distributed test payoff. Effort: large. Risks: AtomVM toolchain; the shared-grammar spike feeds the compiler."

## Overview

Today a GLP author has exactly one complete place to run a program: the reference runtime (with a C# engine emerging alongside). The Gleam runtime exists in pieces — terms, heap, unification, a partial compiler front end, a bytecode instruction set, and a link seam over loopback and TCP — but nobody can take a `.glp` file and *run* it there, let alone have two runtimes talk to each other.

Wave 3 closes that gap. It consolidates seven previously-refined roadmap features into one delivery: a Gleam GLP instance a person can load a program into, run, inspect interactively, exercise against the shared conformance corpus, connect to a second instance, and finally point at a C# instance so the two independent implementations prove they speak the same language over the wire.

The consolidated roadmap items, in dependency order:

| # | Roadmap item | Role in this wave |
|---|---|---|
| 1 | `antlr4-shared-grammar-spike` | Single source of truth for GLP surface syntax across runtimes |
| 2 | `glp-gleam-compiler-and-loader` | Source → bytecode, plus module load and link |
| 3 | `glp-gleam-bytecode-runner` | Execute the bytecode under three-phase HEAD/GUARD/BODY semantics |
| 4 | `glp-gleam-repl` | Interactive standalone instance |
| 5 | `glp-test-corpus-port-and-runner` | Shared conformance corpus runs on Gleam |
| 6 | `glp-gleam-link-layer` | Gleam instance ↔ Gleam instance over the multi-protocol link |
| 7 | `cross-runtime-csharp-gleam-distributed-tests` | C# instance ↔ Gleam instance, proven by tests |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run a GLP program on the Gleam runtime (Priority: P1)

A GLP author points the Gleam runtime at a `.glp` source file and runs a goal. The program is parsed, type-checked, compiled, loaded, and executed, and the author sees the same answer they would get from the reference runtime — success, failure, or suspension.

**Why this priority**: Nothing else in the wave has meaning without it. A runtime that cannot run a program is not a runtime, and every later story consumes this one. On its own it already delivers a usable second implementation of GLP.

**Independent Test**: Take a representative set of programs from the shared corpus, run each on the reference runtime and on the Gleam runtime, and compare outcomes. Delivers value the moment a non-trivial program agrees on both.

**Acceptance Scenarios**:

1. **Given** a valid GLP source file with type and procedure declarations, **When** the author loads it into the Gleam runtime and poses a goal that should succeed, **Then** the goal succeeds and any bound results match the reference runtime's results.
2. **Given** a source file whose goal suspends on an unbound reader, **When** the author runs it, **Then** the Gleam runtime reports suspension rather than success or failure, matching the reference runtime.
3. **Given** a source file that violates the single-reader/single-writer rule, **When** the author loads it, **Then** the load is rejected with a diagnostic naming the offending variable and clause — and the runtime does not crash.
4. **Given** a program that spans multiple modules, **When** the author loads the entry module, **Then** referenced modules are located and linked, and calls across module boundaries resolve.
5. **Given** a malformed source file (bad arity, unknown functor, syntax error), **When** the author loads it, **Then** the runtime returns a structured load error and remains usable for the next load.

---

### User Story 2 - Work interactively in a standalone Gleam instance (Priority: P2)

A GLP author starts a Gleam GLP instance, loads files, poses goals, turns tracing on, bounds a runaway computation, and inspects what happened — without leaving the instance and without any other runtime present.

**Why this priority**: This is the "standalone Gleam GLP instance" the wave promises, and it is how every subsequent story gets exercised by hand. It depends only on P1.

**Independent Test**: Drive a scripted interactive session end to end — load, goal, trace, bound, quit — and check the transcript against the same session driven on the reference runtime.

**Acceptance Scenarios**:

1. **Given** a running Gleam instance, **When** the author loads a file and poses a goal, **Then** the result is displayed and the instance stays ready for the next command.
2. **Given** a running instance, **When** the author enables tracing and re-runs a goal, **Then** the execution steps are reported in an order consistent with the reference runtime's trace.
3. **Given** a goal that would not terminate, **When** the author sets a step bound and runs it, **Then** execution stops at the bound and reports that it was bounded, not that it failed.
4. **Given** a session where a load failed, **When** the author corrects the file and loads again, **Then** the new definitions replace the old and stale definitions do not leak into later goals.

---

### User Story 3 - Prove conformance against the shared corpus (Priority: P2)

A maintainer runs the shared GLP conformance corpus against the Gleam runtime and gets a per-case verdict, plus a summary of where Gleam agrees with and diverges from the reference runtime.

**Why this priority**: Parity is the wave's only credible claim of correctness, and it is what makes the later distributed work trustworthy. Equal in priority to story 2 because a REPL without conformance evidence is a demo, not a runtime.

**Independent Test**: Run the corpus runner and confirm it produces a verdict for every case with no case silently skipped, and that the divergence list is explicit.

**Acceptance Scenarios**:

1. **Given** the shared corpus, **When** the maintainer runs it against the Gleam runtime, **Then** every case reports pass, fail, or explicitly-declared-out-of-scope — never absent.
2. **Given** a case that diverges between runtimes, **When** the run completes, **Then** the report names the case, the expected outcome, and the observed outcome.
3. **Given** a corpus run, **When** it finishes, **Then** the pass count, fail count, and out-of-scope count are reported together, and the same corpus re-run without code changes produces the same counts.

---

### User Story 4 - Connect two Gleam instances (Priority: P3)

An operator starts two Gleam GLP instances, joins them over the link layer, and passes messages between programs running on each, with the connection surviving ordinary message volume and failing cleanly when the peer goes away.

**Why this priority**: This is the step from "a runtime" to "a distributed runtime", and it is the prerequisite for the cross-runtime payoff. It depends on story 1 and is best exercised through story 2.

**Independent Test**: Bring up two instances on one machine, join them, exchange messages in both directions, then kill one and confirm the survivor reports the loss rather than hanging.

**Acceptance Scenarios**:

1. **Given** two running instances, **When** one initiates a link to the other, **Then** the link is established and both sides report the peer as connected.
2. **Given** an established link, **When** a program on one instance sends to a program on the other, **Then** the message is delivered intact and in order.
3. **Given** an established link, **When** the peer process terminates, **Then** the survivor observes the disconnection within a bounded time and reports it, rather than blocking indefinitely.
4. **Given** an inbound connection from a peer that fails capability negotiation, **When** the link is attempted, **Then** it is refused with a stated reason and the instance stays available for other peers.

---

### User Story 5 - Prove C# and Gleam interoperate (Priority: P3)

A maintainer runs a distributed test suite in which a C# GLP instance and a Gleam GLP instance exchange work over the link, demonstrating that the two independent implementations agree on the wire format and on program semantics.

**Why this priority**: This is the wave's headline payoff — two independently-written runtimes proven interoperable — but it is last because it consumes every other story.

**Independent Test**: Run the cross-runtime suite with one instance of each runtime and confirm each scenario passes in both directions (C#-initiates and Gleam-initiates).

**Acceptance Scenarios**:

1. **Given** a C# instance and a Gleam instance, **When** either initiates a link to the other, **Then** the link is established and both report the peer as connected.
2. **Given** an established cross-runtime link, **When** a term is sent from one runtime to the other and echoed back, **Then** the round-tripped term is identical to the original, including nested structures and unbound variables.
3. **Given** a distributed scenario from the suite, **When** it runs with the roles swapped between runtimes, **Then** the outcome is the same in both directions.
4. **Given** a version or capability mismatch between the two instances, **When** they attempt to link, **Then** the mismatch is reported explicitly rather than producing a silent misinterpretation of the stream.

### Edge Cases

- A program loads on the reference runtime but is rejected by the Gleam runtime, or vice versa — the divergence must be reported as a conformance failure, not papered over.
- A corpus case neither succeeds nor fails but suspends — suspension is a first-class outcome and must be compared as such.
- A link is established but the two sides disagree about the wire-format version — the link must be refused with a stated reason.
- A message arrives for a program that has already terminated — the sender must learn the delivery failed rather than waiting forever.
- The same module is loaded twice, or two modules define the same procedure — the resolution rule must be stated and deterministic.
- The interactive instance is asked to run a goal while a previous goal is still suspended — the prior state must not be silently discarded.
- The transport is slow or unreliable — message ordering must still hold, and a partially-received message must never be delivered as if complete.

## Requirements *(mandatory)*

### Functional Requirements

**Surface syntax and front end**

- **FR-001**: The GLP surface syntax MUST have a single authoritative definition shared across runtimes, such that a syntax change is made in one place rather than re-implemented per runtime. [NEEDS CLARIFICATION: the ANTLR4 shared-grammar approach was recorded as superseded during feature 059 — is a shared grammar artifact still in scope for this wave, or is per-runtime hand-written parsing the accepted answer?]
- **FR-002**: The Gleam runtime MUST accept every source construct the reference runtime accepts, and reject every construct the reference runtime rejects, across the agreed conformance corpus.
- **FR-003**: A rejected source file MUST produce a structured diagnostic identifying the file, the clause, and the reason; the runtime MUST remain usable afterwards and MUST NOT terminate the process.

**Compilation, loading and execution**

- **FR-004**: The Gleam runtime MUST compile GLP source to the shared bytecode representation and execute it, preserving the three-phase HEAD / GUARD / BODY execution model.
- **FR-005**: The runtime MUST enforce the single-reader/single-writer rule at load time, rejecting violating clauses.
- **FR-006**: The runtime MUST implement three-valued unification — success, suspension, failure — and MUST report suspension distinctly from failure.
- **FR-007**: The runtime MUST bind writers only, never readers, and MUST never bind a writer to a writer.
- **FR-008**: The runtime MUST resolve references across module boundaries, including modules loaded after the referring module.
- **FR-009**: The runtime MUST support both static module linking and dynamic dispatch of module-qualified calls.
- **FR-010**: Given identical source and goal, the Gleam runtime MUST produce the same outcome classification (success / failure / suspension) and the same result bindings as the reference runtime, for every in-scope corpus case.

**Interactive instance**

- **FR-011**: A person MUST be able to start a standalone Gleam GLP instance, load source files, and pose goals interactively.
- **FR-012**: The instance MUST provide execution tracing that can be turned on and off within a session.
- **FR-013**: The instance MUST allow a bound on execution steps, and MUST report a bounded run as bounded rather than as failure.
- **FR-014**: The instance MUST allow inspecting the compiled form of a loaded procedure.
- **FR-015**: Re-loading a file MUST replace its previous definitions; stale definitions MUST NOT remain reachable.

**Conformance corpus**

- **FR-016**: The shared conformance corpus MUST be runnable against the Gleam runtime by a single command.
- **FR-017**: The corpus runner MUST emit a per-case verdict of pass, fail, or explicitly-declared out-of-scope, with no case silently omitted.
- **FR-018**: Any case declared out of scope MUST carry a recorded reason.
- **FR-019**: The corpus runner MUST report aggregate pass / fail / out-of-scope counts, and repeated runs over unchanged code MUST produce identical counts.

**Link layer**

- **FR-020**: Two Gleam instances MUST be able to establish a link and exchange messages in both directions.
- **FR-021**: Messages MUST be delivered intact and in the order sent, per link.
- **FR-022**: A link MUST negotiate capabilities and wire-format version before carrying program traffic, and MUST refuse with a stated reason on mismatch.
- **FR-023**: An instance MUST accept inbound link attempts, not merely initiate outbound ones.
- **FR-024**: Loss of a peer MUST be observed within a bounded time and surfaced to programs holding references across that link, rather than blocking indefinitely.
- **FR-025**: The link MUST support the transports agreed for this wave. [NEEDS CLARIFICATION: which transports are required for wave-3 acceptance — loopback and TCP only, or must QUIC/WebSocket and ZMQ also be proven?]

**Cross-runtime interoperation**

- **FR-026**: A C# instance and a Gleam instance MUST be able to link to one another, with either side initiating.
- **FR-027**: A term sent between the two runtimes and echoed back MUST be identical to the original, including nested structures, lists, and unbound variables.
- **FR-028**: Every scenario in the cross-runtime suite MUST be exercised in both directions (each runtime as initiator).
- **FR-029**: A capability or version mismatch between runtimes MUST be reported explicitly and MUST NOT result in silent misinterpretation of the stream.
- **FR-030**: The cross-runtime suite MUST be runnable as part of the project's regular test invocation, and its results MUST be reported alongside the other suites.

**Deployment target**

- **FR-031**: The Gleam runtime MUST run on the agreed execution target(s). [NEEDS CLARIFICATION: is running on the embedded AtomVM target required for wave-3 acceptance, or is the full BEAM sufficient for this wave with AtomVM deferred?]

### Key Entities

- **GLP source module**: A named unit of GLP text containing type declarations, procedure declarations, and clauses; may reference other modules.
- **Compiled program**: The loaded, executable form of one or more modules, addressable by procedure name and arity.
- **Goal**: A query posed against a compiled program; resolves to success (with bindings), failure, or suspension.
- **Runtime instance**: One running GLP engine — reference, C#, or Gleam — capable of loading programs, running goals, and holding links.
- **Link**: An established, capability-negotiated connection between two runtime instances, carrying ordered messages.
- **Conformance case**: One corpus entry pairing a source program and goal with its expected outcome across runtimes.
- **Conformance report**: The per-case verdicts and aggregate counts produced by one corpus run.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A GLP author can take an unmodified program from the shared corpus, run it on the Gleam instance, and get the same outcome as the reference runtime — for at least 95% of in-scope corpus cases, with every remaining case individually named and explained.
- **SC-002**: Zero corpus cases are silently skipped: pass + fail + declared-out-of-scope equals the total case count on every run.
- **SC-003**: A new user can start a Gleam instance, load a program, and get an answer to a goal within 5 minutes of first contact, using only the documented commands.
- **SC-004**: Two Gleam instances establish a link and complete a round-trip message exchange on the first attempt, with no manual intervention between start-up and first message.
- **SC-005**: A C# instance and a Gleam instance complete every scenario in the cross-runtime suite in both directions, with 100% of scenarios passing.
- **SC-006**: A term round-tripped between runtimes is identical to the original in 100% of suite cases, including nested and unbound cases.
- **SC-007**: When a peer disappears, the surviving instance reports the loss within 30 seconds; no scenario leaves an instance blocked indefinitely.
- **SC-008**: Repeated runs of the full conformance corpus over unchanged code produce identical verdicts — no flaky cases.
- **SC-009**: The existing reference-runtime test suites remain fully green throughout the wave; this feature adds capability without regressing what already works.

## Assumptions

- The reference runtime remains the arbiter of correct GLP behaviour for the duration of this wave; where Gleam and the reference disagree and the spec is silent, the reference wins until the disagreement is escalated.
- The shared conformance corpus already exists in a form usable by more than one runtime; this wave ports the *runner*, not the corpus semantics.
- The bytecode instruction set is stable and shared; this wave consumes it rather than redefining it. Any instruction-set change requires separate approval under the language-authority rule.
- The wire format used between instances is the one already established for the C# runtime; Gleam conforms to it rather than proposing a variant.
- Feature 059 delivered the Gleam terms, heap, unification, partial compiler front end, bytecode instruction set, and the link and transport seams over loopback and TCP; this wave builds on that base rather than restarting it.
- Several sub-capabilities were recorded as ABSENT or PARTIAL during 059 verification — module static linking and dynamic dispatch, the bytecode lint, the interactive boot and bytecode commands, the inbound pump, link acceptance, the capability gate, and instance network join. This wave is scoped to close them.
- "Standalone" means the Gleam instance requires no other runtime present to load and run a program; it does not imply the instance is packaged for distribution.
- Performance is not a wave-3 acceptance criterion beyond not hanging; parity of *outcome* is what is being proven, not parity of speed.
- The wave depends on wave 2 (`wave-2-consolidated-repl-engine-split-spine`) per the roadmap; if wave 2 has not landed, the engine-split-dependent portions of stories 4 and 5 may need re-sequencing.

## Dependencies

- **Upstream roadmap**: `wave-2-consolidated-repl-engine-split-spine` (recorded blocker), plus the delivered `glp-gleam-core-terms-and-heap`, `glp-gleam-subtree-scaffold`, `result-codec-and-framecodec-ride`, `multi-protocol-link-layer`, and `m2-0-verify-erlang-monitor-atomvm`.
- **Toolchain**: the Gleam/BEAM toolchain, and — if FR-031 resolves to requiring it — a working AtomVM build. AtomVM toolchain instability is the wave's primary recorded risk.
- **Cross-runtime**: a runnable C# GLP instance is required for story 5; without it, stories 1–4 still stand alone.
