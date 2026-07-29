<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: Wave 2 Consolidated — REPL Engine Split Spine

**Feature Branch**: `061-wave-2-consolidated-repl-engine-split-spine`
**Created**: 2026-07-29
**Status**: Draft
**Input**: User description: "Wave 2 consolidated: REPL engine split spine — two-process REPL/engine split with snapshot, liveness, restore-resume. Consolidates in order: repl-engine-process-split-mvp (TCP loopback two-process split MVP), engine-state-snapshot-and-persistence-api, liveness-crash-restart-host, restore-and-resume-with-link-reestablish. Builds on shipped 038 result-codec-and-framecodec-ride. Feature branch 061-wave-2-consolidated-repl-engine-split-spine already created and pushed (use it; do not renumber)."

## Overview

Today the GLP REPL and the GLP engine live and die as one process: closing the
terminal kills every running goal, all engine state, and every established
link. This wave splits them into two cooperating processes — a thin REPL client
and a long-lived engine host — and then makes the engine host durable: its
state can be snapshotted and persisted, its liveness is supervised with
automatic restart on crash, and after a restart it restores its state,
re-establishes its links, and resumes work. The wave consolidates four
previously refined roadmap features, in dependency order, into one delivery:

1. **Split MVP** — two processes over a local connection; client sends source
   text, engine returns a structured result (`repl-engine-process-split-mvp`).
2. **Snapshot + persistence** — engine state captured at quiescence behind a
   durable store interface (`engine-state-snapshot-and-persistence-api`).
3. **Liveness + supervised restart** — a service host that detects engine
   death and restarts it (`liveness-crash-restart-host`).
4. **Restore-and-resume** — the restarted engine reloads its snapshot,
   re-establishes links, and continues (`restore-and-resume-with-link-reestablish`).

It builds on the shipped result-envelope codec (feature 038,
`result-codec-and-framecodec-ride`) and the shipped link-establish core
(feature 025, `multi-protocol-link-layer`).

## Clarifications

### Session 2026-07-29

- Q: How should "quiescent" be defined for snapshotting, and which triggers are in scope? → A: Quiescent = empty goal queue with no reduction in flight and the client transport drained (suspended goals and armed timers are allowed and captured); triggers = explicit on-demand request plus automatic snapshot on graceful shutdown — no periodic snapshotting in this wave.
- Q: Should a restored timer fire at its original absolute deadline, or re-arm with the remaining time as of the snapshot? → A: Remaining-time re-arm — the snapshot stores each timer's remaining duration and restore re-arms from that; behaviour is independent of downtime length and no expired-timer storm fires on restore.
- Q: At a crash, what counts as "committed" work, and is replaying in-flight requests permitted? → A: Committed = everything captured in the last complete snapshot plus result envelopes already handed to the transport; work in flight after that snapshot is discarded on restore — no replay in this wave (at-most-once); the client/peer sees a transport failure for the in-flight request and re-submits. Replay (§10.9) stays a recorded deferral.
- Q: Which verification tool(s) model the full client↔engine protocol (R15 selection)? → A: SPIN + TLA+ + UPPAAL together — SPIN for the wire protocol (deadlock-freedom, unspecified receptions, progress), TLA+ for the crash/restore/resume state consistency, UPPAAL for the timed liveness/supervision properties.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run GLP goals from a separate client process (Priority: P1)

A GLP operator starts the engine host as its own process, then starts the thin
REPL client in another terminal. The client connects locally, the operator
types a goal exactly as in today's REPL, and the answer (bindings, success,
failure, or suspension) comes back and renders as today. Closing the client
leaves the engine — and everything loaded into it — running; reconnecting with
a fresh client finds the same engine session.

**Why this priority**: This is the spine everything else hangs on — without
the two-process split there is nothing to persist, supervise, or restore. It
is independently shippable as the smallest end-to-end split (one engine, one
client) and was ratified as the MVP cut.

**Independent Test**: Start engine host; start client; load a `.glp` program
from source text; run a goal; verify the rendered result matches the
single-process REPL's output for the same program and goal; kill the client;
reconnect; verify loaded program still present.

**Acceptance Scenarios**:

