<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature Specification: A signal a caller treats as evidence must not be observable before the work it reports

**Feature Branch**: `108-evidence-signal-ordering`
**Created**: 2026-09-06
**Status**: Draft
**Input**: User description: "A signal a caller treats as evidence must not be observable before the work it reports"
**Roadmap feature**: `evidence-signals-not-observable-before-the-work-they-report` (WSJF 34.0 / RICE 720000, rank 1)
**Engineer ruling**: `Q-olg15-09` (2026-09-05) — ONE sibling feature to 078, scoped as its complement. **Feature 078 is NOT re-opened.**

---

## Why this feature exists

Feature 078 (`verification-receipts-and-loud-failure`) governs **checks that report a verdict**.
Its FR-001 reads: *"No check may report success, clean, or zero-findings without emitting proof it
executed against its intended target."* That invariant is about **verdicts** — a thing whose
declared job is to say pass or fail.

It does not reach a second, distinct class: **signals that carry no verdict at all, but that callers
nonetheless treat as evidence.** A wait that returns. An idle predicate that reads true. A liveness
flag. A process exit status. None of these claims to be a verdict, so none is covered by 078 — and
yet every one of them is read by a caller as "the work is done", and acted on.

**Seven instances of that class were measured across the fleet in 48 hours.** They are not variants
of one bug; they are four different mechanisms producing the same failure, which is what makes this
a class rather than a defect list.

### The measured instances

| # | signal | what the caller read | what was actually true | lane · date |
|---|---|---|---|---|
| 1 | `HookNotifier.WaitForIdle` returned | the pump has drained | the pump had **taken** an item but not yet **marked itself busy**; caller read a null result. Intermittent, ~1 in 3 | olamnit-glpnet · 2026-09-05 |
| 2 | `doctor` reported `m6_met: true` | an M6 client is running here | **nothing was running on the host** — the predicate was derived from configuration, not observation | shiras-glpnet · 2026-09-05 |
| 3 | `codex exec` exited 0 with an empty findings set | the review found nothing | the prompt was passed positionally, so **no review ran at all** | olamnit-glpnet · 2026-09-05 |
| 4 | `buildkit-scheduler reject` exited 0 | the operation succeeded | the operation was **REFUSED** | fleet tooling · 2026-09-05 |
| 5 | the election board rendered green | the fleet has a seated leader | the running process and its own on-disk state **disagreed**; a restart did not keep it green | shiras-ynglin · 2026-09-05 |
| 6 | `codex exec` exited 0 having emitted **116 KB** | a large, therefore real, review | it read `AGENTS.md`, obeyed a **"STOP AND WAIT"** reading gate, and stopped before opening any code. **The fleet's adopted byte-count heuristic passes it.** | olamnit-glpnet · 2026-09-05 |
| 7 | `ack` exited 0 and `doctor` then reported 0 pending | 13 alerts are acknowledged | a receiver **restart re-materialised the same 13 message ids** as unacknowledged | shiras-glpnet · 2026-09-06 |

Instance 6 is the one that makes the case. The fleet had already learned instances 3 and 4 and
adopted a defence — *"39 bytes means fake, a big transcript means real"*. Instance 6 is 116 KB and
still reports nothing. **A heuristic tuned to one mechanism does not generalise to the class.** That
is precisely why the class needs a stated invariant rather than another per-instance patch.

Instance 7 is the second lesson: the state that reported completion was not the state that
survived. Completion that a restart undoes was never completion.

### The invariant

> **A signal a caller treats as evidence MUST NOT be observable in a state that reports completion
> before the work it reports has completed — and MUST NOT report completion for work that does not
> survive the next restart.**

### Relationship to feature 078 — a hard boundary

078 and 108 partition the space; they do not overlap. The partition is by **what the signal
claims**, not by who emits it.

| | feature 078 | feature 108 (this) |
|---|---|---|
| governs | signals whose declared job is to **state a verdict** | signals that state **no verdict** but are read as evidence |
| examples | a check reporting PASS / clean / zero findings | a wait returning, an idle predicate, a liveness flag, an exit status |
| remedy | the signal must carry a **receipt** proving it ran against its target | the signal must not be **observable early**, and must be **durable** |
| status | implemented; five features are blocked by it | this feature |

**Any requirement here that would change what 078 requires is out of scope by construction.** Where
a signal both states a verdict *and* is observable early, 078 governs the verdict and 108 governs
the ordering; both bind, neither is weakened. This boundary exists because the fleet has already
paid for the alternative: feature 012 was minted twice, three rival M6 clients were built in one
morning, and five rival elections were built in one day. **Do not re-open 078. Do not widen this
into 078.**

