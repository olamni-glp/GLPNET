# Feature Specification: Verify + Harden F1/F2/F3 Against Their Own 3-Role Method Specs

**Feature Branch**: `042-crdtmsg-verify-harden`
**Created**: 2026-07-04
**Status**: Draft
**Input**: User description: "Verify + harden F1/F2/F3 against their own 3-role method specs — F1 (crdtmsg-priorart-sibling-scan), F2 (crdtmsg-webresearch-corpus), F3 (crdtmsg-buildingblocks-synthesis) were delivered via 3-role team pipelines; each pipeline's frozen method (source manifest, claim schema, rubric, failure-mode guards, stop rules) plus its execution records (claim sets, merge log, closure ledger) serve AS THE SPECS. Verify the deliverables fully implement those methods and harden the results: re-check coverage ledgers, adjudicate skipped singletons, close PROVISIONAL bins — so crdtmsg-mvp consumers can rely on them. Folded from codify note cn-20260704T064008-c1de4c16 (2026-07-04, Gabi)."

## Context

Three research features of the `crdt-multiformat-messaging` epic were delivered 2026-07-04 via 3-role
team pipelines (blind scanners → curator merge → owner escalation):

- **F1** `crdtmsg-priorart-sibling-scan` → `docs/research/crdt-multiformat-messaging/priorart-sibling-scan.md`
- **F2** `crdtmsg-webresearch-corpus` → `docs/research/crdt-multiformat-messaging/webresearch-corpus.md`
- **F3** `crdtmsg-buildingblocks-synthesis` → `docs/research/crdt-multiformat-messaging/buildingblocks-synthesis.md`

Each pipeline ran under a **frozen method** (source manifest, claim schema, rubric, failure-mode
guards, stop rules) and produced **execution records** (claim sets, merge log, closure ledger).
Per the owner's ruling (codify note `cn-20260704T064008-c1de4c16`), those methods + execution
records are **the specs** for this feature: the verification contract each deliverable is checked
against. No verification pass has yet confirmed the deliverables fully implement their own methods,
and several hardening obligations are known to be open (single-family survivor blocks, PROVISIONAL
register rows, scanner-C drift list, evidence-of-record pointers into session transcripts).

Since delivery, feature 041 `crdtmsg-mvp` **shipped** (v2026.07.04.4) consuming the F3 block
catalog — so some PROVISIONAL promotion triggers (e.g. "first rich-content document type ships")
may already be met, making parts of the register stale. The three documents are the epic's living
single source of truth; downstream consumers (`crdtmsg-xsd-style-schema-language`, `glp-policy-guard`,
post-MVP features) will keep reading them.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Method-conformance verification of each deliverable (Priority: P1)

As a design-team member relying on the F1/F2/F3 corpus, I want each deliverable audited against its
own frozen method — source manifest coverage, claim-schema conformance, rubric application,
failure-mode-guard execution, stop-rule satisfaction — so that I know the documents actually are
what their methods promised, with every gap named instead of assumed away.

**Why this priority**: This is the verification contract itself — the owner's ruling that the
methods + execution records ARE the specs. Without it, hardening (US2/US3) has no baseline: we
would be polishing documents whose foundational claims were never checked.

**Independent Test**: For one deliverable (e.g. F3), reconstruct its frozen method from the
in-repo record, check every method element against the shipped document, and produce a
conformance ledger with a PASS/GAP verdict + verbatim evidence for each element. Value delivered:
the first objective statement of whether a deliverable implements its own method.

**Acceptance Scenarios**:

1. **Given** a deliverable and its frozen method, **When** the conformance audit runs, **Then**
   every method element (manifest, schema, rubric, guards, stop rules) receives an explicit
   PASS / GAP / DEVIATION verdict with quoted evidence — no element left unexamined.
2. **Given** a method element whose execution record is absent from the repo (e.g. full claim sets
   preserved only in session transcripts), **When** the audit reaches it, **Then** the ledger
   records the evidence gap explicitly (what is missing, where it was said to live, what disposition
   was chosen) instead of silently passing it.
