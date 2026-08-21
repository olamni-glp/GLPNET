<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: YNET Consolidation

**Feature Branch**: `065-ynet-consolidation`
**Created**: 2026-08-03
**Status**: Draft
**Input**: User description: "YNET--consolidation"

## Context

Six open roadmap items were audited against the live repo by a three-role blind
team (3rtask run `20260803T134739Z-fa8a`: 61 attributed claims, 54 CONFIRMED by
a cross-provider critic, 3 REFUTED, 4 claim/method ESCALATEs open). The audit
found the six items are not six independent gaps: one is already built and only
needs finishing (durable-listener-service-box, feature 064), three sit on
shipped machinery and are additive (batch roadmap advance + CalVer
normalisation, atomic toolchain installs, coordination optimisation), and two
are genuine build-new pre-specification work (YNET naming resolver, YNET
battery-budget policy). This feature consolidates them into ONE workstream
executed in the evidence-derived dependency order, instead of six disconnected
items. Full attributed evidence: `.specify/3rtask/runs/20260803T134739Z-fa8a/`
(machine-local, gitignored).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The already-built service box is finished and shipped, not rebuilt (Priority: P1)

A fleet operator sees `durable-listener-service-box` on the roadmap and must
not treat it as missing work: it is code-complete-unshipped (9/9 functional
requirements evidenced, 11/14 tasks done). The consolidation's first step is a
verification gate: confirm the service box has shipped and closed; if it has
not, finish its remaining tail (suite wiring, quickstart verification, full
non-regression gate, plus the audit's finishing items: restore the
replay-idempotence unit test, run the history drill at replay scale N=100, and
settle the per-link replay question) and ship it.

**Why this priority**: Every later item either builds on the shipped state or
was mis-inventoried because this item looked "missing". Correcting the
inventory error first changes what all the rest of the work is.

**Independent Test**: Check the release ledger: the service-box feature shows a
shipped tag and a close record. If yes, the gate passes with no work; if no,
the remaining tail is executed and then the same check passes.

**Acceptance Scenarios**:

1. **Given** the service box is already shipped and closed when this feature is
   implemented, **When** the gate runs, **Then** it records "already satisfied"
   with the evidence (tag + close record) and performs no rebuild.
2. **Given** the service box is unshipped, **When** the gate runs, **Then** the
   remaining tail items are completed, the full non-regression suite is green,
   and the feature is shipped and closed before any P2+ work starts.

---

### User Story 2 - Batch roadmap advance and CalVer normalisation (Priority: P2)

A fleet operator advancing many roadmap features after a wave close must issue
one advance per feature id today. They can instead advance a set of features in
one operation and receive a per-feature outcome report. Separately, installed
toolchain version directories carry inconsistent CalVer strings; after this
work every version identifier follows one normal form.

**Why this priority**: The audit's builder sequenced this first among the
toolchain items: small, purely additive, with working precedents already
shipped. It removes the largest recurring operator toil (wave closes touch
dozens of features).

**Independent Test**: Advance three features in one batch invocation and
verify each reports its own outcome; scan all installed version directories
and count zero non-conforming version strings.

**Acceptance Scenarios**:

1. **Given** N promoted/shipped features, **When** the operator advances them
   as one batch, **Then** each feature advances exactly as it would have
   individually and the report lists each id with its result.
2. **Given** a batch where one id is invalid, **When** the batch runs, **Then**
   valid ids still advance, the invalid id is reported as failed, and nothing
   is silently skipped.
3. **Given** the installed toolchain versions, **When** normalisation runs,
   **Then** all version identifiers conform to the single declared CalVer form
   and a report lists what was renamed.

---

### User Story 3 - Atomic toolchain installs (Priority: P3)

A fleet operator installing or upgrading the toolchain on a host must never be
left with a half-installed, broken environment: an interrupted or failed
install leaves the previously working installation fully operational, and a
completed install passes a smoke verification before it becomes the active
version. A rollback path returns to the prior version.

**Why this priority**: Prerequisites are already shipped (environment lock,
doctor, husk); the reusable idioms exist (the AOT smoke harness, the
atomic-rename deployment idiom). It protects all three hosts but depends on
nothing in P4+.

**Independent Test**: Kill an install mid-flight and verify the prior version
still works; complete an install and verify smoke checks ran before
activation; roll back and verify the prior version is active again.

**Acceptance Scenarios**:

1. **Given** a working installed version, **When** an install of a new version
   is interrupted at an arbitrary point, **Then** the previously active
   version remains fully operational.
