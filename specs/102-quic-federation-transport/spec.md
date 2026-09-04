<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: QUIC federation transport for the ynet oracle

**Feature Branch**: `102-quic-federation-transport`
**Created**: 2026-09-04
**Status**: Draft
**Input**: User description: "QUIC federation transport for the ynet oracle: host a real QUIC listener for broker/guardian/oracle on each host, exchange SPKI pins, and prove a board op crosses between two REAL machines with the fold converging exactly once under a namespaced (space_id, era_counter, host_id) term"

## Why this feature exists

Every lane on every host currently reads a **different board**. Each host has a working local
oracle, but the four local oracles cannot see one another, so a work-package claim made on one host
is invisible on the other three. Two lanes have already claimed overlapping work without either
being able to detect it, and one lane emitted an operation under another lane's identity with no
mechanism to notice.

The oracle itself reports the cause: the four-host board is blocked because **no inter-host
transport is running**. This feature delivers that transport and proves it with a board operation
crossing between two real machines.

It is deliberately scoped to **transport plus the ordering rule that makes a merge safe**. It does
not elect a leader and does not implement PBFT — those sit on top of this and are blocked by its
absence.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A lane on one host sees a claim made on another host (Priority: P1)

A lane working on host A claims a work package through its local oracle. A lane on host B, before
starting the same work, reads its own local oracle and sees that the package is already claimed,
with the claiming lane and the time attributed correctly. Neither lane consults a shared drive, a
mailbox file, or a human.

**Why this priority**: This is the whole point of the feature and the directive's stated goal — one
board for all lanes on all hosts. Every other story is a supporting property of this one. It is also
the smallest thing that delivers real value: even without leader election, duplicate-work detection
across hosts is immediately useful and is a defect the fleet is demonstrably suffering from today.

**Independent Test**: Claim a package on host A's oracle, then read host B's oracle and assert the
claim is present with host A's attribution. Fully testable with two machines and no other story
implemented.

**Acceptance Scenarios**:

1. **Given** two hosts each running a local oracle with the transport enabled, **When** a lane on
   host A appends a claim operation, **Then** a lane reading host B's oracle sees that claim
   attributed to host A's node identity within the convergence window.
2. **Given** the same setup, **When** the operation is delivered to host B more than once (a
   retrying link redelivers it), **Then** host B's fold contains the operation **exactly once**.
3. **Given** a lane on host B that claims the same package concurrently, **When** both operations
   have converged, **Then** both hosts independently report the package as **CONTESTED** and neither
   silently wins.

---

### User Story 2 - An operator can tell whether federation is actually working (Priority: P1)

An operator on any host asks the oracle whether it is federated, and gets an answer that
distinguishes **"the stack is supported"**, **"a listener is bound"**, **"a peer is admitted"** and
**"an operation has actually crossed"** as four separate states, rather than collapsing them into
one green light.

**Why this priority**: Equal-first because without it the feature cannot be trusted. This estate has
recorded **six false greens in one week**, including one that survived CI. The specific misreads
that motivated this story are matters of record: "no listening TCP port" was read as "no QUIC" when
QUIC is UDP and has no TCP socket by design; a `ping` timeout was read as "host is down" when ICMP
was merely filtered; and a mechanism proof taken between two roots **on one machine** was at risk of
being cited as cross-host federation.

**Independent Test**: Run the status surface on a host with no peers configured and assert it
reports *bound but no peer admitted* rather than *federated*. Testable without a second machine.

**Acceptance Scenarios**:

1. **Given** a host where the QUIC stack is supported but no listener is running, **When** the
   operator asks for federation status, **Then** the answer distinguishes *supported* from *bound*
   and does not claim readiness.
2. **Given** a host with a bound listener and an empty peer-pin set, **When** the operator asks,
   **Then** the answer states that **no peer can be admitted** and names the missing pins.
