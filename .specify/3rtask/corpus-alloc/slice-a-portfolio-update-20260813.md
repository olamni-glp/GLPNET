# Slice A — UPDATE 2026-08-13 (portfolio deltas since the 20260812T0824Z snapshot)

This file is an ADDITIVE update to `slice-a-portfolio.md`. Where the two disagree, this file
wins. Lane: **the WHAT** — feature identity, state, score, spec path, blocking edges,
sequencing. (Host capability lives in slice B; tooling/protocol rules live in slice C.)

## A1. Fleet-converged catalog triple
`19 epics / 113 features / 3266 journal lines`, export sha256 `175A20E5…5075E`, agreed
independently by both active hosts (ariellas round `103407Z`, gavriella round `164604Z`).
25 rows are NOT closed. **Only 23 are allocatable** — 2 are already `shipped` and need
close-out only.

## A2. State changes since the snapshot

| feature | was | now | evidence |
|---|---|---|---|
| `durable-listener-service-box` (064) | specified, blocked on cert material | **released** — tag `v2026.08.12.1` → `588ed177`, PRs #150/#151/#152 merged, suite 551/551 on shipped tree `bfee27ba`, drills resume 7/7 + history 4/4 | gavriella `20260812T091121Z` §3 |
| `verification-receipts-and-loud-failure…` (F1, WSJF 7.80 / RICE 1173) | promoted | **specify COMPLETE** — spec dir `specs/078-verification-receipts/`, branch `078-verification-receipts` off develop @ `9ac52d4c`, marathon `mrun-20d9230f767b` open with 11 gates; content 13 witnessed instances / 4 user stories / 18 FRs / 8 SCs / 10 edge cases, checklist 16/16, 0 NEEDS-CLARIFICATION. **Roadmap still shows `promoted`** — see A5. | gavriella `20260812T164655Z` §3 |
| `type-checker-body-atom-moding…` (076, WSJF 4.20 / RICE 2800) | promoted, §1.14 HARD GATE open | **gate DISCHARGED** — engineer approved the occurrence-pair licensing semantics 2026-08-12; `docs/type system/well-typed-clause.md` amended; specify+clarify+plan+tasks+analyze all done. Next work is `/bk-implement` T004→T008 (US1), then T009–T012, then T013–T015. Test baseline to beat: **547/547** plus 3 new FR-007 tests. | repo commit `5c22ac7c`, `677b6a55` |
| `qr-link-provisioning` (067, WSJF 4.00 / RICE 252) | specified, implement done | **implement complete @ `abe9aec5`** on `067b-qr-link-continuation`; marathon `mrun-d15072abb4c4` 6/9. **SHIP DELIBERATELY HELD** by ariellas — 067's C# seam pins the trust material that `buildkit ship` is destroying (mechanism in slice C). Remaining: codexreview → ship → close. | gavriella `164655Z` §1 |
| `pglite-pg16-to-pg17…` (WSJF **14.00** / RICE 1800 — portfolio rank #1) | promoted, premise "likely VOID" | **premise VOID, verified on a second host**: `Test-Path C:\pglite` → False (no migration source) AND `.pgdb\PG_VERSION` → 17 (target state already holds). **Both legs of the premise fail.** Recommendation on the record: `roadmap reject` with rationale, do NOT build. Not yet rejected — a one-way roadmap mutation needing the engineer's word. | gavriella `091121Z` §6 |
| `atomic-toolchain-installs…` (5.67/160) | shipped | shipped; **close-out only** | roadmap |
| `batch-roadmap-advance-calver…` (3.67/80) | shipped | shipped; **close-out only** | roadmap |
| `wave6-consolidation` (066) | specified, US5 tail parked on G1 | **US5 tail UNPARKED** — the 064 release cleared G1, unparking T022→T023 and the whole US5 tail | gavriella `091121Z` §3 |

## A3. Hard blocking edges (recorded in the catalog, not heuristic)

- **F1 `verification-receipts` hard-blocks all five other fleet-RCA standalones.** F2
  `multi-host-state-discipline` (3.00/680), F3 `per-host-toolchain-and-environment-contract`
  (3.60/960), F4 `seam-specification-normative-contracts` (2.62/578), F5
  `single-source-of-truth` (2.60/540), F6 `product-defect-burn-down-with-regression-proof`
  (1.23/240) each carry `blocked-by: verification-receipts-…`. **Any host that claims F2–F6
  before F1 lands is blocked by construction.**
- **F1 ∩ F3 share one surface.** F3 is "declared, machine-checked, loudly refused"; F1 is "no
  check may pass without proving it ran". The loud-refusal mechanism is ONE implementation
  serving both. F3 must reuse F1's mechanism, not build a second.
- **F2 conflicts with work already landed.** F2's scope includes "untracked derived artifacts";
  `.import-manifest.json`, `.import-provenance/` and `.import-refused.json` were untracked +
  gitignored on branch `077-roadmap-sync-mechanics` (**PR #153 → develop, OPEN, NOT MERGED**).
  Whoever picks up F2 must start from that landed state or they re-litigate it. This is a
  genuine conflict, not an overlap.
- **041 `cross-runtime-and-two-host-acceptance` (2.62/625) is ENVIRONMENT-BLOCKED**: requires a
  reachable second LAN host (gavri endpoint) AND an MSVC/msquic-built `quicer` NIF. Neither
  exists on the primary host. Not schedulable as ordinary work.
- **`crdtmsg-post-mvp-completion`** carries its own §1.14 language-authority gate on the
  policy-guard half; degrades to COSE-only if the gate is not opened.

## A4. The engineer's standing sweep order (2026-08-11 directive, still in force)

Strictly one at a time, end-to-end, marathon-recorded, safe restart before each of
`/bk-specify`, `/bk-implement`, `/bk-codexreview`:

1. `type-checker-body-atom-moding…` (076) — **ACTIVE**
2. `madglp-writer-reader-address-discipline-closure…` (5.33/2667)
3. `front-end-goal-term-acceptance-completeness…` (3.60/3000)
4. `pglite-pg16-to-pg17…` (premise now VOID — see A2)
5. `glptutorial-corpus-golden-reconciliation…` (6.50/1700)
6. `crdtmsg-post-mvp-completion…` (2.40/420)
7. `041-cross-runtime-and-two-host-acceptance…` (env-blocked)

## A5. Portfolio-visible consequence of the roadmap/pipeline divergence

Roadmap feature *state* is now known to lag the pipeline for any feature whose spec directory
name does not exactly equal its roadmap slug. Confirmed instances: **078** (specify complete /
roadmap `promoted`) and, prospectively, **069-sc-002-il-parity-bridge**. Practical rule for any
allocation that relies on roadmap state: **roadmap state is not a trustworthy readout of
pipeline position; advance must be asserted explicitly by feature id.** (Mechanism and proof
are in slice C — this entry records only the portfolio consequence.)

## A6. Unscored / unsized rows still in the open set

`distributed-unification-quiescence-protocol-two-runtime-spec-first`,
`full-scope-gleam-glp-implementation` (059), `ynet-consolidation` (065),
`ynet-human-memorable-decentralized-naming-resolver`,
`ynet-mobile-background-battery-budget-scheduling-policy`, `wave6-consolidation` (066),
`durable-listener-service-box` (064, now released) carry **no WSJF and no RICE**. Any ordering
that claims to be priority-driven must say explicitly how it placed these, or it is asserting
an order it cannot justify.

## A7. Duration data

PERT triples exist for exactly **two** features in the whole 23-row allocatable set — both
supplied by gavriella for her own claimed lane, and both explicitly flagged by their author as
*estimates from one host's history, not measurements*:

| feature | O | M | L |
|---|---|---|---|
| F1 `verification-receipts` | 6h | 14h | 30h |
| 066 US5 tail | 3h | 7h | 16h |

The author **declined to give triples for anything not claimed**, on the stated grounds that
inventing them is the same defect class as an empty board reporting success. **No honest
P50/P80/P95 exists for the other 21 rows.** The per-feature pipeline chain is **13 serial
units** (9 stages + 3 safe restarts + 1 sync round) — a structural unit count, not a duration.
