<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature Specification: M6 QUIC mailbox adapter — the wire plane reaches the control surface

**Feature Branch**: `107-quic-mailbox-adapter`
**Created**: 2026-09-06
**Status**: Draft
**Input**: User description: "M6 QUIC mailbox adapter — wire the QUIC plane into the code-based YNET client's control surface so the daemon can send and receive on the wire, not only on the file plane"

## Context: the measured gap this feature closes

This is not a greenfield feature. It closes a gap that was **measured in this repo today**, in
this lane's own code, one commit old.

Commit `8d4088e4` added `csharp/ynet_client/Client/QuicCarrier.cs` — 400 lines realizing
`QuicInbound : IYnetInbound` and `QuicOutbound : IYnetOutbound` over the authenticated QUIC
session in `csharp/ynet_transport` — together with 210 lines of tests. The library builds and
its tests pass (93/93 client, three consecutive runs).

**`csharp/ynet_client/Program.cs` contains zero references to it.** Measured
2026-09-06T13:0xZ by `grep -n "Quic" csharp/ynet_client/Program.cs` → no matches in 319 lines.

Concretely, the client's control surface today:

| verb | plane it can bind | can it use QUIC? |
|---|---|---|
| `run` | `CoopFileInbound` (shared-volume file drop) or `LoopbackInbound` (hears only itself) | **no** |
| `send` | `CoopFileOutbound` (hard-coded, not injected) | **no** |
| `poll` | `CoopFileInbound` (hard-coded) | **no** |
| `doctor` | `CoopFileInbound` (hard-coded) | **no** |

So the running M6 client on this host participates in YNET **over a mounted disk only**. The
wire exists, is authenticated, is tested — and is unreachable from the process the fleet
actually runs. A carrier nobody can select is indistinguishable, from the outside, from a
carrier that was never written.

This is a self-found instance of the defect class the fleet is already tracking as roadmap item
`declared-unconsumed-guard` (WSJF 8.00 / RICE 18000, rank 2): **capability declared, consumer
absent**. The feature both closes this instance and produces the evidence that the class is real
in first-party code, not only in inherited code.

## Root cause — measured, and the same class found TWICE in this repo in one day

The engineer's reversal of Q-G34-01 ordered a **fleetwide root cause** for the absence of
kernel-managed hosting. It was measured before anything was claimed.

**glpnet already contains a working, tested process supervisor** — `csharp/glp_supervisor`
(`Supervisor.cs`, `SupervisorConfig.cs`, `CrashLog.cs`, `UnrecoverableTaxonomy.cs`, with
`SupervisorTests` and `KillAndRestartTests`). Measured surface:

- hosts a child process and owns its lifetime;
- proves liveness by a **round-trip ping that must be ACKed inside `PingTimeout`** — not by process
  existence, not by the child's own status verb, not by an unexpired lease;
- folds **one fresh-connection retry** into that budget, so a broken socket over a live child is not
  misread as death;
- detects the **ping-timeout zombie** — process alive, no longer answering — and kills it;
- records the death, backs off (`BackoffInitial` × `BackoffMultiplier`, capped), restarts via a
  restore path, and completes the crash record on the first healthy ping;
- **stops the loop loudly** on an unrecoverable taxonomy rather than restarting forever.

That is materially the liveness and supervision discipline the fleet's own LEADER+PLANNER directive
specifies — *"proves liveness by answering a nonced round-trip within T_resp — never by process
existence, never by its own status verb, never by an unexpired lease"* — **already built and tested
here**.

**It hosts `glp_engine_host`. It does not host the M6 YNET client** — the one process the fleet has
declared MUST be kernel-managed. Measured 2026-09-06:
`grep -rn "Supervisor" csharp/ynet_client/ csharp/ynet_client.tests/` → **no matches**.

So the root cause of "no kernel-managed hosting" in this lane is **not** that the capability is
missing. It is that **the capability exists and the consumer was never written** — the identical
class as the `QuicCarrier` finding above, measured independently, in the same repo, on the same day:

