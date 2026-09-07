<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature Specification: YNET frame field parity across planes

**Feature Branch**: `110-ynet-frame-field-parity`
**Created**: 2026-09-07
**Status**: Draft
**Input**: User description: "YNET frame field parity - the two planes populate Origin, SenderActor and Sequence differently"

---

## Context and measurement

**Measured GAVRIELLA 2026-09-07T18:05Z, at source, on `develop`.** Every claim below cites a
file and line. Nothing here is carried from the roadmap brief without re-measurement — and
re-measuring changed the answer, which is why the rule exists.

The two carriers share the `YnetFrame` envelope type and **encode it identically**. They
**construct** it differently:

| field | file plane `Client/CoopFileCarrier.cs` | wire plane `Client/QuicCarrier.cs` | in brief? |
|---|---|---|---|
| `Origin` | `_self.Identity` (L185) | `_self.NodeId.ToString()` (L335) | yes |
| `Sequence` | `Interlocked.Increment(ref _sequence) - 1` → **0-based** (L186) | `Interlocked.Increment(ref _sequence)` → **1-based** (L336) | yes |
| `SenderNode` | `_self.Node` (L187) | `_self.NodeId.ToString()` (L337) | 🔴 **NO** |
| `SenderActor` | `_self.Actor` — the **sender's** actor (L188) | `_peer.Actor` — the **destination's** actor (L338) | yes |

🔴 **The roadmap brief says three fields diverge. Four do.** `SenderNode` was not in the
brief and was found only by reading both construction sites side by side. A count carried
from a document is a claim; a count taken from the source is a measurement. This spec
treats the discovery of a fourth divergence as evidence that the brief's *count* was
unmeasured, not that its *author* was careless — the brief itself came from a codexreview
finding, one layer removed from the code.

### Why the existing test does not cover this

`ynet_client.tests/FrameParityTests.cs` (era 107) states the correct property in its own
class doc — *"A message must not change shape depending on which plane carried it"* — and
then does not test it. `Sample()` builds **one preconstructed frame** and hands the **same
object** to both encoders:

```
EncodeAsFilePlaneDoes(Sample())   // JsonSerializer.Serialize      → UTF8 no BOM
EncodeAsWirePlaneDoes(Sample())   // JsonSerializer.SerializeToUtf8Bytes
```

So it proves **the two serializers agree** and says nothing whatever about **what the two
carriers put into the frame**. It is not a bad test — it carries the wave-33 non-empty
guard as its own separate `[Fact]`, which is more rigour than most — it is a **mis-scoped**
one. Its name, `The_same_message_encodes_byte_identically_on_both_planes`, is *true*, and
is not the property the class doc promises.

### Why this matters now, and to whom

`@shiras.yngapp` localised at **16:34Z** that frames are lost at the **carrier → QHSM machine
boundary**: the carrier's `delivered` / `_received` counters track 1:1 by construction, but
the liveness snapshot's `FramesAccepted` is the **QHSM machine's** counter. Carrier handed
off ~145; the machine accepted 13. **A machine that keys on `SenderActor`, `Origin` or
`Sequence` will accept on one plane and drop on the other while both carriers report
success.** This feature does not claim to be the cause of that gap — it establishes whether
it *can* be, which is currently unknown and unmeasurable.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — A cross-plane defect reproduces on the other plane (Priority: P1)

A lane debugging a dropped or misrouted message must be able to reproduce it on whichever
plane is convenient. Today a defect that depends on `SenderActor`, `Origin`, `SenderNode` or
`Sequence` appears on one plane and vanishes on the other, so the investigation concludes
"intermittent" and stops. That is the worst available debugging position.

**Why this priority**: It is the whole value of the feature, and it is on the C-23 F-2
auto-fail condition (YNET messaging must work over the wire *and* in memory). Every other
story is a refinement of it.

**Independent Test**: Drive one logical send through each carrier with a test double and
compare the resulting frames field by field. Delivers value alone: even with no field
changed, the divergence becomes a recorded, named, non-negotiable fact instead of a comment.

**Acceptance Scenarios**:

1. **Given** an identical logical send (same self identity, same peer, same signal, same
   body), **When** it is carried by the file plane and by the wire plane, **Then** the test
   reports, per field, whether the two planes agree — and the report is non-empty before any
   equality is asserted.
2. **Given** a field whose divergence has been ruled intentional, **When** the parity check
   runs, **Then** it passes **and** names the field, the two values, and the ruling that
   permits it — so an intentional divergence is visible rather than absent.
3. **Given** a field whose divergence has **not** been ruled, **When** the parity check runs,
   **Then** it **fails and names the field**, rather than being silently excluded.

---

### User Story 2 — A new divergence cannot be introduced unnoticed (Priority: P2)

A future change to either carrier that adds a fifth divergent field must fail a check rather
than be discovered by a codexreview three eras later.

