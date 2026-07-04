# Phase 0 Research: 042-crdtmsg-verify-harden

All planning-time unknowns resolved. Facts below were established by direct reads of the three
deliverables and git history on 2026-07-04 (planning session); anything the pass must
re-establish at execution time is marked so.

## R1. Verification baselines (resolves FR-005/FR-015 "delivery-time git state")

- **Decision**: F1/F2 delivery-time baseline = commit `c20317ce` (2026-07-03 22:25, the sweep
  commit that first landed both docs — `git log --follow` shows no later edits). F3
  delivery-time baseline = `6ecc975f` (2026-07-04 08:33) — F3 landed at `ee94a04f`, was amended
  by `3204bd1b` (E1–E9 encoding) and `6ecc975f` (E1 store-side fix); the "delivered method
  execution" includes the owner-ruling encoding, so conformance is judged against the final
  delivered state. 041 ship evidence baseline = tag `v2026.07.04.4` (`0945c29a`). Current-HEAD
  baseline = `042-crdtmsg-verify-harden` HEAD at pass execution time.
- **Rationale**: the clarified hybrid ruling says conformance/ledger re-derivation are judged
  against "what the scanners could see" — for F1/F2 that is the repo at delivery; for F3 the
  scanners additionally saw a pinned live-repo view (branch `037-virtual-3270-term`,
  post-040-implement, per F3 provenance) whose exact commit the pass must resolve from
  branch history as its first WP2 action.
- **Alternatives considered**: judging F3 against `ee94a04f` (pre-rulings) — rejected: the
  E1–E9 register is part of the executed method (owner escalation was a method stage), and
  §6's "this ruled register supersedes §1 ESC bins" makes the post-ruling doc the deliverable.

## R2. Method-record strength per deliverable (resolves FR-001 RECORDED/RECONSTRUCTED scoping)

Planning-time survey of the five method elements in each doc:

| Element | F1 | F2 | F3 |
|---|---|---|---|
| Source manifest | PARTIAL — 8 units named (L5–6), no scan-path/glob manifest | PARTIAL — derivation stated (184→148 from 10 theme agents; beacon-42/mstack-18/qmedit-50 extended), no query manifest | RECORDED — L4–5 (F1+F2+pinned repo; A/B/C family partition) |
| Claim schema | IMPLICIT — per-claim fields observable (finding, unit, path, date, signal, confidence, recency) but never declared | IMPLICIT — fixed entry shape (Title/Authors/Venue/URL/relevance/concepts/maps_to/strength) never declared | STATED-BUT-EXTERNAL — "86 schema-conformant claims"; field list deferred to run records/§16 (absent from file) |
| Rubric | PARTIAL — matrix legend S/P/W/·/X (L200) + appendix ranking rule (L268); confidence tiers undefined | PARTIAL — §11 well/moderate/thin DEFINED (L561); per-paper seminal/strong/ok tiers UNDEFINED | RECORDED — bin legend (L24), ≥2-family corroboration (L232), authority order (L5) |
| Failure-mode guards | RECORDED — keyword-collision traps ×3, wire-divergence, name-alias, recency-weighting guards (§14 L244–250) | LARGELY ABSENT — only inline recency/not-peer-reviewed cautions | RECORDED — false-consensus guard, same-family non-corroboration, feasibility veto, zero-self-decision (L232–236) |
| Stop rules | ABSENT — only a recency cutoff (a filter, not a stop rule) | ABSENT — 184→148 reported as outcome, not governed | RECORDED — L235 (1 cycle + targeted counter-queries; 28/28 on cycle 1 → stop) |

- **Decision**: the pass reconstructs F1/F2 methods with heavy RECONSTRUCTED marking and uses
  `docs/research/three-role-agent-teams/METHOD-AND-DOGFOOD.md` only as reconstruction context
  (spec Assumptions — the per-pipeline method as executed is binding). Elements genuinely
  absent (F1/F2 stop rules; F2 guards) are recorded as method GAPs in the conformance ledger,
  not backfilled as if they had existed.
