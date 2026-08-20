<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: Coordination feature-stream durable superset fix

**Feature Branch**: `082-feature-stream-superset`
**Created**: 2026-08-19
**Status**: Draft
**Roadmap**: #13 `coordination-feature-stream-durable-superset-fix` (promoted, WSJF 4.25 / RICE 2625)
**Evidence**: 3rtask run `20260819T162016Z-6e73` (verdict `budget_stop`; 72 claims, 66 CONFIRM / 5 ESCALATE / 1 REFUTE; 5 derived DEFECTs, 5 derived CAUSAL_LINKs) and `docs/research/scheduler-feature-stream-superset-design-2026-08-19.md`
**Input**: Engine-side remediation of the "no steady feature stream" defect cluster, split out from feature 078 so the engine work is a separate feature.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A host receives work without anyone hand-running a verb (Priority: P1)

An engineer on any host runs their normal pipeline. Work packages eligible for them arrive on their board and are assigned to them, without any person remembering to hand-run a readiness verb on some other host first.

**Why this priority**: This is the measured dominant constraint. Across the whole observed window only 4 of 32 work packages ever became visible to the allocator, all promoted by one person on one host. The other 28 were structurally invisible. No downstream improvement can compensate for supply that never enters the queue.

**Independent Test**: On a board with eligible backlog work and no manual intervention, confirm work becomes allocatable within one cycle, and that a board with no readiness writer configured says so loudly instead of reporting an empty result.

**Acceptance Scenarios**:

1. **Given** a board with eligible backlog work and no operator action, **When** a cycle runs, **Then** eligible work becomes allocatable and the transition is attributed to a named writer.
2. **Given** a board with no readiness writer configured, **When** any read reports on it, **Then** it reports "no readiness writer configured" and MUST NOT report "no candidates" or an empty board.
3. **Given** readiness was written, **When** the log is inspected, **Then** every transition names its writer and the reason it fired.

---

### User Story 2 - Work is sized against real capacity, so it can actually be placed (Priority: P1)

Work packages carry effort estimates meaningful against real declared working time, so the allocator can place them.

**Why this priority**: Measured, 96.2% of work packages carried effort exceeding every node's capacity. While that holds, the allocator is arithmetically incapable of placing work no matter who is available. The only two packages ever placed had been re-estimated downward first.

**Independent Test**: Compare the effort distribution against declared capacity and confirm the share of structurally unplaceable work, then confirm uncalibrated nodes are flagged rather than silently defaulted.

**Acceptance Scenarios**:

1. **Given** a work package whose effort exceeds every node's capacity, **When** a cycle runs, **Then** it is reported as unplaceable-by-size, distinctly from unplaceable-by-availability.
2. **Given** a node with no measured actuals, **When** its capacity is used, **Then** it is flagged as uncalibrated and MUST NOT be silently assigned a default.

---

### User Story 3 - Every reader and writer is talking about the same board (Priority: P1)

Any person or tool quoting a board figure states which board it came from, and two tools on the same board produce the same answer.

**Why this priority**: The same physical board is reachable under at least three different names, while several similarly-named boards are genuinely different. A repository was measured with no board bound at all, so its commands resolved to nothing. Two lanes published different figures for one board eleven minutes apart.

**Independent Test**: From a repository with no board configured, confirm commands refuse rather than return an empty result; then confirm two different tools reading one bound board report identical counts.

**Acceptance Scenarios**:

1. **Given** a repository with no board configured, **When** any board command runs, **Then** it refuses with a non-success result naming the missing configuration.
2. **Given** one board reachable under several names, **When** figures are folded, **Then** they resolve to one identity and MUST NOT be reported as conflicting boards.
3. **Given** any published board figure, **When** it is read, **Then** the board it came from is named alongside it.

---

### User Story 4 - One answer to "who owns this?" (Priority: P2)

An engineer asks who owns a work package and gets one answer.

**Why this priority**: The derived proposal surface and the committed record were measured assigning the same work package, at the same effort, to different people; a person had to rule which one counted. Separately, work shown as blocked on every node was committed anyway, producing an over-capacity record.

**Independent Test**: Drive a cycle producing a proposal, commit it, and confirm proposal and committed record never disagree; then attempt to commit a proposal blocked on all nodes.

