# Data Model: 042-crdtmsg-verify-harden

The pass produces structured verification records embedded in Markdown artifacts (no database).
Entities below define the record shapes; the contracts/ files fix their serialized table
formats. Every record carries a **baseline label** (FR-015): `DELIVERY(<commit>)` or
`HEAD(<commit>)`.

## MethodElement (FR-001)

One row per method element per deliverable (5 elements × 3 deliverables = 15 minimum).

| Field | Type | Notes |
|---|---|---|
| feature | F1 \| F2 \| F3 | |
| element | manifest \| claim_schema \| rubric \| guards \| stop_rules | |
| provenance | RECORDED \| RECONSTRUCTED | RECONSTRUCTED carries the inference statement (spec Edge Case 2) |
| statement | text | the method element as reconstructed, with doc line refs |
| source_refs | list | where in the doc/repo the element is stated (or the inputs to the inference) |

Validation: all 15 cells present; no element may be skipped (FR-002 "no element omitted").

## ConformanceFinding (FR-002, FR-003)

One row per method element per deliverable in the conformance ledger.

| Field | Type | Notes |
|---|---|---|
| finding_id | `CF-<F#>-<seq>` | join key for change-log entries and report cross-refs |
| feature / element | as above | |
| verdict | PASS \| GAP \| DEVIATION | GAP = record absent/incomplete; DEVIATION = executed differently than frozen |
| evidence | verbatim quote(s) | mandatory, with doc/commit line refs |
| baseline | DELIVERY(commit) | conformance is always delivery-time (FR-015) |
| deviation_class | harmless \| weakens-a-claim \| invalidates-a-claim | required iff verdict = DEVIATION (FR-003) |
| affected_claims | list of block/claim ids | required iff deviation_class ≠ harmless |
| disposition | text | for GAPs: what is missing, where it was said to live, what was chosen (US1 AS2) |

## SingletonAdjudication (FR-004, FR-014)

One per single-family survivor block (9, list derived per research.md R3.1).

| Field | Type | Notes |
|---|---|---|
| block_id | BB-XXX-N | |
| original_family | F1 \| F2 \| REPO | the one family that sourced it |
| rescan_records | list of evidence/ file refs | one per non-corroborating family re-scanned (blind, R4) |
| verdict | confirmed \| demoted \| promoted \| no-further-evidence | |
| evidence | quote/ref | from the fresh scan, or the explicit no-further-evidence ruling |
| escalated | bool | true → owner item in report §Escalations |
| baseline | HEAD(commit) | hardening runs at current HEAD (FR-015) |

## LedgerRederivation (FR-005)

One per coverage artifact: F1 §12 matrix, F2 §11 table, F3 §3 matrix, F3 §4 closure ledger.

| Field | Type | Notes |
|---|---|---|
| artifact | enum of the four | |
| baseline | DELIVERY(commit) | per hybrid ruling |
| result | REPRODUCED-EXACTLY \| DISCREPANT | |
| discrepancies | list {cell, shipped, rederived, direction} | direction = missed-coverage \| overclaimed (spec Edge Case 6) |
| correction_ref | change-log entry id(s) | required iff DISCREPANT (corrected in the doc) |

## DriftDisposition (FR-006)

One per scanner-C drift item (4: mesh-routes-JSON-only, payloadType-constants-duplicated,
spec-vs-plan store naming, 037 @name promise).

| Field | Type | Notes |
|---|---|---|
| item | text (the drift as recorded in F3 §8) | |
| disposition | corrected-in-corpus \| roadmap-follow-up \| obsolete | none may remain dangling (SC-005) |
| evidence | text + refs | current-HEAD check of whether the drift still holds |
| follow_up_ref | proposed roadmap item name | required iff roadmap-follow-up (FR-009) |

## RulingPropagationCheck (FR-007)

One per ruling E1–E9 × per document appearance.

| Field | Type | Notes |
|---|---|---|
| ruling | E1..E9 | |
| touched_blocks | list of BB ids the ruling changed | from F3 §6 |
| appearances | list {doc, section/line, status-as-shown} | across all three docs |
| consistent | bool | false → corrected + change-log entry; SC-006 requires zero remaining |

## ProvisionalAdjudication (FR-008, FR-009)

One per register row (8, F3 §5).

| Field | Type | Notes |
|---|---|---|
| row_id | BB-XXX-N | |
| trigger_shipped | text | the "promotes when" as shipped |
| trigger_met | met \| not-met \| ambiguous | judged against HEAD(commit) |
| action | self-promoted \| re-affirmed \| escalated | self-promote ONLY for mechanically-met (clarified ruling); ambiguous → escalated |
| evidence | quoted shipped artifact refs | mandatory iff self-promoted (batch-listed in report for owner review) |
| trigger_restated | text | required iff re-affirmed (corrected wording where stale) |
| follow_up_ref | roadmap item | iff closure needs net-new work (FR-009) |

## EvidencePointer (FR-010)

One per pointer enumerated in the three docs.

| Field | Type | Notes |
|---|---|---|
| pointer_id | `EP-<seq>` | |
| doc / location | ref | |
| pointer_text | verbatim | |
| class | in-repo \| sibling-repo \| external-url \| session-transcript \| named-corpus | |
| resolution | resolves \| materialized(evidence/ path) \| host-blocked \| link-rot \| unrecoverable | every one gets exactly one (SC-007) |
| disposition_note | text | required for host-blocked / link-rot / unrecoverable (what was lost, what survives, confidence impact) |

## AmendmentEntry (FR-011) — see contracts/amendment-changelog.md

`{date, doc, section, change, finding_id, baseline}` — 1:1 with every in-place edit (SC-008).

## VerificationReport (FR-012) — see contracts/verification-report.md

The single artifact aggregating all of the above + the owner-review batches (FR-008
promotions; FR-013 escalations, including any hardened-verdict-vs-041 contradictions).

## Relationships

```
MethodElement (15) ──1:1──> ConformanceFinding (15+)         [WP1]
ConformanceFinding.deviation → affected_claims → BB blocks
SingletonAdjudication (9) ──uses──> evidence/ rescan records  [WP2]
LedgerRederivation (4) / DriftDisposition (4) / RulingPropagationCheck (9×docs) [WP2]
ProvisionalAdjudication (8)                                    [WP3]
EvidencePointer (N, enumerated) ──may create──> evidence/ files [WP4]
* ──every doc edit──> AmendmentEntry ──finding_id──> VerificationReport [WP5]
```
