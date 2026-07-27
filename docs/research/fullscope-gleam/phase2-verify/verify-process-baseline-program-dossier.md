<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-process-baseline-program-dossier` (WP b3-c1-019, wave 2)

**Date**: 2026-07-22
**Method**: file-inspection only (process/decision-record drift check — no code to execute). Each of the six records' cited evidence was re-opened in the current tree and compared line-for-line.
**Paired close**: `close-process-baseline-program-dossier` (b3-c2-044) — the per-record reconciliation table below is its input.
**Feeds**: `rule-process-engine-instances-scaling-research` (b3-c2-023) — the rows 40-42 inspection below is the evidence that rule-request cites (ruling is engineer-owned; **not** ruled here).
**Backing detail_ids**: `baseline-program-dossier`, `engine-instances-scaling-research`, `full-scope-gleam-anchor`, `marathon-run-position`, `roadmap-constituent-reconciliation`, `runtime-gap-features-reference`.

## Sources inspected (all present, all as cited)

- `specs/036-glp-gleam-baseline-program/spec.md` (US1 two-epic reconfiguration + US2 faithfulness-spec-with-proofs at :17-66) and its **P4 INDEX** at `docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/INDEX.md`.
- `docs/research/fullscope-gleam/roadmap-snapshot-2026-07-19.md` — a **frozen dated snapshot** (line numbers cannot drift); rows 10-12, 33-42, 62-69, 85, 105-133 read.
- `docs/handover/050-full-gleam-M2-restart-2026-07-13.md` gate list (:5).
- `specs/050-full-gleam-combined/spec.md` :12, :188, :191 (the reconciliation + authoritative-decision-record anchors).
- Record definitions cross-read in `docs/research/fullscope-gleam/gap-inventory-2026-07-19.md` (b1-c1-065/073/059/076/060/072).

## Verdict table

| # | detail_id (inventory id) | cited evidence | verdict |
|---|---|---|---|
| 1 | `baseline-program-dossier` (b1-c1-065) | roadmap:85; 036 spec.md:17-66; 050 spec.md:188; P4 INDEX | **MATCH** — no drift |
| 2 | `engine-instances-scaling-research` (b1-c1-073) | roadmap:40-42 (all `[refined]`) | **MATCH** — no drift |
| 3 | `full-scope-gleam-anchor` (b1-c1-059) | roadmap:69 (`[captured]`, blocked-by combined) | **MATCH** — no drift |
| 4 | `marathon-run-position` (b1-c1-076) | handover:5 (M2 run + 3 gates) | **MATCH** (at record time — live run state not re-queried; see caveat) |
| 5 | `roadmap-constituent-reconciliation` (b1-c1-060) | 050 spec.md:12,191; roadmap:62-67 (six `[refined]`) | **MATCH** — no drift (understatement is by protocol) |
| 6 | `runtime-gap-features-reference` (b1-c1-072) | roadmap:10-12 | **MATCH** — no drift |

**Tally**: 6 MATCH · 0 DRIFT. No drift finding is raised into the feature drift controls. One conditional-drift trigger and two hygiene caveats are flagged below.

## Per-record evidence

### 1. `baseline-program-dossier` — MATCH
- `roadmap-snapshot:85` → `[released] glp-gleam-baseline-program [delivered]`. ✓
- `036 spec.md:17-66` carries US1 ("decision-ready, verified **two-epic reconfiguration**" — *Optional features* + *Full Gleam implementation*) and US2 ("corpus-verified **faithfulness specification with proofs**"). ✓ matches the record's "two-epic reconfiguration … corpus-verified faithfulness spec".
- The **P4 INDEX** exists and is the proof register the dossier created: 4 proved / 2 open / 0 refuted (writer-MGU PI:14 proved; distributed-deref SPIN and computed-answer-soundness GAP-G4 recorded **open** as first-class faithfulness risks). ✓ matches "proofs recorded in the P4 INDEX it created".
- `050 spec.md:188` → "**Authoritative decision record**: the 036 baseline-program dossier (decisions D1–D16, obligation sets FB-M1-*/FB-M2-*, outcome-equivalence Thm 3.34 / Rem 3.35) backs this spec". ✓ matches "authoritative decision record behind 050".

### 2. `engine-instances-scaling-research` — MATCH (and this is the rule-request evidence)
`roadmap:40-42`, all three still `[refined]` with **no `spec:` annotation** (i.e. no spec dir), exactly as recorded:
- :40 `#38 cpp-engine-feasibility [refined]`
- :41 `#48 many-instances-shared-static-memory-cooperative-scheduling [refined]`
- :42 `#17 research-programme-and-llvm-feasibility [refined]`
Released rows in the same file carry `spec: specs/NNN`; these carry none → designed-unstarted research rows, no spec dirs. This is the precise evidence `rule-process-engine-instances-scaling-research` (b3-c2-023) needs for its out-of-scope proposal.

