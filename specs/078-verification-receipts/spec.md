<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: Verification receipts and loud failure (no check may pass without proving it ran)

**Feature Branch**: `078-verification-receipts`
**Created**: 2026-08-12
**Status**: Draft
**Input**: User description: "Verification receipts and loud failure (no check may pass without proving it ran) — RCA cluster F1 of 6, roadmap feature `verification-receipts-and-loud-failure-no-check-may-pass-without-proving-it-ran`, WSJF 7.80 / RICE 1173, hard-blocks F2–F6. Invariant: no check may report success/clean/zero-findings without proof it executed against the intended target; every check emits a receipt distinguishing EMPTY / UNREAD / UNSEARCHABLE. Acceptance must be fault-injected, not hypothetical. Declared areas: buildkit-3rtask, buildkit-codexreview, codeconv-build-gate, coop-protocol, roadmap-sync, test-harness."

## Why this feature exists

Across ~300 deduplicated fleet defects excavated from gate ledgers, COOP threads and handovers, the single largest and most dangerous class is **a mechanism that reports success, zero findings, or nothing at all while not having run** — or having run against the wrong target, revision, host or path. It was witnessed independently by all three evidence corpora.

The damage is not that a check fails. It is that **a check that never ran is indistinguishable from a check that ran and found nothing**. Every downstream signal built on that green is unearned. This class is *why* the other five root causes survived undetected, which is why this feature ships first.

**Witnessed instances this specification must make impossible** (each traceable to the RCA inventory):

| # | Observed silent success | Inventory ids |
|---|---|---|
| 1 | A review reported **0 findings because it never ran** — a mandatory-reading gate silently false-zeroed non-interactive passes. 3 recurrences. | PR-15 |
| 2 | A review tool omitted its findings block in **5/5 passes**, yielding `findings_count=0` while it had really found 5–8 P1/P2 items. | PR-16, AG-04, RT-35, RT-45 |
| 3 | `brief` / `record-output` **silently no-op** on an existing role input, invalidating an entire adjudication round. | TL-07 |
| 4 | A roadmap import **refused 954 untagged entities and applied 0 lines** while `replay --verify` still reported OK — silent split-brain, 20-line divergence measured. | RS-11 |
| 5 | Test skip-guards report an unsupported-platform link as **passed-by-skip**. | RT-24, RT-28, RT-29, RT-16 |
| 6 | A build gate is **compile-only**, so a behaviourally-wrong generated file can be promoted. | CD-03 |
| 7 | Corpus tools are **manual-only**, so the unified suite gates corpus scope by nothing at all. | D8-11, D8-12, D8-14 |
| 8 | **Four separate poll/cursor defects** each silently skipped unread mail — one hid a peer acknowledgement for 14.5 h, one cost a full day of idling. | RT-12, RS-35, RS-36, RT-32 |
| 9 | Probes run **from the wrong directory** returned a false clean. | DI-03 |
| 10 | A scheduler poll against a **retired root** reported *0 actors, empty board, exit 0* — a naive poll concluded "the fleet is idle" from a directory that does not exist. | RT-27 |
| 11 | A workflow status surface reported **`outstanding items: 0`** while its own gate refused on two unsatisfied checklist blockers. | (new, 2026-08-12) |
| 12 | A preventive guard **passed cleanly on the failing case** — it checked a condition that was already false, manufacturing confidence while the protected artifact was destroyed anyway. | (new, 2026-08-12) |
| 13 | `reconcile` reported **"roadmap already in sync with pipeline (no changes)"** immediately after a new feature entered the pipeline, because the spec directory had not slug-matched and was therefore never examined. The companion `link` step *did* report honestly ("no new spec directories matched"), but `reconcile` did not consult that outcome and issued an unqualified in-sync verdict — leaving roadmap state and pipeline state silently divergent. | (new, 2026-08-12) |

Instance 12 is the sharpest statement of the problem: **a guard that passes on the failing case is worse than no guard**, because it converts an unknown risk into a false assurance.

**Instance 13 was discovered while writing this specification**, by running the very tools this feature governs. It is retained deliberately rather than quietly fixed, because it demonstrates three things the requirements below must handle: an aggregate reporting success while a constituent step reported a non-success outcome (FR-009); a check whose scope silently excluded the item it should have examined (FR-002, FR-003); and the fact that this class is still actively producing new instances. It is also a reminder that **this feature is itself subject to its own invariant** — see FR-016.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A check proves it ran (Priority: P1)

