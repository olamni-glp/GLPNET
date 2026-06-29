# P8 — Two-Epic Reconfiguration (GLP → Gleam/AtomVM Baseline Program)

**Feature:** 036-glp-gleam-baseline-program · **Marathon run:** mrun-5611c436ba95 · **Task:** T007
**Date:** 2026-06-29 · **Honors:** `contracts/pipeline-contract.md` (no fastest-path rubric; faithfulness-weighted axes only; every claim cited file:line).

> 🔴 **OWNER-GATED — ADVISORY ONLY.** This document is the *proposed* reconfiguration. It mutates **nothing** on the live roadmap, specs, or code (FR-010 read-only). It becomes actionable **only after owner approval at T014** (FR-011); the mapping in *ADVISORY MIGRATION MAPPING* is what `buildkit-roadmap` would apply at **T015 on approval**, not before. All forks below are presented as **OWNER OPTIONS**, not self-decided. ED-1…ED-6 are treated as FIXED.

**Cite shorthands** (all under `docs/research/glp-gleam-baseline/pipelines/`): **DISP**=`P1b-realignment/DISPOSITIONS.md`; **PB**=`P4-faithfulness/PARITY-BAR.md`; **PI**=`P4-faithfulness/PROOFS/INDEX.md`; **REG/P2**=`P2-concerns/REGISTER.md`; **P3**=`P3-opportunities/REGISTER.md`; **DEC**=`P5-il-machine-language/DECISIONS.md`; **DOS**=`P5-il-machine-language/DOSSIER.md`; **P6**=`P6-gleam-impl/DOSSIER.md`; **P7**=`P7-qhsm-yngenios/DOSSIER.md`; **ANTLR**=`ANTLR-integration/DOSSIER.md`; **ED**=`specs/036-glp-gleam-baseline-program/spec.md:256-276`.

---

## Executive summary

The three open epics — engine/REPL separation, marathon, and Gleam-AtomVM — collapse into **two output epics gated on owner approval**: **Full Gleam implementation** (15 features, the parity core) and **Optional features** (7 beyond-parity / BEAM-superseded mechanisms). The verified seam architecture (ED-1…ED-6: `ANTLR → AST → 4-primitive front-end IL → frozen v2.16.3 bytecode → engine`; **bytecode-on-wire = the front/back seam**) means the engine-separation cluster the failed P1 pre-dropped is **reinstated**, not killed (DISP:50-53): #23/#17/#4/#15 realign onto the seam, #11/#10 fold into the Gleam REPL (#6), #13's C# OS-process-spawn *mechanism* is superseded by BEAM while its seam *contract* is kept (DISP:30,53). M1 = single-instance parity (LOCK at #8); M2 = linked parity vs Dart/C# (LOCK at #5). Two load-bearing proofs remain **OPEN** and gate the LOCKs regardless of any score — **writer-MGU** (PI:14) before any runtime is M1-faithful, **distributed-deref + GAP-G6 oracle** (PI:17; PB:147,170) before M2 is testable. Six genuine forks plus language-authority gates are surfaced for the owner; nothing on the live roadmap moves until T014.

---

## EPIC 1 — *Full Gleam implementation* (scored + topologically ordered)

### Scoring rubric (explicit, owner-adjustable; NOT fastest-path)

Each sub-score **1–5**. **composite = (FV + Enab + RiskRed) ÷ Effort** (as mandated). A second view, **num = FV + Enab + RiskRed** (effort-blind numerator), is the **recommended priority signal** — see the load-bearing caveat below.

- **FV (FaithfulnessValue)** — breadth/load of tied P4 criteria + `crit` flag. 5 = crit=TRUE & ties a broad/LOCK parity set or a load-bearing proof; 1 = NONE (excluded from / not in the parity set).
- **Enab (Enablement)** — centrality of `enables[]`. 5 = unblocks the M1/M2 spine; 1 = folds in / terminal-only.
- **RiskRed (RiskReduction)** — count/severity of retired P2 concerns. 5 = retires a whole risk cluster; 1 = architecture-only/none.
- **Effort** — realignment **size signal**, NOT raw roadmap RICE (roadmap RICE is *inversely* informative; the high-RICE engine-sep rows are fold/supersede that dissolve — REG C-150/C-156/C-165). 1 = trivial fold/small spike; 5 = marathon.