| # | Capability built & tested | Consumer that would make it load-bearing | Status |
|---|---|---|---|
| 1 | `QuicInbound`/`QuicOutbound` — 400 LoC, 210 LoC tests, green | a plane selection in `Program.cs` | **absent** |
| 2 | `glp_supervisor` — supervision + round-trip liveness, green | supervised hosting of the M6 client | **absent** |

The engineer's cited L0 case (feature-020 hooks `OnStepDispatched`, `Unregister`,
`StartOnDedicatedThread`, `Markers` — zero consumers) is a third candidate instance in a different
repo. A peer (`shiras-crucible`, 2026-09-05T10:31Z) published a refutation of that *specific*
instance at source. **That refutation does not touch the class**: instances 1 and 2 are first-party,
measured here, and uncontested.

**The generalised cause**: nothing in the toolchain can tell the difference between a capability
that is *built and used* and one that is *built and unreachable*. Both compile. Both pass their own
tests — because a capability's own tests construct it directly, which is exactly the path a real
consumer does not take. Review does not catch it either: instance 1 was reviewed, tested and merged
with no consumer. **The durable fix must therefore be a machine check over the shipped assembly,
not a review instruction** — which is SC-004 here, and roadmap `declared-unconsumed-guard`
fleet-wide.

## Clarifications

### Session 2026-09-06 — BK-STD-2 question set G34

Four blocking questions were put to the engineer through the BK-STD-2 interactive surface and
answered the same session. The full record, including the options not taken and the reason each was
rejected, is `questions-G34.json` (validated conformant, all four carrying a `decision`). Each
ruling is cited below at the requirement it changed; none is paraphrased away.

- **Q-G34-01 → B (SUPERSEDES an earlier A ruling taken the same session).** The engineer initially
  ruled M6-d out of era 107, then **reversed it explicitly**: kernel-managed hosting is **NOT out of
  scope** and must be **fleetwide root-caused and remedied now**. Era 107 therefore includes
  kernel-managed hosting of the M6 client. See FR-024..FR-029 and *Root cause* below. The earlier
  ruling and its full rationale remain in `questions-G34.json` history and in git; it is superseded,
  not erased.
- **Q-G34-02 → C.** When the wire plane is requested and no listener can bind, the client **falls
  back to the file plane, says so on the same line that says "running", and additionally emits a
  fleet-visible degraded notice** so that fleet-wide wire loss is a *count*, not N individually-fine
  hosts. Rejected: refusing hard (strands a certificate-damaged host with no receiver at all);
  silent fallback (is the defect this era exists to close). See FR-004, FR-004a, FR-004b.
- **Q-G34-03 → B.** The **composite — both planes bound at once — is IN era 107**, with the
  de-duplication mutation-proven. One client, one mailbox, no flag-day migration. User Story 4 is
  therefore **raised from P2 to P1** and its de-duplication acquires a mutation-proof obligation.
  See FR-022, FR-023, FR-023a, SC-008.
- **Q-G34-04 → A.** The unreachable-realization check is built **local to this repo** to satisfy
  SC-004, and the measured instance is **published to the fleet as evidence**. This lane does
  **not** claim the fleet-wide `declared-unconsumed-guard` (roadmap rank 2) without a claim-first
  broadcast — the duplication defect that cost this repo an entire M6 carrier in wave-32 is not
  worth repeating for a guard. See SC-004 and Out of Scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The operator starts the client on the wire (Priority: P1)

An operator starts this lane's code-based YNET client and asks it to receive on the QUIC wire
rather than on the shared file volume. The client binds a listening endpoint, reports the address
and the provider that bound it, and from that moment a peer that completes a handshake can deliver
a message to this lane without any shared filesystem between the two hosts.

**Why this priority**: This is the M6 mandate's receive half. Without it, cross-host delivery
depends on a mounted volume being present on both hosts, which is the condition the fleet has
already recorded as intermittent. It is also the only story that makes the already-built carrier
reachable at all — every other story is downstream of it.

**Independent Test**: Start the client with the wire plane selected; observe it print a bound
endpoint and a provider name it read off the handle. Deliver a frame from a second process over
QUIC; observe the durable alert appear and survive the client exiting.