- **Rationale**: FR-001 demands the RECORDED/RECONSTRUCTED distinction; spec Edge Case 2
  anticipates exactly this asymmetry.

## R3. Known pre-verification anomalies (planning-time observations → WP1/WP2 audit inputs)

Recorded here so the pass adjudicates them; NOT fixed during planning:

1. **F3 §8 names only 7 of the claimed 9 single-family survivors** (BB-ENC-7, BB-CRDT-7,
   BB-WIRE-5, BB-HDR-4, BB-RTE-3, BB-CRDT-10, BB-SCH-3 — an "e.g." list). The authoritative
   9 must be derived from the §1 table's Sources column (blocks with a single claim id /
   single family) before FR-004 re-adjudication can enumerate its targets.
2. **Block-count discrepancy risk**: F3 §8 corroboration arithmetic (14 three-family + 17
   two-family + 9 single-family = 40 blocks) may not match the §1 catalog's enumerated BB-*
   ids (planning-time mapper counted family prefixes summing to ~50; the §1 table itself
   appears to hold 40 rows). The pass re-derives the §1 row count and family-cardinality
   histogram exactly and reconciles or records a DEVIATION.
3. **F2 dangling interview provenance**: "the mstack interview" / "the interview" cited with
   no locatable path (F2 L444/L551/L555) — evidence-pointer candidates for US4 (the artifact
   exists in a sibling repo per F1 §15 #4: `.../PRINCIPAL-INTERVIEW-2026-06-18.md` — likely
   resolvable by pointer, host-blocked for content verification).
4. **F1 has no scanner identities or merge log** — "8 per-repo readers → Curating" with zero
   reconciliation record; the conformance ledger must record this as an execution-record GAP
   (US1 acceptance scenario 2), dispositioned per FR-014's in-doc-summary baseline.
5. **F2 2026-dated arXiv ids** (e.g. 2606.31759, 2601.02254): plausible relative to the
   2026-07-03 run date (past months), NOT presumed fabricated; the F2 bibliographic
   spot-check (R6) verifies existence and flags any that do not resolve.

## R4. Targeted re-execution protocol (resolves FR-014 execution shape)