An engineer or agent reads a green signal and needs to know it was earned. Today a green can mean "ran and found nothing", "ran against the wrong thing", or "never ran". The reader cannot tell them apart, and nothing in the output distinguishes them.

With this story, **every check emits a receipt alongside its verdict**. The receipt names what was examined, how much of it was examined, and when. A verdict without a receipt is not a pass — it is refused as an incomplete result.

**Why this priority**: This is the irreducible core. Without a receipt there is nothing to reason about, and stories 2–4 have no substrate. Implemented alone it already converts the most dangerous instances (1, 2, 9, 10, 11) from silent to visible, because each of those produced a verdict with no evidence of execution.

**Independent Test**: Run any receipt-emitting check against a known target and confirm the receipt names that target and a non-zero examined-count. Then point the same check at a non-existent target and confirm it does **not** report clean. Delivers value standalone: readers can immediately distinguish earned from unearned greens.

**Acceptance Scenarios**:

1. **Given** a check with a target containing known items, **When** it runs and finds no problems, **Then** it reports clean **and** emits a receipt naming the resolved target identity and the count of items examined.
2. **Given** a check whose target cannot be resolved (missing path, retired root, wrong revision), **When** it runs, **Then** it MUST NOT report clean — it reports an unresolved-target outcome naming what it looked for and where.
3. **Given** a check that produces a verdict with no receipt, **When** a consumer reads that verdict, **Then** the consumer refuses it as incomplete rather than treating it as a pass.
4. **Given** a check that examined zero items, **When** it reports, **Then** the zero is explicit and attributed — never rendered as "clean" or "0 findings" without qualification.

---

### User Story 2 - EMPTY, UNREAD and UNSEARCHABLE never collapse (Priority: P1)

Three materially different situations are today all rendered as "nothing found":

- **EMPTY** — the target was resolved and examined in full; there genuinely is nothing.
- **UNREAD** — the target exists and holds items, but some or all were not examined (a cursor skipped them, a filter excluded them, the run stopped early).
- **UNSEARCHABLE** — the target could not be examined at all (absent, unreachable, unsupported, wrong format, permission-refused).

Collapsing these is the direct cause of instances 4, 5, 7, 8 and 10. A skipped test is not a passing test. A refused import is not a converged import. An unread mailbox is not an empty mailbox.

**Why this priority**: P1 alongside US1, not below it. A receipt that reports "0 examined" without saying *which of the three* is happening is still ambiguous, so this is what makes the receipt load-bearing rather than decorative.

**Independent Test**: Drive one check into each of the three states and confirm three distinct, named outcomes — none of which is reported as success. Testable without US3's fault-injection harness by using naturally-occurring targets (an empty directory, a partially-consumed cursor, an absent path).

**Acceptance Scenarios**:

1. **Given** a fully-examined target with no items, **When** the check reports, **Then** the outcome is EMPTY and is a legitimate pass.
2. **Given** a target where items exist beyond the examined range, **When** the check reports, **Then** the outcome is UNREAD, it is **not** a pass, and it states how many items were left unexamined.
3. **Given** a target that cannot be examined, **When** the check reports, **Then** the outcome is UNSEARCHABLE, it is **not** a pass, and it names the reason.
4. **Given** a check that skips items for any reason (unsupported platform, filter, guard), **When** it reports, **Then** those items are counted as skipped in the receipt and the overall verdict cannot be a clean pass on their behalf.
5. **Given** a partially-completed run, **When** it reports, **Then** the examined and unexamined portions are both stated — a partial run never presents as a whole one.

---

### User Story 3 - The guards are proven by fault injection, not assumed (Priority: P2)

The brief is explicit that acceptance must be fault-injected rather than hypothetical, and instance 12 shows why: a guard was believed to work, was reasoned about carefully, and passed cleanly on precisely the case it existed to catch. Nobody had ever made it fail on purpose.

With this story, the acceptance suite **deliberately induces each silent-success mode** and asserts the check refuses loudly.

**Why this priority**: P2 because US1+US2 deliver the mechanism and US3 proves it. But without US3 this feature would itself be an instance of the defect it fixes — a verification mechanism that has never been verified. It is the story that makes the feature self-consistent.