1. **Given** a running engine host and a connected client, **When** the
   operator submits a goal that succeeds with ground bindings, **Then** the
   client renders the same bindings the single-process REPL would render.
2. **Given** a running engine host and a connected client, **When** the
   operator submits a goal that suspends, **Then** the client reports the
   suspension the same way the single-process REPL does.
3. **Given** a client that has loaded a program into the engine, **When** the
   client process exits and a new client connects, **Then** the previously
   loaded program remains available to goals from the new client.
4. **Given** a running engine host, **When** the client sends program source
   containing a compile error, **Then** the client renders the engine's error
   output and the engine remains usable for subsequent requests.

---

### User Story 2 - Snapshot and persist the engine state (Priority: P2)

An operator (or a supervising host) asks a quiescent engine to snapshot its
state. The full execution state — everything needed to later resume as if
nothing happened — is captured and written durably with a monotonically
increasing sequence number. The operator can list and inspect stored snapshots
and the engine can be started "empty" or "from snapshot N".

**Why this priority**: Snapshot is the precondition for both supervised
restart (US3) and restore-and-resume (US4); it delivers standalone value as a
save-point facility even before supervision exists.

**Independent Test**: Load a program, run goals to a known state, snapshot,
start a second engine from the snapshot, and verify the second engine answers
a state-revealing probe goal identically to the first.

**Acceptance Scenarios**:

1. **Given** a quiescent engine with loaded programs and suspended goals,
   **When** a snapshot is requested, **Then** a durable snapshot with the next
   sequence number is written and acknowledged.
2. **Given** a stored snapshot, **When** a fresh engine starts from it,
   **Then** loaded programs, suspended goals, pending timers, and goal-id
   allocation state are present and consistent with the snapshotted engine.
3. **Given** an engine that is not quiescent, **When** a snapshot is
   requested, **Then** the request is refused (or deferred until quiescence)
   with an explicit reason — never a silently inconsistent snapshot.
4. **Given** the primary store is unavailable, **When** a snapshot is
   requested, **Then** the fallback store receives the snapshot and the
   degradation is reported loudly.

---

### User Story 3 - Supervised engine with crash detection and restart (Priority: P3)

