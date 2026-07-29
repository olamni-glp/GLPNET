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

## Clarifications

### Session 2026-07-29

- Q: What is the authoritative "mesh fix" acceptance baseline (FR-003)? →
  A: Resolved from the record (2026-07-02 036 fidelity audit, carried in the
  roadmap profile of `http3-quic-ws-link-completion`): the **dup-id mesh
  eviction bug at `Program.cs:253`** — the regression scenario is a mesh where
  a duplicate peer id no longer evicts a live peer.
- Q: What does "live glp_repl bridge" mean? → A: Resolved from the same
  record: today the tool bridges only the message ENVELOPE — the `--repl`
  flag is accepted but the live REPL process-I/O bridge is inert. Completion
  means a genuine GLP REPL runs over the link via the established link-message
  interface.
- Q: What does "build + re-verify" cover? → A: Resolved from the same record:
  the C# host library is not built in-tree, so 9 integration tests skip and
  the prototype's 18/104-green claim is unreproducible; completion builds it
  in-tree and re-runs the suite so every scenario has a reproducible verdict.
  The Profile-A stack description is also corrected (the Gleam profile relays;
  the reference stack terminates QUIC).
- Q: US3 scope — adopt the existing buildkit 3-role capability or build a
  GLP-native triad implementation? → A: Resolved from the roadmap record: the
  feature is the **formalization of the proven ad-hoc method, designed for
  migration to buildkit** — and that migration has landed (the installed
  toolchain ships the 3-role task-team capability). US3 is therefore
  adopt-and-operationalize: run real glpnet engagements through the formal
  protocol (seeded by the recorded method-and-dogfood document), record the
  evidence, and close the roadmap item. Building GLP-native agent triads is
  explicitly NOT this feature.
- Q: Durable-mesh scope source? → A: The wave delivers the **first-hop
  prototype** exactly as captured in the operator's intake brief
  (`docs/roadmap-intake/durable-mesh-messaging-protocol.md`): directly-
  identifiable targets only; multi-hop routing is future work.
- Q: Signal/fetch wire carriage — reuse the QUIC+WS link or a separate
  control/data split? → A: (engineer-accepted suggestion) US2 rides the
  existing link-layer surface **transport-agnostically**: any link transport
  carries signal and fetch; TCP-backed evidence is acceptable for US2, with
  QUIC+WS exercised once US1 lands. This decouples US1 and US2 for the
  operator-directed parallel run.
- Q: Which intake elements are IN the first-hop prototype vs future? → A:
  (engineer-accepted default, per the Assumptions) IN: signal-then-fetch on
  mailboxes/topics, WAL + hot/analytical tiering, dense per-sender sequence +
  gap detection, retention classes, basic friend-lookup, dead-letter queue,
  originator/recipient CLI. FUTURE: multi-hop routing, the routing-policy
  language (must-have waypoints/excludes), replica advertisement, QoS/uptime
  profiles.

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

### User Story 2 - Durable first-hop mesh messaging (signal-then-fetch) (Priority: P2)

An operator brings up an **originator** instance and one or more **recipient**
instances from the command line (the messaging tool the intake brief names).
The originator accepts content for a named mailbox/topic addressed to a
directly-identifiable target (LAN or defined internet-reachable host — this
wave is the **first hop only**; multi-hop routing is future). Delivery is
Kafka-style **signal-then-fetch**: the originator signals the target that new
content is available; the recipient fetches at its own pace. Messages carry a
**dense per-sender sequence** (no gaps), so a gap is a detectable, named loss.
Every accepted message survives restart via a **write-ahead journal** with a
tiered durable store (recent messages hot, older messages aged to the
analytical tier); duplicate suppression at the recipient holds across
restarts. A sender that cannot resolve a target (station id with no address)
may ask its **known connections (friends)** whether they know it; an
unresolvable message goes to a **dead-letter queue**, never silently dropped.

**Why this priority**: The live mesh (US1) is connection-oriented — a dropped
peer loses in-flight traffic. The durable first hop is the floor of the
resilient multi-hop mesh the intake brief targets.

**Independent Test**: Bring up originator + recipient; take the recipient
offline; send N messages; restart the ORIGINATOR; bring the recipient back;
observe all N arrive exactly once, in per-sender dense-sequence order, and a
message to an unknown station id lands in the dead-letter queue.

**Acceptance Scenarios**:

1. **Given** an offline recipient, **When** the originator accepts messages
   for it, **Then** they are journalled durably and the originator is not
   blocked indefinitely.
2. **Given** journalled undelivered messages and an originator restart,
   **When** the recipient reappears, **Then** it is signalled, fetches the
   backlog at its own pace (resumable), and every accepted message is
   delivered — none lost, none observed twice.
3. **Given** a fetched backlog, **Then** per-sender dense-sequence order holds
   and any gap is reported as a named loss, never silently skipped.
4. **Given** a message addressed to a station id with no resolvable address,
   **When** friend lookup also fails, **Then** the message lands in the
   dead-letter queue with a stated reason.
5. **Given** retention classes (ephemeral / time-windowed / effectively
   permanent) declared at the source, **Then** expiry follows the declared
   class.

---

### User Story 3 - Formal 3-role agent-team orchestration (Priority: P3)