**Independent Test**: Run the fault-injection suite against the implemented guards; every injected fault produces a loud, named refusal. Independently valuable because the suite becomes the regression net for the whole class.

**Acceptance Scenarios**:

1. **Given** a check pointed at a deliberately-removed target, **When** the suite runs, **Then** the check refuses and the suite asserts the refusal — a clean pass here fails the suite.
2. **Given** a check whose output block is deliberately suppressed (reproducing instance 2), **When** the suite runs, **Then** the missing block is detected as UNREAD, not read as zero findings.
3. **Given** a consumer deliberately fed a verdict with no receipt, **When** the suite runs, **Then** the consumer refuses it.
4. **Given** a check run deliberately from the wrong working location (reproducing instance 9), **When** the suite runs, **Then** the target mismatch is detected before any verdict is issued.
5. **Given** a check whose examined-count is deliberately falsified to exceed the target's true size, **When** the suite runs, **Then** the inconsistency is detected.
6. **Given** the fault-injection suite itself does not run, **When** results are collected, **Then** that absence is itself loud — the suite is subject to its own invariant.

---

### User Story 4 - The witnessed defect sites adopt receipts (Priority: P3)

The twelve instances above are real sites in the fleet's own toolchain, spanning the six declared areas. This story retrofits them so the historical failures cannot recur.

**Why this priority**: P3 because it is breadth over mechanism — each site is individually mechanical once US1–US3 exist. Sequenced last so the contract stabilises before it is applied twelve times.

**Independent Test**: For each retrofitted site, reproduce its historical failure and confirm it now surfaces loudly. Each site is independently demonstrable, so the story can land incrementally.

**Acceptance Scenarios**:

1. **Given** any retrofitted site, **When** its historical failure condition is reproduced, **Then** it surfaces loudly instead of reporting success.
2. **Given** a retrofitted site, **When** it operates normally, **Then** it emits a receipt conforming to the same contract as every other site.
3. **Given** the set of declared areas, **When** adoption is reported, **Then** the report states per-area coverage honestly, including areas not yet adopted — coverage is never implied by silence.

---

### Edge Cases

- **A check crashes mid-run.** A partial run must not present as a whole one; the receipt records what was examined before the crash, and the verdict is not a pass.
- **The target is legitimately empty.** EMPTY is a real, valid pass — the feature must not make legitimate emptiness impossible to express, or engineers will route around it.
- **The receipt itself is missing or malformed.** Treated as UNREAD by the consumer. The receipt mechanism is subject to its own invariant and cannot be exempt.
- **Nested checks.** A parent aggregating children must not report clean while any child is UNREAD or UNSEARCHABLE; child outcomes propagate rather than being summarised away.
- **A check cannot determine its own target.** UNSEARCHABLE, never clean. This is the retired-root case (instance 10) and the wrong-directory case (instance 9).
- **A legitimately skipped item** (genuinely unsupported platform). Recorded as skipped with a reason and counted; the verdict is qualified, never a silent pass on its behalf.
- **Receipt volume on very large targets.** Receipts must stay bounded so they do not overwhelm the signal they exist to support.
- **An engineer needs to override a refusal to make progress.** The override is recorded with a rationale and remains visible in the receipt; it never silently converts to a pass.
- **Two checks disagree about the same target's identity.** Surfaced as a conflict rather than resolved by precedence.
- **A check is deleted or disabled entirely.** Its absence from a run is itself a reportable condition — a check that no longer exists must not read as a check that passed.

## Requirements *(mandatory)*

## Clarifications

### Session 2026-08-17

- **Q: Is FR-008 absolute, or phased against adoption?** → **Phased.** A receipt binds only where the producing area has declared adoption; unadopted areas keep working and emit a visible non-adoption marker. Engineer ruling, encoded in FR-008.
- **Q: Who declares adoption, and where does the declaration live?** → A **single checked-in adoption manifest** enumerating every area named in FR-017, each with its state and the date it was set. Behaviour never implies adoption. Encoded as FR-019.
- **Q: What prevents an area from silently never declaring, making FR-008 vacuous?** → **Absence is an error, not a pass and not non-adoption.** An unlisted area causes a refusal that names the missing declaration. Encoded as FR-020, with SC-002's denominator pinned to FR-017's enumeration in FR-021.