2. **Given** a completed new-version install, **When** activation is attempted,
   **Then** activation happens only after the smoke verification passes, and a
   failed smoke verification leaves the prior version active.
3. **Given** an activated new version, **When** the operator rolls back,
   **Then** the prior version is active and passes the same smoke check.

---

### User Story 4 - YNET naming resolver is specified before it is built (Priority: P4)

A distributed-GLP developer needs names resolved to network endpoints in the
YNET overlay. Today no naming/resolution/discovery primitive exists anywhere in
the transport stack, and an adjacent YNET overlay spec directory exists ONLY on
the unmerged branch `origin/051-ynet-transport`. This story produces a
reconciled specification: it inventories what the 051 branch already defines,
records what is adopted/superseded, and only then specifies the resolver — it
does not duplicate unmerged work, and it does not implement anything until the
open engineer ruling on the resolver's output shape (versus the literal
`ep(Host,Port)` seam in the existing programs) is recorded.

**Why this priority**: Genuine build-new work with an addressing/identity
surface; the audit rated it the higher-risk YNET item and it gates the battery
policy's discovery interactions.

**Independent Test**: The reconciliation note exists and maps every 051-branch
YNET artifact to adopted/superseded/out-of-scope; the resolver spec exists;
zero artifacts duplicate 051-branch content; the output-shape ruling is
recorded before any implementation task is generated.

**Acceptance Scenarios**:

1. **Given** the unmerged `origin/051-ynet-transport` branch, **When** the
   reconciliation runs, **Then** every YNET spec artifact on that branch is
   classified (adopt / supersede / out-of-scope) with rationale.
2. **Given** the reconciliation, **When** the resolver spec is written, **Then**
   it cites the reconciliation for every overlapping concept and introduces no
   duplicate of unmerged content.
3. **Given** the open output-shape escalate, **When** implementation is
   proposed, **Then** it is blocked until the engineer's ruling is recorded.

---

### User Story 5 - YNET battery-budget policy is specified before it is built (Priority: P5)

A mobile/edge YNET node operator needs the overlay to respect an energy
budget. No energy or duty-cycle concept exists in any transport spec today.
This story produces the policy specification: what an energy budget is, how a
node declares one, and how overlay behaviour (listening, dialing, relaying)
degrades under budget pressure.

**Why this priority**: Genuine build-new with no existing precedent in the
repo; depends on the resolver spec for its discovery interactions.

**Independent Test**: The policy spec exists, defines budget declaration and
each degraded behaviour testably, and cites the resolver spec at every
discovery touchpoint.

**Acceptance Scenarios**:

1. **Given** no existing energy concept, **When** the policy spec is written,
   **Then** every behavioural rule is stated testably (budget in → observable
   behaviour out) with no implementation mandated.

---

### User Story 6 - Coordination optimisation retargeted (Priority: P6)

A fleet operator benefits from the existing offline optimisation stack, which
today optimises only the refinement engine. After this story, the same
optimisation loop can target at least one coordination surface (co-op
messaging, scheduler, or marathon harness) and demonstrates a measured
improvement on it.

**Why this priority**: Valuable but independent of everything above and the
least evidenced; last in the dependency order.

**Independent Test**: An optimisation run against one coordination target
completes and reports a baseline-vs-optimised metric.

**Acceptance Scenarios**:

1. **Given** the optimisation stack, **When** it is pointed at a coordination
   target, **Then** the run completes and reports baseline and optimised
   scores for that target.

---

### Edge Cases

- The service box ships (P1) between audit time and implementation time: the
  gate must detect "already satisfied" from durable ship evidence, not from
  the audit snapshot.
- A batch advance is interrupted mid-batch: already-advanced features stay
  advanced; the report must say exactly which ids completed.
- CalVer normalisation encounters a version string it cannot map: it must
  refuse to rename that entry and report it, never guess.
- Two hosts cut a release the same UTC day during normalisation (the known
  CalVer crossing): the normal form must be collision-free per day per repo.
- An install interruption occurs during the activation switch itself: the
  switch must be atomic — either the old or the new version is active, never
  neither.
- The 051-ynet-transport branch is merged or deleted before P4 runs: the
  reconciliation classifies against whatever state the branch is in and
  records that state.
- An engineer ruling on an open escalate contradicts a confirmed claim: the
  ruling wins and the affected story's scope is re-stated before work starts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The workstream MUST begin with a ship-state gate for the durable
  listener service box: record "already satisfied" with evidence if shipped
  and closed, otherwise complete its remaining tail (suite wiring, quickstart
  verification, non-regression gate, replay-idempotence test restoration,
  history drill at N=100, per-link replay decision) and ship it. No rebuild of
  already-evidenced work is permitted.
