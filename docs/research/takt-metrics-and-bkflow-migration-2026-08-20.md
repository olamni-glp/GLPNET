# Takt metrics for the buildkit marathon → `/bk-flow` migration

**Date**: 2026-08-20 · **Host**: Gavriella · **Marathon**: `mrun-20d9230f767b` · **Feature**: 078-verification-receipts
**Status**: ACTIVE — this is the takt contract the marathon must emit and `/bk-flow` must consume.

---

## 1. The takt targets (engineer-set, 2026-08-20)

| Unit | Target range | Notes |
|---|---|---|
| One feature **phase** | **30 min – 3 h** | phases: `analyze` · `implement` · `codexreview→ship` · `close` |
| One whole **feature** | **1.5 h – 6 h** | 4 phases × takt, with overlap |

A feature that takes longer than 6 h is **oversized** and must be split at the roadmap layer,
not absorbed by a longer session. A phase that completes in under 30 min is **suspect**, not
fast: verify it actually ran (this is feature 078's own subject — a check that did not run must
never be indistinguishable from a check that passed).

## 2. Sizing scheme (canonical, active)

`buildkit-size scheme show` → **default**, active:

| Label | nano | micro | mini | midi | maxi | saga |
|---|---|---|---|---|---|---|
| Points | 1 | 3 | 7 | 11 | 17 | 35 |

**Takt/point derivation.** With a feature at 1.5–6 h and a typical feature scoring
`midi`(11)–`maxi`(17), the implied rate is **~11–33 min per point**. Use **20 min/point** as
the planning midpoint until enough actuals exist to replace it. Derived phase budgets:

| Size | Points | Implied phase span @20 min/pt | Verdict vs takt |
|---|---|---|---|
| nano | 1 | 20 min | below the 30-min floor — batch these |
| micro | 3 | 1 h | in takt |
| mini | 7 | 2.3 h | in takt |
| midi | 11 | 3.7 h | **exceeds the 3 h phase ceiling** — split |
| maxi | 17 | 5.7 h | one whole feature; never one phase |
| saga | 35 | 11.7 h | **oversized — must be split at roadmap** |

**Rule derived from the table**: a *phase* may be at most `mini` (7 pts). `midi` and above are
*features*, not phases. `saga` never enters the pipeline undivided.

## 3. Measured actuals — this session (2026-08-20)

First real takt data. Source: marathon checkpoints + 3rtask `run.json` + git commit times.

| Work item | Size | Pts | Wall clock | Notes |
|---|---|---|---|---|
| 3rtask planning (5 blind codex passes) | maxi | 17 | ~40 min | 4 method revisions; see §5 defect |
| 3rtask execution (4 blind builders, cycle 1) | maxi | 17 | ~33 min | parallel; 220 merge rows |
| Critic adjudication (codex, 220 rows) | mini | 7 | ~6 min | 184C / 24R / 12E |
| W02 sync develop | nano | 1 | <1 min | |
| W03 verify 082 | nano | 1 | ~2 min | no-op by measurement |
| W04 merge 065 + PR #186 | micro | 3 | ~12 min | |
| W08 recover 5 sync branches | mini | 7 | ~14 min | 5 merges, identical conflict class |
| W10 delete 70 branches | mini | 7 | ~9 min | preservation-first |
| roadmap round 27 | micro | 3 | ~6 min | both publish legs |

**Observed rate**: the non-3rtask items ran at **~1.5–2.5 min/point** — an order of magnitude
faster than the 20 min/pt planning midpoint. The 3rtask items ran at **~2 min/pt** but consumed
**2.53 M tokens**. So wall-clock takt and *token* takt diverge sharply, and wall-clock alone is
not a sufficient takt signal.

### 3.1 The second takt axis — tokens

`buildkit-size tokens report --feature 078-verification-receipts`:

```
3rtask:  2 526 541 tokens (25 records)
analyze:   175 000 tokens (1 record)
feature total: 2 701 541 (reconciles=True)
```