3. **Given** a host that has never received a peer operation, **When** the operator asks, **Then**
   the answer reports **no operation has crossed**, and never infers crossing from reachability.

---

### User Story 3 - A board merge cannot be poisoned by a stale or fabricated term (Priority: P1)

When two hosts' boards converge, leadership-bearing operations are ordered by a namespaced term, so
an operation carrying an enormous term from a different term-space cannot outrank a legitimate one,
and a host that was switched off for a week gains no ordering advantage from having been absent.

**Why this priority**: Equal-first because it is the only part of this feature that is
**irreversible if got wrong**. Term ordering is monotone: once boards fold, no later operation can
lower a winning term. A leader-claim operation carrying a wall-clock-derived term of 5,961,694
exists on a live board today, its emitting code has since been deleted, and the operation still
votes. Merging without this rule installs that fossil as the permanent winner. The rule is therefore
a **precondition of the first merge**, not a follow-up to it.

**Independent Test**: Fold a synthetic log containing a foreign-space operation with a maximal term
against a legitimate operation, and assert the legitimate one wins. Fully testable offline with no
network.

**Acceptance Scenarios**:

1. **Given** two operations whose terms belong to different term-spaces, **When** the fold orders
   them, **Then** they are **not comparable by term** and the foreign-space operation cannot win by
   term magnitude.
2. **Given** a host that has been offline for a prolonged period, **When** it reconnects and
   participates, **Then** its term has **not advanced** as a result of elapsed time alone.
3. **Given** an operation already recorded on the board that is later determined to be faulty,
   **When** it is corrected, **Then** the correction is an **additional** operation and the original
   is **not removed**, because removal is indistinguishable from suppression on an append-only board.

---

### User Story 4 - A reachable listener is not an open one (Priority: P2)

An unknown party who can reach the federation port cannot inject, read, or influence board state.
Admission is by mutually verified node identity, and an unrecognised dialer is refused before any
board data is exchanged.

**Why this priority**: P2 because the safe default already holds — an empty peer set admits nobody,
so the system fails closed before this story is implemented. It is a first-class story rather than a
footnote because the port is being deliberately opened, and because two of the four hosts have been
measured presenting **two network identities each**, so any admission decision keyed on an address
would over-count participants.

**Independent Test**: Dial the listener from a client whose identity is not in the pin set and
assert the connection is refused before any board operation is transferred.

**Acceptance Scenarios**:

1. **Given** a bound listener with a configured peer set, **When** an unrecognised party dials,
   **Then** the connection is refused and no board data is sent.
2. **Given** a dialer that is in the peer set, **When** it connects, **Then** **both** parties verify
   the other's identity before board data flows.
3. **Given** a host reachable at more than one network address, **When** it participates, **Then** it
   is counted as **one** participant, because identity is not derived from address.

---

### Edge Cases

- **A peer is unreachable at connect time.** Federation degrades to local-only operation and says so
  explicitly; it does not report success, and it does not block local oracle use.
- **A peer's identity does not match its pin.** The connection is refused and the mismatch is
  recorded as a distinct, named condition — never folded into a generic connection error, because a
  pin mismatch and an unreachable host demand opposite responses.
- **A host is reachable by name but the name resolves only to a non-routable address.** All four
  hosts currently resolve to link-local addresses by name while being routable by literal address, so
  a name-resolution failure MUST be reported as such and MUST NOT be reported as a transport failure.
- **The same operation arrives twice.** The fold counts it once. Redelivery is certain on a retrying
  link, so a fold that has not been tested against deliberate redelivery is untested, not convergent.
- **A peer sends an operation whose term-space is unknown.** It is retained and reported as
  unordered rather than being coerced into the local space or silently dropped.
- **Clocks disagree between hosts.** No ordering decision depends on wall-clock time, so this has no
  effect on which operation wins.
- **The listener cannot start because the host's software policy refuses to load it.** This is
  reported as a distinct startup failure naming the policy, because it presents as a healthy build
  and a passing test suite followed by a daemon that never runs.