An engineer running substantial planning or execution work convenes a formal
3-role team — planner, independent critic, and builders on disjoint evidence
slices — through the capability that grew out of this repo's proven ad-hoc
method and has since migrated into the installed toolchain. This wave
**adopts and operationalizes** it for glpnet: the written protocol (seeded by
the recorded method-and-dogfood document) is exercised on real wave-5 work,
the practical evidence is recorded, and the roadmap item is closed as
delivered. Building GLP-native agent triads is explicitly out of scope
(resolved from the roadmap record — see Clarifications).

**Why this priority**: Valuable process capability, but it does not gate the
mesh deliverables; its build-scope is small now that the migration has landed
— the work is operationalization and evidence.

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

- **FR-001**: The QUIC+WS link MUST carry a genuine live GLP REPL session —
  the currently-inert REPL process bridge is completed through the established
  link-message interface, so "run GLP over the link" means a real REPL at each
  end, not envelope relay.
- **FR-002**: Ground terms exchanged over the completed link MUST arrive
  byte-identical, in per-sender order, matching the existing link-layer
  guarantees.
- **FR-003**: Three or more live instances MUST form the duplex mesh with
  every peer-pair able to exchange messages; the recorded dup-id mesh
  eviction defect (a duplicate peer id evicting a live peer — the 036
  fidelity-audit finding) MUST be demonstrated fixed by a regression scenario.
- **FR-004**: The full stack, including the host library the integration
  tests need, MUST build clean from the current tree, and the prototype's
  full suite MUST re-run with an explicit per-scenario verdict — the 9
  currently-skipped integration tests execute, making the prototype's green
  claim reproducible.
- **FR-005**: A link fault (peer loss, refused establishment, capability
  mismatch) MUST surface as an explicit, reasoned report on the existing
  fault-monitor surface — never a silent stall or a garbled stream.
- **FR-005a**: The stack-profile documentation MUST be corrected to the
  audited reality: the relay profile relays; the reference stack terminates
  QUIC.

**Durable mesh messaging (US2)**

- **FR-006**: An originator MUST accept a message for a named mailbox/topic
  addressed to a directly-identifiable target (first-hop scope), store it
  durably, and acknowledge acceptance without blocking the sender
  indefinitely — including while the target is offline.
- **FR-007**: Delivery MUST follow signal-then-fetch: the holder signals the
  target that content awaits on a mailbox/topic (the signal need not carry
  the content); the recipient fetches at its own pace; a fetch MUST be
  resumable after interruption.
- **FR-008**: Accepted messages MUST survive process restart via a write-ahead
  journal over a tiered durable store — recent messages served from the hot
  tier, older messages aged to the analytical tier with catch-up queries
  spanning both; on restart the node resumes delivery without operator
  intervention.
- **FR-009**: Delivery MUST be at-least-once on the wire with duplicate
  suppression at the recipient, so each accepted message is observed exactly
  once by the recipient's consumer; suppression MUST hold across restarts.
- **FR-010**: Each sender's messages MUST carry a dense, fully-serializable
  per-sender sequence; order is preserved end-to-end and a sequence gap is a
  detected, named loss (triggering re-fetch where a source is known), never a
  silent skip.
- **FR-011**: A durable-tier failure (store unwritable, journal corrupt) MUST
  produce an explicit refusal or a named fault — never silent loss.
- **FR-011a**: A message whose target cannot be resolved to an address —
  including after asking known connections (the basic friend-lookup) — MUST
  land in a dead-letter queue with a stated reason.
- **FR-011b**: Content retention MUST honor the class declared at the source:
  ephemeral, time-windowed, or effectively permanent.
- **FR-011c**: The operator MUST be able to bring up originator and recipient
  instances from the command line for test scenarios.

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
- **Durable message**: a message accepted for delivery; carries sender,
  target, mailbox/topic, dense per-sender sequence, retention class, and a
  stable identity used for duplicate suppression.
- **Mailbox/topic**: the named stream a message is published to and fetched
  from; the unit a signal refers to.
- **Message journal**: the node-local durable record (write-ahead) of
  accepted messages, replayed on restart; backed by the tiered store (hot
  tier for recent, analytical tier for aged content).
- **Availability signal**: the lightweight notice a holder sends a target
  that content awaits on a mailbox/topic (content not necessarily included).
- **Dead-letter queue**: the durable parking place, with reasons, for
  messages whose target cannot be resolved.
- **Friend lookup**: the basic ask-your-known-connections resolution step for
  a target known only by station id.
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
- The durable tier follows the intake brief's tiering: the repo's standard
  embedded store as the hot tier, aging into its analytical companion — no
  new external service.
- At-least-once + receiver-side duplicate suppression is the delivery model;
  end-to-end exactly-once at the transport level is explicitly out of scope.
- US2 is the FIRST-HOP prototype per the intake brief: multi-hop routing, the
  full routing-policy language (must-have waypoints/excludes), replica
  advertisement, and QoS/uptime profiles are future building blocks unless
  the wave's clarifications pull specific ones in.
- US3 is adopt-and-operationalize (resolved from the roadmap record); no
  GLP-native agent implementation in this wave.
- Wave-4's language-gated items (the two §1.14 GLP-language proposals) are not
  expected to collide with this wave; if a collision emerges it goes to the
  scheduler board per FR-015.
- Fleet coordination (branch 063, scheduler-board claim, 15-day window,
  stage-seam updates) continues as already agreed with the fleet lead; this
  spec does not restate that protocol.
