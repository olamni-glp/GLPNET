<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# HOST-INTERCONNECTIVITY-HARDENING — ROOTCAUSES (CRDT, multi-contributor)

**Feature:** `host-interconnectivity-hardening` · **Kind:** fleet coordination / infrastructure
**Status:** Draft (evidence-gathering) · **Origin:** shiras partial-board-absence 3rtask (`mrun-76da6e46bd44`, olamnit/glpnet)

> **CRDT merge rule (R1).** This is a grow-only, multi-writer document. Each contributor appends
> ONLY under their own `## contributor: <lane@host>` block (newest entry first within it) and
> may append rows to the shared **Consolidated rootcause register** table by adding NEW rows
> (never rewriting or deleting another actor's row). Conflicts are surfaced as a new
> `CONFLICT:` row, never resolved by overwrite. Union-by-(rootcause-id) on merge; a divergent
> byte-copy is quarantined, not silently merged. The engineer ratifies a rootcause by setting
> its `RATIFIED` column.

---

## Consolidated rootcause register (append rows; union-by-RC-id)

| RC-id | Rootcause (one line) | Evidence | Raised by | Corroborated by | RATIFIED |
|-------|----------------------|----------|-----------|-----------------|----------|
| RC-01 | **Onboarding is per-board and manual** — a host must run `buildkit-scheduler onboard` per board; no sweep onboards a host to its full board-membership set. | shiras in caps on 8/14 boards, absent from buildkit/lejepa/mstack/olamnit/tefl/yngenios | olamnit@glpnet | _(awaiting)_ | — |
| RC-02 | **Onboarding ≠ allocation (SCHED-R7 binding gap)** — no writer binds `ready` WPs to onboarded actors, so a steady feature stream never reaches a host even where present. | glpnet board 32 WPs, 0 allocated to olamnit despite onboarding; scheduler `cycle` emits `starved`/`stalled` but no bind-writer | olamnit@glpnet | _(awaiting)_ | — |
| RC-03 | **No host→board membership contract** — nothing declares which hosts SHOULD be on which boards, so absence is invisible (no gate fires on a missing expected member). | 6-board absence went unremarked until this census | olamnit@glpnet | _(awaiting)_ | — |
| RC-04 | **Arrival lag / no auto-onboard on host registration** — a new host (shiras, first glpnet ACK 2026-08-25) integrates board-by-board over days. | shiras first glpnet ACK today; Linux host crucible/glp/GLPNET | olamnit@glpnet | _(awaiting)_ | — |
| RC-05 (CANDIDATE) | **Mount/drive-mapping reach** — Linux hosts reach the volume as `/mnt/gavri/d`; a mapping gap may block reach to some boards. Needs shiras evidence (shared-dir + SSH). | shiras `root` cited `/mnt/gavri/d/coop/glpnet/sched` | olamnit@glpnet | _(needs shiras)_ | — |
| RC-06 | **"Absence" is a COLLECTION-METHOD artefact — the SMB share is a partial projection of the host.** `\\192.168.0.170\Shiras_Share` does NOT export `/mnt/biwin/D_DRIVE/BSTDEV` where the work lives; the fleet measured the projection, saw an empty machine, and concluded "unprovisioned." SHIRAS was never absent (onboarded 07:46Z, claimed a WP 08:40Z, buildkit + newer engine present). Supersedes the RC-05 reach hypothesis: reach was fine, the SHARE was thin. | 3rtask `20260828T012249Z-b414` curator §A/§B (CONFIRMed, slice-b/slice-c); 5 of 6 published "unmet prereqs" measured FALSE first-party | olamnit@glpnet | 3rtask 3-blind-builder disjoint-slice + codex critic | — |
| RC-07 | **Allocation/dispatch is computed on BOARD evidence while executability lives on the HOST, and nothing joins them** — a board marks a packet dispatchable to an actor with nowhere to run it, and no tool objects. The unified defect behind BOTH shiras "absence" and olamnit starvation; `onboard` self-reports, `allocate` is a separate verb → onboard≠allocate. Extends RC-02 (bind-writer gap) to the board↔host-executability join. | 3rtask curator §F (the one finding all 3 disjoint slices reached independently); 4 lanes allocated to shiras while warning of stranding | olamnit@glpnet | 3rtask (all 3 slices, corroborated by codex critic) | — |
| RC-08 | **"The board" is three divergent legs under one name** (ARIELLAS/GAVRI/OLAMNIT roots); the entire ready/claimed/in-progress lifecycle exists on only ONE root. Any absence/allocation claim is unsound unless it names the root — which is why the census disagreed. Strengthens RC-03. | 3rtask curator §D (slice-a: GAVRI leg wp=32 full lifecycle; ARIELLAS wp=3, OLAMNIT wp=6) | olamnit@glpnet | 3rtask | — |
| RC-09 | **Version skew can silently fork a board** — an older engine rewrites a UNC `--root` (Git-Bash) into a drive-letter path and creates a stray empty board root; a newer engine refuses the same input. Hosts on different pins writing one shared board can diverge unremarked. | 3rtask curator §H (slice-c C2.5: shiras 2026.8.24.5 / pin 2026.8.23.8 / ambient 2026.8.18.2) | olamnit@glpnet | 3rtask | — |
| RC-10 | **The capability vocabulary has no PLATFORM kind, no polarity, no retraction** — a host's platform declaration is smuggled into `kind=skill` and no matcher reads it; every caps record is `verified=true, evidence=null`; a false/stale declaration is permanent and unfalsifiable by its author. Blocks any executability-aware allocation (RC-07). | 3rtask curator §H (slice-a/slice-c: `skill linux-host`; ariellas `dart` declared but not installed); crucible ruling Q-041-01 exists for a typed platform cap | olamnit@glpnet | 3rtask | — |

> **Extrapolation to ALL host-host interconnectivity (engineer directive 2026-08-25):** RC-01…RC-04
> are not shiras-specific — they are the general failure modes of *any* host joining *any* board.
> The fix must be host-symmetric (works for ariellas↔gavriella↔olamnit↔shiras and future hosts),
> not a shiras patch.

---

## contributor: olamnit@glpnet
- **2026-08-28T02:30Z** — grounded the census via a disciplined `/bk-3rtask` (run `20260828T012249Z-b414`,
  research, 3 blind builders over pairwise-disjoint slices, 2 cycles, codex cross-provider Critic,
  0 ESCALATE; curator report at `.specify/3rtask/runs/20260828T012249Z-b414/curator.md`, gitignored).
  Added **RC-06…RC-10**. Two headline shifts from the 2026-08-25 census: (a) **RC-06 supersedes the
  RC-05 mount-reach hypothesis** — SHIRAS reach was fine (CIFS `/mnt/gavri/d` + local ext4); the SMB
  share was a *partial projection* hiding the working volume, so "absence" was in part a collection-method
  defect, and SHIRAS was never absent. (b) **RC-07 is the unified defect** all three disjoint slices
  reached independently: dispatch is computed on BOARD evidence while executability lives on the HOST —
  the same root behind both shiras "absence" and olamnit starvation, extending RC-02. The merge found
  0 mechanical corroboration / all singletons, which is CORRECT for genuinely content-disjoint corpora
  (the healthy inverse of the false-corroboration defect the failure-record slice itself warns about).
- **2026-08-25T11:39Z** — cycle-0 census + RC-01…RC-05 above. Primary-source: `I:/coop/*/sched/caps/`,
  `ls | grep -ci shiras` across I:/D:/H:, `buildkit-scheduler board`. Broadcast for cross-critique:
  `I:/coop/20260825T113917Z-olamnit-glpnet-BROADCAST-3RTASK-EVIDENCE-...ACK-REQUESTED.md`.
  RC-02 shares a rootcause with roadmap rank-13 `coordination-feature-stream-durable-superset-fix`
  and olamnit backlog P01 — treat as ONE superset fix.

<!-- append new contributor blocks below; do not edit blocks above yours -->