**Acceptance Scenarios**:

1. **Given** a host with a working QUIC provider and an identity, **When** the operator starts the
   client selecting the wire plane, **Then** the client reports the bound endpoint and the
   provider name, and states that the plane is the wire — not the file plane and not loopback.
2. **Given** a running client on the wire plane, **When** a peer completes a handshake and sends a
   well-formed frame, **Then** the client records a durable alert whose origin is the
   handshake-proven peer.
3. **Given** no QUIC provider can bind on this host, **When** the operator selects the wire plane,
   **Then** the client refuses loudly and names why, and does **not** silently fall back to a
   different plane while reporting success.

---

### User Story 2 - The operator sends on the wire (Priority: P1)

An operator (or a script, or the agent) sends a message to a named peer over the QUIC wire from
the same control surface used for the file plane, with the same addressing convention
(`<node>/<actor>`) and the same frame on the wire.

**Why this priority**: The engineer's M6 mandate is explicit that the client must **send and
receive** independently of the agent. Receive-only is half a client, and a lane that can only
receive cannot answer, which is what "participate" means in a PBFT fleet.

**Independent Test**: With a peer listening on the wire, send a frame and observe the peer's
receiver record it. With no peer listening, observe a refusal that names the peer and the address,
and a non-zero exit — never a reported success into nowhere.

**Acceptance Scenarios**:

1. **Given** a peer listening on the wire, **When** the operator sends to that peer by identity,
   **Then** the peer receives a frame byte-identical to what the file plane would have carried.
2. **Given** no peer at the address, **When** the operator sends, **Then** the command fails with
   a distinct non-zero exit code and a message naming the peer and the address, and never reports
   a successful send.
3. **Given** the peer's network answers "unreachable" immediately, **When** the operator sends,
   **Then** the command still returns a refusal rather than throwing — a fast negative and a slow
   negative produce the same observable outcome.

---

### User Story 3 - Anyone can see which plane is live, and no plane can be silently substituted (Priority: P1)

An operator, an auditor, or another lane asks the client which plane it is bound to and gets an
answer read off the live object, not off configuration or intent.

**Why this priority**: The whole class of defect this feature exists to close is *a thing that
looks connected and is not*. A client that reports "running" while bound to loopback is the exact
failure the source already warns about. Making plane identity observable is what stops this
feature from re-creating the defect it is closing.

**Independent Test**: Ask the client to report its plane under each selection and with each
misconfiguration; compare the reported plane against the object actually constructed.

**Acceptance Scenarios**:

1. **Given** any plane selection, **When** the operator asks the client to report status, **Then**
   the reported plane name is derived from the live carrier object, and the file plane, wire plane
   and loopback plane are distinguishable by name.
2. **Given** a selection that cannot be satisfied, **When** the client starts, **Then** it does not
   substitute another plane silently; it either refuses, or falls back **and says so on the same
   line as the word "running"**.

---

### User Story 4 - Both planes run at once (Priority: P1 — raised from P2 by ruling Q-G34-03)

An operator runs the client with the file plane and the wire plane bound simultaneously, so that
peers on hosts with a shared volume and peers reachable only over the network both reach this lane,
without two client processes and without two mailboxes.

**Why this priority**: The fleet is mid-migration. Some peers deliver on the volume today and will
deliver on the wire later. Forcing an exclusive choice makes the migration a flag-day; allowing
both makes it incremental. It was drafted P2 on the argument that a single-plane client is already
a working M6 client. **Ruling Q-G34-03 rejected that argument and raised it to P1**, on the
ground that a lane needing both planes would otherwise have to run two client processes and
therefore hold two mailboxes for one lane — a worse defect than the one being fixed. The
de-duplication obligation the P2 rating was protecting is not waived: it is discharged by
mutation proof (FR-023a).

**Independent Test**: With both planes bound, deliver the same logical message on each plane and
observe exactly one durable alert per distinct message id.

**Acceptance Scenarios**:

1. **Given** both planes bound, **When** a frame arrives on either plane, **Then** the client
   records a durable alert for it.
