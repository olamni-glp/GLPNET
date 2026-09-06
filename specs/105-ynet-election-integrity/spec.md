<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: YNET election integrity — one host one vote per term, one franchise one submission, on the verified delegation

**Feature Branch**: `105-ynet-election-integrity`
**Created**: 2026-09-05
**Status**: Draft
**Input**: User description: "YNET election integrity: one host one vote per term, one franchise one submission, on the verified delegation"

## Why this specification exists, and what it replaces

A fleetwide leader must be electable from records that give **one answer**, not an answer per
reader. Today they do not, and the reason is **not** the one this lane published this morning.

**A withdrawn premise, recorded rather than deleted.** At 13:14Z this lane broadcast a P0 asserting
that `actor != voter` in a vote record violated engineer ruling G30-02 and left the electorate
undefined, and shipped an audit enforcing `actor == voter`. **Both were wrong.** The voter
signature covers a smaller declared field set — not the outer envelope — so verifying it against
the envelope makes a sound delegation look forged; and the voter id is the SHA-256 of the key that
signed the delegation, which makes it unforgeable by construction. `gavriella.ospark` published
this proof at **08:49Z**, four hours *before* the P0. Reproduced independently here at 14:20Z:
**all five delegations in the oplog verify and are key-bound; zero forgeries.** The P0 was retracted
in full at 14:35Z and the audit was corrected.

**What remains after the retraction is real, and it is what this feature is about.** Once votes are
tallied on the verified franchise, two defects survive that delegation does not touch, both
measured on the live oplog:

- **F3 — one host, many node ids, many votes.** In term 1 host `shiras` backed **two different
  candidates** through two node ids (`1994d86e` `shiras.yngenios-app` and `1b23876b`
  `shiras.yngraw`, each self-voting). Every host holds at least two node ids and `shiras` holds
  five. **Term 2 avoided this by luck, not by rule.**
- **F4 — one franchise, several submissions.** In term 2 the `gavriella` franchise `88cb0251`
  submitted **twice** — directly at 09:52Z, then by delegation from `gavriella.yngcor` at 13:55Z.
  Both named the same candidate, so it deduplicates to one and did no harm. **Nothing prevents two
  submissions from naming different candidates.**

## User Scenarios & Testing *(mandatory)*

### User Story 1 — A lane can determine, without argument, whether a term elected anyone (Priority: P1)

A lane preparing to act on a leader's instruction needs to know whether that leader was elected.
Today it must choose an electorate key first, and the choice decides the answer: on the live oplog,
keying term 2 on the submitting actor, on the claimed voter, or on the host gave **three different
outcomes**, one of which seated a leader. The lane runs one audit and gets one tally, with every
record's franchise shown and every disqualification named.

**Why this priority**: Every other election behaviour depends on the tally being a number rather
than a choice. Without it, two honest lanes reading the same records disagree and neither is wrong.

**Independent Test**: Run the audit against the live oplog on any host and against the fixture;
the tally and the findings are identical on every host, and the fixture's findings are reproduced
exactly.

**Acceptance Scenarios**:

1. **Given** a vote carrying a delegation proof that verifies and is key-bound, **When** the tally
   resolves its franchise, **Then** it is counted for the **delegating** identity, not the submitter.
2. **Given** a vote carrying **no** delegation proof, **When** the tally resolves its franchise,
   **Then** it is counted as a **direct** vote by its actor, and is not treated as malformed.
3. **Given** a vote whose delegation proof does **not** verify, **When** the tally resolves its
   franchise, **Then** the vote is **REFUSED and excluded** — and is **not** counted for the actor.
4. **Given** a franchise with no `hello` anywhere, **When** the tally resolves it, **Then** the vote
   is excluded and the franchise is named in the findings.

---

### User Story 2 — One host cannot vote twice for different candidates in one term (Priority: P1)

A host owning several node ids can today cast several votes in one term. In term 1 one host backed
two different candidates. Under a host electorate that is one elector voting two ways.

**Why this priority**: It is the only measured defect that can change **which candidate wins**, and
it is invisible unless votes are grouped by host after franchise resolution.

**Independent Test**: Run the audit over term 1; F3 fires and names the host and both candidates.
Run it over term 2; F3 does not fire.

**Acceptance Scenarios**:

1. **Given** a term in which one host's franchises name two different candidates, **When** the
   audit runs, **Then** F3 fires, names the host and every candidate, and the run exits non-zero.
2. **Given** a term in which each host names at most one candidate, **When** the audit runs,
   **Then** F3 does not fire.
3. **Given** several node ids on one host all naming the **same** candidate, **When** the tally
   runs, **Then** the host contributes **exactly one** vote to that candidate.

---

### User Story 3 — A repeated submission by one franchise is deduplicated, and said out loud (Priority: P2)

One franchise submitted twice in term 2 — once directly, once by delegation. Both named the same
candidate. The tally must count one, and must not pass over the repetition in silence.

**Why this priority**: Harmless today, and only because the two submissions agreed. The rule must
exist before they disagree, but no live term is currently decided by it.

**Independent Test**: Run the audit over term 2; F4 fires, names both submission timestamps, and
states that they deduplicate to one.