**Acceptance Scenarios**:

1. **Given** a proposal and a committed record for one work package, **When** ownership is read, **Then** exactly one surface is authoritative and they agree.
2. **Given** a proposal blocked on every node, **When** a commit is attempted, **Then** the outcome follows the ruled policy and the over-capacity condition is never recorded silently.

---

### User Story 5 - Supply arrives continuously, not in bursts (Priority: P2)

Eligible work flows onto boards steadily instead of arriving all at once after long silence.

**Why this priority**: Measured, promotion stopped roughly 117 hours before the window closed, 26 work packages were minted in a single nine-second burst nearly 21 days into a 21-day window, and only 58 of 494 possible hourly cycles existed. A burst is not a stream and no downstream gate can convert one into the other.

**Independent Test**: Observe intake over a multi-day period and confirm arrival is distributed rather than concentrated, and that a stalled intake is reported.

**Acceptance Scenarios**:

1. **Given** eligible promoted work exists, **When** time passes, **Then** it is ingested on a cadence and the cadence is observable.
2. **Given** intake has not run beyond its expected interval, **When** the board is read, **Then** the staleness is reported rather than presenting as a healthy empty board.

---

### User Story 6 - Placeholder identities never consume real capacity (Priority: P3)

Capacity and load figures reflect real people only.

**Why this priority**: Records addressed to a placeholder identity were counted as real allocated load against a zero-capacity engineer, making load reporting misleading. Lower priority because it distorts reporting rather than blocking flow.

**Acceptance Scenarios**:

1. **Given** records addressed to a placeholder identity, **When** load is computed, **Then** they are excluded from real capacity and reported separately.

---

### User Story 7 - A contract that denies a capability the tool has, fails (Priority: P3)

Written guidance and the tool it describes cannot silently disagree.

**Why this priority**: The governing guidance states verbatim that a capability does not exist while the running tool exposes it plus six further undocumented ones. Measured: the existing conformance check reports clean here, because it only checks one direction — guidance that *uses* a capability the tool lacks. The reverse direction is unchecked.

**Independent Test**: Introduce a known both-direction divergence and confirm the check fails; confirm it fails for the reverse direction specifically.

**Acceptance Scenarios**:

1. **Given** guidance denying or omitting a capability the tool exposes, **When** conformance runs, **Then** it fails and names the divergence.
2. **Given** a conformance check reporting clean across all units, **When** a known divergence is injected, **Then** the check fails — proving it is live rather than inert.

---

### Edge Cases

- A board is reachable but empty: distinguish "no work" from "no readiness writer" from "not configured" — all three currently present identically.
- Configured board path unreachable (host down, share unmounted): must refuse, never report empty.
- Two hosts write concurrently: convergent merge without lost writes. A wrong-actor write is irreversible under the grow-only single-writer lease, so it must be refused before writing.
- Effort re-estimated downward purely to clear a gate: distinguish genuine re-estimation from gate-gaming in the record.
- Delivery notification when the receiving host is offline: the acknowledgement must survive the outage.
- Guidance legitimately ahead of a tool (capability specified, not yet shipped): must be declarable, not a failure.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide an explicit, named, automatic writer for work readiness; readiness MUST NOT depend solely on a person hand-running a verb.
- **FR-002**: System MUST report "no readiness writer configured" distinctly from "no eligible work"; an absent writer MUST NOT present as an empty board.
- **FR-003**: Every readiness transition MUST record its writer and the reason it fired.
- **FR-004**: System MUST derive effort estimates from measured actuals against declared per-host capacity, and MUST flag uncalibrated nodes rather than defaulting them silently.
- **FR-005**: System MUST report unplaceable-by-size distinctly from unplaceable-by-availability.
- **FR-006**: System MUST support declared availability spanning multiple days; a single-day horizon MUST NOT be assumed.
- **FR-007**: Every invocation MUST bind to one canonically resolved board identity, and MUST refuse with a non-success result when no board is configured.
- **FR-008**: System MUST resolve aliases of one physical board to a single identity, and MUST NOT treat distinct boards as the same one.
- **FR-009**: Every published board figure MUST name the board it was derived from.
- **FR-010**: Exactly one surface MUST be authoritative for assignment; a derived view MUST NOT contradict the committed record.
- **FR-011**: System MUST NOT record an over-capacity assignment silently; the condition MUST be reported.
- **FR-012**: System MUST ingest eligible promoted work on an observable cadence, and MUST report intake staleness beyond its expected interval.
- **FR-013**: Placeholder identities MUST be excluded from real capacity and load, and reported separately.
- **FR-014**: Conformance checking MUST cover both directions — guidance using a capability the tool lacks, AND the tool exposing a capability the guidance denies or omits.
- **FR-015**: Guidance intentionally ahead of the tool MUST be declarable as such without failing conformance.
- **FR-016**: Conformance checking MUST be fire-tested against a known injected divergence, so a check that never fires is distinguishable from one that is inert.
- **FR-017**: Delivery of assigned work MUST use durable queued transport whose acknowledgement state survives the receiving host being unavailable.
- **FR-018**: Acknowledgement deadlines MUST be expressed in units the transport can actually satisfy.
- **FR-019**: The remediation MUST apply to every repository on a host without per-repository editing, and MUST require no per-repository change on any other host.
- **FR-020**: The remediation MUST state its own non-coverage explicitly.
- **FR-021**: System MUST refuse a write attributed to an identity other than the acting host's own.