- **FR-002**: The roadmap advance operation MUST accept a set of feature ids
  in one invocation and report a per-id outcome (advanced / failed with
  reason), with single-id behaviour unchanged.
- **FR-003**: All installed toolchain version identifiers MUST conform to one
  declared CalVer normal form after normalisation, and the normalisation MUST
  report every rename and refuse (with a report entry) any entry it cannot
  map.
- **FR-004**: Toolchain install/upgrade MUST be atomic: an interruption at any
  point leaves the previously active version fully operational, activation
  occurs only after a passing smoke verification, and a rollback operation
  restores the prior version.
- **FR-005**: A YNET naming-resolver specification MUST be produced before any
  resolver implementation, preceded by a reconciliation that classifies every
  YNET artifact on `origin/051-ynet-transport` as adopt / supersede /
  out-of-scope with rationale; no artifact may duplicate unmerged content.
- **FR-006**: A YNET battery-budget policy specification MUST be produced
  before any policy implementation, defining budget declaration and each
  budget-pressure behaviour in testable terms.
- **FR-007**: The optimisation stack MUST be able to target at least one
  coordination surface (co-op messaging, scheduler, or marathon harness) and
  report baseline-vs-optimised scores for it.
- **FR-008**: The five open audit ESCALATEs (slice-bound ship-state
  observability; resolver output shape vs the literal endpoint seam;
  dependency-absence inference; the exhaustive-failure-path assertion;
  atomic-rename as precedent for environment swaps) MUST each carry a recorded
  engineer ruling before the story they affect enters implementation; none may
  be self-resolved by the implementing session.
- **FR-009**: Stories MUST be executed in the evidence-derived dependency
  order (P1 → P6); a later story MUST NOT start implementation before its
  prerequisite story's exit gate is satisfied, except P6 which may run any
  time after P1.

### Key Entities

- **Audit claim**: An attributed, critic-adjudicated statement about repo
  state from run `20260803T134739Z-fa8a`; carries CONFIRM/REFUTE/ESCALATE
  status and grounds a story's scope.
- **Escalate ruling**: An engineer decision recorded against one open
  ESCALATE; unblocks the stories that cite it.
- **Ship-state gate**: The durable evidence check (tag + close record) that
  decides whether P1 is "already satisfied" or "finish and ship".
- **Reconciliation note**: The classification of `origin/051-ynet-transport`
  YNET artifacts (adopt / supersede / out-of-scope) that every later YNET
  spec must cite.
- **Version identifier**: A CalVer string naming an installed toolchain
  version; subject to the single normal form.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The service-box ship-state gate resolves to a durable record
  (either "already satisfied" with evidence, or shipped + closed) before any
  P2+ implementation lands.
- **SC-002**: An operator advances 3+ features in one batch invocation and
  every id reports its own outcome; a scan of installed version directories
  finds 0 non-conforming version identifiers.
- **SC-003**: In an interruption drill of at least 5 kill-points across the
  install/activation path, 0 runs leave the host without a working active
  version, and rollback succeeds in every completed-install case.
- **SC-004**: Both YNET specifications exist, the reconciliation note
  classifies 100% of the 051-branch YNET artifacts, and 0 artifacts duplicate
  unmerged branch content.
- **SC-005**: One optimisation run against a coordination target reports a
  baseline and an optimised score, and the optimised score is not worse than
  baseline.
- **SC-006**: 5/5 open ESCALATEs carry recorded engineer rulings before their
  affected stories implement; 0 escalates are resolved by the implementing
  session itself.

## Assumptions

- The 3rtask evidence directory `.specify/3rtask/runs/20260803T134739Z-fa8a/`
  remains available machine-locally as the audit ground truth; the roadmap
  brief carries its summary if the directory is lost.
- Feature 064 (`durable-listener-service-box`) is being finished in its own
  pipeline; P1 is expected to resolve to "already satisfied" at
  implementation time, and this feature does not duplicate 064's pipeline
  records.
- The six consolidated roadmap items remain represented by this one feature;
  their individual roadmap records are advanced/superseded per the roadmap's
  own discipline, not silently deleted.
- Engineer rulings on the five ESCALATEs are Gabi's to record; this feature
  only tracks and gates on them.
- The two YNET stories deliberately deliver SPECIFICATIONS as their artifact;
  implementing those specs is follow-on work promoted separately once the
  specs are approved.