**Why this priority**: This is the difference between a one-off audit and a durable fix
(C-16). The fourth divergence in this very feature was found by hand; the fifth must not
have to be.

**Independent Test**: Add a deliberate divergence to one carrier and confirm the check
fires. A check never observed failing is a check that cannot pass (era 109's rule, applied
here to its author).

**Acceptance Scenarios**:

1. **Given** the parity check is green, **When** a field is deliberately made to diverge on
   one carrier only, **Then** the check fails and names that field.
2. **Given** a new field is added to `YnetFrame` and populated on only one plane, **Then** the
   check fails rather than ignoring the unknown field.

---

### User Story 3 — The protocol meaning of each field is written down (Priority: P3)

A lane reading `SenderActor` must be able to learn what it means without reading two
carriers and guessing which is right.

**Why this priority**: Necessary for a durable fix, but the harness and the recorded
measurement (P1/P2) are correct under **either** answer to the open protocol question, so
this must not block them.

**Acceptance Scenarios**:

1. **Given** the ruling on each field, **When** a lane reads the envelope's documentation,
   **Then** each of the four fields states its meaning, which plane was authoritative, and
   why the other diverged.

---

### Edge Cases

- **The two planes are never both available.** A host with no wire plane must still run the
  parity check — it compares *construction*, which needs no live peer, not *delivery*.
- **Empty-vs-empty.** Two frames that are both empty compare equal. The non-empty guard runs
  first, as its own case, exactly as era 107 already does — that part of 107 is right and is
  kept.
- **`_self.Identity` vs `_self.NodeId` may legitimately differ**, because the wire has a
  handshake-proven Ed25519 identity the file plane cannot have. A parity check that demands
  equality here would be wrong. This is why "ruled intentional" must be a first-class
  outcome and not a suppression.
- **`Sequence` is per-carrier state.** Two carriers in one process hold independent
  counters; the check must compare *basis* (0- vs 1-based), never absolute values.
- **A field ruled intentional later becomes wrong.** The ruling is data the check reads, not
  a comment, so revoking it re-fires the check.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST produce, for one identical logical send, the frame each carrier
  would construct, without requiring a live peer or a bound socket on either plane.
- **FR-002**: The system MUST compare those two frames **field by field** and report per-field
  agreement, never a single aggregate boolean. (C-20: no capability is a bool.)
- **FR-003**: The system MUST assert both constructed frames are non-empty **before** any
  equality or inequality is asserted, as an independently-named case that cannot be skipped
  or reordered away.
- **FR-004**: The system MUST classify each field as **AGREES**, **DIVERGES-RULED** (with the
  ruling identifier and both values), or **DIVERGES-UNRULED**.
- **FR-005**: A **DIVERGES-UNRULED** field MUST fail the check and name the field and both
  values in the failure message.
- **FR-006**: A **DIVERGES-RULED** field MUST pass **and still be reported** with both values,
  so an intentional divergence is visible in the output rather than absent from it.
- **FR-007**: A field present on `YnetFrame` but not covered by any ruling MUST be treated as
  **DIVERGES-UNRULED** if the planes disagree, and MUST NOT be silently skipped — the check
  enumerates the envelope's fields rather than a hand-maintained list.
- **FR-008**: The system MUST record the four measured divergences (`Origin`, `Sequence`,
  `SenderNode`, `SenderActor`) as data with their file-and-line provenance, not as prose in a
  comment.
- **FR-009**: `Sequence` MUST be compared by **basis** (0-based vs 1-based), not by absolute
  value, since the counters are independent per carrier. **Per the Q-110-02 ruling both planes
  are 1-based**, so the expected outcome for `Sequence` is **AGREES**, not `DIVERGES-RULED`.
- **FR-010**: The check MUST be proven to fail: a deliberate divergence introduced into one
  carrier MUST make it fail, and that proof MUST be executed and recorded, not asserted.
- **FR-011**: The system MUST NOT change the *value* any carrier writes into any field
  **except where an engineer ruling authorises it**. **Satisfied for `Sequence` by Q-110-02**
  (file plane 0-based → 1-based). `Origin`, `SenderNode` and `SenderActor` are ruled
  `may-diverge` and MUST NOT have their values changed by this era.
- **FR-013**: The file plane's `Sequence` MUST become 1-based, matching the wire
  (`CoopFileCarrier.cs:186`). The frame filename `{epochMs}.{seq}.{guidN}.frame` MUST remain
  unique — uniqueness is carried by the GUID, and this MUST be asserted rather than assumed.
- **FR-014**: Each `may-diverge` ruling MUST carry its rationale as **data the check reads**,
  not as a source comment, so that revoking a ruling re-fires the check without a code change.