3. **Given** a discovered deviation between the frozen method and what was actually executed,
   **When** it is recorded, **Then** it is classified (harmless / weakens-a-claim / invalidates-a-claim)
   and every affected downstream claim is enumerated.

---

### User Story 2 - Harden the merged decisions: singletons, ledgers, drift, ruling propagation (Priority: P2)

As an MVP implementer consuming the F3 block catalog, I want the known-weak points hardened — the
9 single-family survivor blocks re-adjudicated, the three coverage ledgers re-checked, the
scanner-C drift list dispositioned, and the E1–E9 owner rulings propagated consistently into every
block's recorded status — so that a block's stated status is trustworthy at face value.

**Why this priority**: These are the concrete weak points the delivery itself flagged (F3 §8 method
audit). They are the most likely places a consumer gets misled today.

**Independent Test**: Re-adjudicate the single-family survivors alone: for each of the 9, record a
confirm/demote/promote verdict with corroborating evidence or an explicit counter-query result.
Value delivered: the false-consensus guard's residual risk is retired.

**Acceptance Scenarios**:

1. **Given** the 9 single-family survivor blocks (F3 §8), **When** hardening completes, **Then**
   each has a recorded re-adjudication verdict (confirmed / demoted / promoted) with evidence from
   at least one additional independent family, or an explicit "no further evidence exists" ruling.
2. **Given** the three coverage artifacts (F1 §12 signal×repo matrix, F2 §11 gap-coverage table,
   F3 §3 constraint matrix + §4 closure ledger 28/28), **When** each is re-derived from its
   sources, **Then** the re-derivation either reproduces the shipped ledger exactly or every
   discrepancy is enumerated and corrected in the document.
3. **Given** the scanner-C known-drift list (mesh routes JSON-only; payloadType constants
   duplicated; spec-vs-plan store naming; 037 @name promise), **When** hardening completes,
   **Then** each drift item has a disposition: corrected in the corpus, recorded as a roadmap
   follow-up, or ruled obsolete — none left dangling.
4. **Given** the E1–E9 rulings (F3 §6), **When** propagation is checked, **Then** every block whose
   status a ruling changed shows the post-ruling status consistently everywhere it appears across
   the three documents, with zero contradictions remaining.

---

### User Story 3 - Close the PROVISIONAL register against current reality (Priority: P3)

As the epic owner, I want each of the 8 PROVISIONAL register rows (F3 §5) re-adjudicated against
the **current** repo state — in particular the shipped 041 MVP — so that rows whose promotion
triggers have since been met are closed (promoted with evidence) and the remainder re-affirmed
with a still-valid trigger, leaving no stale bins.

**Why this priority**: Valuable but dependent on US1/US2 confidence; the register is small and its
staleness is bounded.

**Independent Test**: Adjudicate the register alone: for each row, evaluate its "promotes when"
trigger against the current repo (e.g. BB-CRDT-7 Fugue/Peritext — 041 shipped a rich-text document
type; BB-ENC-7 CBOR — E3 ruled it MVP-CORE and 041 shipped a CBOR surface). Value delivered: a
register with zero already-met triggers still marked PROVISIONAL.

**Acceptance Scenarios**:

1. **Given** a PROVISIONAL row whose trigger is now met, **When** adjudicated, **Then** the row is
   closed: block status updated with pointed evidence (what shipped, where), and the register row
   marked resolved.
2. **Given** a PROVISIONAL row whose trigger is not yet met, **When** adjudicated, **Then** the row
   is re-affirmed with the trigger restated against current reality (and corrected if the original
   wording is now ambiguous or wrong).
3. **Given** a closure that would require net-new implementation work (not evidence), **When**
   recorded, **Then** it is captured as a proposed roadmap follow-up — never implemented inside
   this feature.

---

### User Story 4 - Make the evidence record self-contained (Priority: P4)

As a future consumer (or a future verification pass), I want every evidence pointer in the three
documents to resolve to an in-repo artifact or carry an explicit availability disposition — in
particular the full 86-claim scanner output currently said to be "preserved in the F3 run records
(session transcripts)" — so the corpus stands on durable evidence rather than ephemeral session
state.

