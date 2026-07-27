<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T082 close-process-baseline-program-dossier` (b3-c2-044)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Closes**: `verify-process-baseline-program-dossier` (b3-c1-019) — 6 MATCH, 0 DRIFT
**Gated by**: `rule-process-engine-instances-scaling-research` (b3-c2-023) — engineer-owned ruling
**Backing detail_ids**: `baseline-program-dossier`, `engine-instances-scaling-research`, `full-scope-gleam-anchor`, `marathon-run-position`, `roadmap-constituent-reconciliation`, `runtime-gap-features-reference`

## What was verified DELIVERED

All six process/decision records were re-opened in the current tree and compared line-for-line
against their cited evidence: **6 MATCH, 0 DRIFT**. No drift was raised into the feature drift
controls. Sources inspected: `specs/036-glp-gleam-baseline-program/spec.md` (US1 two-epic
reconfiguration + US2 faithfulness-spec-with-proofs, :17-66) and its P4 INDEX
(`docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/INDEX.md`); the **frozen dated**
`roadmap-snapshot-2026-07-19.md` (line numbers cannot drift); `docs/handover/050-full-gleam-M2-restart-2026-07-13.md:5`;
`specs/050-full-gleam-combined/spec.md:12,188,191`.

## Inspectable evidence (fresh-session reproducible)

| Check | Command / anchor | Result |
|---|---|---|
| Baseline dossier + proof register | `specs/036-.../spec.md:17-66` + P4 INDEX | MATCH — two-epic reconfig + faithfulness spec; P4 INDEX = 4 proved / 2 open / 0 refuted |
| Scaling-research rows (rule-request evidence) | `roadmap-snapshot-2026-07-19.md:40-42` | all three `[refined]`, **no `spec:` annotation** (no spec dir) |
| Full-scope anchor | `roadmap-snapshot-2026-07-19.md:69` | `[captured] … [blocked-by: gleam-implementation-combined-full-gleam-feature]` |
| Marathon run position | `docs/handover/050-full-gleam-M2-restart-2026-07-13.md:5` | MATCH at record time — M1 `mrun-d96119d59d07` DISCHARGED; M2 open; 3 discharge gates |
| Constituent reconciliation | `roadmap-snapshot-2026-07-19.md:62-67` + `050 spec.md:12,191` | six rows `[refined]`; understatement is **by protocol** |
| Runtime-gap-features reference | `roadmap-snapshot-2026-07-19.md:10-12` | MATCH — comparison-guards `[released, delivered]`; nested-struct HEAD + FCP abandon `[refined]` |

## Disposition (per detail_id)

| detail_id | disposition |
|---|---|
| `baseline-program-dossier` (b1-c1-065) | **MATCH — no drift** (roadmap:85; 036 spec.md:17-66; 050 spec.md:188; P4 INDEX) |
| `engine-instances-scaling-research` (b1-c1-073) | **MATCH — no drift**; the roadmap:40-42 evidence (three `[refined]` research rows, no spec dirs) feeds `rule-process-engine-instances-scaling-research` (b3-c2-023) — ruling engineer-owned, **not** made here |
| `full-scope-gleam-anchor` (b1-c1-059) | **MATCH — no drift** (captured, blocked-by combined) |
| `marathon-run-position` (b1-c1-076) | **MATCH at record time**; live run state not re-queried (file-inspection method) — corroborated indirectly by the combined feature being unshipped |
| `roadmap-constituent-reconciliation` (b1-c1-060) | **MATCH — no drift**; the six `[refined]` states **understate** recorded M1 delivery **by protocol** (050 spec.md:12,191), not by error — see conditional-drift trigger |
| `runtime-gap-features-reference` (b1-c1-072) | **MATCH — no drift** (roadmap:10-12) |

## Conditional-drift trigger (recorded, NOT performed by this close)

The six `[refined]` constituent rows (`roadmap:62-67`) and the `[captured]` anchor (`roadmap:69`)
are consistent **only while the combined Full-Gleam feature is unshipped**. Per the 050
reconciliation protocol (`specs/050-full-gleam-combined/spec.md:12,191` — "roadmap rows stay
`refined`; advance/close them when this ships"), the `[refined]` states understate M1 delivery by
design; reading them naively as "not delivered" would be a false-drift finding (the WP's explicit
Risk). They become **genuine drift** only when `gleam-implementation-combined-full-gleam-feature`
(`roadmap:68`) advances from `[promoted]` to released, at which point the protocol obliges advancing
or closing those rows and re-registering the marathon gates. This close **records the trigger**; the
roadmap/marathon-row mutation is engineer-gated (advisory tools only) and is performed at that
future ship, not here.

**Close status: CLOSED.** Six records reconciled MATCH with 0 drift; the conditional-drift trigger
is recorded for the combined-feature ship; the scaling-research out-of-scope disposition is held
pending the engineer-owned `rule-process-engine-instances-scaling-research` (b3-c2-023) ruling.