*Resolution rationale:* the phased ruling alone would have left FR-008 satisfiable by declaring nothing — SC-002 measures coverage *within declared areas*, so an empty declaration set met it trivially. FR-019/020/021 close that hole without weakening the phasing.

### Functional Requirements

**The invariant**

- **FR-001**: No check may report success, clean, or zero-findings without emitting proof it executed against its intended target.

**Receipts**

- **FR-002**: Every check MUST emit a receipt with its verdict, recording at minimum: the resolved target identity, the count of items examined, the count skipped with reasons, the outcome classification, and when it ran.
- **FR-003**: The resolved target identity MUST be recorded as actually resolved at run time, not as requested — so a check that resolved to a different path, revision, host or root than intended is visibly different in the receipt.
- **FR-004**: A receipt MUST be machine-readable so consumers can enforce FR-008 without human interpretation, and human-readable enough to be actionable where it is displayed.
- **FR-005**: Receipts MUST be bounded in size regardless of target size.

**The three-way distinction**

- **FR-006**: Every check outcome MUST be classified as exactly one of PASS, EMPTY, UNREAD, UNSEARCHABLE, or FAIL.
- **FR-007**: Only PASS and EMPTY may be treated as successful. UNREAD and UNSEARCHABLE MUST NOT be reported as success, aggregated into success, or rendered in a way a reader would mistake for success.

**Consumers**

- **FR-008**: Any consumer of a check verdict MUST refuse a verdict lacking a conforming receipt, rather than defaulting to treating it as a pass — binding wherever the producing area has **declared adoption** per FR-019. Where an area has declared non-adoption, its verdicts remain usable and MUST carry a visible non-adoption marker. An area with **no declaration at all** is not non-adoption: it is an error under FR-020.
- **FR-009**: An aggregating check MUST NOT report success while any constituent is UNREAD or UNSEARCHABLE; constituent outcomes propagate to the aggregate.
- **FR-010**: Where a check reports counts, the receipt MUST allow those counts to be reconciled against the target's true size, so a falsified or impossible count is detectable.

**Loud failure**

- **FR-011**: A refusal MUST name what was expected, what was found, and where it looked — sufficient to act on without re-running.
- **FR-012**: A refusal MUST NOT be suppressible by ordinary configuration. Where an engineer must proceed regardless, an explicit recorded override with a rationale is the only path, and it remains visible in the receipt.
- **FR-013**: The absence of an expected check from a run MUST itself be reported; a check that did not run must not be indistinguishable from one that passed.

**Fault-injected acceptance**

- **FR-014**: Each silent-success mode this feature closes MUST have an acceptance test that deliberately induces it and asserts a loud refusal.
- **FR-015**: The acceptance suite MUST fail if an injected fault produces a clean pass.
- **FR-016**: The fault-injection suite MUST itself be subject to FR-001 and FR-013 — its own non-execution is loud.

**Adoption**

- **FR-017**: Every declared area — 3rtask, codexreview, build gate, coop protocol, roadmap sync, test harness — MUST report its adoption state honestly, including non-adoption.
- **FR-018**: Adoption reporting MUST state per-area coverage explicitly; absence of a report MUST NOT be readable as full coverage.
- **FR-019**: Adoption MUST be declared explicitly, per area, in a single checked-in adoption manifest that **enumerates every area named in FR-017**. Each entry records the area, its adoption state, and the date that state was set. The manifest is the sole authority for whether FR-008 binds; no area may be inferred adopted from its behaviour, and emitting a conforming receipt does not by itself constitute a declaration.
- **FR-020**: The absence of an area's entry from the adoption manifest MUST be an error — never a pass, and never equivalent to declared non-adoption. A consumer encountering an unlisted area MUST refuse under FR-008 and name the missing declaration under FR-011.
- **FR-021**: The denominator of SC-002 is fixed by FR-019's enumeration, not by the set of areas that happen to have declared. An empty or partial declaration set therefore cannot satisfy SC-002 — it fails FR-020 first.

### Key Entities

