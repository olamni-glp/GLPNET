# P8 — Final Completeness Pass (GLP → Gleam/AtomVM Baseline Program)

**Feature:** 036-glp-gleam-baseline-program · **Marathon run:** mrun-5611c436ba95 · **Task:** T013
**Date:** 2026-06-29 · **Read-only (FR-010):** judges RECONFIGURATION.md (T007) + the P1b/P4/P6/P7/P2/P3 feeders against on-disk artifacts; mutates nothing on the live roadmap/specs/code. All cites verified `file:line` this pass.

**Cite shorthands** (under `docs/research/glp-gleam-baseline/`): **REC**=`pipelines/P8-synthesis/RECONFIGURATION.md`; **DISP**=`pipelines/P1b-realignment/DISPOSITIONS.md`; **PB**=`pipelines/P4-faithfulness/PARITY-BAR.md`; **PI**=`pipelines/P4-faithfulness/PROOFS/INDEX.md`; **P2**=`pipelines/P2-concerns/REGISTER.md`; **P3**=`pipelines/P3-opportunities/REGISTER.md`; **P6**=`pipelines/P6-gleam-impl/DOSSIER.md`; **P7**=`pipelines/P7-qhsm-yngenios/DOSSIER.md`; **CI**=`CORPUS-INDEX.md`.

---

## 1. Success-criteria self-assessment summary