Table is in **valid topological order** (seq 1→15; every feature follows its `blocked_by`). M1 LOCK falls at seq 10 (#8); M2 LOCK at seq 15 (#5).

| seq | id | name | classification | composite | FV·Enab·RiskRed·Effort (num) | blocked_by | enables | tied P4 criterion(s) | re-scope note |
|----|----|------|----------------|-----------|------------------------------|-----------|---------|----------------------|---------------|
| 1 | #4 | il-codec-spike (fwd obligation) | SHIPPED/CLOSED(029)+fwd-oblig REALIGN→full-gleam | **6.50** | 4·4·5·2 (13) | shipped 029 (no in-set blocker) | #23, #15, #17 Phase-B | FB-M1-06 (PB:60) | live `specified`→close (drift); **MUST NOT cite 029 IlCodec as proof for the Dart/Gleam path** (DISP:26; DEC:44-46) |
| 2 | #15 | result-codec-and-framecodec-ride | REALIGN + SPLIT (M1/M2) | **4.50** | 3·3·3·2 (9) | #4 | #23, #36(frame) | FB-M1-17(PB:71); FB-M1-41/42(PB:95-96); FB-M2-06(PB:123) | (a) envelope-codec→full-gleam, (b) FrameCodec frame→#36, (c) C# TcpTransport→BEAM `gen_tcp`; correct cite `FrameCodec.cs:64 OffKind`=fragmentation→payload-type prefix byte (DISP:27) |
| 3 | #23 | compiled-il-on-the-wire + factor-out-compiler | REALIGN (reinstate) | **4.00** | 2·3·3·2 (8) | #4, #15 | #17 Phase-B; bytecode-on-wire seam | FB-M1-06 (PB:60) | re-title (IL never crosses; **bytecode** does); `VariableMap` crosses wire; effort < "large" (`GlpCompiler` standalone); REFUTES P1CA:108 (DISP:24) |
| 4 | #17 | antlr4-shared-grammar-spike | REALIGN(reinstate)+FOLD(Ph-A/B)+DROP C++ | **3.00** | 1·3·5·3 (9) | Phase-A:027; Phase-B:#4,#23 | #27 (canonical `.g4`); corpus surface | **NONE direct** — enabling/arch-fit (ED-4 fixed input); admits GAP-G1/G8 corpus (PB:165,172), declared honestly | Phase-A grammar-as-verifier (parse 100% `programs/`) → Phase-B production parser+IL-identity; **DROP C++ target** (DISP:25) |
| 5 | #27 | glp-gleam-compiler-and-loader | ALIGNED | **2.20** | 4·3·4·5 (11) | #17 | #26; unblocks GAP-G1/G2 | FB-M1-06(PB:60); **SRSW-preservation proved** PI:13 | hand-ported RD parser conforming to #17's canonical `.g4` (no BEAM ANTLR target); spawn via raw `erlang:spawn`+Subjects (DISP:18) |
| 6 | #11 | result-envelope-and-deep-resolve | FOLD-into-#6 (component) | **3.50** | 3·2·2·2 (7) | folds→#6 | #6 (component) | FB-M1-17/19/20/21 deref(depth-32); FB-M1-41/42; var→writer=FB-M1-14 | realign-lite — add parallel `ResolvedBindings` (`Bindings` is shallow `Heap.Dereference`); server-side deep-resolve so no live heap addr escapes (DISP:28) |
| 7 | #10 | structured-output-capture-seam | FOLD-into-#6 (no standalone parity tie) | **4.00** | 1·2·1·1 (4) | folds→#6 | #6 (envelope plumbing) | **NONE** — captured-output EXCLUDED from parity set (PB:9) | architecture/maintainability value, **NOT** faithfulness; if NOT folded → standalone Optional (owner, D15) (DISP:29) |
| 8 | #26 | glp-gleam-bytecode-runner | ALIGNED | **3.00** | 5·5·5·5 (15) | #27, 034 | #6, #8; unblocks RISK-PROOF-writerMGU | FB-M1-01..45; **SUSPEND proved** PI:15; **suspension/reactivation proved** PI:16; FB-M1-35(PB:89); FB-M1-06(PB:60) | value-copy port MUST dedupe activations by **`(goal_id, suspension_generation)`** (NOT bare goal_id, PB:89 defect); preserve positional X-register; marathon (DISP:17) |
| 9 | #6 | glp-gleam-repl (absorbs #11,#10) | ALIGNED | **3.25** | 5·4·4·4 (13) | #26, #27 | #8, **M1**; #36 (M1 instance for M2) | FB-M1-41/42/43/44; FB-M1-17(PB:71) | single combined **in-process** Gleam instance; engine = typed Gleam value; in-process binding of the **ED-1 seam** (identical envelope to over-the-wire); re-count folded #11/#10 scope (DISP:19; C-156) |
| 10 | #8 | glp-test-corpus-port-and-runner | ALIGNED — **M1 LOCK** | **4.00** | 5·2·5·3 (12) | #6, #26, #27 | **M1 LOCK** | **ALL FB-M1** by outcome-equivalence (Thm 3.34/Rem 3.35) | MUST add GAP-G1/G2/G3/G8 + FORK-1 corpus as criteria+tests before parity declared complete; 100% agreement vs Dart, green on BEAM, no `gleam_otp` (DISP:20) |
| 11 | M2-0 | verify `erlang:monitor` on AtomVM v0.6.6 *(PROPOSED-NEW)* | ADD-NEW (gating spike) | **11.00** | 3·4·4·1 (11) | — | #36, #30, #21 | FB-M2-20(PB:130); GAP-G6(PB:170); feeds RISK-PROOF-distDeref(PI:17) | gating spike ahead of #36's fault model; **owner-fork if `monitor` absent/partial** on AtomVM 0.6.6 (DISP:23; D10) |
| 12 | #36 | glp-gleam-link-layer | ALIGNED — whole M2 term-seam | **3.00** | 5·5·5·5 (15) | M2-0, #6, #15(frame) | #5, **M2**, #29, #21, #18 | FB-M2-* TLV + deferred-local-assignment; **RISK-PROOF-distDeref(PI:17 OPEN)**; GAP-G6(PB:170) | byte-for-byte TLV term codec + FrameCodec envelope, globalize/localize on `known/1`, fault-as-data, loopback→`gen_tcp`; gate on RISK-PROOF-distDeref + GAP-G6 oracle + M2-0; **M2 parity ≠ ISA-identity** (DISP:21) |
| 13 | #21 | multi-accept-transport-extension | SUPERSEDE-by-BEAM + FOLD-into-#36 | **3.00** | 1·1·1·1 (3) | #36 | — | **NONE** (folded) | many concurrent links → #36 `gen_tcp` acceptor pool; no standalone C# feature; carries no independent tie (DISP:31; P3 O-70) |
| 14 | #13 | repl-engine-process-split-mvp | SUPERSEDE C# mechanism; **KEEP seam CONTRACT** | **4.00** | 1·1·2·1 (4) | contract via #6, #36 | — | **NONE** (deployment binding) | in-process(#6) + over-the-wire(#36 `gen_tcp`) = two bindings of one seam; standalone C# demonstrator = owner discretion (D14); REFUTES P1CA:11,103 (DISP:30) |
| 15 | #5 | cross-runtime-csharp-gleam-distributed-tests | ALIGNED (keep-cross-runtime) — **M2 LOCK** | **3.67** | 5·2·4·3 (11) | #36, 025 | **M2 LOCK** | FB-M2-06/07/10 byte-identical codec; FB-M2-04/05 dist-deref; RISK-RUBRIC-M2 (Thm 5.7) | role-parameterized program split across shipped C#(025) + Gleam instance; identical adversarial-corpus verdicts; inherently cross-runtime — retain, do not collapse (DISP:22) |

### 🔴 Load-bearing caveat on the composite (owner must read before sequencing)

The mandated **÷Effort** makes the composite a throughput ratio that **inverts faithfulness-criticality** — re-importing exactly the fastest-path bias the contract forbids. Symptoms: the four crit=TRUE anchors that *deliver* parity and hold the two OPEN proofs (**#26, #27, #6, #36**) **sink to the bottom** on effort; trivial folds (#10, #13, #21) and the cheap gating spike **M2-0** float to the top despite FV=1 or single-tie. **Use the effort-blind numerator `num` for sequencing priority:**

`#26 (15) = #36 (15) > #6 (13) = #4 (13) > #8 (12) > #5 (11) = #27 (11) = M2-0 (11) > #15 (9) = #17 (9) > #23 (8) > #11 (7) > #10 (4) = #13 (4) > #21 (3)`

This aligns with the ratified topo backbone: **034 → #26 → #27 → #6 → #8 = M1 LOCK**; **M2-0 → #36 → #5 = M2 LOCK** (DISP backbone). Nothing crit=TRUE is dispensable on a low composite. Use ÷Effort only to schedule **cheap enablers ahead of the marathons they unblock** (#4, #15, M2-0, #17), never to decide what to drop.

### Mandatory sequencing gates (override any score)

- **RISK-PROOF-writerMGU OPEN** (FB-M1-14/15; PI:14; PB:146) — gates declaring **any** runtime M1-faithful → must precede #8 (M1 LOCK); owned by #26/#27 (DISP:77).
- **RISK-PROOF-distDeref OPEN + GAP-G6 quiescence oracle UNDEFINED** (PI:17; PB:147,170) — M2 linked parity is **not testable** until defined+grounded → gates #36/#5 (M2 LOCK); owned by #36/M2-0/#5 (DISP:78).
- **FORK-1 must be decided + corpus'd before #26/#8 declare any runtime faithful** — F4 (034) already shipped a default, so this is decide-before-F5-locks, not greenfield (P2 C-160/C-196; D5).
- **GAP-G1 language-authority approval before the SRSW checker is built** — else the corpus won't type-check (P2 C-11; D6) → gates #27/front-end.
- **ISA-freeze ⇄ Section-15-codec mutual block** — freeze must first AUTHOR Section-15 (P2 C-109; DEC:44,49) → precedes #4-forward/#23.

### 4 features carry NO P4 tie — flagged, all legitimately non-parity (NOT silent gaps)

**#17** (enabling/arch-fit only, ED-4 head-of-pipeline, declared honestly DISP:25), **#10** (captured-output EXCLUDED from observable set, PB:9; DISP:29), **#13** (deployment binding, seam realized by #6+#36; DISP:30), **#21** (folded into #36; DISP:31). Each is classified enabling/folded/deployment, not a faithfulness omission.

---

## EPIC 2 — *Optional features* (beyond-parity / BEAM-superseded; unordered)

| id | name | why optional | re-scope note |
|----|------|--------------|---------------|
| #20 | engine-state-snapshot-and-persistence-api | internal heap layout **EXCLUDED** from parity (PB:7-9); durability ≠ a criterion (DISP:32) | re-scope Gleam-native (OTP dets/ETS/AtomVM-flash); expand scope (+`_waitReaders`,`_goalId`,`InfrastructureGoalIds`,`GlpChannels`); defer after #6/#26; must preserve FB-M1-34..40 across restore |
| #18 | restore-and-resume-with-link-reestablish | warm-restart durability; net-new `RewireHandle`; **no new parity tie** (DISP:33) | verbatim-address snapshot constrains #20; Gleam-native re-wire over `gen_tcp`; after #20+#36 |
| #30 | liveness-crash-restart-host | OTP supervisors supersede the C# host (operability, **NONE** parity) (DISP:34) | self-prove-GLP-goal half needs a **NEW system predicate** behind language-authority gate **DEF-F1** (DEFERRALS.md:48-49) before any impl; gated by M2-0 |
| #29 | multi-client-control-program-in-glp | consumes FB-M2-11/12 (PB:114-115) but introduces **no new criterion** → demonstrator (DISP:35) | GLP-written N-client control (`serve/2`+`mwm`); source-text-dispatch variant testable on the in-process instance now; `mwm` excluded from type-check → Lean fan-in proof; FOLD-into-#36 |
| #28 | cpp-engine-feasibility | AtomVM already answers the footprint thesis (F1 viable-host; BEAM ~2.6KB); **NONE** parity (DISP:36) | HIBERNATE — revisit only if LLVM/C++ greenlit; if specified, narrow to C++-executor-only + MUST emit explicit infeasibility verdict; grounded ED-2/ED-3, **NOT a fastest-path drop** |
| #33 | many-instances-shared-static-memory-cooperative-scheduling | BEAM natively gives the shared atom/literal pool + reduction-count cooperative scheduler → **nothing to build**; ties no criterion (DISP:37) | REFUTE Lens-A full-gleam (no bespoke feature); residual experiment / fold→#28; GAP-G3 fairness/liveness owned by #8 corpus (PB:167) |
| #16 | research-programme-and-llvm-feasibility | **NONE** parity; drafted reports (DISP:38) | close reports; fix LingoDB citation (`2502.06854`→LingoDB VLDB 2022); HIBERNATE LLVM behind a future greenlight; reassign Lean links to #1a; grounded ED-3, not speed |

---

## ADVISORY MIGRATION MAPPING — every existing not-completed feature → target epic + re-scope

> This is the set of moves `buildkit-roadmap` would apply **at T015 on owner approval**, not before. 24 rows total: 15 Full-Gleam (incl. 1 PROPOSED-NEW M2-0) + 7 Optional + 1 Closed + 1 Harness. (M2-0 is ADD-NEW, the rest are existing.)

**→ Full Gleam implementation (15)**
- **#26** → full-gleam/M1 — value-copy port MUST dedupe activations by `(goal_id, suspension_generation)` (gen-scoped, NOT bare goal_id per PB:89/FB-M1-38); preserve positional X-register.
- **#27** → full-gleam/M1 — hand-ported RD parser conforming to #17 canonical `.g4`; no BEAM ANTLR target.
- **#6** → full-gleam/M1 — single in-process instance; absorbs #11+#10; in-process binding of the ED-1 seam.
- **#8** → full-gleam/M1 (LOCK) — MUST add GAP-G1/G2/G3/G8 + FORK-1 corpus as criteria+tests before parity declared complete.
- **#36** → full-gleam/M2 — gate on RISK-PROOF-distDeref + GAP-G6 oracle + M2-0; M2 parity ≠ ISA-identity.
- **#5** → full-gleam/M2 (LOCK) — inherently cross-runtime; retain, do not collapse.
- **M2-0** → full-gleam/M2 (**ADD-NEW**) — gating spike; owner-fork if `monitor` absent/partial.
- **#23** → full-gleam/M1 (REINSTATE) — re-title (bytecode, not IL, crosses); `VariableMap` crosses wire; effort < "large".
- **#17** → full-gleam/M1 (REINSTATE + FOLD Phase-A/B + DROP C++) — Phase-A grammar-as-verifier → Phase-B production parser + IL-identity.
- **#4** → full-gleam/M1 (SHIPPED 029 + fwd-obligation) — close live drift; MUST NOT cite 029 IlCodec as proof for the Dart/Gleam path.
- **#15** → full-gleam/M1+M2 (REALIGN + SPLIT) — envelope-codec→full-gleam; frame→#36; C# TcpTransport→BEAM `gen_tcp`; correct cite `FrameCodec.cs:64`=fragmentation.
- **#11** → full-gleam component (FOLD→#6) — add parallel `ResolvedBindings`.
- **#10** → full-gleam folded component (FOLD→#6) — architecture/maintainability only; if NOT folded → Optional (owner D15).
- **#13** → full-gleam contract via #6/#36; C# residual→optional (SUPERSEDE mechanism, KEEP seam contract).
- **#21** → full-gleam (→#36) (SUPERSEDE-by-BEAM + FOLD) — `gen_tcp` acceptor pool.

**→ Optional features (7)**
- **#20** → optional (REALIGN/DEFER) — expand scope; after #6/#26.
- **#18** → optional (REALIGN/DEFER) — after #20+#36.
- **#30** → optional (SUPERSEDE-by-BEAM) — language-authority gate DEF-F1 before any impl; gated by M2-0.
- **#29** → optional N-client demonstrator (FOLD-into-#36) — `mwm` excluded from type-check → Lean fan-in proof.
- **#28** → optional conditional spike (HIBERNATE) — revisit only if LLVM/C++ greenlit; if specified, narrow to C++-executor-only + explicit infeasibility verdict.
- **#33** → optional (SUPERSEDE-by-BEAM) — residual experiment / fold→#28.
- **#16** → optional (KEEP-and-CLOSE + HIBERNATE LLVM) — fix LingoDB citation; reassign Lean links to #1a.

**→ Closed (1)**
- **#2** engine-review-and-design-dossier → `closed` — SHIPPED (026); live `reviewed`→mark released (drift); conclusions ratified into ED-1..ED-4 (DISP:39).

**→ Harness (1)**
- **#030** marathon-refinement → `harness` — SHIPPED; live `specified`→mark released (drift; tags v2026.06.12.1+v2026.06.19.1); re-point to **drive #26/#27/#36/#5 marathons**; 024 shared-cluster schema inert (DISP:40).

> **Out of 036 scope (correct-by-scope, no disposition):** 6 not-completed roadmap features in OTHER epics — `depgraph-mark-and-recompute`, `semantic-tombstone-enrichment`, `depgraph-cross-run-trends` (epic codeconv-postv1); `abandon-operation`, `zmq-comm-base`, `nested-structure-head-matching` (epic glp-runtime-gaps). 036 compresses **only** engine-separation + marathon + Gleam-AtomVM. **Roadmap-staleness flag:** `semantic-tombstone-enrichment` shows delivered=false/specified yet shipped 2026-06-26 (v2026.06.26.1); #4/#2/#030 likewise show drift — DISPOSITIONS already records `live→close (drift)` for #4/#2/#030 (DISP:26,39,40).

---

## CONSOLIDATED OWNER-DECISION LIST (forks — options, NOT self-decided)

> All forks are OWNER OPTIONS under the FR-010/FR-011 read-only gate. Recommendations where present are explicitly **advisory**. D1–D3 and D13 are carried from the ANTLR / P7 dossiers via the synthesis digest (cite-attribution to those dossiers; not re-opened on disk in this validation pass — flagged for completeness).

**D1 — ANTLR grammar packaging: combined `Glp.g4` vs split `GlpLexer.g4`+`GlpParser.g4`.** Options: **A combined** — the only shape verified end-to-end today (combined→Dart→byte-identical 17-op bytecode + execution-equivalent), C# half unbuilt (ANTLR:38-50,80-82,94); **B split** — production-faithful, `tokenVocab` seam quarantines GLP's worst lexical hazards (`.`/`=..`, dual-role `|`, SRSW `?`), but split→Dart unbuilt (ANTLR:52-67,98-104). Advisory rec: **B, conditioned on first closing the split→Dart gap** (ANTLR:98). Consequence: trades "verified-as-a-whole today" against "production-faithful + separable, pending one build"; B adds uncounted regen/drift-gate tooling (P2 C-168). *Gates:* #17, #27, FR-005/SC-005.

**D2 — ANTLR error-recovery posture.** Options: keep the spike's `BailErrorStrategy` (loud fail, no recovery) vs production located diagnostics over the whole corpus. The negative corpus (`run_all_tests.sh` Sections C/D) must be **syntactically accepted then rejected by the type/SRSW checker, not the grammar** (ANTLR:124; P2 C-119). Consequence: a too-tight grammar wrongly rejects valid negative-corpus programs. *Gates:* #8, ANTLR front-end.

**D3 — Accepted-language-surface changes a clean grammar admits silently.** A clean grammar naturally accepts `=..` **uniformly in head and body** (lifting the head-only restriction) and structs-in-lists in goals (ANTLR:128; P2 C-33/C-122). These are **language-authority decisions** (CLAUDE.md §1.14), not grammar accidents — ratify or constrain. *Gates:* ANTLR front-end, #27, language authority.

**D4 — ISA freeze + v1/v2 (IOp/IOpV2) opcode-split (ED-6 obligation #3).** *(borderline eng/owner — flag for a ruling BEFORE #4-forward/#23.)* Unify/version the v1/v2 ISA split before bytecode crosses any boundary (DEC:8,49; DISP:69). Consequence: freezing toward v2 can retroactively invalidate the spike's byte-identity anchor (P2 C-108); Obligation-1 (Section-15 codec) and Obligation-3 (freeze) are **mutually blocking** — freeze must first AUTHOR Section-15 (P2 C-109); 029 IlCodec must NOT be cited as proof (DEC:46). *Gates:* #4-forward, #23, all M1 runtimes, ED-6 codec, P4/P8.

**D5 — FORK-1 circular-term deref discriminator (GAP-G5 vs FB-M1-23).** *(the single criterion-level fork in P4.)* Options: **A** — all deref visited-set hits = loud SRSW error (current Dart, HEAP:265-266); defer cross-goal cycles to M2 (risk: unfaithful to core:166). **B** — distinguish structural/cross-goal cycles (graceful, terminate at Bound struct FB-M1-24) from genuine pointer cycles (loud error); pin discriminator in **both** Dart and Gleam (PB:178-185; P6:131-133; DISP:65). Advisory rec: **B**, with a constructed `p(X,f(Y?)),p(Y,f(X?))` corpus before either runtime is declared faithful. Note: F4 (034) already shipped a possibly-unfaithful default (P2 C-196) → decide-before-F5-locks. *Gates:* #26, #8; owner gate.

**D6 — Language-authority gate (a): `ground/1` SRSW relaxation (GAP-G1).** Approve the single-reader relaxation so the Gleam SRSW checker honors **multiple ground-reader occurrences** (core:139; PB:165), or not. Consequence: **without approval the corpus won't type-check** (P2 C-11). Requires Gabi approval **before any impl**. *Gates:* #27, front-end checker, #8, P4.

**D7 — Language-authority gate (b): `#30` self-prove-GLP-goal NEW system predicate (DEF-F1).** Approve a net-new system predicate for the self-supervision half of #30, or leave it blocked (DISP:34,66; DEFERRALS.md:48-49). Consequence: blocks #30's self-prove half; new primitive needs explicit approval (CLAUDE.md §1.14). *Gates:* #30 (Optional).

**D8 — M1 concurrency granularity (P6 FORK-A).** Options: **A1 scheduler-actor** (one BEAM process threading `#(heap,Q,S,F)`, goals-as-data, zero spawns, deterministic ⇒ trivially outcome-equivalent) vs **A2 process-per-goal/variable** (P6:120-123). Advisory rec: **A1**. Consequence: A2 makes FB-M1-35 single-fire a distributed-disarm race and BEAM `receive` resumes-where-blocked vs GLP's resume-at-κ (FB-M1-38) — higher faithfulness risk, no M1 benefit (P6:123; P2 C-62; P3 TRAP-1). *Gates:* #26 scheduler/runner.

**D9 — M1 binding store (P6 FORK-B).** Options: **B1 immutable threaded store** (already built as F4/034, 54 tests green; serves persistence + the M2 ED-1 seam) vs **B2 ETS** (out) vs **B3 process-cells** (out) (P6:125-129). Advisory rec: **B1**. Consequence: mostly settled by F4-as-built, but the ETS-OUT ruling rests on inference-from-absence, not an observed-failure spike (P6:128; P2 C-36/C-61). *Gates:* #26 binding-store.

**D10 — M2-0 conditional fork: `erlang:monitor` fallback fault model.** *(PROPOSED-NEW gating spike; itself net-new scope, P2 C-211.)* Run M2-0 first; **if `erlang:monitor` is absent/partial on AtomVM v0.6.6**, choose a fallback for the #36 fault-as-data model + the #30/#21 OTP-supersession (DISP:23,67; P2 C-69/C-127). Consequence: "raw spawn satisfies M2" is a category error — `gen_tcp`/`monitor` on AtomVM v0.6.6 host were **never opened** (P2 C-68). *Gates:* #36, #30, #21; M2 fault model.

**D11 — M2 design decisions with no Dart/C# behavior to be faithful to yet.** (a) Epoch/fencing token for a conflicting double-bind (FB-M2-R2) — a **recommendation, not in code**; FB-M2-20 monotonicity only emergent (PB:160,130,157; P2 C-20/C-133/C-139). (b) M2 wire-framing spec (FB-M2-R3) — version byte / outer CRC / fragmentation / dual polarity-byte / in-band `#serializer:` sentinel all unsettled (PB:161; P2 C-21/C-91/C-134). Consequence: until owner-decided + implemented these cannot become parity criteria. *Gates:* #36, ED-6-adjacent codec, Optional.

**D12 — M2 wire-seam security / trust boundary (zero corpus coverage).** The M2 seam deserializes executable bytecode + terms from a peer with **no authn/authz/signing/integrity**, and FB-M2-16 FORBIDS wire-side SRSW re-check — a textbook untrusted-deserialization/RCE boundary with no threat model (P2 C-212/MISS-01). Owner must decide whether a threat model / NFR is in scope; a defensive recheck would itself be unfaithful (FB-M2-16). *Gates:* #36, ED-1 seam, Full-Gleam, owner gate.

**D13 — QHSM/YngeniOS packaging fork (P7).** Options: **A active-object** (engine as rich QHSM/QActive — finer kernel observability + kernel-composable GLP-commit via `GlpUnit`) vs **B port/FFI** (engine = opaque service behind a ~5-state supervisor QHSM — lowest faithfulness risk) (P7:64-74). Embedding sub-fork: PATH-A in-process native AOK (plain BEAM only) vs PATH-B out-of-process guardian (the **only** realizable AtomVM embedding) (P7:99-108). Advisory rec: **B for M1**; choose A only if M2/a product feature needs kernel-level GLP-commit ACID composition — **sequenceable, not mutually exclusive**; on AtomVM both reduce to PATH-B (P7:74). Prefer the already-built `Olamnit.Kernel` (C#) until an owner-gated kernel verification pass exists (P7:124). *Gates:* P7 packaging, M1 deliverable wrapper, M2 GATEWAY transport.

**D14 — `#13` standalone C# over-the-wire process-split demonstrator: keep or drop?** Keep a thin C# split as a distinct Optional deliverable, or treat in-process #6 + M2 #36 (`gen_tcp`) as sufficient seam evidence (DISP:30,68). Consequence: whether a C# operational instance is independently maintained (ED-1 seam contract realized either way by #6+#36). *Gates:* Optional; #13.

**D15 — `#10` structured-output-capture seam: fold into #6 or standalone Optional.** *(minor, owner-discretion.)* Fold injectable output/trace hooks into #6's envelope plumbing, or — if not folded — it becomes a standalone Optional with **no parity tie** (captured-output excluded, PB:9) (DISP:29,71). Consequence: architecture/maintainability value only, never faithfulness. *Gates:* #6 / Optional.

---

## Key risks (top P2) and opportunities (top P3) folded into the rationale

**Risks that constrain ordering (work-to-discharge gates):** (1) **RISK-PROOF-writerMGU OPEN** gates declaring any runtime M1-faithful → precede #8 (PI:14; P2 C-01/C-146/C-189). (2) **ED-6 float-decode on AtomVM UNVERIFIED** — `/float` bit-syntax extraction not grounded; must spike before committing the Gleam codec, else the M2 byte-parity decision is invalidated (P6:139; P2 C-24/C-53/C-83/C-202). (3) **ISA-freeze ⇄ Section-15 mutual block** (P2 C-109). (4) **M2-0 `erlang:monitor` spike gates #36** (P2 C-127). (5) **RISK-PROOF-distDeref OPEN + GAP-G6 oracle UNDEFINED** → M2 not testable until defined (PI:17; P2 C-02/C-16/C-126/C-195). (8) **F5 generation-scoped dedup `(goal_id, suspension_generation)`** is a hard constraint — the bar text "dedupe by goal_id" (PB:89) is **defective** and breaks FB-M1-38 if implemented verbatim (P6:47,143; P2 C-31/C-148/C-197; P3 TRAP-2). (9) **64-bit overflow parity MASKED on plain-BEAM** (Gleam Int = bignum) — corpus-green on BEAM is not an AtomVM-faithfulness signal for 64-bit edges (P6:142; P2 C-26/C-52/C-143/C-203). (10) **M2 transport never opened on AtomVM** — raw spawn+Subjects are same-node only; SC-001 satisfiable on loopback masks real-socket/MTU/CRC/FIFO parity (P2 C-68/C-171/C-175). (13) **Process/restart-safety** — marathon-row + tracker staleness threatens SC-008; resume could re-run completed pipelines (P2 C-182/C-208).

**Opportunities that justify the fold/supersede verdicts:** **O-66** process boundary + message passing IS the ED-1 seam in-node → justifies superseding #13's C# OS-process-spawn mechanism by BEAM while keeping the seam contract. **O-65** `gen_tcp`/`inet` deletes `TcpTransport`/`TcpEndpoint`/`_SocketReader` (386 lines) → grounds #15's `C# TcpTransport→gen_tcp` split. **O-70** cheap `spawn` + blocking `accept` → grounds #21 FOLD-into-#36. **O-01/O-02** mailbox + first-class Pids/Subjects delete the inbound-pump + IsolateManager → fold into #36. **O-09/O-18** `link`/`monitor`/`trap_exit` → grounds #30/#21 OTP-supersession, **gated by D10/M2-0**. **O-27/O-39/O-45..O-53** ground-kind ADT + bit-syntax → enables the Section-15 codec de-embedding (D4/ED-6) and a materially smaller Gleam codec. **O-20..O-41 cluster** immutability + closed-ADT exhaustive match + `Result`-no-exceptions delete a large defensive surface → quantified justification for keeping F4's immutable store (D9-B1) and **re-expressing #26/#27 rather than transliterating**.

**Counterweights the roadmap must NOT bank as savings (they UP-size, not fold):** **O-24/TRAP-2** immutable value-copy makes single-fire HARDER — it *forces* the F5 `(goal_id, suspension_generation)` dedup obligation (an added obligation). **Fold-in scope leakage** — #6 absorbs envelope/deep-resolve/output-capture; #36 absorbs frame-envelope + `serve/2`/`mwm`; scope must be re-counted into the receiving features, not treated as free deletions (P2 C-156/C-176). **O-71** marathon (#030) folds to NOTHING — Python/PGLite/DBOS tooling; "compressing 3 epics" must not over-claim feature consolidation.

---

## Success-criteria self-assessment

**SC-001 (100% not-completed features have a disposition + target-epic) — PASS within the program's 3-epic mandate.** 23/23 not-completed features in {gleam-atomvm, repl-engine-separation, marathon} have a disposition row + non-blank target_epic (DISP:17-40). 6 not-completed roadmap features have no disposition but are in OTHER epics (codeconv-postv1, glp-runtime-gaps), OUTSIDE the 036 mandate — correct-by-scope, not a defect. A literal whole-roadmap reading would flag those 6; flagged for transparency.

**SC-002a (valid topological sort) — PASS.** The 15-node order `#4 → #15 → #23 → #17 → #27 → #11 → #10 → #26 → #6 → #8 → M2-0 → #36 → #21 → #13 → #5` satisfies every in-set `blocked_by` (DISP:17-31). External SHIPPED anchors (034, 029, 027, 025) are delivered=true → position-0. The #23↔#17 "enables/blocked_by" pair is consistent (#23 precedes #17 Phase-B), not a cycle. No violation.

**SC-002b (100% scored) — PASS.** All 15 Full-Gleam features carry FV/Enab/RiskRed/Effort + composite + numerator. None unscored.

**SC-002c (100% tie ≥1 P4 criterion) — PASS within scope, with 4 declared-infrastructure exceptions.** 11/15 tie ≥1 confirmed P4 criterion (re-verified: #26, #27, #6, #8, #4, #36, #5, M2-0, #15, #11, + #23). **4 carry NONE — each legitimately non-parity, declared honestly in DISPOSITIONS, NOT silent gaps:** #17 (enabling/arch-fit, DISP:25), #10 (captured-output EXCLUDED, PB:9/DISP:29), #13 (deployment binding, DISP:30), #21 (folded, DISP:31). A strict literal "100%" reading flags these 4; all are correctly classified as enabling/folded/deployment, not faithfulness omissions.

**Forks presented as OWNER OPTIONS — PASS.** D1–D15 above each carry competing options + consequence + gated features under the FR-010/FR-011 read-only gate; recommendations are explicitly advisory. No self-decided fork.
