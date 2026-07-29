<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: Wave 5 consolidated: captured triad

**Feature Branch**: `063-wave-5-consolidated-captured-triad`
**Created**: 2026-07-29
**Status**: Draft
**Input**: User description: "Wave 5 consolidated: captured triad — consolidates three captured features for delivery: (1) HTTP3/QUIC+WS link completion (live glp_repl bridge, mesh fix, build+re-verify), (2) durable mesh messaging protocol (signal-then-fetch, WAL/PGLite tiering), (3) formal 3-role agent-team orchestration (planning + execution triads). Advance the consolidated features shipped/closed at wave close. Wave-5 is roadmap-recorded blocked-by wave-4 (operator directed parallel run): sequence wave-4-dependent material LAST and flag hard collisions on the scheduler board."

## Overview

Wave 5 empties the captured backlog by delivering three features as one
consolidated wave: the HTTP/3 (QUIC) + WebSocket channel-link is **completed**
from its proven prototype (feature 036) into the live GLP REPL, with its
recorded mesh defect fixed and the whole stack rebuilt and re-verified; a
**durable mesh messaging protocol** gives peers on that mesh store-and-forward
delivery that survives disconnects and restarts (signal-then-fetch with a
durable local tier); and the **3-role agent-team orchestration** model
(planner / builder / critic triads for planning and execution) is delivered as
a formal, usable capability. At wave close, the three consolidated roadmap
features are advanced shipped/closed so the roadmap reflects delivery.

The wave runs in parallel with wave 4 by operator direction even though the
roadmap records wave-5 blocked-by wave-4: any wave-5 material that depends on
wave-4 output is sequenced LAST, and a hard collision is flagged on the shared
scheduler board — never worked around.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Complete the QUIC+WS link into the live REPL (Priority: P1)

An operator starts two (or more) live GLP REPL instances on LAN hosts and
links them over the genuine HTTP/3 (QUIC) + WebSocket channel directly from
the REPL — no separate prototype tool. GLP programs running in those REPLs
exchange terms over the link exactly as they do over the existing TCP link.
The previously-recorded mesh defect (multi-peer duplex mesh) is fixed: three
or more instances form a peer-to-peer mesh and every peer-pair exchanges
messages. The stack builds clean from the current tree, and the prototype's
demo scenarios re-verify green against the completed implementation.

**Why this priority**: This is the wave's largest user-visible payoff and the
direct continuation of the shipped link layer — the prototype proved the
transport; completion makes it a usable capability of the product.

**Independent Test**: From two clean checkouts on two hosts (or two processes
on one host), start the live REPL on each, establish a QUIC+WS link between
them by running a GLP goal, exchange a stream of ground terms both ways, then
repeat with three instances in a mesh — all scenarios pass without any
prototype-only tooling.

**Acceptance Scenarios**:

1. **Given** two live REPL instances, **When** one runs a listener goal and the
   other a connector goal naming the QUIC scheme, **Then** a genuine QUIC+WS
   link is established and ground terms flow both directions, byte-identical.
2. **Given** three or more live instances, **When** they form the duplex mesh,
   **Then** every peer-pair exchanges messages (the recorded mesh defect no
   longer reproduces).
3. **Given** the current source tree, **When** the full build and the
   prototype's demo scenarios are re-run, **Then** the build is clean and every
   re-verified scenario reports its outcome explicitly (pass, or a named,
   reasoned failure — never silence).

---

### User Story 2 - Durable mesh messaging that survives disconnects (Priority: P2)