An operator installs the engine under a supervising service host. The
supervisor pings the engine on a timer; if the engine dies or stops
responding, the supervisor records the crash (with the engine's exit signal),
restarts the engine, and triggers restore-and-resume. The operator can query
liveness status and the crash/restart history.

**Why this priority**: Supervision turns the split engine into an unattended
service; it needs US1 (a separate engine process to supervise) and US2 (a
snapshot to restart from) but delivers its own value: no more silently dead
engines.

**Independent Test**: Run the engine under the supervisor, kill the engine
process externally, verify the supervisor detects death within the ping
interval, restarts it, and the restarted engine reports healthy.

**Acceptance Scenarios**:

1. **Given** a supervised healthy engine, **When** the liveness ping fires,
   **Then** the supervisor records a healthy heartbeat and takes no action.
2. **Given** a supervised engine that crashes, **When** the next ping (or the
   process-exit signal) detects death, **Then** the supervisor logs the crash
   with its exit information and starts a replacement engine.
3. **Given** a replacement engine started by the supervisor, **When** it comes
   up, **Then** it is started from the latest snapshot via the
   restore-and-resume path (US4) rather than empty.
4. **Given** an engine in an unrecoverable state (per the recorded taxonomy),
   **When** the supervisor classifies it, **Then** it stops restart-looping
   and surfaces the condition to the operator.

---

### User Story 4 - Restore, re-establish links, resume (Priority: P4)

After a restart (supervised or manual), the engine reloads the latest
snapshot, re-establishes its ephemeral links from their durable definitions,
re-wires its cursors, and resumes draining work. A peer that was connected
before the crash can continue interacting after the restart without manual
reconfiguration. A kill-and-restart correctness test passes: the observable
stream of results after crash-restore is consistent with a run that never
crashed (no lost, duplicated, or reordered committed work).

**Why this priority**: This completes the durability story — it is the payoff
of US2+US3 and the wave's headline outcome ("two-process REPL/engine with
snapshot, liveness, restore-resume").

**Independent Test**: The kill-and-restart test: run a program that streams
committed results to a peer over an established link; kill the engine
mid-stream; let the supervisor restart it; verify the peer-observable
committed results are exactly consistent with an uninterrupted run.

**Acceptance Scenarios**:

1. **Given** a snapshot containing suspended goals and link definitions,
   **When** the engine restores, **Then** every persistent construct is
   reloaded and every ephemeral link is re-established from its definition.
2. **Given** a restored engine whose heap contains already-bound cells at
   former link endpoints, **When** links are re-wired, **Then** the re-wire
   path adopts the restored state instead of aborting on pre-bound cells.
3. **Given** a peer connected before the crash, **When** the engine restarts
   and re-establishes the link, **Then** the peer can continue the exchange
   without manual reconfiguration.
4. **Given** the kill-and-restart correctness test, **When** it runs in the
   suite, **Then** it passes deterministically.

---

### Edge Cases

- Engine host started when another engine host already owns the port/endpoint:
  second instance must refuse loudly, not queue silently.
- Client connects while the engine is mid-restore: engine must refuse or hold
  requests until restore completes, with an explicit status, never answer from
  a half-restored state.
- Snapshot requested during an in-flight snapshot: second request refused or
  coalesced; sequence numbers stay monotonic; no interleaved partial writes.
- Crash during snapshot write: on restart, the store must yield the last
  complete snapshot; a torn write is detected and discarded.
- Restart storm (engine crashes immediately after every restore): supervisor
  must apply the unrecoverable-state taxonomy / backoff and stop looping.
- Snapshot taken with zero loaded programs (empty engine): restore of an
  empty snapshot yields a healthy empty engine.
- Link peer unreachable at re-establish time: engine resumes local work;
  link re-establish retries per the link layer's existing policy; resumption
  of the drain for that link waits for establishment rather than failing the
  whole restore.
- Client submits a request while the engine connection is lost mid-request:
  client reports the transport failure distinctly from a goal failure.

## Requirements *(mandatory)*

### Functional Requirements

**Split MVP (US1)**

- **FR-001**: The system MUST provide a client process and an engine process
  that communicate over a local transport connection, with the client sending
  program source text and goal requests and the engine returning structured
  result envelopes (as established by shipped feature 038).
- **FR-002**: The MVP MUST support exactly one engine serving one client
  connection at a time (multi-client is explicitly deferred — DEF-A2).
- **FR-003**: The engine MUST bootstrap its prelude context (root `self.glp`)
  itself; the client MUST remain a thin terminal holding no local language
  context (ratified R7).
- **FR-004**: The MVP result envelope MUST carry the ground-only field subset
  with bindings pre-rendered to display strings by the engine, and MUST
  include the captured program output as a length-prefixed UTF-8 text field
  (ratified R6, R3; full field set deferred — DEF-C1).
- **FR-005**: The engine host MUST be a new, separately startable component,
  not a mode flag on the existing REPL (ratified R5); the existing
  single-process REPL MUST remain available and unchanged in behaviour.
- **FR-006**: Engine-side errors (compile, runtime, protocol) MUST be
  returned to the client as structured results that render meaningfully, and
  MUST leave the engine usable for subsequent requests.
- **FR-007**: The client MUST distinguish transport-level failure (lost
  engine) from goal-level failure in what it reports to the operator.

**Snapshot + persistence (US2)**

- **FR-010**: The engine MUST be able to capture, at quiescence, a snapshot
  containing its complete resumable execution state: heap, goal queue,
  suspended-goal records, per-goal tables, goal-id allocation state, loaded
  program units, wait/reader registrations, infrastructure goal identities,
  and channel state (scope expansion per DEF-D1 is binding).
- **FR-011**: Snapshots MUST preserve heap cell identity verbatim so that
  externally held references remain valid after restore (verbatim-address
  constraint — DEF-E2 mandated here).
- **FR-012**: Snapshots MUST be written through a durable store interface
  with a primary durable backend and a plain-file fallback, carrying a
  monotonically increasing sequence number per engine identity.
- **FR-013**: The store MUST expose: write snapshot, read latest complete
  snapshot, read snapshot by sequence, and list snapshots; a torn or
  incomplete write MUST never be returned as a valid snapshot.
- **FR-014**: The engine is quiescent when the goal queue is empty, no
  reduction is in flight, and the client transport is drained; suspended
  goals and armed timers are permitted in a quiescent engine and are
  captured. Snapshots are triggered on explicit on-demand request and
  automatically on graceful shutdown; periodic snapshotting is out of scope
  for this wave. A snapshot request against a non-quiescent engine MUST be
  deferred until quiescence (with its pending state reported to the
  requester); the engine MUST NOT emit an inconsistent snapshot.
- **FR-015**: Pending timer state MUST be captured as each timer's remaining
  duration at snapshot time; on restore, each timer re-arms with that
  remaining duration (remaining-time semantics). Restore MUST NOT fire
  timers whose absolute deadline passed during downtime as an immediate
  batch; downtime length MUST NOT change observable timer ordering.

**Liveness + supervised restart (US3)**

- **FR-020**: A supervising host MUST run the engine as a managed service,
  ping it for liveness on a configurable timer, and record heartbeats.
- **FR-021**: MVP liveness MUST be host-timer based only; a self-prove GLP
  liveness goal is EXCLUDED from this wave because it requires a new system
  predicate, which is gated on explicit language-authority approval (Gabi,
  CLAUDE.md §1.14 / DEF-F1) — it may only be proposed, never implemented,
  within this wave.
- **FR-022**: On engine death (process exit or ping timeout), the supervisor
  MUST record the crash with exit information and start a replacement engine
  via the restore path.
- **FR-023**: The supervisor MUST classify unrecoverable states per an
  explicit recorded taxonomy (DEF-F2) and stop restart-looping (with backoff
  and operator surfacing) when one is met.
- **FR-024**: The supervisor MUST expose liveness status and crash/restart
  history to the operator on demand.
- **FR-025**: The MVP supervisor targets Windows service hosting; the design
  MUST keep the supervision contract portable (Linux hosting deferred).

**Restore-and-resume (US4)**

- **FR-030**: On restart-from-snapshot, the engine MUST reload all persistent
  constructs from the latest complete snapshot before accepting client or
  peer traffic.
- **FR-031**: The engine MUST re-establish ephemeral links from their durable
  link/listen definitions via the existing link-establish core (feature 025),
  and MUST re-wire restored endpoints through a dedicated re-wire path that
  adopts pre-bound restored cells instead of aborting on them (DEF-E1).
- **FR-032**: After restore, the engine MUST re-wire its cursors and resume
  draining committed work such that the peer-observable committed result
  stream is consistent with an uninterrupted run — no committed work lost or
  duplicated. Committed = state captured in the last complete snapshot plus
  result envelopes already handed to the transport. Work in flight after
  that snapshot is discarded on restore — no replay in this wave
  (at-most-once); the client/peer observes a transport failure for the
  in-flight request and re-submits. In-flight-request replay (§10.9)
  remains a recorded deferral (DEF-X3).
- **FR-033**: A kill-and-restart correctness test exercising US4 end-to-end
  MUST be added to the permanent test suite.

**Cross-cutting**

- **FR-040**: The complete client↔engine protocol introduced by this wave
  MUST be modelled and checked with three tools per the R15 selection made
  at this spec step: SPIN for the wire protocol (deadlock-freedom, no
  unspecified receptions, named progress properties — fulfilling R14/DEF-A3),
  TLA+ for crash/restore/resume state consistency, and UPPAAL for the timed
  liveness/supervision properties; formal proof work remains off the
  critical path (R11).
- **FR-041**: Every seed's metric table MUST be produced per the shared
  template (R8) with the protocol-verification row mandatory (R14).
- **FR-042**: Core GLP language surface MUST NOT change in this wave; any
  discovered need for new guards/predicates STOPS and goes to the language
  authority first (CLAUDE.md §1.14).
- **FR-043**: On wave completion, the MVP-gate review (Deferral Register
  Anchor A) MUST be run and its outcomes recorded; the four consolidated
  roadmap features MUST be advanced shipped/closed at wave close.

### Key Entities

- **Engine host**: the long-lived process owning the GLP execution state;
  startable empty or from a snapshot; serves one client (MVP).
- **REPL client**: thin terminal process; submits source text and goals,
  renders structured results; holds no language context.
- **Result envelope**: the structured answer for one request (established by
  feature 038); MVP subset is ground-only with pre-rendered bindings plus a
  length-prefixed output text field.
- **Snapshot**: a sequence-numbered, complete, verbatim capture of the
  engine's resumable state (heap, queues, suspensions, tables, ids, loaded
  units, waits, infrastructure goal ids, channels, timers).