### Key Entities

- **Board**: The shared queue of work packages, with one canonical identity and possible aliases.
- **Work package**: A unit of work with a state, an effort estimate, and at most one owner.
- **Readiness transition**: The event making a work package visible to allocation; carries writer and reason.
- **Capacity declaration**: A host's available working time over a stated horizon.
- **Assignment**: The authoritative binding of a work package to an owner.
- **Conformance finding**: A recorded divergence between written guidance and tool behaviour, with direction.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Over a 14-day observation, at least 90% of eligible work reaches an allocatable state without manual intervention. Observed baseline: 4 of 32 (12.5%), all manual.
- **SC-002**: Fewer than 10% of work packages carry effort exceeding every host's capacity. Observed baseline: 96.2%.
- **SC-003**: Every host with declared availability receives at least one assignment per 72 hours. Observed baseline for one host: zero over 21 days.
- **SC-004**: 100% of board commands run without a configured board refuse rather than returning an empty result. Observed baseline: returns empty at success.
- **SC-005**: Two independent readings of one board within the same interval agree 100% of the time.
- **SC-006**: Zero disagreements between proposed and committed ownership over a 14-day observation. Observed baseline includes at least one.
- **SC-007**: Intake gaps beyond the expected interval are reported 100% of the time. Observed baseline includes an unreported 117-hour gap.
- **SC-008**: Injected guidance/tool divergences are detected in both directions 100% of the time. Observed baseline detects the reverse direction 0% of the time — clean across 62 units with a known divergence present.

## Assumptions

- The remediation is engine-side. Every requirement is satisfied once in the shared toolchain and inherited by all repositories via the existing deployment mechanism; no per-repository implementation is assumed.
- Hosts continue to keep their own availability and their own board binding; those are legitimately per-host inputs and are not centralised by this feature.
- The existing convergent multi-writer substrate is retained. This feature changes what is written and by whom, not the merge semantics.
- The environment override currently causing the running toolchain to differ from the pinned one is a condition to remove before rollout, not a mechanism to build on.
- Consolidating readiness authority is acceptable; today one host effectively gates supply for all, which is the condition being removed.

## Out of Scope

- **Whether a central allocator may assign work to another person.** Open engineer escalation, unanswered since 2026-08-13; its author recorded that the allocation design cannot be completed without it. FR-010 deliberately specifies *one authoritative surface* without ruling *who may write it*.
- **Whether a proposal blocked on every node may be force-committed.** Open engineer escalation; FR-011 requires the condition be reported either way.
- **Capability-name normalisation at the fail-closed identity gate.** Open engineer escalation. The observed candidates never exercised that gate, so no defect is derivable here.
- **The categorical claim that availability was never a factor.** Escalated, not confirmed: the evidence shows capacity is *insufficient* to explain one host's zero, not that availability is irrelevant. A controlled counterfactual is owed.
- **Recovering unmerged historical branches.** The standing ruling is that those candidates are behaviours to reimplement, not branches to merge.