**Why this priority**: Materialization is the least urgent for current consumers but determines
whether any future audit is possible at all.

**Independent Test**: Enumerate all evidence pointers in the three docs; for each, verify
resolution or record disposition. Value delivered: zero dangling pointers.

**Acceptance Scenarios**:

1. **Given** an evidence pointer to a recoverable artifact, **When** materialization runs, **Then**
   the artifact is preserved in-repo (under the epic's research directory) and the pointer updated.
2. **Given** an evidence pointer whose artifact is unrecoverable, **When** dispositioned, **Then**
   the document records that explicitly (what was lost, what summary survives, what confidence
   impact follows) — never a silent dead pointer.

---

### Edge Cases

- Session transcripts holding the full claim sets may be partially or wholly unavailable — the
  verification depth then depends on the ruling under [NEEDS CLARIFICATION] below.
- A frozen method itself may be under-specified in the surviving record (e.g. F1/F2 methods are
  less explicitly materialized than F3's §8 audit) — the pass must first reconstruct each method
  from the best in-repo evidence and record where reconstruction is inference, not record.
- Source repositories named in manifests may be unavailable on this host (sibling Mac/Linux paths);
  verification of those manifest rows must record host-blocked status rather than fail or guess.
- F2's external sources may have link-rotted since the scan; the pass re-verifies bibliographic
  existence, not full re-reading, unless a claim is contested.
- A hardening verdict may contradict a decision the shipped 041 MVP already built on — such a
  contradiction is recorded and escalated to the owner; the 041 code is never modified by this
  feature.
- Re-derived ledgers may disagree with shipped ones in either direction (missed coverage or
  overclaimed coverage) — both are findings; neither is silently normalized.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The pass MUST reconstruct, for each of F1/F2/F3, its frozen method (source manifest,
  claim schema, rubric, failure-mode guards, stop rules) from in-repo records, marking every
  element as RECORDED (verbatim record exists) or RECONSTRUCTED (inferred, with the inference
  stated).
- **FR-002**: The pass MUST verify each deliverable against every element of its own method and
  produce a per-feature conformance ledger with PASS / GAP / DEVIATION verdicts and verbatim
  evidence for each element; no element may be omitted.
- **FR-003**: Every deviation found MUST be classified (harmless / weakens-a-claim /
  invalidates-a-claim) with affected downstream claims enumerated.
- **FR-004**: The pass MUST re-adjudicate all 9 single-family survivor blocks (F3 §8) and record a
  confirmed / demoted / promoted verdict per block with the corroborating evidence or an explicit
  no-further-evidence ruling.
- **FR-005**: The pass MUST re-derive the three coverage artifacts (F1 §12 matrix, F2 §11 table,
  F3 §3 matrix + §4 closure ledger) from their sources and either confirm them exactly or enumerate
  and correct every discrepancy.
- **FR-006**: The pass MUST disposition every scanner-C known-drift item (F3 §8): corrected in the
  corpus, recorded as a proposed roadmap follow-up, or ruled obsolete.
- **FR-007**: The pass MUST verify E1–E9 ruling propagation: every block status touched by a ruling
  is consistent across all its appearances in the three documents; inconsistencies are corrected.
- **FR-008**: The pass MUST re-adjudicate all 8 PROVISIONAL register rows (F3 §5) against current
  repo state; rows with met triggers are closed with evidence, others re-affirmed with corrected
  triggers.
- **FR-009**: Closure or promotion requiring net-new implementation MUST be captured as a proposed
  roadmap follow-up and MUST NOT be implemented within this feature.
- **FR-010**: The pass MUST enumerate every evidence pointer in the three documents and either
  resolve it to an in-repo artifact (materializing recoverable ones) or record an explicit
  availability disposition.
- **FR-011**: All hardening edits MUST be made to the three documents in place (they remain the
  epic's single source of truth), and each amended document MUST carry a change-log section
  recording every amendment of this pass with its rationale — zero silent edits.
- **FR-012**: The pass MUST produce one consolidated verification report for the feature (the
  conformance ledgers, adjudication verdicts, dispositions, and materialization outcomes in one
  place) stored with the epic's research corpus.
- **FR-013**: Contradictions between a hardened verdict and decisions the shipped 041 MVP built on
  MUST be recorded in the verification report and escalated to the owner — never self-ruled and
  never patched into 041.
- **FR-014**: Verification depth for claims whose full execution records are unavailable in-repo
  MUST follow [NEEDS CLARIFICATION: the full claim sets (86 claims, per-claim fields) are recorded
  as living in session transcripts, which may be gone — when a claim's execution record is
  unrecoverable, is verification against the in-doc summaries + independent spot re-derivation
  sufficient, or must the affected scan be RE-EXECUTED (fresh blind re-scan of the named sources)
  to regenerate the evidence?].

### Key Entities

- **Frozen method**: the per-pipeline verification contract — source manifest, claim schema,
  rubric, failure-mode guards, stop rules; RECORDED or RECONSTRUCTED per element.
- **Execution record**: what the pipeline actually did — claim sets, merge log, closure ledger;
  each item resolvable in-repo or dispositioned.
- **Conformance ledger**: per-deliverable table of method elements × verdict (PASS/GAP/DEVIATION)
  × evidence.
- **Adjudication verdict**: per-block outcome for singletons and PROVISIONAL rows —
  confirmed/demoted/promoted/closed with evidence.
- **Drift disposition**: per known-drift-item outcome — corrected / roadmap follow-up / obsolete.
- **Verification report**: the consolidated, durable output artifact of the pass.
- **Change log**: per-document amendment record for all in-place hardening edits.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 3 of 3 deliverables have a complete method-conformance ledger — every method element
  carries a verdict + evidence; zero unexamined elements remain.
- **SC-002**: 9 of 9 single-family survivor blocks carry a recorded re-adjudication verdict with
  evidence or an explicit no-further-evidence ruling.
- **SC-003**: 8 of 8 PROVISIONAL register rows are re-adjudicated; zero rows with already-met
  triggers remain marked PROVISIONAL.
- **SC-004**: The re-derived closure ledger reproduces 28/28 constraint coverage or every
  discrepancy found is enumerated and corrected — zero unresolved ledger contradictions.
- **SC-005**: 4 of 4 scanner-C known-drift items carry a disposition; zero dangling.
- **SC-006**: Zero cross-document inconsistencies remain in E1–E9-affected block statuses.
- **SC-007**: 100% of evidence pointers in the three documents resolve in-repo or carry an explicit
  availability disposition; zero dangling transcript pointers.
- **SC-008**: Every amendment appears in its document's change log; a reviewer sampling any 10
  amendments finds 10/10 recorded — zero silent edits.
- **SC-009**: The consolidated verification report exists in the epic's research corpus and is
  referenced from all three hardened documents.

## Assumptions

- **Adjudicative, not constructive**: this feature verifies and hardens documents; it implements
  no blocks, writes no production code, and never modifies shipped 041 code. Anything needing
  implementation becomes a proposed roadmap follow-up (FR-009).
- **In-place hardening**: the three documents are the epic's living single source of truth, so
  amendments land in them directly with change logs (FR-011), not in parallel addendum documents.
- **F2 source re-verification is bibliographic** (the source exists and says what the corpus claims
  at spot-check depth), not a full re-read of ~150 external sources, unless a specific claim is
  contested by another finding.
- **Host-blocked manifest rows** (sibling-repo paths unavailable on this Windows host) are recorded
  as such and do not fail the pass.
- **Owner escalation stays the rule**: genuine conflicts discovered by this pass go to the owner
  (as F3's zero-self-decision rule required); the pass never self-rules a contested decision.
- **The 3-role team formalism** (`docs/research/three-role-agent-teams/METHOD-AND-DOGFOOD.md`) is
  context for reconstructing the methods, but the per-pipeline frozen methods as executed are the
  binding contract.