- **Snapshot store**: durable interface over a primary backend with file
  fallback; monotonic sequence per engine identity; torn-write safe.
- **Supervisor**: the service host that pings, records, restarts, classifies
  unrecoverable states, and reports history.
- **Link definition**: the durable description (identity, endpoints, listen
  role) from which an ephemeral link is re-established after restore.
- **Crash record**: exit information + timestamp + restart outcome, queryable
  by the operator.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a representative program set, goals submitted through the
  split client return results identical in content to the single-process
  REPL's results for the same inputs, with 100% agreement on the ground-only
  envelope subset.
- **SC-002**: An operator can kill the engine process at an arbitrary point
  in the kill-and-restart test and the system returns to serving correct
  results without human intervention, with zero committed results lost or
  duplicated, in 100% of suite runs.
- **SC-003**: Engine death is detected and a replacement engine is serving
  again within one ping interval plus restore time; the whole
  detect-restart-restore cycle completes without operator action.
- **SC-004**: A snapshot/restore round-trip reproduces engine state such that
  every state-revealing probe in the suite answers identically pre- and
  post-restore (100% of probes).
- **SC-005**: The full wave ships with the wire protocol model checked
  (deadlock-freedom + named liveness properties pass), the metric tables
  complete, and the existing REPL + unit suites green (no regression from
  baseline).