---

## Clarifications

### Session 2026-09-06

- Q: What produces the enumeration that SC-002 measures coverage against? → A: **A declared checked-in manifest IS the denominator, cross-checked by a mechanical scan; any scan hit absent from the manifest is an ERROR, never a pass.** (Two independent sources must agree, so under-declaration and scan blind spots each fail loudly. Encoded in FR-014a/FR-014b and SC-002.)
- Q: Does a non-conforming signal refuse the consumer, or only warn it? → A: **Refuse — phased by declared adoption, with feature 078's informed-consent override (briefing + ack + rationale + scope + mandatory expiry).** One override mechanism serves both features; an area that has declared non-adoption keeps working behind a visible marker. (Encoded in FR-006a/FR-006b/FR-006c.)
- Q: What iteration count and control make SC-003 falsifiable? → A: **40 iterations under declared contention, AND a mandatory negative control — the harness must be shown to FAIL against the pre-fix code.** Without the control, 40 is arbitrary and a green harness may simply be incapable of failing. (Encoded in FR-018a and SC-003/SC-005.)
- Q: Where should the canonical M6 YNET client binary live on OLAMNIT? → A: **A stable per-host directory outside every repo, registered with `bk-onrestart`.** Not the session scratchpad — a completion signal that a reboot undoes is instance 5/7 of this very feature. (Out of scope for feature 108's requirements; recorded here because it was decided in the same round and because it is the M6 half of this era. Tracked as WP-02, not as an FR.)

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A caller can trust that a wait means the work happened (Priority: P1)

An engineer or an automated caller waits on a concurrency signal — an idle predicate, a quiescence
wait, a drain — and then reads the result of the work. Today that read can return nothing, because
the signal became observable in the window between the work being accepted and the work being
recorded as in progress. The caller has no way to tell a genuine empty result from a premature one.

**Why this priority**: This is the mechanism that produced the exemplar instance, it is
intermittent (so it survives testing), and it silently corrupts every downstream decision. It is
also the one with a known, cheap, general remedy.

**Independent Test**: Take any wait-style signal in the lane. Drive it under contention for a
declared number of iterations. The caller must observe a correct result on **every** iteration, or
the signal must refuse. Delivers value alone: one hardened wait is one class of silent wrong answer
removed, whether or not any other story ships.

**Acceptance Scenarios**:

1. **Given** work has been accepted but not yet begun, **When** a caller observes the completion
   signal, **Then** the signal MUST NOT report completion.
2. **Given** work has been accepted, begun, and its result published, **When** a caller observes
   the completion signal, **Then** the signal reports completion and the caller reads the published
   result.
3. **Given** a wait-style signal driven under contention for the declared iteration count,
   **When** the run finishes, **Then** zero iterations observed completion-with-no-result.
4. **Given** a signal that cannot determine whether outstanding work exists, **When** a caller
   observes it, **Then** it refuses rather than reporting completion.

---

### User Story 2 - A caller can tell "did not run" apart from "ran and found nothing" (Priority: P1)

A caller reads an exit status, or an empty result set, and concludes the work ran and was clean.
Instances 3, 4 and 6 are all this shape: the work did not run, or was refused, and the caller could
not tell. Instance 6 additionally defeats the size heuristic the fleet adopted after instance 3.

**Why this priority**: Equal-first with US1 because it is the shape that produced a **false green
on a security review** — the highest-consequence instance measured. It is also the shape most
likely to recur, because every wrapper around every external tool has this seam.

**Independent Test**: For each declared consumer of an exit status or an emptiness signal, inject
(a) a did-not-run condition and (b) a refused condition. The consumer must classify both as
non-success, and must name which one. Delivers value alone.

**Acceptance Scenarios**:

1. **Given** a tool that was invoked in a way that caused it to do no work, **When** it exits 0,
   **Then** the consumer MUST NOT classify the outcome as success.
2. **Given** an operation that was **refused**, **When** it exits 0, **Then** the consumer MUST NOT
   classify the outcome as success, and the refusal MUST be named.
3. **Given** a consumer that asserts on output size as a proxy for work having happened, **When**
   the audit runs, **Then** that consumer is reported as non-conforming — size is not evidence.
4. **Given** an outcome that is genuinely "ran, found nothing", **When** the consumer classifies
   it, **Then** it is distinguishable in the record from "did not run" and from "refused".

---

### User Story 3 - A completion signal survives a restart, or it never claimed completion (Priority: P2)

A caller acknowledges, commits, or seats something; the signal says done; a restart shows it undone.
Instances 5 and 7. The work was reported complete against state that did not survive.

**Why this priority**: P2 rather than P1 because it is slower to bite than US1/US2 — it needs a
restart to surface — but it is the most expensive when it does, because the caller has already
acted on the false completion and moved on.

**Independent Test**: Perform the operation, observe the completion signal, restart the reporting
component, and re-observe. The two observations must agree. Delivers value alone.

**Acceptance Scenarios**:

1. **Given** an operation whose signal reported completion, **When** the reporting component is
   restarted, **Then** re-observing the signal reports the same completion.
2. **Given** a signal derived from in-memory state only, **When** the audit runs, **Then** it is
   reported as non-conforming for US3.
3. **Given** a signal whose durable state and running state disagree, **When** a caller observes it,
   **Then** it refuses rather than reporting either.

---

### User Story 4 - A lane can find its own instances without waiting to be told (Priority: P2)

Every instance in the table above was found by a human noticing an anomaly, days after the signal
started lying. A lane needs a mechanical way to enumerate its own evidence-bearing signals and see
which ones are unproven.

**Why this priority**: P2 because the invariant and the exemplar fixes (US1–US3) deliver value
without it — but without US4 this feature closes seven instances and the eighth is found the same
expensive way.

**Independent Test**: Run the audit against this lane. It enumerates the lane's evidence-bearing
signal surfaces, classifies each, and names the unproven ones. Delivers value alone.

**Acceptance Scenarios**:

1. **Given** a lane with evidence-bearing signals, **When** the audit runs, **Then** every such
   signal appears in the report with a classification.
2. **Given** a signal with no conformance evidence, **When** the audit runs, **Then** it is reported
   as **unproven** — never as conforming.
3. **Given** the audit cannot read part of the lane, **When** it reports, **Then** it reports that
   region as unexamined rather than omitting it. *(This feature is subject to 078's FR-001 — see
   FR-016.)*

---

### Edge Cases

- **A signal with genuinely nothing to report.** An idle predicate on a queue that has never
  received work is legitimately idle. FR-004 requires "no outstanding work" to be distinguishable
  in the record from "work outstanding but not yet visible" — the first is a conforming report, the
  second is the defect.
- **A signal whose underlying work is unbounded.** A wait on a stream that never ends cannot report
  completion at all. FR-009 requires such a signal to be declared non-terminating rather than
  reporting a completion it cannot justify.
- **An external tool whose exit-status contract cannot be changed.** The consumer, not the tool, is
  the conformance target (FR-006) — the wrapper must establish evidence the tool ran.
- **A signal that is correct today but whose producer is later refactored.** FR-013 requires the
  conformance evidence to be a live executable check, not a one-time audit note.
- **Two observers disagree about the same signal.** FR-010: disagreement is itself a non-conforming
  observation and MUST refuse rather than pick a side.
- **The audit itself reports clean.** FR-016 binds this feature to 078's FR-001 — the audit must
  prove it ran, and its own non-execution must be loud.
- **A signal that is early only under contention that this host cannot produce.** FR-012 requires
  the declared iteration count and the contention conditions to be recorded with the result, so a
  green on an idle host is not mistaken for a green under load.

---

## Requirements *(mandatory)*

### The invariant and its scope

- **FR-001**: A signal that a caller treats as evidence of completed work MUST NOT be observable in
  a state that reports completion before that work has completed.
- **FR-002**: "Treated as evidence" MUST be determined by **how the signal is consumed**, not by how
  it is named or documented. A signal read by any consumer as grounds to proceed is in scope, even
  if its producer never described it as evidence.
- **FR-003**: This feature governs only signals that state **no verdict**. Signals that state a
  verdict are governed by feature 078 and MUST NOT be re-specified here. Where both apply, both
  bind and neither is weakened. *(Q-olg15-09)*

### Concurrency and wait-style signals (US1)

- **FR-004**: A completion or idle signal MUST distinguish "no outstanding work" from "work
  outstanding but not yet observable", and MUST report completion only for the first.
- **FR-005**: Work MUST be counted as outstanding from the moment it is **accepted**, not from the
  moment it **begins**. The window between acceptance and commencement is the defect in instance 1
  and MUST NOT exist.
- **FR-006**: The conformance target is the **consumer** of the signal as well as its producer. A
  consumer that reads a non-conforming signal MUST refuse it rather than treating it as evidence.
- **FR-006a**: The refusal binds only where the producing area has **declared adoption**, in the same
  per-area declared form feature 078 uses. An area that has declared non-adoption keeps working and
  its signals carry a visible non-adoption marker. An area with **no declaration at all** is an
  error, not non-adoption — mirroring 078's FR-019/FR-020 exactly, so one rule governs both.
- **FR-006b**: A refusal MUST be overridable only through feature 078's **informed-consent override**:
  a briefing, an explicit acknowledgement, a rationale, a declared scope, and a **mandatory expiry**.
  This feature MUST reuse 078's override machinery rather than introduce a second one — two override
  mechanisms is how an override becomes unauditable.
- **FR-006c**: An override that has expired MUST resume refusing. An override with no expiry MUST be
  rejected at the point it is recorded, not at the point it is relied on.

### Did-not-run, refused, and empty (US2)

- **FR-007**: A consumer MUST classify every outcome as exactly one of: **RAN-AND-COMPLETE**,
  **RAN-AND-EMPTY**, **DID-NOT-RUN**, **REFUSED**, or **INDETERMINATE**. Only the first two may be
  treated as success.
- **FR-008**: A process exit status alone MUST NOT be sufficient to classify an outcome as
  RAN-AND-COMPLETE or RAN-AND-EMPTY. Positive evidence that the work ran is required. *(Instances
  3, 4, 6.)*
- **FR-009**: A refusal MUST NOT be reported through a success channel. A component that refuses an
  operation MUST make the refusal distinguishable from success by its consumer without inspecting
  prose. *(Instance 4.)*
- **FR-010**: Output size, output presence, and elapsed time MUST NOT be used as evidence that work
  ran. A conformance check MUST assert on **content that only the completed work could produce**.
  *(Instance 6: 116 KB, exit 0, zero review — the byte-count heuristic passes it.)*
- **FR-011**: Where a consumer wraps an external tool whose exit contract cannot be changed, the
  wrapper MUST establish the positive evidence required by FR-008 itself, and MUST record which
  evidence it used.

### Durability (US3)

- **FR-012**: A signal that reports completion MUST report the same completion after the reporting
  component is restarted. Completion that a restart undoes was not completion. *(Instances 5, 7.)*
- **FR-013**: Where a signal's durable state and its running state disagree, the signal MUST refuse
  rather than reporting either. *(Instance 5.)*

### Audit and evidence (US4)

- **FR-014**: A lane MUST be able to enumerate its evidence-bearing signal surfaces and obtain a
  classification for each: **conforming**, **non-conforming**, or **unproven**.
- **FR-014a**: The enumeration is a **declared, checked-in manifest**. The manifest is the
  denominator for SC-002 — coverage is never measured against the subset that happened to be
  examined.
- **FR-014b**: A **mechanical scan** MUST run against the manifest on every conformance run. A signal
  surface the scan finds that the manifest does not list MUST be reported as an **ERROR** — never as
  a pass, and never silently added. Equally, a manifest entry the scan cannot locate MUST be reported
  as an error rather than assumed present. The two sources are independent by construction: the
  manifest catches what the scan's patterns miss, and the scan catches what nobody declared.
- **FR-015**: The absence of conformance evidence for a signal MUST be reported as **unproven**, and
  MUST NOT be reported as conforming. Absence is not a pass.
- **FR-016**: Conformance evidence MUST be a live executable check that fails when the property
  regresses, not a recorded assertion that the property once held. A note is not evidence.
- **FR-017**: The conformance run and its report are themselves subject to feature 078's FR-001 —
  they MUST prove they executed against their intended target, and their non-execution MUST be
  loud. This feature does not exempt itself from its sibling.
- **FR-018**: A conformance run for a contention-sensitive property MUST record the iteration count
  and the contention conditions under which it passed, so that a pass on an idle host is not read as
  a pass under load. *(Instance 1 is ~1 in 3 under contention and 0 in N when idle.)*
- **FR-018a**: The declared iteration count for a contention-sensitive property is **40**, and every
  such conformance check MUST ship with a **negative control**: the check MUST be demonstrated to
  FAIL against the defect it governs. A check that has never been shown capable of failing is not
  evidence that the property holds — it is only evidence that the check ran. *(This is FR-010's rule
  applied to the conformance suite itself.)*

### Reporting and refusal

- **FR-019**: Every non-conforming or unproven finding MUST name the signal, the consumer that reads
  it, and which of FR-004/007/012 it fails — so a reader can act without re-deriving the analysis.
- **FR-020**: The report MUST distinguish a region that was **examined and clean** from a region
  that was **not examined**. An unexaminable region is reported, never omitted.

---

### Key Entities

- **Evidence-bearing signal**: an observable — a wait returning, a predicate reading true, a status
  code, an emptiness — that at least one consumer reads as grounds to proceed. Identified by its
  producing surface and its consumers.
- **Outcome classification**: exactly one of RAN-AND-COMPLETE, RAN-AND-EMPTY, DID-NOT-RUN, REFUSED,
  INDETERMINATE (FR-007).
- **Conformance evidence**: a live executable check bound to one signal, which fails if the signal
  becomes observable early, becomes non-durable, or loses its did-not-run discrimination (FR-016).
- **Conformance report**: the per-lane enumeration of signals with classifications, examined and
  unexamined regions, and the contention conditions each contention-sensitive pass was obtained
  under (FR-014, FR-018, FR-020).

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All seven measured instances in the table above are classified against FR-004 /
  FR-007 / FR-012, and each is either **fixed with a live conformance check** or **disclosed as
  carried with a named owner**. Zero instances are silently closed.
- **SC-002**: Every evidence-bearing signal surface **listed in the declared manifest** appears in the
  conformance report with a classification. The denominator is the manifest (FR-014a), not the subset
  that happened to be examined — an unexamined surface counts against the total (FR-020).
  Additionally, the mechanical scan finds **zero** surfaces absent from the manifest and **zero**
  manifest entries it cannot locate (FR-014b); either is a failure of SC-002, not a warning.
- **SC-003**: A signal driven under its declared contention conditions for **40** iterations observes
  a correct result on **100%** of them. A single early observation is a failure, not a flake. The
  run is only scored once its negative control has been shown to fail (FR-018a) — an unfalsifiable
  100% scores zero.
- **SC-004**: For every consumer covered by US2, an injected **did-not-run** and an injected
  **refused** are each classified as non-success and named. Measured by fault injection with a
  passing negative control — the injection must be shown to be capable of failing the check.
- **SC-005**: The conformance suite detects a **deliberately reintroduced** instance of each of the
  four mechanisms (early wait, exit-status-only, size-as-evidence, non-durable completion) within
  one run. A suite that cannot fail is not evidence (FR-016).
- **SC-006**: A restart of each US3-covered reporting component preserves its reported completion —
  measured by observe / restart / re-observe, with the two observations compared mechanically.
- **SC-007**: The report distinguishes examined-and-clean from not-examined for 100% of declared
  regions. Zero regions are omitted.

---

## Assumptions

- **Scope is this lane's surfaces plus the cross-lane instances already measured.** The invariant is
  fleet-wide and is published for adoption, but this feature delivers conformance for
  `olamnit-glpnet` only. Other lanes adopt by binding to the published invariant, exactly as 078 was
  adopted. Fixing another lane's signal in place is out of scope and is the failure mode that
  produced three rival M6 clients.
- **The exemplar remedy for the wait class is an outstanding-work counter incremented at
  *acceptance* and decremented after the handler *publishes*.** This is stated as the known-good
  shape, not mandated — FR-004/FR-005 state the property; any implementation meeting them conforms.
- **Instances 2, 5 and 7 are owned by other lanes** (shiras-glpnet, shiras-ynglin). This feature
  classifies and publishes them; it does not fix them in place. SC-001 is met for those by
  disclosure with a named owner.
- **Feature 078 is implemented and five features are blocked by it.** No requirement here changes
  078. The cross-reference is added in both directions as documentation only.
- **`Origin` on the coop file carrier is unauthenticated** and is a separately-tracked P1 belonging
  to the canonical client (`Q-glpnetshiras-50`). It is not in scope here; it is an authentication
  defect, not an ordering one.
- **The contention conditions available on this host bound what SC-003 can measure.** A property
  that is only early under contention this host cannot generate is recorded as unproven rather than
  claimed (FR-018).

---

## Out of Scope

- Re-opening, widening, or amending feature 078 in any respect (`Q-olg15-09`).
- Authoring a rival M6 client, election, or transport. This feature audits signals; it does not
  build carriers.
- Fixing another lane's signal in that lane's tree.
- Any change to the GLP language definition, its guards, kernels or type system (CLAUDE.md §1.14).