## Requirements *(mandatory)*

### Functional Requirements

**Transport**

- **FR-001**: Each host MUST be able to host a listener that accepts federation connections from the
  other hosts, bound to an address reachable by peers rather than to a loopback-only address.
- **FR-002**: The listener's bind address, port, and peer set MUST be operator-configurable without
  rebuilding, and the configuration MUST be readable back for verification.
- **FR-003**: A host MUST be able to dial a peer by literal network address, not solely by name.
- **FR-004**: Enabling federation MUST NOT be required for local oracle operation; with federation
  disabled or unreachable, the local oracle MUST continue to serve its own lanes unchanged.

**Admission and identity**

- **FR-005**: Both parties to a federation connection MUST verify the other's identity before any
  board operation is exchanged.
- **FR-006**: A party whose identity is not in the configured peer set MUST be refused, and the
  default empty peer set MUST admit nobody.
- **FR-007**: A participant MUST be identified by a stable node identity that is independent of its
  network address, and MUST count as exactly one participant regardless of how many addresses it
  answers on.
- **FR-008**: An identity mismatch MUST be reported as a distinct condition, separately from
  unreachability and from a generic transport error.

**Board convergence**

- **FR-009**: An operation appended on one host MUST appear in every federated peer's fold with its
  originating participant correctly attributed.
- **FR-010**: The fold MUST include a redelivered operation **exactly once**.
- **FR-011**: Federation MUST be additive: it MUST NOT remove, rewrite, or reorder operations already
  present in a peer's log.
- **FR-012**: Two hosts that have exchanged the same set of operations MUST produce identical folds,
  independent of the order in which the operations arrived.

**Term ordering (precondition of any merge)**

- **FR-013**: A leadership-bearing operation MUST carry a term consisting of a term-space identifier,
  a counter, and the originating participant's identity.
- **FR-014**: Terms MUST be compared **only within the same term-space**; terms from different spaces
  MUST NOT be ordered relative to one another by magnitude.
- **FR-015**: The counter MUST advance only on a leadership event, and MUST NOT advance as a function
  of elapsed time.
- **FR-016**: An operation from an unrecognised term-space MUST be retained and reported as unordered
  rather than dropped or coerced.
- **FR-017**: A faulty operation MUST be correctable only by appending a superseding operation; the
  system MUST NOT provide a means of deleting an operation from the log.
- **FR-018**: The system MUST refuse to merge a peer's board when either side is not term-space
  aware, rather than merging under the older ordering rule.

**Observability — states must stay distinguishable**

- **FR-019**: The status surface MUST report *stack supported*, *listener bound*, *peer admitted*,
  and *operation received from a peer* as four separately-reported states.
- **FR-020**: The status surface MUST NOT infer any later state from an earlier one; in particular it
  MUST NOT report federation working on the basis of reachability alone.
- **FR-021**: A state that could not be measured MUST be reported as **unknown**, never as a negative
  result. An unmeasured condition and a measured-absent condition MUST be distinguishable by the
  reader.
- **FR-022**: A proof obtained between two participants **on the same machine** MUST NOT be reported
  as evidence of cross-host federation.
- **FR-023**: The startup path MUST report a refusal by host software policy as a distinct named
  failure rather than as a generic startup error.

**Operator safety**

- **FR-024**: Opening host reachability MUST be scoped to the local network and the single federation
  port; it MUST NOT require disabling host protections.
- **FR-025**: Every configuration change made to enable federation MUST be reversible by a documented
  action, and that action MUST be recorded alongside the change that required it.

### Key Entities

- **Participant**: A host taking part in federation. Has a stable identity independent of address,
  one or more reachable addresses, and an admission credential. Counts once regardless of address
  count.
- **Peer set**: The participants a given host will admit. Empty by default, meaning admit nobody.
- **Board operation**: An append-only record carrying its originating participant, a unique
  identifier used for exactly-once folding, and — when leadership-bearing — a term.