- **SC-006**: All four consolidated roadmap features are advanced to
  shipped/closed at wave close, and the Anchor-A MVP-gate review is recorded.

## Pre-Specify Obligations Applied *(binding context)*

Per the roadmap briefs' PRE-SPECIFY pointers, this spec applies the ratified
decisions and actions the deferral anchors owned by its seeds
(`docs/research/repl-engine-separation/reconciliation/DECISIONS-LOG.md`,
`DEFERRALS.md`):

- **R3, R5, R6, R7, R11** applied to the split MVP (FR-003/004/005, FR-040).
- **R14/DEF-A3**: full protocol model obligation lands here (FR-040).
- **DEF-D1** (snapshot scope expansion) binding in FR-010; **DEF-D2**
  (U-P1–U-P7): U-P1/U-P2/U-P5/U-P6/U-P7 carried as the three NEEDS
  CLARIFICATION markers (FR-014/015/032); U-P3 (address stability) resolved
  by FR-011; U-P4 (blob format) delegated to planning within FR-012/013.
- **DEF-E1** (re-wire path) in FR-031; **DEF-E2** (verbatim addresses) in
  FR-011.
- **DEF-F1** (language-authority gate on self-prove goal) in FR-021/042;
  **DEF-F2** (taxonomy, Windows-first platform) in FR-023/025.
- **Anchor A** (MVP-gate review after the split MVP ships) scheduled at wave
  close (FR-043).

## Assumptions

- The shipped feature 038 result-envelope codec and transport framing are the
  wire foundation; this wave does not redesign them.
- The shipped feature 025 link-establish core provides link establishment;
  this wave adds only the restore-time re-wire path (DEF-E1) on top of it.
- MVP platform for the supervised host is Windows (this repo is the Windows
  workstream); the supervision contract is kept portable but Linux hosting is
  out of scope for this wave.
- One engine / one client is the MVP concurrency model; multi-accept and
  multi-client control programs are separate roadmap features (DEF-A2) and
  out of scope.
- Snapshot durability uses the established repo storage approach (durable
  primary + plain-file fallback with monotonic sequencing); exact blob format
  is a planning-stage decision inside FR-012/013.
- Formal (Lean/Rocq) proof work stays off this wave's critical path (R11);
  the pragmatic protocol-verification tier (R14) is in scope.
- The wave is delivered on this single feature branch `061-…` through the
  standard buildkit pipeline; internal ordering follows the four user-story
  priorities (US1 → US2 → US3 → US4).
- Dart-mirror byte-parity for the result codec remains deferred (DEF-A1) —
  out of scope here.
