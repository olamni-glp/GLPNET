<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Standardised cross-host SITREP + roadmap table format

**Purpose**: every host (`ariellas`, `gavriella`, `olamnit`, `shiras`) and every repo emits the
same two tables, so a reader can diff two hosts without re-deriving either. Adopted 2026-08-23.

🔴 **Every field below must be MEASURED from a durable source. No field may be an estimate.**
Where a value cannot be measured, write `unmeasurable` — never `0`, never a guess.

---

## Table A — SITREP header (one row per repo lane)

| Field | Source of truth (how to measure) |
|---|---|
| `host` | the machine's actor id — `ariellas` \| `gavriella` \| `olamnit` \| `shiras` |
| `repo` | repo directory name |
| `branch` | `git branch --show-current` |
| `run_id` | `buildkit-marathon resume --feature <f>` → `run` |
| `steps` | `<done>/<total>` from the same line |
| `outstanding_items` | same line |
| `board_root` | `buildkit-scheduler root` (must print `exists=True`) |
| `wp_open_here` | count of durable ops under `<root>/ops/<actor>/*.jsonl` where the last `allocate`/`claim` names this actor and last `transition.to_state` ∉ {done} |
| `prs_open` | `gh pr list --state open` |
| `develop_ahead_of_main` | `git rev-list --count origin/main..origin/develop` |
| `blocks_open` | count of engineer-ruling blocks in that repo's `docs/current_plan.md` |

## Table B — roadmap: every epic and feature NOT closed

🔴 **Fold the signed export's `heads` list. Do NOT use `buildkit-roadmap status`** — status is
blind to epic-less features and under-reports (it showed 99 of 115 when the true figure was
different).

```
buildkit-roadmap export
# then fold: heads[] where entity_kind == 'feature' and state != 'closed'
```

Columns, in this order: `# | state | epic | feature slot | spec_path`.
Sort by `state`, then `epic`, then `slot`. Report the state counts above the table.

## Table C — takt (per-phase and per-feature)

`buildkit-marathon takt --feature <f>` — report `n / p50 / p80 / max / band / verdict` per phase,
plus the feature total, plus **`measurable / total` steps and `sources: k/4`**.

🔴 **Unmeasurable steps must be stated as a count, never folded in as zero.**
🔴 **The only permissible durations are the generic takt range or a size-adjusted estimate
computed from ACTUAL measurements. An LLM estimate is never permitted.**

Bands: phase **0.5–3.0 h**; feature (era) **1.5–6.0 h**.
Sizes: `nano 1 · micro 3 · mini 7 · midi 11 · maxi 17 · saga 35`.

## Table D — what's next

`rank | step | size | state | blocked-by`. `state` ∈ {unblocked, held, gated}.
A `held`/`gated` row **must** name the block it waits on. Never list a blocked step as next
without naming its blocker.

---

## Known measurement traps (apply on every host)

1. `marathon resume`'s `next:` field can be **stale** — a `defer`ed item's steps are not removed
   from the `next` computation. Read the live ledger item, not `next`.
2. A **bare feature number is not an identifier** — `065` resolves to two spec dirs that answer
   the stage question differently. Key on `spec_path`.
3. Measure a feature's stage **on the ref that owns its spec dir**. `066`/`067` have no spec dir
   on `develop` or `main`.
4. Test ref containment against **branches AND tags** after `fetch --prune --tags`. A
   `refs/remotes`-only test yields false "uncontained".
5. Read the scheduler's **durable ops**, not `views/` — the allocate view contradicts the durable
   allocate ops and re-proposes from scratch each cycle.
6. Verify a lock-holder PID is **alive** (`Get-Process`, sampling CPU twice) before believing the
   "STUCK lock" message. Git-Bash `ps -p` cannot see native Windows PIDs.