| SC | Verdict | Basis (verified this pass) |
|----|---------|----------------------------|
| **SC-001** disposition + target-epic for every not-completed feature | **MET (in-scope)** | DISP:17-40 = 24 rows; 23 in-mandate features {gleam-atomvm, repl-engine-separation, marathon} each classified + non-blank target_epic; 6 OTHER-epic features correctly out-of-mandate (REC:121). Caveat: denominator rests on hand-caught backward drift (P2 C-209). |
| **SC-002a** valid topological order | **MET** (1 internal-wording defect) | 15-node order REC:175 satisfies every in-set `blocked_by` (DISP:17-31). BUT REC:55 "topo backbone 034→#26→#27" sequences #26 before its blocker #27 — see G-09. |
| **SC-002b** 100% scored | **MET** | All 15 carry FV·Enab·RiskRed·Effort + composite + num (REC:32-47). |
| **SC-002c** 100% tied to ≥1 P4 criterion | **PARTIAL** | 11/15 = 73% literal; 4 (#17,#10,#13,#21) carry NONE, declared enabling/folded/deployment (REC:179; DISP:25,29,30,31). Classification verified sound — literal "100%" needs an owner-ratified exception. See G-03. |
| **SC-003** every M1/M2 criterion cites a primary source | **MET** | All 45 M1 + 21 M2 rows carry page/`file:line`; recovered-11 M2 re-grounded to live `multiagent/*.dart` (PB:107,153-154). |
| **SC-004** load-bearing invariants recorded; none silently skipped | **MET (recording req.)** | PI:21-27 — proved 3, open 2, refuted 0; explicit "no load-bearing invariant silently skipped" (PI:27). Caveat: the 2 most load-bearing are OPEN (writer-MGU PI:14, dist-deref PI:17) and gate the LOCKs; the 3 "proved" prove method-on-toy (PI:13,15,16 scope notes). |
| **SC-005** ≥1 ANTLR option built/run + IL-vs-direct cites a spike | **MET** | Option A built+run end-to-end (`spike/p5-il-merge` byte-identical + execution-equivalent); IL + direct codegen both exercised in the one spike. |
| **SC-006** Gleam-impl + QHSM dossiers cite located sources, no fabrication | **MET** | P6/P7 every material claim cited; absent sources marked PROVISIONAL not fabricated (P7 §4). Caveat: `MSTACK/docs/diana` genuinely absent (P7:118) and SC-006/US4 named it an acceptance source — not addressed in REC self-assessment. See G-13. |
| **SC-007** zero mutations before approval | **MET (as evidenced)** | Whole program read-only; migration mapping is advisory at T015 (REC:6,87). Latent risk: superseded P1 "Step 0 roadmap hygiene" (P2 C-169) must stay un-executed. |
| **SC-008** resume-from-checkpoint works | **PARTIAL (UNMET-leaning)** | No recorded induced-interruption resume; trackers stale vs disk (INDEX rows PENDING / tasks.md `[ ]` while artifacts exist + untracked tree → resume re-runs completed pipelines, P2 C-208). See G-12. |
| **SC-009** owner decides from synthesis alone | **PARTIAL (lean-MET)** | REC is decision-shaped (exec summary, scored table, composite-inversion trap, D1-D15, migration map, SC self-assessment). Single high-load gate + digest-carried D1-D3/D13 + un-surfaced gaps below. See §3 verdict. |
| **US5 / FR-002** loop-until-dry | **SPLIT** | P3 **dry** ("Saturation reached? YES", P3:151). P2 **NOT dry** ("Saturation: NOT reached", P2:301) — 5 zero-coverage classes surfaced (C-212..C-216); no consecutive empty rounds recorded. See G-04. |

**Roll-up:** MET = SC-001, 002a, 002b, 003, 004, 005, 006, 007 (several with load-bearing caveats). PARTIAL = SC-002c, SC-008, SC-009, and P2 loop-until-dry. No SC is cleanly UNMET; SC-002c / SC-008 / P2-exhaustiveness are the three that fail a literal reading.

---

## 2. FINAL GAP TABLE

| id | gap | severity | disposition | action / rationale | cite |
|----|-----|----------|-------------|--------------------|------|
| **G-01** | CORPUS-INDEX §G/§H transposes the two siblings: labels `qhstate-Yngenios` a "stub, coordination notes only" when it is a ~3820-file QHSM worktree of `qhstate` with the AOK C++23 port + spec-034 pipeline; the "thin" label belongs to `mstack-coop` (only `COOP/`). | MED (doc defect; corrected downstream in P7; no M1/M2 decision rests on it) | **FOLD-NOW** | Correct CI §G rows 95-96 + §H bullet 106-108 on disk (exact text in §3 below). Until fixed, any pipeline trusting §G/§H inherits the falsehood (P7:120 warns "must be corrected before any P7 design cites them by §G/§H"). | CI:95,96,106-108 vs P7:118,120,153 |
| **G-02** | F5 activation dedup key left as bare `goal_id` at source (PB:89 / DISP:17), which taken literally BREAKS FB-M1-38; correct key is `(goal_id, suspension_generation)`. | MED | **ACCEPT-WITH-RATIONALE** | REC (40,90,163) + P6 (47,143) are authoritative and consistent on the generation-scoped key; the owner reads REC, not the raw bar. A one-line "superseded by `(goal_id, suspension_generation)`" note on PB:89 / DISP:17 would harden it but is not blocking. | PB:89; DISP:17; REC:40,90,163; P6:47 |
| **G-03** | SC-002c literal "100% tie ≥1 P4 criterion" is 11/15 (73%); #17/#10/#13/#21 carry NONE. | MED | **OWNER-DECISION** | Classification verified sound (all 4 legitimately non-parity: #17 enabling/ED-4 head-of-pipeline DISP:25; #10 captured-output excluded PB:9/DISP:29; #13 deployment binding DISP:30; #21 folded→#36 DISP:31). Owner ratifies an exception amending SC-002 to exempt declared enabling/folded/deployment features, OR attaches a real tie (e.g. #17→GAP-G1/G8 corpus-admission). | REC:179; DISP:25,29,30,31; PB:9 |
| **G-04a** | P2 register self-certifies **"Saturation: NOT reached"**; REC folds "top P2" without surfacing the non-saturation. | MED | **FOLD-NOW** | Add a one-line flag to REC Key-risks: "P2 feeder is self-certified NON-saturated (P2:301); 5 zero-coverage classes surfaced on the first breadth scan." | P2:301; REC:163-167 |
| **G-04b** | 6 of the 7 critic MISS classes absent from REC owner-decisions/risks. Only **security = D12** (REC:151) is folded. Missing: MISS-02 performance bar, MISS-03 AtomVM pre-1.0 supply-chain, **MISS-04 cross-runtime differential-test harness**, MISS-05 observability, MISS-07 migration rollback; MISS-06 owner-gate throughput is structurally split (D1-D15) but the concern is never named. | MED-HIGH (MISS-04 most urgent — it undercuts the SC-002a topo-PASS claim; #5/#8 assume a harness that is never scoped/sequenced) | **OWNER-DECISION** (surfaced via FOLD-NOW) | Add owner-decision/risk rows (or explicit deferrals) for MISS-02..05,07 before the T014 gate; most urgently scope MISS-04 as a build obligation and sequence it under #8/#5. Then run ≥1 more P2 round on these classes until consecutive empty rounds are recorded (US5 close). | P2:269-275,301; REC:151 |
| **G-05** | **GAP-G7 (suspension-set MINIMALITY)** escalated to an explicit owner gate by P7:138, framed as a genuine fork by PB:171 (observable parity requirement vs implementation latitude), yet absent from every REC gate and the D-list (grep: G7/"minimality" never appears in REC). | MED | **FOLD-NOW** | Add G7 to REC as an owner gate/D-decision (it changes the observable suspended-set / `blockingReaders`). Note the P4-vs-P7 severity conflict: P4 rates "(M1, minor)" PB:171, P7 overrode to owner-gate P7:138 — owner resolves. | P7:138; PB:171; REC (absent) |
| **G-06** | **GAP-G4 (computed answer = logical consequence; soundness)** carried in no REC gate, D-decision, or migration note, and not registered as `open` in PI. | MINOR (proof-phase, not a directional fork) | **FOLD-NOW** | Add G4 as a recorded P4 faithfulness-proof obligation (one line in REC + an `open` row in PI alongside writer-MGU/dist-deref), so a load-bearing soundness invariant is not silently dropped. | PB:168; PI:11-17 (absent) |
| **G-07** | ED-6 **float-decode on AtomVM** is a hard precondition on the M2 byte-parity codec ("must spike before committing the Gleam codec, else the M2 byte-parity decision is invalidated") but appears only as Key-risk prose (REC:163 risk 2), not in the "Mandatory sequencing gates (override any score)" block (REC:57-63). | MINOR-MED | **FOLD-NOW** | Promote to a 6th mandatory sequencing gate gating #4-forward/#15/#36 codec — equal in force to gate 5 (Section-15 codec). | REC:163; REC:57-63; P6:139; P2 C-202 (P2:252) |
| **G-08** | FORK-1 (D5) presented two-way (loud-all vs structural-vs-cycle); the discriminator is actually three-way — self-bind→Unbound (FB-M1-22) is a third arm the owner must also pin. | MINOR | **ACCEPT-WITH-RATIONALE** | Option B's "structural/cross-goal" language partially covers it and the self-bind→Unbound recognizer is code-grounded (PI:14, `heap_fcp.dart:312-323`). Recommend making the third arm explicit in D5; not blocking. | REC:137; PB:181-183; PI:14 |
| **G-09** | REC:55 "ratified topo backbone: **034 → #26 → #27 → #6 → #8**" sequences #26 ahead of its blocker #27, contradicting the valid sort REC:175 (#27 before #26) and DISP:17 (#26 `blocked_by #27`). Written in num-priority order (#26 num=15 > #27 num=11) but mislabeled "topo backbone". | MED (load-bearing ordering statement; could mislead owner sequencing) | **FOLD-NOW** | Relabel REC:55 as the *priority* backbone, or reorder to `034 → #27 → #26 → #6 → #8` to match REC:175 / DISP:17. The valid topo sort (REC:175) is correct. | REC:55 vs REC:175; DISP:17 |
| **G-10** | P1b disposition vocabulary is out-of-enum vs `data-model.md` (ADD-NEW/SHIPPED/CLOSED/FOLD-into-#6 classifications; M1/M2 milestones; crit "TRUE (seam)"); P8 consumes vocabulary the data-model does not define (FR-008). | MED | **ACCEPT-WITH-RATIONALE** | Extensions are semantically clear and necessary to express the realignment; the owner ratifies the extended vocabulary (or data-model is updated post-gate). Disclosed via P2 C-210. | P2 C-210 (P2:260); DISP:23,26,28-40 |
| **G-11** | Synthesis invents net-new scope (feature M2-0) inside a read-only dispositioning program scoped to the existing not-completed set. | MED | **ACCEPT-WITH-RATIONALE** | Disclosed honestly + owner-gated: M2-0 marked PROPOSED-NEW/ADD-NEW and routed through fork D10 (REC:43,147); becomes a feature only on owner approval. | REC:43,147; DISP:23; P2 C-211 (P2:261) |
| **G-12** | SC-008 resume-safety: INDEX status rows / tasks.md checkboxes / marathon_* rows are stale vs disk (artifacts exist + post-date the tracker; tree untracked) → a tracker-driven resume re-runs completed pipelines. | HIGH (program-integrity, not a REC-content gap) | **FOLD-NOW** (harness action) | Reconcile the durable checkpoint to disk (update INDEX + tasks.md + marathon_* rows to mark P1b/P4/ANTLR/P6/P7/P2/P3/P8 complete) and record an actual induced-interruption resume showing no duplicated output. Orchestrator/harness action, outside RECONFIGURATION. | P2 C-208 (P2:258) |
| **G-13** | `diana` named as an acceptance source by SC-006/US4/FR-007 (CI:101-105) is genuinely absent (P7:118); REC's SC self-assessment covers only SC-001/002a-c and never rules diana in/out. | MED | **OWNER-DECISION** | Owner rules diana in-scope (supply the docs) or out-of-scope; acceptable-for-design as-is (P7 routes around it, YngeniOS product-altitude stack marked PROVISIONAL, not used by either packaging design P7:118). | CI:101-105; P7:118; P2 C-178 (P2:228) |

**Verified-sound, NO change required** (recorded so the gate is honest about what is closed): SC-002c classification of the 4 infra features (both critics ratified); D1-D3/D13 digest-carry faithfully matches the ANTLR/P7 dossiers (re-opened + verified); the 3 "proved" obligations are honestly scoped method-on-toy; the 2 OPEN proofs (writer-MGU, dist-deref) are correctly carried as mandatory LOCK gates overriding any score (REC:59-60); GAP-G3 fairness is owner'd to #8 (not ownerless); GAP-G1/G2/G5/G6 + STRONG corpus gaps all gate the correct features.

---

## 3. FOLD-NOW edits (for the orchestrator)

### 3.1 CORPUS-INDEX.md §G (rows 95-96) — corrected siblings

Replace the `qhstate-Yngenios` and `mstack-coop` rows with:

```
| qhstate-Yngenios | D:\bstdev\research\qhstate-Yngenios\ | `src/`, `Csharp/`, `specs/` (incl. `034-*` full pipeline, `023-aok-os-synthesis`), `synthesis-os/`, `zephyr/`, `ports/aok/`, `vendor/rtos-kernels-cxx23/aok/`, `codeconv/`, `workflows/`, `tools/`, `tests/`, `docs/`, `examples/` — a ~3820-file QHSM tree; `.git` is a **worktree of `qhstate`**. (CORRECTED 2026-06-29 per P7 DOSSIER.md:120 — was mislabeled "stub".) | P7 (QHSM/YngeniOS; AOK C++23 port + spec-034 pipeline). |
| mstack-coop | D:\bstdev\research\mstack-coop\ | **thin — coordination notes only**; only structured dir is `COOP/`; **no `docs/` dir** (`COOP/README.md`, `architecture-evidence-captured.md`, `task-diana-research.md`, `note-phaseb-gabi-named-components.md`). (CORRECTED 2026-06-29 — the prior `src/…/synthesis-os/` listing here actually described qhstate-Yngenios.) | P7 (YngeniOS microkernel context; NATO-DIANA tender notes). |
```

### 3.2 CORPUS-INDEX.md §H (the two qhstate-Yngenios bullets, lines 106-108) — corrected

Replace the bullet beginning "⚠️ **`qhstate-Yngenios` is a stub** …" with:

```
- ✅ **`qhstate-Yngenios` is NOT a stub** (CORRECTED 2026-06-29 per P7 direct `ls`, DOSSIER.md:120). It is a ~3820-file QHSM tree whose `.git` is a worktree of `qhstate`; `specs/034-*` carries the full pipeline artifact set, and `vendor/rtos-kernels-cxx23/aok/` + `ports/aok/` hold the real AOK C++23 port (spec-023, `Status: Draft`). P7's YngeniOS grounding cites it firsthand. The "coordination-notes-only / thin" label belongs to **`mstack-coop`** (only structured dir `COOP/`, no `docs/`) — the original index transposed the two siblings.
```

(The `MSTACK/docs/diana` ABSENT bullet at CI:101-105 is correct as-is — diana is genuinely absent; leave it.)

### 3.3 RECONFIGURATION.md — see §4.

---

## 4. Does RECONFIGURATION.md need revision?

**Not complete-as-is — it needs a small, bounded patch set before the T014 gate.** The two-epic core, the scored+ordered table, the composite-inversion remedy, the 15 owner-forks, the migration mapping, and the 2 OPEN-proof LOCK gates are all sound and need no rework. The following precise edits close the surfacing gaps so the owner gate is over the complete risk set:

1. **REC:55 (G-09)** — relabel the line as the *priority/numerator* backbone (it is not a topo order), or reorder to `034 → #27 → #26 → #6 → #8` so it matches the valid sort at REC:175 and `#26 blocked_by #27` (DISP:17).
2. **REC:57-63 (G-07)** — add a 6th mandatory sequencing gate: **ED-6 float-decode on AtomVM UNVERIFIED → must spike before committing the Gleam codec; gates #4-forward/#15/#36** (P6:139; P2 C-202). Currently only prose at REC:163.
3. **REC:57-63 / D-list (G-05)** — add **GAP-G7 suspension-set minimality** as an owner gate/decision (P7:138 escalated it; PB:171 frames the fork), with the P4-vs-P7 severity conflict noted.
4. **REC:163-167 Key-risks (G-04a + G-04b)** — surface that the P2 feeder is **self-certified NON-saturated** (P2:301), and add owner-decision/risk rows (or explicit deferrals) for **MISS-02 performance bar, MISS-03 AtomVM pre-1.0 supply-chain, MISS-04 cross-runtime differential-test harness, MISS-05 observability, MISS-07 migration rollback** (P2:270-275). Scope **MISS-04** as a build obligation under #8/#5 — it is presently assumed but never sequenced, which undercuts the SC-002a topo-PASS claim.
5. **REC self-assessment (G-13)** — add an SC-006 line ruling on `diana` (named acceptance source, genuinely absent P7:118): owner supplies or rules out of scope.
6. **(Optional) REC:34-47 scoring table** — add a one-line "method-on-toy scope" caveat to the #26/#27 ties to PI:13/15/16, and make the FORK-1/D5 third arm (self-bind→Unbound, G-08) explicit. Hardening, not blocking.

Items G-02/G-10/G-11 are ACCEPT-WITH-RATIONALE (already disclosed/authoritative in REC); G-12 is a harness/tracker action outside RECONFIGURATION.

---

## 5. Owner-readiness verdict (SC-009)

**QUALIFIED-YES — after the §4 patch set; NO on a strict literal reading as-is.**

The owner **can** approve or amend the two-epic reconfiguration and the D1-D15 forks from RECONFIGURATION.md alone: the load-bearing decision content is present and grounded — the M1 LOCK (#8) and M2 LOCK (#5) placements, the 2 OPEN proofs carried as score-overriding gates (REC:59-60), the composite-inversion trap with the `num` remedy (REC:49-55), the consolidated forks with options+consequence+advisory-rec (REC:125-157), and the advisory (non-executed, reversible-by-non-execution) migration mapping. Approve/amend does not require re-deriving from source.

It is **not clean**, for four reasons the owner must see first: (a) RECONFIGURATION silently omits 6 of 7 critic MISS risk classes and never states that its P2 feeder is self-certified non-saturated (G-04a/b) — most consequentially the differential-test harness (MISS-04) that #5/#8 assume; (b) one escalated owner gate (GAP-G7 minimality, G-05) and one soundness obligation (GAP-G4, G-06) were dropped between P7/P4 and P8; (c) the ED-6 float-decode hard-precondition sits as prose, not a gate (G-07); (d) the internal "topo backbone" contradiction at REC:55 (G-09) could misdirect sequencing. D1-D3/D13 are also carried via digest (REC:127), so full grounding for those four forks lives in the ANTLR/P7 dossiers — acceptable (verified faithful this pass) but means the synthesis is not wholly self-contained for them.

**Bottom line:** fold in §3 (CORPUS-INDEX) + §4 items 1-5 (RECONFIGURATION) — a bounded edit set, no re-derivation — and the T014 owner gate is then over the complete, self-contained risk set. Without them, an owner approving "from synthesis alone" decides against a synthesis that omits surfaced, load-bearing material.
