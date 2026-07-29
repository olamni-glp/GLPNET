<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: Wave 4 consolidated — parallel-safe fillers

**Feature Branch**: `062-wave-4-consolidated-parallel-safe-fillers`
**Created**: 2026-07-29
**Status**: Draft
**Input**: User description: "Wave 4 consolidated: parallel-safe fillers — consolidates 11 refined parallel-safe roadmap features with no hard ordering constraints, clearing the refined backlog outside the two engine/gleam spines."

## Overview *(context)*

Wave 4 is a **consolidation wave**: it clears eleven refined, parallel-safe roadmap
items that sit outside the two big spines (the REPL/engine split spine and the full-Gleam
spine). The items are independent — none hard-depends on another within the wave — so they
are delivered as separable slices under one branch and one marathon run. Two of the eleven
are GLP **language** changes and are therefore governed by the language-authority gate
(DISCIPLINE.md §1.14 / CLAUDE.md "Language Authority"); their **implementation is deferred**
behind a written proposal and the operator's express approval, and they are documented here
only so the wave's scope is complete.

The eleven consolidated items (roadmap slugs, by rank):
`research-programme-and-llvm-feasibility`, `compiled-il-on-the-wire-and-factor-out-compiler`,
`multi-accept-transport-extension`, `depgraph-mark-and-recompute`, `depgraph-cross-run-trends`,
`cpp-engine-feasibility`, `multi-client-control-program-in-glp`, `abandon-operation` (§1.14),
`zmq-comm-base`, `many-instances-shared-static-memory-cooperative-scheduling`,
`nested-structure-head-matching` (§1.14).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Depgraph tooling enhancements (Priority: P1)

As a developer running the codeconv conversion toolchain, I want the dependency-graph tool to
support a mark-and-recompute convenience action and cross-run trend reporting, so I can
re-scope a partially-changed codebase and see how graph metrics move across runs without
hand-diffing exports.

**Why this priority**: Highest RICE of the wave (400 / 337), fully self-contained Python
tooling over the existing catalog, lowest risk, and immediately usable. This slice alone is a
viable, shippable MVP.

**Independent Test**: Run the depgraph tool with the new mark-and-recompute subcommand on a
small fixture project and confirm only the marked subgraph is recomputed; run it across two
recorded runs and confirm a deterministic trend report is produced. Verifiable via the
existing codeconv pytest suite with new fixtures.

**Acceptance Scenarios**:

1. **Given** a project with an existing depgraph run, **When** the developer marks a subset of
   files and requests recompute, **Then** only the marked subgraph and its dependents are
   recomputed and the rest is preserved.
2. **Given** two or more recorded depgraph runs, **When** the developer requests a cross-run
   trend report, **Then** a deterministic, secret-redacted report of per-metric deltas is
   produced and is byte-identical on re-run of unchanged inputs.

---

### User Story 2 - Written feasibility studies (Priority: P2)

As the project's technical lead, I want three feasibility questions answered as written,
decision-ready reports — a staged research programme + LLVM feasibility, a C++
engine+scheduler+compiler feasibility, and a many-instances shared-static-memory cooperative-
scheduling feasibility — so that later spine work can be sequenced on evidence rather than
speculation.

**Why this priority**: These are research spikes, not runtime changes; they carry no
regression risk and unblock informed prioritisation. Their deliverable is a document, not
shipped runtime behaviour.

**Independent Test**: Each study is delivered as a reviewable report (recommendation, options
considered, risks, staged plan) under the feature's `research/` area and can be read and
signed off independently of the other slices.

**Acceptance Scenarios**:

1. **Given** the research-programme + LLVM feasibility question, **When** the study is
   delivered, **Then** it states a go/no-go recommendation with a staged plan and explicit
   risks.
2. **Given** the C++ engine feasibility and the many-instances scheduling questions, **When**
   each study is delivered, **Then** each records the options considered and a clear
   recommendation the lead can act on.

---

### User Story 3 - Engine & transport extensions (Priority: P2)

As a runtime engineer, I want the compiled-IL-on-the-wire work (factoring the compiler out so
compiled intermediate form can cross the wire), the multi-accept TCP transport extension, and
the ZMQ base communication primitives, so the distributed runtime has the transport and
compiler seams the later spines depend on.