- **Term**: An ordering value comprising a term-space, a counter and an originating participant.
  Comparable only within a space.
- **Term-space**: A named ordering universe. Operations in different spaces are incomparable by term.
- **Fold**: The deterministic function from a set of operations to current board state. Order- and
  duplicate-independent.
- **Federation status**: The four independently-measured states of FR-019, each with an explicit
  *unknown* value.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A claim made on one host is visible, correctly attributed, on a second **physically
  separate** host, with no shared filesystem involved in the transfer.
- **SC-002**: An operation delivered twice appears exactly once in the receiving fold, verified by a
  test that deliberately redelivers it.
- **SC-003**: Two hosts holding the same operation set produce byte-identical folds regardless of
  arrival order.
- **SC-004**: A dialer not in the peer set is refused in 100% of attempts, with no board data
  transferred, verified by a negative-control test that would fail if admission were open.
- **SC-005**: A term from a foreign term-space never wins an ordering decision, verified against a
  synthetic operation carrying a maximal term value.
- **SC-006**: A participant reachable at multiple addresses is counted exactly once.
- **SC-007**: For each of the four federation states, a positive control (the state is genuinely
  reached) and a negative control (it is genuinely not reached) produce **different** reported
  results — no state may be reported identically in both cases.
- **SC-008**: An operator following the written procedure enables federation between two hosts and
  observes a crossed operation, without needing to disable a host protection.
- **SC-009**: Every configuration change made to enable federation can be undone by following the
  recorded reversal, returning the host to its prior state.
- **SC-010**: A deliberately corrupted or absent measurement is reported as *unknown* and never as a
  clean negative, verified by a test that removes the ability to measure.

## Assumptions

- The four hosts remain on a single local network segment and are directly routable to one another.
  Measured true at time of writing; if it ceases to hold, FR-003's literal-address dialling becomes
  insufficient and this assumption must be revisited.
- Hostname resolution between the hosts is **not** reliable for federation purposes; configuration is
  therefore expected to carry literal addresses. This is an observed condition, not a preference.
- The existing per-host oracle remains the interface lanes use. This feature federates it; it does
  not replace it, and a second oracle must not be introduced.
- The existing node-identity scheme already present in the codebase is reused. No second identity
  scheme is introduced by this feature, and the unresolved question of which of the estate's two
  authentication models is authoritative is **out of scope** here — this feature depends only on
  having *a* stable identity, not on that ruling.
- Development-grade admission credentials are acceptable for the first cross-host proof, on the
  understanding that they are throwaway and that production identity is a separate decision.
- Leader election, PBFT, and the fleet coordinator are **out of scope**. They consume this transport
  and are blocked by its absence; nothing here elects anything.
- The board's existing append-only semantics and its existing fold are reused rather than redesigned.
- Host software policy on at least one target host refuses to load unsigned newly-built binaries;
  satisfying that policy is a dependency of this feature but its resolution is tracked separately.

## Dependencies

- A per-host oracle exists and is operable locally on each participating host. Measured present.
- A transport implementation with mutual identity verification exists in the codebase and binds on at
  least one host. Measured present and bound; it has not been run between two machines.
- A term-space-aware fold must exist before the first cross-host merge. This is the ordering rule of
  FR-013–FR-018 and is a **hard precondition**, not a parallel task.
- Host reachability configuration is an authorised operator action, recorded per FR-025.

## Out of Scope

- Electing a leader; implementing PBFT; the fleetwide coordinator; the fleetwide signature verifier.
- Replacing or duplicating the existing per-host oracle.
- Resolving which of the estate's two authentication models is the board of record.
- Any change to host software policy; that dependency is satisfied elsewhere.
- Federating more than two hosts in the first delivery. Two hosts prove the mechanism end-to-end;
  extending to four is configuration, and the spec's requirements are written to hold for four.