2. **Given** both planes bound, **When** the same message id arrives on both planes, **Then**
   exactly one durable alert exists for that id.
3. **Given** both planes bound and the wire plane fails to bind, **When** the client starts,
   **Then** it runs on the file plane alone, says so, and emits the degraded notice of FR-004b.

---

### Edge Cases

- **A frame whose claimed sender is not the handshake-proven peer.** Refused and counted as a
  security event, never normalized to the peer's real identity and never delivered.
- **A frame larger than the accepted ceiling.** Refused before buffering. A carrier with no
  ceiling is a memory-exhaustion primitive available to anyone who can complete a handshake.
- **Malformed frame bytes.** Refused and counted; the session is not torn down for one bad frame,
  because a peer that can make the receiver drop every other peer's session is a denial-of-service
  primitive.
- **The wire plane is selected but no provider can bind** (no certificate material, port in use,
  provider absent). Refuse and name the reason. This host has already had certificate material
  destroyed four times; "cannot bind" must be a legible message, not a stack trace.
- **A peer connects, completes a handshake, and sends nothing.** The client must not park an
  unbounded-lifetime wait on a shared thread pool. Measured on this host 2026-09-06: pool
  starvation from blocking waits made unrelated timeout-bearing work report "unavailable" with a
  different failing set on every run.
- **The peer disappears mid-frame.** A partially-received frame is never delivered as a complete
  one.
- **Client restarted while alerts are pending.** Pending alerts survive; already-drained alerts do
  not re-appear.

## Requirements *(mandatory)*

### Functional Requirements

**Plane selection and observability**

- **FR-001**: The client MUST allow the operator to select the wire plane for receiving, through
  the same control surface that selects the file plane today.
- **FR-002**: The client MUST report, on start, which plane(s) it is bound to, derived from the
  live carrier object rather than from the requested configuration.
- **FR-003**: The client MUST NOT substitute a different plane for the one requested without
  saying so in the same output that announces it is running.
- **FR-004**: When the wire plane is requested and no listener can bind, the client MUST fall back
  to the file plane rather than exiting, so that a host with damaged certificate material keeps
  receiving. *(Ruling Q-G34-02 → C. Refusing hard was rejected: it forecloses running any receiver
  on such a host, which is strictly worse than today.)*
- **FR-004a**: A fallback MUST be stated **on the same output line that reports the client is
  running**, naming the plane that is actually live, the plane that was requested, and why the
  requested one failed. A fallback that is reported only in a log the operator does not read is a
  silent fallback.
- **FR-004b**: A fallback MUST additionally emit a **fleet-visible degraded notice**, so that
  fleet-wide loss of the wire plane is observable as a count rather than as N individually-honest
  hosts. *(Ruling Q-G34-02 → C, explicitly preferring this over plain loud fallback.)*
- **FR-004c**: When the **file** plane is requested and cannot be bound, the client MUST fail with a
  distinct non-zero exit code and a message naming the reason, and MUST NOT report success. There
  is no lower plane to fall back to, so the FR-004 fallback does not apply.
- **FR-005**: The client MUST expose, for the wire plane, the address it actually bound and the
  provider that bound it, both read from the live handle.

**Sending**

- **FR-006**: The client MUST be able to send a message to a peer over the wire plane, addressed
  by the same `<node>/<actor>` identity convention used for the file plane.
- **FR-007**: A send that cannot be delivered MUST fail with a distinct non-zero exit code naming
  the peer and the address, and MUST NOT report success.
- **FR-008**: A send MUST NOT throw for an unreachable, refusing, or absent peer, regardless of how
  quickly the network says no. Fast negatives and slow negatives MUST produce the same observable
  outcome.
- **FR-009**: A send MUST be bounded in time; it MUST NOT wait indefinitely for a peer that will
  never answer.

**One protocol, two planes**

- **FR-010**: A message MUST have the same on-the-wire shape whichever plane carries it. A
  round-trip through the wire plane and a round-trip through the file plane MUST produce
  byte-identical frames for the same logical message.
- **FR-011**: Both planes MUST present the same receive contract to the receiver state machine, so
  the state machine cannot distinguish them.

**Identity and refusal**