- **Check**: Any mechanism that inspects a target and issues a verdict — a test, a review pass, a build gate, a sync validation, a poll, a status probe.
- **Target**: What a check examines, identified by whatever makes it unambiguous in its domain (path, revision, host, root, cursor position, item set).
- **Receipt**: The evidence a check ran, bound to a single verdict — resolved target identity, examined and skipped counts with reasons, outcome classification, timestamp.
- **Outcome classification**: Exactly one of PASS, EMPTY, UNREAD, UNSEARCHABLE, FAIL.
- **Override**: A recorded engineer decision to proceed past a refusal, carrying a rationale and remaining visible thereafter.
- **Adoption report**: The honest per-area statement of which checks emit conforming receipts and which do not.
- **Adoption manifest**: The single checked-in enumeration of every area named in FR-017, each with an explicit adoption state and the date it was set. It is the sole authority for whether FR-008 binds; an area absent from it is an error, not a pass.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All thirteen witnessed instances are reproducible as deliberate faults, and **13 of 13** produce a loud, named refusal instead of a silent success.
- **SC-002**: **100%** of checks in the declared areas emit a conforming receipt with every verdict; any that do not are named in the adoption report rather than omitted.
- **SC-003**: A reader can determine, from the verdict alone and without re-running anything, whether a green was earned — verified by having a reader who did not run the check correctly classify **20 of 20** sample verdicts, including unearned ones.
- **SC-004**: Zero outcomes in the declared areas render UNREAD or UNSEARCHABLE as success, measured by fault injection across every check in scope.
- **SC-005**: A check pointed at an unresolvable target reports a non-success outcome in **100%** of injected cases; the historical exit-0-empty-board behaviour occurs zero times.
- **SC-006**: Every override is accompanied by a recorded rationale — **100%**, with zero silent suppressions.
- **SC-007**: The acceptance suite fails when any injected fault yields a clean pass, demonstrated by deliberately weakening one guard and confirming the suite goes red.
- **SC-008**: Time to identify which check produced an unearned green drops to **under 5 minutes** from the receipt alone, against a historical baseline of hours-to-days (instance 8 cost 14.5 h and a full day respectively).

## Assumptions

- **Scope is the fleet's own toolchain**, not the GLP language or runtime. The declared areas are buildkit-3rtask, buildkit-codexreview, the codeconv build gate, the COOP protocol, roadmap-sync, and the test harness. GLP semantics are untouched, so no §1.14 language-authority gate applies.
- **"Check" is deliberately broad.** Tests, reviews, gates, polls, imports and status probes are all in scope; the witnessed instances span all of these, and a contract covering only tests would leave most of the class open.
- **Receipts are additive.** Existing verdicts keep their current shape and gain a receipt beside them, so adoption can be incremental and no consumer breaks on day one.
- **Retrofit is incremental and honestly reported.** Not every site adopts simultaneously; FR-017/FR-018 exist precisely so partial adoption is visible rather than implied complete.
- **EMPTY must remain expressible as a pass.** If legitimate emptiness became a failure, engineers would suppress the mechanism, reintroducing the defect through the back door.
- **This feature hard-blocks F2–F6** (`multi-host-state-discipline`, `per-host-toolchain-contract`, `seam-specification`, `single-source-of-truth`, `product-defect-burn-down`) — recorded as hard edges in the roadmap catalog. Their acceptance suites are only trustworthy once a check cannot pass without running.
- **F3 will reuse this feature's loud-refusal mechanism** rather than building a second one; the two share that surface, per the conflict analysis fanned to the fleet on 2026-08-12.
- **F2 must start from the untracked-derived-artifacts state already landed** on `077-roadmap-sync-mechanics` (PR #153), not re-litigate it.
- **Overrides are engineer decisions, never agent decisions.** The mechanism records them; it does not grant them.
- **Known open item, recorded not resolved:** this feature's own roadmap row is **not slug-linked** to this spec directory. `link` matches a spec dir to a promoted feature by exact slug, and the roadmap slug (`verification-receipts-and-loud-failure-no-check-may-pass-without-proving-it-ran`) does not equal the directory name (`078-verification-receipts`). The directory was kept short to match the branch and the `NNN-short-name` convention. **Consequence: `reconcile` will not mirror this feature's stage state into the roadmap**, so its roadmap advances must be made explicitly by feature id rather than relied upon from reconcile. The same gap already affects `069-sc-002-il-parity-bridge`. This is instance 13 and is in scope for this feature's own fix, not a defect to route around.