**Acceptance Scenarios**:

1. **Given** one franchise submitting twice for the **same** candidate, **When** the tally runs,
   **Then** it contributes **one** vote and F4 is reported with both timestamps.
2. **Given** one franchise submitting for **two different** candidates in one term, **When** the
   audit runs, **Then** it is reported as a **conflict**, and the tally does **not** silently pick one.

---

### Edge Cases

- **A signature-verification library is unavailable.** The audit **refuses and exits 2**. It must
  never report an unverified tally: every delegation would be dropped, and a term that met quorum
  would be reported as failing to.
- **A record appears in several node files.** Deduplicate by record id as part of reading. The live
  oplog genuinely contains such duplicates; counting them would inflate a tally.
- **An unparseable line.** Reported as a finding. Never skipped silently.
- **No vote records at all.** Exit 2, never 0 — absence of votes is not conformance.
- **A term spanning more than one roster epoch** (live in term 2, where one record predates the
  field). Reported; it does not by itself disqualify a term.
- **A candidate voting for itself.** Recorded and visible. Whether it counts is a rule question
  this feature does not decide.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST resolve each vote's franchise as: the **actor** when no delegation
  proof is present; the **verified voter** when a proof is present, verifies, and is key-bound; and
  **REFUSED** when a proof is present and fails either test.
- **FR-002**: The system MUST NOT fall back to the actor when a delegation proof fails. A forger
  could otherwise strip a failing signature and have the vote counted as their own.
- **FR-003**: The system MUST verify a delegation signature over the declared voter field set, not
  over the outer record envelope.
- **FR-004**: The system MUST require the voter identity to equal the digest of the public key that
  signed the delegation, and MUST reject the vote when it does not.
- **FR-005**: The system MUST group resolved franchises by **host** and count each host at most once
  per candidate per term.
- **FR-006**: The system MUST report **F3** when one host's franchises name more than one candidate
  in a term, naming the host and every candidate.
- **FR-007**: The system MUST report **F4** when one franchise submits more than once in a term,
  naming every submission, and MUST count it once when all submissions agree.
- **FR-008**: The system MUST report a **conflict** when one franchise's submissions in a term name
  different candidates, and MUST NOT silently choose between them.
- **FR-009**: The system MUST exclude a franchise with no `hello` record and report **F2**.
- **FR-010**: The system MUST deduplicate records by record id while reading.
- **FR-011**: The system MUST exit non-zero when any F1, F2 or F3 finding is present, and MUST exit
  2 — never 0 — when it cannot measure (no records, unreadable root, or no verification library).
- **FR-012**: The system MUST ship a positive control that constructs real signing keys and proves
  a valid delegation is **counted**, a forged one is **refused and not downgraded**, and that each
  finding can fire. The control MUST run in the repository's test suite.
- **FR-013**: The audit MUST be runnable by any lane against any oplog root without installation
  beyond a signature library, and MUST print each record's resolved franchise so a disagreement can
  be located rather than merely asserted.

### Key Entities

- **Vote record** — a term, a candidate, a submitting actor, and optionally a delegation proof
  (voter identity, voter public key, voter signature). Carries host and lane when the emitter
  supplies them.
- **Hello record** — binds a node id to a host and a lane. The only route from a franchise to a
  host, and therefore to the electorate.
- **Franchise** — the identity a vote is counted for after resolution: the actor, the verified
  voter, or none.
- **Term** — the unit within which quorum is assessed. Hosts, not lanes, are the electors.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Two lanes on different hosts, auditing the same records, produce **identical**
  tallies and findings — no reader-dependent outcomes remain.
- **SC-002**: Every delegated vote in the live records is counted for its delegating identity, and
  every vote whose proof fails is excluded — currently **5 of 5** verified and key-bound.
- **SC-003**: For each live term the audit reports **exactly one** of: a candidate meeting quorum,
  or no candidate meeting quorum — never a result that depends on the electorate key chosen.
- **SC-004**: A host holding several node ids contributes **at most one** vote per candidate per
  term, and any host naming more than one candidate is reported before the tally is quoted.
- **SC-005**: The positive control fails if any finding stops firing, or if a forged delegation is
  ever counted — so a regression is visible in the suite rather than in a later election.
- **SC-006**: A run that cannot measure is distinguishable from a clean run by exit code alone.

## Assumptions

- **Hosts are the electors** and quorum is 3 of 4, per `RULINGS-20260905T0050Z-shiras-hatzinor`.
  If that changes, only the grouping key changes; FR-001..FR-004 are unaffected.
- **The delegation scheme itself is sound.** This feature verifies signatures and key binding only.
  **Key distribution, revocation and replay are out of scope and have not been reviewed.**
- **The oplog is the record of truth** for votes, and is readable as newline-delimited records under
  one root.
- **This lane does not own the emitter or the board's tally.** GLPNET contains no vote emitter. The
  paired production fix — gating the board's tally on the verified franchise — belongs to the owner
  of the election code, where the verification routine **already exists and is not called**. This
  feature delivers the **rules and the audit**, which is what lets that fix be verified independently.
- **A signature library is available** where the audit runs. Where it is not, the audit refuses; it
  does not degrade.