- **FR-012**: The existing era-107 `FrameParityTests` serializer-agreement cases MUST be
  retained, not replaced — they test a real and different property (the two *encoders* agree).
  Their scope MUST be corrected in their own documentation so no future reader mistakes them
  for carrier parity.

### Key Entities

- **`YnetFrame`**: the shared envelope. Fields in scope: `Origin`, `Sequence`, `SenderNode`,
  `SenderActor`, `Signal`, `Body`.
- **Field ruling**: a record binding one field name to an outcome (`must-agree` /
  `may-diverge`), a rationale, and a ruling identifier. Read by the check; not a comment.
- **Constructed-frame pair**: the two frames one logical send yields, one per carrier.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any field of the shared envelope, a reader can determine from the check's
  output alone whether the two planes agree, and if not, what each writes — **without opening
  either carrier's source**.
- **SC-002**: All four currently-measured divergences are reported by the check. A run that
  reports fewer than four is a regression in the check, not an improvement in the code.
- **SC-003**: The check has been **observed failing** against a deliberately introduced
  divergence, and the transcript of that failure is recorded. An acceptance check never seen
  to fail cannot pass.
- **SC-004**: Adding a field to the envelope that is populated on only one plane causes a
  failure naming that field, demonstrated once.
- **SC-005**: The count of divergent fields is produced **by the check**, never typed by a
  human into a document. (Three lanes have published a wrong count from eye-parsing today;
  this spec's own opening measurement corrected a count carried from a brief.)
- **SC-006**: Zero fields change value in this feature. Verified by diffing both carriers'
  construction sites before and after: the construction sites are byte-identical at ship.

---

## Clarifications

### Session 2026-09-07 — engineer rulings

- **Q-110-01 — What does `SenderActor` mean? → RULED: `may-diverge`, declared per-plane.**
  Both populations are legitimate. The file plane's `SenderActor` is the **sender's** actor;
  the wire plane's is the **destination's**, because the two planes have different addressing
  models. **No value changes on either carrier.** The divergence is recorded as an explicit
  `may-diverge` ruling with its rationale, and the parity check reports it as
  **DIVERGES-RULED** — visible in every run, never suppressed and never absent.
  🔴 **The residual risk is named rather than closed:** a field whose meaning depends on the
  carrier is exactly the condition that makes a cross-plane defect unreproducible. The ruling
  accepts that cost; SC-001 is what keeps it survivable, because a reader can now learn the
  per-plane meaning from the check's output without opening either carrier.

- **Q-110-02 — Is `Sequence`'s basis a ruling or a defect? → RULED: standardise on 1-based.**
  The **file plane changes** from `Increment(ref _sequence) - 1` (0-based) to
  `Increment(ref _sequence)` (1-based), matching the wire. First message is #1.
  **Blast radius, re-measured at 18:20Z after the ruling rather than assumed when asking:**
  the file-plane `Sequence` has exactly **one** consumer — a component of the frame filename
  `{epochMs}.{seq}.{guidN}.frame` (`CoopFileCarrier.cs:194`), whose uniqueness is carried by
  the GUID, not the sequence. Nothing parses the sequence back out of a filename. The wire
  plane's dedup key `{authenticatedPeer}#{frame.Sequence}` (`QuicCarrier.cs:275`) is on the
  **other** carrier and is untouched. When this question was put, this lane rated changing the
  load-bearing file plane the riskier of the two directions; **the measurement says otherwise
  and the correction is recorded here rather than left standing.**

- **Q-110-03 — Is `Origin`/`SenderNode` divergence intentional? → RULED by extension of
  Q-110-01: `may-diverge`.** The wire has a handshake-proven Ed25519 identity the file plane
  cannot have. Recorded with that rationale; reported as **DIVERGES-RULED**.

**Net effect on scope:** three of the four divergences become declared `may-diverge` rulings
with **zero** code change; one (`Sequence`) becomes a **one-line change on the file plane**.
FR-011's precondition — "not until ruled" — is now **satisfied**, so the `Sequence` change is
in scope for this era.

---

## Assumptions

- Both carriers can be constructed in a test with a stub self-identity and stub peer, without
  a live socket or a real inbox. If false, FR-001 needs a seam and that is in scope.
- `YnetFrame` remains one shared type across both planes. If the planes ever fork the type,
  this feature's premise changes and the spec must be revisited rather than patched.
- The four divergences measured at 18:05Z on `develop` are the complete current set. FR-007
  exists precisely because this assumption will rot — the check enumerates rather than trusts it.
- Deciding field *meaning* is a fleet protocol decision (C-18/C-19: raise, do not take another
  lane's slice). This lane builds the instrument and records the reading.
- No iroh or transport slice is claimed. Sidecar `@gavriella.ospark` era 043; carrier
  `@gavriella.qhstate`; listener `@shiras.glpnet`. Claim published 18:00Z to 148/148 lanes.