A GLP program on one mesh peer sends a message to a peer that is currently
offline (or becomes offline mid-delivery). The sender's node stores the
message durably, signals availability when the peer reappears, and the peer
fetches what it missed (signal-then-fetch). A node restart loses no accepted
message: the durable tier (write-ahead journal over the node's local store)
replays undelivered messages. Delivery is at-least-once with duplicate
suppression at the receiver, so a program observes each message once.

**Why this priority**: The live mesh (US1) is connection-oriented — a dropped
peer loses in-flight traffic. Durable delivery is what turns the mesh into a
messaging fabric programs can rely on.

**Independent Test**: On a two-peer mesh, take the receiver offline, send N
messages, restart the SENDER process, bring the receiver back, and observe all
N messages arrive exactly once at the program level, in order per sender.

**Acceptance Scenarios**:

1. **Given** an offline peer, **When** a program sends messages to it, **Then**
   the messages are accepted and durably stored, and the sender's program is
   not blocked indefinitely.
2. **Given** stored undelivered messages and a sender restart, **When** the
   receiver reappears, **Then** it is signalled, fetches the backlog, and every
   accepted message is delivered — none lost, none delivered twice to the
   program.
3. **Given** a receiver that was offline during sends, **When** it fetches,
   **Then** per-sender order is preserved.

---

### User Story 3 - Formal 3-role agent-team orchestration (Priority: P3)

An engineer running substantial planning or execution work convenes a formal
3-role team — a planner who drafts the method, an independent critic who
red-teams it, and builders who execute disjoint slices — with the roles,
hand-offs, convergence loop, and evidence rules stated as a written, reusable
protocol rather than ad-hoc practice. The engineer invokes the orchestration
for a concrete piece of glpnet work and receives an attributed, convergent
result with the deciding engineer in the loop at every gate.

**Why this priority**: Valuable process capability, but it does not gate the
mesh deliverables and carries the widest scope uncertainty
[NEEDS CLARIFICATION: is this the adoption/operationalization of the existing
buildkit 3-role task-team capability for glpnet work, or a NEW GLP-native
implementation (agent triads written in GLP running on the multi-agent
runtime)? The two readings differ by an order of magnitude in scope].

**Independent Test**: Run one planning engagement and one execution engagement
through the documented protocol on real wave-5 work items; verify each
produced an attributed plan/result, a recorded critic pass, and an explicit
engineer decision at each gate.

**Acceptance Scenarios**:

1. **Given** a work item and the documented protocol, **When** the planning
   triad runs, **Then** the output is a method the critic has adversarially
   reviewed and the engineer has explicitly approved.
2. **Given** an approved method, **When** the execution triad runs, **Then**
   builders work disjoint slices, the critic merges claims mechanically, and
   conflicts escalate to the engineer rather than being resolved silently.

---

### User Story 4 - Wave close advances the roadmap (Priority: P4)

When the wave ships, the operator sees the three consolidated roadmap
features advanced to their delivered state (shipped/closed) with receipts, so
the fleet-wide roadmap reflects reality without manual bookkeeping.

**Why this priority**: Administrative but mandated by the wave's charter; it
keeps the three-host roadmap truthful.

**Independent Test**: After ship, the roadmap state for the three consolidated
features reads closed/delivered on this host and survives the next fleet sync
round unchanged.

**Acceptance Scenarios**:

1. **Given** the wave has shipped, **When** the close-out runs, **Then** the
   three consolidated features are advanced with attributed, durable records.

### Edge Cases

- A QUIC+WS link peer disappears mid-stream — the survivor must observe a
  bounded, reasoned fault (never an indefinite block), consistent with the
  existing link-layer fault rules.
- The mesh partitions (A–B up, B–C down) — messaging between still-connected
  pairs must continue; durable delivery to the unreachable peer resumes on
  heal.
- The durable store is full or unwritable — the sender must receive an
  explicit refusal, never silent message loss.
- Duplicate signal or duplicate fetch after a crash — the receiver's
  duplicate suppression must hold across restarts.
- A wave-4 output this wave depends on has not landed when its dependent task
  comes up — the task is parked and the collision flagged on the scheduler
  board, never worked around locally.
- Re-verification finds a prototype scenario that no longer passes — the
  divergence is reported as a named failure with a reason, never skipped.

## Requirements *(mandatory)*

### Functional Requirements

**Link completion (US1)**

- **FR-001**: The live GLP REPL MUST be able to establish the HTTP/3 (QUIC) +
  WebSocket channel-link from a GLP goal, with either side initiating, using
  the same link surface as the existing transports.
- **FR-002**: Ground terms exchanged over the completed link MUST arrive
  byte-identical, in per-sender order, matching the existing link-layer
  guarantees.
- **FR-003**: Three or more live instances MUST form the duplex mesh with
  every peer-pair able to exchange messages; the recorded mesh defect
  [NEEDS CLARIFICATION: pin the authoritative defect record for the "mesh fix"
  — which failing scenario/symptom from the 036 line is the acceptance
  baseline?] MUST be demonstrated fixed by a regression scenario.
- **FR-004**: The full stack MUST build clean from the current tree, and every
  prototype demo scenario MUST be re-run against the completed implementation
  with an explicit per-scenario verdict.
- **FR-005**: A link fault (peer loss, refused establishment, capability
  mismatch) MUST surface as an explicit, reasoned report on the existing
  fault-monitor surface — never a silent stall or a garbled stream.

**Durable mesh messaging (US2)**

- **FR-006**: A node MUST accept a program's outbound message for an offline
  peer, store it durably, and acknowledge acceptance to the program without
  blocking it indefinitely.
- **FR-007**: Message delivery MUST follow signal-then-fetch: the holder
  signals availability; the receiver fetches when ready; a fetch MUST be
  resumable after interruption.
- **FR-008**: Accepted messages MUST survive process restart via a write-ahead
  journal over the node's durable local store; on restart the node MUST resume
  delivery of undelivered messages without operator intervention.
- **FR-009**: Delivery MUST be at-least-once on the wire with duplicate
  suppression at the receiving node, so a program observes each accepted
  message exactly once; suppression MUST hold across restarts.
- **FR-010**: Per-sender message order MUST be preserved end-to-end.
- **FR-011**: A durable-tier failure (store unwritable, journal corrupt) MUST
  produce an explicit refusal or a named fault — never silent loss.

**3-role orchestration (US3)**

- **FR-012**: The 3-role model (planner, critic, builders) MUST exist as a
  written, reusable protocol covering both planning and execution triads:
  roles, hand-offs, convergence loop, evidence and attribution rules, and the
  engineer's decision gates.
- **FR-013**: Running the protocol on a real work item MUST produce an
  attributed result with a recorded critic pass and explicit engineer
  decisions; conflicting builder claims MUST escalate, never merge silently.

**Wave close (US4)**

- **FR-014**: At wave close, the three consolidated roadmap features MUST be
  advanced to their delivered state with durable, attributed records that
  survive fleet roadmap synchronization.

**Cross-cutting (operator-directed parallel run)**

- **FR-015**: Work that depends on wave-4 output MUST be sequenced last; a
  hard collision with in-flight wave-4 material MUST be flagged on the shared
  scheduler board and parked — never resolved by local workaround.

### Key Entities

- **Channel-link**: one established QUIC+WS link between two live instances;
  carries the existing link-layer frame and fault contracts.
- **Mesh**: the set of live instances and their pairwise links; peer-to-peer,
  no hub.
- **Durable message**: a program-level message accepted for delivery; carries
  sender, destination, per-sender sequence, and a stable identity used for
  duplicate suppression.
- **Message journal**: the node-local durable record of accepted-but-
  undelivered messages, replayed on restart.
- **Availability signal**: the lightweight notice a holder sends a reappeared
  peer that messages await fetch.
- **Triad engagement**: one run of the planning or execution protocol —
  its roles, inputs, attributed claims, critic verdicts, and engineer
  decisions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Two operators with no prototype tooling can link two live
  instances over QUIC+WS and exchange terms in under 5 minutes from REPL
  start.
- **SC-002**: A three-instance mesh run completes with 100% of peer-pairs
  exchanging messages; the recorded mesh defect's regression scenario passes.
- **SC-003**: 100% of the prototype's demo scenarios have an explicit verdict
  against the completed implementation; every failure is named and reasoned.
- **SC-004**: In the disconnect drill (receiver offline for N messages +
  sender restart), 100% of accepted messages arrive, each observed exactly
  once by the program, in per-sender order — for N at least 1,000.
- **SC-005**: No messaging scenario leaves a program blocked indefinitely:
  every blocked wait resolves or faults within the link layer's bounded-
  silence limits.
- **SC-006**: One planning and one execution triad engagement complete on real
  wave-5 work with full attribution and recorded engineer decisions.
- **SC-007**: At wave close, all three consolidated roadmap features read
  delivered/closed on every fleet host after the next sync round.

## Assumptions

- "Live glp_repl bridge" means the production REPLs that already carry the
  link layer (the reference implementation and its shipped mirror); extending
  the completed QUIC+WS transport to the newest runtime port follows the same
  seam but is not gated by this wave.
- The durable local tier reuses the node's existing embedded store (the same
  store family the repo already standardizes on) — no new external service.
- Durable messaging rides ABOVE the existing link layer (any transport the
  link supports), so it does not depend on the QUIC completion to be testable
  — TCP-backed tests are acceptable evidence for US2.
- At-least-once + receiver-side duplicate suppression is the delivery model;
  end-to-end exactly-once at the transport level is explicitly out of scope.
- The 3-role orchestration deliverable is gated by the scope clarification in
  US3; until ruled, estimates treat it as the smaller (adopt/operationalize)
  reading.
- Wave-4's language-gated items (the two §1.14 GLP-language proposals) are not
  expected to collide with this wave; if a collision emerges it goes to the
  scheduler board per FR-015.
- Fleet coordination (branch 063, scheduler-board claim, 15-day window,
  stage-seam updates) continues as already agreed with the fleet lead; this
  spec does not restate that protocol.
