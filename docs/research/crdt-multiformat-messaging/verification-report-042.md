# Verification Report — 042-crdtmsg-verify-harden

> **Run date**: 2026-07-04 · **Pass executor**: Claude (feature `042-crdtmsg-verify-harden`, marathon run `mrun-5b9a5befaae8`; all verification labor Claude agents per Constitution V)
> **Verdict summary**: PENDING — populated at report assembly (T029).
>
> **Test-Protocol baseline note (T003)**: `bash test/run_all_tests.sh` is **environment-blocked on
> this Windows host** — the script hard-invokes `/home/user/dart-sdk/bin/dart` (absent here), a
> pre-existing harness/env mismatch recorded in `docs/known-issues.md` §"Feature 041 — GLP REPL
> baseline on Windows (T056)". Section A errors 198/204 with "No such file or directory" and the
> run aborts in Section B. This feature's diff is documentation-only (no code-test surface is
> touched), so the bracketing baseline is recorded as ENV-BLOCKED (identical state expected at
> re-test, T030), not green. Pre-change HEAD: `6ff3a8c9`.

## Verification baselines (FR-005/FR-015 hybrid ruling; plan.md table, scanner-C row resolved by T002)

| Baseline | Commit | Used for |
|---|---|---|
| F1/F2 delivery-time | `c20317ce` (2026-07-03 22:25 +0100) | F1/F2 method-conformance + F1 §12 / F2 §11 ledger re-derivation |
| F3 delivery-time | `6ecc975f` (2026-07-04 08:33 +0100; initial delivery `ee94a04f` 07:52, amended `3204bd1b` E1–E9 encoding, `6ecc975f` E1 store-side fix) | F3 method-conformance + §3/§4 ledger re-derivation |
| F3 scanner-C repo view | `d2689a71` (2026-07-04 07:44 +0100) — **resolved this pass (T002)**: the `037-virtual-3270-term` branch head immediately preceding F3's delivery commit `ee94a04f`. Candidates `c20317ce..d2689a71` differ only in `.specify/` roadmap-sync exports + codify notes (verified by `git diff --name-only c20317ce d2689a71`), so every scanned surface (docs/, specs/, csharp/, glp_runtime/) is identical across the candidate range — the residual ambiguity is immaterial. | re-deriving what scanner C could see |
| Current HEAD | `6ff3a8c9` (branch `042-crdtmsg-verify-harden`, execution-start HEAD) | hardening, PROVISIONAL closure, drift dispositions, evidence materialization |
| 041 ship evidence | tag `v2026.07.04.4` = `0945c29a` | PROVISIONAL trigger adjudication (US3) |

## 1. Method reconstruction (FR-001)

All line refs cite the doc at its DELIVERY baseline (F1/F2 `c20317ce`, F3 `6ecc975f`); the docs
are byte-identical at HEAD up to the 042 change-log section (per-file `git log`), so refs remain
navigable. Reconstruction context: `docs/research/three-role-agent-teams/METHOD-AND-DOGFOOD.md`
(the 3-role formalism — planning triad generator→validator→curator; execution triad blind
scanners→evaluator→curator); per spec Assumptions it informs inference only — the per-pipeline
method as executed is the binding contract.

### 1.1 F1 — priorart-sibling-scan (baseline DELIVERY(c20317ce))

| element | provenance | statement (as reconstructed) | source_refs |
|---|---|---|---|
| source manifest | RECONSTRUCTED | 8 scan units, one blind reader per repo, consolidated by a Curating role: `glpnet · buildkit-beacon · mstack (MSTACK + mstack-coop) · olamnit (+assistant/coop) · buildkit · crucible · qhstate (+Yngenios/coop) · research-loose` — the unit list is RECORDED verbatim (L6) and the 8-reader shape is stated (L5 "consolidates raw findings from 8 per-repo readers"), but no scan-path/glob manifest, no per-reader assignment record, and no reader claim sets survive in-repo. Inference: whole-repo scans per unit, one reader each. | F1 L5–L6 |
| claim schema | RECONSTRUCTED | Never declared. Fields observable uniformly across the doc + §15 appendix: {finding, unit, path (absolute), date/mtime, signal mapping S1–S8, confidence ∈ high/med/low, recency flag pre/post-cutoff}. Inference from the fixed §15 table columns (`# · File · Date · Signals · Conf`, L270) and inline claim carriage ("Confidence + recency are carried on every claim", L7). | F1 L7, L268–L304 |
| rubric | RECONSTRUCTED | Two recorded fragments + one undefined tier set: (a) coverage-matrix legend RECORDED — "**S** strong (built/defined here since cutoff) · **P** partial · **W** weak/reference-only · **·** absent · **X** keyword-collision trap" (L200); (b) appendix ranking rule RECORDED — "Rank by (recency since cutoff × directness × confidence)" (L268); (c) the confidence tiers high/med/low are USED throughout §15 but never defined — reconstructed as reader judgment with no recorded criteria. | F1 L200, L268 |
| failure-mode guards | RECORDED | Explicit §14 "Risks & caveats": keyword-collision traps ×3 (spec-035 "semantic-tombstone" = codeconv inventory metadata, NOT a message tombstone, L244; CRDT surveyed-and-deferred ≠ built, L245; olamnit/beacon "QUIC" hits are zero — WS/MCP only, L246); wire-divergence caveat (concept-convergence hides byte-divergence, L247); name-alias guard ("Marcel" = "Marcelle", L248); recency-weighting guard (pre-cutoff artifacts weighted down, L249). | F1 L243–L250 |
| stop rules | RECONSTRUCTED | ABSENT from the record. The only bounding device is the recency cutoff 2026-06-05 (L4) — a source FILTER, not a stop rule. No cycle count, no coverage-closure criterion, no re-scan condition anywhere. Inference: single consolidation pass over the 8 readers' output, stopping when consolidation completed — recorded here as an absence, not backfilled (research.md R2). | F1 L4 (absence elsewhere) |

### 1.2 F2 — webresearch-corpus (baseline DELIVERY(c20317ce))

