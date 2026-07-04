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

*(pending T007)*

### 2.2 F2 conformance ledger (CF-F2-*)

*(pending T008)*

### 2.3 F3 conformance ledger (CF-F3-*)

*(pending T009)*

## 3. Singleton re-adjudication (FR-004/FR-014, SC-002)

*(pending T012 derivation + T017/T018 re-scans and verdicts)*

## 4. Coverage-ledger re-derivation (FR-005, SC-004)

### 4.1 F1 §12 signal×repo matrix

*(pending T013)*

### 4.2 F2 §11 gap-coverage table

*(pending T014)*

### 4.3 F3 §3 constraint matrix

*(pending T015)*

### 4.4 F3 §4 closure ledger (28/28)

*(pending T015)*

## 5. Drift dispositions (FR-006, SC-005)

*(pending T019)*

## 6. Ruling propagation (FR-007, SC-006)

*(pending T020)*

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