- **Decision**: reuse the 3-role formalism for the singleton re-scans. Per singleton block:
  (a) identify the two families that did NOT corroborate it; (b) spawn one blind scanner agent
  per non-corroborating family (blind = given the family's source manifest and the claim
  TOPIC, not F3's verdict or the original claim text); (c) curator compares fresh claims to
  the block's recorded decision → confirmed / demoted / promoted verdict with evidence, or an
  explicit no-further-evidence ruling; (d) genuine conflicts escalate to the owner (FR-013).
  F3 merge decisions for multi-family blocks are re-derived from in-doc data only (§1 sources
  column + §2 catalog + §4 ledger), no re-scan. Contested/discrepant claims discovered during
  WP1/WP2 join the re-scan queue.
- **Rationale**: clarified ruling (option C); blindness preserves the false-consensus guard
  the original method used; family-targeted scanning bounds cost to ≤ 2 scans × 9 blocks +
  contested extras.
- **Alternatives considered**: full 3-scanner re-run per block (rejected — re-scanning the
  corroborating family adds nothing to a single-family weakness); trusting in-doc summaries
  for singletons too (rejected by the clarification ruling).

## R5. PROVISIONAL adjudication evidence map (US3 planning inputs)

The 8 rows (F3 §5) with planning-time evidence expectations — the pass re-establishes each
against current HEAD:

| Row | Trigger (condensed) | Planning-time expectation |
|---|---|---|
| BB-ENC-7 CBOR | non-term generic payload consumer appears | Already promoted by E3 ruling (PROV → ACCEPTED/MVP-CORE); register row likely stale — closure = record E3 + 041 CBOR surface evidence |
| BB-ENC-8 markdown | owner picks lossless vs render-only | Likely NOT met — re-affirm (owner-choice trigger = judgment, never self-promoted) |
| BB-SIG-4 hop attestation | E4 base ruled + nesting-growth bound designed | E4 ruled (half met); bound design = net-new work → re-affirm or roadmap follow-up |
| BB-VER-5 lenses | first restructuring migration need | Check 041 for restructuring migrations; likely re-affirm |
| BB-CRDT-7 Fugue/Peritext | first ordered/rich-content document type ships | 041 shipped rich-text CRDT (Fugue+Peritext MANDATORY in 041) — likely mechanically met → self-promote with 041 evidence |
| BB-CRDT-10 columnar history | history sync bandwidth-relevant | Likely not met → re-affirm |
| BB-RTE-4 distance-vector | mesh grows beyond static topology | Likely not met → re-affirm |
| BB-SCH-3 codegen | >2 runtimes hand-maintaining codecs | Count runtimes maintaining codecs at HEAD; likely not met → re-affirm |

- **Decision**: evidence for "met" = concrete shipped artifact (file/tag/spec reference quoted
  per row, FR-008); expectations above are hypotheses only — every row is re-adjudicated
  against current HEAD, and anything ambiguous (e.g. whether a 041 surface counts as a
  "non-term generic payload consumer") escalates rather than self-promotes.

## R6. F2 bibliographic re-verification depth (resolves spec Assumption boundary)

- **Decision**: two tiers. Tier 1 (mandatory): every load-bearing paper named in F2 §11's
  "Load-bearing papers" column — verify bibliographic existence (title/authors/venue/URL
  resolve). Tier 2 (best-effort sweep): all remaining curated entries get an existence check
  in batch (URL liveness or metadata match); link-rot is recorded as a disposition
  (spec Edge Case 4), not a failure. Full re-reads only for claims contested by another
  finding (per FR-014's contested-claim clause).
- **Rationale**: spec Assumption fixes spot-check depth; §11 verdicts are the F3-load-bearing
  surface, so their citations get the mandatory tier.

## R7. Evidence materialization layout (resolves FR-010/US4 target)

- **Decision**: new `docs/research/crdt-multiformat-messaging/evidence/` directory holds:
  per-singleton re-scan records (`f3-rescan-<block-id>.md`), the merge-log re-derivation
  (`f3-merge-rederivation.md`), any recovered artifacts, and an `evidence-index.md` mapping
  every pointer in the three docs → resolution or disposition. The consolidated report is
  `docs/research/crdt-multiformat-messaging/verification-report-042.md` (referenced from all
  three docs per SC-009). The 86-claim transcript pointer is dispositioned as unrecoverable
  (what was lost, what survives in-doc, confidence impact) and superseded by the targeted
  re-execution records — per FR-014.
- **Rationale**: keeps everything a downstream consumer needs in the epic's one research
  directory (Principle VIII); the report name carries the feature number for traceability.
- **Alternatives considered**: storing evidence under `specs/042.../` (rejected — spec dirs
  are pipeline artifacts, not the epic's living corpus); one giant report file with inlined
  evidence (rejected — per-singleton records need to be independently referenceable from the
  amended F3 rows).

## R8. Change-log format (resolves FR-011/SC-008 mechanics)

- **Decision**: each of the three docs gains a terminal `## Change log (042 verification pass)`
  section; one entry per amendment: `date · section touched · what changed · why (finding id
  in the report) · baseline label`. Every in-place edit made by the pass MUST have exactly one
  entry; the report's finding ids are the join key. Format contract in
  `contracts/amendment-changelog.md`.
- **Rationale**: SC-008's 10/10 sampling test needs a deterministic 1:1 edit↔entry mapping;
  finding-id join makes the report the single index of the whole pass.