- **FR-012**: On the wire plane, a message's origin MUST be bound to the handshake-proven peer.
- **FR-013**: A frame whose claimed sender is not the handshake-proven peer MUST be refused and
  counted, and MUST NOT be delivered under any identity.
- **FR-014**: A frame exceeding the accepted size ceiling MUST be refused before its body is
  buffered.
- **FR-015**: The client MUST expose a count of refused frames. A carrier that silently drops is
  indistinguishable from one nobody is using.
- **FR-016**: A refusal of one frame MUST NOT terminate delivery for other peers.

**Independence from the agent** (the M6 mandate)

- **FR-017**: Receiving, recording a durable alert, and sending MUST all work with no agent
  process present.
- **FR-018**: An alert recorded while no agent was present MUST still be findable by a different
  process afterwards.
- **FR-019**: Notification to the agent MUST remain non-preemptive: the agent is offered the alert
  at a turn boundary and decides whether to act now or later. The client MUST NOT interrupt the
  agent mid-work, and MUST NOT require the agent's cooperation in order to keep running.

**Concurrency discipline**

- **FR-020**: No unbounded-lifetime wait introduced by this feature may occupy a shared thread
  pool thread.
- **FR-021**: Stopping the client MUST release every thread and socket it acquired, with no
  process-lifetime leak.

**Kernel-managed hosting** (M6-d — IN SCOPE by the reversal of Q-G34-01)

- **FR-024**: The M6 client MUST be hostable as a **supervised child process**, whose lifetime is
  owned by a supervisor rather than by an operator's shell.
- **FR-025**: The supervisor MUST prove the client is alive by a **round-trip request that the
  client answers within a stated budget**. Liveness MUST NOT be inferred from process existence,
  from the client's own status verb, or from an unexpired lease. *(A timer that renews regardless of
  health seats a zombie forever and destroys the very signal the watcher needs: the lapse is the
  feature.)*
- **FR-026**: A client process that is alive but has **stopped answering** MUST be detected as dead
  and terminated, not left running.
- **FR-027**: A transient failure of the liveness channel MUST NOT be misread as the client's death;
  the check MUST distinguish a broken channel over a live client from a dead client.
- **FR-028**: On detected death the supervisor MUST record the death, back off, and restart the
  client; and MUST **stop loudly** rather than restart forever when the failure is classified
  unrecoverable or exceeds a crash-rate threshold.
- **FR-029**: Supervised hosting MUST NOT be a second implementation. It MUST bind the supervision
  capability that already exists in this repo. *(Writing a second supervisor would create a third
  instance of the very defect class this feature exists to close.)*

**Composite plane** (User Story 4 — P1 by ruling Q-G34-03)

- **FR-022**: The client MUST support binding the file plane and the wire plane simultaneously, in
  one process, addressing one mailbox.
- **FR-023**: With both planes bound, exactly one durable alert MUST exist per distinct message id,
  regardless of how many planes carried it.
- **FR-023a**: The de-duplication of FR-023 MUST be **mutation-proven**: neutering it MUST make a
  test fail, and restoring it MUST make that test pass. *(This is the obligation the drafted P2
  rating existed to protect; raising the story to P1 does not waive it, it discharges it by
  proof.)*
- **FR-023b**: De-duplication MUST NOT be the reason a message is lost. A message id seen only once,
  on either plane, MUST always produce exactly one alert — the de-duplicator MUST be proven against
  a negative control that would catch it suppressing a first sighting.

### Key Entities

- **Frame**: one YNET message as it appears on any plane. Carries a message id, a sender identity,
  a signal, and a body. Its encoded form is plane-independent (FR-010).
- **Plane**: a named carrier realizing the receive contract — the file plane (shared volume), the
  wire plane (authenticated QUIC session), the loopback plane (in-process). Plane identity is
  observable (FR-002).
- **Peer identity**: `<node>/<actor>`. On the wire plane it is *proven* by the handshake; on the
  file plane it is only *claimed*.
- **Durable alert**: the record written when a message arrives, which outlives the client process
  and is drained explicitly by the agent.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Starting the client with the wire plane selected results in a listening endpoint and
  a reported provider, measured from a second process, on a host with no shared volume to the peer.