**A feature that fits the 6 h wall-clock takt can still blow a token budget by 15×.** The takt
contract therefore needs **two** dimensions per phase: `wall_clock_seconds` AND `tokens`. A
single-axis takt is the same class of error as a gate that reports pass without running.

## 4. What the marathon must emit (the contract)

Per completed step, the marathon already stores `started_at` and `completed_at` (checkpoint
rows). To make takt first-class, each checkpoint should additionally carry:

```
takt: {
  size_label:  nano|micro|mini|midi|maxi|saga,
  points:      <int from the active scheme>,
  phase:       analyze|implement|codexreview-ship|close|other,
  wall_seconds:<completed_at - started_at>,
  tokens:      <from the spec-020 ledger for this step>,
  in_takt:     true|false,          // 30min<=wall<=3h for a phase
  oversize:    true|false           // points > 7 for a phase
}
```

`wall_seconds` and `points` are already derivable today (checkpoint timestamps +
`buildkit-size show`); `tokens` is already recorded per stage. **No new store is required** —
this is a projection over three existing sources, which is why it can ship without a schema
migration.

## 5. Blocker found while measuring — the phase clock is not trustworthy yet

Two defects measured today corrupt any takt derived naively:

1. **`marathon expand --steps` is comma-delimited with no escaping.** Two of twelve step texts
   split on internal commas, creating 2 truncated steps + 2 orphan fragments; 2 further probe
   steps were minted while diagnosing. Steps are **grow-only** — no delete verb — so the board
   is permanently inflated **13 → 30**. Any "steps complete / steps total" takt ratio computed
   from this run is therefore **wrong by construction** (10/30 understates real progress).
2. **The blind Critic is non-deterministic**: two passes over artifacts differing in exactly one
   element produced 7 verdict flips, 6 on byte-identical text (~25% churn). A phase whose exit
   gate is a Critic verdict has **no reproducible completion time**.

**Consequence for takt**: report takt per *checkpointed step*, never as a fraction of a step
total, until (1) is fixed; and treat any Critic-gated phase duration as a **lower bound**.

## 6. `/bk-flow` migration readiness — 5–20 sessions

| Prereq | State | Blocking? |
|---|---|---|
| Canonical size scheme active | ✅ default scheme, `child_total=112` recorded | no |
| Per-stage token ledger | ✅ `buildkit-size tokens` reconciles=True | no |
| Step start/complete timestamps | ✅ checkpoint rows | no |
| `takt` projection emitted per step | ❌ not implemented (§4) | **yes** |
| Step board integrity | ❌ grow-only + delimiter defect (§5.1) | **yes** |
| Reproducible phase-exit gates | ❌ Critic non-determinism (§5.2) | **yes** |
| `bk-flow` reachable on PATH | ❌ `bk-flow.exe` exists in the pinned .4 venv but is **absent from PATH**, and the umbrella CLI has no `flow` subcommand (`mitem-01a01736-4472`) | **yes** |

**Assessment**: 4 of 7 prerequisites are unmet. The migration is **not** a next-session move; it
is realistically **10–20 sessions** out, and the two integrity defects (§5) should be fixed
*before* takt data is accumulated, because takt measured on a corrupt board bakes in the error.

## 7. Recommended sequence

1. Fix `expand --steps` delimiter handling + add a step-void verb (unblocks board integrity).
2. Emit the §4 `takt` projection on every checkpoint (no migration needed).
3. Accumulate ≥3 features of real actuals; replace the 20 min/pt planning constant with measured
   per-size medians.
4. Put `bk-flow` on PATH or add the `flow` subcommand to the umbrella CLI.
5. Re-derive the size→phase mapping in §2 from actuals, then migrate.

---

*Derived from marathon `mrun-20d9230f767b` and 3rtask run `20260820T072729Z-1de6`
(4 blind builders / 4 disjoint slices / 0 independence violations).*