| element | provenance | statement (as reconstructed) | source_refs |
|---|---|---|---|
| source manifest | RECONSTRUCTED | Derivation stated but query-level manifest absent: "consolidates 184 candidate papers from 10 theme search agents into a final curated index" (L4), 148 curated (L5), per-theme counts table RECORDED (L27–L39: schema-languages 12 · encodings-spectrum 19 · schema-evolution 19 · skip-unknown 10 · crdt-foundations 20 · crdt-systems 8 · crdt-messages-stores 14 · capability-tokens 16 · signatures-attestation 15 · transport-routing 15). Scope rule RECORDED: external literature extending the internal corpora beacon-42/mstack-18/qmedit-50 with `[INTERNAL-OVERLAP]` tagging (L6). NO query manifest (which searches each theme agent ran) and no per-agent candidate lists survive. | F2 L4–L7, L25–L39 |
| claim schema | RECONSTRUCTED | Never declared. Fixed entry shape observable across all 148 entries: {seq, title, authors, year, venue, URL, summary sentence(s), *concepts* list, *maps_to* (canonical S/gap ids — the agents' own guesses "re-mapped… during curation", L7), strength tier}. Inference from uniform entry layout in §1–§10. | F2 L7, entry layout §1–§10 |
| rubric | RECONSTRUCTED | Split record: (a) §11 gap-coverage verdict tiers RECORDED and defined — "**well** = external literature richly covers the design space · **moderate** = component pieces exist, the exact artifact is partial · **thin** = only academic/analogue, the artifact is genuinely net-new" (L561); (b) per-paper strength tiers **seminal/strong/ok** applied to every one of the 148 entries but never defined anywhere — reconstructed as curator judgment (seniority/foundationality ordering) with no recorded criteria. | F2 L561; tiers used throughout §1–§10 |
| failure-mode guards | RECONSTRUCTED | LARGELY ABSENT as a designed element. Observable inline cautions only: recency/not-peer-reviewed weighting notes on 2026 arXiv entries ("*(Recent, not peer-reviewed — weight accordingly.)*", §7 items 13–14, L392/L395–396); the `[INTERNAL-OVERLAP <corpus>]` tag as an implicit double-count guard (L6); one folded-duplicate note (bet365/Riak-DT, L347). No keyword-collision, fabrication, or link-rot guards recorded — an F2 method gap per research.md R2, recorded as absent. | F2 L6, L347, L392–L396 |
| stop rules | RECONSTRUCTED | ABSENT. The 184→148 curation is reported as an outcome (L4–L5) with no recorded inclusion/exclusion criterion, no per-theme quota, and no saturation/closure rule. Inference: curation stopped when the 10 theme sets were merged and deduplicated once. Recorded as an absence. | F2 L4–L5 (absence elsewhere) |

### 1.3 F3 — buildingblocks-synthesis (baseline DELIVERY(6ecc975f))

| element | provenance | statement (as reconstructed) | source_refs |
|---|---|---|---|
| source manifest | RECORDED | Three hard-disjoint evidence FAMILIES, one blind scanner each: A = F1 doc only, B = F2 corpus only, C = live glpnet repo pinned set only (branch `037-virtual-3270-term`, post-040-implement — resolved to `d2689a71` by this pass, see baselines table). Provenance line names all inputs (L4); method line records the family partition and blindness (L5). | F3 L4–L5 |
| claim schema | RECONSTRUCTED | Existence RECORDED, field list EXTERNAL/ABSENT: "emitted 86 schema-conformant claims (A:30, B:29, C:27)" (L5, L230) and "per-claim fields intact" (L237), but the field list is deferred to "the three claim sets, appendix §16" (L81) — **no §16 exists in the file** (sections run 0–8) and the claim sets live in session transcripts (L237). Reconstructed fields from the §1 Sources column claim-id usage (A-*/B-*/C-* ids clustered per design slot): {claim_id, family, design-slot, content, source citation, signals/gaps mapping}. | F3 L5, L81, L230, L237 |
| rubric | RECORDED | Bin legend: "**ACC** accepted · **PROV** provisional · **ESC** escalated (owner decision required) · **FI** force-include (OC-mandated, gaps marked)" (L24); MVP tiers CORE/OPT/POST carried per block (§1 table); scoring gate = corroboration rule (below) + authority order (below). | F3 L24, §1 table |
| failure-mode guards | RECORDED | Four explicit guards: false-consensus guard (corroboration requires ≥2 distinct FAMILIES, L232); same-family non-corroboration ("two same-family scanners cannot corroborate", L5); feasibility veto duty on scanner C (the known-drift list is its output, L236); zero-self-decision rule ("Conflicts are ESCALATED, never self-decided", L5; "Curator made zero self-decisions on genuine conflicts", L234). | F3 L5, L232, L234, L236 |
| stop rules | RECORDED | Coverage-closure stop: "ledger closed 28/28 on cycle 1 → stop rule met, no cycle-3 re-scan" (L235) — i.e. stop when the closure ledger (§4) covers all 28 rows; otherwise re-scan cycles would continue. | F3 L235, §4 |
| authority order (additional) | RECORDED | "Authority order (validator V-3.5, adopted): brief constraints > repo head > F1 > F2 > inference" (L5); applied to resolve recorded conflicts with losers preserved as alternatives_rejected (L233). | F3 L5, L233 |
| corroboration rule (additional) | RECORDED | "≥2 distinct FAMILIES (F1/F2/REPO), per the false-consensus guard. 3-family: 14 blocks. 2-family: 17. Single-family survivors: 9 (each counter-queried…)" (L232). | F3 L232 |
| cycle protocol (additional) | RECONSTRUCTED | Cycle STRUCTURE partially recorded: "1 full cycle + targeted counter-queries" executed, cycle-3 re-scan conditionally skipped (L235) — implying a designed multi-cycle protocol (≥3 cycles available) whose full definition lived in the planning triad's "8 recorded decisions" (L5), which are NOT enumerated in-repo. Reconstructed: cycle 1 = full 3-scanner sweep; cycle 2 = targeted counter-queries on singletons/conflicts; cycle 3 = re-scan, triggered only if the closure ledger had not closed. | F3 L5, L235 |

**§1 totals**: 18 method elements (F1: 5, F2: 5, F3: 8) — F1 1 RECORDED / 4 RECONSTRUCTED;
F2 0 RECORDED / 5 RECONSTRUCTED; F3 6 RECORDED / 2 RECONSTRUCTED. The F1/F2
RECONSTRUCTED-heaviness is the recorded property research.md R2 predicted, not a defect
introduced here; absences (F1/F2 stop rules, F2 guards) are recorded as absences, not backfilled.

## 2. Conformance ledgers (FR-002/FR-003, SC-001)

### 2.1 F1 conformance ledger (CF-F1-*)

| finding_id | element | provenance | verdict | evidence (verbatim, `F1 L<n>`) | gap disposition / deviation class | affected claims | baseline |
|---|---|---|---|---|---|---|---|
| CF-F1-1 | source manifest | RECONSTRUCTED | GAP | Coverage side verifiable: L5 "consolidates raw findings from 8 per-repo readers"; L6 names all 8 units; all 8 appear as §12 matrix columns (L202) and every unit yields body findings (glpnet §3; beacon §5 L81/§6 L94; mstack §4 L62/§6 L93; olamnit §5 L79/§6 L96; buildkit §6 L98/§10 L173; crucible §6 L97/§7 L132; qhstate §5 L78/§7 L126; research-loose §10 L179, §15 rows 32–33). Execution-record side ABSENT: no scanner identities, no per-reader claim sets, no merge/reconciliation log anywhere in the 309 lines. | GAP: per-reader outputs lived in reader sessions, not in-repo — unrecoverable; consolidated in-doc findings (§3–§11, §12, §15) accepted as evidence baseline per FR-014 | none directly (unit coverage independently confirmable in-doc); traceability of any single claim to its originating reader is lost | DELIVERY(c20317ce) |
| CF-F1-2 | claim schema | RECONSTRUCTED | DEVIATION | L7 promises "Confidence + recency are carried on every claim". §15 (L270–L304): all 33 rows carry Conf; but Date holds only a recency flag on rows 26/28/32, month-only on row 17, split tiers on rows 21/32. Body claims break the promise: the §3 Gleam-port claim (L47 "A Gleam-term surface exists for S1.") carries no confidence, no date, and has NO §15 row; §3 040-tmsg (L49) and gleam_quic (L50) carry dates but no inline confidence; §4 provenance table (L58–L66) has no Conf column; §6/§7 use "STRONG" (rubric tier), not high/med/low. | DEVIATION: **weakens-a-claim** | F1 §5 row "glpnet 038 + glp_gleam" (L80) and §12 S1/glpnet=S (L204) partially rest on the metadata-less Gleam-term claim; F3 blocks consuming F1 claims about the Gleam-term surface (S1 interchange, e.g. BB-ENC-5/BB-ENC-9 anchors) and §4 S8 provenance rows inherit claims whose promised confidence weighting is absent | DELIVERY(c20317ce) |
| CF-F1-3 | rubric | RECONSTRUCTED | GAP | Recorded fragments ARE applied: legend L200 used consistently in §12 (e.g. L204 "P (JSON)", L209 "· (neg)", L208 "· (oos)"); ranking rule L268 evidenced by §15 ordering (rows 1–25 high → 26–30 med → 31–33 low/mixed). But (a) the **X** tier is defined and NEVER used — no §12 cell carries X although §14 declares 3 traps (L244–L246); traps were handled by down-rating instead; (b) confidence tiers high/med/low used on all 33 §15 rows yet defined nowhere. | GAP: confidence-tier criteria and directness weights never recorded — unrecoverable; in-doc usage accepted per FR-014. X-tier non-application noted as harmless within-rubric deviation | §15 ranking order and every §12 S-vs-P-vs-W distinction rest on unrecorded judgment criteria; F3 blocks weighting F1 claims by confidence inherit the undefined tiers | DELIVERY(c20317ce) |
| CF-F1-4 | failure-mode guards | RECORDED | PASS | All 6 recorded guards (L244–L250) demonstrably executed: trap#1 — §8 L149 excludes spec-035 from S4 and §8's S4 discussion (L142–L147) never cites it; trap#2 — §10 L177 glpnet CRDT "deliberately deferred", L178 mstack "NEGATIVE: no CRDT"; trap#3 — §9 L162 olamnit "WAL/WebSocket/WSS, NOT QUIC", L163 "NO QUIC" for beacon/crucible/qhstate; wire-divergence — L18 "converge on *concepts* … but not on *bytes*", L219; Marcel=Marcelle — L68; recency-weighting — pre-cutoff flags at L43/L45/L127 and §15 rows 26/28/31/32. | — | — | DELIVERY(c20317ce) |
| CF-F1-5 | stop rules | RECONSTRUCTED | GAP | Element absent from the record; cannot PASS. Only bounding device is L4's recency cutoff — a source FILTER, not a stop rule. No cycle count, coverage-closure criterion, or re-scan condition anywhere in L1–L309; footer L308 reports outcomes only. | GAP: recorded-as-absent per FR-001; single-pass-consolidation inference accepted; underlying stop criterion unrecoverable — the method itself is under-specified here | any completeness claim over the 8 units (that scanning stopped because coverage closed rather than by fiat) is unverifiable; F3 blocks relying on F1 negative/absence claims (e.g. "defined **nowhere**", L221–L223) inherit this | DELIVERY(c20317ce) |

Auditor notes (T007): the X-tier is orphaned (defined L200, used nowhere — traps absorbed as
down-ratings; T013 should not expect X cells). The §3 Gleam-port claim (L47) is the one
substantive body claim with no §15 row and no confidence/date — the concrete counterexample to
L7. §15 ordering has one inversion (rows 31/32) defensible only via the unrecorded directness
weight. Footer tallies (33 sources · 9 gaps · 8 units) verify. Typo L240 `DuglexLink` (correct
at L161/L290) — cosmetic, uncorrected (no claim impact).

### 2.2 F2 conformance ledger (CF-F2-*)

| finding_id | element | provenance | verdict | evidence (verbatim, `F2 L<n>`) | gap disposition / deviation class | affected claims | baseline |
|---|---|---|---|---|---|---|---|
| CF-F2-1 | source manifest | RECONSTRUCTED | GAP | Verified parts hold: per-theme table L29–L39 (12/19/19/10/20/8/14/16/15/15 = 148) reconciles EXACTLY with an independent per-section count (zero numbering breaks §1–§10); overlap tagging executed — 31 `[INTERNAL-OVERLAP …]` tags, spot-checked at L96/L154/L255/L404/L446 per the L6 keep-but-tag rule. Missing: L4 "consolidates 184 candidate papers from 10 theme search agents" vs L5 "**Total papers (curated):** 148" — the 184→148 derivation is NOT traceable (no list of the 36 dropped, no drop reasons, no per-agent candidate lists); no query manifest anywhere in-repo. | GAP: query manifest + 36-drop list unrecoverable (lived in agent transcripts); in-doc-summary baseline per FR-014. Counts/tagging verified true — only the upstream derivation is missing. | none directly (the 148-entry index is internally consistent); the un-audited 36 drops cap confidence in per-theme *completeness* claims feeding §11 verdicts (esp. the "thin" calls gap3/gap7) and F3 B-* claims | DELIVERY(c20317ce) |
| CF-F2-2 | claim schema | RECONSTRUCTED | PASS | Fixed shape {seq, title, authors, year, venue, URL, summary, concepts, maps_to, tier} carried on all 148 entries — mechanically verified: 148 `*concepts:*` lines, 148 `*maps_to:*` lines, 148 tier tokens (55 seminal + 65 strong + 28 ok), 148 headings each with URL + year; every-10th-entry spot-check complete (e.g. L74–L76 Kaitai). Blemish: 3 entries carry an arXiv-id placeholder in the authors slot (L391 "— (arXiv 2606.31759) (2026)", L394, L498) — deferred to T027 bibliographic check, not a schema break. | — | none — no missing URL/maps_to/tier anywhere | DELIVERY(c20317ce) |
| CF-F2-3 | rubric | RECONSTRUCTED | GAP | §11 gap tiers defined (L561) and applied: 8 of 9 verdicts consistent with their rationale (e.g. gap7 L571 thin; gap1 L565 moderate). One application blemish: gap3 L567 verdict "**moderate→thin**" is a hybrid value outside the 3-value legend (rationale genuinely straddles both; no verdict flip either way). Per-paper tiers **seminal/strong/ok** applied to all 148 entries (55/65/28) but DEFINED NOWHERE. | GAP: per-paper tier criteria unrecoverable (curator judgment, never recorded); in-doc usage accepted per FR-014. gap3 hybrid = harmless legend deviation. | per-paper tier weights feed F3 B-* claim strength; gap3 hybrid affects §11 gap3 row only (both readings mark the message-level tombstone net-new) | DELIVERY(c20317ce) |
| CF-F2-4 | failure-mode guards | RECONSTRUCTED | GAP | Largely absent as a designed element — no keyword-collision, fabrication, or link-rot guards (observable cautions only: overlap tag L6; folded-duplicate note L347). Guard-execution census of ALL 2026 arXiv ids (exactly 4): `2606.31759` L392 and `2606.18966` L395 fully flagged "(Recent, not peer-reviewed…)"; `2601.02254` (Vouchsafe §8.10, L432) and `2603.24775` (AIP §9.14, L499) carry only "*(Recent.)*" — the "not peer-reviewed" caution ABSENT. All 4 carry tier **ok** (lowest), so weight-down was applied via tier. | GAP (designed guard set absent — in-doc-summary baseline per FR-014) + guard-execution inconsistency on 2/4 recency flags, class **harmless** (recency IS flagged; ok tier already discounts) | F2 §8.10 (S2, gap9) and §9.14 (S3, gap5); §11 gap5 lists "AIP" load-bearing (L569) and §12 cluster 6 cites it (L586) — gap5 "well" does NOT flip (rests on JWS/COSE/BLS/Biscuit anchors). F3 blocks consuming B-* claims sourced to AIP/Vouchsafe inherit not-peer-reviewed status — flagged for T027 | DELIVERY(c20317ce) |
| CF-F2-5 | stop rules | RECONSTRUCTED | GAP | ABSENT. 184→148 reported purely as outcome (L4, L5, L591); no inclusion/exclusion criterion, per-theme quota, saturation/closure rule, or re-scan condition in the 592 lines. Only curation-rule trace: the L7 maps_to re-mapping note — a normalization step, not a stop rule. | GAP: unrecoverable (stopping decision lived in the curating session); in-doc-summary baseline per FR-014; single merge+dedupe-pass inference recorded, not backfilled | caps confidence in coverage-*sufficiency* behind §11's "well" density claims and "thin" calls; F3 single-family B-* survivors are the exposure — deferred to T017/T018 blind re-scans | DELIVERY(c20317ce) |

Auditor notes (T008): independent per-section entry counts match the declared table EXACTLY
(12/19/19/10/20/8/14/16/15/15 = 148, zero numbering breaks). The two 2026 arXiv ids lacking the
full "not peer-reviewed" caution are `2601.02254` (Vouchsafe) and `2603.24775` (AIP) — both
tier-ok so the discount survives; AIP is load-bearing in §11 gap5 → carried into T014/T027.
Claim-schema census was mechanical (148/148 on every field); the only defect class is the 3
author-placeholder entries (L391/L394/L498). The two heaviest F2 method losses are upstream of
the document (query manifest; identity of the 36 drops) — everything downstream verified clean.

### 2.3 F3 conformance ledger (CF-F3-*)

| finding_id | element | provenance | verdict | evidence (verbatim, `F3 L<n>`) | gap disposition / deviation class | affected claims | baseline |
|---|---|---|---|---|---|---|---|
| CF-F3-1 | source manifest | RECORDED | PASS | L5: "3 blind scanners (A=F1 doc only, B=F2 corpus only, C=live-repo pinned set only) emitted 86 schema-conformant claims (A:30, B:29, C:27)". Verified against §1 (L28–L77): every Sources cell uses only A-*/B-*/C-* ids; max ids = A-30, B-29, C-27 (zero out-of-range); ALL 86 ids appear at least once — full claim consumption. Only 3 cells carry non-id parentheticals (L34, L41, L66) — counter-query annotations, not claim ids. | — | — | DELIVERY(6ecc975f) |
| CF-F3-2 | claim schema | RECONSTRUCTED | GAP | Schema existence asserted (L5 "86 schema-conformant claims", L237 "per-claim fields intact") but the field list and claim sets are absent in-repo. L81 defers to "full field detail in the three claim sets, appendix §16" — **no §16 exists** (sections run 0–8; dangling internal reference). L237: claim sets "preserved in the F3 run records (session transcripts)" — not in-repo. | GAP: per-claim record unrecoverable in-repo; §1 Sources column is the only in-doc summary (claim_id, family, design-slot re-derivable). FR-014 targeted re-execution re-derives slot↔claim clustering but not original per-claim field content. Dangling §16 corrected via change-log (row 19). | none invalidated; all 50 BB-* rows lose per-claim evidence depth | DELIVERY(6ecc975f) |
| CF-F3-3 | rubric | RECORDED | PASS | L24 bin legend; all 50 §1 rows use only these bins (+ CORE/OPT/POST tiers). Every §1 ESC marker has a matching §6 register entry (E1–E9 all present: L60/L37/L32/L31+L50/L47/L71/L67/L42/L75 ↔ L201–L209). | — | — | DELIVERY(6ecc975f) |
| CF-F3-4 | failure-mode guards | RECORDED | DEVIATION | Three of four guards demonstrably executed: feasibility veto (L236 drift list); zero-self-decision (all 9 escalations owner-RULED L199–L209; L234; L212 "E1 b" ambiguity returned to owner); same-family non-corroboration (L5, consistent with one-scanner-per-family). BUT the false-consensus guard's execution RECORD fails arithmetic: L232's 14/17/9 histogram matches the shipped §1 table under no counting convention (see CF-F3-7). | DEVIATION: **weakens-a-claim** (scoped to the false-consensus guard's recorded tally; the other three guards would PASS standalone) | BB-VER-6, BB-CRDT-6, BB-CRDT-9, BB-CRDT-11 (via CF-F3-7) | DELIVERY(6ecc975f) |
| CF-F3-5 | stop rules | RECORDED | PASS | L162 "28/28 — every row covered; none out-of-scope"; L235 "closed 28/28 on cycle 1 → stop rule met". Arithmetic: §4 items = 4 OC + 8 S + 9 gap + 7 C = 28 — checkable and checks out; every "Covered by" cell non-empty. (Row-LIST accuracy is a separate FR-005 finding — §4.4.) | — | — | DELIVERY(6ecc975f) |
| CF-F3-6 | authority order | RECORDED | PASS | Declared L5; applied L233 with each recorded resolution consistent (CBOR-vs-term-codec → repo head wins for term payloads, CBOR provisional; emit-low/accept-range vs hard-reject → layer split, no rank inversion; scanner 3-vs-6 → family constraint). Genuine conflicts escalated (L234). "Losers → alternatives_rejected" records live in transcripts (folded into CF-F3-2's gap). | — | — | DELIVERY(6ecc975f) |
| CF-F3-7 | corroboration rule | RECORDED | DEVIATION | Rule recorded L232 — but the audit found: §1 has **50** rows, not 40 (14+17+9); actual histogram **28/9/13** strict (**28/12/10** lenient) — no bucket matches under either convention. The "e.g." list names 7 of the claimed 9, and one named block (BB-SCH-3, Sources "B-04 A-9", L77) is **2-family** — not a singleton at all. Actual singletons = 13 (strict); only 5–7 have a recorded per-block disposition; 4 ACC blocks are single-family with NO L232 disposition (acceptance rests on §2 prose authority anchors: L125 D5 gate, L133 040-ruled, L136 D-B2 gates, L138 040 FR-037 shape). Mitigation: the error is conservative for corroborated blocks — actual 3-family coverage (28) is double the claimed 14. | DEVIATION: **weakens-a-claim** — the merge-log corroboration accounting (L232) is factually wrong in all three buckets and the total; "each counter-queried" unsubstantiated for 6–8 of the actual singletons. Corrected in F3 §8 via change-log; full re-adjudication of all 13 actual singletons executed by this pass (report §3). | BB-VER-6, BB-CRDT-6, BB-CRDT-9, BB-CRDT-11 (single-family ACC, no recorded disposition); secondarily the L232 merge-log claim itself | DELIVERY(6ecc975f) |
| CF-F3-8 | cycle protocol | RECONSTRUCTED | GAP | Cycle execution summarized in-doc (L235) but the protocol's definition lived in the planning triad's "8 recorded decisions" (L5) — enumerated NOWHERE in-repo; only 1 of 8 ("3 scanners, one per FAMILY") plus the L233 scanner-count note survive in summary. Per-counter-query records also transcript-only (L237). | GAP: in-doc summary (L5/L233/L235) is the only baseline; the 7 unenumerated planning decisions are unrecoverable even by FR-014 re-execution (re-running scans cannot recreate planning-time decisions). | none directly; the reconstructed cycle-2/3 semantics in §1.3 remain inference | DELIVERY(6ecc975f) |

Auditor notes (T009): the 40-vs-50 total suggests the L232 histogram was computed over an
earlier, smaller block set and never recomputed against the shipped table. Manifest evidence is
unusually strong (all 86 ids consumed, zero out-of-range, zero unused). The corroboration
deviation is directionally conservative for multi-family blocks — no corroborated block is
weaker than recorded; harm is confined to singleton accounting. The four affected single-family
ACC blocks all cite owner-ruling/standing-gate anchors in §2 prose.

### 2.4 Totals and deviation classification (T010/T011, SC-001)

Per-ledger totals (elements = PASS + GAP + DEVIATION, zero omitted, zero empty-evidence rows):

- **F1: 5 elements → 1 PASS · 3 GAP · 1 DEVIATION** (CF-F1-1..5)
- **F2: 5 elements → 1 PASS · 4 GAP · 0 DEVIATION** (CF-F2-1..5)
- **F3: 8 elements → 4 PASS · 2 GAP · 2 DEVIATION** (CF-F3-1..8)

**SC-001: met** — 3 of 3 deliverables have complete conformance ledgers; 18 of 18 method
elements examined.

Cross-ledger DEVIATION classification (FR-003):

| finding | class | affected downstream claims |
|---|---|---|
| CF-F1-2 (claim schema: "confidence+recency on every claim" not upheld) | **weakens-a-claim** | F1 §5 glpnet row + §12 S1/glpnet=S rest partly on the metadata-less Gleam-term claim (L47); F3 blocks anchored on F1 S1-interchange claims (BB-ENC-5/9 golden-parity anchors) inherit unweighted confidence |
| CF-F3-4 (false-consensus guard's recorded tally wrong) | **weakens-a-claim** | scoped to the guard's execution record; substance re-established by this pass's re-adjudication (§3) |
| CF-F3-7 (corroboration histogram wrong in every bucket; singleton list under-enumerated) | **weakens-a-claim** | BB-VER-6, BB-CRDT-6, BB-CRDT-9, BB-CRDT-11 (single-family ACC with no recorded counter-query disposition — retired by the §3 re-scans); the L232 merge-log claim itself (corrected, F3 change-log row 20) |

No **invalidates-a-claim** deviation was found: CF-F3-7's error is conservative for corroborated
blocks (actual 3-family coverage 28 vs claimed 14 — no block is weaker than recorded), and
CF-F1-2's missing metadata weakens weighting, not existence, of the affected claims. Minor
harmless deviations recorded inside GAP rows: F1 X-tier defined-but-unused (CF-F1-3), F2 gap3
hybrid verdict value and 2/4 partial recency flags (CF-F2-3/4).

## 3. Singleton re-adjudication (FR-004/FR-014, SC-002)

### 3.1 Derivation of the authoritative singleton list (T012, baseline DELIVERY(6ecc975f))

Derived mechanically from the F3 §1 Sources column (A-* = family A/F1, B-* = family B/F2,
C-* = family C/repo; full 50-block cardinality mapping in the T009 audit record, summarized in
§2.3). Counting convention: the three "(+F1 …)" parentheticals (L34, L41, L66) are counter-query
corroboration NOTES, not scanner-emitted claim ids — scanner-family cardinality is counted
STRICT (they do not make a second family), with the lenient (post-counter-query) view reported
alongside.

- **§1 table row count: 50** (ENC 9 + WIRE 5 + HDR 4 + CAP 4 + SIG 4 + VER 6 + CRDT 11 + RTE 4 + SCH 3) — not the 40 implied by §8's histogram.
- **Derived histogram (strict)**: 3-family **28** · 2-family **9** · single-family **13** (lenient: 28/12/10). Shipped §8 claim "14/17/9" (L232) matches NO bucket under either convention → CF-F3-7 DEVIATION; F3 §8 corrected via change-log.
- **Authoritative singleton list (strict, 13 blocks)** — this pass's FR-004 re-scan queue (a superset of the spec's "9"; the shipped "9" cannot be reconstructed from the shipped table, and §8's "e.g." list even includes 2-family BB-SCH-3):
  BB-ENC-7 (B), BB-ENC-8 (A), BB-WIRE-5 (C), BB-HDR-4 (C), BB-VER-5 (B), BB-VER-6 (C),
  BB-CRDT-6 (C), BB-CRDT-7 (B), BB-CRDT-8 (B), BB-CRDT-9 (C), BB-CRDT-10 (B), BB-CRDT-11 (C),
  BB-RTE-3 (C) — parenthesis = the one sourcing family; re-scans target the two OTHER families
  per block (research.md R4 blind protocol).
- The 2-family blocks (9, strict): BB-ENC-2, BB-CAP-4, BB-SIG-2, BB-SIG-3, BB-SIG-4, BB-CRDT-5,
  BB-RTE-2, BB-RTE-4, BB-SCH-3 — corroborated, no re-scan (FR-014 in-doc baseline; T016 re-derives
  their merges from in-doc data).

### 3.2 Singleton verdicts (T017/T018)

Protocol executed as ruled (research.md R4): per singleton, one blind Claude scanner agent per
non-corroborating family, given the family manifest + claim TOPIC only — never F3's verdict —
26 blind scans total (12×A, 8×B, 6×C). Curation by the pass's single writer. All records under
[evidence/](evidence/) (`f3-rescan-<block-id>.md`). Additionally, T016 re-derived the merge
decisions of all 37 multi-family blocks from in-doc data only: **37 COHERENT, 0 CONTESTED**
([evidence/f3-merge-rederivation.md](evidence/f3-merge-rederivation.md)) — no blocks joined the
re-scan queue. Baseline for all verdicts: HEAD(6ff3a8c9). Finding ids SR-042-01..13.

| finding_id | block | original family | verdict | essence | record |
|---|---|---|---|---|---|
| SR-042-01 | BB-ENC-7 | B | **confirmed** (strengthened to 3-family) | A corroborates the qmedit CBOR-TLV family + recommendation; C holds the SHIPPED 041 CBOR surface (`CborCodec.cs`, FR-002, 48-cell matrix green) | [f3-rescan-BB-ENC-7.md](evidence/f3-rescan-BB-ENC-7.md) |
| SR-042-02 | BB-ENC-8 | A | **no-further-evidence** | B: no markdown-encoding literature; C: nothing shipped — both confirm the block's own absence framing; PROV/POST stands | [f3-rescan-BB-ENC-8.md](evidence/f3-rescan-BB-ENC-8.md) |
| SR-042-03 | BB-WIRE-5 | C | **confirmed** (weak external corroboration) | A corroborates BE headers in two units; B the LE-lineage payload varint family; no contradiction — primary authority stays the shipped goldens | [f3-rescan-BB-WIRE-5.md](evidence/f3-rescan-BB-WIRE-5.md) |
| SR-042-04 | BB-HDR-4 | C | **confirmed** | A independently records the PeerId-keyed identity design; B grounds key-centric naming (SPKI/SDSI/UCAN/CONIKS); ownership rules stay 040-owner-ruled | [f3-rescan-BB-HDR-4.md](evidence/f3-rescan-BB-HDR-4.md) |
| SR-042-05 | BB-VER-5 | B | **confirmed** (as PROV; trigger unmet) | A: lens machinery cited-not-built; C: version HANDLING shipped, zero TRANSLATION machinery — PROV standing + trigger wording valid | [f3-rescan-BB-VER-5.md](evidence/f3-rescan-BB-VER-5.md) |
| SR-042-06 | BB-VER-6 | C | **confirmed** (partial corroboration) | B corroborates acyclicity-by-design (move-op cycle avoidance, grow-only DAGs); codec-boundary CycleGuard stays repo+D5-gate authority; A: no evidence | [f3-rescan-BB-VER-6.md](evidence/f3-rescan-BB-VER-6.md) |
| SR-042-07 | BB-CRDT-6 | C | **confirmed** | A (beacon `(hlc,origin)` LWW, olamnit LWW-by-mutability) + B (LWW/MV catalogue, cr-sqlite, DVV detection) corroborate; tie-break stays the recorded open item | [f3-rescan-BB-CRDT-6.md](evidence/f3-rescan-BB-CRDT-6.md) |
| SR-042-08 | BB-CRDT-7 | B | **confirmed** (strengthened; register trigger MET) | A corroborates at bibliography level; C holds SHIPPED Fugue+Peritext+RichTextDoc (SC-012/013) — promotion executed in §7 | [f3-rescan-BB-CRDT-7.md](evidence/f3-rescan-BB-CRDT-7.md) |
| SR-042-09 | BB-CRDT-8 | B | **confirmed** | A holds the blocklace/nested-attestation lineage; C the SHIPPED day-one hash chain (`Dot.cs` pred_hash, FR-025) with full Byzantine deferred — exactly the E7 split | [f3-rescan-BB-CRDT-8.md](evidence/f3-rescan-BB-CRDT-8.md) |
| SR-042-10 | BB-CRDT-9 | C | **confirmed** (strengthened) | A DIRECTLY corroborates the ground-relay law (F1 L43/L48/L18); B corroborates the value-only replication family; CorrIds half stays D-B2-gated | [f3-rescan-BB-CRDT-9.md](evidence/f3-rescan-BB-CRDT-9.md) |
| SR-042-11 | BB-CRDT-10 | B | **no-further-evidence** | A: nothing; C: no columnar/RLE/compaction (delta+Merkle is the current answer) — block legitimately rests on B-22 alone; PROV/POST stands | [f3-rescan-BB-CRDT-10.md](evidence/f3-rescan-BB-CRDT-10.md) |
| SR-042-12 | BB-CRDT-11 | C | **confirmed** (partial corroboration) | Both families corroborate the durable tamper-evident provenance substrate; the including-refusals clause stays 040-owner authority | [f3-rescan-BB-CRDT-11.md](evidence/f3-rescan-BB-CRDT-11.md) |
| SR-042-13 | BB-RTE-3 | C | **confirmed** (no-further-corroborating-evidence; owner-ruled + shipped twice) | B: nothing; A: one CONTRASTING sibling design (olamnit degrade) — recorded as design-space context, not a conflict: the law is an explicit 040 owner ruling implemented at HEAD in `routing.py` + `Addressing.cs` | [f3-rescan-BB-RTE-3.md](evidence/f3-rescan-BB-RTE-3.md) |

**SC-002: met and exceeded** — 13 of 13 derived singletons (⊇ the spec's 9) carry a recorded
verdict with evidence or an explicit no-further-evidence ruling. Verdict census: 11 confirmed ·
2 no-further-evidence · 0 demoted · 0 promoted-by-rescan (BB-CRDT-7's promotion comes from its
register trigger, §7) · 0 escalated from re-scans. No F3 §1/§2 status changes arise from the
verdicts themselves (statuses stand as corrected by §6-propagation); the F3 §8 corroboration
bullet now points here (change-log row 20).

## 4. Coverage-ledger re-derivation (FR-005, SC-004)

### 4.1 F1 §12 signal×repo matrix

**Result: DISCREPANT (2 of 64 cells)** — re-derived from the document's own body (§3–§11, §13–§15)
at DELIVERY(c20317ce); both cells corrected in `priorart-sibling-scan.md` (change-log rows 2–3).

| finding_id | cell | shipped | re-derived | direction | body evidence | correction |
|---|---|---|---|---|---|---|
| LR-042-F1-1 | S8 × qhstate | `P (eng interview)` | `·` | overclaimed | No qhstate row exists in the §4 provenance table (F1 L58–L66) and no "engineering interview" for qhstate is mentioned anywhere in the body; §4's closing enumeration (L68) omits qhstate; §15 rank 1 attributes qhstate spec-036 "S1,S3,S4,S6,S7,epic" (L272) — S8 absent. The annotation appears to have leaked from reader-stage raw findings never consolidated into §4. | F1 §12 cell → `·`; change-log row 2 |
| LR-042-F1-2 | S5 × crucible | `·` | `W` | missed-coverage | F1 L163: "**buildkit-beacon**, **crucible**, **qhstate** — **NO QUIC** (WebSocket + MCP only; qhstate transport out of scope)." — identical single-sentence evidence yields W for beacon but `·` for crucible; `·` (absent) contradicts the body's attribution of a (weak) WS/MCP transport presence. qhstate's `· (oos)` is the correctly-differentiated case. | F1 §12 cell → `W (WS)`; change-log row 3 |

8 further cells are REPRODUCED-with-note (S-vs-P / P-vs-W shades with body support for either
reading — not discrepancies): S1×bk-beacon P, S2×glpnet S, S3×glpnet P, S5×olamnit P,
S6×olamnit S, S7×crucible P, S8×olamnit P, S2×research-loose W. Auditor notes: several S cells
rest on "defined" rather than "built" (legend permits both — e.g. buildkit S1/S5/S7 "unify"/
"reuse" while §13 L219 states the reconciled wire is not yet built); F1 L163 is a single grouped
sentence carrying three matrix cells — the direct source of LR-042-F1-2.

### 4.2 F2 §11 gap-coverage table

**Result: DISCREPANT (2 of 9 rows)** — re-derived from the corpus's own 148 entries at
DELIVERY(c20317ce). All 9 shipped VERDICTS and prose rationales survive re-derivation; both
discrepancies are confined to the Load-bearing-papers column (overclaims); corrected in
`webresearch-corpus.md` (change-log rows 2–4).

| finding_id | row | shipped | re-derived | direction | evidence | correction |
|---|---|---|---|---|---|---|
| LR-042-F2-1 | gap3 load-bearing list | "Optimized OR-Set, Peritext, **Merkle-CRDTs**" (L567) | Merkle-CRDTs does not bear | overclaimed | Corpus entry §7.4 Merkle-CRDTs (L364–366) has *maps_to:* "S6, gap4" — no gap3 — and its summary (causality tracking + partial sync) says nothing about tombstones | drop Merkle-CRDTs from the gap3 row; change-log row 2 |
| LR-042-F2-2 | gap9 load-bearing list | "Amoeba sparse capabilities, CHERI, KeyKOS, EROS, **Macaroons**" (L573) | Macaroons does not bear | overclaimed | Entry §8.1 Macaroons (L404–406) has *maps_to:* "S2" only; the §8 header (L402) itself splits the cluster: the token subset grounds delegation, "the OS/hardware lineage grounds the amulet" — the corpus's own structure excludes Macaroons from the gap9 lineage | drop Macaroons from the gap9 row; change-log row 3 |

Per-gap maps_to entry counts (re-derived): gap1: 5 · gap2: 17 · gap3: 7 · gap4: 21 · gap5: 17 ·
gap6: 19 · gap7: 5 · gap8: 18 · gap9: 7 — consistent with the shipped verdict tiering
(well=gap2/5/6/8/9, moderate=gap1/4, thin=gap7, moderate→thin=gap3).

Auditor notes (T014): (1) the §2 section header (L88) glossed gap3 as "self-describing skip",
contradicting the canonical legend (gap3 = message-level semantic tombstone) — the likely origin
of 5 of 7 gap3-mapped entries being canonicalization papers; true tombstone coverage is 2
entries (OR-Set, Peritext), which *strengthens* the shipped "→thin" lean — header gloss
corrected (change-log row 4, finding LR-042-F2-3). (2) §7's header claims gap3 bearing yet no
§7 entry maps to gap3 — left uncorrected (header prose, no ledger impact; recorded here).
(3) gap1/gap6 load-bearing entries CDDL and Blocklace have adjacent-but-defensible maps_to —
not discrepancies. (4) L575's "Thin gaps" summary sentence is loosely worded but each named
row's parenthetical flags its net-new component — no verdict change warranted.

### 4.3 F3 §3 constraint matrix

**Result: DISCREPANT (4 of 4 rows; 18 discrepant cells: 17 missed-coverage + 1 overclaim)** —
re-derived mechanically from the §1 OC column at DELIVERY(6ecc975f). Finding id **LR-042-F3-1**.
No reading of the "(core)" header reproduces the shipped lists: the all-carriers reading misses
17 carriers; the CORE-tier reading is violated by OPT members in every row (BB-CRDT-11 in OC-1,
BB-VER-4 in OC-2/OC-4, BB-CRDT-7 in OC-3, OPT/POST members of "BB-CRDT-1..11" in OC-4) while
omitting CORE carriers (BB-ENC-2, BB-SCH-1/2, BB-RTE-2/3, BB-CRDT-3, BB-SIG-2, BB-WIRE-1,
BB-VER-6). The one hard §3-vs-§1 contradiction: **BB-ENC-6 sat in the OC-3 row (L159) while its
§1 OC column is `INFRA` only (L33)** — the sole overclaim.

| row | shipped (L155–160) | missing carriers (per §1 OC column) | overclaimed |
|---|---|---|---|
| OC-1 | CAP-1/2/3/4, HDR-1/4, RTE-1/3, CRDT-11 | WIRE-1 (L37), SIG-2 (L51), SCH-2 (L76) | — |
| OC-2 | SIG-1/2/3(/4), ENC-4, HDR-2, VER-3/4 | CRDT-8 (L67) | — |
| OC-3 | HDR-2, ENC-1/3/5/6, VER-1/2, WIRE-1/2, CRDT-7 | ENC-2, ENC-7, ENC-8, VER-5, CRDT-3, RTE-2, RTE-3, SCH-1, SCH-2 | **ENC-6** (OC=INFRA) |
| OC-4 | CRDT-1..11, VER-3/4, HDR-3 | ENC-2, VER-5, VER-6, SCH-1 | — |

**Correction applied**: all four rows rewritten as the complete §1 carrier sets and the header's
"(core)" qualifier replaced by "(every §1 carrier)" — no selection rule reproducing the shipped
subsets exists, so the mechanical carrier set is the only derivable content (change-log rows
2–5). INFRA-only blocks (ENC-6/9, WIRE-3/4/5, RTE-4, SCH-3) legitimately belong to no OC row;
all seven surface in §4 rows, so nothing is orphaned.

### 4.4 F3 §4 closure ledger (28/28)

**Result: the headline count REPRODUCES; the row lists are DISCREPANT (12 of 17 in-ledger
S/gap rows + all 7 C-cluster mappings; 8 overclaim cells + 32 missed-coverage cells).**
Finding id **LR-042-F3-2** (S/gap rows) and **LR-042-F3-3** (C1..C7 line). Baseline
DELIVERY(6ecc975f).

- **28/28 arithmetic**: §4 items = 4 OC + 8 S + 9 gap + 7 C = 28; every one of the 28 rows
  retains ≥1 §1-verified covering block even after removing the overclaimed cells — the claim
  "every row covered; none out-of-scope" is TRUE as an existence claim (SC-004's substance),
  while ~68% of the derivable rows had inexact lists.
- Rows exact as shipped: S5, S8, gap1, gap4, gap8.
- Overclaims (block listed without carrying the id in §1): S1×SCH-1, S7×SCH-3, gap2×ENC-2,
  gap2×ENC-9, gap5×SIG-2, gap5×SIG-3, gap7×RTE-4, C1×ENC-7. The S1/S7 pair is a likely
  SCH-1↔SCH-3 transposition (one swap explains both plus a missed cell).
- Missed coverage (32 cells) — largest: S2 (+WIRE-1, HDR-3, CAP-4, RTE-2, RTE-4),
  S4 (+HDR-2, CRDT-5, CRDT-7, RTE-2, SCH-2), C3 (+ENC-3, ENC-6, HDR-2, VER-6, RTE-2).
  Full per-row enumeration with §1 line evidence is preserved in the T015 agent record and is
  reflected 1:1 in the corrected lists.

**Correction applied**: every discrepant S/gap row and the C1..C7 line rewritten to the complete
§1-derived carrier sets (change-log rows 6–18). The final closure count remains **28/28** —
corrected from inexact lists to exact ones, no row lost coverage.

## 5. Drift dispositions (FR-006, SC-005)

All four scanner-C drift items (F3 §8 L236) dispositioned against HEAD(6ff3a8c9) — code state at
check time identical to 6ff3a8c9 (only 042 report docs committed since). Finding ids DD-042-1..4.

| finding_id | item | current-HEAD finding | disposition | action |
|---|---|---|---|---|
| DD-042-1 | mesh routes JSON-only (binary codecs unused by the router) | **Still true.** `csharp/glp_quick_host/Program.cs` L308–320 `TryRoute` still does `JsonDocument.Parse(frame)`; `glp_quick_host.csproj` references only `GlpLink` — no `glp_crdtmsg`/`glp_wire_registry`. 041 built the library-level seam only (`csharp/glp_crdtmsg/route/Mesh.cs` sends `MessageCodec.Binary.Encode`; its header comment L4–5: "the glp_quick_host QUIC/WS adapter is the drop-in replacement") — the swap has not happened. F3 L95/L236 remain accurate. | **roadmap-follow-up** | proposed follow-up **`unified-wire-mesh-migration`** (§10): migrate `glp_quick_host` `Mesh.TryRoute` onto the registry-typed binary payload header via 041's `ILinkTransport`/`MeshNode` seam, preserving router payload-opacity (E2/BB-WIRE-1 mandate). No corpus edit. |
| DD-042-2 | payloadType constants duplicated | **Fixed by 041** (`v2026.07.04.4`): `csharp/glp_wire_registry/WireRegistry.cs` L44–54 defines `PayloadType.IlProgram=0x10`, `ResultEnvelope=0x11`, `CrdtMessage=0x12` with uniqueness validation (SC-010); both former duplicators const-alias it (`ResultEnvelopeCodec.cs` L29, `PayloadHeader.cs` L20). F3 L96 + L148 stale. | **corrected-in-corpus** | F3 BB-WIRE-2 (L96), BB-SCH-2 (L148), §8 drift item annotated — change-log rows 21–23 |
| DD-042-3 | spec-vs-plan store naming (PGlite-DuckLake vs file-WAL) | **Still true (textual divergence persists; no ruling record).** `specs/040-rcopy-file-transfer-service/spec.md` L677/L705 still name "PGlite-backed DuckLake"; what shipped is file-WAL (`glp_quick/src/glp_quick/rcopy/wal.py`; 040 plan.md L44–45). The 040 plan flag (L49–54: "so analyze/owner can confirm rather than silently accept") has NO confirmation record in tasks.md, the analyze trail, or the bk-close retrospective (bd14d774). | **roadmap-follow-up** | proposed follow-up **`040-store-naming-closure`** (§10): record the owner ruling the 040 plan flag requested — confirm file-WAL as the ruled store (amending the 040 spec Assumption + roadmap capture text) or schedule the deferred queryable projection store. No corpus edit — L236 accurately records the drift as owner-flagged. |
| DD-042-4 | 037 @name promise shipped unimplemented once (origin of BB-RTE-3) | **Fixed in code, and the corpus already frames it as historical.** @name loud-fail implemented twice at HEAD: `glp_quick/src/glp_quick/terminal/routing.py` (`resolve()` returns ok=False + report on unknown name, FR-040; commit 3620a230) and 041 `csharp/glp_crdtmsg/route/Addressing.cs` L33/L41 (`unknown @name` → `CrdtMsgException`, SC-007). F3 L236 says "shipped unimplemented **once**" (past tense) and L143 "born from the 037 silent-fallback defect" — neither implies it is still unimplemented. | **obsolete** | none (historical statement, accurate as written) |

SC-005: 4 of 4 items dispositioned, zero dangling.

## 6. Ruling propagation (FR-007, SC-006)

Sweep of E1–E9 (F3 §6) across every appearance of each touched block in all three docs, at
HEAD(6ff3a8c9). Classification: explicit `En` markers point into the ruled register (whose
heading states "RULED by Gabi 2026-07-04") → consistent-by-reference per the F3 §6 supersession
note; a location asserting a stale status with no marker → INCONSISTENT, corrected.

**Inconsistencies found: 14. Corrected: 14. Remaining: 0** (SC-006). Finding ids RP-042-01..15
(RP-042-15 = the supersession-note extension; RP-042-03/06 land via the §5 register adjudication
below, §7):

| finding_id | ruling | location | stale content | correction |
|---|---|---|---|---|
| RP-042-01 | E3 | F3 §1 BB-ENC-7 row | Bin=PROV, MVP=OPT, no E3 marker | ACC (ruled E3) / CORE (F3 change-log row 24) |
| RP-042-02 | E3 | F3 §2 BB-ENC-7 | "(PROV)… Adopt when a generic-payload need materializes" | ACCEPTED — E3-ruled, MVP-CORE; adopted as fourth MVP surface (row 25) |
| RP-042-03 | E3 | F3 §5 BB-ENC-7 register row | "promotes when a non-term generic payload consumer appears" — overtaken, nothing signals promotion | resolved in the §7 register adjudication (T022 edits; see report §7) |
| RP-042-04 | E3 | F3 §7 item 2 / L226 | ENC-7 absent from the MVP cut; terminal line swept it into "provisional per §5" | item 2 += ENC-7 + E3 surface set (row 30); terminal line scoped (row 33) |
| RP-042-05 | E4 | F3 §2 BB-HDR-4 | "per-peer credentials are future work" with no E4 marker | E4 per-peer Ed25519 enrolment propagated; transport certs remain future (row 26) |
| RP-042-06 | E4 | F3 §5 BB-SIG-4 register row | "promotes when E4 base format ruled…" — first condition met 2026-07-04, unannotated | resolved in the §7 register adjudication (T022 edits; see report §7) |
| RP-042-07 | E7 | F3 §2 BB-CRDT-4 | no mention of hash-chained op ids | E7 day-one hash-chaining appended (row 28) |
| RP-042-08 | E7/E1 | F3 §2 BB-HDR-3 | op-id identity/seam unstated post-ruling | E7/E1 op-id ruling appended (row 27) |
| RP-042-09 | E9 | F3 §2 BB-SCH-2 | no mention of dual representations | E9 dual-representation hosting appended (row 29) |
| RP-042-10 | E6 | F3 §7 item 9 | experimental GLP guard deliverable missing from the cut | guard added, §1.14 propose-first noted (row 32) |
| RP-042-11 | E1 | F3 §7 item 7 / cut | store-first ruling invisible; CRDT-1/2 store skeleton absent | store-first skeleton + ruled substrates/seam added (row 31) |
| RP-042-12 | E9 | F3 §7 item 10 + terminal line | SCH-1 core + dual-DSL absent; terminal line over-broad (affirmatively false for ENC-7/CRDT-1/2/SCH-1 post-ruling) | item 10 rewritten; terminal line scoped to "marked PROV in §1" (row 33) |
| RP-042-13 | E1 | F1 §10 "Fit" + §14 item 4 | recommends roadmap_crdt as THE durable-store engine — E1 demoted it | supersession notes appended (F1 change-log rows 4–5) |
| RP-042-14 | E5 | F2 §11 gap9 | "the concrete 16 B Amoeba amulet impl is the net-new build" — E5 rejected literal 16 B | E5 note appended (F2 change-log row 5) |

Sweep notes (T020): ~10 F3 §2 entries survive on their explicit En markers
(consistent-by-reference); the F3 §6 curator note was extended to state that the supersession
covers En-marked §2/§5/§7 passages (RP-042-15, row 34), closing the L212-scope gap. F3 §7's
header was updated post-ruling but its list body had not been re-cut — the four §7 corrections
are exactly the missed ruling deltas. Remaining §5 rows (ENC-8, VER-5, CRDT-7, CRDT-10, RTE-4,
SCH-3) were checked against all nine rulings and are NOT overtaken by any ruling; the
041-shipped-rich-text interaction with BB-CRDT-7 is a register matter (report §7), not a
propagation defect.

## 7. PROVISIONAL register closure (FR-008/FR-009, SC-003)

*(pending T021–T023)*

### 7a. Promotions for owner review

*(pending T023)*

### 7b. Escalations

*(pending T023)*

## 8. Evidence-pointer census (FR-010, SC-007)

*(pending T025–T028; full census: [evidence/evidence-index.md](evidence/evidence-index.md))*

## 9. Owner escalations (FR-013)

*(pending T029)*

## 10. Proposed roadmap follow-ups (FR-009)

*(pending T024/T029)*

## 11. Amendment index (FR-011, SC-008)

*(pending T029. Note: the three `SETUP-042-*` change-log rows logging each doc's change-log-section
addition (amendment-changelog contract rule 4) reference this report's baselines/setup header rather
than a §1–§8 finding — a documented exception: no §1–§8 finding existed yet at setup time.)*

## 12. Success-criteria checklist

*(pending T029/T031: SC-001..SC-009 with measured values)*
