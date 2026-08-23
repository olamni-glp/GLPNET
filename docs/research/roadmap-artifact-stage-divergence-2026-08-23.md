<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Measured defect: roadmap state diverges from artifact state

**Measured**: 2026-08-23, host `Ariellas`, lane `ariellas`.
**Marathon step**: T01 (`mstep-01a02e83-16cd-7011-86a2-a401c4ea199a`), run `mrun-f5ef56dba3c1`.
**Seed evidence**: 3rtask run `20260823T112021Z-6855` (method frozen after 4 blind codex review
rounds, 12→8→6→1 refutes; 3 blind builders; 236 claims; independence audit clean).

This step was framed as "record the divergence the 3rtask found". Re-measuring it directly
**confirmed the divergence and found two further defects the 3rtask did not report**, plus one
correction to how its table must be read.

## Method

Two independent measurements, neither derived from the other:

- **Roadmap side** — the signed export `ariellas__glpnet__20260823T123736Z.json`, folding the
  `heads` list (118 feature records). Read from the export, **not** from `roadmap status`,
  because status is blind to epic-less features.
- **Artifact side** — `git ls-tree` / `git show` against the ref that actually owns each spec
  dir. `067` and `066` have **no spec dir on `develop` or `main`**; their artifacts exist only
  on their own feature branches. A measurement taken on `develop` alone reports them absent.

## Result

| Roadmap slot | state | spec dir | plan/tasks | tasks done |
|---|---|---|---|---|
| `qr-link-provisioning` | **specified** | `specs/067-qr-link-provisioning` (branch only) | both present | 17/27 on `067`, **27/27 on `067b`** |
| `wave6-consolidation` | **specified** | `specs/066-wave6-consolidation` (branch only) | both present | **12/30** |
| `full-scope-gleam-glp-implementation` | **specified** | `specs/059-full-scope-gleam-glp-implementation` | both present | **75/98** |
| `wave-3-consolidated-full-gleam-chain` | **closed** | *same dir as the row above* | — | — |
| `ynet-consolidation` | **specified** | `specs/065-ynet-consolidation` | **neither** | no `tasks.md` |
| `glp-runtime-consol` | **closed** | *`spec_path` is EMPTY* | both present | 17/17 |

## Defect 1 — the roadmap understates the stage reached (CONFIRMED)

`067`, `066` and `059` are recorded `specified` while each carries a full artifact set and
partially-completed `tasks.md`. The roadmap is wrong by up to five stages, exactly as the 3rtask
reported. Any plan aimed at "specified → done" for these three is aimed at the wrong stage.

## Defect 2 — one spec dir carries TWO roadmap rows in contradictory states (NEW)

`specs/059-full-scope-gleam-glp-implementation` is the `spec_path` of **both**
`full-scope-gleam-glp-implementation` (**specified**) and `wave-3-consolidated-full-gleam-chain`
(**closed**). "The roadmap says 059 is specified" and "the roadmap says 059 is closed" are
**both true**, depending which row is read. This is the mechanism behind the session-3
footnote that 059's close is recorded under another feature id. No reconcile pass can resolve
this, because neither row is wrong on its own terms.

## Defect 3 — a `closed` row with an empty `spec_path` (NEW)

`glp-runtime-consol` is **closed** with `spec_path` empty, while `specs/065-glp-runtime-consol`
exists with a complete artifact set at 17/17. Same family as the recorded block on
roadmap↔spec linkage being unrepairable for slug-mismatched features: `link --auto` matches
only *promoted* features, `reconcile` reports in-sync, and `link` has no manual mode — so this
row can never acquire its spec path.

## Correction — the feature number `065` is ambiguous

Two spec dirs share it, and they answer the stage question differently:

- `specs/065-glp-runtime-consol` — full artifact set, **17/17** tasks.
- `specs/065-ynet-consolidation` — `spec.md` + `checklists` only, **no plan, no tasks**.

The 3rtask row "065: plan absent, tasks absent" is **correct for `065-ynet-consolidation`** and
wrong for the other. Resolving the bare number `065` to the first glob match reverses the
finding. **Any measurement keyed on a bare feature number is unsound in this repo.** The
roadmap side is right about `ynet-consolidation`: `specified` with no plan and no tasks is
an accurate record.

## What this does and does not change

It does **not** disturb the session-3 root cause. `MISSING_REVIEW_GATE` at `codexreview` stands:
these features hold at `implement` and the gate is what they have not passed. Defects 2 and 3
are *recording* faults that make the stall harder to see; they are not the stall.

It does change the measurement rule: **measure a feature's stage on the ref that owns its spec
dir, and key on the spec path, never on the feature number.**