**Why this priority**: Real runtime capability with tests, but larger and riskier than the
tooling slice; independent of US1/US2 and of each other.

**Independent Test**: Each extension is covered by its own runtime/unit tests that pass under
the existing suites without regressing the baselines (REPL suite; Gleam/C# suites as
applicable).

**Acceptance Scenarios**:

1. **Given** the multi-accept transport extension, **When** more than one client connects to a
   listening endpoint, **Then** each connection is accepted and served without dropping the
   others.
2. **Given** the compiler factored out, **When** a program is compiled to intermediate form on
   one side and sent over the wire, **Then** the receiving side executes it with results equal
   to local execution.
3. **Given** the ZMQ base primitives, **When** a sender and receiver base are wired, **Then**
   a round-trip message is delivered and covered by a test.

---

### User Story 4 - Multi-client control program in GLP (Priority: P3)

As a GLP author, I want a multi-client control program written in GLP that coordinates several
clients, so the runtime's concurrency surface is exercised by a real GLP program (per the
GLP-first principle, DISCIPLINE.md §1.12).

**Why this priority**: Valuable exercise of the runtime but lower rank; depends only on
existing shipped link/transport surface.

**Independent Test**: Load the GLP program in the REPL and run its play; it type-checks,
compiles, and reaches the expected succeeded/suspended outcome, added as a regression case.

**Acceptance Scenarios**:

1. **Given** the control program loaded, **When** its goal is run, **Then** it type-checks and
   runs to the documented outcome for N coordinated clients.

---

### User Story 5 - GLP language items (Priority: P3, GATED — implementation deferred)

As the language owner, I want the two proposed GLP language changes — the abandon operation
(FCP-exact) and nested-structure matching in the HEAD phase — captured with a written §1.14
proposal, so they can be approved or rejected before any implementation touches the language.

**Why this priority**: These change what the language *is*. Per DISCIPLINE.md §1.14 they
cannot be implemented without a written proposal and the operator's express approval. This
wave delivers the **proposal artifacts only**; implementation is explicitly out of scope until
approval is granted.

**Independent Test**: A written §1.14 proposal exists for each item (motivation, exact
semantics, FCP reference for the abandon operation, type-system impact, test plan) and is
presented for approval. No language/runtime code is changed under this wave for these two
items.

**Acceptance Scenarios**:

1. **Given** the abandon-operation proposal, **When** it is delivered, **Then** it states exact
   semantics with the FCP reference and a test plan, and is marked "awaiting §1.14 approval".
2. **Given** the nested-structure-head-matching proposal, **When** it is delivered, **Then** it
   states the exact HEAD-phase matching semantics and type-system impact, and is marked
   "awaiting §1.14 approval".

---

### Edge Cases

- What happens when a depgraph recompute is requested for files not present in any recorded
  run? → The tool reports the unknown paths and recomputes nothing, rather than fabricating
  nodes.
- How does the system handle a cross-run trend request with only one recorded run? → It
  reports that at least two runs are required; it does not emit a degenerate trend.
- What happens if the operator does **not** approve a §1.14 item? → The item stays deferred;
  the wave still closes on the other slices, and the deferred item is recorded as such (it does
  not block the wave).
- How is a feasibility study that concludes "no-go" handled? → A no-go is a valid, complete
  deliverable; the study is done, and any dependent roadmap item is annotated accordingly.
- What happens if an engine/transport extension would regress a baseline suite? → It is not
  merged; the regression is a stop-and-report per the Bug Protocol.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The depgraph tool MUST provide a mark-and-recompute action that recomputes only a
  marked subgraph and its dependents, preserving unmarked results.
- **FR-002**: The depgraph tool MUST produce a deterministic, secret-redacted cross-run trend
  report of per-metric deltas across two or more recorded runs; re-running on unchanged inputs
  MUST be byte-identical.
- **FR-003**: The system MUST deliver a written, decision-ready feasibility study for each of:
  the staged research programme + LLVM feasibility; the C++ engine+scheduler+compiler
  feasibility; and the many-instances shared-static-memory cooperative-scheduling feasibility.
- **FR-004**: The runtime MUST accept and serve more than one concurrent client on a listening
  endpoint (multi-accept transport extension) without dropping existing connections.
- **FR-005**: The compiler MUST be factored so that compiled intermediate form can be produced
  independently of execution and transmitted over the wire, with remote execution results equal
  to local execution.
