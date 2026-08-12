<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: Durable listener service box (gavri variant)

**Feature Branch**: `064-durable-listener-service-box`
**Created**: 2026-08-03
**Status**: Draft
**Input**: User description: "durable-listener-service-box — GLP QUIC listeners that survive REPL restarts (gavri variant: host-owned persistence, ZERO new GLP language surface). MVP: (1) resume-goal hook — REPL auto-arms the listener goal on launch; (2) host-owned PGlite-backed message log appending each received crdtmsg and replaying on boot — NO store_put/2 or store_get/2 GLP predicates, no §1.14 language-authority gate; (3) re-bind-on-boot of the QUIC listener via the same hook. Include: QuicTransport.ConnectAsync retry-until-ct fix (TCP parity, FR-004 role-order independence). Explicitly OUT: FCP-continuation snapshot (own future feature); any new GLP predicates/guards/kernels. Target users: glpnet service operators (Gabi/Marcelle) running durable GLP endpoints (e.g. the gavri↔Olamnit chat). Supervision by YngeniOS service box per spec-047 is an external pointer, out of scope here."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Listener survives a REPL restart with no operator action (Priority: P1)

A service operator runs a GLP endpoint (for example, the gavri↔Olamnit chat listener)
inside a REPL process. The process ends — a crash, a machine reboot, or a deliberate
restart. When the REPL next launches, the service comes back by itself: the listener
goal the operator registered is automatically re-armed and the endpoint accepts peer
connections again, without the operator re-typing the load-and-run sequence.

**Why this priority**: Today the endpoint dies with the process and stays dead until a
human re-arms it by hand — the single failure that makes GLP endpoints unusable as
services. Auto-re-arm is the smallest change that turns a demo into a service, and it
is the foundation the other two stories build on.

**Independent Test**: Register a listener goal, restart the REPL process, and verify a
peer can connect and exchange a message with zero operator keystrokes after launch.
Delivers a service that self-heals across restarts even without history.

**Acceptance Scenarios**:

1. **Given** a registered resume goal for a listener service, **When** the REPL process
   is stopped and started again, **Then** the listener endpoint accepts a new peer
   connection without any operator input after launch.
2. **Given** a registered resume goal, **When** the REPL launches and the goal's
   program file is missing or fails to load, **Then** the operator sees a clear
   diagnostic naming the registration and the cause, and the REPL remains usable.
3. **Given** no registered resume goal, **When** the REPL launches, **Then** startup
   behavior is exactly as before this feature (no prompt, no delay, no new output).

---

### User Story 2 - Message history survives restarts (Priority: P2)

Every message the service receives is recorded durably by the host as it arrives.
When the service comes back after a restart, its message history is complete: the
operator (and the service logic) see every message that was received before the
restart, in the order received, with none duplicated — so a conversation such as the
gavri↔Olamnit chat continues where it left off instead of starting blank.

**Why this priority**: A listener that re-arms but forgets everything is only half a
service; the chat use case is only useful if history persists. Depends on Story 1
(there must be a service to replay into), hence P2.

**Independent Test**: Send N messages to the service, restart the REPL, and compare
the post-restart visible history against the sent sequence — complete, ordered, no
duplicates. Can be tested with Story 1's re-arm alone, no other feature needed.

**Acceptance Scenarios**:

1. **Given** a service that has received messages, **When** the REPL restarts and the
   service re-arms, **Then** the full pre-restart message history is visible in the
   order originally received, with no duplicates and no gaps.
2. **Given** a message arrives, **When** the process crashes immediately afterwards,
   **Then** after restart that message is present in the history (a received message
   is durable before the service acts on it).
3. **Given** a restart replays history, **When** replay completes, **Then** newly
   arriving messages append after the replayed history and are themselves durable.
4. **Given** repeated restarts with no new traffic, **When** the operator inspects the
   history each time, **Then** it is byte-for-byte identical across restarts (replay
   is idempotent — it never re-appends what it replays).

---

### User Story 3 - Dialing peers are insulated from listener restarts (Priority: P3)

A peer that dials the service while it is restarting does not fail: the connection
attempt keeps retrying within its connection budget, and succeeds as soon as the
listener is re-armed. To the dialing peer, a restart of the service is invisible —
the same role-order independence the fleet's TCP transport already provides.

**Why this priority**: Without it, every service restart turns into a coordination
problem for peers ("wait, then redial"). With Stories 1+2 delivered, this closes the
loop so restarts are invisible end to end. It is last because peers can work around
it manually (redial), whereas Stories 1-2 have no workaround.

**Independent Test**: Start a dial before the listener is up (or mid-restart) and
verify it completes successfully once the listener arms, provided the wait stays
within the dialer's connection budget. Testable against any listener, independent of
Stories 1-2.

**Acceptance Scenarios**:

1. **Given** a listener that is not yet accepting, **When** a peer dials and the
   listener arms within the dialer's connection budget, **Then** the connection
   succeeds without the dialer taking any special action.
2. **Given** a listener that never arms, **When** a peer dials, **Then** the attempt
   fails only after the connection budget is exhausted, with the existing
   transport-fault reporting (never an instant hard failure while budget remains).

---

### Edge Cases

- Registered resume goal whose program file was moved or deleted: launch surfaces a
  named diagnostic (Story 1, scenario 2); the REPL never hangs or exits.
- The listener's network endpoint is still occupied at re-arm time (previous process
  lingering, another process squatting): re-arm retries within a bounded window, then
  reports a clear failure; it never silently drops the registration.
- Crash mid-append to the durable log: on restart the history contains either the
  complete message or no trace of it — never a torn/corrupt entry that breaks replay.
- Two registrations for the same service: the operator's registration surface
  prevents or clearly reports duplicates; replay never runs twice for one service.
- History grows large: replay time may grow with history size; the MVP accepts this
  (see Assumptions) and the history remains inspectable by the operator.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Operators MUST be able to register a resume goal — a service goal plus
  the program it needs — that the REPL automatically arms on every subsequent launch.
- **FR-002**: On launch, the REPL MUST arm every registered resume goal without any
  operator interaction, and launch behavior with zero registrations MUST be unchanged
  from before this feature.
- **FR-003**: The registration MUST be durable across process restarts and machine
  reboots, and MUST be inspectable and removable by the operator.
- **FR-004**: The host MUST durably record every message the service receives, at or
  before the point the service acts on it, so that a crash immediately after receipt
  never loses the message.
- **FR-005**: On re-arm, the host MUST replay the stored history to the service in
  original receipt order, exactly once per stored message (idempotent across repeated
  restarts — replay never re-appends replayed messages).
- **FR-006**: All persistence in this feature MUST be host-owned: the feature adds
  ZERO new GLP language surface — no new predicates, guards, body kernels, directives,
  or type-system features. (This is the gavri variant; the store_put/2 + store_get/2
  language-surface variant is explicitly rejected for this feature, so the §1.14
  language-authority gate is not triggered.)
- **FR-007**: Re-arm MUST re-bind the service's listening endpoint so peers can
  connect after restart exactly as before it.
- **FR-008**: A dialing peer whose connection attempt begins while the listener is
  down MUST succeed once the listener arms, provided this happens within the dialer's
  existing connection budget (role-order independence, at parity with the fleet's TCP
  transport behavior).
- **FR-009**: Every failure in this feature's surface (registration load failure,
  re-bind failure, log append failure, replay failure) MUST produce an explicit,
  operator-visible diagnostic naming the cause — never a silent skip or a hang.

### Key Entities

- **Resume-goal registration**: the operator's durable declaration of a service —
  which goal to arm, with which program — inspected and honored at every launch.
- **Message log entry**: one durably recorded received message, carrying its receipt
  order; the unit of replay.
- **Service listener endpoint**: the network identity peers dial; re-bound at re-arm
  so it is stable across restarts from the peers' point of view.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After an intentional REPL restart, a registered service accepts a peer
  message with **zero operator keystrokes** post-launch, within 10 seconds of launch.
- **SC-002**: In a restart drill of at least 100 messages received before restart,
  **100%** are present after restart, in receipt order, with **zero duplicates** —
  and a second restart leaves the history byte-identical (replay idempotence).
- **SC-003**: A peer dialing continuously through a service restart window completes
  its connection without any dialer-side special handling in **100%** of drill runs
  where the listener re-arms within the dialer's connection budget.
- **SC-004**: The feature ships with **zero change** to the GLP language surface: the
  set of predicates, guards, kernels, directives and types accepted by the engine is
  identical before and after the feature (verified by the existing language-surface
  test suites passing unmodified).
- **SC-005**: With no registrations present, REPL launch time and output are unchanged
  (no measurable regression for non-service users).

## Assumptions

- One REPL process serves one registered service endpoint at a time (the chat use
  case); multi-service-per-process registration may work but is not drilled in MVP.
- The host persistence store already available to glpnet hosts is reused; no new
  external service is introduced by this feature.
- Replay time growing linearly with history size is acceptable for MVP volumes
  (chat-scale, thousands of messages); log compaction/retention is future work.
- Process supervision (auto-restarting the REPL itself) is provided externally
  (YngeniOS service box per spec-047, referenced from the Olamni repo) and is out of
  scope; this feature makes a restart *recover*, not *happen*.
- FCP-continuation snapshot (capturing live in-flight computation state) is explicitly
  out of scope — a future feature; this feature recovers a service from its durable
  registration + message history, not from a process image.
- The message kind persisted in MVP is the service's received-message stream (the
  chat's message terms); other stream kinds follow the same shape later if needed.