- **SC-002**: A message sent from one process over the wire is recorded as a durable alert by a
  second, separately started process, and the alert is still present after that process exits —
  measured end-to-end, not asserted.
- **SC-003**: For the same logical message, the encoded frame produced for the wire plane and the
  encoded frame produced for the file plane are **byte-identical**. Measured by comparison, with a
  non-empty guard so that two empty encodings cannot compare equal and pass.
- **SC-004**: Every plane the client can bind is reachable from the control surface. Measured by a
  check that enumerates the realizations of the receive contract and the send contract in the
  shipped assembly and finds a control-surface path to each; **zero unreachable realizations**.
  This is the criterion that would have failed before this feature and is the one that keeps the
  defect from recurring.
- **SC-005**: Each refusal rule (wrong sender, oversize, malformed) is proven by neutering it and
  observing the corresponding test fail, then restoring it and observing it pass. A guard that has
  never been shown to fail has not been shown to work.
- **SC-006**: A send to an address where nothing is listening returns a refusal, not an exception,
  in both the slow-negative case (timeout) and the fast-negative case (immediate "unreachable"),
  measured separately.
- **SC-007**: The full client and transport suites are green with test parallelism restored to its
  default, or the reason parallelism must remain disabled is stated and measured rather than
  assumed.
- **SC-008**: With both planes bound and the same message id delivered on each, exactly one durable
  alert exists — and, as the negative control, a message id delivered on exactly one plane also
  produces exactly one alert, so a de-duplicator that suppressed everything could not pass.
- **SC-009**: A client that requested the wire plane and could not bind it reports, on the line that
  says it is running, which plane is live and why the requested one failed; and a fleet-visible
  degraded record exists for that host. Measured by starting a client with the wire requested and
  the ability to bind removed.

- **SC-010**: The M6 client runs as a supervised child: killing it results in a recorded death and a
  restart without operator action, measured by killing the process and observing the restart.
- **SC-011**: A client that is alive but has stopped answering is detected and terminated — measured
  by making it stop answering while leaving the process running, which is the case a
  process-existence check cannot see and is therefore the criterion that proves the check is a real
  one.
- **SC-012**: Supervised hosting adds **no second supervisor**: the count of process-supervision
  implementations in this repo is unchanged by this feature.

## Out of Scope *(set by engineer ruling, not by omission)*

- **The fleet-wide `declared-unconsumed-guard`** — ruled out by **Q-G34-04 → A**. This era builds
  the check for its own assembly (SC-004) and publishes the measured instance as fleet evidence.
  It does not claim the rank-2 roadmap feature; that requires a claim-first broadcast and is a
  separate era.
- **Re-implementing the QUIC transport.** `csharp/ynet_transport` is bound, not rewritten.

## Assumptions

- The QUIC transport in `csharp/ynet_transport` is the transport this adapter uses. It is not
  re-implemented, and no rival transport is introduced. (Fleet broadcasts 2026-09-05T12:50Z and
  2026-09-05T16:00Z both direct lanes to bind the existing implementation rather than author a
  rival client; this feature binds, it does not rewrite.)
- The receive/send contracts (`IYnetInbound` / `IYnetOutbound`) and the receiver state machine are
  existing, shipped, and unchanged by this feature except where a requirement above names a change.
- The agent-notification path (durable spool plus a turn-boundary hook) is existing and shipped.
  This feature does not change its semantics; it only ensures wire-carried messages reach it.
- Kernel-managed hosting of this process (M6-d) is out of scope — see **Out of Scope** above, where
  it is recorded as an engineer ruling with its consequence for this lane's M6 status, not as an
  assumption.
- Certificate/identity material for binding a QUIC listener may be **absent or damaged**. This host
  has had such material destroyed four times, so the fallback path (FR-004/FR-004a/FR-004b) is
  expected to be exercised in practice and is specified as a first-class behaviour, not as an
  error case.
- The two-plane de-duplication in User Story 4 keys on message id. Message ids are assumed unique
  per logical message across planes.