- **FR-006**: The runtime MUST provide ZMQ base send/receive communication primitives covered
  by a round-trip test.
- **FR-007**: A multi-client control program written in GLP MUST be provided that type-checks,
  compiles, and runs to a documented outcome, added as a regression case.
- **FR-008**: The system MUST deliver a written §1.14 proposal for the abandon operation
  (FCP-exact semantics + test plan) and for nested-structure HEAD-phase matching (exact
  semantics + type-system impact). Implementation of either MUST NOT proceed without the
  operator's recorded approval.
- **FR-009**: Each implemented slice MUST NOT regress the established test baselines (REPL
  suite; codeconv pytest; Gleam/C# suites where touched). A regression is a stop-and-report,
  not a workaround (DISCIPLINE.md §1.2/§1.8).
- **FR-010**: Each slice MUST be independently reviewable and shippable; the wave MUST be able
  to close on the delivered slices even if a §1.14 item remains deferred.
- **FR-011**: Any GLP code delivered MUST include type and procedure declarations and satisfy
  SRSW and the type checker via the REPL pipeline (no separate tools).

### Key Entities *(include if feature involves data)*

- **Depgraph run**: a recorded dependency-graph computation over a project, with per-file nodes,
  edges, and metrics; the unit compared across runs for trends.
- **Feasibility study**: a written report with a recommendation, options considered, risks, and
  (where go) a staged plan; the deliverable for the research slices.
- **§1.14 language proposal**: a written proposal for a language change (motivation, exact
  semantics, references, type-system impact, test plan) in a state of awaiting/approved/rejected.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Both depgraph enhancements are delivered with tests; a mark-and-recompute on a
  fixture recomputes only the marked subgraph, and a cross-run trend report is byte-identical on
  re-run of unchanged inputs.
- **SC-002**: All three feasibility studies are delivered as reviewable reports, each with an
  explicit go/no-go recommendation and named risks.
- **SC-003**: The multi-accept endpoint serves at least two concurrent clients with zero dropped
  connections in the acceptance test.
- **SC-004**: Compiled-IL-on-the-wire produces remote execution results identical to local
  execution on the acceptance program.
- **SC-005**: The GLP multi-client control program runs to its documented outcome and is a
  passing regression case.
- **SC-006**: No delivered slice regresses any established test baseline (all pre-change suites
  remain green).
- **SC-007**: A written §1.14 proposal exists for each of the two language items; zero language
  or runtime code for those two items is changed without a recorded operator approval.
- **SC-008**: The wave closes with every item in a terminal state — delivered, delivered-as-study,
  or explicitly deferred (for a §1.14 item) — with no item silently dropped.

## Assumptions

- Feasibility items (research-programme/LLVM, C++ engine, many-instances scheduling) are
  delivered as **written studies/ADRs**, not full implementations — they are feasibility
  questions by nature. Full builds, if any, are follow-on roadmap features.
- The two §1.14 items are **proposal-only** in this wave; their implementation is out of scope
  until the operator approves the written proposals. A non-approval defers the item without
  blocking wave close.
- "Parallel-safe" holds within the wave: the eleven items have no hard ordering constraints on
  each other, so slices proceed independently and in any order.
- Existing shipped transport/link surface (from prior waves) is reused for the engine/transport
  slices and the GLP control program; this wave does not re-implement it.
- Delivery follows buildkit GitFlow (feature `062-…` → develop → release/* → main); release cuts
  are coordinated through the fleet lead (ariellas) to avoid same-day CalVer `.N` collisions.
- Wave-5 is roadmap-recorded as depending on Wave-4 output; that dependency stays recorded and is
  coordinated on the shared scheduler board.

### Open clarifications (to resolve in `/bk-clarify`)

- [NEEDS CLARIFICATION: Should the compiled-IL-on-the-wire + factor-out-compiler slice (US3) be
  delivered as a working runtime capability in this wave, or reduced to a feasibility/spike
  deliverable like US2? It is the largest and riskiest engine item and may not fit a "filler"
  wave.]
- [NEEDS CLARIFICATION: For the two §1.14 items, is the desired Wave-4 deliverable the written
  proposals only (assumed here), or does the operator intend to approve-and-implement within this
  wave? This determines whether US5 stays gated/deferred or becomes in-scope implementation.]