### 3. `full-scope-gleam-anchor` — MATCH
`roadmap:69` → `[captured] full-scope-gleam-glp-implementation [blocked-by: gleam-implementation-combined-full-gleam-feature]`. ✓ The anchor is a captured, unspecified ambition blocked behind the combined feature (`:68` `[promoted] gleam-implementation-combined-full-gleam-feature`, not yet released) — exactly the record's "captured row, no spec dir, blocked".

### 4. `marathon-run-position` — MATCH at record time; live-state caveat
`handover:5` → "M2 marathon run = **`mrun-6bea075ec79e`** (M1's `mrun-d96119d59d07` is **DISCHARGED**). 14 US4 steps seeded; **3 M2-lock discharge gates registered (T061 FR-016, T066 acceptance, T068 final regression)**." ✓ matches the record verbatim (M1 discharged; open M2 run; the three gates = 16/16 capstone / acceptance sweep / final regression).
- **Caveat (scope, not drift):** this is a process-state snapshot dated 2026-07-13/19; this file-inspection verify does **not** re-query the live marathon run to confirm M2 is still open. It is corroborated indirectly: the combined feature has **not** shipped (record #5, and `:68` still `[promoted]`), so the "M2 open / reconciliation pending" posture is internally consistent as of the snapshot.

### 5. `roadmap-constituent-reconciliation` — MATCH; understatement is by protocol, NOT drift
This is the record that disarms the WP's Risk. `roadmap:62-67` — the six Full-Gleam constituent rows are **all still `[refined]`** (glp-gleam-bytecode-runner, glp-gleam-compiler-and-loader, glp-gleam-repl, glp-test-corpus-port-and-runner, glp-gleam-link-layer, cross-runtime-csharp-gleam-distributed-tests). Two spec anchors confirm this is deliberate:
- `050 spec.md:12` → "Folded constituent features (**roadmap rows stay `refined`; advance/close them when this ships**): [the six rows]."
- `050 spec.md:191` → "**Roadmap reconciliation**: the six constituent roadmap rows remain `refined` during this feature and are advanced/closed when it ships; the `wave-3-consolidated-full-gleam-chain` ordering row is **superseded**." ✓ (matches the record's "and wave-3 superseded").
So the `[refined]` states **understate** recorded M1 delivery *by design*. Reading them naively as "not delivered" would be a false-drift finding — the WP's explicit Risk — and is rejected here.

### 6. `runtime-gap-features-reference` — MATCH
`roadmap:10-12`: `#13 comparison-guards [released] (delivered; spec docs/guards-reference.md#comparison-guards)`; `#51 nested-structure-head-matching [refined]`; `#43 abandon-operation (FCP-exact) [refined]`. ✓ Comparison guards recorded delivered (independently confirmed DELIVERED by the sibling `verify-guards-guard-defined`, b3-c1-002); nested-structure HEAD matching and the FCP-exact abandon operation remain designed-unstarted (refined, no spec dirs) in the reference runtimes and correspondingly unpromised for Gleam.

## Drift controls / activation

- **No drift raised.** All six process/decision records match current repo state; nothing is flagged into the feature drift controls.
- **Conditional-drift trigger (hand to `close-process-baseline-program-dossier`, b3-c2-044):** the six `[refined]` constituent rows (#5) and the `[captured]` anchor (#3) are consistent **only while the combined Full-Gleam feature is unshipped**. When `gleam-implementation-combined-full-gleam-feature` (roadmap:68) advances from `[promoted]` to released, those rows become **genuine drift** and the reconciliation protocol (050:12,191) obliges advancing/closing them. That roadmap/marathon-state update is exactly the close WP's deliverable; this verify records the trigger, does not perform it.
- **Feeds `rule-process-engine-instances-scaling-research` (b3-c2-023):** record #2 supplies the cited rows-40-42 evidence (three `[refined]` research rows, no spec dirs). The out-of-scope ruling is **engineer-owned** and is not made here.
- **Two citation-hygiene caveats (not drift):**
  1. The `D1–D16` / `outcome-equivalence theorem` literals in record #1's evidence resolve via `050 spec.md:188` and the dossier artifacts, **not** verbatim in `036 spec.md:17-66` (which carries the two-epic + faithfulness-spec-with-proofs content). A future reviewer grepping only 036 spec.md:17-66 for "D1–D16" would find nothing — the citation is triangulated, not single-source.
  2. The P4 INDEX's two `open` obligations (distributed-deref SPIN; computed-answer-soundness GAP-G4) remain recorded faithfulness risks. These are **consistent** with record #1 (which never claimed all-proved) and are owned by the sibling WP `verify-proofs-proof-dist-deref-convergence` (b3-c1-020) — not adjudicated here.
